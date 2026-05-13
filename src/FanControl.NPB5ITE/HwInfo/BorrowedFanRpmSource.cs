namespace FanControl.NPB5ITE.HwInfo
{
    public sealed class BorrowedFanRpmSource : IFanRpmSource
    {
        private readonly IFanRpmSource _source;

        public BorrowedFanRpmSource(IFanRpmSource source)
        {
            _source = source;
        }

        public FanRpmReading ReadCpuFanRpm()
        {
            return _source.ReadCpuFanRpm();
        }
    }
}
