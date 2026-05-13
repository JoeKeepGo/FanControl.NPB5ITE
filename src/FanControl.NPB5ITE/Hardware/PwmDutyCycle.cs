using System;

namespace FanControl.NPB5ITE.Hardware
{
    public static class PwmDutyCycle
    {
        public const byte MinimumRawValue = 0x00;
        public const byte MaximumRawValue = 0xFF;

        public static byte PercentToRaw(float percent)
        {
            if (float.IsNaN(percent) || percent <= 0.0f)
            {
                return MinimumRawValue;
            }

            if (percent >= 100.0f)
            {
                return MaximumRawValue;
            }

            return (byte)Math.Floor(percent * MaximumRawValue / 100.0f);
        }

        public static float RawToPercent(byte raw)
        {
            return raw * 100.0f / MaximumRawValue;
        }
    }
}
