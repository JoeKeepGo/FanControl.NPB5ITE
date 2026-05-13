using System;
using System.Globalization;

namespace FanControl.NPB5ITE
{
    public sealed class PluginOptions
    {
        public const float DefaultMinimumPwmPercent = 35.0f;
        public const float ExperimentalMinimumPwmPercent = 10.0f;
        public const float DefaultCriticalCpuTemperatureCelsius = 85.0f;

        public HardwareIdentity HardwareIdentity { get; private set; } = HardwareIdentity.Unknown;

        public bool UsesTestedHardwareDefaults { get; private set; }

        public bool EnableHardwareWrites { get; private set; }

        public bool EnableExperimentalRegisters { get; private set; }

        public bool AllowLowPwm { get; private set; }

        public bool AllowManualWithoutCpuTemperature { get; private set; }

        public float MinimumPwmPercent { get; private set; } = DefaultMinimumPwmPercent;

        public float CriticalCpuTemperatureCelsius { get; private set; } = DefaultCriticalCpuTemperatureCelsius;

        public static PluginOptions FromEnvironment()
        {
            return FromEnvironment(HardwareIdentity.DetectSystem());
        }

        public static PluginOptions FromEnvironment(HardwareIdentity hardwareIdentity)
        {
            var disableWrites = IsEnabled("FANCONTROL_NPB5ITE_DISABLE_WRITES");
            var disableTestedHardwareDefaults = IsEnabled("FANCONTROL_NPB5ITE_DISABLE_TESTED_HARDWARE_DEFAULTS");
            var usesTestedHardwareDefaults = hardwareIdentity.IsTestedNpb5RpBnb && !disableTestedHardwareDefaults && !disableWrites;
            var allowLowPwm = IsEnabled("FANCONTROL_NPB5ITE_ALLOW_LOW_PWM");

            return new PluginOptions
            {
                HardwareIdentity = hardwareIdentity,
                UsesTestedHardwareDefaults = usesTestedHardwareDefaults,
                EnableHardwareWrites = !disableWrites && (usesTestedHardwareDefaults || IsEnabled("FANCONTROL_NPB5ITE_ENABLE_WRITES")),
                EnableExperimentalRegisters = !disableWrites && (usesTestedHardwareDefaults || IsEnabled("FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS")),
                AllowLowPwm = allowLowPwm,
                AllowManualWithoutCpuTemperature = IsEnabled("FANCONTROL_NPB5ITE_ALLOW_MANUAL_WITHOUT_CPU_TEMP"),
                MinimumPwmPercent = ReadMinimumPwmPercent(allowLowPwm)
            };
        }

        private static float ReadMinimumPwmPercent(bool allowLowPwm)
        {
            var configured = ReadFloat("FANCONTROL_NPB5ITE_MIN_PWM_PERCENT");
            if (!configured.HasValue)
            {
                return DefaultMinimumPwmPercent;
            }

            if (!allowLowPwm)
            {
                return Clamp(configured.Value, DefaultMinimumPwmPercent, 100.0f);
            }

            return Clamp(configured.Value, ExperimentalMinimumPwmPercent, 100.0f);
        }

        private static float? ReadFloat(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool IsEnabled(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
