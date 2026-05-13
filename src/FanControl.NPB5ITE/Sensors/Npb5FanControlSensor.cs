using System;
using System.Threading;
using System.Threading.Tasks;
using FanControl.NPB5ITE.Hardware;
using FanControl.NPB5ITE.Logging;
using FanControl.NPB5ITE.Safety;
using FanControl.NPB5ITE.Temperature;
using FanControl.Plugins;

namespace FanControl.NPB5ITE.Sensors
{
    public sealed class Npb5FanControlSensor : IPluginControlSensor, IDisposable
    {
        private static readonly TimeSpan ApplyInterval = TimeSpan.FromMilliseconds(500);
        private const float SignificantChangePercent = 1.0f;

        private readonly IIte8613fIo _hardware;
        private readonly Npb5FanRpmSensor _rpmSensor;
        private readonly ICpuTemperatureSource _temperatureSource;
        private readonly FanSafetyPolicy _safetyPolicy;
        private readonly PluginOptions _options;
        private readonly PluginLog _log;
        private readonly object _sync = new object();
        private readonly AutoResetEvent _workerSignal = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _restoreApplied = new ManualResetEventSlim(true);
        private readonly CancellationTokenSource _workerCancellation = new CancellationTokenSource();
        private readonly Task _workerTask;
        private float? _pendingValue;
        private float? _lastRequestedValue;
        private DateTime _nextApplyUtc = DateTime.MinValue;
        private bool _disposed;

        public Npb5FanControlSensor(
            IIte8613fIo hardware,
            Npb5FanRpmSensor rpmSensor,
            ICpuTemperatureSource temperatureSource,
            FanSafetyPolicy safetyPolicy,
            PluginOptions options,
            PluginLog log)
        {
            _hardware = hardware;
            _rpmSensor = rpmSensor;
            _temperatureSource = temperatureSource;
            _safetyPolicy = safetyPolicy;
            _options = options;
            _log = log;
            _workerTask = Task.Factory.StartNew(
                RunWorker,
                _workerCancellation.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public string Id => "NPB5_ITE_CPU_FAN_PWM";

        public string Name => "NPB5 CPU Fan Control";

        public string Origin => "NPB5 ITE IT8613F";

        public float? Value { get; private set; }

        public void Set(float value)
        {
            if (_disposed)
            {
                return;
            }

            lock (_sync)
            {
                _pendingValue = value;
            }

            _workerSignal.Set();
        }

        public void Reset()
        {
            QueueRestoreAutomaticControl();
        }

        public void Update()
        {
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            QueueRestoreAutomaticControl();
            if (!_restoreApplied.Wait(TimeSpan.FromSeconds(2)))
            {
                _log.Warning("Automatic fan control restore did not complete within the close timeout.");
            }

            _workerCancellation.Cancel();
            _workerSignal.Set();

            try
            {
                if (!_workerTask.Wait(TimeSpan.FromSeconds(2)))
                {
                    _log.Warning("Fan control worker did not stop within the close timeout.");
                }
            }
            catch (AggregateException exception)
            {
                _log.Error("Fan control worker stopped with an error.", exception.Flatten());
            }
            finally
            {
                _workerCancellation.Dispose();
                _restoreApplied.Dispose();
                _workerSignal.Dispose();
            }
        }

        private void QueueRestoreAutomaticControl()
        {
            lock (_sync)
            {
                _pendingValue = 0.0f;
                _lastRequestedValue = null;
                _nextApplyUtc = DateTime.MinValue;
                _restoreApplied.Reset();
            }

            Value = null;
            _workerSignal.Set();
        }

        private void RunWorker()
        {
            var cancellationToken = _workerCancellation.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var requestedValue = TakePendingValueForApply(out var waitTime);
                    if (requestedValue.HasValue)
                    {
                        ApplyRequestedValue(requestedValue.Value);
                        continue;
                    }

                    _workerSignal.WaitOne(waitTime);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _log.Error("Fan control worker failed.", exception);
                    TryRestoreAutomaticControlAfterFailure();
                    Thread.Sleep(ApplyInterval);
                }
            }
        }

        private float? TakePendingValueForApply(out TimeSpan waitTime)
        {
            lock (_sync)
            {
                if (!_pendingValue.HasValue)
                {
                    waitTime = Timeout.InfiniteTimeSpan;
                    return null;
                }

                var now = DateTime.UtcNow;
                var requestedValue = _pendingValue.Value;
                var isReleaseRequest = IsReleaseRequest(requestedValue);

                if (now < _nextApplyUtc && !isReleaseRequest)
                {
                    waitTime = _nextApplyUtc - now;
                    return null;
                }

                if (_lastRequestedValue.HasValue &&
                    !isReleaseRequest &&
                    Math.Abs(requestedValue - _lastRequestedValue.Value) < SignificantChangePercent)
                {
                    waitTime = Timeout.InfiniteTimeSpan;
                    return null;
                }

                _pendingValue = null;
                if (isReleaseRequest)
                {
                    _lastRequestedValue = null;
                    _nextApplyUtc = DateTime.MinValue;
                }
                else
                {
                    _lastRequestedValue = requestedValue;
                    _nextApplyUtc = now + ApplyInterval;
                }

                waitTime = TimeSpan.Zero;
                return requestedValue;
            }
        }

        private void ApplyRequestedValue(float value)
        {
            var inputs = new FanSafetyInputs
            {
                RequestedPwmPercent = value,
                CpuTemperatureCelsius = _temperatureSource.ReadCpuTemperatureCelsius(),
                FanRpmReadSucceeded = _rpmSensor.LastReadSucceeded,
                HardwareWritesEnabled = _options.EnableHardwareWrites
            };

            var decision = _safetyPolicy.Evaluate(inputs);
            Value = decision.PwmPercent;
            _log.Info("Fan control request " + value + "% -> " + decision.Action + ". " + decision.Reason);

            try
            {
                ApplyDecision(decision);
                if (IsReleaseRequest(value))
                {
                    _restoreApplied.Set();
                }
            }
            catch (Exception exception)
            {
                _log.Error("Failed to apply fan control decision: " + decision.Reason, exception);
                TryRestoreAutomaticControlAfterFailure();
                ClearLastRequestedValue();
                if (IsReleaseRequest(value))
                {
                    _restoreApplied.Set();
                }

                Value = null;
            }
        }

        private static bool IsReleaseRequest(float value)
        {
            return float.IsNaN(value) || value <= 0.0f;
        }

        private void ApplyDecision(FanSafetyDecision decision)
        {
            switch (decision.Action)
            {
                case FanSafetyAction.RestoreAutomaticControl:
                    _hardware.RestoreAutomaticControl();
                    break;
                case FanSafetyAction.ApplyManualPwm:
                    if (decision.PwmPercent == null)
                    {
                        throw new InvalidOperationException("Manual PWM decision did not include a PWM value.");
                    }

                    EnsurePwmCapability();
                    _hardware.ApplyManualPwm(decision.PwmPercent.Value);
                    break;
                case FanSafetyAction.ApplyFullSpeed:
                    EnsurePwmCapability();
                    _hardware.ApplyFullSpeed();
                    break;
                default:
                    throw new InvalidOperationException("Unknown fan safety action: " + decision.Action + ".");
            }
        }

        private void EnsurePwmCapability()
        {
            var capability = _hardware.GetPwmControlCapability();
            if (!capability.CanApplyManualPwm)
            {
                _log.Warning(capability.Summary);
            }
        }

        private void TryRestoreAutomaticControlAfterFailure()
        {
            try
            {
                _hardware.RestoreAutomaticControl();
            }
            catch (Exception restoreException)
            {
                _log.Error("Failed to restore automatic fan control after control failure.", restoreException);
            }
        }

        private void ClearLastRequestedValue()
        {
            lock (_sync)
            {
                _lastRequestedValue = null;
                _nextApplyUtc = DateTime.MinValue;
            }
        }
    }
}
