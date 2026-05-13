using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace FanControl.NPB5ITE.Hardware
{
    public sealed class PwmControlCapability
    {
        private readonly IReadOnlyList<PwmControlBlocker> _blockers;

        public PwmControlCapability(
            bool hardwareWritesEnabled,
            bool experimentalRegistersEnabled,
            bool ioPortProviderConfigured,
            int confirmedRegisterCount,
            int experimentalRegisterCount,
            IReadOnlyList<PwmControlBlocker> blockers)
        {
            HardwareWritesEnabled = hardwareWritesEnabled;
            ExperimentalRegistersEnabled = experimentalRegistersEnabled;
            IoPortProviderConfigured = ioPortProviderConfigured;
            ConfirmedRegisterCount = confirmedRegisterCount;
            ExperimentalRegisterCount = experimentalRegisterCount;
            _blockers = blockers;
        }

        public bool HardwareWritesEnabled { get; }

        public bool ExperimentalRegistersEnabled { get; }

        public bool IoPortProviderConfigured { get; }

        public int ConfirmedRegisterCount { get; }

        public int ExperimentalRegisterCount { get; }

        public IReadOnlyList<PwmControlBlocker> Blockers => _blockers;

        public bool CanApplyManualPwm => _blockers.Count == 0;

        public string Summary
        {
            get
            {
                if (CanApplyManualPwm)
                {
                    return "Manual PWM writes are eligible.";
                }

                return "Manual PWM writes are blocked: " + string.Join("; ", DescribeBlockers()) + ".";
            }
        }

        public static PwmControlCapability Evaluate(PluginOptions options, IIoPort ioPort)
        {
            return Evaluate(options, ioPort, RegisterMap.ConfirmedCpuFanControl, RegisterMap.ExperimentalCpuFanControl);
        }

        public static PwmControlCapability Evaluate(
            PluginOptions options,
            IIoPort ioPort,
            PwmRegisterSet? confirmedCpuFanControl,
            PwmRegisterSet? experimentalCpuFanControl = null,
            bool? isProcessElevated = null)
        {
            var blockers = new List<PwmControlBlocker>();
            var ioPortProviderConfigured = ioPort.IsAvailable;
            var confirmedRegisterCount = confirmedCpuFanControl?.Registers.Count ?? 0;
            var experimentalRegisterCount = experimentalCpuFanControl?.Registers.Count ?? 0;
            var hasWritableRegisterMap = confirmedRegisterCount > 0 || (options.EnableExperimentalRegisters && experimentalRegisterCount > 0);
            var writableRegisterMap = confirmedCpuFanControl ?? (options.EnableExperimentalRegisters ? experimentalCpuFanControl : null);
            var requiresElevation = writableRegisterMap?.Registers.Any(register => register.AddressSpace == RegisterAddressSpace.It8613eHardwareMonitor) == true;

            if (!options.EnableHardwareWrites)
            {
                blockers.Add(PwmControlBlocker.HardwareWritesDisabled);
            }

            if (confirmedRegisterCount == 0 && experimentalRegisterCount > 0 && !options.EnableExperimentalRegisters)
            {
                blockers.Add(PwmControlBlocker.ExperimentalRegistersDisabled);
            }

            if (!ioPortProviderConfigured)
            {
                blockers.Add(PwmControlBlocker.NoIoPortProvider);
            }

            if (requiresElevation && !isProcessElevated.GetValueOrDefault(IsProcessElevated()))
            {
                blockers.Add(PwmControlBlocker.ProcessNotElevated);
            }

            if (!hasWritableRegisterMap)
            {
                blockers.Add(PwmControlBlocker.NoConfirmedRegisterMap);
            }

            return new PwmControlCapability(
                options.EnableHardwareWrites,
                options.EnableExperimentalRegisters,
                ioPortProviderConfigured,
                confirmedRegisterCount,
                experimentalRegisterCount,
                blockers);
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("CanApplyManualPwm: " + CanApplyManualPwm);
            builder.AppendLine("HardwareWritesEnabled: " + HardwareWritesEnabled);
            builder.AppendLine("ExperimentalRegistersEnabled: " + ExperimentalRegistersEnabled);
            builder.AppendLine("IoPortProviderConfigured: " + IoPortProviderConfigured);
            builder.AppendLine("ConfirmedRegisterCount: " + ConfirmedRegisterCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("ExperimentalRegisterCount: " + ExperimentalRegisterCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("Summary: " + Summary);

            foreach (var blocker in _blockers)
            {
                builder.AppendLine("Blocker: " + Describe(blocker));
            }

            return builder.ToString();
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"canApplyManualPwm\": " + JsonBool(CanApplyManualPwm) + ",");
            builder.AppendLine("  \"hardwareWritesEnabled\": " + JsonBool(HardwareWritesEnabled) + ",");
            builder.AppendLine("  \"experimentalRegistersEnabled\": " + JsonBool(ExperimentalRegistersEnabled) + ",");
            builder.AppendLine("  \"ioPortProviderConfigured\": " + JsonBool(IoPortProviderConfigured) + ",");
            builder.AppendLine("  \"confirmedRegisterCount\": " + ConfirmedRegisterCount.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("  \"experimentalRegisterCount\": " + ExperimentalRegisterCount.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("  \"summary\": \"" + Escape(Summary) + "\",");
            builder.AppendLine("  \"blockers\": [");

            for (var index = 0; index < _blockers.Count; index++)
            {
                builder.Append("    \"" + Escape(Describe(_blockers[index])) + "\"");
                builder.AppendLine(index == _blockers.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        public static string Describe(PwmControlBlocker blocker)
        {
            switch (blocker)
            {
                case PwmControlBlocker.HardwareWritesDisabled:
                    return "Hardware writes are disabled; FANCONTROL_NPB5ITE_ENABLE_WRITES is not enabled";
                case PwmControlBlocker.ExperimentalRegistersDisabled:
                    return "Experimental IT8613E fan2 PWM registers are present but FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS is not enabled";
                case PwmControlBlocker.ProcessNotElevated:
                    return "Process is not elevated; Administrator access is required for IT8613E HWM register writes";
                case PwmControlBlocker.NoIoPortProvider:
                    return "No low-level I/O provider is configured for Super I/O port access";
                case PwmControlBlocker.NoConfirmedRegisterMap:
                    return "No confirmed NPB5/RPBNB IT8613E PWM register map is enabled";
                default:
                    return "Unknown blocker: " + blocker;
            }
        }

        private IEnumerable<string> DescribeBlockers()
        {
            foreach (var blocker in _blockers)
            {
                yield return Describe(blocker);
            }
        }

        private static string JsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static bool IsProcessElevated()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                using (var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query))
                {
                    if (GetTokenInformation(
                        identity.Token,
                        TokenInformationClass.TokenElevation,
                        out var elevation,
                        Marshal.SizeOf<TokenElevation>(),
                        out _))
                    {
                        return elevation.TokenIsElevated != 0;
                    }
                }
            }
            catch
            {
                // Fall back to role membership below.
            }

            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(
            IntPtr tokenHandle,
            TokenInformationClass tokenInformationClass,
            out TokenElevation tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        private enum TokenInformationClass
        {
            TokenElevation = 20
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenElevation
        {
            public int TokenIsElevated;
        }
    }
}
