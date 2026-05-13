using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using FanControl.NPB5ITE.Diagnostics;
using FanControl.NPB5ITE.HwInfo;
using FanControl.NPB5ITE.Logging;

namespace FanControl.NPB5ITE.Hardware
{
    public sealed class Ite8613fIo : IIte8613fIo
    {
        private const string SourceName = "IT8613F direct";
        private const byte ConfigurationControlRegister = 0x02;
        private const byte DeviceSelectRegister = 0x07;
        private const byte ChipIdRegister = 0x20;
        private const byte ChipVersionRegister = 0x22;
        private const byte BaseAddressRegister = 0x60;
        private const byte It87EnvironmentControllerLogicalDevice = 0x04;
        private const byte It87GpioLogicalDevice = 0x07;
        private const byte AddressRegisterOffset = 0x05;
        private const byte DataRegisterOffset = 0x06;
        private const byte FanMainControlRegister = 0x13;

        private readonly IIoPort _ioPort;
        private readonly PluginOptions _options;
        private readonly PluginLog _log;
        private readonly PwmRegisterSet? _confirmedCpuFanControl;
        private readonly PwmRegisterSet? _experimentalCpuFanControl;
        private readonly Stack<RegisterWriteSnapshot> _restoreStack = new Stack<RegisterWriteSnapshot>();
        private readonly HashSet<ushort> _snapshottedRegisters = new HashSet<ushort>();
        private readonly object _sync = new object();
        private ushort? _hwmBase;
        private byte? _lastAppliedRawDuty;
        private bool _manualModeApplied;
        private bool _disposed;

        public Ite8613fIo(IIoPort ioPort, PluginOptions options, PluginLog log)
            : this(ioPort, options, log, RegisterMap.ConfirmedCpuFanControl, RegisterMap.ExperimentalCpuFanControl)
        {
        }

        public Ite8613fIo(
            IIoPort ioPort,
            PluginOptions options,
            PluginLog log,
            PwmRegisterSet? confirmedCpuFanControl,
            PwmRegisterSet? experimentalCpuFanControl = null)
        {
            _ioPort = ioPort;
            _options = options;
            _log = log;
            _confirmedCpuFanControl = confirmedCpuFanControl;
            _experimentalCpuFanControl = experimentalCpuFanControl;
        }

        public FanRpmReading ReadCpuFanRpm()
        {
            return FanRpmReading.Unavailable(
                SourceName,
                "Direct IT8613F RPM registers are not confirmed for NPB5 yet.");
        }

        public void ApplyManualPwm(float pwmPercent)
        {
            lock (_sync)
            {
                EnsureCanApplyManualPwm();
                var registerSet = RequireWritableCpuFanControl();
                var rawDuty = PwmDutyCycle.PercentToRaw(pwmPercent);

                if (_lastAppliedRawDuty == rawDuty)
                {
                    return;
                }

                _log.Info("Applying manual CPU fan PWM " + pwmPercent + "% as raw duty " + rawDuty + ".");
                if (!_manualModeApplied)
                {
                    WriteRegisterWithSnapshot(registerSet.ManualModeRegister, registerSet.ManualModeValue);
                    _manualModeApplied = true;
                }

                WriteRegisterWithSnapshot(registerSet.DutyRegister, rawDuty);
                _lastAppliedRawDuty = rawDuty;
            }
        }

        public void ApplyFullSpeed()
        {
            ApplyManualPwm(100.0f);
        }

        public void RestoreAutomaticControl()
        {
            lock (_sync)
            {
                if (!_options.EnableHardwareWrites)
                {
                    return;
                }

                if (_restoreStack.Count == 0 && _confirmedCpuFanControl != null)
                {
                    WriteRegister(_confirmedCpuFanControl.AutomaticModeRegister, _confirmedCpuFanControl.AutomaticModeValue);
                    _manualModeApplied = false;
                    _lastAppliedRawDuty = null;
                    return;
                }

                while (_restoreStack.Count > 0)
                {
                    var snapshot = _restoreStack.Pop();
                    WriteRegister(snapshot.Register, snapshot.OldValue);
                }

                _snapshottedRegisters.Clear();
                _manualModeApplied = false;
                _lastAppliedRawDuty = null;
            }
        }

        public RegisterDump CaptureReadOnlyDump(string modeLabel)
        {
            var dump = new RegisterDump(modeLabel, SourceName);
            dump.AddNote("Diagnostic mode does not write PWM duty, fan mode, Auto, or EC control registers.");
            dump.AddNote("The Super I/O configuration unlock/select sequence is used only to read chip identity and base addresses.");

            if (!_ioPort.IsAvailable)
            {
                dump.AddNote("Low-level I/O provider is unavailable; live Super I/O register reads were skipped.");
                return dump;
            }

            var lhmIoPort = _ioPort as LibreHardwareMonitorLpcIoPort;
            if (lhmIoPort != null)
            {
                dump.AddNote("LibreHardwareMonitor PawnIO config access: " + lhmIoPort.ConfigAccessStatus);
            }

            var pawnIoConfigPort = _ioPort as ISuperIoConfigPort;
            if (pawnIoConfigPort != null)
            {
                CaptureReadOnlyDump(pawnIoConfigPort, 0, RegisterMap.PrimarySuperIoIndexPort, RegisterMap.PrimarySuperIoDataPort, dump);
                CaptureReadOnlyDump(pawnIoConfigPort, 1, RegisterMap.AlternateSuperIoIndexPort, RegisterMap.AlternateSuperIoDataPort, dump);
            }
            else
            {
                dump.AddNote("High-level PawnIO LPC config methods are unavailable; using raw port fallback.");
                CaptureReadOnlyDump(RegisterMap.PrimarySuperIoIndexPort, RegisterMap.PrimarySuperIoDataPort, dump);
                CaptureReadOnlyDump(RegisterMap.AlternateSuperIoIndexPort, RegisterMap.AlternateSuperIoDataPort, dump);
            }

            return dump;
        }

        public PwmControlCapability GetPwmControlCapability()
        {
            return PwmControlCapability.Evaluate(_options, _ioPort, _confirmedCpuFanControl, _experimentalCpuFanControl);
        }

        public PwmControlCapability GetPwmControlCapability(bool isProcessElevated)
        {
            return PwmControlCapability.Evaluate(_options, _ioPort, _confirmedCpuFanControl, _experimentalCpuFanControl, isProcessElevated);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            RestoreAutomaticControl();
            _ioPort.Dispose();
            _disposed = true;
        }

        private void EnsureCanApplyManualPwm()
        {
            var capability = GetPwmControlCapability();
            if (!capability.CanApplyManualPwm)
            {
                throw new HardwareAccessException(capability.Summary);
            }
        }

        private PwmRegisterSet RequireWritableCpuFanControl()
        {
            if (_confirmedCpuFanControl != null)
            {
                return _confirmedCpuFanControl;
            }

            if (_options.EnableExperimentalRegisters && _experimentalCpuFanControl != null)
            {
                return _experimentalCpuFanControl;
            }

            if (_experimentalCpuFanControl != null)
            {
                throw new HardwareAccessException("Experimental NPB5/RPBNB IT8613E PWM registers exist but FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS is not enabled.");
            }

            if (_confirmedCpuFanControl == null)
            {
                throw new HardwareAccessException("No confirmed NPB5/RPBNB IT8613E PWM register map or Auto restore path exists.");
            }

            return _confirmedCpuFanControl;
        }

        private void CaptureReadOnlyDump(
            ISuperIoConfigPort configPort,
            int slot,
            ushort indexPort,
            ushort dataPort,
            RegisterDump dump)
        {
            var prefix = "SIO slot " + slot.ToString(CultureInfo.InvariantCulture) + " 0x" + indexPort.ToString("X2", CultureInfo.InvariantCulture);

            try
            {
                configPort.SelectSlot(slot);

                var beforeEnterChipId = configPort.ReadConfigWord(ChipIdRegister);
                dump.AddValue(prefix + " chip id before enter high", (ushort)((indexPort << 8) | ChipIdRegister), (byte)(beforeEnterChipId >> 8));
                dump.AddValue(prefix + " chip id before enter low", (ushort)((indexPort << 8) | (ChipIdRegister + 1)), (byte)(beforeEnterChipId & 0xFF));

                if (!(slot == 1 && beforeEnterChipId != 0x0000 && beforeEnterChipId != 0xFFFF))
                {
                    EnterIt87Configuration(configPort, indexPort);
                }
                else
                {
                    dump.AddNote(prefix + " appeared to already be in configuration mode before unlock.");
                }

                var chipId = configPort.ReadConfigWord(ChipIdRegister);
                dump.AddValue(prefix + " chip id high", (ushort)((indexPort << 8) | ChipIdRegister), (byte)(chipId >> 8));
                dump.AddValue(prefix + " chip id low", (ushort)((indexPort << 8) | (ChipIdRegister + 1)), (byte)(chipId & 0xFF));

                if (chipId != 0x8613)
                {
                    dump.AddNote(prefix + " did not report IT8613E; chip id was 0x" + chipId.ToString("X4", CultureInfo.InvariantCulture) + ".");
                    return;
                }

                configPort.FindBars();
                configPort.WriteConfigByte(DeviceSelectRegister, It87EnvironmentControllerLogicalDevice);
                var hwmBase = configPort.ReadConfigWord(BaseAddressRegister);
                Thread.Sleep(1);
                var hwmBaseVerify = configPort.ReadConfigWord(BaseAddressRegister);
                var version = (byte)(configPort.ReadConfigByte(ChipVersionRegister) & 0x0F);

                dump.AddValue(prefix + " IT8613E version", (ushort)((indexPort << 8) | ChipVersionRegister), version);
                dump.AddValue(prefix + " HWM base high", (ushort)((indexPort << 8) | BaseAddressRegister), (byte)(hwmBase >> 8));
                dump.AddValue(prefix + " HWM base low", (ushort)((indexPort << 8) | (BaseAddressRegister + 1)), (byte)(hwmBase & 0xFF));

                if (hwmBase != hwmBaseVerify || hwmBase < 0x100 || (hwmBase & 0xF007) != 0)
                {
                    dump.AddNote(prefix + " HWM base address is invalid or unstable: 0x" + hwmBase.ToString("X4", CultureInfo.InvariantCulture) + ".");
                    return;
                }

                configPort.WriteConfigByte(DeviceSelectRegister, It87GpioLogicalDevice);
                var gpioBase = configPort.ReadConfigWord(BaseAddressRegister + 2);
                Thread.Sleep(1);
                var gpioBaseVerify = configPort.ReadConfigWord(BaseAddressRegister + 2);
                dump.AddValue(prefix + " GPIO base high", (ushort)((indexPort << 8) | (BaseAddressRegister + 2)), (byte)(gpioBase >> 8));
                dump.AddValue(prefix + " GPIO base low", (ushort)((indexPort << 8) | (BaseAddressRegister + 3)), (byte)(gpioBase & 0xFF));

                if (gpioBase != gpioBaseVerify || gpioBase < 0x100 || (gpioBase & 0xF007) != 0)
                {
                    dump.AddNote(prefix + " GPIO base address is invalid or unstable: 0x" + gpioBase.ToString("X4", CultureInfo.InvariantCulture) + ".");
                }

                CaptureIt8613eHardwareMonitorRegisters(prefix, hwmBase, dump, configPort);
            }
            catch (Exception exception)
            {
                dump.AddNote(prefix + " PawnIO config read failed: " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                try
                {
                    ExitIt87Configuration(indexPort, dataPort);
                }
                catch (Exception exception)
                {
                    dump.AddNote(prefix + " configuration exit failed: " + exception.Message);
                }
            }
        }

        private void CaptureReadOnlyDump(ushort indexPort, ushort dataPort, RegisterDump dump)
        {
            var prefix = "SIO 0x" + indexPort.ToString("X2", CultureInfo.InvariantCulture);

            try
            {
                EnterIt87Configuration(indexPort);

                var chipId = ReadConfigWord(indexPort, dataPort, ChipIdRegister);
                dump.AddValue(prefix + " chip id high", (ushort)((indexPort << 8) | ChipIdRegister), (byte)(chipId >> 8));
                dump.AddValue(prefix + " chip id low", (ushort)((indexPort << 8) | (ChipIdRegister + 1)), (byte)(chipId & 0xFF));

                if (chipId != 0x8613)
                {
                    dump.AddNote(prefix + " did not report IT8613E; chip id was 0x" + chipId.ToString("X4", CultureInfo.InvariantCulture) + ".");
                    return;
                }

                SelectLogicalDevice(indexPort, dataPort, It87EnvironmentControllerLogicalDevice);
                var hwmBase = ReadConfigWord(indexPort, dataPort, BaseAddressRegister);
                Thread.Sleep(1);
                var hwmBaseVerify = ReadConfigWord(indexPort, dataPort, BaseAddressRegister);
                var version = (byte)(ReadConfigByte(indexPort, dataPort, ChipVersionRegister) & 0x0F);

                dump.AddValue(prefix + " IT8613E version", (ushort)((indexPort << 8) | ChipVersionRegister), version);
                dump.AddValue(prefix + " HWM base high", (ushort)((indexPort << 8) | BaseAddressRegister), (byte)(hwmBase >> 8));
                dump.AddValue(prefix + " HWM base low", (ushort)((indexPort << 8) | (BaseAddressRegister + 1)), (byte)(hwmBase & 0xFF));

                if (hwmBase != hwmBaseVerify || hwmBase < 0x100 || (hwmBase & 0xF007) != 0)
                {
                    dump.AddNote(prefix + " HWM base address is invalid or unstable: 0x" + hwmBase.ToString("X4", CultureInfo.InvariantCulture) + ".");
                    return;
                }

                SelectLogicalDevice(indexPort, dataPort, It87GpioLogicalDevice);
                var gpioBase = ReadConfigWord(indexPort, dataPort, BaseAddressRegister + 2);
                Thread.Sleep(1);
                var gpioBaseVerify = ReadConfigWord(indexPort, dataPort, BaseAddressRegister + 2);
                dump.AddValue(prefix + " GPIO base high", (ushort)((indexPort << 8) | (BaseAddressRegister + 2)), (byte)(gpioBase >> 8));
                dump.AddValue(prefix + " GPIO base low", (ushort)((indexPort << 8) | (BaseAddressRegister + 3)), (byte)(gpioBase & 0xFF));

                if (gpioBase != gpioBaseVerify || gpioBase < 0x100 || (gpioBase & 0xF007) != 0)
                {
                    dump.AddNote(prefix + " GPIO base address is invalid or unstable: 0x" + gpioBase.ToString("X4", CultureInfo.InvariantCulture) + ".");
                }

                CaptureIt8613eHardwareMonitorRegisters(prefix, hwmBase, dump);
            }
            catch (Exception exception)
            {
                dump.AddNote(prefix + " read failed: " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                try
                {
                    ExitIt87Configuration(indexPort, dataPort);
                }
                catch (Exception exception)
                {
                    dump.AddNote(prefix + " configuration exit failed: " + exception.Message);
                }
            }
        }

        private void CaptureIt8613eHardwareMonitorRegisters(string prefix, ushort hwmBase, RegisterDump dump)
        {
            CaptureIt8613eHardwareMonitorRegisters(prefix, hwmBase, dump, null);
        }

        private void CaptureIt8613eHardwareMonitorRegisters(string prefix, ushort hwmBase, RegisterDump dump, ISuperIoConfigPort? configPort)
        {
            var registers = new[]
            {
                (Name: "configuration", Register: (byte)0x00),
                (Name: "fan main control", Register: FanMainControlRegister),
                (Name: "fan1 pwm control", Register: (byte)0x15),
                (Name: "fan2 pwm control", Register: (byte)0x16),
                (Name: "fan3 pwm control", Register: (byte)0x17),
                (Name: "fan4 pwm control", Register: (byte)0x7F),
                (Name: "fan1 pwm duty ext", Register: (byte)0x63),
                (Name: "fan2 pwm duty ext", Register: (byte)0x6B),
                (Name: "fan3 pwm duty ext", Register: (byte)0x73),
                (Name: "fan4 pwm duty ext", Register: (byte)0x7B),
                (Name: "fan1 tach low", Register: (byte)0x0D),
                (Name: "fan1 tach high", Register: (byte)0x18),
                (Name: "fan2 tach low", Register: (byte)0x0E),
                (Name: "fan2 tach high", Register: (byte)0x19),
                (Name: "fan3 tach low", Register: (byte)0x0F),
                (Name: "fan3 tach high", Register: (byte)0x1A),
                (Name: "vendor id", Register: (byte)0x58)
            };

            foreach (var register in registers)
            {
                var value = ReadHardwareMonitorByte(hwmBase, register.Register, configPort);
                dump.AddValue(prefix + " hwm 0x" + hwmBase.ToString("X4", CultureInfo.InvariantCulture) + " " + register.Name, (ushort)(0xA000 | register.Register), value);

                if (value == 0xCC)
                {
                    dump.AddNote(prefix + " register " + register.Name + " currently equals 0xCC, matching BIOS raw PWM 204.");
                }
            }
        }

        private byte ReadHardwareMonitorByte(ushort hwmBase, byte register)
        {
            return ReadHardwareMonitorByte(hwmBase, register, null);
        }

        private byte ReadHardwareMonitorByte(ushort hwmBase, byte register, ISuperIoConfigPort? configPort)
        {
            var addressPort = (ushort)(hwmBase + AddressRegisterOffset);
            var dataPort = (ushort)(hwmBase + DataRegisterOffset);

            if (configPort != null)
            {
                configPort.WriteIoPortByte(addressPort, register);
                return configPort.ReadIoPortByte(dataPort);
            }

            _ioPort.WriteByte(addressPort, register);
            return _ioPort.ReadByte(dataPort);
        }

        private byte ReadConfigByte(ushort indexPort, ushort dataPort, byte register)
        {
            _ioPort.WriteByte(indexPort, register);
            return _ioPort.ReadByte(dataPort);
        }

        private ushort ReadConfigWord(ushort indexPort, ushort dataPort, byte register)
        {
            var high = ReadConfigByte(indexPort, dataPort, register);
            var low = ReadConfigByte(indexPort, dataPort, (byte)(register + 1));
            return (ushort)((high << 8) | low);
        }

        private void SelectLogicalDevice(ushort indexPort, ushort dataPort, byte logicalDevice)
        {
            _ioPort.WriteByte(indexPort, DeviceSelectRegister);
            _ioPort.WriteByte(dataPort, logicalDevice);
        }

        private void EnterIt87Configuration(ushort indexPort)
        {
            _ioPort.WriteByte(indexPort, 0x87);
            _ioPort.WriteByte(indexPort, 0x01);
            _ioPort.WriteByte(indexPort, 0x55);
            _ioPort.WriteByte(indexPort, indexPort == RegisterMap.AlternateSuperIoIndexPort ? (byte)0xAA : (byte)0x55);
        }

        private static void EnterIt87Configuration(ISuperIoConfigPort configPort, ushort indexPort)
        {
            configPort.WriteIoPortByte(indexPort, 0x87);
            configPort.WriteIoPortByte(indexPort, 0x01);
            configPort.WriteIoPortByte(indexPort, 0x55);
            configPort.WriteIoPortByte(indexPort, indexPort == RegisterMap.AlternateSuperIoIndexPort ? (byte)0xAA : (byte)0x55);
        }

        private void ExitIt87Configuration(ushort indexPort, ushort dataPort)
        {
            if (indexPort == RegisterMap.AlternateSuperIoIndexPort)
            {
                return;
            }

            _ioPort.WriteByte(indexPort, ConfigurationControlRegister);
            _ioPort.WriteByte(dataPort, RegisterMap.ExitConfigurationModeValue);
        }

        private void WriteRegisterWithSnapshot(RegisterDefinition register, byte value)
        {
            if (register.Confidence != RegisterConfidence.Confirmed && !_options.EnableExperimentalRegisters)
            {
                throw new HardwareAccessException("Refusing to write unconfirmed register: " + register.Name + ".");
            }

            if (_snapshottedRegisters.Add(GetSnapshotKey(register)))
            {
                var oldValue = ReadRegister(register);
                _restoreStack.Push(new RegisterWriteSnapshot(register, oldValue));
            }

            WriteRegister(register, value);
        }

        private static ushort GetSnapshotKey(RegisterDefinition register)
        {
            return (ushort)(((ushort)register.AddressSpace << 12) | register.Address);
        }

        private byte ReadRegister(RegisterDefinition register)
        {
            try
            {
                if (register.AddressSpace == RegisterAddressSpace.It8613eHardwareMonitor)
                {
                    return ReadHardwareMonitorByte(ResolveHwmBase(), (byte)register.Address, RequireConfigPort());
                }

                return _ioPort.ReadByte(register.Address);
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("Failed to read register " + register.Name + ".", exception);
            }
        }

        private void WriteRegister(RegisterDefinition register, byte value)
        {
            try
            {
                if (register.AddressSpace == RegisterAddressSpace.It8613eHardwareMonitor)
                {
                    WriteHardwareMonitorByte(ResolveHwmBase(), (byte)register.Address, value, RequireConfigPort());
                    return;
                }

                _ioPort.WriteByte(register.Address, value);
            }
            catch (Exception exception)
            {
                _log.Error("Hardware register write failed for " + register.Name + ".", exception);
                throw new HardwareAccessException("Failed to write register " + register.Name + ".", exception);
            }
        }

        private void WriteHardwareMonitorByte(ushort hwmBase, byte register, byte value, ISuperIoConfigPort configPort)
        {
            var addressPort = (ushort)(hwmBase + AddressRegisterOffset);
            var dataPort = (ushort)(hwmBase + DataRegisterOffset);

            configPort.WriteIoPortByte(addressPort, register);
            configPort.WriteIoPortByte(dataPort, value);
            configPort.ReadIoPortByte(addressPort);
        }

        private ushort ResolveHwmBase()
        {
            if (_hwmBase.HasValue)
            {
                return _hwmBase.Value;
            }

            var configPort = RequireConfigPort();

            try
            {
                configPort.SelectSlot(0);
                EnterIt87Configuration(configPort, RegisterMap.PrimarySuperIoIndexPort);

                var chipId = configPort.ReadConfigWord(ChipIdRegister);
                if (chipId != 0x8613)
                {
                    throw new HardwareAccessException("Expected IT8613E chip id 0x8613 at Super I/O 0x2E, got 0x" + chipId.ToString("X4", CultureInfo.InvariantCulture) + ".");
                }

                configPort.FindBars();
                configPort.WriteConfigByte(DeviceSelectRegister, It87EnvironmentControllerLogicalDevice);
                var hwmBase = configPort.ReadConfigWord(BaseAddressRegister);
                Thread.Sleep(1);
                var hwmBaseVerify = configPort.ReadConfigWord(BaseAddressRegister);

                if (hwmBase != hwmBaseVerify || hwmBase < 0x100 || (hwmBase & 0xF007) != 0)
                {
                    throw new HardwareAccessException("IT8613E HWM base address is invalid or unstable: 0x" + hwmBase.ToString("X4", CultureInfo.InvariantCulture) + ".");
                }

                _hwmBase = hwmBase;
                return hwmBase;
            }
            finally
            {
                ExitIt87Configuration(RegisterMap.PrimarySuperIoIndexPort, RegisterMap.PrimarySuperIoDataPort);
            }
        }

        private ISuperIoConfigPort RequireConfigPort()
        {
            var configPort = _ioPort as ISuperIoConfigPort;
            if (configPort == null)
            {
                throw new HardwareAccessException("LibreHardwareMonitor/PawnIO LpcPort config access is required for IT8613E indexed HWM registers.");
            }

            return configPort;
        }
    }
}
