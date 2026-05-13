using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using FanControl.NPB5ITE;
using FanControl.NPB5ITE.Hardware;
using FanControl.NPB5ITE.HwInfo;
using FanControl.NPB5ITE.Logging;
using LibreHardwareMonitor.Hardware;

namespace FanControl.NPB5ITE.Diagnostics
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var options = DiagnosticOptions.Parse(args);
            var outputDirectory = options.OutputDirectory;

            Directory.CreateDirectory(outputDirectory);

            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };

            try
            {
                computer.Open();
                UpdateHardware(computer.Hardware);

                var snapshot = HardwareSnapshot.From(computer.Hardware);
                var report = HardwareReport.From(computer.Hardware);
                var hwInfoSnapshot = HwInfoVsbSnapshot.Capture();
                var isElevated = IsProcessElevated();
                var pwmCapability = CreatePwmCapability(isElevated);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                var label = options.SafeLabel;
                var sioRegisterDump = CreateReadOnlyRegisterDump(label);
                sioRegisterDump.AddNote("Process elevated: " + isElevated + ".");
                var directFanReading = ReadDirectCpuFanRpm();
                sioRegisterDump.AddNote(directFanReading.Succeeded
                    ? "Direct IT8613E CPU fan RPM: " + directFanReading.Rpm.GetValueOrDefault().ToString("0", CultureInfo.InvariantCulture) + "."
                    : "Direct IT8613E CPU fan RPM unavailable: " + directFanReading.Message);
                var textPath = Path.Combine(outputDirectory, "lhm-sensors-" + label + "-" + timestamp + ".txt");
                var jsonPath = Path.Combine(outputDirectory, "lhm-sensors-" + label + "-" + timestamp + ".json");
                var reportPath = Path.Combine(outputDirectory, "lhm-report-" + label + "-" + timestamp + ".txt");
                var hwInfoTextPath = Path.Combine(outputDirectory, "hwinfo-vsb-" + label + "-" + timestamp + ".txt");
                var hwInfoJsonPath = Path.Combine(outputDirectory, "hwinfo-vsb-" + label + "-" + timestamp + ".json");
                var pwmCapabilityTextPath = Path.Combine(outputDirectory, "pwm-capability-" + label + "-" + timestamp + ".txt");
                var pwmCapabilityJsonPath = Path.Combine(outputDirectory, "pwm-capability-" + label + "-" + timestamp + ".json");
                var sioRegisterTextPath = Path.Combine(outputDirectory, "sio-registers-" + label + "-" + timestamp + ".txt");
                var sioRegisterJsonPath = Path.Combine(outputDirectory, "sio-registers-" + label + "-" + timestamp + ".json");

                File.WriteAllText(textPath, snapshot.ToText());
                File.WriteAllText(jsonPath, snapshot.ToJson());
                File.WriteAllText(reportPath, report.ToText());
                File.WriteAllText(hwInfoTextPath, hwInfoSnapshot.ToText());
                File.WriteAllText(hwInfoJsonPath, hwInfoSnapshot.ToJson());
                File.WriteAllText(pwmCapabilityTextPath, pwmCapability.ToText());
                File.WriteAllText(pwmCapabilityJsonPath, pwmCapability.ToJson());
                File.WriteAllText(sioRegisterTextPath, sioRegisterDump.ToText());
                File.WriteAllText(sioRegisterJsonPath, sioRegisterDump.ToJson());

                Console.WriteLine("Wrote " + textPath);
                Console.WriteLine("Wrote " + jsonPath);
                Console.WriteLine("Wrote " + reportPath);
                Console.WriteLine("Wrote " + hwInfoTextPath);
                Console.WriteLine("Wrote " + hwInfoJsonPath);
                Console.WriteLine("Wrote " + pwmCapabilityTextPath);
                Console.WriteLine("Wrote " + pwmCapabilityJsonPath);
                Console.WriteLine("Wrote " + sioRegisterTextPath);
                Console.WriteLine("Wrote " + sioRegisterJsonPath);
                Console.WriteLine("Process elevated: " + isElevated);
                Console.WriteLine(pwmCapability.Summary);

                Console.WriteLine(directFanReading.Succeeded
                    ? "Direct IT8613E CPU fan RPM: " + directFanReading.Rpm.GetValueOrDefault().ToString("0", CultureInfo.InvariantCulture)
                    : "Direct IT8613E CPU fan RPM unavailable: " + directFanReading.Message);

                var fanReading = hwInfoSnapshot.FindBestCpuFanRpm();
                Console.WriteLine(fanReading.Succeeded
                    ? "HWiNFO CPU fan RPM: " + fanReading.Rpm.GetValueOrDefault().ToString("0", CultureInfo.InvariantCulture)
                    : "HWiNFO CPU fan RPM unavailable: " + fanReading.Message);

                if (options.SetPwmPercent.HasValue)
                {
                    RunExperimentalPwmProbe(options);
                }
            }
            finally
            {
                computer.Close();
            }

            return 0;
        }

        private static FanRpmReading ReadDirectCpuFanRpm()
        {
            using (var hardware = new Ite8613fIo(new LibreHardwareMonitorLpcIoPort(), PluginOptions.FromEnvironment(), new PluginLog()))
            {
                return hardware.ReadCpuFanRpm();
            }
        }

        private static PwmControlCapability CreatePwmCapability(bool isProcessElevated)
        {
            using (var hardware = new Ite8613fIo(new LibreHardwareMonitorLpcIoPort(), PluginOptions.FromEnvironment(), new PluginLog()))
            {
                return hardware.GetPwmControlCapability(isProcessElevated);
            }
        }

        private static RegisterDump CreateReadOnlyRegisterDump(string label)
        {
            using (var hardware = new Ite8613fIo(new LibreHardwareMonitorLpcIoPort(), PluginOptions.FromEnvironment(), new PluginLog()))
            {
                return hardware.CaptureReadOnlyDump(label);
            }
        }

        private static void RunExperimentalPwmProbe(DiagnosticOptions options)
        {
            if (!options.SetPwmPercent.HasValue)
            {
                return;
            }

            var percent = options.SetPwmPercent.Value;
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            Console.WriteLine("Experimental PWM probe requested: " + percent.ToString("0.##", CultureInfo.InvariantCulture) + "%.");
            Console.WriteLine("Writes require FANCONTROL_NPB5ITE_ENABLE_WRITES=1 and FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS=1.");

            using (var hardware = new Ite8613fIo(new LibreHardwareMonitorLpcIoPort(), PluginOptions.FromEnvironment(), new PluginLog()))
            {
                hardware.ApplyManualPwm(percent);
                WriteProbeSnapshot(options, "after-set", timestamp);

                Console.WriteLine("Applied experimental PWM. Waiting " + options.RestoreAfterSeconds.ToString(CultureInfo.InvariantCulture) + " seconds before restore.");
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(options.RestoreAfterSeconds));

                var afterSnapshot = HwInfoVsbSnapshot.Capture();
                var afterReading = afterSnapshot.FindBestCpuFanRpm();
                var afterDirectReading = hardware.ReadCpuFanRpm();
                Console.WriteLine(afterDirectReading.Succeeded
                    ? "Direct IT8613E CPU fan RPM after experimental PWM: " + afterDirectReading.Rpm.GetValueOrDefault().ToString("0", CultureInfo.InvariantCulture)
                    : "Direct IT8613E CPU fan RPM after experimental PWM unavailable: " + afterDirectReading.Message);
                Console.WriteLine(afterReading.Succeeded
                    ? "HWiNFO CPU fan RPM after experimental PWM: " + afterReading.Rpm.GetValueOrDefault().ToString("0", CultureInfo.InvariantCulture)
                    : "HWiNFO CPU fan RPM after experimental PWM unavailable: " + afterReading.Message);

                hardware.RestoreAutomaticControl();
                WriteProbeSnapshot(options, "after-restore", timestamp);
                Console.WriteLine("Restored previous IT8613E fan control register values.");
            }
        }

        private static void WriteProbeSnapshot(DiagnosticOptions options, string phase, string timestamp)
        {
            var dump = CreateReadOnlyRegisterDump(options.SafeLabel + "-" + phase);
            var hwInfo = HwInfoVsbSnapshot.Capture();
            var baseName = options.SafeLabel + "-" + phase + "-" + timestamp;
            var registerTextPath = Path.Combine(options.OutputDirectory, "sio-registers-" + baseName + ".txt");
            var registerJsonPath = Path.Combine(options.OutputDirectory, "sio-registers-" + baseName + ".json");
            var hwInfoTextPath = Path.Combine(options.OutputDirectory, "hwinfo-vsb-" + baseName + ".txt");
            var hwInfoJsonPath = Path.Combine(options.OutputDirectory, "hwinfo-vsb-" + baseName + ".json");

            File.WriteAllText(registerTextPath, dump.ToText());
            File.WriteAllText(registerJsonPath, dump.ToJson());
            File.WriteAllText(hwInfoTextPath, hwInfo.ToText());
            File.WriteAllText(hwInfoJsonPath, hwInfo.ToJson());

            Console.WriteLine("Wrote " + registerTextPath);
            Console.WriteLine("Wrote " + registerJsonPath);
            Console.WriteLine("Wrote " + hwInfoTextPath);
            Console.WriteLine("Wrote " + hwInfoJsonPath);
        }

        private static bool IsProcessElevated()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static void UpdateHardware(IEnumerable<IHardware> hardwareItems)
        {
            foreach (var hardware in hardwareItems)
            {
                hardware.Update();
                UpdateHardware(hardware.SubHardware);
            }
        }
    }

    internal sealed class DiagnosticOptions
    {
        private DiagnosticOptions(string outputDirectory, string label, float? setPwmPercent, int restoreAfterSeconds)
        {
            OutputDirectory = Path.GetFullPath(outputDirectory);
            Label = label;
            SetPwmPercent = setPwmPercent;
            RestoreAfterSeconds = restoreAfterSeconds;
        }

        public string OutputDirectory { get; }

        public string Label { get; }

        public string SafeLabel => Sanitize(Label);

        public float? SetPwmPercent { get; }

        public int RestoreAfterSeconds { get; }

        public static DiagnosticOptions Parse(string[] args)
        {
            var outputDirectory = Path.Combine(Environment.CurrentDirectory, "diagnostics");
            var label = "snapshot";
            float? setPwmPercent = null;
            var restoreAfterSeconds = 8;

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];

                if (string.Equals(arg, "--label", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    label = args[++index];
                    continue;
                }

                if (string.Equals(arg, "--set-pwm", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    setPwmPercent = float.Parse(args[++index], CultureInfo.InvariantCulture);
                    continue;
                }

                if (string.Equals(arg, "--restore-after-seconds", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    restoreAfterSeconds = int.Parse(args[++index], CultureInfo.InvariantCulture);
                    continue;
                }

                if (!arg.StartsWith("-", StringComparison.Ordinal))
                {
                    outputDirectory = arg;
                }
            }

            return new DiagnosticOptions(outputDirectory, label, setPwmPercent, restoreAfterSeconds);
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder();

            foreach (var character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '-' || character == '_'
                    ? character
                    : '_');
            }

            return builder.Length == 0 ? "snapshot" : builder.ToString();
        }
    }

    internal sealed class HardwareReport
    {
        private readonly IReadOnlyList<HardwareReportNode> _reports;

        private HardwareReport(IReadOnlyList<HardwareReportNode> reports)
        {
            _reports = reports;
            CapturedAtUtc = DateTime.UtcNow;
        }

        public DateTime CapturedAtUtc { get; }

        public static HardwareReport From(IEnumerable<IHardware> hardwareItems)
        {
            return new HardwareReport(hardwareItems.Select(HardwareReportNode.From).ToArray());
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("CapturedAtUtc: " + CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture));

            foreach (var report in _reports)
            {
                report.AppendText(builder, 0);
            }

            return builder.ToString();
        }
    }

    internal sealed class HardwareReportNode
    {
        private HardwareReportNode(string name, string identifier, string type, string report, IReadOnlyList<HardwareReportNode> children)
        {
            Name = name;
            Identifier = identifier;
            Type = type;
            Report = report;
            Children = children;
        }

        private string Name { get; }

        private string Identifier { get; }

        private string Type { get; }

        private string Report { get; }

        private IReadOnlyList<HardwareReportNode> Children { get; }

        public static HardwareReportNode From(IHardware hardware)
        {
            return new HardwareReportNode(
                hardware.Name,
                hardware.Identifier.ToString(),
                hardware.HardwareType.ToString(),
                SafeGetReport(hardware),
                hardware.SubHardware.Select(From).ToArray());
        }

        public void AppendText(StringBuilder builder, int indent)
        {
            var prefix = new string(' ', indent);
            builder.AppendLine(prefix + Type + ": " + Name + " [" + Identifier + "]");

            if (!string.IsNullOrWhiteSpace(Report))
            {
                builder.AppendLine(Report.TrimEnd());
            }

            foreach (var child in Children)
            {
                child.AppendText(builder, indent + 2);
            }
        }

        private static string SafeGetReport(IHardware hardware)
        {
            try
            {
                return hardware.GetReport() ?? string.Empty;
            }
            catch (Exception exception)
            {
                return "Failed to get report: " + exception.Message;
            }
        }
    }

    internal sealed class HardwareSnapshot
    {
        private readonly IReadOnlyList<HardwareNode> _hardware;

        private HardwareSnapshot(IReadOnlyList<HardwareNode> hardware)
        {
            _hardware = hardware;
            CapturedAtUtc = DateTime.UtcNow;
        }

        public DateTime CapturedAtUtc { get; }

        public static HardwareSnapshot From(IEnumerable<IHardware> hardwareItems)
        {
            return new HardwareSnapshot(hardwareItems.Select(HardwareNode.From).ToArray());
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("CapturedAtUtc: " + CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture));

            foreach (var hardware in _hardware)
            {
                hardware.AppendText(builder, 0);
            }

            return builder.ToString();
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"capturedAtUtc\": \"" + CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture) + "\",");
            builder.AppendLine("  \"hardware\": [");

            for (var index = 0; index < _hardware.Count; index++)
            {
                _hardware[index].AppendJson(builder, 4);
                builder.AppendLine(index == _hardware.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }

    internal sealed class HardwareNode
    {
        private HardwareNode(
            string name,
            string identifier,
            string type,
            IReadOnlyList<SensorNode> sensors,
            IReadOnlyList<HardwareNode> children)
        {
            Name = name;
            Identifier = identifier;
            Type = type;
            Sensors = sensors;
            Children = children;
        }

        private string Name { get; }

        private string Identifier { get; }

        private string Type { get; }

        private IReadOnlyList<SensorNode> Sensors { get; }

        private IReadOnlyList<HardwareNode> Children { get; }

        public static HardwareNode From(IHardware hardware)
        {
            return new HardwareNode(
                hardware.Name,
                hardware.Identifier.ToString(),
                hardware.HardwareType.ToString(),
                hardware.Sensors.Select(SensorNode.From).ToArray(),
                hardware.SubHardware.Select(From).ToArray());
        }

        public void AppendText(StringBuilder builder, int indent)
        {
            var prefix = new string(' ', indent);
            builder.AppendLine(prefix + Type + ": " + Name + " [" + Identifier + "]");

            foreach (var sensor in Sensors)
            {
                sensor.AppendText(builder, indent + 2);
            }

            foreach (var child in Children)
            {
                child.AppendText(builder, indent + 2);
            }
        }

        public void AppendJson(StringBuilder builder, int indent)
        {
            var prefix = new string(' ', indent);
            builder.AppendLine(prefix + "{");
            builder.AppendLine(prefix + "  \"name\": \"" + Json.Escape(Name) + "\",");
            builder.AppendLine(prefix + "  \"identifier\": \"" + Json.Escape(Identifier) + "\",");
            builder.AppendLine(prefix + "  \"type\": \"" + Json.Escape(Type) + "\",");
            builder.AppendLine(prefix + "  \"sensors\": [");

            for (var index = 0; index < Sensors.Count; index++)
            {
                Sensors[index].AppendJson(builder, indent + 4);
                builder.AppendLine(index == Sensors.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine(prefix + "  ],");
            builder.AppendLine(prefix + "  \"children\": [");

            for (var index = 0; index < Children.Count; index++)
            {
                Children[index].AppendJson(builder, indent + 4);
                builder.AppendLine(index == Children.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine(prefix + "  ]");
            builder.Append(prefix + "}");
        }
    }

    internal sealed class SensorNode
    {
        private SensorNode(string name, string identifier, string type, float? value, float? minimum, float? maximum)
        {
            Name = name;
            Identifier = identifier;
            Type = type;
            Value = value;
            Minimum = minimum;
            Maximum = maximum;
        }

        private string Name { get; }

        private string Identifier { get; }

        private string Type { get; }

        private float? Value { get; }

        private float? Minimum { get; }

        private float? Maximum { get; }

        public static SensorNode From(ISensor sensor)
        {
            return new SensorNode(
                sensor.Name,
                sensor.Identifier.ToString(),
                sensor.SensorType.ToString(),
                sensor.Value,
                sensor.Min,
                sensor.Max);
        }

        public void AppendText(StringBuilder builder, int indent)
        {
            var prefix = new string(' ', indent);
            builder.AppendLine(prefix + Type + ": " + Name + " [" + Identifier + "] value=" + Format(Value) + " min=" + Format(Minimum) + " max=" + Format(Maximum));
        }

        public void AppendJson(StringBuilder builder, int indent)
        {
            var prefix = new string(' ', indent);
            builder.AppendLine(prefix + "{");
            builder.AppendLine(prefix + "  \"name\": \"" + Json.Escape(Name) + "\",");
            builder.AppendLine(prefix + "  \"identifier\": \"" + Json.Escape(Identifier) + "\",");
            builder.AppendLine(prefix + "  \"type\": \"" + Json.Escape(Type) + "\",");
            builder.AppendLine(prefix + "  \"value\": " + Json.NumberOrNull(Value) + ",");
            builder.AppendLine(prefix + "  \"minimum\": " + Json.NumberOrNull(Minimum) + ",");
            builder.AppendLine(prefix + "  \"maximum\": " + Json.NumberOrNull(Maximum));
            builder.Append(prefix + "}");
        }

        private static string Format(float? value)
        {
            return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "null";
        }
    }

    internal static class Json
    {
        public static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        public static string NumberOrNull(float? value)
        {
            return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "null";
        }
    }
}
