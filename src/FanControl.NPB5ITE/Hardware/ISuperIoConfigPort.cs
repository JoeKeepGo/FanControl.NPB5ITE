namespace FanControl.NPB5ITE.Hardware
{
    public interface ISuperIoConfigPort
    {
        void SelectSlot(int slot);

        byte ReadIoPortByte(ushort port);

        void WriteIoPortByte(ushort port, byte value);

        void FindBars();

        byte ReadConfigByte(byte register);

        ushort ReadConfigWord(byte register);

        void WriteConfigByte(byte register, byte value);
    }
}
