namespace FanControl.NPB5ITE.Hardware
{
    public sealed class RegisterDefinition
    {
        public RegisterDefinition(string name, ushort address, RegisterConfidence confidence, string notes)
            : this(name, address, RegisterAddressSpace.DirectIo, confidence, notes)
        {
        }

        public RegisterDefinition(string name, ushort address, RegisterAddressSpace addressSpace, RegisterConfidence confidence, string notes)
        {
            Name = name;
            Address = address;
            AddressSpace = addressSpace;
            Confidence = confidence;
            Notes = notes;
        }

        public string Name { get; }

        public ushort Address { get; }

        public RegisterAddressSpace AddressSpace { get; }

        public RegisterConfidence Confidence { get; }

        public string Notes { get; }
    }
}
