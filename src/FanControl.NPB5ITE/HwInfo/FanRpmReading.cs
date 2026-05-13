namespace FanControl.NPB5ITE.HwInfo
{
    public sealed class FanRpmReading
    {
        private FanRpmReading(bool succeeded, float? rpm, string source, string message)
        {
            Succeeded = succeeded;
            Rpm = rpm;
            Source = source;
            Message = message;
        }

        public bool Succeeded { get; }

        public float? Rpm { get; }

        public string Source { get; }

        public string Message { get; }

        public static FanRpmReading Success(float rpm, string source)
        {
            return new FanRpmReading(true, rpm, source, string.Empty);
        }

        public static FanRpmReading Unavailable(string source, string message)
        {
            return new FanRpmReading(false, null, source, message);
        }
    }
}
