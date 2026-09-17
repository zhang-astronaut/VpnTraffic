---
feature: vpn-traffic
status: delivered
updated: 2026-09-17
branch: feat/multi-sub-persist
commits: a1efe67..ebacc66
---

# VpnTraffic — Command Palette 订阅流量

## Report

**What was built** — 修复重启后订阅丢失：设置固定写入 `%LOCALAPPDATA%\VpnTraffic\settings.json`（不再使用会返回目录的 `BaseSettingsPath`）。新增多订阅：设置里每行 `Name|URL`，目录持久化于 `subscriptions.json`，每条订阅独立刷新循环、`history-{id}.json` 与 Dock band（`quota:{id}`），可在停靠栏分别固定。旧版单一 `subscriptionUrl` 自动迁移。审查后补上 **Id 按 URL 稳定保留**，避免改无关设置时冲掉 Dock 固定与历史文件。

**Verification**

| 命令 | 结果 |
| --- | --- |
| `dotnet build VpnTraffic/VpnTraffic.csproj -c Release` | PASS |
| `dotnet run --project VpnTraffic.Smoke -c Release` | ALL PASS（含 id preserved by url） |
| MSIX `build-msix` + `deploy-local` | PASS，`VpnTraffic 0.2.0.0` 已安装 |

**Journey log**

1. `Utilities.BaseSettingsPath` 对打包应用可能返回裸 `LocalState` 目录 → 必须落到具体 `.json` 文件。
2. Dock 固定身份是构造时的 `quota:{id}` 字符串；目录 Id 漂移会直接丢 pin。
3. `RaiseItemsChanged` 是 `ListPage` protected，只能在页面子类内调用。
4. 多订阅用「每行 Name|URL」比动态 Form 列表更贴合 JsonSettingsManager 能力。

## [S1] Problem

机场/代理订阅用户需要在不登录网页面板的情况下，随时看到订阅总流量、已用与剩余百分比。数据必须来自订阅链接本身的账号配额（多设备共用同一订阅），而不是本机网卡流量。PowerToys Command Palette 的 Dock 快捷栏适合常驻展示，设置页用于配置订阅与刷新策略；同时要降低磁盘写入与请求频率。

**Amendment (0.2)** — 用户报告：重启后已填写的订阅链接会丢失。根因：`Utilities.BaseSettingsPath("VpnTraffic")` 在本机返回包内 `LocalState` **目录**而非 `settings.json` 文件，`JsonSettingsManager.SaveSettings` 无法持久化。另需支持**同时配置多个订阅**，并**分别固定到 Dock**。

## [S2] Design

### Identity

| 项 | 值 |
| --- | --- |
| 扩展类名 / 项目 | `VpnTraffic` |
| 显示名 | `VpnTraffic` / 中文「VPN 流量」 |
| CLSID | `a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17` |
| Package Identity Name | `VpnTraffic` |
| Publisher | `CN=VpnTraffic` |
| Version | `0.2.0.0` |
| AppExtension | `com.microsoft.commandpalette` / Id=`VpnTraffic` / PublicFolder=`Public` |
| SDK | `Microsoft.CommandPalette.Extensions` 0.11.260520004 |
| TFM | `net10.0-windows10.0.26100.0` |

三处 CLSID 一致：`[Guid]`、`com:Class Id`、`CmdPalProvider/Activation/CreateInstance ClassId`。

### Persistence (0.2 fix)

- **禁止**单独依赖 `Utilities.BaseSettingsPath` 作为 `JsonSettingsManager.FilePath`。
- 固定路径：`%LOCALAPPDATA%\VpnTraffic\settings.json`（`AppPaths.SettingsFile`；包内会重定向到 `LocalCache\Local\VpnTraffic\`，与已验证可写的 `history*.json` 同目录）。
- `SaveSettings` 失败写 `Diag.log`，不静默吞掉。
- 目录：`subscriptions.json` 为多订阅目录文件；历史：`history-{id}.json`。

### Multi-subscription + Dock pins

```text
VpnTrafficCommandsProvider
  ├─ SubscriptionCatalog  (Name|URL lines + subscriptions.json)
  ├─ SubscriptionRuntime[] (each: QuotaService + WrappedDockItem id=quota:{id})
  ├─ TopLevel → QuotaListPage (all subs)
  └─ GetDockBands → one band per subscription (user pins independently)
```

- 设置项 `subscriptions`：多行 `Name|https://...`（`#` 注释；非法 URL 忽略）。
- 迁移：若目录空且存在旧键 `subscriptionUrl`，写入目录为 `Default|<url>` 并回填设置表单。
- 每条订阅独立 `QuotaService` / 历史文件 / Dock 标题 `Name · 45% · 12.3GB`。
- 无订阅时仍暴露一个 `quota:empty` 占位 band。

### Data source

HTTP GET 订阅 URL，解析响应头 `subscription-userinfo`（多 UA：clash.meta 等）。失败保留上次成功数据并标注 Error。

### Settings

路径：`%LOCALAPPDATA%\VpnTraffic\settings.json`

| Key | 类型 | 默认 |
| --- | --- | --- |
| `subscriptions` | TextSetting multiline | `""` |
| `subscriptionUrl` | TextSetting（仅迁移） | `""` |
| `refreshIntervalSeconds` | ChoiceSetSetting | `60` |
| `showHistoryChart` | ToggleSetting | `true` |
| `persistHistory` | ToggleSetting | `true` |
| `maxHistoryPoints` | ChoiceSetSetting | `48` |

### History & disk wear

- 每订阅 `history-{id}.json`；落盘条件不变（≥15min 或 ≥64MiB）。
- Load 合并内存点。

## Out of Scope

- 节点解析 / 代理连通性
- 本机网卡流量
- 画廊上架（WinGet 已有 0.1.0 提交）

## Tasks

- [x] T9: 修复 settings.json 路径与 Save 诊断 — acceptance: 设置后文件存在且重启后 URL 仍在 (covers: S2 Persistence)
- [x] T10: SubscriptionCatalog + 多 Runtime/Dock — acceptance: 多行 Name|URL 产生多个 GetDockBands (covers: S2 Multi-sub)
- [x] T11: 旧 URL 迁移 + 表单回填 — acceptance: 仅有 subscriptionUrl 时目录与表单被填充 (covers: S2 Multi-sub)
- [x] T12: 0.2.0 构建/部署/冒烟 — acceptance: smoke ALL PASS + MSIX 0.2.0.0 安装 (covers: S2)
- [x] T13: Id 按 URL 稳定 — acceptance: 改名后 Id 不变；无关设置不重写目录 (covers: S2 Multi-sub)
