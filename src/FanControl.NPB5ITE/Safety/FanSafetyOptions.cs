namespace FanControl.NPB5ITE.Safety
{
    public sealed class FanSafetyOptions
    {
        public float MinimumPwmPercent { get; set; } = PluginOptions.DefaultMinimumPwmPercent;

        public float CriticalCpuTemperatureCelsius { get; set; } = PluginOptions.DefaultCriticalCpuTemperatureCelsius;

        public bool AllowLowPwm { get; set; }

        public bool AllowManualWithoutCpuTemperature { get; set; }
    }
}
