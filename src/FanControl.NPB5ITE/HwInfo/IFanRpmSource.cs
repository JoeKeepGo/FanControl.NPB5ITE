namespace FanControl.NPB5ITE.HwInfo
{
    public interface IFanRpmSource
    {
        FanRpmReading ReadCpuFanRpm();
    }
}
