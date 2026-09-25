# AGENTS.md

## 项目概述

右键菜单管家是 Windows 10 / 11 的右键菜单管理工具。程序直接读写注册表，开关即时生效；写入前抓取现场快照，程序重启后仍可逐条撤销。

当前实现基于 Avalonia 12（`net10.0-windows`）。原 WPF 版是迁移时的行为基准，完整保留在 git 历史的首个提交中（`git show c9554a0`）；对比两版实现或排查行为差异时以原版为准。

`README.md` 与当前实现同步；改动功能或目录结构后需同步更新。

## 仓库结构

```
ContextMenuManager/
├── ContextMenuManager.Avalonia/   主工程（解决方案内唯一项目）
│   ├── Models/                    场景、菜单项模型
│   ├── Services/                  扫描、写入、快照、备份、提权、系统优化、图标提取
│   ├── ViewModels/                MVVM 基础设施、主视图模型、EntryView、UiBridge
│   ├── Views/                     主窗口、新建/编辑对话框、消息框、动画、同步对话框辅助
│   ├── Converters/                IsVisible 语义的布尔转换器
│   ├── MainWindow.axaml App.axaml 主界面与应用启动接线
│   └── app.manifest               asInvoker 清单
├── ContextMenuManager.slnx        解决方案
├── README.md
└── AGENTS.md
```

## 构建与运行

```powershell
# 构建
dotnet build ContextMenuManager.slnx

# 运行
dotnet run --project ContextMenuManager.Avalonia
```

发布：`dotnet publish ContextMenuManager.Avalonia -c Release` 输出单文件 `ContextMenuManager.exe`。

每次改动后的最低验证要求：编译 0 警告、0 错误；涉及界面时启动工程做冒烟检查，窗口应正常打开且不闪退。

## 迁移历史与实现差异

迁移分两步完成（git 历史可查）：首个提交为双工程并存的迁移完成态，第二个提交移除 WPF 版并重组工程。移植时的关键适配点如下，修改这些文件时留意对应语义：

| 文件 | 相对 WPF 原版的差异 |
| --- | --- |
| `ViewModels/Infrastructure.cs` | 属性变化时触发 `CommandManager.InvalidateRequerySuggested`（Avalonia 侧同名 shim，替代 WPF 的自动重查） |
| `ViewModels/MainViewModel.cs` | `ICollectionView` 换成 `EntryView`；对话框、剪贴板、退出走 `UiBridge` |
| `ViewModels/ItemViewModels.cs` | `ImageSource` 换成 Avalonia `Bitmap`；Dispatcher 换成 `Dispatcher.UIThread` |
| `Services/IconLoader.cs` | WPF Imaging 换成 `GetIconInfo` + `GetDIBits` + `WriteableBitmap` |
| `ViewModels/EntryView.cs` | 新增：WPF `ICollectionView` 的最小替代（过滤、多级排序、分组、Reset 通知） |
| `ViewModels/UiBridge.cs` | 新增：视图层注入的 `ShowError` / `Confirm` / `CopyText` / `Shutdown` 四个钩子 |
| `ViewModels/CommandManager.cs` | 新增：命令重查 shim，替代 WPF `CommandManager.RequerySuggested` |
| `Views/SyncDialog.cs` | 新增：嵌套 Dispatcher 帧，把异步 `ShowDialog` 收敛成同步语义 |
| `Converters/Converters.cs` | 新增：Avalonia `IsVisible` 语义的布尔转换器（无 `Visibility` 类型） |
| `Views/Fx.cs` | 新增：行选中类翻转（`Fx.SelectedEntry`）、`Reveal` 错峰淡入；Slide / Toast 同签名预留 |

视图层文件（`MainWindow.axaml(.cs)`、`AddItemDialog.axaml(.cs)`、`MsgBox.axaml(.cs)`、`App.axaml(.cs)`）为 Avalonia 全量实现，功能与 WPF 版对照迁移，取舍见 git 历史中迁移提交的说明。

`UiBridge` 四个钩子必须保持挂接（`App.axaml.cs`）；漏接 `Shutdown` 会导致提权续跑时旧窗口不退出。`EntryView.Groups` 由 MainWindow 的 ItemsControl 消费（未分组时为单个匿名组，组头按组名为空隐藏）。

### 遗留事项

- 界面行为尚未与 WPF 版逐项对照：分组渲染、行闪光、错峰淡入、选中高亮的视觉效果需人工过一遍。
- 条目列表未虚拟化（ItemsControl 全量实例化），文件类型场景上千条时可能偏慢；必要时可换 TreeDataGrid 或引入虚拟化容器。
- 强调色跟随依赖 Fluent 主题的 `SystemAccentColor` 资源；主题未提供时退回 Win11 默认蓝（`AccentBrush`）。
- `SyncDialog` 的模态往返（打开新建 / 编辑 / 消息框后关闭）未经自动测试，需人工点验。
- 提权续跑（`--resume-from-elevation`）未实测；改动 `Elevation`、`PendingOps` 后必须走一遍 HKLM 写入流程。

## 编码约定

- 行为基准是 git 历史中的 WPF 版（首个提交）；行为有疑问时对照原版实现，与原版不一致的改动需在注释中说明原因。
- 注释与文档使用中文，遵循仓库现有风格：全角标点，中文与英文、数字之间加半角空格。
- 依赖约束：除 Avalonia 家族包外，不引入新的第三方 NuGet 包。
- 提权语义不可破坏：程序默认以 `asInvoker` 运行，仅写入 HKLM 时写队列落盘、UAC 提权重启续跑。改动 `Elevation`、`PendingOps` 相关逻辑后，需实际走一遍提权流程。
- 注册表读写必须区分 64 位与 32 位视图，并把结果写回原视图；不允许绕过 `KeySnapshot` 现场快照直接写入。
