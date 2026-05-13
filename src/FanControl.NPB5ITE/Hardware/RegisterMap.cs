namespace FanControl.NPB5ITE.Hardware
{
    public static class RegisterMap
    {
        public const ushort PrimarySuperIoIndexPort = 0x2E;
        public const ushort PrimarySuperIoDataPort = 0x2F;
        public const ushort AlternateSuperIoIndexPort = 0x4E;
        public const ushort AlternateSuperIoDataPort = 0x4F;
        public const byte EnterConfigurationModeKey = 0x87;
        public const byte ExitConfigurationModeRegister = 0x02;
        public const byte ExitConfigurationModeValue = 0x02;

        public static readonly PwmRegisterSet? ConfirmedCpuFanControl = null;

        public static readonly PwmRegisterSet? ExperimentalCpuFanControl = new PwmRegisterSet(
            new RegisterDefinition(
                "Experimental IT8613E fan2 PWM control",
                0x16,
                RegisterAddressSpace.It8613eHardwareMonitor,
                RegisterConfidence.Experimental,
                "Observed on NPB5/RPBNB IT8613E at HWM base 0x0A30 while BIOS Software Mode raw 204 was active. Keeps bit 7 clear for software operation."),
            0x00,
            new RegisterDefinition(
                "Experimental IT8613E fan2 PWM duty ext",
                0x6B,
                RegisterAddressSpace.It8613eHardwareMonitor,
                RegisterConfidence.Experimental,
                "Read-only admin dump showed 0xCC when BIOS Manual PWM Setting was 204. Must confirm with raw 153/127 before moving to Confirmed."),
            new RegisterDefinition(
                "Experimental IT8613E fan2 PWM control restore",
                0x16,
                RegisterAddressSpace.It8613eHardwareMonitor,
                RegisterConfidence.Experimental,
                "No Auto restore value is confirmed. Restore uses old-value snapshots only."),
            0x00);

        public static RegisterDefinition[] ConfirmedPwmRegisters =>
            ConfirmedCpuFanControl == null
                ? new RegisterDefinition[0]
                : new[]
                {
                    ConfirmedCpuFanControl.ManualModeRegister,
                    ConfirmedCpuFanControl.DutyRegister,
                    ConfirmedCpuFanControl.AutomaticModeRegister
                };

        public static readonly RegisterDefinition[] ExperimentalPwmRegisters =
            ExperimentalCpuFanControl == null
                ? new RegisterDefinition[0]
                : new[]
                {
                    ExperimentalCpuFanControl.ManualModeRegister,
                    ExperimentalCpuFanControl.DutyRegister,
                    ExperimentalCpuFanControl.AutomaticModeRegister
                };
    }
}
