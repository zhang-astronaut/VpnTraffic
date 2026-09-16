# VpnTraffic — PowerToys Command Palette 扩展

在命令面板 Dock / 列表中展示机场订阅的**总流量、已用流量与使用百分比**。数据来自订阅链接响应头 `subscription-userinfo`（多设备共用同一订阅即可），不监控本机网卡流量。

## 功能

- 设置页填写订阅链接
- Dock 快捷栏实时展示：`45% · 12.3GB`（Compact 自包含）/ 剩余与到期（Default）
- 列表页汇总 + 按时间点的用量柱状图
- 可配置：刷新间隔（15s–15m）、是否展示时间线、是否稀疏落盘、历史点数
- 降低硬件损耗：历史点默认内存为主；仅当间隔 ≥15 分钟或用量变化 ≥64 MiB 时才写盘
- 界面随系统语言切换（zh-CN / en-US）

## 环境要求

- Windows 10 19041+
- .NET 10 SDK（本仓库 TFM：`net10.0-windows10.0.26100.0`）
- PowerToys / Command Palette（已测 PowerToys 0.101 + CmdPal 0.12）
- Windows SDK 10.0.26100（makeappx / signtool）

## 构建与安装

```powershell
# 建议先补环境变量（部分精简 shell 会缺失 ProgramFiles，导致 NuGet 还原失败）
$env:ProgramFiles = "C:\Program Files"
$env:ProgramW6432 = "C:\Program Files"

# 编译 + 打 MSIX
powershell -ExecutionPolicy Bypass -File scripts\build-msix.ps1

# 自签证书并侧载（首次安装证书到 Root/TrustedPeople 需要管理员）
powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1
```

安装成功后：

1. 打开 Command Palette
2. 运行 **Reload Command Palette Extension**
3. 搜索 **VpnTraffic / VPN 流量**
4. 可选：Dock 编辑模式中固定流量带

## 项目结构

```
VpnTraffic/
  VpnTraffic.cs                 # IExtension + COM CLSID + Program
  VpnTrafficCommandsProvider.cs # 顶级命令 / Dock / 设置
  Services/                     # 订阅解析、历史、刷新循环、图表
  Pages/QuotaListPage.cs
  Localization/Localizer.cs
  Package.appxmanifest          # 三处相同 CLSID + com.microsoft.commandpalette
scripts/build-msix.ps1
scripts/deploy-local.ps1
VpnTraffic.Smoke/               # 纯逻辑冒烟测试（无 WinRT）
docs/compose/spec/vpn-traffic.md
```

## 自测

```powershell
dotnet run --project VpnTraffic.Smoke/VpnTraffic.Smoke.csproj -c Release
dotnet build VpnTraffic/VpnTraffic.csproj -c Release
```

## CLSID

`{a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17}` — 必须与 `[Guid]`、`com:Class Id`、`CreateInstance ClassId` 一致。
