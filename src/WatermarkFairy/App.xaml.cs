using System.IO;
using System.Windows;
using Serilog;
using Serilog.Core;
using WatermarkFairy.Converters;
using WatermarkFairy.Services;

namespace WatermarkFairy;

/// <summary>
/// App.xaml 交互逻辑
/// </summary>
public partial class App : Application
{
    public static ILogger Log { get; private set; } = null!;
    public static string FontSource => FontLoader.LoadedFrom ?? "none";

    protected override void OnStartup(StartupEventArgs e)
    {
        // 加载思源黑体（M1-2.1 patch）
        FontLoader.EnsureLoaded();

        // 初始化日志
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WatermarkFairy", "logs");
        Directory.CreateDirectory(logDir);

        Log = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        Log.Information("Watermark Fairy 启动 v{Version}",
            typeof(App).Assembly.GetName().Version);

        // 注册全局转换器
        RegisterConverters();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Watermark Fairy 退出");
        (Log as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void RegisterConverters()
    {
        // 转换器在 App.xaml 里注册（M1-7 加）
        // 这里只放注释：实际 converter 在 App.xaml <Application.Resources> 里
    }
}