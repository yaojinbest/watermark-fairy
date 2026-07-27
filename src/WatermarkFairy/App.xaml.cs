using System.IO;
using System.Windows;
using Serilog;
using Serilog.Core;

namespace WatermarkFairy;

/// <summary>
/// App.xaml 交互逻辑
/// </summary>
public partial class App : Application
{
    public static ILogger Log { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
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

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Watermark Fairy 退出");
        (Log as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
