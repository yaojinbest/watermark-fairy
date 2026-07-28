using System.Collections.Concurrent;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// Mock 云端同步服务（M2.1）
/// 用于 CI 测试 + 离线模式
/// 内存存储 + 简单计数 + 模拟网络延迟
/// </summary>
public class MockCloudSyncService : ICloudSyncService
{
    private readonly ConcurrentDictionary<long, (TemplateRecord Record, DateTime CreatedAt, DateTime UpdatedAt)> _store = new();
    private long _nextId = 1;

    public bool IsAuthenticated { get; private set; }
    public string? CurrentUserEmail { get; private set; }

    /// <summary>模拟网络延迟（毫秒），0 = 不延迟</summary>
    public int SimulatedDelayMs { get; set; } = 0;

    /// <summary>模拟上传失败（CI 测错误路径用）</summary>
    public bool SimulateUploadFailure { get; set; }

    public Task<CloudAuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult(new CloudAuthResult(false, ErrorMessage: "邮箱不能为空"));

        if (string.IsNullOrWhiteSpace(password))
            return Task.FromResult(new CloudAuthResult(false, ErrorMessage: "密码不能为空"));

        if (password.Length < 6)
            return Task.FromResult(new CloudAuthResult(false, ErrorMessage: "密码至少 6 位"));

        IsAuthenticated = true;
        CurrentUserEmail = email;
        return Task.FromResult(new CloudAuthResult(true, email));
    }

    public Task LogoutAsync()
    {
        IsAuthenticated = false;
        CurrentUserEmail = null;
        return Task.CompletedTask;
    }

    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public async Task<CloudUploadResult> UploadTemplateAsync(TemplateRecord record, CancellationToken ct = default)
    {
        if (SimulatedDelayMs > 0) await Task.Delay(SimulatedDelayMs, ct);
        if (!IsAuthenticated)
            return new CloudUploadResult(false, ErrorMessage: "未登录");
        if (SimulateUploadFailure)
            return new CloudUploadResult(false, ErrorMessage: "模拟失败");

        var id = Interlocked.Increment(ref _nextId);
        // B1 CI-fix: 用 record 自身的 CreatedAt/UpdatedAt，不用 now
        // （之前用 now 覆盖导致 PullAllCloud_LocalNewer_SkipsCloudVersion 失败：
        //  测试上传 UpdatedAt=-2h，mock 存为 now，与 local 更新时间比较反了 → shouldOverwrite=true → successCount 应为 0 实为 1）
        _store[id] = (record, record.CreatedAt, record.UpdatedAt);
        return new CloudUploadResult(true, id);
    }

    public async Task<CloudDownloadResult> DownloadTemplateAsync(long cloudId, CancellationToken ct = default)
    {
        if (SimulatedDelayMs > 0) await Task.Delay(SimulatedDelayMs, ct);
        if (!IsAuthenticated)
            return new CloudDownloadResult(false, ErrorMessage: "未登录");
        if (!_store.TryGetValue(cloudId, out var entry))
            return new CloudDownloadResult(false, ErrorMessage: $"模板 {cloudId} 不存在");

        return new CloudDownloadResult(true, entry.Record);
    }

    public async Task<IReadOnlyList<CloudTemplateInfo>> ListCloudTemplatesAsync(CancellationToken ct = default)
    {
        if (SimulatedDelayMs > 0) await Task.Delay(SimulatedDelayMs, ct);
        if (!IsAuthenticated)
            return Array.Empty<CloudTemplateInfo>();

        return _store
            .Select(kvp => new CloudTemplateInfo(kvp.Key, kvp.Value.Record.Name, kvp.Value.CreatedAt, kvp.Value.UpdatedAt))
            .OrderByDescending(t => t.UpdatedAt)
            .ToList();
    }

    public async Task<bool> DeleteCloudTemplateAsync(long cloudId, CancellationToken ct = default)
    {
        if (SimulatedDelayMs > 0) await Task.Delay(SimulatedDelayMs, ct);
        if (!IsAuthenticated) return false;
        return _store.TryRemove(cloudId, out _);
    }
}