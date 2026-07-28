using WatermarkFairy.Services;

namespace WatermarkFairy.Services;

/// <summary>
/// 自动更新服务接口（M4-1）
/// 实现：FakeUpdateService（开发/CI 默认）/ SquirrelUpdateService（生产）
/// 文档：docs/SPEC.md §5
/// </summary>
public interface IUpdateService
{
    /// <summary>是否正在检查/下载更新</summary>
    bool IsBusy { get; }

    /// <summary>上次检查时间（未检查过为 null）</summary>
    DateTime? LastCheckTime { get; }

    /// <summary>当前应用版本</summary>
    string CurrentVersion { get; }

    /// <summary>检查更新（不下载）</summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>下载已发现的更新包（不等安装）</summary>
    Task<UpdateDownloadResult> DownloadAsync(IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>应用已下载的更新并重启 app（调用方需先保存工作）</summary>
    void ApplyAndRestart();
}

public sealed record UpdateCheckResult(
    bool IsAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseNotesUrl,
    DateTime CheckedAt);

public sealed record UpdateDownloadResult(
    bool Success,
    string? ErrorMessage,
    string? DownloadedVersion);