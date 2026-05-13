# FanControl.NPB5ITE

Experimental Fan Control plugin for Minisforum Venus Series NPB5 / RPBNB systems with an ITE IT8613E/F hardware monitor.

This plugin exposes:

- `NPB5 CPU Fan`: CPU fan RPM sensor.
- `NPB5 CPU Fan Control`: Fan Control PWM control sensor.
- A diagnostics tool for LibreHardwareMonitor, HWiNFO Gadget/VSB, PWM capability, and read-only Super I/O / IT8613E register snapshots.

## Safety Notice

This project writes low-level hardware monitor registers when manual PWM is explicitly enabled. Wrong register writes can make cooling unstable.

Use it only if you understand and accept the risk. Keep a way to restore BIOS fan control, monitor CPU temperature during testing, and do not leave experimental settings unattended.

Default behavior is read-only. Manual PWM writes require all required environment switches and Administrator access.

## Hardware Status

Theoretical target family:

- Minisforum Venus Series NPB5 / NPB7 and closely related NAB5 / NAB6 systems when they use a compatible ITE IT8613E/F hardware monitor layout.

Tested target:

- Minisforum NPB5 / RPBNB.
- Windows 11.
- ITE IT8613E/F hardware monitor.
- Super I/O index port `0x2E`.
- IT8613E chip ID `0x8613`.
- HWM base observed as `0x0A30`.

Other Venus Series models are not guaranteed to work. Treat NPB7, NAB5, NAB6, and any board revision other than the tested NPB5 as unvalidated until read-only diagnostics confirm the same chip, base address, RPM source, PWM duty register, and restore behavior.

Current PWM implementation:

- Uses the experimental fan2 PWM duty register `0x6B`.
- Uses IT8613E HWM indexed register access through LibreHardwareMonitor/PawnIO.
- Converts Fan Control percentage to 8-bit PWM duty `0..255`.
- Restores old register values captured before manual writes.

The current register map is still marked experimental. `RegisterMap.ConfirmedCpuFanControl` is intentionally empty.

## Platform Support

Minimum supported operating system:

- Windows 10 Version 1607, build `10.0.14393`.

Tested operating system:

- Windows 11.

Fan Control must be a .NET 10 build compatible with `FanControl.Plugins.dll`.

## Features

- Fan RPM import from LibreHardwareMonitor motherboard sensors.
- HWiNFO Gadget/VSB RPM fallback when LibreHardwareMonitor does not expose the NPB5 fan.
- Fan Control manual/curve PWM sensor with coalesced background writes.
- Default 35% minimum PWM.
- Optional experimental low-PWM floor down to 10%.
- 0% control request releases manual control and restores previous/automatic state.
- Full speed at or above 85 C.
- Restore previous/automatic control on reset, close, RPM read failure, temperature read failure, or write failure.
- Read-only diagnostics for hardware confirmation.

Current HWiNFO status:

- PWM writes do not depend on HWiNFO.
- CPU temperature reads do not depend on HWiNFO.
- On the tested NPB5, Fan RPM currently depends on HWiNFO Gadget/VSB because LibreHardwareMonitor does not expose a positive NPB5 CPU fan RPM sensor.
- If HWiNFO is not running and no other RPM source succeeds, the safety policy restores previous/automatic control instead of applying manual PWM.

Full standalone operation without HWiNFO is on the roadmap.

## Install From Release

1. Download the latest release package.
2. Close Fan Control.
3. Extract `FanControl.NPB5ITE.dll` into:

   ```text
   C:\Program Files (x86)\FanControl\Plugins
   ```

4. Start Fan Control as Administrator if you intend to test PWM writes.
5. Add the `NPB5 CPU Fan` RPM sensor and `NPB5 CPU Fan Control` control sensor in Fan Control.

If Windows blocks the downloaded DLL, open the file properties and unblock it before starting Fan Control.

## Runtime Configuration

Manual PWM writes are disabled unless explicitly enabled.

Recommended read-only startup:

```powershell
Start-Process 'C:\Program Files (x86)\FanControl\FanControl.exe'
```

Experimental manual PWM startup:

```powershell
$env:FANCONTROL_NPB5ITE_ENABLE_WRITES='1'
$env:FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS='1'
Start-Process 'C:\Program Files (x86)\FanControl\FanControl.exe' -Verb RunAs
```

If CPU temperature is unavailable through LibreHardwareMonitor, manual writes are blocked. For short controlled tests only:

```powershell
$env:FANCONTROL_NPB5ITE_ALLOW_MANUAL_WITHOUT_CPU_TEMP='1'
```

To test lower PWM values, opt in explicitly:

```powershell
$env:FANCONTROL_NPB5ITE_ALLOW_LOW_PWM='1'
$env:FANCONTROL_NPB5ITE_MIN_PWM_PERCENT='10'
```

Environment variables:

| Variable | Default | Description |
| --- | --- | --- |
| `FANCONTROL_NPB5ITE_ENABLE_WRITES` | disabled | Allows any hardware write path. |
| `FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS` | disabled | Allows writes to the experimental IT8613E fan2 register map. |
| `FANCONTROL_NPB5ITE_ALLOW_MANUAL_WITHOUT_CPU_TEMP` | disabled | Allows manual PWM when CPU temperature is unavailable. Use only for short tests. |
| `FANCONTROL_NPB5ITE_ALLOW_LOW_PWM` | disabled | Allows `FANCONTROL_NPB5ITE_MIN_PWM_PERCENT` below 35%. |
| `FANCONTROL_NPB5ITE_MIN_PWM_PERCENT` | `35` | Minimum manual PWM. With low-PWM opt-in, the hard floor is 10%. |

Plugin logs are written to:

```text
%LOCALAPPDATA%\FanControl.NPB5ITE\plugin.log
```

## Build

Requirements:

- Windows.
- .NET 10 SDK.
- Fan Control .NET 10 build.

Place `FanControl.Plugins.dll` from your Fan Control install into:

```text
lib\FanControl.Plugins.dll
```

Build and test:

```powershell
dotnet restore
dotnet build ".\NPB5 FanControl.sln" -c Release
dotnet run --project ".\tests\FanControl.NPB5ITE.Tests\FanControl.NPB5ITE.Tests.csproj" -c Release --no-build
```

The project includes compile-time Fan Control API stubs so the safety logic can still build when `lib\FanControl.Plugins.dll` is missing. Do not deploy a DLL built with the stubs.

## Package

Build release output:

```powershell
dotnet build ".\NPB5 FanControl.sln" -c Release
```

The plugin DLL is produced at:

```text
src\FanControl.NPB5ITE\bin\Release\net10.0-windows\FanControl.NPB5ITE.dll
```

For a release zip, include the plugin DLL and optionally the PDB:

```text
FanControl.NPB5ITE.dll
FanControl.NPB5ITE.pdb
```

Local release package:

```powershell
.\scripts\package-release.ps1 -Version 0.1.0
```

Do not include local diagnostic output, deployment logs, or Fan Control installation DLLs.

Release packages are currently built locally. GitHub-hosted release builds are not enabled yet because the deployable plugin must be compiled against the real `FanControl.Plugins.dll`, which is not committed to this repository.

## Diagnostics

Run read-only diagnostics:

```powershell
dotnet run --project ".\tools\FanControl.NPB5ITE.Diagnostics\FanControl.NPB5ITE.Diagnostics.csproj" -c Release -- ".\diagnostics" --label snapshot
```

If Super I/O reads report chip ID `0x0000`, rerun from an elevated Administrator terminal.

Suggested register confirmation workflow:

1. Set fan mode outside this plugin.
2. Capture a read-only snapshot for BIOS Auto.
3. Capture snapshots for known BIOS manual PWM values, for example raw `127`, `153`, and `204`.
4. Compare HWiNFO RPM, LibreHardwareMonitor output, PWM capability output, and IT8613E HWM register dumps.
5. Move registers from experimental to confirmed only after repeated captures show stable, predictable behavior.

Experimental write probe:

```powershell
$env:FANCONTROL_NPB5ITE_ENABLE_WRITES='1'
$env:FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS='1'
dotnet run --project ".\tools\FanControl.NPB5ITE.Diagnostics\FanControl.NPB5ITE.Diagnostics.csproj" -c Release -- ".\diagnostics" --label pwm60-probe --set-pwm 60 --restore-after-seconds 8
```

The probe writes PWM, captures snapshots, waits, and restores the previous register values. Use only while watching RPM and temperature.

## Known Limitations

- Only the NPB5/RPBNB IT8613E/F path has been investigated.
- NPB7, NAB5, NAB6, and other similar Venus Series systems are theoretical targets only.
- PWM writes remain experimental and require explicit opt-in.
- Auto restore is based on old-value register snapshots, not a fully confirmed BIOS Auto register map.
- HWiNFO fallback is currently required for RPM feedback on the tested NPB5.
- Fan Control must run elevated for experimental IT8613E HWM register writes.

## Roadmap

- Confirm direct IT8613E tach/RPM register decoding so the plugin can run without HWiNFO.
- Repeat read-only diagnostics on NPB7, NAB5, and NAB6 before enabling any model-specific claims.
- Confirm BIOS Auto restore registers and values instead of relying only on old-value snapshots.
- Move the PWM map from experimental to confirmed after repeated NPB5 captures prove stable behavior.
- Add GitHub-hosted release builds once the Fan Control plugin API reference can be restored in CI without committing local installation DLLs.

## Repository Layout

```text
src\FanControl.NPB5ITE                  Plugin implementation
tools\FanControl.NPB5ITE.Diagnostics    Diagnostics and register capture tool
tests\FanControl.NPB5ITE.Tests          Lightweight safety and hardware abstraction tests
lib                                     Local external DLLs, ignored by git
diagnostics                             Local diagnostic captures, ignored by git
dist                                    Local release/deploy output, ignored by git
```

## License

Licensed under the GNU Affero General Public License v3.0 or later. See `LICENSE`.
