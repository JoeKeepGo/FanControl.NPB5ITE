using System;
using System.Collections.Generic;
using System.Reflection;

namespace FanControl.NPB5ITE.Hardware
{
    public sealed class LibreHardwareMonitorLpcIoPort : IIoPort, ISuperIoConfigPort
    {
        private readonly object? _lpcIo;
        private readonly object?[] _lpcPorts = new object?[2];
        private readonly MethodInfo? _readPort;
        private readonly MethodInfo? _writePort;
        private readonly MethodInfo? _lpcPortReadIoPort;
        private readonly MethodInfo? _lpcPortWriteIoPort;
        private readonly MethodInfo? _lpcPortFindBars;
        private readonly MethodInfo? _lpcPortReadByte;
        private readonly MethodInfo? _lpcPortReadWord;
        private readonly MethodInfo? _lpcPortWriteByte;
        private readonly MethodInfo? _lpcPortClose;
        private readonly MethodInfo? _close;
        private readonly string? _unavailableReason;
        private readonly string _configAccessStatus = "Not initialized.";
        private int _selectedSlot;

        public LibreHardwareMonitorLpcIoPort()
        {
            try
            {
                var type = Type.GetType("LibreHardwareMonitor.PawnIo.LpcIo, LibreHardwareMonitorLib", throwOnError: false);
                if (type == null)
                {
                    _unavailableReason = "LibreHardwareMonitor PawnIO LpcIo type is not available.";
                    _configAccessStatus = "LpcIo type is not available.";
                    return;
                }

                _lpcIo = Activator.CreateInstance(type);
                _readPort = FindInstanceMethod(type, "ReadPort", typeof(ushort));
                _writePort = FindInstanceMethod(type, "WritePort", typeof(ushort), typeof(byte));
                _close = FindInstanceMethod(type, "Close");

                var lpcPortType = Type.GetType("LibreHardwareMonitor.Hardware.Motherboard.Lpc.LpcPort, LibreHardwareMonitorLib", throwOnError: false);
                if (lpcPortType != null)
                {
                    _lpcPorts[0] = Activator.CreateInstance(lpcPortType, new object[] { RegisterMap.PrimarySuperIoIndexPort, RegisterMap.PrimarySuperIoDataPort });
                    _lpcPorts[1] = Activator.CreateInstance(lpcPortType, new object[] { RegisterMap.AlternateSuperIoIndexPort, RegisterMap.AlternateSuperIoDataPort });
                    _lpcPortReadIoPort = FindInstanceMethod(lpcPortType, "ReadIoPort", typeof(ushort));
                    _lpcPortWriteIoPort = FindInstanceMethod(lpcPortType, "WriteIoPort", typeof(ushort), typeof(byte));
                    _lpcPortFindBars = FindInstanceMethod(lpcPortType, "FindBars");
                    _lpcPortReadByte = FindInstanceMethod(lpcPortType, "ReadByte", typeof(byte));
                    _lpcPortReadWord = FindInstanceMethod(lpcPortType, "ReadWord", typeof(byte));
                    _lpcPortWriteByte = FindInstanceMethod(lpcPortType, "WriteByte", typeof(byte), typeof(byte));
                    _lpcPortClose = FindInstanceMethod(lpcPortType, "Close");
                    _configAccessStatus = "LpcPort type found: " + lpcPortType.FullName + ". Missing methods: " + string.Join(", ", MissingConfigMethods());
                }
                else
                {
                    _configAccessStatus = "LpcPort type LibreHardwareMonitor.Hardware.Motherboard.Lpc.LpcPort was not found.";
                }

                if (_lpcIo == null || _readPort == null || _writePort == null)
                {
                    _unavailableReason = "LibreHardwareMonitor PawnIO LpcIo methods are not available.";
                    _lpcIo = null;
                }
            }
            catch (Exception exception)
            {
                _unavailableReason = "LibreHardwareMonitor PawnIO LpcIo could not be initialized: " + exception.Message;
            }
        }

        public bool IsAvailable => _lpcIo != null && _readPort != null && _writePort != null;

        public bool HasConfigAccess =>
            _lpcPorts[0] != null &&
            _lpcPorts[1] != null &&
            _lpcPortReadIoPort != null &&
            _lpcPortWriteIoPort != null &&
            _lpcPortFindBars != null &&
            _lpcPortReadByte != null &&
            _lpcPortReadWord != null &&
            _lpcPortWriteByte != null;

        public string? UnavailableReason => _unavailableReason;

        public string ConfigAccessStatus => _configAccessStatus;

        public byte ReadByte(ushort port)
        {
            EnsureAvailable();

            try
            {
                return (byte)_readPort!.Invoke(_lpcIo, new object[] { port })!;
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor PawnIO failed to read port 0x" + port.ToString("X4") + ".", exception);
            }
        }

        public void WriteByte(ushort port, byte value)
        {
            EnsureAvailable();

            try
            {
                _writePort!.Invoke(_lpcIo, new object[] { port, value });
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor PawnIO failed to write port 0x" + port.ToString("X4") + ".", exception);
            }
        }

        public void SelectSlot(int slot)
        {
            EnsureConfigAccess();

            if (slot < 0 || slot >= _lpcPorts.Length)
            {
                throw new HardwareAccessException("Invalid LPC slot " + slot + ".");
            }

            _selectedSlot = slot;
        }

        public byte ReadIoPortByte(ushort port)
        {
            EnsureConfigAccess();

            try
            {
                return (byte)_lpcPortReadIoPort!.Invoke(SelectedConfigPort(), new object[] { port })!;
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor LpcPort failed to read I/O port 0x" + port.ToString("X4") + ".", exception);
            }
        }

        public void WriteIoPortByte(ushort port, byte value)
        {
            EnsureConfigAccess();

            try
            {
                _lpcPortWriteIoPort!.Invoke(SelectedConfigPort(), new object[] { port, value });
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor LpcPort failed to write I/O port 0x" + port.ToString("X4") + ".", exception);
            }
        }

        public void FindBars()
        {
            EnsureConfigAccess();

            try
            {
                _lpcPortFindBars!.Invoke(SelectedConfigPort(), Array.Empty<object>());
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor PawnIO failed to find LPC BARs.", exception);
            }
        }

        public byte ReadConfigByte(byte register)
        {
            EnsureConfigAccess();

            try
            {
                return (byte)_lpcPortReadByte!.Invoke(SelectedConfigPort(), new object[] { register })!;
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor PawnIO failed to read LPC config register 0x" + register.ToString("X2") + ".", exception);
            }
        }

        public ushort ReadConfigWord(byte register)
        {
            EnsureConfigAccess();

            try
            {
                return (ushort)_lpcPortReadWord!.Invoke(SelectedConfigPort(), new object[] { register })!;
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor PawnIO failed to read LPC config word 0x" + register.ToString("X2") + ".", exception);
            }
        }

        public void WriteConfigByte(byte register, byte value)
        {
            EnsureConfigAccess();

            try
            {
                _lpcPortWriteByte!.Invoke(SelectedConfigPort(), new object[] { register, value });
            }
            catch (Exception exception)
            {
                throw new HardwareAccessException("LibreHardwareMonitor PawnIO failed to write LPC config register 0x" + register.ToString("X2") + ".", exception);
            }
        }

        public void Dispose()
        {
            try
            {
                _close?.Invoke(_lpcIo, Array.Empty<object>());
                foreach (var lpcPort in _lpcPorts)
                {
                    if (lpcPort != null)
                    {
                        _lpcPortClose?.Invoke(lpcPort, Array.Empty<object>());
                    }
                }
            }
            catch
            {
                // Best-effort release of an optional low-level provider.
            }
        }

        private void EnsureAvailable()
        {
            if (!IsAvailable)
            {
                throw new HardwareAccessException(_unavailableReason ?? "LibreHardwareMonitor PawnIO LpcIo is unavailable.");
            }
        }

        private void EnsureConfigAccess()
        {
            EnsureAvailable();

            if (!HasConfigAccess)
            {
                throw new HardwareAccessException("LibreHardwareMonitor PawnIO LpcIo config methods are unavailable.");
            }
        }

        private object SelectedConfigPort()
        {
            return _lpcPorts[_selectedSlot]!;
        }

        private IEnumerable<string> MissingConfigMethods()
        {
            if (_lpcPorts[0] == null)
            {
                yield return "primary LpcPort ctor";
            }

            if (_lpcPorts[1] == null)
            {
                yield return "alternate LpcPort ctor";
            }

            if (_lpcPortReadIoPort == null)
            {
                yield return "ReadIoPort";
            }

            if (_lpcPortWriteIoPort == null)
            {
                yield return "WriteIoPort";
            }

            if (_lpcPortFindBars == null)
            {
                yield return "FindBars";
            }

            if (_lpcPortReadByte == null)
            {
                yield return "ReadByte";
            }

            if (_lpcPortReadWord == null)
            {
                yield return "ReadWord";
            }

            if (_lpcPortWriteByte == null)
            {
                yield return "WriteByte";
            }
        }

        private static MethodInfo? FindInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            return type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: parameterTypes,
                modifiers: null);
        }
    }
}
