namespace FanControl.NPB5ITE.Safety
{
    public sealed class FanSafetyDecision
    {
        public FanSafetyDecision(FanSafetyAction action, float? pwmPercent, string reason)
        {
            Action = action;
            PwmPercent = pwmPercent;
            Reason = reason;
        }

        public FanSafetyAction Action { get; }

        public float? PwmPercent { get; }

        public string Reason { get; }
    }
}
