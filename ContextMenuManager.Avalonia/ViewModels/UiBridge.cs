namespace ContextMenuManager.ViewModels;

/// <summary>
/// 视图层注入的 UI 回调。Avalonia 主窗口启动时挂接真实实现；
/// 对话框与剪贴板由视图层负责，ViewModel 不直接依赖具体 UI 框架的类型。
/// 默认实现为空操作，视图未挂接时后台流程照常运行。
/// </summary>
public static class UiBridge
{
    /// <summary>错误提示框，参数为（标题、正文）。</summary>
    public static Action<string, string> ShowError { get; set; } = static (_, _) => { };

    /// <summary>确认框，参数为（标题、正文），返回 true 表示用户同意。</summary>
    public static Func<string, string, bool> Confirm { get; set; } = static (_, _) => true;

    /// <summary>复制文本到剪贴板（Avalonia 的剪贴板 API 为异步）。</summary>
    public static Func<string, Task> CopyText { get; set; } = static _ => Task.CompletedTask;

    /// <summary>退出当前实例（排队提权重启成功后关闭旧窗口）。</summary>
    public static Action Shutdown { get; set; } = static () => { };
}
