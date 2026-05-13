using System;
using FanControl.NPB5ITE.Diagnostics;
using FanControl.NPB5ITE.HwInfo;

namespace FanControl.NPB5ITE.Hardware
{
    public interface IIte8613fIo : IFanRpmSource, IDisposable
    {
        void ApplyManualPwm(float pwmPercent);

        void ApplyFullSpeed();

        void RestoreAutomaticControl();

        RegisterDump CaptureReadOnlyDump(string modeLabel);

        PwmControlCapability GetPwmControlCapability();

        PwmControlCapability GetPwmControlCapability(bool isProcessElevated);
    }
}
