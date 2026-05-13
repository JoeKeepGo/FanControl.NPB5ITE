namespace FanControl.NPB5ITE.Hardware
{
    public sealed class RingIo : IIoPort
    {
        public bool IsAvailable => false;

        public byte ReadByte(ushort port)
        {
            throw new HardwareAccessException("No WinRing0/inpoutx64 I/O provider is configured for port reads.");
        }

        public void WriteByte(ushort port, byte value)
        {
            throw new HardwareAccessException("No WinRing0/inpoutx64 I/O provider is configured for port writes.");
        }

        public void Dispose()
        {
        }
    }
}
