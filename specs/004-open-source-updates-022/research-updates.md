# 0.2.2 更新机制调研

日期 2026-10-18。仅研究与方案建议，未安装依赖、未修改应用、未执行 Windows 更新验收。API 依据下列官方文档和上游 `develop` 源码；实施时必须锁定发布版并再次核对 API，不能把上游源码可用等同于已在本项目验证。

## 已核实的本仓库事实

- `Sources/TypstStudio/AutomaticUpdater.swift` 已使用 Sparkle 的 `SPUUserDriver` 实现非模态下载、进度和等待用户重启；`prepareToRestart` 是退出前保护入口。无需重写 Mac 更新器。
- `scripts/sign-update.sh` 使用本机 Keychain 中的私钥签署 DMG/appcast；`scripts/publish-update-manifest.py` 校验 DMG 签名、架构和版本后发布 `update.json`。这个 JSON 本身不是签名清单，不能据此声称清单获得密码学认证。
- Windows 是 WinUI 3、`net10.0-windows`、unpackaged、自包含；Inno 安装器以 `PrivilegesRequired=lowest` 写入 LocalAppData，使用固定 AppId。当前 `[Run]` 含 `skipifsilent`，静默安装不会通过该项自动启动应用。
- 项目原则要求本地构建、不增加 GitHub Actions，并保持 Windows 独立标签与 Mac 更新源隔离。

## 已核实的外部事实

### NetSparkle 可做原生 WinUI 界面的底层更新器

官方提供 core 包 `NetSparkleUpdater.SparkleUpdater`；内置界面是 WinForms、WPF、Avalonia，没有官方内置 WinUI 3 界面。可自行实现 `IUIFactory`，也可不设 UIFactory，由应用处理事件。其 .NET 6+ 定位使其成为 .NET 10 候选，但仍需本项目 ARM64/x64 构建验证。[官方 README](https://github.com/NetSparkleUpdater/NetSparkle#installing-netsparkle)

源码确认 `CheckForUpdatesQuietly()` 返回 `Task<UpdateInfo>`，`InitAndBeginDownload(AppCastItem)` 可启动下载，`InstallUpdate(...)` 会再次验证文件并调用可取消的关闭准备流程；事件模式还需要应用处理 `CloseApplicationAsync`。`PreparingToExitAsync` 可设置 Cancel，适合“保存／不保存／取消”。Windows 安装辅助脚本等待原进程退出，最多约 90 秒，之后运行安装器；不能在关闭事件中才开始长时间询问用户。`.exe` 可直接作为安装包。[SparkleUpdater 源码](https://github.com/NetSparkleUpdater/NetSparkle/blob/develop/src/NetSparkle/SparkleUpdater.cs)

`SecurityMode.Strict` 要求 feed 和安装文件均有签名及公钥；发布流程包含 appcast 的独立 `.signature` 文件。`Ed25519Checker` 是现成验签实现。应固定公钥而非从同一个远端清单下载信任根；不要使用 Unsafe 或可缺省验签的模式。[SecurityMode](https://github.com/NetSparkleUpdater/NetSparkle/blob/develop/src/NetSparkle/Enums/SecurityMode.cs)、[Ed25519Checker](https://github.com/NetSparkleUpdater/NetSparkle/blob/develop/src/NetSparkle/SignatureVerifiers/Ed25519Checker.cs)、[官方发布步骤](https://github.com/NetSparkleUpdater/NetSparkle#how-updates-work)

### GitHub API + 下载器不是完整更新系统

公开 Release 可匿名读取，返回标签、draft/prerelease、资产名称、大小、下载 URL 和 digest；`latest` 属于整个仓库，不是 Windows 或某架构的 latest，`make_latest=false` 可避免 Windows Release 抢占现有 Mac latest。匿名 REST 请求主要限额是每来源 IP 每小时 60 次。实现自有检查器必须处理标签过滤、分页、版本排序、限流与缓存，不能把列表第一项当 Windows 最新版本。[Release API](https://docs.github.com/en/rest/releases/releases)、[限流文档](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)

推论：`HttpClient + SHA-256 + Process.Start` 虽短，但还缺开发者签名信任、取消和恢复、进程退出交接、安装失败与重启策略。来自同一可变远端的安装器与 SHA-256 只能检验一致性，不能代替独立固定公钥的开发者签名。采用自有清单时必须先验清单签名，再校验受签名保护的版本、平台、架构、URL、长度和文件摘要。

### 非阻塞、安装与重启约束

WinUI `DispatcherQueue` 用于将 UI 操作送回所属线程；“async 方法”本身不保证内部的同步哈希或解析不占 UI 线程。NetSparkle 签名验证源码包含同步文件读取，因此需把更新工作与界面状态桥接明确分开。[Microsoft DispatcherQueue](https://learn.microsoft.com/en-us/windows/apps/develop/dispatcherqueue)、[验签源码](https://github.com/NetSparkleUpdater/NetSparkle/blob/develop/src/NetSparkle/SignatureVerifiers/Ed25519Checker.cs)

Inno `/NORESTART` 只禁止操作系统重启，不负责应用重启；`/CLOSEAPPLICATIONS` 与 `/FORCECLOSEAPPLICATIONS` 可关闭应用，不应取代编辑器的保存流程。`skipifsilent` 会跳过静默安装的启动项。[Inno 参数](https://jrsoftware.org/ishelp/topic_setupcmdline.htm)、[Run 项](https://jrsoftware.org/ishelp/topic_runsection.htm)

Sparkle 已提供自定义用户驱动及签名归档。2.9+ 还支持 `SURequireSignedFeed`，需同时启用 `SUVerifyUpdateBeforeExtraction`；签名 feed 与安装包签名是两个不同保护层。不能假定 NetSparkle 的独立 `.signature` 格式与 Sparkle 的签名 feed 格式互通。[Sparkle 设置](https://sparkle-project.org/documentation/customization/)、[发布指南](https://sparkle-project.org/documentation/publishing/)

## 建议方案（设计判断，尚待批准与实测）

**保留 Mac Sparkle；Windows 采用 NetSparkle core + 一个很薄的原生 WinUI 更新状态服务 + 现有 Inno 安装器。** 不引入 WPF/Avalonia UI，不做跨平台更新抽象，不自行重写密码学、下载校验和退出交接。对当前双平台产品，这比完整自研 updater 更容易长期维护。

1. 首个窗口可交互后触发一次检查。后台完成网络、下载和验签，仅通过 DispatcherQueue 更新 InfoBar/进度。采用 quiet/event 模式，避免默认升级对话框；每次启动只运行一个检查，不使用重复循环。所有任务观察异常，支持超时和下载取消；失败不阻断编辑。
2. 匹配当前安装的进程/包架构，分别提供 Windows ARM64、x64 feed。ARM64 上运行 x64 安装版仍走 x64 feed，不在更新中隐式换架构。feed 不混入 Mac 包。版本采用明确数值比较，拒绝当前版本或降级；缺包时保持当前安装。
3. 推荐公开发布仓库中维护固定路径的 Windows feeds，例如 `updates/windows/arm64/appcast.xml` 与 `updates/windows/x64/appcast.xml`，安装器 URL 指向独立 Windows Release 标签的不可变版本资产。feed 和 `.signature` 一起提交；读取跨更新瞬间不匹配时安全失败并稍后重试。若不希望存放仓库文件，可用独立固定 feed Release，但不要依赖全仓库 `/latest/download/`。Mac 既有 feed URL 与 latest 选择继续保持兼容。
4. 本地生成独立 Windows Ed25519 密钥、离线保管并把公钥编入应用；签署两个安装器及对应 feeds。上传完整版本资产并验证之后，最后推进 feed。只发布安装器/校验和而不更新 feed，视为未完成更新发布。Mac 使用其既有密钥及工具；不为“统一”迁移密钥格式。
5. 下载验签完成后停在“更新已准备好”。只有用户点击重启安装后才进入统一退出流程。所有脏文档、保存失败、外部修改冲突都必须允许取消；取消后保留已验证安装包和正常编辑状态。准备通过后才调用安装交接，`CloseApplicationAsync` 只做已经批准的关闭，不再弹保存问题。
6. 安装参数固定于本机代码，不从远端接受任意命令。使用 `/NORESTART`，禁止强制杀进程。重启只选择一个负责方。建议 Inno 在成功的更新安装后启动应用、NetSparkle `RelaunchAfterUpdate=false`；这需要给现有 `[Run]` 增加受控的更新模式并验证，不能直接套用当前 `skipifsilent`。失败时保留安装日志及用户可重新启动旧应用的路径，不能声称 Inno 天然支持事务回滚。

自研 GitHub API 路线的合理备选是“仅提示新版本并打开官方 Release 页”。如果产品仍要求应用内下载、验证、重启安装，则它需要补齐上述完整安全和生命周期逻辑，当前不推荐。

## 实施前必须验证的边界

- 锁定 NetSparkle NuGet 版本；在 WinUI .NET 10 的 ARM64、x64 包分别验证 quiet 检查、事件线程、签名校验、取消和安装交接。上游 develop 不是可直接视为已发布的 API 合同。
- 实测弱网/断网/超时、404、损坏签名、feed 与签名不同步、缺架构资产、重复点击、取消下载、磁盘满。编辑和预览应持续响应；记录设备及延迟口径。
- 用真实旧版本安装目录测试升级；覆盖未保存文本、保存失败、保存取消、多窗口和外部冲突。确认退出前不会执行安装器。
- 覆盖安装取消/失败、安装成功只启动一次、路径含空格和非 ASCII、Windows ARM64 与 x64；检查自包含资源完整。未做这些测试前，只能称方案可行候选，不能称升级链路已验收。
