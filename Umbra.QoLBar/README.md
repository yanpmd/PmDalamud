# QoL Bar for Umbra

把 [QoLBarCN](https://github.com/UnknownX7/QoLBar) 的快捷栏功能迁移到 [Umbra](https://github.com/una-xiv/umbra) 插件上。
迁移后，QoL Bar 的每一个「栏」都是一个 Umbra 工具栏部件（widget），可以像其它部件一样被添加到主工具栏或任意辅助工具栏中。

## 功能

- **栏位（Bar）**：可创建任意数量的栏，每栏包含一组快捷方式。
- **快捷方式类型**：命令、分类（子菜单）、间隔。
- **执行模式**：默认（执行全部行）、逐行轮换（Incremental）、随机（Random）。
- **命令**：支持多条命令、`//m<n>` 执行宏、`//i<id>` 使用物品、`//` 进入宏模式。
- **图标**：游戏图标（`名称::图标ID`，参数 `f/n/l/h/g/r`）以及自定义图标（负数 ID，放在插件 `icons` 目录的 PNG）。
- **热键**：栏热键与其内部快捷方式热键，可设置是否透传给游戏。
- **条件集**：登录、角色、目标、武器状态、艾欧泽亚/本地时间、HUD 布局、按键、队伍人数、宠物/陆行鸟、避难所、探索模式、界面存在/可见、插件、职业、职能、区域、条件标志、条件集嵌套，以及若干预设。
  条件不满足时，绑定该栏的部件会自动隐藏。
- **导入/导出**：与 QoLBarCN 剪贴板格式（GZip + Base64）兼容，可互相导入导出。

## 安装

1. 构建：`dotnet build -c Release`（需要 .NET SDK；PATH 里的 `dotnet` 若只有运行时，请使用 `c:\Users\<你>\AppData\Local\Microsoft\dotnet\dotnet.exe`）。
2. 把 `out\Release` 下的**全部文件**（`Umbra.QoLBar.dll`、`Newtonsoft.Json.dll`）复制到 Umbra 插件目录：

   ```
   %APPDATA%\XIVLauncher\installedPlugins\Umbra\dist\plugins\Umbra.QoLBar\
   ```

   实际路径以 Umbra 的配置目录为准；配置窗口「设置」页签底部会显示配置文件位置，其同级 `plugins` 目录即为插件目录。
3. 在 Umbra 设置中启用自定义插件（Custom Plugins），重启后生效。
4. 通过 Umbra 的「添加部件（Add Widget）」窗口，选择 **QoL Bar**，即可把栏添加到任意工具栏。

## 使用

- 点击部件 → 弹出栏内快捷方式菜单，底部有「编辑此栏」按钮。
- 聊天命令 `/qolbar` → 打开配置窗口。
- `/qolbar visible <on|off|toggle> <栏名|序号>` → 显示/隐藏指定栏。
- `/qolbar reload` → 重新读取配置文件。
- 配置窗口三个页签：**栏位**（栏与快捷方式编辑）、**条件集**、**设置**（语言、高清图标、条件缓存等）。

## 配置存储

`{Umbra 配置目录}/QoLBar/config.json`，自定义图标目录为同级的 `QoLBar/icons/{数字}.png`。
JSON 字段与 QoLBarCN 保持一致，可直接把原插件的导出内容导入。

## 代码结构

| 文件 | 说明 |
| --- | --- |
| `QoLBarManager.cs` | `[Service]` 核心服务：配置读写、栏管理、命令执行、热键与窗口调度、`/qolbar` 命令 |
| `Widgets/QoLBarWidget.cs` | `[ToolbarWidget]` 部件，把一个栏渲染成 `MenuPopup` |
| `ConditionManager.cs` / `Conditions.cs` | 条件引擎与全部条件、预设 |
| `GameActions.cs` / `Structures/` | 命令队列、宏、物品、演奏等游戏交互（移植自 QoLBarCN 的 `Game.cs`） |
| `KeybindManager.cs` | 热键轮询与录入 |
| `IconManager.cs` | 游戏/自定义图标加载与名称解析 |
| `Configuration.cs` / `Importing.cs` | 数据模型与导入导出 |
| `UI/` | 配置窗口、快捷方式编辑器、图标浏览器、导入窗口（ImGui） |
| `Localization.cs` / `loc/` | 嵌入式多语言（en、zh-CN） |

## 与 QoLBarCN 的差异

- 栏不再自带窗口/布局（位置、缩放、列数等），外观与位置完全由 Umbra 工具栏管理；相关字段仍保留以便导入兼容。
- 分类以菜单分组（Group）呈现；「逐行轮换 / 随机」模式的分类在点击时直接执行其中一个子项。
- 栏热键执行该栏的第一个可执行快捷方式。
- 自定义图标在菜单中通过节点图片渲染；部件自身的图标仍使用 Umbra 标准的图标配置（游戏图标 / FA / 字形 / 位图）。
- 图标边框、图标动画等 QoLBar 特有绘制效果不再支持。
