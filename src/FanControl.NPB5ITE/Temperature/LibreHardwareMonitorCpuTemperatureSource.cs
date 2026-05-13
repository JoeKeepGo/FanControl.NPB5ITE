using System;
using System.Collections.Generic;
using System.Linq;
using FanControl.NPB5ITE.Logging;

#if USE_LIBREHARDWAREMONITOR
using LibreHardwareMonitor.Hardware;
#endif

namespace FanControl.NPB5ITE.Temperature
{
    public sealed class LibreHardwareMonitorCpuTemperatureSource : ICpuTemperatureSource, IDisposable
    {
        private const string SourceName = "LibreHardwareMonitor CPU";

        private readonly PluginLog _log;
        private bool _unavailableLogged;

#if USE_LIBREHARDWAREMONITOR
        private Computer? _computer;
#endif

        public LibreHardwareMonitorCpuTemperatureSource(PluginLog log)
        {
            _log = log;
        }

        public float? ReadCpuTemperatureCelsius()
        {
#if USE_LIBREHARDWAREMONITOR
            try
            {
                var computer = EnsureOpen();
                UpdateHardware(computer.Hardware);

                var candidates = FindCpuTemperatureSensors(computer.Hardware)
                    .Where(candidate => candidate.TemperatureCelsius > 0)
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenByDescending(candidate => candidate.TemperatureCelsius)
                    .ToArray();

                if (candidates.Length == 0)
                {
                    LogUnavailable("No positive CPU temperature sensor was reported.");
                    return null;
                }

                return candidates[0].TemperatureCelsius;
            }
            catch (Exception exception)
            {
                _log.Error("Failed to read CPU temperature through LibreHardwareMonitor.", exception);
                return null;
            }
#else
            LogUnavailable("LibreHardwareMonitorLib.dll was not available at build time.");
            return null;
#endif
        }

        public void Dispose()
        {
#if USE_LIBREHARDWAREMONITOR
            _computer?.Close();
            _computer = null;
#endif
        }

        private void LogUnavailable(string message)
        {
            if (_unavailableLogged)
            {
                return;
            }

            _unavailableLogged = true;
            _log.Warning(SourceName + " unavailable: " + message);
        }

#if USE_LIBREHARDWAREMONITOR
        private Computer EnsureOpen()
        {
            if (_computer != null)
            {
                return _computer;
            }

            _computer = new Computer
            {
                IsCpuEnabled = true
            };

            _computer.Open();
            _log.Info("LibreHardwareMonitor CPU temperature reader opened.");

            return _computer;
        }

        private static void UpdateHardware(IEnumerable<IHardware> hardwareItems)
        {
            foreach (var hardware in hardwareItems)
            {
                hardware.Update();
                UpdateHardware(hardware.SubHardware);
            }
        }

        private static IEnumerable<CpuTemperatureCandidate> FindCpuTemperatureSensors(IEnumerable<IHardware> hardwareItems)
        {
            foreach (var hardware in hardwareItems)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature || sensor.Value == null)
                    {
                        continue;
                    }

                    var score = Score(hardware, sensor);
                    if (score <= 0)
                    {
                        continue;
                    }

                    yield return new CpuTemperatureCandidate(sensor.Value.Value, score);
                }

                foreach (var candidate in FindCpuTemperatureSensors(hardware.SubHardware))
                {
                    yield return candidate;
                }
            }
        }

        private static int Score(IHardware hardware, ISensor sensor)
        {
            var text = (hardware.Name + " " + hardware.Identifier + " " + sensor.Name + " " + sensor.Identifier).ToUpperInvariant();
            var score = hardware.HardwareType == HardwareType.Cpu ? 10 : 0;

            if (text.Contains("CPU PACKAGE"))
            {
                score += 12;
            }

            if (text.Contains("CORE MAX"))
            {
                score += 10;
            }

            if (text.Contains("CPU"))
            {
                score += 4;
            }

            if (text.Contains("DISTANCE"))
            {
                score -= 20;
            }

            return score;
        }

        private sealed class CpuTemperatureCandidate
        {
            public CpuTemperatureCandidate(float temperatureCelsius, int score)
            {
                TemperatureCelsius = temperatureCelsius;
                Score = score;
            }

            public float TemperatureCelsius { get; }

            public int Score { get; }
        }
#endif
    }
}
