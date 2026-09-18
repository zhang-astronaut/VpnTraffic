---
feature: vpn-traffic
status: delivered
updated: 2026-09-17
branch: feat/multi-sub-persist
commits: a1efe67..HEAD
---

# VpnTraffic 鈥?Command Palette 璁㈤槄娴侀噺

## Report

**What was built** 鈥?淇閲嶅惎鍚庤闃呬涪澶憋細璁剧疆鍥哄畾鍐欏叆 `%LOCALAPPDATA%\VpnTraffic\settings.json`锛堜笉鍐嶄娇鐢ㄤ細杩斿洖鐩綍鐨?`BaseSettingsPath`锛夈€傛柊澧炲璁㈤槄锛氳缃噷姣忚 `Name|URL`锛岀洰褰曟寔涔呭寲浜?`subscriptions.json`锛屾瘡鏉¤闃呯嫭绔嬪埛鏂板惊鐜€乣history-{id}.json` 涓?Dock band锛坄quota:{id}`锛夛紝鍙湪鍋滈潬鏍忓垎鍒浐瀹氥€傛棫鐗堝崟涓€ `subscriptionUrl` 鑷姩杩佺Щ銆傚鏌ュ悗琛ヤ笂 **Id 鎸?URL 绋冲畾淇濈暀**锛岄伩鍏嶆敼鏃犲叧璁剧疆鏃跺啿鎺?Dock 鍥哄畾涓庡巻鍙叉枃浠躲€?

**Verification**

| 鍛戒护 | 缁撴灉 |
| --- | --- |
| `dotnet build VpnTraffic/VpnTraffic.csproj -c Release` | PASS |
| `dotnet run --project VpnTraffic.Smoke -c Release` | ALL PASS锛堝惈 id preserved by url锛?|
| MSIX `build-msix` + `deploy-local` | PASS锛宍VpnTraffic 0.2.0.0` 宸插畨瑁?|

**Journey log**

1. `Utilities.BaseSettingsPath` 瀵规墦鍖呭簲鐢ㄥ彲鑳借繑鍥炶８ `LocalState` 鐩綍 鈫?蹇呴』钀藉埌鍏蜂綋 `.json` 鏂囦欢銆?
2. Dock 鍥哄畾韬唤鏄瀯閫犳椂鐨?`quota:{id}` 瀛楃涓诧紱鐩綍 Id 婕傜Щ浼氱洿鎺ヤ涪 pin銆?
3. `RaiseItemsChanged` 鏄?`ListPage` protected锛屽彧鑳藉湪椤甸潰瀛愮被鍐呰皟鐢ㄣ€?
4. 澶氳闃呯敤銆屾瘡琛?Name|URL銆嶆瘮鍔ㄦ€?Form 鍒楄〃鏇磋创鍚?JsonSettingsManager 鑳藉姏銆?

## [S1] Problem

鏈哄満/浠ｇ悊璁㈤槄鐢ㄦ埛闇€瑕佸湪涓嶇櫥褰曠綉椤甸潰鏉跨殑鎯呭喌涓嬶紝闅忔椂鐪嬪埌璁㈤槄鎬绘祦閲忋€佸凡鐢ㄤ笌鍓╀綑鐧惧垎姣斻€傛暟鎹繀椤绘潵鑷闃呴摼鎺ユ湰韬殑璐﹀彿閰嶉锛堝璁惧鍏辩敤鍚屼竴璁㈤槄锛夛紝鑰屼笉鏄湰鏈虹綉鍗℃祦閲忋€侾owerToys Command Palette 鐨?Dock 蹇嵎鏍忛€傚悎甯搁┗灞曠ず锛岃缃〉鐢ㄤ簬閰嶇疆璁㈤槄涓庡埛鏂扮瓥鐣ワ紱鍚屾椂瑕侀檷浣庣鐩樺啓鍏ヤ笌璇锋眰棰戠巼銆?

**Amendment (0.2)** 鈥?鐢ㄦ埛鎶ュ憡锛氶噸鍚悗宸插～鍐欑殑璁㈤槄閾炬帴浼氫涪澶便€傛牴鍥狅細`Utilities.BaseSettingsPath("VpnTraffic")` 鍦ㄦ湰鏈鸿繑鍥炲寘鍐?`LocalState` **鐩綍**鑰岄潪 `settings.json` 鏂囦欢锛宍JsonSettingsManager.SaveSettings` 鏃犳硶鎸佷箙鍖栥€傚彟闇€鏀寔**鍚屾椂閰嶇疆澶氫釜璁㈤槄**锛屽苟**鍒嗗埆鍥哄畾鍒?Dock**銆?

## [S2] Design

### Identity

| 椤?| 鍊?|
| --- | --- |
| 鎵╁睍绫诲悕 / 椤圭洰 | `VpnTraffic` |
| 鏄剧ず鍚?| `VpnTraffic` / 涓枃銆孷PN 娴侀噺銆?|
| CLSID | `a7c3e91b-5d2f-4b8e-9c1a-6f0d2e8b4a17` |
| Package Identity Name | `VpnTraffic` |
| Publisher | `CN=VpnTraffic` |
| Version | `0.2.0.0` |
| AppExtension | `com.microsoft.commandpalette` / Id=`VpnTraffic` / PublicFolder=`Public` |
| SDK | `Microsoft.CommandPalette.Extensions` 0.11.260520004 |
| TFM | `net10.0-windows10.0.26100.0` |

涓夊 CLSID 涓€鑷达細`[Guid]`銆乣com:Class Id`銆乣CmdPalProvider/Activation/CreateInstance ClassId`銆?

### Persistence (0.2 fix)

- **绂佹**鍗曠嫭渚濊禆 `Utilities.BaseSettingsPath` 浣滀负 `JsonSettingsManager.FilePath`銆?
- 鍥哄畾璺緞锛歚%LOCALAPPDATA%\VpnTraffic\settings.json`锛坄AppPaths.SettingsFile`锛涘寘鍐呬細閲嶅畾鍚戝埌 `LocalCache\Local\VpnTraffic\`锛屼笌宸查獙璇佸彲鍐欑殑 `history*.json` 鍚岀洰褰曪級銆?
- `SaveSettings` 澶辫触鍐?`Diag.log`锛屼笉闈欓粯鍚炴帀銆?
- 鐩綍锛歚subscriptions.json` 涓哄璁㈤槄鐩綍鏂囦欢锛涘巻鍙诧細`history-{id}.json`銆?

### Multi-subscription + Dock pins

```text
VpnTrafficCommandsProvider
  鈹溾攢 SubscriptionCatalog  (Name|URL lines + subscriptions.json)
  鈹溾攢 SubscriptionRuntime[] (each: QuotaService + WrappedDockItem id=quota:{id})
  鈹溾攢 TopLevel 鈫?QuotaListPage (all subs)
  鈹斺攢 GetDockBands 鈫?one band per subscription (user pins independently)
```

- 璁剧疆椤?`subscriptions`锛氬琛?`Name|https://...`锛坄#` 娉ㄩ噴锛涢潪娉?URL 蹇界暐锛夈€?
- 杩佺Щ锛氳嫢鐩綍绌轰笖瀛樺湪鏃ч敭 `subscriptionUrl`锛屽啓鍏ョ洰褰曚负 `Default|<url>` 骞跺洖濉缃〃鍗曘€?
- 姣忔潯璁㈤槄鐙珛 `QuotaService` / 鍘嗗彶鏂囦欢 / Dock 鏍囬 `Name 路 45% 路 12.3GB`銆?
- 鏃犺闃呮椂浠嶆毚闇蹭竴涓?`quota:empty` 鍗犱綅 band銆?

### Data source

HTTP GET 璁㈤槄 URL锛岃В鏋愬搷搴斿ご `subscription-userinfo`锛堝 UA锛歝lash.meta 绛夛級銆傚け璐ヤ繚鐣欎笂娆℃垚鍔熸暟鎹苟鏍囨敞 Error銆?

### Settings

璺緞锛歚%LOCALAPPDATA%\VpnTraffic\settings.json`

| Key | 绫诲瀷 | 榛樿 |
| --- | --- | --- |
| `subscriptions` | TextSetting multiline | `""` |
| `subscriptionUrl` | TextSetting锛堜粎杩佺Щ锛?| `""` |
| `refreshIntervalSeconds` | ChoiceSetSetting | `60` |
| `showHistoryChart` | ToggleSetting | `true` |
| `persistHistory` | ToggleSetting | `true` |
| `maxHistoryPoints` | ChoiceSetSetting | `48` |

### Dock (0.3 display hierarchy)

CmdPal **Title** 鏄ぇ鍙蜂富鏂囨锛涙満鍦哄悕涓嶅緱鎶㈠崰 Title銆?

| 鐘舵€?| Title锛堝ぇ锛?| Subtitle锛堢簿绠€妯″紡闅愯棌锛?|
| --- | --- | --- |
| 鏈夐厤棰?| `{pct}% 路 {used}` | `{鍚嶇О} 路 鍓﹞left} 路 鍒版湡{days}` |
| 鏈夐敊 | `鈿?{403\|no-userinfo\|鈥` | `{鍚嶇О} 路 {閿欒鎻愮ず}` |
| 鏃犻厤缃?| `{鍚嶇О}` | `鏈厤缃闃卄 |

鍒楄〃椤典富琛岋細`{pct}% 路 {used} / {total}`锛屽壇琛屽悓 Dock Subtitle銆?

### History & disk wear

- 姣忚闃?`history-{id}.json`锛涜惤鐩樻潯浠朵笉鍙橈紙鈮?5min 鎴?鈮?4MiB锛夈€?
- Load 鍚堝苟鍐呭瓨鐐广€?

## Out of Scope

- 鑺傜偣瑙ｆ瀽 / 浠ｇ悊杩為€氭€?
- 鏈満缃戝崱娴侀噺
- 鐢诲粖涓婃灦锛圵inGet 宸叉湁 0.1.0 鎻愪氦锛?

## Tasks

- [x] T9: 淇 settings.json 璺緞涓?Save 璇婃柇 鈥?acceptance: 璁剧疆鍚庢枃浠跺瓨鍦ㄤ笖閲嶅惎鍚?URL 浠嶅湪 (covers: S2 Persistence)
- [x] T10: SubscriptionCatalog + 澶?Runtime/Dock 鈥?acceptance: 澶氳 Name|URL 浜х敓澶氫釜 GetDockBands (covers: S2 Multi-sub)
- [x] T11: 鏃?URL 杩佺Щ + 琛ㄥ崟鍥炲～ 鈥?acceptance: 浠呮湁 subscriptionUrl 鏃剁洰褰曚笌琛ㄥ崟琚～鍏?(covers: S2 Multi-sub)
- [x] T12: 0.2.0 鏋勫缓/閮ㄧ讲/鍐掔儫 鈥?acceptance: smoke ALL PASS + MSIX 0.2.0.0 瀹夎 (covers: S2)
- [x] T13: Id 鎸?URL 绋冲畾 鈥?acceptance: 鏀瑰悕鍚?Id 涓嶅彉锛涙棤鍏宠缃笉閲嶅啓鐩綍 (covers: S2 Multi-sub)
- [x] T14: Dock/鍒楄〃 Title 浼樺厛宸茬敤涓庡崰姣?鈥?acceptance: Title 褰㈠ `45% 路 12.3GB`锛屽悕绉板湪 Subtitle (covers: S2 Dock 0.3)

