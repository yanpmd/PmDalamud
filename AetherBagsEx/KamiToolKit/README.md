# KamiToolKit

C# 库，让 Dalamud 插件能够用 C# 构建 **FFXIV 的原生 UI**。

它把游戏的 ATK 节点树暴露给托管代码，使插件可以在游戏自己的界面里插入原生节点 ——
而不是画一层与游戏割裂的 ImGui 覆盖层。由此得到的界面在字体、贴图、主题色、
动画、多语言、手柄导航与音效上与游戏原生界面完全一致。

---

## 目录

- [它解决什么问题](#它解决什么问题)
- [快速开始](#快速开始)
- [核心概念](#核心概念)
- [功能一览](#功能一览)
- [生命周期与线程约束](#生命周期与线程约束)
- [调试](#调试)
- [目录结构](#目录结构)
- [本仓库中的副本](#本仓库中的副本)

---

## 它解决什么问题

Dalamud 生态里，插件界面通常用 ImGui 实现。它方便，但也有固有局限：

- 无法使用游戏自带的字体、贴图与主题色
- 不参与游戏的焦点、手柄导航与音效系统
- 缩放、分辨率与 UI 缩放设置需要自行适配
- 视觉上与游戏界面明显割裂

KamiToolKit 走的是另一条路：**在游戏真正的 UI 里插入原生节点**。
为此它解决了三个核心难题。

### 1. 虚表劫持

游戏原生节点通过虚表（vtable）分发 `Destroy`、`OnSetup`、`ReceiveEvent` 等回调。
KTK 复制节点的虚表到 UI 内存，把这些槽位替换为托管委托，
这样原生代码回调进来时就能回到 C#，反之托管代码也能安全地覆写原生行为。

### 2. UI 内存分配

节点不是用托管堆上的内存拼装的，而是直接用游戏的 **UI 内存池**分配。
这意味着它们真正混进了原生节点树，被游戏的布局、渲染、焦点与输入系统一视同仁。

### 3. 原生链表维护

游戏的子节点是**反向**兄弟链表。KTK 手工实现了链接维护，
让托管代码能像操作普通树一样增删、移动与重排子节点。

---

## 快速开始

### 初始化

```csharp
using Dalamud.Plugin;
using KamiToolKit;

public class MyPlugin : IDalamudPlugin {
    public MyPlugin(IDalamudPluginInterface pluginInterface) {
        // 初始化是异步的：它需要读取窗口位置配置，并在框架线程上安装原生回调。
        _ = InitializeAsync(pluginInterface);
    }

    private async Task InitializeAsync(IDalamudPluginInterface pluginInterface) {
        await KamiToolKitLibrary.InitializeAsync(pluginInterface, "MyPlugin");

        var window = new MyWindow {
            InternalName = "MyPluginWindow",
            Title = "My Plugin",
            Size = new Vector2(400, 300),
        };

        window.Open();
    }
}
```

### 建立一个窗口

```csharp
using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

public sealed class MyWindow : NativeAddon {

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValues) {
        var list = new VerticalListNode {
            Position = ContentStartPosition,
            Size = ContentSize,
            ItemSpacing = 4f,
        };

        list.AddNode(new TextNode { String = "Hello" });

        new TextButtonNode {
            Size = new Vector2(120, 24),
            String = "Close",
            OnClick = () => Close(),
        }.AttachNode(this);

        list.AttachNode(this);

        base.OnSetup(addon, atkValues);
    }
}
```

`NativeAddon` 提供了窗口的全部生命周期能力：
`Open` / `OpenAsync` / `Close` / `CloseAsync` / `Toggle`、
`ContentStartPosition` 与 `ContentSize`（内容区，已扣除标题栏与内边距）、
`SetWindowSize` / `SetWindowPosition`，
以及 `RememberClosePosition`（记忆窗口位置，持久化到 `addon.json`）。

---

## 核心概念

### 节点树

所有 UI 元素都是节点。节点分三层：

| 层级 | 说明 | 代表类 |
| --- | --- | --- |
| `ResNode` | 最基础的资源节点，只负责绘制与尺寸 | `ResNode`、`ImageNode`、`TextNode` |
| `ComponentNode<T, TU>` | 组件节点，带有可交互的原生组件 | `IconNode`、`CheckboxNode`、`SliderNode` |
| `SimpleComponentNode` | 简化组件基类，省去手写组件样板 | `TextButtonNode`、`TextInputNode` |

节点用 `AttachNode` 挂载，支持挂到窗口、任意节点，甚至原生指针：

```csharp
node.AttachNode(this);                      // 挂到窗口
node.AttachNode(parentNode);                // 挂到另一个节点
node.AttachNode(parentNode, NodePosition.AsFirstChild);
node.DetachNode();
```

> 注意：挂到 `ComponentNode` 时会被重定向到其 `UldManager.RootNode`，
> 而不是组件本身。

### 部件（Parts）

部件是挂载到既有节点上的附加组件，用来复用原生 UI 的常见组合：

```csharp
// IconExtras 是一组图标附加件的集合
iconNode.IconExtras.AntsNode.IsVisible = true;                      // 蚂蚁线（关系高亮）
iconNode.IconExtras.QuantityTextNode.String = "99";                 // 数量文本
iconNode.IconExtras.CooldownNode.CooldownImage.IsVisible = true;    // 冷却遮罩

// 也可以给图像类节点直接加贴图部件
imageNode.AddPart(new Part { TexturePath = "ui/uld/..." });
```

可用的部件包括 `IconExtras`、`AntsNode`、`CooldownNode`、`AlternateCooldownNode`、
`CursorNode`、`IconIndicator`、`SelectableNode`、`ListNavNode`、`TextInputButton`、
`HoldButtonProgressNode`、滚动条与滑块的背景 / 前景按钮等。

### 时间轴（Timeline）

KTK 封装了游戏原生的 `AtkTimeline`，可用 `TimelineBuilder` 以流式 API 构建关键帧，
再挂到任意节点上：

```csharp
var builder = new TimelineBuilder();

builder.BeginFrameSet(0, 30)
    .AddLabelPair(0, 30, labelId: 1)   // 定义一个可被播放的标签
    .AddFrame(0, alpha: 0, scale: new Vector2(0.9f, 0.9f))
    .AddFrame(30, alpha: 255, scale: new Vector2(1f, 1f))
    .EndFrameSet();

var timeline = builder.Build();
node.AddTimeline(timeline);

// 播放 / 停止
timeline.PlayAnimation(1);
timeline.StopAnimation();
```

关键帧可以设置的位置、透明度、颜色、旋转、缩放、文本颜色与贴图部件，
分别对应 `KeyFrameBuilder` 上的 `Position` / `Alpha` / `AddColor` / `MultiplyColor` /
`RotationDegrees` / `Scale` / `TextColor` / `TextOutlineColor` / `Part`。

原生动画会随游戏的渲染循环一起推进，因此与 UI 帧率、缩放完全同步。

---

## 功能一览

### 节点体系

- **图元** —— `ImageNode`、`TextNode`、`LabelTextNode`、`CategoryTextNode`、`CounterNode`、
  `NineGridNode`、`BorderNineGridNode`、`HorizontalLineNode`、`VerticalLineNode`、
  `ImGuiImageNode`、`TimelineNode`、`ClippingMaskNode`、`SelectableTextNode`。
- **颜色** —— `ColorImageNode`、`ColorSquareNode`、`ColorRingWithSquareNode`、
  `ColorEditNode`、`ColorPickerNode`、`ColorPreviewWithInputNode`、`AlphaBarNode`、
  `AlphaImageNode`、`ColorGradientSquare`。
- **图标** —— `IconNode`（游戏图标资源）、`IconImageNode`、`TextureImageNode`、
  `TextureButtonNode`、`IconButtonNode`、`ImGuiIconButtonNode`。
- **列表项** —— `ListItemNode`、`ListItemWithFocusNav`、`TreeListItemNode`。

### 布局

| 类 | 用途 |
| --- | --- |
| `VerticalListNode` / `HorizontalListNode` | 基础纵向 / 横向列表 |
| `TabbedVerticalListNode` | 支持多级缩进（Tab）的纵向列表 |
| `AlignedVerticalListNode` / `AlignedHorizontalListNode` | 带对齐语义的列表 |
| `GridNode` | 固定行列网格 |
| `ListBoxNode` | 四角锚定、方向可选的列表容器 |
| `HorizontalFlexNode` | Flex 布局 |
| `LayoutListNode` | 布局列表基类，统一提供 `ItemSpacing` / `FirstItemSpacing` 与增删查 |
| `ListNode<T, TU>` | 带**节点池虚拟化**的列表，只渲染可视区域 |
| `TreeListNode<T, TU>` | 树形分组列表，支持折叠与空态占位 |
| `NestableTreeListNode<T, TU>` | 可嵌套的分组树 |
| `CollapsingHeaderNode` | 原生折叠分组头，带展开 / 折叠动画 |

### 滚动

`ScrollingNode<T>` 以纯 `ResNode` 作为裁剪节点 —— 这一点很关键：
裁剪边界不再是组件节点，拖放管理器便不会把它误判为放置目标。
内置滚动条、横向模式、`ScrollToStart` / `ScrollToEnd`，
并在尺寸变化时自动重排。

### 组件

`ButtonBase`、`TextButtonNode`、`CircleButtonNode`、`IconButtonNode`、`CheckboxNode`、
`SliderNode` / `FloatSliderNode`、`NumericInputNode`、`TextInputNode`、`SearchInputNode`、
`DropDownNode<T>` / `EnumDropDownNode<T>` / `StringDropDownNode`、`MultiStateButtonNode`、
`HoldButtonNode`、`DragDropNode`、`ScrollBarNode`、`ProgressBarNode`、
`RadioButtonGroupNode`、`TabBarNode` / `TabBarRadioButtonNode`、`HotbarNode`、
`NavFocusNode`、`WindowNode`。

### 地图叠加

`MapOverlayController` 可在世界地图之上叠加自定义标记
（`MapMarkerNode`、`FlagMarkerNode`、`MapMarkerInfo`），
支持跟随缩放偏移、PvP 隐藏、Ctrl 隐藏、tooltip 与点击事件。

### HUD 叠加

`UiOverlay/OverlayController` 支持四个叠加层级
（Background / Behind / Above / Foreground），可在角色 HUD 上叠加常驻面板。

### 控制器

| 控制器 | 作用 |
| --- | --- |
| `AddonController<T>` | 接管某个既有原生 addon 的生命周期 |
| `DynamicAddonController` | 同上，但支持动态增删 |
| `AddonFactoryController` | 完全替换原生 addon 的内容 |
| `NativeListController<T, TU>` | 就地改造游戏原生列表（增删改元素、导出元素数据） |

### 右键菜单

`ContextMenu` 封装游戏原生右键菜单（`AgentContext`），支持多级子菜单、图标与启用状态。

### 其它

- `Classes` —— `ColorHelper`、`GridSize`、`Part` / `PartsList`、`DragDropPayload`、
  `MapMarkerInfo`、`TabBarEntry`、`TreeListSection`、`AddonConfigFile`、
  `ViewportEventListener`、`InventoryItemTooltip` 等。
- `Extensions` —— 贴图加载（`AtkUldPartExtensions.LoadTexture`，支持游戏主题化路径）、
  `ByteColor` 与 `ReadOnlySeString` 的辅助扩展等。
- `Enums` —— `CircleButtonIcon`、`CostTextColor`、`CounterFont`、`DrawFlags`、
  `FlexFlags`、`LayoutAnchor`、`LayoutOrientation`、`NodePosition`、`OverlayLayer`、
  `TextInputFlags`、`VerticalListAnchor`、`VerticalListAlignment`、`WrapMode` 等 17 个枚举。

---

## 生命周期与线程约束

### 初始化与释放

```csharp
await KamiToolKitLibrary.InitializeAsync(pluginInterface, "MyPlugin");
// ...
await KamiToolKitLibrary.DisposeAsync();
```

`InitializeAsync` 会读取窗口位置配置（`addon.json`，并自动迁移旧格式的分文件配置）、
在框架线程上安装原生回调，并注册调试助手。

> **重要**：`IFramework.Run` 在非框架线程上会把任务排到下一帧才执行。
> 因此插件构造函数里**不能** `await` 初始化 —— 那会死锁。
> 正确做法是像上文示例那样，把初始化放到异步任务里。

`Dispose` 必须**在主线程**调用，否则 KTK 会直接跳过全部清理
（原生回调的 hook 不会被卸载）。若插件释放时不在主线程，
请把它派发回框架线程。

### 线程约束

节点的创建、挂载、释放等操作**必须在主线程**执行。
KTK 在关键位置调用 `ThreadSafety.AssertMainThread()` 来尽早暴露违规。

事件回调通常来自游戏主线程，但某些 Dalamud 事件的线程语义可能变化，
建议统一用 `Framework.RunOnTick(...)` 切回主线程后再操作节点。

---

## 调试

DEBUG 构建下，KTK 会注册一个命令 `/ktkdebug_<插件名>`，打开一个 ImGui 窗口，
列出所有存活的节点与 addon，用于排查泄漏与意外的原生分配。

---

---

## 目录结构

```
KamiToolKit/
├── BaseTypes/
│   ├── NodeBase/              # 节点基类：属性、链接、事件、释放、编辑、提示、时间轴
│   ├── ComponentNode/         # 组件基类与虚表
│   └── NativeAddon/           # 原生窗口：生命周期、配置、关闭回调、虚表
├── Nodes/                     # 节点实现（33 个）
│   ├── ComponentNode/         # 交互组件（31 个）
│   ├── Layout/                # 布局容器（13 个）
│   ├── Simplified/            # 简化基类（5 个）
│   └── Parts/                 # 部件（17 个）
├── Classes/                   # 辅助类型（20 个）
├── Controllers/               # addon / 原生列表控制器（4 个）
├── Addons/                    # 预制 addon（ColorPickerAddon）
├── MapOverlay/                # 地图叠加（3 个）
├── UiOverlay/                 # HUD 叠加（2 个）
├── Timelines/                 # 动画时间轴（13 个）
├── ContextMenu/               # 原生右键菜单（3 个）
├── Enums/                     # 枚举（17 个）
├── Interfaces/                # 接口（5 个）
├── Extensions/                # 扩展方法（15 个）
├── Internal/                  # 内部实现（链表维护、服务、工具）
├── Debug/                     # 调试窗口与节点 / addon 列表页
├── Experimental.cs            # 实验性开关
└── KamiToolKitLibrary.cs      # 入口：初始化与释放
```

---

## 本仓库中的副本

本目录是 [MidoriKami/KamiToolKit](https://github.com/MidoriKami/KamiToolKit) 的副本，
当前基于上游提交 `b4b7cab`，并带有两处本地改动：

| 文件 | 改动 |
| --- | --- |
| `KamiToolKitLibrary.cs` | 增加同步的 `Initialize` 入口。插件构造函数不在框架线程上，此时 `await InitializeAsync` 会因 `IFramework.Run` 排到下一帧而死锁；而该流程实际只需同步读取配置并安装 hook，可以安全同步执行。 |
| `Nodes/ComponentNode/IconNode.cs` | 增加 `IsGlamoured` 属性，用于把「已染色」状态暴露给节点使用者。 |

上游在 `b4b7cab` 之后重构了命名空间（`Premade.Node.*` → `Nodes` / `Nodes.Simplified`，
基类移入 `BaseTypes`），并删除、变更了大量类型。
升级前请先确认使用方（例如 AetherBagsEx 的 `Compat/` 兼容层）是否仍然匹配。

---

## 致谢

- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) —— 游戏原生结构体定义
- [Dalamud](https://github.com/goatcorp/Dalamud) —— 插件宿主

