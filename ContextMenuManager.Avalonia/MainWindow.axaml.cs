using Avalonia.Controls;
using Avalonia.Input;
using ContextMenuManager.Models;
using ContextMenuManager.Services;
using ContextMenuManager.ViewModels;
using ContextMenuManager.Views;

namespace ContextMenuManager.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // WPF 版挂在 Loaded：首屏加载完成后初始化；提权实例接着重放待执行队列
        Opened += async (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            await vm.InitializeAsync();
            if (App.PendingResume && Elevation.IsElevated)
                await vm.ResumePendingAsync();
        };
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    // ------------------------------------------------------------------ 对话框回调

    internal AddVerbRequest? ShowAddDialog(SceneDefinition scene)
    {
        var dialog = new AddItemDialog(scene);
        return SyncDialog.Wait(dialog.ShowDialog<bool>(this)) ? dialog.Result : null;
    }

    internal EditVerbRequest? ShowEditDialog(MenuEntry entry)
    {
        var dialog = new AddItemDialog(entry);
        return SyncDialog.Wait(dialog.ShowDialog<bool>(this)) ? dialog.EditResult : null;
    }

    // ------------------------------------------------------------------ 快捷键（WPF 的 InputBindings / ApplicationCommands.Find）

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            Vm?.RefreshCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Vm?.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (Vm?.IsScenePage == true)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    // ------------------------------------------------------------------ 列表事件

    /// <summary>行点击设置 SelectedEntry（等价于 WPF ListBox 的选中行为；选中高亮由 Fx.SelectedEntry 类翻转）。</summary>
    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is { } vm && sender is Control { DataContext: MenuEntryViewModel entry })
            vm.SelectedEntry = entry;
    }

    /// <summary>容器就绪时按序号错峰淡入（WPF 用 AlternationIndex，Avalonia 的 ContainerPrepared 直接带 Index）。</summary>
    private void OnEntryContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        Fx.Reveal(e.Container, e.Index);
    }
}
