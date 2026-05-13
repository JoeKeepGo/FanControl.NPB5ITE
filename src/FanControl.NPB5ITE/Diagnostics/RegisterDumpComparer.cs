using System.Collections.Generic;

namespace FanControl.NPB5ITE.Diagnostics
{
    public static class RegisterDumpComparer
    {
        public static RegisterDumpDiff Compare(RegisterDump before, RegisterDump after)
        {
            var diff = new RegisterDumpDiff();
            var beforeByAddress = new Dictionary<ushort, RegisterValue>();

            foreach (var value in before.Values)
            {
                beforeByAddress[value.Address] = value;
            }

            foreach (var value in after.Values)
            {
                if (!beforeByAddress.TryGetValue(value.Address, out var oldValue))
                {
                    continue;
                }

                if (oldValue.Value != value.Value)
                {
                    diff.Add(new RegisterValueChange(value.Name, value.Address, oldValue.Value, value.Value));
                }
            }

            return diff;
        }
    }
}
