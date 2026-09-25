using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using ContextMenuManager.ViewModels;

namespace ContextMenuManager.Views;

/// <summary>
/// 自 WPF 版 Fx.cs 移植的动画工具集：
/// 行闪光 —— 样式动画由 Classes.flash 驱动（见 App.axaml），此处只负责选中高亮的类翻转；
/// Reveal —— 列表载入时按序号错峰淡入上移（等价于 WPF 的 Fx.Reveal + AlternationIndex）；
/// SlideIn / SlideOut / ToastIn —— 抽屉滑入滑出与 Toast 抬起，与 WPF 版签名一致（WPF 版同样未接线，保留备用）。
/// </summary>
public static class Fx
{
    static Fx()
    {
        SelectedEntryProperty.Changed.AddClassHandler<Control>(OnSelectedEntryChanged);
    }

    // ---------------- 选中高亮 ----------------

    /// <summary>
    /// 把主窗口的 SelectedEntry 绑到每一行（views:Fx.SelectedEntry）。
    /// 值变化或行 DataContext 变化（虚拟化复用）时翻转 "selected" 样式类。
    /// </summary>
    public static readonly AttachedProperty<object?> SelectedEntryProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("SelectedEntry", typeof(Fx));

    // XAML 附加属性语法按 WPF 习惯查找 Get/Set 访问器
    public static object? GetSelectedEntry(Control row) => row.GetValue(SelectedEntryProperty);

    public static void SetSelectedEntry(Control row, object? value) => row.SetValue(SelectedEntryProperty, value);

    private static void OnSelectedEntryChanged(Control row, AvaloniaPropertyChangedEventArgs e)
    {
        SyncSelected(row);
        //DataContext 可能被模板复用换掉：补一次监听，重复挂接前先摘除保证幂等
        row.DataContextChanged -= OnRowDataContextChanged;
        row.DataContextChanged += OnRowDataContextChanged;
    }

    private static void OnRowDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control row) SyncSelected(row);
    }

    private static void SyncSelected(Control row)
    {
        var selected = row.DataContext is MenuEntryViewModel vm
            && ReferenceEquals(vm, row.GetValue(SelectedEntryProperty));

        const string key = "selected";
        if (selected)
        {
            if (!row.Classes.Contains(key)) row.Classes.Add(key);
        }
        else
        {
            row.Classes.Remove(key);
        }
    }

    // ---------------- Reveal（错峰淡入） ----------------

    /// <summary>载入时按序号淡入上移：delay = min(index, 12) × 26ms，时长与缓动与 WPF 版一致。</summary>
    public static void Reveal(Control container, int index)
    {
        if (index < 0) return;
        var delay = TimeSpan.FromMilliseconds(Math.Min(index, 12) * 26);

        container.Opacity = 0;
        var shift = container.RenderTransform as TranslateTransform ?? new TranslateTransform();
        container.RenderTransform = shift;
        shift.Y = 9;

        var fade = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(240),
            Delay = delay,
            FillMode = FillMode.Forward,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, 1d) } },
            },
        };
        _ = fade.RunAsync(container);

        var move = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(320),
            Delay = delay,
            FillMode = FillMode.Forward,
            Easing = new BackEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.YProperty, 9d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.YProperty, 0d) } },
            },
        };
        _ = move.RunAsync(shift);
    }

    // ---------------- 抽屉滑入滑出 ----------------

    public static void SlideIn(Control target, double fromX)
    {
        var shift = target.RenderTransform as TranslateTransform ?? new TranslateTransform();
        target.RenderTransform = shift;
        shift.X = fromX;
        target.Opacity = 0;

        _ = Fade(0d, 1d, TimeSpan.FromMilliseconds(160)).RunAsync(target);
        _ = Move(TranslateTransform.XProperty, fromX, 0d,
            TimeSpan.FromMilliseconds(300), new CubicEaseOut()).RunAsync(shift);
    }

    public static async void SlideOut(Control target, double toX, Action? done = null)
    {
        if (target.RenderTransform is not TranslateTransform shift)
        {
            target.IsVisible = false;
            done?.Invoke();
            return;
        }

        var fade = Fade(1d, 0d, TimeSpan.FromMilliseconds(160));
        var move = Move(TranslateTransform.XProperty, shift.X, toX,
            TimeSpan.FromMilliseconds(200), new CubicEaseIn());
        await Task.WhenAll(fade.RunAsync(target), move.RunAsync(shift));

        target.IsVisible = false;
        done?.Invoke();
    }

    /// <summary>Toast 从底部抬起。</summary>
    public static void ToastIn(Control target)
    {
        var shift = target.RenderTransform as TranslateTransform ?? new TranslateTransform();
        target.RenderTransform = shift;
        shift.Y = 22;
        target.Opacity = 0;

        _ = Fade(0d, 1d, TimeSpan.FromMilliseconds(180)).RunAsync(target);
        _ = Move(TranslateTransform.YProperty, 22d, 0d,
            TimeSpan.FromMilliseconds(300), new BackEaseOut()).RunAsync(shift);
    }

    // ---------------- 动画工厂 ----------------

    private static Animation Fade(double from, double to, TimeSpan duration) => new()
    {
        Duration = duration,
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, from) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, to) } },
        },
    };

    private static Animation Move(AvaloniaProperty<double> property, double from, double to,
        TimeSpan duration, Easing easing) => new()
    {
        Duration = duration,
        FillMode = FillMode.Forward,
        Easing = easing,
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(property, from) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(property, to) } },
        },
    };
}
