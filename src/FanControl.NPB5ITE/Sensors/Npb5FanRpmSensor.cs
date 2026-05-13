using System;
using FanControl.NPB5ITE.HwInfo;
using FanControl.NPB5ITE.Logging;
using FanControl.Plugins;

namespace FanControl.NPB5ITE.Sensors
{
    public sealed class Npb5FanRpmSensor : IPluginSensor, IDisposable
    {
        public const string SensorId = "NPB5_ITE_CPU_FAN_RPM";

        private readonly IFanRpmSource _rpmSource;
        private readonly PluginLog _log;
        private FanRpmReading _lastReading = FanRpmReading.Unavailable("Startup", "No update has run yet.");
        private string _lastLoggedState = string.Empty;

        public Npb5FanRpmSensor(IFanRpmSource rpmSource, PluginLog log)
        {
            _rpmSource = rpmSource;
            _log = log;
        }

        public string Id => SensorId;

        public string Name => "NPB5 CPU Fan";

        public string Origin => "NPB5 ITE IT8613F";

        public float? Value { get; private set; }

        public bool LastReadSucceeded => _lastReading.Succeeded;

        public string LastFailureReason => _lastReading.Message;

        public void Update()
        {
            try
            {
                _lastReading = _rpmSource.ReadCpuFanRpm();
                Value = _lastReading.Rpm;
                LogReadingState(_lastReading);
            }
            catch (Exception exception)
            {
                _log.Error("CPU fan RPM update failed.", exception);
                _lastReading = FanRpmReading.Unavailable("Sensor", exception.Message);
                Value = null;
                LogReadingState(_lastReading);
            }
        }

        private void LogReadingState(FanRpmReading reading)
        {
            var state = reading.Succeeded
                ? "OK:" + reading.Rpm.GetValueOrDefault().ToString("0")
                : "FAIL:" + reading.Source + ":" + reading.Message;

            if (string.Equals(state, _lastLoggedState, StringComparison.Ordinal))
            {
                return;
            }

            _lastLoggedState = state;

            if (reading.Succeeded)
            {
                _log.Info("CPU fan RPM read from " + reading.Source + ": " + reading.Rpm.GetValueOrDefault().ToString("0") + " RPM.");
            }
            else
            {
                _log.Warning("CPU fan RPM unavailable from " + reading.Source + ": " + reading.Message);
            }
        }

        public void Dispose()
        {
            if (_rpmSource is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
