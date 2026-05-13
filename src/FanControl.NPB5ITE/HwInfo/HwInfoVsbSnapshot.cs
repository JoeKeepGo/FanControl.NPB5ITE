using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace FanControl.NPB5ITE.HwInfo
{
    public sealed class HwInfoVsbSnapshot
    {
        private const string SourceName = "HWiNFO Gadget";
        private const string HwiNfo64VsbKey = @"SOFTWARE\HWiNFO64\VSB";
        private const string HwiNfo32VsbKey = @"SOFTWARE\HWiNFO32\VSB";
        private const string SensorPrefix = "Sensor";
        private const string LabelPrefix = "Label";
        private const string ValuePrefix = "Value";
        private const string ValueRawPrefix = "ValueRaw";

        private readonly IReadOnlyList<HwInfoVsbEntry> _entries;

        private HwInfoVsbSnapshot(string sourceKey, string? unavailableReason, IReadOnlyList<HwInfoVsbEntry> entries)
        {
            SourceKey = sourceKey;
            UnavailableReason = unavailableReason;
            _entries = entries;
            CapturedAtUtc = DateTime.UtcNow;
        }

        public string SourceKey { get; }

        public string? UnavailableReason { get; }

        public DateTime CapturedAtUtc { get; }

        public IReadOnlyList<HwInfoVsbEntry> Entries => _entries;

        public bool IsAvailable => string.IsNullOrWhiteSpace(UnavailableReason);

        public static HwInfoVsbSnapshot Capture()
        {
            using (var key = OpenVsbKey(out var sourceKey))
            {
                if (key == null)
                {
                    return Unavailable("HWiNFO VSB registry key was not found.");
                }

                return new HwInfoVsbSnapshot(sourceKey, null, ReadEntries(key).ToArray());
            }
        }

        public static HwInfoVsbSnapshot FromEntries(IEnumerable<HwInfoVsbEntry> entries)
        {
            return new HwInfoVsbSnapshot("Test", null, entries.ToArray());
        }

        public static HwInfoVsbSnapshot Unavailable(string reason)
        {
            return new HwInfoVsbSnapshot(SourceName, reason, Array.Empty<HwInfoVsbEntry>());
        }

        public FanRpmReading FindBestCpuFanRpm()
        {
            if (!IsAvailable)
            {
                return FanRpmReading.Unavailable(SourceName, UnavailableReason ?? "HWiNFO VSB is unavailable.");
            }

            var candidates = _entries
                .Where(entry => entry.IsRpm && entry.Rpm != null)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Index)
                .ToArray();

            if (candidates.Length == 0)
            {
                return FanRpmReading.Unavailable(SourceName, "No RPM value is currently exported by HWiNFO.");
            }

            var best = candidates[0];
            return FanRpmReading.Success(best.Rpm.GetValueOrDefault(), SourceName + " " + best.DisplayName);
        }

        public string ToText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Source: " + SourceKey);
            builder.AppendLine("CapturedAtUtc: " + CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture));

            if (!IsAvailable)
            {
                builder.AppendLine("Unavailable: " + UnavailableReason);
            }

            foreach (var entry in _entries)
            {
                builder.Append("Index ");
                builder.Append(entry.Index.ToString(CultureInfo.InvariantCulture));
                builder.Append(": ");
                builder.Append(entry.DisplayName);
                builder.Append(" value=");
                builder.Append(entry.Value);
                builder.Append(" raw=");
                builder.Append(entry.ValueRaw);
                builder.Append(" unit=");
                builder.Append(entry.Unit);
                builder.Append(" rpm=");
                builder.Append(entry.Rpm?.ToString("0.###", CultureInfo.InvariantCulture) ?? "null");
                builder.Append(" score=");
                builder.AppendLine(entry.Score.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"sourceKey\": \"" + Escape(SourceKey) + "\",");
            builder.AppendLine("  \"capturedAtUtc\": \"" + CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture) + "\",");
            builder.AppendLine("  \"unavailableReason\": " + JsonStringOrNull(UnavailableReason) + ",");
            builder.AppendLine("  \"entries\": [");

            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];
                builder.AppendLine("    {");
                builder.AppendLine("      \"index\": " + entry.Index.ToString(CultureInfo.InvariantCulture) + ",");
                builder.AppendLine("      \"sensor\": \"" + Escape(entry.Sensor) + "\",");
                builder.AppendLine("      \"label\": \"" + Escape(entry.Label) + "\",");
                builder.AppendLine("      \"value\": \"" + Escape(entry.Value) + "\",");
                builder.AppendLine("      \"valueRaw\": \"" + Escape(entry.ValueRaw) + "\",");
                builder.AppendLine("      \"unit\": \"" + Escape(entry.Unit) + "\",");
                builder.AppendLine("      \"rpm\": " + NumberOrNull(entry.Rpm) + ",");
                builder.AppendLine("      \"score\": " + entry.Score.ToString(CultureInfo.InvariantCulture));
                builder.Append("    }");
                builder.AppendLine(index == _entries.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static RegistryKey? OpenVsbKey(out string sourceKey)
        {
            var key = Registry.CurrentUser.OpenSubKey(HwiNfo64VsbKey, false);
            if (key != null)
            {
                sourceKey = "HKCU\\" + HwiNfo64VsbKey;
                return key;
            }

            key = Registry.CurrentUser.OpenSubKey(HwiNfo32VsbKey, false);
            if (key != null)
            {
                sourceKey = "HKCU\\" + HwiNfo32VsbKey;
                return key;
            }

            sourceKey = SourceName;
            return null;
        }

        private static IEnumerable<HwInfoVsbEntry> ReadEntries(RegistryKey key)
        {
            foreach (var sensorName in key.GetValueNames().Where(name => name.StartsWith(SensorPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                if (!int.TryParse(sensorName.Substring(SensorPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                {
                    continue;
                }

                yield return HwInfoVsbEntry.FromValues(
                    index,
                    GetStringValue(key, SensorPrefix + index),
                    GetStringValue(key, LabelPrefix + index),
                    GetStringValue(key, ValuePrefix + index),
                    GetStringValue(key, ValueRawPrefix + index));
            }
        }

        private static string GetStringValue(RegistryKey key, string name)
        {
            return Convert.ToString(key.GetValue(name), CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string JsonStringOrNull(string? value)
        {
            return value == null ? "null" : "\"" + Escape(value) + "\"";
        }

        private static string NumberOrNull(float? value)
        {
            return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "null";
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
