using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FanControl.NPB5ITE.Logging;

#if USE_LIBREHARDWAREMONITOR
using LibreHardwareMonitor.Hardware;
#endif

namespace FanControl.NPB5ITE.HwInfo
{
    public sealed class LibreHardwareMonitorFanRpmSource : IFanRpmSource, IDisposable
    {
        private const string SourceName = "LibreHardwareMonitor";

        private readonly PluginLog _log;
        private bool _inventoryLogged;

#if USE_LIBREHARDWAREMONITOR
        private Computer? _computer;
#endif

        public LibreHardwareMonitorFanRpmSource(PluginLog log)
        {
            _log = log;
        }

        public FanRpmReading ReadCpuFanRpm()
        {
#if USE_LIBREHARDWAREMONITOR
            try
            {
                var computer = EnsureOpen();
                UpdateHardware(computer.Hardware);

                var candidates = FindFanSensors(computer.Hardware)
                    .Where(candidate => candidate.Rpm > 0)
                    .OrderByDescending(candidate => candidate.Score)
                    .ToArray();

                if (candidates.Length == 0)
                {
                    LogInventory(computer.Hardware);
                    return FanRpmReading.Unavailable(SourceName, "No positive motherboard fan RPM sensor was reported.");
                }

                var best = candidates[0];
                return FanRpmReading.Success(best.Rpm, SourceName + " " + best.DisplayName);
            }
            catch (Exception exception)
            {
                _log.Error("Failed to read CPU fan RPM through LibreHardwareMonitor.", exception);
                return FanRpmReading.Unavailable(SourceName, exception.Message);
            }
#else
            return FanRpmReading.Unavailable(SourceName, "LibreHardwareMonitorLib.dll was not available at build time.");
#endif
        }

        public void Dispose()
        {
#if USE_LIBREHARDWAREMONITOR
            _computer?.Close();
            _computer = null;
#endif
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
                IsMotherboardEnabled = true
            };

            _computer.Open();
            _log.Info("LibreHardwareMonitor motherboard reader opened.");

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

        private static IEnumerable<FanSensorCandidate> FindFanSensors(IEnumerable<IHardware> hardwareItems)
        {
            foreach (var hardware in hardwareItems)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Fan || sensor.Value == null)
                    {
                        continue;
                    }

                    yield return new FanSensorCandidate(
                        sensor.Value.Value,
                        hardware.Name + " / " + sensor.Name,
                        Score(hardware, sensor));
                }

                foreach (var candidate in FindFanSensors(hardware.SubHardware))
                {
                    yield return candidate;
                }
            }
        }

        private void LogInventory(IEnumerable<IHardware> hardwareItems)
        {
            if (_inventoryLogged)
            {
                return;
            }

            _inventoryLogged = true;
            var hardwareNames = FlattenHardware(hardwareItems)
                .Select(hardware => hardware.HardwareType + ":" + hardware.Name)
                .ToArray();

            _log.Warning("LibreHardwareMonitor found no positive fan RPM. Hardware inventory: " + string.Join("; ", hardwareNames));
        }

        private static IEnumerable<IHardware> FlattenHardware(IEnumerable<IHardware> hardwareItems)
        {
            foreach (var hardware in hardwareItems)
            {
                yield return hardware;

                foreach (var child in FlattenHardware(hardware.SubHardware))
                {
                    yield return child;
                }
            }
        }

        private static int Score(IHardware hardware, ISensor sensor)
        {
            var text = (hardware.Name + " " + hardware.Identifier + " " + sensor.Name + " " + sensor.Identifier).ToUpperInvariant();
            var score = 0;

            if (text.Contains("CPU"))
            {
                score += 8;
            }

            if (text.Contains("FAN"))
            {
                score += 4;
            }

            if (text.Contains("ITE") || text.Contains("IT8613"))
            {
                score += 3;
            }

            if (text.Contains("GPU") || text.Contains("NVIDIA") || text.Contains("AMD") || text.Contains("RADEON") || text.Contains("GEFORCE"))
            {
                score -= 10;
            }

            return score;
        }

        private sealed class FanSensorCandidate
        {
            public FanSensorCandidate(float rpm, string displayName, int score)
            {
                Rpm = rpm;
                DisplayName = displayName + " (" + rpm.ToString("0", CultureInfo.InvariantCulture) + " RPM)";
                Score = score;
            }

            public float Rpm { get; }

            public string DisplayName { get; }

            public int Score { get; }
        }
#endif
    }
}
