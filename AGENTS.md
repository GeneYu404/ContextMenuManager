# AGENTS.md

## 项目概述

右键菜单管家是 Windows 10 / 11 的右键菜单管理工具。程序直接读写注册表，开关即时生效；写入前抓取现场快照，程序重启后仍可逐条撤销。

仓库当前维护两个 UI 工程：

- `ContextMenuManager`：WPF 版，功能完整，是行为基准。
- `ContextMenuManager.Avalonia`：Avalonia 12 迁移版，共享层与视图层均已迁移；界面行为待对照 WPF 版逐项验证。

`README.md` 目前描述的是 WPF 版，迁移完成后需同步更新。

## 仓库结构

```
ContextMenuManager/
├── ContextMenuManager/            WPF 版工程
│   ├── Models/                    场景、菜单项模型
│   ├── Services/                  扫描、写入、快照、备份、提权、系统优化
│   ├── ViewModels/                MVVM 基础设施与主视图模型
│   ├── Views/ Converters/         WPF 视图与转换器（行为基准）
│   └── MainWindow.xaml App.xaml
├── ContextMenuManager.Avalonia/   Avalonia 12 迁移版工程
│   ├── Services/IconLoader.cs     分叉文件：GetDIBits 解码图标
│   ├── ViewModels/                分叉与新增文件，见「迁移状态」
│   ├── Views/                     AddItemDialog、MsgBox、Fx、SyncDialog
│   ├── Converters/                IsVisible 语义的转换器
│   ├── App.axaml MainWindow.axaml 完整资源样式与主界面
│   └── app.manifest               与 WPF 版相同的 asInvoker 清单
├── ContextMenuManager.slnx        解决方案，包含两个工程
├── README.md
└── AGENTS.md
```

## 构建与运行

```powershell
# 构建整个解决方案（两个工程都必须通过）
dotnet build ContextMenuManager.slnx

# 运行 WPF 版
dotnet run --project ContextMenuManager

# 运行 Avalonia 迁移版
dotnet run --project ContextMenuManager.Avalonia
```

发布方式与 README 相同：`dotnet publish ContextMenuManager -c Release` 输出单文件 exe。

每次改动后的最低验证要求：整个解决方案编译 0 警告、0 错误；涉及界面时启动 Avalonia 工程做冒烟检查，窗口应正常打开且不闪退。

## 迁移状态

### 共享文件（链接编译）

以下文件只有一份源码，通过 `Compile Include` 同时编入两个工程。修改会同时影响两边，改完必须重新构建整个解决方案：

- `Models/` 全部
- `Services/` 全部（`IconLoader.cs` 除外）
- `ViewModels/LogStore.cs`

### 分叉文件（各有一份）

| 文件 | Avalonia 侧的差异 |
| --- | --- |
| `ViewModels/Infrastructure.cs` | 属性变化时触发 `CommandManager.InvalidateRequerySuggested`；Avalonia 侧为同名 shim，替代 WPF 的自动重查 |
| `ViewModels/MainViewModel.cs` | `ICollectionView` 换成 `EntryView`；对话框、剪贴板、退出换到 `UiBridge` |
| `ViewModels/ItemViewModels.cs` | `ImageSource` 换成 Avalonia `Bitmap`；Dispatcher 换成 `Dispatcher.UIThread` |
| `Services/IconLoader.cs` | WPF Imaging 换成 `GetIconInfo` + `GetDIBits` + `WriteableBitmap` |

Avalonia 侧新增文件：`ViewModels/EntryView.cs`（过滤、排序、分组）、`ViewModels/UiBridge.cs`（视图层回调）、`ViewModels/CommandManager.cs`（命令重查 shim）。

### 视图层（Avalonia 版已完成）

| 文件 | 内容 |
| --- | --- |
| `Converters/Converters.cs` | InverseBool / NullToVisible / ZeroToBool / FlashOdd，产出 bool 供 IsVisible 使用 |
| `Views/Fx.cs` | 行选中类翻转（`Fx.SelectedEntry`）、Reveal 错峰淡入、Slide / Toast（与 WPF 版同签名预留） |
| `Views/SyncDialog.cs` | 嵌套 Dispatcher 帧，把异步 ShowDialog 收敛成同步语义 |
| `Views/MsgBox.axaml(.cs)` | 自写模态消息框，供 UiBridge 的 ShowError / Confirm 使用 |
| `Views/AddItemDialog.axaml(.cs)` | 完整表单 + 实时 .reg 预览 + 复制 / 另存（文件对话框与剪贴板走异步 API） |
| `MainWindow.axaml(.cs)` | 完整主界面：导航、搜索筛选、分组列表、详情、优化页、日志页、状态栏、快捷键 |
| `App.axaml(.cs)` | 资源与样式（卡片 / 开关 / 强调按钮 / 行闪光动画）、MainViewModel 构造、UiBridge 四钩子接线 |

`UiBridge` 四个钩子（`ShowError` / `Confirm` / `CopyText` / `Shutdown`）已在 `App.axaml.cs` 挂接；`EntryView.Groups` 已由 MainWindow 的 ItemsControl 消费（未分组时为单个匿名组，组头按组名为空隐藏）。

### 遗留事项

- 界面行为尚未与 WPF 版逐项对照：分组渲染、行闪光、错峰淡入、选中高亮的视觉效果需人工过一遍。
- 条目列表未虚拟化（ItemsControl 全量实例化），文件类型场景上千条时可能偏慢；必要时可换 TreeDataGrid 或引入虚拟化容器。
- 强调色跟随依赖 Fluent 主题的 `SystemAccentColor` 资源；主题未提供时退回 Win11 默认蓝（`AccentBrush`）。

## 编码约定

- 行为基准是 WPF 版。修 bug 时先在 WPF 版定位并修复；只改到共享文件时两边同时生效，改到分叉文件时必须同步两侧并分别构建验证。
- 分叉文件中，类型映射与桥接调用属于 Avalonia 侧的合理差异；业务逻辑改动不允许只落一侧。
- 注释与文档使用中文，遵循仓库现有风格：全角标点，中文与英文、数字之间加半角空格。
- 依赖约束：除 Avalonia 家族包外，不引入新的第三方 NuGet 包。
- 提权语义不可破坏：程序默认以 `asInvoker` 运行，仅写入 HKLM 时写队列落盘、UAC 提权重启续跑。改动 `Elevation`、`PendingOps` 相关逻辑后，需实际走一遍提权流程。
- 注册表读写必须区分 64 位与 32 位视图，并把结果写回原视图；不允许绕过 `KeySnapshot` 现场快照直接写入。
