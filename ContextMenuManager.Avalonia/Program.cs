using Avalonia;

namespace ContextMenuManager.Avalonia;

internal static class Program
{
    // Avalonia 配置、不使用 Avalonia.Xaml；必须在 Main 中构建 AppBuilder
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
