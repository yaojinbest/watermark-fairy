using System.Reflection;

namespace WatermarkFairy.Services;

/// <summary>
/// Fake 更新服务（M4-1）
/// 默认实现：DI fallback + 离线/无网环境 + 测试基线
/// 总是返回 "无更新"，避免误触发更新流程
/// </summary>
public sealed class FakeUpdateService : IUpdateService
{
    /// <summary>用于测试注入：强制下一次 CheckForUpdate 返回有更新</summary>
    public bool ForceUpdateAvailable { get; set; }

    /// <summary>用于测试注入：模拟下载失败</summary>
    public bool SimulateDownloadFailure { get; set; }

    /// <summary>用于测试注入：模拟网络延迟</summary>
    public int SimulatedDelayMs { get; set; } = 0;

    public bool IsBusy { get; private set; }
    public DateTime? LastCheckTime { get; private set; }
    public string CurrentVersion { get; } = ResolveCurrentVersion();

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            if (SimulatedDelayMs > 0)
                await Task.Delay(SimulatedDelayMs, ct);

            LastCheckTime = DateTime.UtcNow;

            if (ForceUpdateAvailable)
            {
                return new UpdateCheckResult(
                    IsAvailable: true,
                    CurrentVersion: CurrentVersion,
                    LatestVersion: "9.9.9-test",
                    ReleaseNotesUrl: "https://github.com/yaojinbest/watermark-fairy/releases/tag/v9.9.9-test",
                    CheckedAt: LastCheckTime.Value);
            }

            return new UpdateCheckResult(
                IsAvailable: false,
                CurrentVersion: CurrentVersion,
                LatestVersion: null,
                ReleaseNotesUrl: null,
                CheckedAt: LastCheckTime.Value);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<UpdateDownloadResult> DownloadAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            if (SimulatedDelayMs > 0)
            {
                for (int i = 1; i <= 5; i++)
                {
                    await Task.Delay(SimulatedDelayMs / 5, ct);
                    progress?.Report(i / 5.0);
                }
            }
            else
            {
                progress?.Report(1.0);
            }

            if (SimulateDownloadFailure)
            {
                return new UpdateDownloadResult(
                    Success: false,
                    ErrorMessage: "模拟下载失败",
                    DownloadedVersion: null);
            }

            return new UpdateDownloadResult(
                Success: true,
                ErrorMessage: null,
                DownloadedVersion: ForceUpdateAvailable ? "9.9.9-test" : CurrentVersion);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyAndRestart()
    {
        // Fake 实现不真重启；测试可验证不会抛异常
    }

    private static string ResolveCurrentVersion()
    {
        var asm = typeof(IUpdateService).Assembly;
        var v = asm.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}