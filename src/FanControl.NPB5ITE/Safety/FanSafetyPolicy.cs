using System;

namespace FanControl.NPB5ITE.Safety
{
    public sealed class FanSafetyPolicy
    {
        private const float MaximumPwmPercent = 100.0f;
        private const float ReleaseThresholdPercent = 0.0f;

        private readonly FanSafetyOptions _options;

        public FanSafetyPolicy(FanSafetyOptions options)
        {
            if (float.IsNaN(options.MinimumPwmPercent) || options.MinimumPwmPercent > MaximumPwmPercent)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Minimum PWM must be between the safety floor and 100%.");
            }

            if (options.MinimumPwmPercent < PluginOptions.DefaultMinimumPwmPercent)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Minimum PWM is below the configured safety floor.");
            }

            if (float.IsNaN(options.CriticalCpuTemperatureCelsius)
                || options.CriticalCpuTemperatureCelsius < PluginOptions.MinimumCriticalCpuTemperatureCelsius
                || options.CriticalCpuTemperatureCelsius > PluginOptions.MaximumCriticalCpuTemperatureCelsius)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Critical CPU temperature must be between the configured safety bounds.");
            }

            _options = options;
        }

        public FanSafetyDecision Evaluate(FanSafetyInputs inputs)
        {
            if (!inputs.HardwareWritesEnabled)
            {
                return new FanSafetyDecision(
                    FanSafetyAction.RestoreAutomaticControl,
                    null,
                    "Hardware writes are not explicitly enabled.");
            }

            if (!inputs.FanRpmReadSucceeded)
            {
                return new FanSafetyDecision(
                    FanSafetyAction.RestoreAutomaticControl,
                    null,
                    "Fan RPM read failed; restoring automatic control.");
            }

            if (inputs.CpuTemperatureCelsius == null && !_options.AllowManualWithoutCpuTemperature)
            {
                return new FanSafetyDecision(
                    FanSafetyAction.RestoreAutomaticControl,
                    null,
                    "CPU temperature is unavailable; restoring automatic control.");
            }

            if (inputs.CpuTemperatureCelsius >= _options.CriticalCpuTemperatureCelsius)
            {
                return new FanSafetyDecision(
                    FanSafetyAction.ApplyFullSpeed,
                    MaximumPwmPercent,
                    "CPU temperature is at or above critical threshold.");
            }

            if (float.IsNaN(inputs.RequestedPwmPercent) || inputs.RequestedPwmPercent <= ReleaseThresholdPercent)
            {
                return new FanSafetyDecision(
                    FanSafetyAction.RestoreAutomaticControl,
                    null,
                    "Requested PWM is zero; releasing manual control.");
            }

            var clamped = Clamp(inputs.RequestedPwmPercent, _options.MinimumPwmPercent, MaximumPwmPercent);

            return new FanSafetyDecision(
                FanSafetyAction.ApplyManualPwm,
                clamped,
                clamped.Equals(inputs.RequestedPwmPercent)
                    ? "Requested PWM is within safe bounds."
                    : "Requested PWM was clamped to the safe minimum.");
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
