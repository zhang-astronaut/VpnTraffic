---
feature: vpn-traffic
status: designed
updated: 2026-09-16
branch: feat/vpn-traffic-cmdpal
commits: cec5137..TBD
---

# VpnTraffic — Command Palette 订阅流量

## Report

## [S1] Problem

机场/代理订阅用户需要在不登录网页面板的情况下，随时看到订阅总流量、已用与剩余百分比。数据必须来自订阅链接本身的账号配额（多设备共用同一订阅），而不是本机网卡流量。PowerToys Command Palette 的 Dock 快捷栏适合常驻展示，设置页用于配置订阅与刷新策略；同时要降低磁盘写入与请求频率。

## [S2] Design

### Identity

| 项 | 值 |
| --- | --- |
| 扩展类名 / 项目 | `VpnTraffic` |
| 显示名 | `VpnTraffic`（系统语言 zh 时显示「VPN 流量」） |
| CLSID | `a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17` |
| Package Identity Name | `VpnTraffic` |
| Publisher | `CN=VpnTraffic` |
| Version | `0.1.0.0` |
| AppExtension | `com.microsoft.commandpalette` / Id=`VpnTraffic` |
| SDK | `Microsoft.CommandPalette.Extensions` 0.11.260520004 |
| TFM | `net8.0-windows10.0.19041.0`（可被 .NET 10 SDK 构建） |

三处 CLSID 必须一致：`[Guid]`、`com:Class Id`、`CmdPalProvider/Activation/CreateInstance ClassId`。

### Data source

HTTP GET 订阅 URL，解析响应头 `subscription-userinfo`：

```
upload=<bytes>; download=<bytes>; total=<bytes>; expire=<unix_seconds>
```

- `used = upload + download`，`percent = used / total`（total=0 时显示未知）。
- 也可读取 `profile-title`（可选展示）。
- 不下载/解析节点 YAML；请求体仅用于兼容部分机场把信息放在 body 的情况（可选忽略，首版仅头）。
- 失败时：保留上次成功快照，UI 显示错误状态，不崩溃。
- HttpClient 超时 15s，不跟随过多重定向（MaxAutomaticRedirections=5）。

### Architecture

```text
VpnTraffic (IExtension, [Guid])
  └─ VpnTrafficCommandsProvider (CommandProvider)
       ├─ Settings = JsonSettingsManager.Settings  (FormContent 设置页)
       ├─ TopLevelCommands → QuotaListPage (ContentPage)
       └─ GetDockBands → WrappedDockItem(QuotaDockItem)   # 自包含 Title/Subtitle

Services/
  SubscriptionClient   # HTTP + header 解析 → QuotaSnapshot
  QuotaSnapshot        # used/total/upload/download/expire/fetchedAt/error
  HistoryStore         # 内存采样点 + 稀疏落盘 JSON
  QuotaService         # 定时刷新、发布快照、驱动 Dock/列表更新
  Localizer            # zh-CN / en-US，按 CultureInfo.CurrentUICulture

Pages/
  QuotaListPage        # 汇总 + 历史柱状图（Markdown 块 + 列表项）
```

### Settings (JsonSettingsManager)

路径：`Utilities.BaseSettingsPath("VpnTraffic")`

| Key | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `subscriptionUrl` | TextSetting | `""` | 订阅链接，required |
| `refreshIntervalSeconds` | ChoiceSetSetting | `60` | 15 / 30 / 60 / 300 / 900 |
| `showHistoryChart` | ToggleSetting | `true` | 列表页展示时间点柱状图 |
| `persistHistory` | ToggleSetting | `true` | 允许稀疏落盘；关闭则纯内存 |
| `maxHistoryPoints` | ChoiceSetSetting | `48` | 24 / 48 / 96 |

`Settings.SettingsChanged` 后：重载 URL/间隔、必要时立即刷新。

### Dock (Default + Compact)

- Title（Compact 可见）：`45% · 12.3GB` — 百分比 + 已用，自包含。
- Subtitle（Default 可见）：`27.7GB left · expire 12d` 或错误摘要。
- 就地更新 `WrappedDockItem.Items`/Title 数据，必要时 `RaiseItemsChanged`；避免整表重建导致滚动跳顶。
- Icon：Segoe MDL2 流量字形 `\uE968`（或 `\uE946`）。

### History & disk wear

- 采样：每次成功刷新追加 `{t, used, total}`，内存上限 `maxHistoryPoints`。
- 落盘条件（全部满足才写）：`persistHistory==true` 且（距上次写盘 ≥ **15 分钟** 或 used 增量 ≥ **64 MiB**）。
- 文件：`%LOCALAPPDATA%\VpnTraffic\history.json`，原子写（temp + replace）。
- 启动加载后合并、按时间排序、裁剪到上限。
- 设置变更保存仅在 SettingsChanged 触发一次，不在刷新循环写。

### UI / Localization

- 语言：`CultureInfo.CurrentUICulture` 以 `zh` 开头 → 中文，否则英文。
- 列表页结构：
  1. 汇总项：已用 / 总量 / 剩余 / 百分比 / 过期
  2. 刷新项：上次更新时间、手动刷新命令
  3. 历史：Markdown 柱状图（`█` 块字符 + 时间标签）或逐点 ListItem
  4. 设置入口（若有）/ 错误提示

### Error behavior

| 场景 | 行为 |
| --- | --- |
| URL 为空 | Dock「未配置」；列表引导打开设置 |
| HTTP 非 2xx / 超时 | 保留上次数据；Subtitle/列表显示错误短句 |
| 头缺失/格式错 | 同上，错误「无 subscription-userinfo」 |
| 落盘失败 | 忽略并继续内存运行 |

### Out of scope (implementation)

- 节点解析 / 代理连通性测试
- 多订阅账户切换
- 本机网卡流量监控
- WinGet / 商店 / 画廊上架脚本（可后续）

## Out of Scope

见 S2 末尾；本分支交付可本地部署与使用的 MSIX 侧载包 + 源码。

## Tasks

- [ ] T1: 工程骨架与解决方案 — acceptance: `dotnet restore/build` 通过，manifest 含三处一致 CLSID (covers: S2)
- [ ] T2: SubscriptionClient + QuotaSnapshot — acceptance: 单元可解析 `subscription-userinfo` 字符串 (covers: S2)
- [ ] T3: HistoryStore 稀疏落盘 — acceptance: 阈值未达不写盘；达阈值原子写 JSON (covers: S2)
- [ ] T4: QuotaService 定时刷新 + 设置集成 — acceptance: 改间隔/URL 后生效；失败保留上次 (covers: S2)
- [ ] T5: Dock band 自包含标题 — acceptance: Title 含百分比与已用；Compact 可读 (covers: S2)
- [ ] T6: QuotaListPage 汇总+柱状图+zh/en — acceptance: 无配置/错误/成功三态不崩 (covers: S2)
- [ ] T7: MSIX 打包与本地部署脚本 — acceptance: `Add-AppxPackage` 成功且 CmdPal Reload 可见扩展 (covers: S2)
- [ ] T8: 自测清单与文档 — acceptance: 清单勾选 + Report 填写 (covers: S1; covers: S2)
