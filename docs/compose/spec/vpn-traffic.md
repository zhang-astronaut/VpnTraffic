---
feature: vpn-traffic
status: delivered
updated: 2026-09-16
branch: feat/vpn-traffic-cmdpal
commits: cec5137..ad99d72
---

# VpnTraffic — Command Palette 订阅流量

## Report

**What was built** — PowerToys Command Palette 扩展 `VpnTraffic`：在设置页配置机场订阅链接后，通过 HTTP 响应头 `subscription-userinfo` 读取账号总流量/已用（兼容多设备共用订阅）。Dock 快捷栏以自包含标题展示 `百分比 · 已用`（Compact 可读），Default 模式附带剩余与到期；列表页提供汇总、立即刷新与时间点柱状图。可配置刷新间隔（默认 60s）、是否展示时间线、是否稀疏落盘、历史点数。历史点优先存内存，仅当间隔 ≥15 分钟或用量变化 ≥64 MiB 时原子写 `%LOCALAPPDATA%\VpnTraffic\history.json`，降低 SSD 写入。界面按系统语言 zh/en 切换。MSIX 侧载包已本机安装（`VpnTraffic_0.1.0.0_x64__wggbhrd4kyk4j`）。

**Verification**

| 命令 | 结果 |
| --- | --- |
| `dotnet build VpnTraffic/VpnTraffic.csproj -c Release` | PASS（0 warning） |
| `dotnet run --project VpnTraffic.Smoke -c Release` | ALL PASS（解析/稀疏落盘/图表） |
| `scripts/build-msix.ps1` + `deploy-local.ps1` | PASS，`Get-AppxPackage VpnTraffic` 0.1.0.0 |
| CLSID 三处一致 + `com.microsoft.commandpalette` + `internetClient` + `Public/` | PASS |

人工 UI：安装后需在 Command Palette 执行 **Reload Command Palette Extension**，再固定 Dock。本环境无法自动点选 UI，扩展进程 COM 启动已验证可运行。

**Journey log**

1. 精简 shell 缺 `ProgramFiles` 环境变量会导致 NuGet restore `path1 null`，构建前需补全。
2. Toolkit 0.11 对齐 .NET 10；TFM 用 `net10.0-windows10.0.26100.0`（本机 UAP Platform 只有 26100）。
3. `JsonSettingsManager` 为 abstract，需子类 `VpnJsonSettingsManager`。
4. AppExtension `PublicFolder="Public"` 必须在包内实际存在 `Public/` 目录，否则 CmdPal 不易发现。
5. 审查修复：保留旧成功数据时 Dock 也要显示 Error；Dispose 需取消并等待 HTTP（15s）完成后再释放资源；`HistoryStore.Load` 改为合并而非清空。

## [S1] Problem

机场/代理订阅用户需要在不登录网页面板的情况下，随时看到订阅总流量、已用与剩余百分比。数据必须来自订阅链接本身的账号配额（多设备共用同一订阅），而不是本机网卡流量。PowerToys Command Palette 的 Dock 快捷栏适合常驻展示，设置页用于配置订阅与刷新策略；同时要降低磁盘写入与请求频率。

## [S2] Design

### Identity

| 项 | 值 |
| --- | --- |
| 扩展类名 / 项目 | `VpnTraffic` |
| 显示名 | `VpnTraffic` / 中文「VPN 流量」 |
| CLSID | `a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17` |
| Package Identity Name | `VpnTraffic` |
| Publisher | `CN=VpnTraffic` |
| Version | `0.1.0.0` |
| AppExtension | `com.microsoft.commandpalette` / Id=`VpnTraffic` / PublicFolder=`Public` |
| SDK | `Microsoft.CommandPalette.Extensions` 0.11.260520004 |
| TFM | `net10.0-windows10.0.26100.0` |

三处 CLSID 一致：`[Guid]`、`com:Class Id`、`CmdPalProvider/Activation/CreateInstance ClassId`。

### Data source

HTTP GET 订阅 URL，解析响应头 `subscription-userinfo`：

```
upload=<bytes>; download=<bytes>; total=<bytes>; expire=<unix_seconds>
```

- `used = upload + download`，`percent = used / total`（total≤0 时未知）。
- 可选 `profile-title`。
- HttpClient 超时 15s，最多 5 次重定向。
- 失败时保留上次成功快照并在 Error 字段标注；Dock Subtitle 与列表均展示错误。

### Architecture

```text
VpnTrafficExtension (IExtension, [Guid])
  └─ VpnTrafficCommandsProvider (CommandProvider)
       ├─ Settings = VpnJsonSettingsManager (JsonSettingsManager)
       ├─ TopLevelCommands → QuotaListPage (ListPage)
       └─ GetDockBands → WrappedDockItem  # 自包含 Title/Subtitle

Services/
  SubscriptionClient   # HTTP + header → QuotaSnapshot
  HistoryStore         # 内存采样 + 稀疏落盘合并加载
  QuotaService         # 定时刷新、保留旧数据+错误、Dispose 可取消 HTTP
  ChartRenderer        # █░ 时间点柱状图
```

### Settings

路径：`Utilities.BaseSettingsPath("VpnTraffic")`

| Key | 类型 | 默认 |
| --- | --- | --- |
| `subscriptionUrl` | TextSetting | `""` |
| `refreshIntervalSeconds` | ChoiceSetSetting | `60`（15/30/60/300/900） |
| `showHistoryChart` | ToggleSetting | `true` |
| `persistHistory` | ToggleSetting | `true` |
| `maxHistoryPoints` | ChoiceSetSetting | `48`（24/48/96） |

### Dock

- Title（Compact）：`45% · 12.3GB`
- Subtitle（Default）：剩余 · 到期；若有刷新错误则追加 ` · {error}`
- 就地更新 `WrappedDockItem.Items`

### History & disk wear

- 内存上限 `maxHistoryPoints`；落盘条件：`persistHistory` 且（≥15min 或 ≥64MiB 增量）
- 文件：`%LOCALAPPDATA%\VpnTraffic\history.json`（temp + replace）
- Load 合并内存点，避免设置变更清空未落盘采样

### Localization

`CultureInfo.CurrentUICulture` 以 `zh` 开头 → 中文，否则英文。

## Out of Scope

- 节点解析 / 代理连通性
- 多订阅切换
- 本机网卡流量
- WinGet / 画廊上架

## Tasks

- [x] T1: 工程骨架与解决方案 — acceptance: `dotnet build` 通过，manifest 三处 CLSID 一致 (covers: S2)
- [x] T2: SubscriptionClient + QuotaSnapshot — acceptance: smoke 解析 `subscription-userinfo` (covers: S2)
- [x] T3: HistoryStore 稀疏落盘 — acceptance: 阈值未达不写盘；达阈值原子写 JSON (covers: S2)
- [x] T4: QuotaService 定时刷新 + 设置集成 — acceptance: 失败保留上次；Dispose 等待取消 (covers: S2)
- [x] T5: Dock band 自包含标题 — acceptance: Title 含百分比与已用；错误进 Subtitle (covers: S2)
- [x] T6: QuotaListPage 汇总+柱状图+zh/en — acceptance: 无配置/错误/成功三态 (covers: S2)
- [x] T7: MSIX 打包与本地部署 — acceptance: Add-AppxPackage 成功 (covers: S2)
- [x] T8: 自测清单与文档 — acceptance: Report + 验证表 (covers: S1; covers: S2)
