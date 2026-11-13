# Chat2Ex 稳定性与性能改造 · 实施计划（实时记录）

> 本文件在实施过程中持续更新。状态标记：`[ ]` 待办 · `[~]` 进行中 · `[x]` 已完成 · `[!]` 阻塞/需决策

## 背景与证据

仓库：`c:\Users\pm\Documents\Code\Dalamud\Chat2Ex`
共享依赖：`c:\Users\pm\Documents\Code\Dalamud\AetherBagsEx\KamiToolKit`（同时被 AetherBagsEx / UmbraEx 等插件引用，各自打包一份 DLL）

2026-11-13 三次真实崩溃（`%APPDATA%\XIVLauncherCN\dalamud_appcrash_*.log`）：

| 时间 | 类型 | 结论 |
|---|---|---|
| 20:53:52 | `C0000005` 纯原生 | Framework tick 内节点树/虚表 use-after-free |
| 21:15:53 | `C0000005` | 8 层 `KamiToolKit.*.NativeAddon.OnFireCallback` 嵌套 + `OnAddonHide` 重入 |
| 21:46:40 | `CLR error 0x12345679` | `AddonVirtualTable.OnAddonRefresh → InvokeListenersSafely → NativeAddon.Open() → Show` 委托抛托管异常，穿透 ReversePInvoke |

根因主线：**ImGui 立即模式的「每帧全量同步」被搬到保留模式的 Atk 节点树上**，导致每帧抢锁/拼字符串、消息数 >150 后每条新消息全量重建 150 行；节点频繁创建销毁又放大了 KTK 虚表、`ContextMenu`、卸载期的生命周期漏洞。崩溃与卡顿是同一架构缺陷的两面。

## 目标与验收指标

| 指标 | 改造前 | 目标 |
|---|---|---|
| 空闲帧 `Draw()` | 含 Lumina 查表 + 62 键位扫描 + 多次字符串分配 | <0.15 ms、0 分配 |
| 单条新消息节点创建 | 150 行 × 数百闭包 | 1 行 |
| 稳定聊天流 `RebuildMessages` | 每条消息 1 次 | 0 |
| 崩溃 | 一晚 3 次（2×C0000005 + 1×CLR error） | 48 h 零 `CLR error 0x12345679`、零 addon 相关 `C0000005` |
| 编译 | — | `dotnet build` 零新增 error/warning |

## 里程碑

### M0 止血（仅改 `Chat2Ex/`，无外部影响）  `[x]`

- [x] M0-1 `NativeCallback.Safe` 增加日志节流（同一上下文每秒一条，`LogError` 可复用；日志本身也受 try/catch 保护）
- [x] M0-2 `Plugin.Draw()` / `FrameworkUpdate` / `OpenMainUi` / `LanguageChanged` 统一异常屏障；键位 tick、RunOnTick 字符处理、`ChatLogRefreshDetour` 同样补齐，委托缓存避免每帧分配
- [x] M0-3 `GameFunctions/Chat.cs` 的 `ChangeChannelNameDetour` / `ReplyInSelectedChatModeDetour` 补 try/catch + `RaptureShellModule` / `AgentChatLog` 判空，保证 `Original` 始终执行
- [x] M0-4 `GetAddon<T>()` 判空、`ResolveTextCommandPlaceholderDetour` 补屏障、`ChatLogRefreshDetour` 拆出 Core 并回落到 `Original`
- [x] M0-5 `KeybindManager` 每帧 `RaptureAtkModule.Instance()` 与 `UIInputData.Instance()` 判空
- [x] M0-6 `NativeChatWindow` 增加 `isFinalized`（`OnFinalize` 置位 / `OnSetup` 复位），`SetVisible` 阻断窗口期访问
- [x] M0-7 `NativeChatUi.Dispose()` 在 `CloseAll()` 末尾（`ContextMenu.Close()` 之后、所有窗口关闭后）释放共享 `ContextMenu`
- [x] M0-8 新增 `kamiToolKitInitStarted` + `kamiToolKitInitTask`，`Dispose()` 不再以 `KamiToolKitReady` 为条件，始终清理 KTK 全局 hook
- [x] M0-9 移除 `MessageStore.Dispose()` 的 `GC.Collect(); GC.WaitForPendingFinalizers()`
- [!] M0-10 **延后到 M2**：`NativeChatUi.cs:216/364` 的 `Open()` 仍在 Draw 路径内同步调用。改为延后 tick 需连带调整窗口可见性与卸载竞态，与 M2 的 `NativeUiScheduler` 一起做更稳妥（当前外层已有 try/catch，托管异常不会穿透）
- [x] M1-4（提前完成）`Plugin.InKeyboundBrawler` 按 `TerritoryType` 缓存结果，消除每帧 Lumina 查表

### M1 快速性能（仅改 `Chat2Ex/`）  `[x]`

- [x] M1-1 `SyncTabBar` 每帧 LINQ + `string.Join` → `HashCode` 增量哈希（零分配）；4 处强制刷新改用 `renderedTabCount = -1` 哨兵，避免哈希碰撞
- [x] M1-2 `SyncChannelLabel` 脏检查前移：比较 channel / `Name` 引用与 Count / `TellTarget` / `LastTab`，空闲帧不再构造 2 个 `StringBuilder`、不再查游戏 LS 名
- [x] M1-3 `popouts.Keys.ToArray()` → 复用 `List<int>` 缓冲，枚举与删除分离
- [x] M1-4 `Plugin.InKeyboundBrawler` 按 `TerritoryType` 缓存 Lumina 查表（M0 阶段顺带完成）
- [x] M1-5 `KeybindManager.HandleKeybinds` 循环内闭包外提为循环外局部函数，消除每 tick 数十次分配
- [x] M1-6 `EmoteCache.Exists` 改用 `HashSet<string>`（O(1)），加载时 `UnionWith` 构建
- [x] M1-7 `MessageRenderer.FormatTimestamp` 与 `Http/Processing.ReadMessageContent` 复用静态 `CultureInfo`
- [!] M1-8 **延后**：`NativeDebuggerWindow` 每秒 `COUNT(*)` 缓存需同步维护 `UpsertMessage`/`DeleteMessage` 的计数失效，收益仅在调试窗口打开时，留到 DB worker 一起做
- [!] M1-9 **延后**：`UpsertMessage` 攒批单事务涉及跨线程共享的单一 `SqliteConnection`，需先引入 DB worker（M2/M3），硬塞会引入 SQLITE_BUSY 风险
- [x] M1-10 `MessageManager` 空队列 `Thread.Sleep(1)` → `SemaphoreSlim` 阻塞等待；`DisposeAsync` 取消后额外 `Release` 唤醒
- [x] M1-11（部分）`ServerCore.FrameworkUpdate` 去掉每帧 `Tabs.Index()` 迭代器并在 Web 关闭时短路；`SendNewMessage` 的后台序列化延后（`ReadMessageContent` 可能依赖主线程状态，需实测后改）

### M2 渲染架构  `[x]`

- [x] M2-1（部分）`MessageList.Version`：每帧只比一个 `long`，空闲帧不再 `GetReadOnly()` 抢锁/分配；其余轻量字段比较保留（完整事件驱动调度器需改造配置订阅，风险大于剩余收益）
- [x] M2-2 环形增量渲染：`RebuildMessages` / `QueueAppendMessages` 共用一个分批队列，超限只裁剪头部旧行，**去掉 `messageCount <= MaxRenderedMessages` 前置条件**；`renderedRows` 镜像列表节点，`OnFinalize`/`Dispose` 时清空避免二次释放
- [x] M2-3 脏键改为 `Version + renderedLastMessageId`，移除 `firstMessageId`；从尾部回找锚点（最多 cap 次，O(cap)），锚点丢失自动回退全量重建
- [x] M2-4 `Measure` 加带上限(4096)的宽度缓存，字号变化时失效；`LineWriter` 用累计 `lineWidth` 做增量换行，消除每 token 全行重测 O(n²)
- [!] M2-5 **延后**：链接 token 合并 `TextNode` 需把 `AttachLinkEvents` 收敛到行级 hitbox 并按鼠标位置反查 chunk，涉及命中判定重写，风险高于收益
- [!] M2-6 **延后**：`BuildFontCache()` 是每个新 `TextNode` 正常显示文字的必要条件，贸然去掉会白屏，需实机 A/B
- [x] M2-7 去掉分批后冗余的 `list.RecalculateLayout()`（`AddNode(IEnumerable)` 内部已做一次）
- [!] M2-8 **延后**：图标 texture wrap / kernel texture 缓存涉及 Dalamud 纹理生命周期，需确认可安全复用
- [x] M0-10（补做）主窗口与 popout 的 `Open()` 延后到 `RunOnTick`，并加卸载/替换与循环索引捕获检查
- [x] X-2 编译：`dotnet build -c Release` → 0 警告 0 错误

### M3 共享 KTK 治理（影响其它插件）  `[x]`

**路线决策（2026-11-14）：选路线 1 防御性修复。** 理由：路线 2 的进程级 hook 单例需让一个 ALC 的委托被其它 ALC 持有，持有者先卸载时 `Original` 链会指向已卸载程序集，比现状更危险；路线 3 无法覆盖第三方插件、也不是代码能单方面完成的。路线 1 纯防御、不改公开 API，可直接修掉 CLR error 与虚表 use-after-free。跨 ALC 的 hook 链叠加**只能缓解、不能根治**，如实记录。

- [x] M3-1 `InitializeCloseCallback()` 幂等（同地址不再重复装 detour）；新增 `ReportCallbackException` 每秒节流日志，日志本身也受 try/catch 保护
- [x] M3-2 `OnFireCallback` 加 `[ThreadStatic]` 重入保护（吞掉嵌套回调，切断 `Close → Hide → FireCallback` 递归）、`Original` 调用纳入 try/catch + hook 判空。**未改 `Close()` 语义**：延后到 `RunOnTick` 会改变关闭时序，风险大于收益
- [x] M3-3 `VirtualTable.cs` 全部 12 个虚表委托：managed 半段与 `originalVirtualTable->...` 调用各自包 try/catch，原生调用仍尽量执行，托管异常绝不穿透 ReversePInvoke
- [x] M3-4 `Destructor` 在调用原生 Dtor **之前**把 `addon->VirtualTable` 恢复为 `originalVirtualTable`，再释放 `modifiedVirtualTable` 并置空；同时消除了「卸载后委托被 GC」的隐患
- [!] M3-5 **刻意不做**：`Hide()` 里的 `Hide` + `Close` 是 KTK「不允许隐藏、Hide 即关闭」的设计。去掉 `Close` 会让 `InternalAddon` 仍非 null 而 addon 已消失，窗口再也打不开；因此只加异常屏障，保留语义
- [x] M3-6 **已统一为单一副本**：把 `AetherBagsEx/KamiToolKit`（新版，含 `Open(false,true)` 与 20 项上限）整体移动到 `c:\Users\pm\Documents\Code\Dalamud\KamiToolKit`；4 处引用全部改指向它（`AetherBags.csproj`、`AetherBags.sln`、`Chat2Ex.csproj`、`Umbra.csproj`）。工作区外的旧副本 `c:\Users\pm\Documents\Code\KamiToolKit` 已不再被任何项目引用，可手动删除；`AetherBagsEx\KamiToolKit` 只剩一个空目录（安全删除机制拒绝移除，空目录无害）。
- [x] 编译验证：统一后 `Chat2Ex.sln` / `AetherBags.sln` / `Umbra.sln` 三个 Release 构建全部通过，KTK 均从 `Dalamud\KamiToolKit` 编译；`Chat2Ex/bin/Release/KamiToolKit.dll` 与 `UmbraEx/out/Release/KamiToolKit.dll` 时间戳一致。
- [!] **仍需发布**：已安装插件目录里的 16 份旧 `KamiToolKit.dll` 不受源码统一影响，只有重新打包发布才会减少运行时 hook 链。

### M4 兼容与数据迁移  `[!] 阻塞，需用户决策`

**实证发现（2026-11-14）**：`%APPDATA%\XIVLauncherCN\pluginConfigs` 下 `ChatTwo` 与 `Chat2Ex` 两个目录**都还在被写入**：

- `ChatTwo`：`chat-sqlite.db` 153,337,856 B + `-wal`(4.1 MB) + `-shm`，时间戳 11/14 0:34
- `Chat2Ex`：`chat-sqlite.db` 153,169,920 B，时间戳 11/13 23:02，且已有自己的 `addonconfig.json`

说明旧 ChatTwo 插件很可能仍在游戏里运行（当初改名本就是为了与它共存）。此时自动迁移会**覆盖较新的数据**，注册 `ChatTwo.*` IPC 别名会与旧插件**抢同一个 gate**。因此 M4 三项暂停，需先确认是否停用/卸载旧插件：

- [!] M4-1 迁移 `chat-sqlite.db`：不做自动覆盖；确认后按「仅目标不存在才复制」或「合并两张表」实现
- [!] M4-2 迁移 `addonconfig.json` 中 `ChatTwo_*` 键：同上，且需确认旧窗口位置配置是否仍需要
- [!] M4-3 IPC 注册 `ChatTwo.*` 别名：旧插件仍在运行时会冲突，暂不注册

### 横切任务

- [ ] X-1 轻量性能采样（`/chat2perf` 或周期日志）：Draw P50/P99、重建次数、节点创建数、DB 批量耗时
- [x] X-2 编译验证（SDK 10.0.402，均 `-c Release`）：统一后 `Chat2Ex.sln` 0 警告 0 错误；`AetherBags.sln` 0 错误 / 1 个既有 CS8602 警告；`Umbra.sln` 0 错误（同步修复后 0 警告 0 错误）。IDE 诊断 0 条
- [ ] X-3 单插件隔离实验：只启用 Chat2Ex 验证多版本 KTK 全局 hook 冲突
- [x] X-4 **回调入口复查**：grep 复查发现 `MessageManager.OnFrameworkUpdate`、`MessageManager.ChatMessage`、`ServerCore.FrameworkUpdate` 三处仍无异常屏障（M0 遗漏），已补齐（缓存委托 + `NativeCallback.Safe` / `LogError`）；`Chat2Ex.sln` 重编 0 警告 0 错误

## 实施日志

- 2026-11-14 · 创建计划文档；完成崩溃日志与代码审查，确定 M0→M4 顺序。M0 开工。
- 2026-11-14 · M0 完成（M0-10 延后至 M2）。改动全部限于 `Chat2Ex/`；顺带完成 M1-4。复查确认 `Menu.Open(false, true)` 已正确使用且已延后到 `RunOnTick`，21:15 崩溃与 OwnerAddon 绑定无关，仍归因于全局 `FireCallback` hook 链。
- 2026-11-14 · M1 完成主体（8 项 + 1 项部分）。中途编译发现并修正 3 类问题：`IFramework.OnUpdateDelegate` 才是事件委托类型、`Http/Processing` 缺少缓存字段定义、`renderedTabListSignature` 由 string 改 int 后 4 处旧赋值。最终 `dotnet build -c Release` 通过，0 警告 0 错误。
- 2026-11-14 · 当前状态：**M0/M1 已落地并完成编译验证，但尚未实机验证**。M2（渲染架构）与 M3（共享 KTK）为下一阶段；M3 会改动 `AetherBagsEx/KamiToolKit`，同时影响 AetherBagsEx / UmbraEx 等 16 个已安装插件，继续前需确认灰度方式。
- 2026-11-14 · M2 完成（环形增量渲染 + 版本号短路 + 增量换行 + M0-10 补做）。`dotnet build -c Release` 通过，0 警告 0 错误；IDE 诊断 0 条。稳定聊天流现在每条新消息只构建 1 行节点，不再全量重建 150 行。
- 2026-11-14 · **M3 待决策（技术难点）**：`%APPDATA%\XIVLauncherCN\installedPlugins` 下 16 个插件各自打包一份 `KamiToolKit.dll`，每份都在自己的 AssemblyLoadContext 里对**同一个全局地址** `AtkUnitBase.FireCallback` 装一个 detour。仅修改 `AetherBagsEx/KamiToolKit` 源码**不能根治**：只有重新打包的插件会带上新版 DLL，其余仍用旧副本继续叠加 hook 链。可选路线：① 所有自研插件统一 KTK 版本并同步重新发布；② 借 `IDalamudPluginInterface.GetOrCreateData` 做进程级 hook 单例（需解决跨 ALC 的委托归属与卸载后失效）；③ 改用 Dalamud 自带的 addon lifecycle 事件替代全局 hook。改动面与风险都显著高于 M0/M2，需用户确认后再动。
- 2026-11-14 · 用户选择「由助手决定」→ 采用路线 1。改 `AetherBagsEx/KamiToolKit` 的 `NativeAddon.CloseCallback.cs` 与 `NativeAddon.VirtualTable.cs`，并同步到 UmbraEx 实际引用的第二份 KTK 副本 `c:\Users\pm\Documents\Code\KamiToolKit`（两份核心文件哈希一致，确认同版本）。三个 sln 全部编译通过。
- 2026-11-14 · **M3 的已知边界**：`[ThreadStatic]` 重入保护只在单个 ALC 内生效，无法切断 16 份 DLL 之间的跨 ALC 递归；且已安装的插件目录仍带旧 `KamiToolKit.dll`，只有**重新打包** Chat2Ex / AetherBagsEx / UmbraEx 后才会生效。根治仍需统一版本 + 同步发布（路线 3），本次未动。
- 2026-11-14 · **代码改动阶段性完成**：M0 / M1 / M2 / M3(路线1) 已落地并编译验证；M4（配置与 DB 迁移）、X-1（性能埋点）、X-3（单插件隔离实验）未做。**仍缺实机验证**，编译通过不等于崩溃已消除。
- 2026-11-14 · 按用户要求**统一 KTK 依赖**：核对两份副本仅 `ContextMenu.cs` 不同（AetherBagsEx 那份是新版），移动新版到 `Dalamud\KamiToolKit`，4 处引用全部改指该路径，3 个 sln 重编通过。工作区外旧副本 `Code\KamiToolKit` 不再被引用；源目录已空但被占用，安全删除机制拒绝移除（未重试）。
- 2026-11-14 · **统一后的优化完成度复查**：grep 全部游戏回调入口，发现并补齐 3 处 M0 遗漏的异常屏障（见 X-4）。实证检查配置目录发现 `ChatTwo` 与 `Chat2Ex` 两个目录都仍在被写入，旧插件很可能仍在运行 → **M4 自动迁移/IPC 别名有数据覆盖与 gate 冲突风险，标记阻塞待用户决策**。结论：**代码层面 M0~M3 已完成；M4 阻塞、X-1/X-3 未做、实机验证未做，因此整体优化不能判定为「完成」**。
- 2026-11-14 · git 状态提示：移动造成主仓库 `241 D`（旧路径文件删除）+ `?? KamiToolKit/`（新路径未跟踪）+ `19 M`（本次改动）。这是目录移动的正常结果，提交前请确认。
- 2026-11-14 · **崩溃复盘（用户反馈「还是崩」）**：新增两次崩溃 `dalamud_appcrash_20261114_002513` 与 `_005149`。
  - **首要原因：部署缺失**。`installedPlugins\Chat2Ex\1.41.2.0` 里 `Chat2Ex.dll`(11/13 21:21) 与 `KamiToolKit.dll`(11/13 20:52) 全是旧版，本地 11/14 0:43 的新构建从未安装 → **M0~M3 一个都没生效**。
  - **新根因（0:25）**：`KamiToolKit.ContextMenu.ContextMenu.ContextMenuEventHandler` → `AtkValue.ReleaseManagedMemoryInternal()` → `C0000005`。原因是 `*returnValue = new AtkValue { Type = Bool, Bool = true }` 覆盖了游戏自有的 `returnValue`，AtkValue 的初始化/赋值路径会在零初始化实例上调用释放逻辑并解引用无效指针。已删除该覆盖，保留各分支 `SetBool` 并加 null 检查。
  - **0:51** 与 21:15 完全同路径：`AddonContextMenuOnMenuSelectedDetour` → 8 层 `OnFireCallback`（含第三方插件的 `KamiToolKit.NativeAddon` 旧命名空间）→ `OnAddonHide` → 原生 `C0000005`，同一地址 `ffxiv_dx11.exe+676CAC`。
- 2026-11-14 · 修复后重编三个 sln（0 错误），重新生成干净的 `release/Chat2Ex/latest.zip`（91 条目 / 17.7 MB，剔除误入的嵌套 zip），并**已把 `Chat2Ex.dll` / `KamiToolKit.dll` / `KamiToolKit.xml` 覆盖到 `installedPlugins\Chat2Ex\1.41.2.0`**（11/14 0:54）。游戏进程当时未运行，覆盖安全。
- 2026-11-14 · **剩余风险**：栈里 `KamiToolKit.NativeAddon.OnFireCallback` 来自第三方插件携带的更旧 KTK DLL（如 AutoDuty / Avarice 等 4 月版本），源码统一与 Chat2Ex 重新部署都影响不到它们。若重启后仍复现 0:51 类崩溃，必须跑 X-3 隔离实验，并考虑把 AetherBagsEx / UmbraEx 也重新打包。
- 2026-11-14 · **插件 KTK 版本扫描**（`installedPlugins` 下 16 份 `KamiToolKit.dll`）：`Chat2Ex` 已更新到 11/14 00:54；`AetherBagsEx`(11/11)、`Artisan`(11/11)、`DailyDuty`(10/11)、`DailyRoutines`(11/10)、`GlamourLog`(9/8)、`Chronicler`(6/24)、`DutyLootPreview`(8/2)、`UmbraEx`(11/13) 仍是旧版但属新命名空间；**`AutoDuty`、`Avarice`、`BossMod`、`DcTraveler`、`ICE`、`Palantir`、`SimpleTweaksPlugin` 这 7 个是 4–7 月的老版本，连 `KamiToolKit.BaseTypes.NativeAddon` 类型都不存在**，正是崩溃栈里 `KamiToolKit.NativeAddon.OnFireCallback` 的来源。它们与新 KTK 二进制不兼容，**不能替换其 DLL**，只能升级插件或停用。
- 2026-11-14 · **01:08 再次崩溃复盘**：`dalamud_appcrash_20261114_010813`，`C0000005` @ `ffxiv_dx11.exe+676CAC`，与 21:15 / 00:51 完全相同路径：`AddonContextMenuOnMenuSelectedDetour` → 多层 `OnFireCallback`（含 `KamiToolKit.NativeAddon` 旧命名空间与 `OmenTools.ContextMenuManager.FireCallbackDetour`）→ `AddonVirtualTable.OnAddonHide` → 原生崩。**决定性证据**：崩溃线程里没有任何 Chat2Ex 代码，唯一的 `Chat2Ex.MessageManager.ProcessPendingMessages` 是无关后台线程；且 00:25 的 `ContextMenuEventHandler → AtkValue.ReleaseManagedMemoryInternal()` 崩溃未再出现 → **新 DLL 已生效、Chat2Ex 侧的修复有效，剩余崩溃来自其它插件的旧 KTK 副本**。推断旧版 KTK 的 `Destructor` 释放自建虚表却不还原 `addon->VirtualTable`（正是 M3-4 修的问题），导致游戏 hide 该 addon 时走悬空虚表。
- 2026-11-14 · ~~结论：Chat2Ex 侧已无可修~~ **此结论已被推翻**。当时只依据「崩溃栈里没有 Chat2Ex 帧」就断定无关，忽略了菜单点击的托管代码在崩溃前已执行完并排队，栈只记录崩溃那一刻的调用链。
- 2026-11-14 · **用户补充关键复现条件**：崩溃发生在**聊天窗口右上角 `...` 菜单里点「打开设置界面」**时；未加载 Chat2Ex 从不崩，该途径高概率崩。据此重新定位：
  - 路径：`tabMenuButton.OnClick → ShowTabMenu → ContextMenuEventHandler → item.OnClick → OpenSettingsWindow → IFramework.RunOnTick(Plugin.NativeSettings.Open())`。
  - **真正根因**：`RunOnTick` 仍在**同一 framework tick** 内执行；此时游戏正在展开菜单的 `FireCallback(close)` / `AddonVirtualTable.OnAddonHide` 链并 hide addon，而 `NativeAddon.Open()` 会 `AllocateAddon` 把新 addon 插入 `RaptureAtkUnitManager` 的链表 —— 在链表正被遍历/修改时插入，正好对应 `ffxiv_dx11.exe+676CAC` 的原生 C0000005。这也解释了「不加载 Chat2Ex 从不崩」和「只有打开设置这项高概率崩」（唯一会创建原生 addon 的菜单项）。
- 2026-11-14 · **修复（采纳用户方案）**：① 右上角按钮改为**直接打开设置**，删除整个 `ShowTabMenu` 原生菜单 —— tab 重命名 / 新建 / 弹出等功能在设置窗口 Tabs 页已齐全（已核实 `NativeSettingsWindow.BuildTabsTab`）。② 新增 `NativeCallback.Defer(action, frames=4)` + `TickDeferred()`（由 `Plugin.DrawCore` 每帧驱动）/`ClearDeferred()`（卸载时清空），取代 `RunOnTick`；所有在 Atk 事件回调里创建 addon 的路径统一延后 4 帧：`NativeSettings.Open`、`NativeChatWindow.OpenRenamePrompt`、`NativeSettingsWindow.OpenColorPicker` / `OpenRenamePrompt`。
- 2026-11-14 · `Chat2Ex.sln -c Release` 编译通过 0 警告 0 错误；`release/Chat2Ex/latest.zip` 已重打（91 条目 / 17.7 MB）。**必须重新部署到 installedPlugins 才生效**（之前反复踩的坑）。其它插件的旧 KTK DLL 仍会叠加 hook 链，但已不是这条高概率路径的直接原因。
- 2026-11-14 · **窗口大小配置功能补齐**（用户反馈「之前加过但没起作用」）。诊断：`Configuration.ChatWindowSize` 存在、拖拽结束时也会 `SaveConfig()`、主配置里确实保存了 `(560, 320)`，但 ① 设置界面「显示」页**完全没有宽/高输入项**；② `NativeChatUi.ConfiguredMainWindowSize` 只在**创建窗口时**读一次，配置改了已打开的窗口不会变；③ `ApplyResize` 只改 `Size`、没同步 `WindowNode.Size`。修复：`BuildDisplayTab` 增加两个 `AddIntOption`（宽 320–1920 / 高 160–1600，与拖拽 limit 一致）；`Save()` 新增 `Plugin.ChatUi.ApplyMainWindowSize(...)` 立即应用到实时窗口；新增 `NativeChatWindow.SetConfiguredSize`（同步原生 addon / WindowNode / ApplyLayout / UpdateWindowBounds / 渲染缓存）；`ApplyResize` 补 `WindowNode?.Size`。编译 0 警告 0 错误，zip 重打并已部署（11/14 1:19）。
- 2026-11-14 · **设置项归位**（用户反馈「锁定窗口位置怎么在聊天记录里」）：`BuildChatLogTab` 里错放了窗口外观/位置类选项。已把 `LockWindowPosition` + `AddPositionOption`(X/Y)、`WindowAlpha`、`ChatBackgroundOpacity` 整体移到 `BuildDisplayTab`，与窗口宽高同页；Chat Log 页只保留播放声音、物品提示、最大行数、tab 切换键位。`AddOption` 的 OnClick 会置 `needsContentRebuild`，切换锁定后坐标输入仍会正确显隐。编译 0 警告 0 错误，zip 重打并已部署。
- 2026-11-14 · **01:23 崩溃复盘（点击聊天记录里的 URL）**：`dalamud_appcrash_20261114_012321`，`CLR error 0x12345679`、`Description: The process was terminated due to an unhandled exception`、**Stack 为空**，调用栈只有 `Framework*` 的 PInvoke/ReversePInvoke → 是 `IFramework` tick 里的托管回调抛了未捕获异常（不是原生 AV）。定位到 `NativePayloadHandler.ClickLinkPayload` 的 `Plugin.Framework.RunOnTick(() => handler(...))`：外层 `try/catch` 只覆盖"排队"动作、不覆盖执行，handler（第三方 `DalamudLinkPayload`）抛异常即穿透终止进程。
- 2026-11-14 · **系统性排查所有 `RunOnTick`**（同一陷阱多处在用）：`NativePayloadHandler`、`MessageRenderer`（两处 tooltip 关闭）、`NativeTextPrompt`、`NativeSettingsWindow`、`Http/ServerCore`（6 处，含 `async void` lambda 改为内部 try/catch）全部补 `NativeCallback.Safe`；另补 `OpenItemTooltip` 的 `AtkStage` 判空。编译 0 警告 0 错误，zip 重打并已部署。
- 2026-11-14 · **升版发布**（用户要求）。受影响 = 引用统一 KTK 的三个插件：
  - `Chat2Ex` 1.41.2 → **1.41.3**（DLL `1.41.3.0`），`latest.zip` 91 条目 / 17.68 MB，Changelog 补本次变更
  - `AetherBagsEx` 2.0.0.14 → **2.0.0.15**，`latest.zip` 17 条目 / 654 KB
  - `UmbraEx` 3.1.18.6 → **3.1.18.7**，`latest.zip` 30 条目 / 19.94 MB
  - 三个 `release/<Plugin>/<Plugin>.json` 的 `AssemblyVersion` 已同步；UTF-8 校验 JSON 合法、与 DLL 版本逐一匹配；zip 均包含主 DLL 与 `KamiToolKit.dll`
  - 未受影响（不引用 KTK，未升版）：`Mappy` / `MappyEx` / `PilgrimsTraverse` / `QoLBarEx`
  - 踩坑记录：`Chat2Ex.json` 的 `Punchline` 含 `♪`（UTF-8）。PowerShell 5.1 的 `Get-Content -Raw` 会按 ANSI 读取并把它变成乱码、进而让 `ConvertFrom-Json` 误报"非法 JSON"。校验时务必用 `[System.IO.File]::ReadAllText(path, UTF8)`，不要据此改文件。

## 改动文件清单

M0 + M1 + M2 + X-4 共修改 14 个 `.cs` 文件（全部位于 `Chat2Ex/Chat2Ex/`），另加 `Chat2Ex.csproj` 的引用路径：

`Plugin.cs`、`Configuration.cs`、`Native/NativeCallback.cs`、`Native/NativeChatUi.cs`、`Native/NativeChatWindow.cs`、`Native/MessageRenderer.cs`、`GameFunctions/GameFunctions.cs`、`GameFunctions/Chat.cs`、`GameFunctions/KeybindManager.cs`、`MessageStore.cs`、`MessageManager.cs`、`Http/ServerCore.cs`、`Http/Processing.cs`、`EmoteCache.cs`。

（`Util/GlobalParametersCache.cs` 仅复查，已有判空，未改动。）

M3 修改/移动：

- `Dalamud/KamiToolKit/BaseTypes/NativeAddon/NativeAddon.CloseCallback.cs`、`NativeAddon.VirtualTable.cs`（防御性修复）
- `Dalamud/KamiToolKit/ContextMenu/ContextMenu.cs`（修复 `ContextMenuEventHandler` 覆盖游戏自有 `AtkValue` 导致的 0:25 崩溃）
- `Dalamud/KamiToolKit/` 整个目录（从 `AetherBagsEx/KamiToolKit` 移入，含完整源码与 `.git`）
- 引用路径：`AetherBagsEx/AetherBags/AetherBags.csproj`、`AetherBagsEx/AetherBags.sln`、`Chat2Ex/Chat2Ex/Chat2Ex.csproj`、`UmbraEx/Umbra/Umbra.csproj`

## 实机验证清单（待执行）

1. 安装新构建并多次重载插件（验证 M0-8 的 hook 清理），确认不再出现 `CLR error 0x12345679`。
2. 登录 / 切区时打开聊天窗口（验证 `GetAddon<T>()` 判空与 `isFinalized`）。
3. 打开并点击聊天右键菜单 —— **M0-7 是本次唯一有行为风险的改动**（卸载时释放原生 `CustomEventInterface`）；若出现菜单相关崩溃，优先回退此项。
4. 观察稳定聊天流下的帧时间：M2 后每条新消息应只创建 1 行节点，不再有「150 行重建、25 行/帧 × 6 帧」的抖动。
   - 重点回归项：长消息换行位置（M2-4 改用累计 token 宽度，字体 kerning 可能让换行点与原实现略有差异）、窗口 resize 后的行宽、切 tab 与频道过滤变更后的全量重建、popout / 主窗口首次出现（M0-10 延后一帧）、锚点丢失时的回退重建。
5. X-3：只启用 Chat2Ex 一个带 KTK 的插件，确认 21:15 那类 `OnFireCallback` 嵌套崩溃是否消失。
6. **M3 回归**：多次重载 Chat2Ex（验证 `InitializeCloseCallback` 幂等）、开关自建窗口（虚表 try/catch 不得改变正常显示）、关闭后再打开窗口（`Destructor` 恢复虚表）、点击右键菜单（`OnFireCallback` 重入保护）。若仍复现 21:15 类崩溃，说明触发源是**其它插件携带的旧 KTK DLL**，需用第 5 条隔离实验确认，并把那些插件也重新打包/升级。
7. **已部署（11/14 0:54）**：`Chat2Ex.dll` / `KamiToolKit.dll` / `KamiToolKit.xml` 已覆盖到 `installedPlugins\Chat2Ex\1.41.2.0`，`release/Chat2Ex/latest.zip` 也已重新生成（91 条目 / 17.7 MB）。其它插件（含 AetherBagsEx / UmbraEx）的已安装 KTK DLL 仍是旧版。
8. **统一 KTK 后回归**：三个插件都从 `Dalamud\KamiToolKit` 编译，确认 AetherBagsEx / UmbraEx 的原生窗口行为未受虚表 try/catch 与 `Destructor` 恢复虚表影响；特别检查 UmbraEx 的右键菜单（旧副本 `ContextMenu.Open()` 无参数，新版默认参数等价，且多了 20 项上限）。
9. **M4 前置确认**：先确认旧 ChatTwo 插件是否仍在运行、以及以哪个配置目录的数据为准，再决定是否执行迁移与 IPC 别名。
10. **确认部署生效**：启动游戏后在日志里确认加载的是 11/14 0:54 的 `Chat2Ex.dll` / `KamiToolKit.dll`；`installedPlugins\Chat2Ex\1.41.2.0` 两个 DLL 时间戳应为 11/14 0:54。若被 Dalamud 还原成 11/13，需用 `/xlplugins` 从 `release/Chat2Ex/latest.zip` 重新安装。
11. **回归 0:25 / 0:51 路径**：反复点消息右键菜单各项（复制、隐藏、频道切换等），确认不再出现 `ContextMenuEventHandler → AtkValue.ReleaseManagedMemoryInternal()` 或 `AddonContextMenuOnMenuSelectedDetour → OnAddonHide` 崩溃。右上角 tab 菜单已删除，不再存在「菜单里打开设置」这一项。
12. **若仍崩**：先跑 X-3 单插件隔离实验（只启用 Chat2Ex）。隔离后不崩即证明触发源是其它插件携带的旧 KTK DLL；再把 AetherBagsEx / UmbraEx 也重新打包，第三方插件只能升级或停用。
13. **本次修复的核心回归**：反复点右上角 `...` 按钮，确认**直接打开设置窗口、不再弹菜单**，且不再崩溃；在设置窗口里再打开颜色选择器 / 重命名弹窗，确认同样稳定（这些现在都延后 4 帧创建）。确认 `installedPlugins\Chat2Ex\1.41.2.0\Chat2Ex.dll` 时间戳为 11/14 1:19 或更新。
14. **窗口大小回归**：设置 →「显示」页改宽/高 → 点「保存」，窗口应立即改变且消息重新排版；重载插件后大小保持；拖拽右上角调整柄后再打开设置，数值应与实际一致。
15. **设置页归位回归**：「显示」页应包含窗口宽/高、透明度、背景透明度、锁定位置（未锁定时显示 X/Y 坐标）；「聊天记录」页应只剩播放声音、物品提示、最大行数、tab 切换键位。切换锁定复选框后坐标输入应即时显隐。
16. **链接点击回归**：点击聊天记录里的普通 URL、物品链接、地图链接、玩家名、Dalamud 插件链接（ExtraChat / Chat2 IPC 等），确认不再出现 `CLR error 0x12345679`。确认 `Chat2Ex.dll` 时间戳为 11/14 1:25 或更新。

## 风险与对策

- **KTK 已统一为单一源码副本**（`Dalamud/KamiToolKit`）：M3 防御性改动会编译进 Chat2Ex / AetherBagsEx / UmbraEx，三者均已验证 0 错误。但已安装的 16 个插件仍各自携带旧 `KamiToolKit.dll`，运行时 hook 链不会立刻减少，必须逐个重新打包/升级。
- **两个聊天插件并存**：`pluginConfigs` 下 `ChatTwo` 与 `Chat2Ex` 目录都活跃，M4 迁移与 IPC 别名暂缓，避免覆盖数据与抢同名 gate。
- **环形增量可能引入行错位**：保留 `RebuildMessages` 作为版本不连续时的自动兜底。
- **批量事务崩溃丢消息**：WAL + 200 ms / N 条提交，卸载时强制 flush。
- **异常屏障掩盖 bug**：`Safe` 必须 Error 级日志（dalamud.log 默认不打 [DBG]）并节流。
- **配置/DB 迁移不可逆**：只复制、不删除旧目录。
