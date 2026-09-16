# VpnTraffic — PowerToys Command Palette 扩展

在命令面板 Dock / 列表中展示机场订阅的**总流量、已用流量与使用百分比**。数据来自订阅链接响应头 `subscription-userinfo`（多设备共用同一订阅即可），不监控本机网卡流量。

## 安装

### WinGet（推荐，合并后可用）

```powershell
winget install zhang-astronaut.VpnTraffic
```

> 首次提交已进入 [winget-pkgs PR #435758](https://github.com/microsoft/winget-pkgs/pull/435758)，合并后即可从 WinGet 安装。

### GitHub Release

下载 [VpnTraffic-Setup-0.1.0.0.exe](https://github.com/zhang-astronaut/VpnTraffic/releases/download/v0.1.0.0/VpnTraffic-Setup-0.1.0.0.exe) 并运行，然后在 Command Palette 执行 **Reload Command Palette Extension**。

## 功能

- 设置页填写订阅链接
- Dock 快捷栏实时展示：`45% · 12.3GB`（Compact 自包含）/ 剩余与到期（Default）
- 列表页汇总 + 按时间点的用量柱状图
- 可配置：刷新间隔（15s–15m）、是否展示时间线、是否稀疏落盘、历史点数
- 降低硬件损耗：历史点默认内存为主；仅当间隔 ≥15 分钟或用量变化 ≥64 MiB 时才写盘
- 界面随系统语言切换（zh-CN / en-US）
- 多 UA 兼容（clash.meta / ClashforWindows / v2rayN 等）以获取 `subscription-userinfo`

## 从源码构建

```powershell
$env:ProgramFiles = "C:\Program Files"
$env:ProgramW6432 = "C:\Program Files"

# 本地 MSIX 侧载（开发）
powershell -ExecutionPolicy Bypass -File scripts\build-msix.ps1
powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1

# WinGet 安装包（Inno + unpackaged COM）
powershell -ExecutionPolicy Bypass -File scripts\build-exe.ps1
```

### 环境要求

- Windows 10 19041+
- .NET 10 SDK
- PowerToys / Command Palette
- Windows SDK 10.0.26100（makeappx，MSIX 路径）
- Inno Setup 6（安装包路径）

## 项目结构

```
VpnTraffic/           # 扩展源码（C# / CmdPal Toolkit）
VpnTraffic.Smoke/     # 纯逻辑冒烟测试
scripts/              # build-msix / deploy-local / build-exe
setup.iss             # Inno Setup（WinGet）
winget/               # winget-pkgs 清单
docs/compose/spec/    # 设计与交付文档
```

## CLSID

`{a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17}` — 与 `[Guid]`、`com:Class Id`、`CreateInstance ClassId` 一致。

## License

MIT
