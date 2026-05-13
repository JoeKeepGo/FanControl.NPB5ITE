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
            TestedNpb5HardwareDefaultsEnableWrites();
            DisableWritesOverridesTestedHardwareDefaults();
            UnknownHardwareDefaultsRemainReadOnly();
            EnvironmentMinimumPwmCannotBypassDefaultWithoutLowPwmOptIn();
            RegisterDumpComparerFindsChangedValues();
            HwInfoRawRpmIsParsed();
            HwInfoFormattedRpmFallbackIsParsed();
            HwInfoSnapshotSelectsCpuFanRpm();
            PwmCapabilityReportsDefaultBlockers();
            PwmCapabilityAllowsConfirmedMapWithIoAndOptIn();
            PwmCapabilityAllowsExperimentalMapWithExplicitOptIn();
            HardwareStillRefusesWithoutConfirmedRegisters();
            DirectIteTachCountReadsCpuFanRpm();
            DirectIteTachRejectsInvalidCount();
            CompositeFanRpmSourceUsesDirectSourceBeforeFallbacks();
            FanRpmReadingRoundsToWholeRpm();
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

        private static void TestedNpb5HardwareDefaultsEnableWrites()
        {
            WithoutPluginEnvironment(() =>
            {
                var options = PluginOptions.FromEnvironment(CreateTestedHardwareIdentity());

                AssertEqual(true, options.UsesTestedHardwareDefaults, nameof(TestedNpb5HardwareDefaultsEnableWrites));
                AssertEqual(true, options.EnableHardwareWrites, nameof(TestedNpb5HardwareDefaultsEnableWrites));
                AssertEqual(true, options.EnableExperimentalRegisters, nameof(TestedNpb5HardwareDefaultsEnableWrites));
                AssertEqual(false, options.AllowLowPwm, nameof(TestedNpb5HardwareDefaultsEnableWrites));
                AssertEqual(35.0f, options.MinimumPwmPercent, nameof(TestedNpb5HardwareDefaultsEnableWrites));
            });
        }

        private static void DisableWritesOverridesTestedHardwareDefaults()
        {
            WithoutPluginEnvironment(() =>
            {
                WithEnvironment("FANCONTROL_NPB5ITE_DISABLE_WRITES", "1", () =>
                {
                    var options = PluginOptions.FromEnvironment(CreateTestedHardwareIdentity());

                    AssertEqual(false, options.UsesTestedHardwareDefaults, nameof(DisableWritesOverridesTestedHardwareDefaults));
                    AssertEqual(false, options.EnableHardwareWrites, nameof(DisableWritesOverridesTestedHardwareDefaults));
                    AssertEqual(false, options.EnableExperimentalRegisters, nameof(DisableWritesOverridesTestedHardwareDefaults));
                });
            });
        }

        private static void UnknownHardwareDefaultsRemainReadOnly()
        {
            WithoutPluginEnvironment(() =>
            {
                var options = PluginOptions.FromEnvironment(HardwareIdentity.Unknown);

                AssertEqual(false, options.UsesTestedHardwareDefaults, nameof(UnknownHardwareDefaultsRemainReadOnly));
                AssertEqual(false, options.EnableHardwareWrites, nameof(UnknownHardwareDefaultsRemainReadOnly));
                AssertEqual(false, options.EnableExperimentalRegisters, nameof(UnknownHardwareDefaultsRemainReadOnly));
            });
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
            WithoutPluginEnvironment(() =>
            {
                var capability = PwmControlCapability.Evaluate(PluginOptions.FromEnvironment(HardwareIdentity.Unknown), new FakeIoPort(isAvailable: false));

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
                using (var hardware = new Ite8613fIo(new FakeIoPort(isAvailable: true), PluginOptions.FromEnvironment(HardwareIdentity.Unknown), new PluginLog()))
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

        private static void DirectIteTachCountReadsCpuFanRpm()
        {
            var ioPort = new FakeSuperIoPort(isAvailable: true);
            ioPort.SetConfigWord(0x20, 0x8613);
            ioPort.SetConfigWord(0x60, 0x0A30);
            ioPort.SetHardwareMonitorByte(0x0E, 0x87);
            ioPort.SetHardwareMonitorByte(0x19, 0x00);

            using (var hardware = new Ite8613fIo(ioPort, PluginOptions.FromEnvironment(), new PluginLog()))
            {
                var reading = hardware.ReadCpuFanRpm();

                AssertEqual(true, reading.Succeeded, nameof(DirectIteTachCountReadsCpuFanRpm));
                AssertEqual(5000.0f, Round(reading.Rpm.GetValueOrDefault()), nameof(DirectIteTachCountReadsCpuFanRpm));
                AssertEqual("IT8613F direct", reading.Source, nameof(DirectIteTachCountReadsCpuFanRpm));
            }
        }

        private static void DirectIteTachRejectsInvalidCount()
        {
            var ioPort = new FakeSuperIoPort(isAvailable: true);
            ioPort.SetConfigWord(0x20, 0x8613);
            ioPort.SetConfigWord(0x60, 0x0A30);
            ioPort.SetHardwareMonitorByte(0x0E, 0xFF);
            ioPort.SetHardwareMonitorByte(0x19, 0xFF);

            using (var hardware = new Ite8613fIo(ioPort, PluginOptions.FromEnvironment(), new PluginLog()))
            {
                var reading = hardware.ReadCpuFanRpm();

                AssertEqual(false, reading.Succeeded, nameof(DirectIteTachRejectsInvalidCount));
                AssertContains("tach count is invalid", reading.Message, nameof(DirectIteTachRejectsInvalidCount));
            }
        }

        private static void CompositeFanRpmSourceUsesDirectSourceBeforeFallbacks()
        {
            using (var source = new CompositeFanRpmSource(new IFanRpmSource[]
            {
                new FakeFanRpmSource(FanRpmReading.Success(4321.0f, "IT8613F direct")),
                new FakeFanRpmSource(FanRpmReading.Success(1234.0f, "HWiNFO"))
            }))
            {
                var reading = source.ReadCpuFanRpm();

                AssertEqual(true, reading.Succeeded, nameof(CompositeFanRpmSourceUsesDirectSourceBeforeFallbacks));
                AssertEqual(4321.0f, reading.Rpm.GetValueOrDefault(), nameof(CompositeFanRpmSourceUsesDirectSourceBeforeFallbacks));
                AssertEqual("IT8613F direct", reading.Source, nameof(CompositeFanRpmSourceUsesDirectSourceBeforeFallbacks));
            }
        }

        private static void FanRpmReadingRoundsToWholeRpm()
        {
            var reading = FanRpmReading.Success(2934.78f, "Test");

            AssertEqual(2935.0f, reading.Rpm.GetValueOrDefault(), nameof(FanRpmReadingRoundsToWholeRpm));
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

        private static HardwareIdentity CreateTestedHardwareIdentity()
        {
            return new HardwareIdentity(
                "Shenzhen Meigao Electronic Equipment Co.,Ltd",
                "RPBNB",
                "Micro Computer (HK) Tech Limited",
                "Venus Series",
                "American Megatrends International, LLC.",
                "RPBNB.0.09");
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

        private static void WithoutPluginEnvironment(Action action)
        {
            var names = new[]
            {
                "FANCONTROL_NPB5ITE_ENABLE_WRITES",
                "FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS",
                "FANCONTROL_NPB5ITE_DISABLE_WRITES",
                "FANCONTROL_NPB5ITE_DISABLE_TESTED_HARDWARE_DEFAULTS",
                "FANCONTROL_NPB5ITE_ALLOW_LOW_PWM",
                "FANCONTROL_NPB5ITE_MIN_PWM_PERCENT",
                "FANCONTROL_NPB5ITE_ALLOW_MANUAL_WITHOUT_CPU_TEMP"
            };

            var originals = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var name in names)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }

                action();
            }
            finally
            {
                foreach (var pair in originals)
                {
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                }
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

        private sealed class FakeFanRpmSource : IFanRpmSource
        {
            private readonly FanRpmReading _reading;

            public FakeFanRpmSource(FanRpmReading reading)
            {
                _reading = reading;
            }

            public FanRpmReading ReadCpuFanRpm()
            {
                return _reading;
            }
        }

        private sealed class FakeSuperIoPort : IIoPort, ISuperIoConfigPort
        {
            private readonly System.Collections.Generic.Dictionary<byte, byte> _configValues = new System.Collections.Generic.Dictionary<byte, byte>();
            private readonly System.Collections.Generic.Dictionary<byte, byte> _hardwareMonitorValues = new System.Collections.Generic.Dictionary<byte, byte>();
            private byte _selectedHardwareMonitorRegister;

            public FakeSuperIoPort(bool isAvailable)
            {
                IsAvailable = isAvailable;
            }

            public bool IsAvailable { get; }

            public void SetConfigWord(byte register, ushort value)
            {
                _configValues[register] = (byte)(value >> 8);
                _configValues[(byte)(register + 1)] = (byte)(value & 0xFF);
            }

            public void SetHardwareMonitorByte(byte register, byte value)
            {
                _hardwareMonitorValues[register] = value;
            }

            public byte ReadByte(ushort port)
            {
                return ReadIoPortByte(port);
            }

            public void WriteByte(ushort port, byte value)
            {
                WriteIoPortByte(port, value);
            }

            public void SelectSlot(int slot)
            {
            }

            public byte ReadIoPortByte(ushort port)
            {
                if (port == 0x0A36)
                {
                    return _hardwareMonitorValues.TryGetValue(_selectedHardwareMonitorRegister, out var value) ? value : (byte)0;
                }

                return 0;
            }

            public void WriteIoPortByte(ushort port, byte value)
            {
                if (port == 0x0A35)
                {
                    _selectedHardwareMonitorRegister = value;
                }
            }

            public void FindBars()
            {
            }

            public byte ReadConfigByte(byte register)
            {
                return _configValues.TryGetValue(register, out var value) ? value : (byte)0;
            }

            public ushort ReadConfigWord(byte register)
            {
                var high = ReadConfigByte(register);
                var low = ReadConfigByte((byte)(register + 1));
                return (ushort)((high << 8) | low);
            }

            public void WriteConfigByte(byte register, byte value)
            {
                _configValues[register] = value;
            }

            public void Dispose()
            {
            }
        }
    }
}
