namespace FanControl.NPB5ITE.Diagnostics
{
    public sealed class RegisterValueChange
    {
        public RegisterValueChange(string name, ushort address, byte before, byte after)
        {
            Name = name;
            Address = address;
            Before = before;
            After = after;
        }

        public string Name { get; }

        public ushort Address { get; }

        public byte Before { get; }

        public byte After { get; }
    }
}
