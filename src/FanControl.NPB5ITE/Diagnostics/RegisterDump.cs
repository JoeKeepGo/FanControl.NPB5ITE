using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FanControl.NPB5ITE.Diagnostics
{
    public sealed class RegisterDump
    {
        private readonly List<RegisterValue> _values = new List<RegisterValue>();
        private readonly List<string> _notes = new List<string>();

        public RegisterDump(string modeLabel, string source)
        {
            ModeLabel = modeLabel;
            Source = source;
            CapturedAtUtc = DateTime.UtcNow;
        }

        public string ModeLabel { get; }

        public string Source { get; }

        public DateTime CapturedAtUtc { get; }

        public IReadOnlyList<RegisterValue> Values => _values;

        public IReadOnlyList<string> Notes => _notes;

        public void AddValue(string name, ushort address, byte value)
        {
            _values.Add(new RegisterValue(name, address, value));
        }

        public void AddNote(string note)
        {
            _notes.Add(note);
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Mode: " + ModeLabel);
            builder.AppendLine("Source: " + Source);
            builder.AppendLine("CapturedAtUtc: " + CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture));

            foreach (var note in _notes)
            {
                builder.AppendLine("Note: " + note);
            }

            foreach (var value in _values)
            {
                builder.AppendLine(value.Name + " 0x" + value.Address.ToString("X4", CultureInfo.InvariantCulture) + " = 0x" + value.Value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"modeLabel\": \"" + Escape(ModeLabel) + "\",");
            builder.AppendLine("  \"source\": \"" + Escape(Source) + "\",");
            builder.AppendLine("  \"capturedAtUtc\": \"" + CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture) + "\",");
            builder.AppendLine("  \"notes\": [");

            for (var index = 0; index < _notes.Count; index++)
            {
                builder.Append("    \"" + Escape(_notes[index]) + "\"");
                builder.AppendLine(index == _notes.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"values\": [");

            for (var index = 0; index < _values.Count; index++)
            {
                var value = _values[index];
                builder.Append("    { \"name\": \"" + Escape(value.Name) + "\", \"address\": \"0x" + value.Address.ToString("X4", CultureInfo.InvariantCulture) + "\", \"value\": \"0x" + value.Value.ToString("X2", CultureInfo.InvariantCulture) + "\" }");
                builder.AppendLine(index == _values.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
