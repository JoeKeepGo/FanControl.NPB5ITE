using System;
using FanControl.NPB5ITE.Hardware;
using FanControl.NPB5ITE.HwInfo;
using FanControl.NPB5ITE.Logging;
using FanControl.NPB5ITE.Safety;
using FanControl.NPB5ITE.Sensors;
using FanControl.NPB5ITE.Temperature;
using FanControl.Plugins;

namespace FanControl.NPB5ITE
{
    public sealed class Plugin : IPlugin2, IDisposable
    {
        private readonly PluginLog _log;

        private PluginOptions? _options;
        private Ite8613fIo? _hardware;
        private Npb5FanRpmSensor? _rpmSensor;
        private Npb5FanControlSensor? _controlSensor;
        private ICpuTemperatureSource? _temperatureSource;
        private bool _disposed;

        public Plugin()
            : this(new PluginLog())
        {
        }

        public Plugin(IPluginLogger logger)
            : this(new PluginLog(logger.Log))
        {
        }

        private Plugin(PluginLog log)
        {
            _log = log;
        }

        public string Name => "NPB5 ITE IT8613F";

        public void Initialize()
        {
            _options = PluginOptions.FromEnvironment();
            _hardware = new Ite8613fIo(new LibreHardwareMonitorLpcIoPort(), _options, _log);
            _log.Info("Plugin initialized. Hardware writes enabled: " + _options.EnableHardwareWrites + ". Tested hardware defaults: " + _options.UsesTestedHardwareDefaults + ". Minimum PWM: " + _options.MinimumPwmPercent + "%. Low PWM enabled: " + _options.AllowLowPwm + ".");
            _log.Info("Detected hardware: " + _options.HardwareIdentity.Summary + ".");
            _log.Warning(_hardware.GetPwmControlCapability().Summary);
        }

        public void Load(IPluginSensorsContainer container)
        {
            EnsureInitialized();

            var rpmSource = new CompositeFanRpmSource(
                new IFanRpmSource[]
                {
                    new BorrowedFanRpmSource(_hardware!),
                    new LibreHardwareMonitorFanRpmSource(_log),
                    new HwInfoFanRpmSource(_log)
                });

            _rpmSensor = new Npb5FanRpmSensor(rpmSource, _log);

            _rpmSensor.Update();
            container.FanSensors.Add(_rpmSensor);
            _log.Info("Control sensor is exposed for Fan Control compatibility. " + _hardware!.GetPwmControlCapability().Summary);

            var safetyPolicy = new FanSafetyPolicy(new FanSafetyOptions
            {
                MinimumPwmPercent = _options!.MinimumPwmPercent,
                CriticalCpuTemperatureCelsius = _options.CriticalCpuTemperatureCelsius,
                AllowLowPwm = _options.AllowLowPwm,
                AllowManualWithoutCpuTemperature = _options.AllowManualWithoutCpuTemperature
            });

            _controlSensor = new Npb5FanControlSensor(
                _hardware!,
                _rpmSensor,
                _temperatureSource = new LibreHardwareMonitorCpuTemperatureSource(_log),
                safetyPolicy,
                _options,
                _log);

            container.ControlSensors.Add(_controlSensor);
        }

        public void Update()
        {
            _rpmSensor?.Update();
            _controlSensor?.Update();
        }

        public void Close()
        {
            try
            {
                _controlSensor?.Dispose();
                _controlSensor = null;
            }
            catch (Exception exception)
            {
                _log.Error("Failed while restoring automatic fan control during plugin close.", exception);
            }
            finally
            {
                try
                {
                    _hardware?.RestoreAutomaticControl();
                }
                catch (Exception exception)
                {
                    _log.Error("Failed while restoring automatic fan control from hardware layer during plugin close.", exception);
                }

                _hardware?.Dispose();
                _rpmSensor?.Dispose();
                if (_temperatureSource is IDisposable disposableTemperatureSource)
                {
                    disposableTemperatureSource.Dispose();
                }

                _hardware = null;
                _rpmSensor = null;
                _temperatureSource = null;
                _options = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Close();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void EnsureInitialized()
        {
            if (_hardware == null || _options == null)
            {
                Initialize();
            }
        }
    }
}
