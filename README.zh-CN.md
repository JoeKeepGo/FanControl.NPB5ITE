# FanControl.NPB5ITE

[English](README.md) | [简体中文](README.zh-CN.md)

面向 Minisforum Venus Series NPB5 / RPBNB、ITE IT8613E/F 硬件监控芯片的 Fan Control 插件。

本插件提供：

- `NPB5 CPU Fan`：CPU 风扇 RPM 传感器。
- `NPB5 CPU Fan Control`：Fan Control PWM 控制传感器。
- 诊断工具：用于直接读取 IT8613E tach/RPM、LibreHardwareMonitor、HWiNFO Gadget/VSB 回退、PWM 能力，以及只读 Super I/O / IT8613E 寄存器快照。

## 安全说明

启用手动 PWM 后，本项目会写入底层硬件监控寄存器。错误的寄存器写入可能导致散热不稳定。

请只在理解并接受风险的情况下使用。初次配置时，请保留恢复 BIOS 风扇控制的方式，并监控 CPU 温度。

未知硬件上默认行为是只读。在已测试的 NPB5 / RPBNB 硬件指纹上，手动 PWM 写入默认启用，并需要管理员权限。

## 硬件状态

可能兼容的目标系列：

- Minisforum Venus Series NPB5 / NPB7，以及硬件监控布局兼容的相关 NAB5 / NAB6 机型。

已测试目标：

- Minisforum NPB5 / RPBNB。
- Windows 11。
- BIOS `RPBNB.0.09` 和 BIOS `1.02`。
- ITE IT8613E/F 硬件监控芯片。
- Super I/O index port `0x2E`。
- IT8613E chip ID `0x8613`。
- 已观察到的 HWM base 为 `0x0A30`。
- 已测试的 baseboard manufacturer 字符串：`Shenzhen Meigao Electronic Equipment Co.,Ltd` 和 `Meigao Innovation Technology (Shen Zhen) Co., Ltd`。

其他 Venus Series 机型在启用 PWM 控制前，应先通过只读诊断验证。NPB7、NAB5、NAB6，以及不同于已测试 NPB5 的主板版本，需要确认芯片、base address、RPM 来源、PWM duty 寄存器和恢复行为一致。

当前 PWM 实现：

- 使用已观察到的 fan2 PWM duty 寄存器 `0x6B`。
- 通过 LibreHardwareMonitor/PawnIO 访问 IT8613E HWM indexed register。
- 将 Fan Control 百分比转换为 8-bit PWM duty `0..255`。
- 手动写入前记录旧寄存器值，并在恢复时写回。
- 通过已观察到的 fan2 tach 寄存器 `0x0E` 和 `0x19` 直接读取 CPU 风扇 RPM，公式为 `675000 / tachCount`。

PWM 寄存器映射只会在已测试的 NPB5 / RPBNB 硬件指纹上默认启用。未知硬件仍保持只读，除非用户显式 opt-in。

## 平台支持

最低支持系统：

- Windows 10 Version 1607，build `10.0.14393`。

已测试系统：

- Windows 11。

Fan Control 需要使用与 `FanControl.Plugins.dll` 兼容的 .NET 10 构建。

## 功能

- 直接从 IT8613E/F HWM fan2 tach 寄存器读取 CPU 风扇 RPM。
- LibreHardwareMonitor 主板风扇 RPM 回退来源。
- HWiNFO Gadget/VSB RPM 回退来源。
- Fan Control 手动/曲线 PWM 控制传感器，带后台合并写入。
- 默认 10% 最低 PWM。
- 0% 控制请求会释放手动控制，并恢复此前/自动状态。
- CPU 温度达到或超过可配置的临界阈值时强制全速，默认 95 C。
- Reset、Close、RPM 读取失败、温度读取失败或写入失败时，恢复此前/自动控制。
- 只读诊断用于硬件确认。

当前 HWiNFO 状态：

- PWM 写入不依赖 HWiNFO。
- CPU 温度读取不依赖 HWiNFO。
- 在已测试的 NPB5 上，CPU 风扇 RPM 不再依赖 HWiNFO。插件会优先直接读取 IT8613E/F tach 寄存器。
- 当 direct tach 和 LibreHardwareMonitor 主板 RPM 来源不可用时，HWiNFO Gadget/VSB 仍作为回退。
- 如果 HWiNFO 未运行且没有其他 RPM 来源成功，安全策略会恢复此前/自动控制，而不是继续应用手动 PWM。

插件仍然需要通过 LibreHardwareMonitor/PawnIO 或 Fan Control 自带的 `LibreHardwareMonitorLib.dll` 进行底层端口访问；这不同于依赖 HWiNFO64 应用本身。

## 从 Release 安装

1. 下载最新 release 包。
2. 关闭 Fan Control。
3. 将 `FanControl.NPB5ITE.dll` 解压到：

   ```text
   C:\Program Files (x86)\FanControl\Plugins
   ```

4. 如需直接 IT8613E/F tach 读取或测试 PWM 写入，请以管理员权限启动 Fan Control。
5. 在 Fan Control 中添加 `NPB5 CPU Fan` RPM 传感器和 `NPB5 CPU Fan Control` 控制传感器。

如果 Windows 阻止下载的 DLL，请打开文件属性并解除阻止后再启动 Fan Control。

如果没有管理员权限，直接 Super I/O 读取可能会得到 chip ID `0x0000`；此时插件会回退到 LibreHardwareMonitor 风扇传感器，或在可用时回退到 HWiNFO Gadget/VSB。

## 运行时配置

在已测试的 NPB5 / RPBNB 硬件指纹上，手动 PWM 写入默认启用。Fan Control 仍需要管理员权限才能直接访问 IT8613E/F 寄存器。

已测试 NPB5 / RPBNB 的正常启动：

```powershell
Start-Process 'C:\Program Files (x86)\FanControl\FanControl.exe' -Verb RunAs
```

只读启动或紧急禁用：

```powershell
$env:FANCONTROL_NPB5ITE_DISABLE_WRITES='1'
Start-Process 'C:\Program Files (x86)\FanControl\FanControl.exe' -Verb RunAs
```

未知硬件默认禁用写入。如需强制启用，需要显式配置：

```powershell
$env:FANCONTROL_NPB5ITE_ENABLE_WRITES='1'
$env:FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS='1'
```

如果无法通过 LibreHardwareMonitor 获取 CPU 温度，手动写入会被阻止。仅限短时间受控测试时可使用：

```powershell
$env:FANCONTROL_NPB5ITE_ALLOW_MANUAL_WITHOUT_CPU_TEMP='1'
```

默认最低手动 PWM 为 10%。如果想使用更保守的曲线，可以提高最低值：

```powershell
$env:FANCONTROL_NPB5ITE_MIN_PWM_PERCENT='20'
```

默认临界 CPU 温度阈值为 95 C。可以在 70 C 到 100 C 之间调整：

```powershell
$env:FANCONTROL_NPB5ITE_CRITICAL_CPU_TEMP_C='90'
```

环境变量：

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `FANCONTROL_NPB5ITE_DISABLE_WRITES` | disabled | 强制只读模式，即使当前是已测试的 NPB5 / RPBNB 硬件指纹。 |
| `FANCONTROL_NPB5ITE_DISABLE_TESTED_HARDWARE_DEFAULTS` | disabled | 关闭已测试 NPB5 / RPBNB 硬件指纹的自动写入默认值。 |
| `FANCONTROL_NPB5ITE_ENABLE_WRITES` | disabled | 在未识别硬件上允许硬件写入。已测试 NPB5 / RPBNB 不需要。 |
| `FANCONTROL_NPB5ITE_ENABLE_EXPERIMENTAL_REGISTERS` | disabled | 在未识别硬件上允许写入已观察到的 IT8613E fan2 寄存器映射。已测试 NPB5 / RPBNB 不需要。 |
| `FANCONTROL_NPB5ITE_ALLOW_MANUAL_WITHOUT_CPU_TEMP` | disabled | CPU 温度不可用时仍允许手动 PWM。仅限短时间测试。 |
| `FANCONTROL_NPB5ITE_MIN_PWM_PERCENT` | `10` | 最低手动 PWM。低于 10% 的值会被钳制到 10%。 |
| `FANCONTROL_NPB5ITE_CRITICAL_CPU_TEMP_C` | `95` | 触发强制全速的临界 CPU 温度阈值。取值会被钳制到 70..100 C。 |

插件日志路径：

```text
%LOCALAPPDATA%\FanControl.NPB5ITE\plugin.log
```

## 构建

要求：

- Windows。
- .NET 10 SDK。
- Fan Control .NET 10 构建。

将 Fan Control 安装目录中的 `FanControl.Plugins.dll` 放入：

```text
lib\FanControl.Plugins.dll
```

构建和测试：

```powershell
dotnet restore
dotnet build ".\NPB5 FanControl.sln" -c Release
dotnet run --project ".\tests\FanControl.NPB5ITE.Tests\FanControl.NPB5ITE.Tests.csproj" -c Release --no-build
```

项目包含编译期 Fan Control API stub，因此缺少 `lib\FanControl.Plugins.dll` 时安全逻辑仍可构建。不要部署用 stub 构建出的 DLL。

## 打包

构建 release 输出：

```powershell
dotnet build ".\NPB5 FanControl.sln" -c Release
```

插件 DLL 输出路径：

```text
src\FanControl.NPB5ITE\bin\Release\net10.0-windows\FanControl.NPB5ITE.dll
```

release zip 可包含插件 DLL 和可选 PDB：

```text
FanControl.NPB5ITE.dll
FanControl.NPB5ITE.pdb
```

本地 release 打包：

```powershell
.\scripts\package-release.ps1 -Version 0.2.4
```

不要包含本地诊断输出、部署日志或 Fan Control 安装 DLL。

Release 包通过真实的 `FanControl.Plugins.dll` 在本地构建。

## 诊断

运行只读诊断：

```powershell
dotnet run --project ".\tools\FanControl.NPB5ITE.Diagnostics\FanControl.NPB5ITE.Diagnostics.csproj" -c Release -- ".\diagnostics" --label snapshot
```

如果 Super I/O 读取报告 chip ID `0x0000`，请从管理员终端重新运行。

建议的寄存器确认流程：

1. 在插件外设置风扇模式。
2. 为 BIOS Auto 捕获只读快照。
3. 为已知 BIOS 手动 PWM 值捕获快照，例如 raw `127`、`153`、`204`。
4. 对比 direct IT8613E RPM、HWiNFO RPM、LibreHardwareMonitor 输出、PWM capability 输出和 IT8613E HWM 寄存器 dump。
5. 在尝试曲线前，确认 RPM、PWM duty 和恢复行为与已测试 NPB5 行为一致。

写入探测：

```powershell
dotnet run --project ".\tools\FanControl.NPB5ITE.Diagnostics\FanControl.NPB5ITE.Diagnostics.csproj" -c Release -- ".\diagnostics" --label pwm60-probe --set-pwm 60 --restore-after-seconds 8
```

探测会写入 PWM、捕获快照、等待，然后恢复此前寄存器值。请只在观察 RPM 和温度时使用。

## 已知限制

- 目前只调查过 NPB5/RPBNB IT8613E/F 路径。
- NPB7、NAB5、NAB6 和其他类似 Venus Series 系统在启用 PWM 控制前应先验证。
- PWM 写入只在已测试的 NPB5 / RPBNB 硬件指纹上默认启用。其他系统需要显式 opt-in。
- Auto restore 基于旧值寄存器快照，而不是完全确认的 BIOS Auto 寄存器映射。
- Direct IT8613E tach RPM 目前只在一台 NPB5 / Windows 11 上验证。
- Fan Control 必须以管理员权限运行，才能直接读取 IT8613E HWM tach 或写入 IT8613E HWM 寄存器。

## 路线图

本项目目前在已测试的 NPB5 / Windows 11 环境下已足够稳定可用。暂无主动功能路线图。

未来更改预计仅限关键安全修复、已测试环境的兼容性修复，或来自相同硬件系列且经过明确验证的报告。

## 仓库结构

```text
src\FanControl.NPB5ITE                  插件实现
tools\FanControl.NPB5ITE.Diagnostics    诊断和寄存器捕获工具
tests\FanControl.NPB5ITE.Tests          轻量安全和硬件抽象测试
lib                                     本地外部 DLL，git 忽略
diagnostics                             本地诊断捕获，git 忽略
dist                                    本地 release/deploy 输出，git 忽略
```

## 致谢

感谢：

- [Fan Control](https://github.com/Rem0o/FanControl.Releases) 提供插件平台和优秀的风扇控制界面，本项目正是构建在它之上。
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 及其 PawnIO/LPC 访问层，使得从托管代码访问底层 IT8613E/F 寄存器成为可能。
- HWiNFO 在 direct IT8613E/F tach 路径验证期间提供了早期 RPM 参考来源。
- Fan Control 社区积累的硬件讨论和排障知识，让这种面向具体机型的插件变得可行。
- 已测试的 Minisforum NPB5 / RPBNB 环境及其诊断捕获，为 direct RPM 读取和 PWM 控制提供了寄存器证据。

## 许可证

使用 GNU Affero General Public License v3.0 or later 授权。见 `LICENSE`。
