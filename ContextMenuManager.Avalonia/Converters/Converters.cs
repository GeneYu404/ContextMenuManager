using System.Globalization;
using Avalonia.Data.Converters;

namespace ContextMenuManager.Converters;

/// <summary>true → false，false → true。Avalonia 用 IsVisible（bool），语义对应 WPF 的 InverseBoolToVisibilityConverter。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>非空（非 null 且非空字符串）→ true。Invert=True 时反过来；对应 WPF 的 NullToVisibilityConverter。</summary>
public sealed class NullToVisibleConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value is string s ? s.Length > 0 : value is not null;
        if (Invert) has = !has;
        return has;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>计数为 0 → true。用于列表空状态提示的 IsVisible。</summary>
public sealed class ZeroToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count == 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// FlashToken 为奇数 → true。行闪光按奇偶翻转 Classes.flash，
/// 交替「样式匹配 / 解除」正好让样式动画每次都重放（等价于 WPF 每次 Token 变化都重放一次）。
/// </summary>
public sealed class FlashOddConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int token && token % 2 != 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
