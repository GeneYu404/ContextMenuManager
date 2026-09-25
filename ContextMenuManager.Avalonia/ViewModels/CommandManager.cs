namespace ContextMenuManager.ViewModels;

/// <summary>
/// WPF CommandManager 的最小替代。
/// WPF 会在输入事件后自动重查命令的 CanExecute；Avalonia 没有对应机制，
/// 改由 ObservableObject.OnPropertyChanged 统一触发 —— 属性一变即刷新按钮状态。
/// </summary>
public static class CommandManager
{
    public static event EventHandler? RequerySuggested;

    public static void InvalidateRequerySuggested() => RequerySuggested?.Invoke(null, EventArgs.Empty);
}
