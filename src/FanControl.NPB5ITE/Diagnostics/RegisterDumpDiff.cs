using System.Collections.Generic;

namespace FanControl.NPB5ITE.Diagnostics
{
    public sealed class RegisterDumpDiff
    {
        private readonly List<RegisterValueChange> _changes = new List<RegisterValueChange>();

        public IReadOnlyList<RegisterValueChange> Changes => _changes;

        public void Add(RegisterValueChange change)
        {
            _changes.Add(change);
        }
    }
}
