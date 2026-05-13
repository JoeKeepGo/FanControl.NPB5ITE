using System;

namespace FanControl.NPB5ITE.Hardware
{
    public sealed class HardwareAccessException : Exception
    {
        public HardwareAccessException(string message)
            : base(message)
        {
        }

        public HardwareAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
