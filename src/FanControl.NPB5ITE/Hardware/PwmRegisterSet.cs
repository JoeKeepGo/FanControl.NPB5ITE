using System.Collections.Generic;

namespace FanControl.NPB5ITE.Hardware
{
    public sealed class PwmRegisterSet
    {
        public PwmRegisterSet(
            RegisterDefinition manualModeRegister,
            byte manualModeValue,
            RegisterDefinition dutyRegister,
            RegisterDefinition automaticModeRegister,
            byte automaticModeValue)
        {
            ManualModeRegister = manualModeRegister;
            ManualModeValue = manualModeValue;
            DutyRegister = dutyRegister;
            AutomaticModeRegister = automaticModeRegister;
            AutomaticModeValue = automaticModeValue;
        }

        public RegisterDefinition ManualModeRegister { get; }

        public byte ManualModeValue { get; }

        public RegisterDefinition DutyRegister { get; }

        public RegisterDefinition AutomaticModeRegister { get; }

        public byte AutomaticModeValue { get; }

        public IReadOnlyList<RegisterDefinition> Registers
        {
            get
            {
                return new[]
                {
                    ManualModeRegister,
                    DutyRegister,
                    AutomaticModeRegister
                };
            }
        }
    }
}
