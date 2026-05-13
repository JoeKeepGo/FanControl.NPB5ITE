namespace FanControl.NPB5ITE.Safety
{
    public sealed class FanSafetyInputs
    {
        public float RequestedPwmPercent { get; set; }

        public float? CpuTemperatureCelsius { get; set; }

        public bool FanRpmReadSucceeded { get; set; }

        public bool HardwareWritesEnabled { get; set; }
    }
}
