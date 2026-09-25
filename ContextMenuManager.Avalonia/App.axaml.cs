using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using ContextMenuManager.ViewModels;
using ContextMenuManager.Views;

namespace ContextMenuManager.Avalonia;

public partial class App : Application
{
    /// <summary>带 --resume-from-elevation 启动：首屏加载完成后重放待执行队列（提权续跑）。</summary>
    public static bool PendingResume { get; private set; }

    private MainWindow? _window;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            PendingResume = (desktop.Args ?? []).Any(a =>
                a.StartsWith("--resume-from-elevation", StringComparison.OrdinalIgnoreCase));

            _window = new MainWindow();

            // 构造主视图模型：新建 / 编辑对话框回调（同步语义由 SyncDialog 的嵌套帧保证）
            var window = _window;
            var vm = new MainViewModel(
                scene => window.ShowAddDialog(scene),
                entry => window.ShowEditDialog(entry));
            window.DataContext = vm;
            desktop.MainWindow = window;

            // UiBridge 四个钩子：错误框 / 确认框 / 剪贴板 / 退出
            UiBridge.ShowError = (title, message) => MsgBox.ShowModal(window, title, message);
            UiBridge.Confirm = (title, message) => MsgBox.ShowModal(window, title, message, confirm: true);
            UiBridge.CopyText = text => window.Clipboard is { } clipboard
                ? clipboard.SetTextAsync(text)
                : Task.CompletedTask;
            UiBridge.Shutdown = () => desktop.Shutdown();

            // 强调色：Fluent 主题提供系统强调色时跟随，缺失则保留默认 Win11 蓝
            if (ResourceNodeExtensions.TryFindResource(this, "SystemAccentColor", out var accent)
                && accent is Color color
                && Resources.TryGetValue("AccentBrush", out var brushResource)
                && brushResource is SolidColorBrush brush)
            {
                brush.Color = color;
            }
        }

        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        base.OnFrameworkInitializationCompleted();
    }

    private void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 等价于 WPF 版的 DispatcherUnhandledException 处理
        if (_window is not null)
        {
            MsgBox.ShowModal(_window, "右键菜单管家",
                $"发生未处理的错误：\n\n{e.Exception.Message}\n\n程序会继续运行，建议刷新当前列表。");
        }
        e.Handled = true;
    }
}
