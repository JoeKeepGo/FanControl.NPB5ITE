namespace FanControl.NPB5ITE.Hardware
{
    public sealed class RegisterWriteSnapshot
    {
        public RegisterWriteSnapshot(RegisterDefinition register, byte oldValue)
        {
            Register = register;
            OldValue = oldValue;
        }

        public RegisterDefinition Register { get; }

        public byte OldValue { get; }
    }
}
