using System;
using System.Globalization;

namespace FanControl.NPB5ITE
{
    public sealed class PluginOptions
    {
        public const float DefaultMinimumPwmPercent = 35.0f;
        public const float ExperimentalMinimumPwmPercent = 10.0f;
        public const float DefaultCriticalCpuTemperatureCelsius = 85.0f;

        public bool EnableHardwareWrites { get; private set; }

        public bool EnableExperimentalRegisters { get; private set; }

        public bool AllowLowPwm { get; private set; }

        public bool AllowManualWithoutCpuTemperature { get; private set; }

        public float MinimumPwmPercent { get; private set; } = DefaultMinimumPwmPercent;

        public float CriticalCpuTemperatureCelsius { get; private set; } = DefaultCriticalCpuTemperatureCelsius;

        public static PluginOptions FromEnvironment()
        {
            return new PluginOptions
            {
                EnableHardwareWrites = IsEnabled("FANCONTROL_NPB5ITE_ENABLE_WRITES"),
                EnableExperimentalRegisters = IsEnabled("FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS"),
                AllowLowPwm = IsEnabled("FANCONTROL_NPB5ITE_ALLOW_LOW_PWM"),
                AllowManualWithoutCpuTemperature = IsEnabled("FANCONTROL_NPB5ITE_ALLOW_MANUAL_WITHOUT_CPU_TEMP"),
                MinimumPwmPercent = ReadMinimumPwmPercent()
            };
        }

        private static float ReadMinimumPwmPercent()
        {
            var configured = ReadFloat("FANCONTROL_NPB5ITE_MIN_PWM_PERCENT");
            if (!configured.HasValue)
            {
                return DefaultMinimumPwmPercent;
            }

            if (!IsEnabled("FANCONTROL_NPB5ITE_ALLOW_LOW_PWM"))
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
