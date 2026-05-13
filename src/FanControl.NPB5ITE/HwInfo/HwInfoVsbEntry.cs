using System;
using System.Globalization;

namespace FanControl.NPB5ITE.HwInfo
{
    public sealed class HwInfoVsbEntry
    {
        private const string RpmUnit = "RPM";

        private static readonly CultureInfo ParseCulture = CultureInfo.GetCultureInfo("en-US");

        private HwInfoVsbEntry(
            int index,
            string sensor,
            string label,
            string value,
            string valueRaw,
            string unit,
            float? rpm,
            int score)
        {
            Index = index;
            Sensor = sensor;
            Label = label;
            Value = value;
            ValueRaw = valueRaw;
            Unit = unit;
            Rpm = rpm;
            Score = score;
        }

        public int Index { get; }

        public string Sensor { get; }

        public string Label { get; }

        public string Value { get; }

        public string ValueRaw { get; }

        public string Unit { get; }

        public float? Rpm { get; }

        public int Score { get; }

        public bool IsRpm => string.Equals(Unit, RpmUnit, StringComparison.OrdinalIgnoreCase);

        public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Sensor : Label + " - " + Sensor;

        public static HwInfoVsbEntry FromValues(int index, string sensor, string label, string value, string valueRaw)
        {
            sensor = sensor ?? string.Empty;
            label = label ?? string.Empty;
            value = value ?? string.Empty;
            valueRaw = valueRaw ?? string.Empty;

            var unit = ExtractUnit(value);
            var isRpm = string.Equals(unit, RpmUnit, StringComparison.OrdinalIgnoreCase);
            return new HwInfoVsbEntry(
                index,
                sensor,
                label,
                value,
                valueRaw,
                unit,
                isRpm ? ParseRpm(valueRaw, value) : null,
                CalculateScore(sensor, label));
        }

        private static string ExtractUnit(string formattedValue)
        {
            var trimmed = (formattedValue ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length < 2 ? string.Empty : parts[parts.Length - 1];
        }

        private static float? ParseRpm(string rawValue, string formattedValue)
        {
            var rpm = ParseNumber(rawValue);

            if (rpm != null)
            {
                return rpm;
            }

            var trimmed = (formattedValue ?? string.Empty).Trim();
            var separatorIndex = trimmed.IndexOf(' ');
            var numericText = separatorIndex < 0 ? trimmed : trimmed.Substring(0, separatorIndex);
            return ParseNumber(numericText);
        }

        private static float? ParseNumber(string value)
        {
            var trimmed = (value ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return null;
            }

            const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;

            if (float.TryParse(trimmed, styles, ParseCulture, out var parsed))
            {
                return parsed;
            }

            if (float.TryParse(trimmed, styles, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int CalculateScore(string sensor, string label)
        {
            var text = ((sensor ?? string.Empty) + " " + (label ?? string.Empty)).ToUpperInvariant();
            var score = 0;

            if (text.Contains("CPU"))
            {
                score += 8;
            }

            if (text.Contains("FAN"))
            {
                score += 4;
            }

            if (text.Contains("ITE") || text.Contains("IT8613"))
            {
                score += 2;
            }

            return score;
        }
    }
}
