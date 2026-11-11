# 物品图标角标（Badge）维护说明

改角标的大小、位置、贴图、判定条件之前先看这份。基于 AetherBagsEx `2.0.0.13`。

## 1. 总览

背包格子是 `InventoryDragDropNode`（继承 KTK `DragDropNode`）。右上角的小标记不是游戏原生画的，而是挂到 `IconNode` 上的独立 `IconImageNode` 子节点，一共三种：

| 角标 | 字段 | 图标来源 | 触发条件 | 当前尺寸 / 位置 |
| --- | --- | --- | --- | --- |
| 武器 / 工具职业纹章 | `_jobIconNode` | `ArmouryJobGroups.GetJobIconId()`，映射 623xx / 624xx / 625xx | 物品是武器或工具，且分组能归到单一职业 | `24×24` @ `(18, 3)` |
| 已收集 | `_collectedIconNode` | `CollectionStatus.CollectedIconId`（当前 `71112`） | `CollectionStatus.IsCollected(item.ItemId)` | `24×24` @ `(18, 3)`；同格有职业纹章时 `(18, 21)` |
| 外部 IPC 徽章 | `_badgeNode` | `ItemDecoration.Badge`（`BadgeInfo`） | IPC 源提供了装饰 | `16×16`，四角由 `BadgePosition` 决定 |

另外两类视觉标记**不是**独立节点，别在上面浪费时间：

- `IconNode.IsGlamoured` → 原生 `IconComponentFlags.IsGlamoured`。目前 `ApplyIconMarker()` 写成 `_isCollectable && !_isCollected`，只对「未登记的收藏品」保留原生标记。
- HQ 右上角金色闪光：`InventoryItemExtensions.GetIconId()` 对 HQ 物品用 `iconId + 1_000_000`，是图标贴图自带的内容。

## 2. 文件与职责

| 文件 | 作用 |
| --- | --- |
| `Nodes/Inventory/InventoryDragDropNode.cs` | **唯一真正创建 / 定位角标节点的地方**。`ApplyCollectedBadge()`、`CollectedBadgePosition()`、`JobIconId` setter、`ApplyBadge()`、`ResetForReuse()` 都在这里 |
| `Nodes/Inventory/InventoryCategoryNode.cs` | 把数据写到节点上：`ApplyItemDataToNode()` 与 `RefreshNodeVisuals()` 中设置 `JobIconId` / `IsCollectable` / `IsCollected` |
| `Helpers/CollectionStatus.cs` | `IsCollected(uint itemId)` 判定 + `CollectedIconId` 常量 |
| `Inventory/Categories/ArmouryJobGroup.cs` | `GetJobIconId()` 与 `GetJobIconIdForJob()` 的职业 → 图标 ID 映射表（`62403` = WAR） |
| `IPC/ExternalCategorySystem/IExternalItemSource.cs` | `ItemDecoration` / `BadgeInfo` / `BadgePosition` 定义 |
| `IPC/ExternalCategorySystem/ExternalCategoryManager.cs` | 装饰缓存与 `GetDecoration()` |
| `Extensions/InventoryItemExtensions.cs` | HQ 图标 ID 变体 |

`Tags/` 目录是服务器信息栏（DTR）的文本占位符，和图标角标**无关**。

## 3. 数据链路

```
InventoryStateBase.RefreshFromGame
  → InventoryScanner / CategoryBucketManager（含 ArmouryJobGroups.ResolveBucketKey）
  → InventoryFilter → InventoryAddonBase.Refresh
  → CategoriesNode.SyncWithListDataByKey
  → InventoryCategoryNode.UpdateItemGrid()
  → _itemGridNode.SyncWithListDataByKey<ItemInfo, InventoryDragDropNode, ulong>
       ├─ createNodeMethod : CreateInventoryDragDropNode()
       ├─ updateNode       : node.ItemInfo = data; ApplyItemDataToNode(node, data)
       └─ resetNodeForReuse: node.ResetForReuse()
```

`ApplyItemDataToNode()` 中的顺序很重要：`IconId` → `JobIconId` → `IsCollectable` → `IsCollected`。因为绿勾位置依赖 `_jobIconId`，必须等 `JobIconId` 先赋值。延迟填充路径还会走 `RefreshNodeVisuals()`，那里也会重设 `IsCollectable` / `IsCollected`。

## 4. 关键坑（踩过的）

### 4.1 贴图采样：只画出一角

`IconImageNode` 构造时把 part 的 `TextureSize` 固定为 `32×32`。只设 `WrapMode.Stretch` **不会**让整张贴图适配节点，游戏只采样贴图左上角 `32×32` 再拉伸到节点尺寸。

- 武器纹章（623xx / 624xx / 625xx）贴图刚好匹配 `32×32`，`WrapMode.Stretch` 显示正常 —— **不要给它加 `FitTexture`**，实测改动会破坏已正常的效果。
- `94646` 那类图标尺寸不同，只设 `Stretch` 会「只在下面露一角」。解决办法是 `FitTexture = true`（= `ImageNodeFlags.AutoFit` + `WrapMode.Stretch`，见 KTK `Nodes/ImageNode.cs`）。`_collectedIconNode` 目前就带着这个。

换图标 ID 后如果发现只显示一角，先看这里，而不是先怀疑位置。

### 4.2 坐标系与 attach 行为

`_xxxIconNode.AttachNode(IconNode)` 里的 `IconNode` 是 KTK 的 `ComponentNode`。`NodeBase.PerformNativeAttach()` 会把 `AsLastChild` 重定向成 `AfterAllSiblings`，并挂到 `Component->UldManager.RootNode` 的兄弟链末尾（因此画在 IconExtras 之上）。`Position` 是 `IconNode` 的局部坐标，默认格子 `44×48`。

定位公式（贴右上角、右侧留 2px、上方留 3px）：

```
x = 44 - size - 2
y = 3
```

例如 `16×16 → (26,3)`、`24×24 → (18,3)`、`32×32 → (10,2)`。

### 4.3 两个角标同时出现

`CollectedBadgePosition()` 靠 `_jobIconId` 判断：非 0 时把绿勾挪到右下。但格子只有 `44×48`，两个 `24×24`（更别说 `32×32`）必然在垂直方向有重叠。实际几乎不会遇到 —— 可解锁物品（乐谱 / 九宫幻卡 / 宠物 / 坐骑）不是武器。真碰到就把绿勾单独缩小或换角。

`JobIconId` setter 末尾会重算 `_collectedIconNode.Position`，所以调整位置时要同时改「初始化器里的 `Position`」和 `CollectedBadgePosition()` 两处。

### 4.4 节点池与状态短路

- `IsCollected` / `IsCollectable` setter 都有 `if (_isCollected == value) return;`，只有值翻转才重绘。
- `ResetForReuse()` 在节点回池时直接复位 `_isCollected` / `_isCollectable` / `_jobIconId` 并隐藏三个角标节点，但**不销毁**节点（`_collectedIconNode` 会复用）。所以改初始化器里的尺寸后，必须重载插件让节点重建，热改不会作用到已存在的池化节点。
- `ClearDecoration()` 只管 IPC 徽章和边框，不要在那里清已收集 / 职业角标。

### 4.5 判定条件

`CollectionStatus.IsCollected()`：`itemId == 0` 直接 false；用 `IDataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId)` 取行，再 `IUnlockState.IsItemUnlockable(item) && IsItemUnlocked(item)`。

- 没有持久化配置，只有 2 秒 TTL 的内存 `Dictionary` 缓存（注释说明是「开窗期间刚使用乐谱」的场景）。
- Dalamud 的 `IsItemUnlocked` 内部先检查 `PlayerState.IsLoaded`，且只对特定 `Item.ItemAction.Action.RowId` 认为「可解锁」；普通装备 / 素材恒为 false。这是设计如此，不是 bug。
- 传入的是原生 `InventoryItem.ItemId`。若以后发现 HQ / 事件物品判定异常，可考虑换成 `item.GetBaseItemId()`（见 `InventoryItemExtensions`）。

## 5. 本次调参记录（结论）

| 项 | 过程 | 最终值 |
| --- | --- | --- |
| 已收集图标 ID | `230419`（gpose 贴纸符号，不是勾，不显示） → `94646`（绿色花朵 / 纹章符号，语义不对） → `71112` | `71112` |
| 已收集贴图适配 | `WrapMode.Stretch`（只画一角） → `FitTexture = true` | `FitTexture = true` |
| 角标尺寸 | `16×16`（偏小） → `40×40`（诊断用，覆盖整格） → `32×32`（偏大） → `24×24` | `24×24` |
| 角标位置 | `(26,3)` → 诊断 `(2,2)` → `(10,2)` → `(18,3)` | `(18,3)`，避让 `(18,21)` |

调试手法记录：想确认某个 icon ID 实际画成什么，把 `Size` 临时放大到 `40×40`、`Position` 移到 `(2,2)`（正好完整落在 `44×48` 内），重载插件看整张贴图，看完再调回去。

## 6. 构建 / 验证 / 发布

```powershell
# PATH 里的 dotnet 没有 SDK，必须用本机 SDK
& 'c:\Users\pm\AppData\Local\Microsoft\dotnet\dotnet.exe' build `
  'c:\Users\pm\Documents\Code\Dalamud\AetherBagsEx\AetherBags\AetherBags.csproj' -c Release --nologo
```

- 构建后需**在游戏内重载 dev 插件**才能看到 UI 改动（节点池复用，热更新 DLL 不够）。
- `Dalamud.NET.Sdk` 会自动在 `bin/Release/AetherBagsEx/` 生成 `latest.zip` 和 `AetherBagsEx.json` manifest，版本号取自 `.csproj` 的 `<Version>`。这个 zip 就是可直接分发的发布包（内含 `AetherBagsEx.dll`、`KamiToolKit.dll`、`Assets/`、manifest、changelog）。旧发布包里的 `SixLabors.ImageSharp.dll` 已不再是依赖（`AetherBagsEx.deps.json` 只剩 `KamiToolKit`），无需补。

发布一个小版本要同步改 **4 处**：

1. `AetherBags/AetherBags.csproj` → `<Version>`。
2. `release/AetherBagsEx/AetherBagsEx.json` → `AssemblyVersion`。
3. `release/Dalamud.json` → AetherBagsEx 条目的 `AssemblyVersion` 和 `LastUpdate`（Unix 秒）。
4. 重新构建后，把 `bin/Release/AetherBagsEx/latest.zip` 覆盖到 `release/AetherBagsEx/latest.zip`。

改完可用反射 / 解压确认 zip 内 manifest 的 `AssemblyVersion` 与新版本一致。
