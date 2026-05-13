namespace FanControl.NPB5ITE.Diagnostics
{
    public sealed class RegisterValue
    {
        public RegisterValue(string name, ushort address, byte value)
        {
            Name = name;
            Address = address;
            Value = value;
        }

        public string Name { get; }

        public ushort Address { get; }

        public byte Value { get; }
    }
}
