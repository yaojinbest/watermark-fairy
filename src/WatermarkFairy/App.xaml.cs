using System.IO;
using System.Windows;
using System.Windows.Threading;
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
        // v0.2.5 加全局未处理异常捕获（owner build 后 exe 无法启动,需要诊断）
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 加载思源黑体（M1-2.1 patch）
        try
        {
            FontLoader.EnsureLoaded();
        }
        catch (Exception ex)
        {
            // 字体加载失败不阻断启动（fallback 到系统字体）
            ShowFatalError("字体加载失败", ex);
        }

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

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log?.Fatal(ex, "AppDomain 未处理异常（即将崩溃）");
        ShowFatalError("未处理的域异常", ex);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log?.Fatal(e.Exception, "Dispatcher 未处理异常（UI 线程）");
        ShowFatalError("未处理的 UI 异常", e.Exception);
        e.Handled = true;  // 阻止进程崩溃
    }

    private static void ShowFatalError(string title, Exception? ex)
    {
        try
        {
            var msg = $"{title}:\n{ex?.GetType().Name}: {ex?.Message}\n\n{ex?.StackTrace}";
            MessageBox.Show(msg, "Watermark Fairy 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // MessageBox 自己也崩了，无能为力
        }
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
    ///
    /// 现状：v0.1.0 使用 FakeUpdateService（永远返"无更新"，零网络/零依赖）。
    /// 原计划用 Squirrel.Windows 2.0.1 但该包仅 .NET Framework 4.5，
    /// Clowd.Squirrel 已弃用改为 Velopack。真实自动更新调研延后到 M4.5。
    /// </summary>
    private static async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            IUpdateService updateService = new FakeUpdateService();
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