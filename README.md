# FanControl.NPB5ITE

[English](README.md) | [简体中文](README.zh-CN.md)

Fan Control plugin for Minisforum Venus Series NPB5 / RPBNB systems with an ITE IT8613E/F hardware monitor.

This plugin exposes:

- `NPB5 CPU Fan`: CPU fan RPM sensor.
- `NPB5 CPU Fan Control`: Fan Control PWM control sensor.
- A diagnostics tool for direct IT8613E tach/RPM reads, LibreHardwareMonitor, HWiNFO Gadget/VSB fallback, PWM capability, and read-only Super I/O / IT8613E register snapshots.

## Safety Notice

This project writes low-level hardware monitor registers when manual PWM is explicitly enabled. Wrong register writes can make cooling unstable.

Use it only if you understand and accept the risk. Keep a way to restore BIOS fan control and monitor CPU temperature during initial setup.

Default behavior is read-only. Manual PWM writes require all required environment switches and Administrator access.

## Hardware Status

Likely compatible target family:

- Minisforum Venus Series NPB5 / NPB7 and closely related NAB5 / NAB6 systems when they use a compatible ITE IT8613E/F hardware monitor layout.

Tested target:

- Minisforum NPB5 / RPBNB.
- Windows 11.
- ITE IT8613E/F hardware monitor.
- Super I/O index port `0x2E`.
- IT8613E chip ID `0x8613`.
- HWM base observed as `0x0A30`.

Other Venus Series models should be verified with read-only diagnostics before enabling PWM control. NPB7, NAB5, NAB6, and board revisions other than the tested NPB5 need the same chip, base address, RPM source, PWM duty register, and restore behavior.

Current PWM implementation:

- Uses the observed fan2 PWM duty register `0x6B`.
- Uses IT8613E HWM indexed register access through LibreHardwareMonitor/PawnIO.
- Converts Fan Control percentage to 8-bit PWM duty `0..255`.
- Restores old register values captured before manual writes.
- Reads CPU fan RPM directly from the observed fan2 tach registers `0x0E` and `0x19` using `675000 / tachCount`.

The PWM register map remains gated behind an explicit opt-in environment variable so the plugin stays read-only by default.

## Platform Support

Minimum supported operating system:

- Windows 10 Version 1607, build `10.0.14393`.

Tested operating system:

- Windows 11.

Fan Control must be a .NET 10 build compatible with `FanControl.Plugins.dll`.

## Features

- Direct CPU fan RPM reads from IT8613E/F HWM fan2 tach registers.
- LibreHardwareMonitor motherboard fan RPM fallback.
- HWiNFO Gadget/VSB RPM fallback.
- Fan Control manual/curve PWM sensor with coalesced background writes.
- Default 35% minimum PWM.
- Optional low-PWM floor down to 10%.
- 0% control request releases manual control and restores previous/automatic state.
- Full speed at or above 85 C.
- Restore previous/automatic control on reset, close, RPM read failure, temperature read failure, or write failure.
- Read-only diagnostics for hardware confirmation.

Current HWiNFO status:

- PWM writes do not depend on HWiNFO.
- CPU temperature reads do not depend on HWiNFO.
- On the tested NPB5, CPU fan RPM no longer depends on HWiNFO. The plugin first reads IT8613E/F tach registers directly.
- HWiNFO Gadget/VSB remains a fallback when direct tach and LibreHardwareMonitor motherboard RPM sources are unavailable.
- If HWiNFO is not running and no other RPM source succeeds, the safety policy restores previous/automatic control instead of applying manual PWM.

The plugin still requires low-level port access through LibreHardwareMonitor/PawnIO or Fan Control's bundled `LibreHardwareMonitorLib.dll`; this is separate from requiring the HWiNFO64 application.

## Install From Release

1. Download the latest release package.
2. Close Fan Control.
3. Extract `FanControl.NPB5ITE.dll` into:

   ```text
   C:\Program Files (x86)\FanControl\Plugins
   ```

4. Start Fan Control as Administrator for direct IT8613E/F tach reads and for PWM write tests.
5. Add the `NPB5 CPU Fan` RPM sensor and `NPB5 CPU Fan Control` control sensor in Fan Control.

If Windows blocks the downloaded DLL, open the file properties and unblock it before starting Fan Control.

Without Administrator access, direct Super I/O reads may fail with chip ID `0x0000`; in that case the plugin falls back to LibreHardwareMonitor fan sensors or HWiNFO Gadget/VSB if available.

## Runtime Configuration

Manual PWM writes are disabled unless explicitly enabled.

Recommended read-only startup:

```powershell
Start-Process 'C:\Program Files (x86)\FanControl\FanControl.exe'
```

Manual PWM startup:

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
| `FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS` | disabled | Allows writes to the observed IT8613E fan2 register map. Kept as an explicit safety opt-in. |
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
.\scripts\package-release.ps1 -Version 0.2.0
```

Do not include local diagnostic output, deployment logs, or Fan Control installation DLLs.

Release packages are built locally against the real `FanControl.Plugins.dll`.

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
4. Compare direct IT8613E RPM, HWiNFO RPM, LibreHardwareMonitor output, PWM capability output, and IT8613E HWM register dumps.
5. Confirm that RPM, PWM duty, and restore behavior match the tested NPB5 behavior before trying curves.

Write probe:

```powershell
$env:FANCONTROL_NPB5ITE_ENABLE_WRITES='1'
$env:FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS='1'
dotnet run --project ".\tools\FanControl.NPB5ITE.Diagnostics\FanControl.NPB5ITE.Diagnostics.csproj" -c Release -- ".\diagnostics" --label pwm60-probe --set-pwm 60 --restore-after-seconds 8
```

The probe writes PWM, captures snapshots, waits, and restores the previous register values. Use only while watching RPM and temperature.

## Known Limitations

- Only the NPB5/RPBNB IT8613E/F path has been investigated.
- NPB7, NAB5, NAB6, and other similar Venus Series systems should be verified before PWM control is enabled.
- PWM writes require explicit opt-in.
- Auto restore is based on old-value register snapshots, not a fully confirmed BIOS Auto register map.
- Direct IT8613E tach RPM has only been verified on one NPB5/Windows 11 system.
- Fan Control must run elevated for direct IT8613E HWM tach reads and IT8613E HWM register writes.

## Roadmap

This project is currently considered stable enough for the tested NPB5 / Windows 11 setup. No active feature roadmap is planned.

Future changes are expected to be limited to critical safety fixes, compatibility fixes for the tested setup, or clearly verified reports from the same hardware family.

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
