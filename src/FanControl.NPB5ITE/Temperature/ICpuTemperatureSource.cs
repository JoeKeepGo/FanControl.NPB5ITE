namespace FanControl.NPB5ITE.Temperature
{
    public interface ICpuTemperatureSource
    {
        float? ReadCpuTemperatureCelsius();
    }
}
