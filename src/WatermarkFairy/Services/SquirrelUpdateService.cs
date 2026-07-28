using Squirrel;

namespace WatermarkFairy.Services;

/// <summary>
/// Squirrel.Windows 自动更新实现（M4-1）
/// 数据源：GitHub Releases（https://github.com/yaojinbest/watermark-fairy/releases）
/// 文档：docs/SPEC.md §5
/// 依赖：Squirrel.Windows 2.0.1（csproj 已声明）
/// </summary>
public sealed class SquirrelUpdateService : IUpdateService
{
    private const string GithubRepoUrl = "https://github.com/yaojinbest/watermark-fairy";

    public bool IsBusy { get; private set; }
    public DateTime? LastCheckTime { get; private set; }
    public string CurrentVersion { get; }

    public SquirrelUpdateService()
    {
        CurrentVersion = ResolveCurrentVersion();
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            using var mgr = await UpdateManager.GitHubUpdateManager(GithubRepoUrl, prerelease: false, ct);
            var info = await mgr.CheckForUpdate().WaitAsync(ct);
            LastCheckTime = DateTime.UtcNow;

            if (info is null || info.FutureReleaseEntry is null)
            {
                return new UpdateCheckResult(
                    IsAvailable: false,
                    CurrentVersion: CurrentVersion,
                    LatestVersion: null,
                    ReleaseNotesUrl: null,
                    CheckedAt: LastCheckTime.Value);
            }

            var latestVersion = info.FutureReleaseEntry.Version.ToString();
            var releaseNotesUrl = info.FutureReleaseEntry.GetReleaseNotesUrl(GithubRepoUrl)?.ToString();

            return new UpdateCheckResult(
                IsAvailable: true,
                CurrentVersion: CurrentVersion,
                LatestVersion: latestVersion,
                ReleaseNotesUrl: releaseNotesUrl,
                CheckedAt: LastCheckTime.Value);
        }
        catch (Exception ex)
        {
            // 网络/DNS/GitHub API 错误等不致命：返回 IsAvailable=false 让 UI 静默
            return new UpdateCheckResult(
                IsAvailable: false,
                CurrentVersion: CurrentVersion,
                LatestVersion: null,
                ReleaseNotesUrl: $"更新检查失败：{ex.GetType().Name}：{ex.Message}",
                CheckedAt: DateTime.UtcNow);
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
            using var mgr = await UpdateManager.GitHubUpdateManager(GithubRepoUrl, prerelease: false, ct);
            var info = await mgr.CheckForUpdate().WaitAsync(ct);

            if (info is null || info.FutureReleaseEntry is null)
            {
                return new UpdateDownloadResult(
                    Success: false,
                    ErrorMessage: "没有可用更新",
                    DownloadedVersion: null);
            }

            // Squirrel 不直接报进度；这里占位 report(0/1)
            progress?.Report(0.0);
            await mgr.DownloadReleases(info.ReleasesToApply).WaitAsync(ct);
            progress?.Report(1.0);

            return new UpdateDownloadResult(
                Success: true,
                ErrorMessage: null,
                DownloadedVersion: info.FutureReleaseEntry.Version.ToString());
        }
        catch (Exception ex)
        {
            return new UpdateDownloadResult(
                Success: false,
                ErrorMessage: $"下载失败：{ex.GetType().Name}：{ex.Message}",
                DownloadedVersion: null);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyAndRestart()
    {
        UpdateManager.RestartApp();
    }

    private static string ResolveCurrentVersion()
    {
        var asm = typeof(IUpdateService).Assembly;
        var v = asm.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}