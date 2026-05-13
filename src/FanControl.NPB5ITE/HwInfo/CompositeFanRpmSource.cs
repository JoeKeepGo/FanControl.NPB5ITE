using System;
using System.Collections.Generic;
using System.Linq;

namespace FanControl.NPB5ITE.HwInfo
{
    public sealed class CompositeFanRpmSource : IFanRpmSource, IDisposable
    {
        private readonly IReadOnlyList<IFanRpmSource> _sources;

        public CompositeFanRpmSource(IEnumerable<IFanRpmSource> sources)
        {
            _sources = sources.ToArray();
        }

        public FanRpmReading ReadCpuFanRpm()
        {
            var lastMessage = string.Empty;

            foreach (var source in _sources)
            {
                var reading = source.ReadCpuFanRpm();

                if (reading.Succeeded)
                {
                    return reading;
                }

                lastMessage = reading.Source + ": " + reading.Message;
            }

            return FanRpmReading.Unavailable("Composite", string.IsNullOrWhiteSpace(lastMessage)
                ? "No RPM sources are configured."
                : lastMessage);
        }

        public void Dispose()
        {
            foreach (var source in _sources.OfType<IDisposable>())
            {
                source.Dispose();
            }
        }
    }
}
