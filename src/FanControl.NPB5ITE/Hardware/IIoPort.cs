using System;

namespace FanControl.NPB5ITE.Hardware
{
    public interface IIoPort : IDisposable
    {
        bool IsAvailable { get; }

        byte ReadByte(ushort port);

        void WriteByte(ushort port, byte value);
    }
}
