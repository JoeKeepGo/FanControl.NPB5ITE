using System;
using System.Linq;
using FanControl.NPB5ITE.Diagnostics;
using FanControl.NPB5ITE.Hardware;
using FanControl.NPB5ITE.HwInfo;
using FanControl.NPB5ITE.Logging;
using FanControl.NPB5ITE.Safety;

namespace FanControl.NPB5ITE.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            MinimumPwmIsClamped();
            CriticalTemperatureForcesFullSpeed();
            MissingRpmRestoresAuto();
            DisabledWritesRestoreAuto();
            ZeroPwmRestoresAuto();
            LowPwmRequiresExplicitOptIn();
            ExperimentalLowPwmAllowsTenPercentMinimum();
            SafetyPolicyRejectsInvalidMinimumPwm();
            EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn();
            RegisterDumpComparerFindsChangedValues();
            HwInfoRawRpmIsParsed();
            HwInfoFormattedRpmFallbackIsParsed();
            HwInfoSnapshotSelectsCpuFanRpm();
            PwmCapabilityReportsDefaultBlockers();
            PwmCapabilityAllowsConfirmedMapWithIoAndOptIn();
            PwmCapabilityAllowsExperimentalMapWithExplicitOptIn();
            HardwareStillRefusesWithoutConfirmedRegisters();
            ManualPwmWritesModeAndRawDutyWithSnapshot();
            RepeatedManualPwmDoesNotRewriteHardware();
            PwmRaw127IsAboutHalfDuty();
            PwmPercent50ConvertsToRaw127();

            Console.WriteLine("All tests passed.");
            return 0;
        }

        private static void MinimumPwmIsClamped()
        {
            var policy = CreatePolicy(allowManualWithoutTemperature: true);
            var decision = policy.Evaluate(new FanSafetyInputs
            {
                RequestedPwmPercent = 5.0f,
                FanRpmReadSucceeded = true,
                HardwareWritesEnabled = true
            });

            AssertEqual(FanSafetyAction.ApplyManualPwm, decision.Action, nameof(MinimumPwmIsClamped));
            AssertEqual(35.0f, decision.PwmPercent.GetValueOrDefault(), nameof(MinimumPwmIsClamped));
        }

        private static void CriticalTemperatureForcesFullSpeed()
        {
            var policy = CreatePolicy(allowManualWithoutTemperature: false);
            var decision = policy.Evaluate(new FanSafetyInputs
            {
                RequestedPwmPercent = 40.0f,
                CpuTemperatureCelsius = 85.0f,
                FanRpmReadSucceeded = true,
                HardwareWritesEnabled = true
            });

            AssertEqual(FanSafetyAction.ApplyFullSpeed, decision.Action, nameof(CriticalTemperatureForcesFullSpeed));
            AssertEqual(100.0f, decision.PwmPercent.GetValueOrDefault(), nameof(CriticalTemperatureForcesFullSpeed));
        }

        private static void MissingRpmRestoresAuto()
        {
            var policy = CreatePolicy(allowManualWithoutTemperature: true);
            var decision = policy.Evaluate(new FanSafetyInputs
            {
                RequestedPwmPercent = 70.0f,
                FanRpmReadSucceeded = false,
                HardwareWritesEnabled = true
            });

            AssertEqual(FanSafetyAction.RestoreAutomaticControl, decision.Action, nameof(MissingRpmRestoresAuto));
        }

        private static void DisabledWritesRestoreAuto()
        {
            var policy = CreatePolicy(allowManualWithoutTemperature: true);
            var decision = policy.Evaluate(new FanSafetyInputs
            {
                RequestedPwmPercent = 70.0f,
                FanRpmReadSucceeded = true,
                HardwareWritesEnabled = false
            });

            AssertEqual(FanSafetyAction.RestoreAutomaticControl, decision.Action, nameof(DisabledWritesRestoreAuto));
        }

        private static void ZeroPwmRestoresAuto()
        {
            var policy = CreatePolicy(allowManualWithoutTemperature: true);
            var decision = policy.Evaluate(new FanSafetyInputs
            {
                RequestedPwmPercent = 0.0f,
                FanRpmReadSucceeded = true,
                HardwareWritesEnabled = true
            });

            AssertEqual(FanSafetyAction.RestoreAutomaticControl, decision.Action, nameof(ZeroPwmRestoresAuto));
            AssertEqual(false, decision.PwmPercent.HasValue, nameof(ZeroPwmRestoresAuto));
        }

        private static void LowPwmRequiresExplicitOptIn()
        {
            try
            {
                new FanSafetyPolicy(new FanSafetyOptions
                {
                    MinimumPwmPercent = 10.0f,
                    CriticalCpuTemperatureCelsius = 85.0f,
                    AllowManualWithoutCpuTemperature = true
                });
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException(nameof(LowPwmRequiresExplicitOptIn) + " failed. Expected ArgumentOutOfRangeException.");
        }

        private static void ExperimentalLowPwmAllowsTenPercentMinimum()
        {
            var policy = new FanSafetyPolicy(new FanSafetyOptions
            {
                MinimumPwmPercent = 10.0f,
                CriticalCpuTemperatureCelsius = 85.0f,
                AllowLowPwm = true,
                AllowManualWithoutCpuTemperature = true
            });

            var decision = policy.Evaluate(new FanSafetyInputs
            {
                RequestedPwmPercent = 5.0f,
                FanRpmReadSucceeded = true,
                HardwareWritesEnabled = true
            });

            AssertEqual(FanSafetyAction.ApplyManualPwm, decision.Action, nameof(ExperimentalLowPwmAllowsTenPercentMinimum));
            AssertEqual(10.0f, decision.PwmPercent.GetValueOrDefault(), nameof(ExperimentalLowPwmAllowsTenPercentMinimum));
        }

        private static void SafetyPolicyRejectsInvalidMinimumPwm()
        {
            try
            {
                new FanSafetyPolicy(new FanSafetyOptions
                {
                    MinimumPwmPercent = 101.0f,
                    CriticalCpuTemperatureCelsius = 85.0f,
                    AllowLowPwm = true,
                    AllowManualWithoutCpuTemperature = true
                });
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException(nameof(SafetyPolicyRejectsInvalidMinimumPwm) + " failed. Expected ArgumentOutOfRangeException.");
        }


        private static void EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn()
        {
            WithEnvironment("FANCONTROL_NPB5ITE_ALLOW_LOW_PWM", null, () =>
            {
                WithEnvironment("FANCONTROL_NPB5ITE_MIN_PWM_PERCENT", "10", () =>
                {
                    var options = PluginOptions.FromEnvironment();

                    AssertEqual(false, options.AllowLowPwm, nameof(EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn));
                    AssertEqual(35.0f, options.MinimumPwmPercent, nameof(EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn));
                });
            });

            WithEnvironment("FANCONTROL_NPB5ITE_ALLOW_LOW_PWM", "1", () =>
            {
                WithEnvironment("FANCONTROL_NPB5ITE_MIN_PWM_PERCENT", "5", () =>
                {
                    var options = PluginOptions.FromEnvironment();

                    AssertEqual(true, options.AllowLowPwm, nameof(EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn));
                    AssertEqual(10.0f, options.MinimumPwmPercent, nameof(EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn));
                });
            });

            WithEnvironment("FANCONTROL_NPB5ITE_ALLOW_LOW_PWM", "1", () =>
            {
                WithEnvironment("FANCONTROL_NPB5ITE_MIN_PWM_PERCENT", "200", () =>
                {
                    var options = PluginOptions.FromEnvironment();

                    AssertEqual(100.0f, options.MinimumPwmPercent, nameof(EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn));
                });
            });
        }

        private static void RegisterDumpComparerFindsChangedValues()
        {
            var before = new RegisterDump("Auto", "Test");
            before.AddValue("Example", 0x0020, 0x10);

            var after = new RegisterDump("Manual80", "Test");
            after.AddValue("Example", 0x0020, 0x20);

            var diff = RegisterDumpComparer.Compare(before, after);

            AssertEqual(1, diff.Changes.Count, nameof(RegisterDumpComparerFindsChangedValues));
            AssertEqual((byte)0x10, diff.Changes[0].Before, nameof(RegisterDumpComparerFindsChangedValues));
            AssertEqual((byte)0x20, diff.Changes[0].After, nameof(RegisterDumpComparerFindsChangedValues));
        }

        private static void HwInfoRawRpmIsParsed()
        {
            var entry = HwInfoVsbEntry.FromValues(
                14,
                "Shenzhen Meigao Electronic Equipment Co.,Ltd RPBNB (ITE IT8613E)",
                "CPU",
                "3,750 RPM",
                "3750");

            AssertEqual(true, entry.IsRpm, nameof(HwInfoRawRpmIsParsed));
            AssertEqual(3750.0f, entry.Rpm.GetValueOrDefault(), nameof(HwInfoRawRpmIsParsed));
        }

        private static void HwInfoFormattedRpmFallbackIsParsed()
        {
            var entry = HwInfoVsbEntry.FromValues(
                14,
                "ITE IT8613E",
                "CPU",
                "3,750 RPM",
                string.Empty);

            AssertEqual(true, entry.IsRpm, nameof(HwInfoFormattedRpmFallbackIsParsed));
            AssertEqual(3750.0f, entry.Rpm.GetValueOrDefault(), nameof(HwInfoFormattedRpmFallbackIsParsed));
        }

        private static void HwInfoSnapshotSelectsCpuFanRpm()
        {
            var snapshot = HwInfoVsbSnapshot.FromEntries(new[]
            {
                HwInfoVsbEntry.FromValues(1, "ITE IT8613E", "System", "1000 RPM", "1000"),
                HwInfoVsbEntry.FromValues(2, "ITE IT8613E", "CPU", "3750 RPM", "3750")
            });

            var reading = snapshot.FindBestCpuFanRpm();

            AssertEqual(true, reading.Succeeded, nameof(HwInfoSnapshotSelectsCpuFanRpm));
            AssertEqual(3750.0f, reading.Rpm.GetValueOrDefault(), nameof(HwInfoSnapshotSelectsCpuFanRpm));
        }

        private static void PwmCapabilityReportsDefaultBlockers()
        {
            WithEnvironment("FANCONTROL_NPB5ITE_ENABLE_WRITES", null, () =>
            {
                var capability = PwmControlCapability.Evaluate(PluginOptions.FromEnvironment(), new FakeIoPort(isAvailable: false));

                AssertEqual(false, capability.CanApplyManualPwm, nameof(PwmCapabilityReportsDefaultBlockers));
                AssertEqual(true, capability.Blockers.Contains(PwmControlBlocker.HardwareWritesDisabled), nameof(PwmCapabilityReportsDefaultBlockers));
                AssertEqual(true, capability.Blockers.Contains(PwmControlBlocker.ExperimentalRegistersDisabled), nameof(PwmCapabilityReportsDefaultBlockers));
                AssertEqual(true, capability.Blockers.Contains(PwmControlBlocker.NoIoPortProvider), nameof(PwmCapabilityReportsDefaultBlockers));
                AssertEqual(true, capability.Blockers.Contains(PwmControlBlocker.NoConfirmedRegisterMap), nameof(PwmCapabilityReportsDefaultBlockers));
            });
        }

        private static void PwmCapabilityAllowsConfirmedMapWithIoAndOptIn()
        {
            WithEnvironment("FANCONTROL_NPB5ITE_ENABLE_WRITES", "1", () =>
            {
                var capability = PwmControlCapability.Evaluate(
                    PluginOptions.FromEnvironment(),
                    new FakeIoPort(isAvailable: true),
                    CreateTestPwmRegisterSet());

                AssertEqual(true, capability.CanApplyManualPwm, nameof(PwmCapabilityAllowsConfirmedMapWithIoAndOptIn));
            });
        }

        private static void PwmCapabilityAllowsExperimentalMapWithExplicitOptIn()
        {
            WithEnvironment("FANCONTROL_NPB5ITE_ENABLE_WRITES", "1", () =>
            {
                WithEnvironment("FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS", "1", () =>
                {
                    var capability = PwmControlCapability.Evaluate(
                        PluginOptions.FromEnvironment(),
                        new FakeIoPort(isAvailable: true),
                        confirmedCpuFanControl: null,
                        experimentalCpuFanControl: CreateTestExperimentalPwmRegisterSet(),
                        isProcessElevated: true);

                    AssertEqual(true, capability.CanApplyManualPwm, nameof(PwmCapabilityAllowsExperimentalMapWithExplicitOptIn));
                    AssertEqual(3, capability.ExperimentalRegisterCount, nameof(PwmCapabilityAllowsExperimentalMapWithExplicitOptIn));
                });
            });
        }

        private static void HardwareStillRefusesWithoutConfirmedRegisters()
        {
            WithEnvironment("FANCONTROL_NPB5ITE_ENABLE_WRITES", "1", () =>
            {
                using (var hardware = new Ite8613fIo(new FakeIoPort(isAvailable: true), PluginOptions.FromEnvironment(), new PluginLog()))
                {
                    try
                    {
                        hardware.ApplyManualPwm(50.0f);
                    }
                    catch (HardwareAccessException exception)
                    {
                        AssertContains("FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS is not enabled", exception.Message, nameof(HardwareStillRefusesWithoutConfirmedRegisters));
                        return;
                    }
                }

                throw new InvalidOperationException(nameof(HardwareStillRefusesWithoutConfirmedRegisters) + " failed. Expected HardwareAccessException.");
            });
        }

        private static void ManualPwmWritesModeAndRawDutyWithSnapshot()
        {
            WithEnvironment("FANCONTROL_NPB5ITE_ENABLE_WRITES", "1", () =>
            {
                var ioPort = new FakeIoPort(isAvailable: true);
                using (var hardware = new Ite8613fIo(ioPort, PluginOptions.FromEnvironment(), new PluginLog(), CreateTestPwmRegisterSet()))
                {
                    hardware.ApplyManualPwm(50.0f);
                    AssertEqual(2, ioPort.Writes.Count, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((ushort)0x0100, ioPort.Writes[0].Port, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((byte)0x01, ioPort.Writes[0].Value, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((ushort)0x0101, ioPort.Writes[1].Port, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((byte)0x7F, ioPort.Writes[1].Value, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));

                    hardware.RestoreAutomaticControl();
                    AssertEqual(4, ioPort.Writes.Count, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((ushort)0x0101, ioPort.Writes[2].Port, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((byte)0x00, ioPort.Writes[2].Value, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((ushort)0x0100, ioPort.Writes[3].Port, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                    AssertEqual((byte)0x00, ioPort.Writes[3].Value, nameof(ManualPwmWritesModeAndRawDutyWithSnapshot));
                }
            });
        }

        private static void RepeatedManualPwmDoesNotRewriteHardware()
        {
            WithEnvironment("FANCONTROL_NPB5ITE_ENABLE_WRITES", "1", () =>
            {
                var ioPort = new FakeIoPort(isAvailable: true);
                using (var hardware = new Ite8613fIo(ioPort, PluginOptions.FromEnvironment(), new PluginLog(), CreateTestPwmRegisterSet()))
                {
                    hardware.ApplyManualPwm(50.0f);
                    hardware.ApplyManualPwm(50.0f);

                    AssertEqual(2, ioPort.Writes.Count, nameof(RepeatedManualPwmDoesNotRewriteHardware));

                    hardware.RestoreAutomaticControl();
                    AssertEqual(4, ioPort.Writes.Count, nameof(RepeatedManualPwmDoesNotRewriteHardware));
                }
            });
        }

        private static void PwmRaw127IsAboutHalfDuty()
        {
            AssertEqual(49.80392f, Round(PwmDutyCycle.RawToPercent(127)), nameof(PwmRaw127IsAboutHalfDuty));
        }

        private static void PwmPercent50ConvertsToRaw127()
        {
            AssertEqual((byte)127, PwmDutyCycle.PercentToRaw(50.0f), nameof(PwmPercent50ConvertsToRaw127));
            AssertEqual((byte)204, PwmDutyCycle.PercentToRaw(80.0f), nameof(PwmPercent50ConvertsToRaw127));
            AssertEqual((byte)153, PwmDutyCycle.PercentToRaw(60.0f), nameof(PwmPercent50ConvertsToRaw127));
        }

        private static FanSafetyPolicy CreatePolicy(bool allowManualWithoutTemperature)
        {
            return new FanSafetyPolicy(new FanSafetyOptions
            {
                MinimumPwmPercent = 35.0f,
                CriticalCpuTemperatureCelsius = 85.0f,
                AllowLowPwm = false,
                AllowManualWithoutCpuTemperature = allowManualWithoutTemperature
            });
        }

        private static PwmRegisterSet CreateTestPwmRegisterSet()
        {
            var modeRegister = new RegisterDefinition("Test CPU fan mode", 0x0100, RegisterConfidence.Confirmed, "Test only.");
            var dutyRegister = new RegisterDefinition("Test CPU fan duty", 0x0101, RegisterConfidence.Confirmed, "Test only.");

            return new PwmRegisterSet(
                modeRegister,
                0x01,
                dutyRegister,
                modeRegister,
                0x00);
        }

        private static PwmRegisterSet CreateTestExperimentalPwmRegisterSet()
        {
            var modeRegister = new RegisterDefinition("Test experimental CPU fan mode", 0x16, RegisterAddressSpace.It8613eHardwareMonitor, RegisterConfidence.Experimental, "Test only.");
            var dutyRegister = new RegisterDefinition("Test experimental CPU fan duty", 0x6B, RegisterAddressSpace.It8613eHardwareMonitor, RegisterConfidence.Experimental, "Test only.");

            return new PwmRegisterSet(
                modeRegister,
                0x00,
                dutyRegister,
                modeRegister,
                0x00);
        }

        private static void AssertEqual<T>(T expected, T actual, string testName)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException(testName + " failed. Expected " + expected + ", got " + actual + ".");
            }
        }

        private static void AssertContains(string expected, string actual, string testName)
        {
            if (actual == null || !actual.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(testName + " failed. Expected '" + actual + "' to contain '" + expected + "'.");
            }
        }

        private static float Round(float value)
        {
            return MathF.Round(value, 5);
        }

        private static void WithEnvironment(string name, string? value, Action action)
        {
            var original = Environment.GetEnvironmentVariable(name);
            try
            {
                Environment.SetEnvironmentVariable(name, value);
                action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, original);
            }
        }

        private sealed class FakeIoPort : IIoPort
        {
            private readonly System.Collections.Generic.Dictionary<ushort, byte> _values = new System.Collections.Generic.Dictionary<ushort, byte>();

            public FakeIoPort(bool isAvailable)
            {
                IsAvailable = isAvailable;
            }

            public bool IsAvailable { get; }

            public System.Collections.Generic.List<(ushort Port, byte Value)> Writes { get; } = new System.Collections.Generic.List<(ushort Port, byte Value)>();

            public byte ReadByte(ushort port)
            {
                return _values.TryGetValue(port, out var value) ? value : (byte)0;
            }

            public void WriteByte(ushort port, byte value)
            {
                _values[port] = value;
                Writes.Add((port, value));
            }

            public void Dispose()
            {
            }
        }
    }
}
