using Avalonia.Threading;

namespace ContextMenuManager.Views;

/// <summary>
/// 在同步回调里等待模态对话框结果。
/// Avalonia 的 ShowDialog 是异步的，而 MainViewModel 的对话框回调是同步签名 ——
/// 这里开一个嵌套 Dispatcher 帧（等价于 WPF ShowDialog 的阻塞消息泵）泵到任务完成。
/// </summary>
internal static class SyncDialog
{
    public static TResult Wait<TResult>(Task<TResult> dialogTask)
    {
        if (dialogTask.IsCompleted) return dialogTask.GetAwaiter().GetResult();

        var frame = new DispatcherFrame();
        _ = dialogTask.ContinueWith(
            _ => Dispatcher.UIThread.Post(() => frame.Continue = false),
            TaskContinuationOptions.ExecuteSynchronously);
        Dispatcher.UIThread.PushFrame(frame);
        return dialogTask.GetAwaiter().GetResult();
    }
}
