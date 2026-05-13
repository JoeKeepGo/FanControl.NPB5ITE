namespace FanControl.NPB5ITE.Temperature
{
    public sealed class UnknownCpuTemperatureSource : ICpuTemperatureSource
    {
        public float? ReadCpuTemperatureCelsius()
        {
            return null;
        }
    }
}
