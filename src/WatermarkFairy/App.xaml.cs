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

        // 后台检查更新（M4-1）：不阻塞启动，仅记录日志
        _ = CheckForUpdatesInBackgroundAsync();

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

    /// <summary>
    /// 后台检查更新（M4-1）
    /// fire-and-forget：不弹 UI、不阻塞启动
    /// 有更新时仅记录日志；UI 通知留待 M4-2/M4-3（设置面板的"关于"页）
    /// </summary>
    private static async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            IUpdateService updateService = new SquirrelUpdateService();
            var result = await updateService.CheckForUpdateAsync();
            if (result.IsAvailable)
            {
                Log.Information("发现新版本 v{Latest}（当前 v{Current}）：{Url}",
                    result.LatestVersion, result.CurrentVersion, result.ReleaseNotesUrl);
            }
            else
            {
                Log.Debug("更新检查：当前 v{Current} 已是最新（{Note}）",
                    result.CurrentVersion,
                    string.IsNullOrEmpty(result.ReleaseNotesUrl) ? "ok" : result.ReleaseNotesUrl);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "后台更新检查失败（不影响启动）");
        }
    }
}