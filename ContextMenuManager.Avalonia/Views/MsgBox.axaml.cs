using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ContextMenuManager.Views;

/// <summary>
/// 自写的简单模态消息框（Avalonia 没有内置 MessageBox），供 UiBridge 使用。
/// confirm=false 为「确定」单按钮；confirm=true 为「是 / 否」。
/// 关闭语义与 WPF MessageBox 一致：确定 / 是 → true，否 / Esc / 点 X → false。
/// </summary>
public partial class MsgBox : Window
{
    private bool _resultSet;

    private MsgBox(string title, string message, bool confirm)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;

        if (confirm)
        {
            PrimaryButton.Content = "是";
            SecondaryButton.Content = "否";
        }
        else
        {
            SecondaryButton.IsVisible = false;
        }

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CloseWith(true);
            else if (e.Key == Key.Escape) CloseWith(false);
        };

        // 点 X 关闭等价于取消
        Closing += (_, e) =>
        {
            if (_resultSet) return;
            e.Cancel = true;
            _resultSet = true;
            Dispatcher.UIThread.Post(() => Close(false));
        };
    }

    private void OnPrimary(object? sender, RoutedEventArgs e) => CloseWith(true);

    private void OnSecondary(object? sender, RoutedEventArgs e) => CloseWith(false);

    private void CloseWith(bool result)
    {
        _resultSet = true;
        Close(result);
    }

    /// <summary>模态显示并同步等待结果（语义与 WPF MessageBox.Show 一致）。</summary>
    public static bool ShowModal(Window owner, string title, string message, bool confirm = false)
    {
        var box = new MsgBox(title, message, confirm);
        return SyncDialog.Wait(box.ShowDialog<bool>(owner));
    }
}
