using System;
using FanControl.NPB5ITE.Logging;

namespace FanControl.NPB5ITE.HwInfo
{
    public sealed class HwInfoFanRpmSource : IFanRpmSource
    {
        private const string SourceName = "HWiNFO Gadget";

        private readonly PluginLog _log;

        public HwInfoFanRpmSource(PluginLog log)
        {
            _log = log;
        }

        public FanRpmReading ReadCpuFanRpm()
        {
            try
            {
                return HwInfoVsbSnapshot.Capture().FindBestCpuFanRpm();
            }
            catch (Exception exception)
            {
                _log.Error("Failed to read HWiNFO fan RPM registry values.", exception);
                return FanRpmReading.Unavailable(SourceName, exception.Message);
            }
        }
    }
}
