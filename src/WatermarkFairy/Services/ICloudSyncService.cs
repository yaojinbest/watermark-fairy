using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 云端同步服务接口（M2）
/// 实现：MockCloudSyncService（测试）/ SupabaseCloudSyncService（生产）
/// </summary>
public interface ICloudSyncService
{
    /// <summary>当前是否已认证</summary>
    bool IsAuthenticated { get; }

    /// <summary>当前登录用户邮箱（未登录时为 null）</summary>
    string? CurrentUserEmail { get; }

    /// <summary>登录</summary>
    Task<CloudAuthResult> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>登出</summary>
    Task LogoutAsync();

    /// <summary>测试连接（验证 URL + key 可用）</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>上传模板到云端</summary>
    Task<CloudUploadResult> UploadTemplateAsync(TemplateRecord record, CancellationToken ct = default);

    /// <summary>从云端下载模板（按 cloud id）</summary>
    Task<CloudDownloadResult> DownloadTemplateAsync(long cloudId, CancellationToken ct = default);

    /// <summary>列出云端所有模板</summary>
    Task<IReadOnlyList<CloudTemplateInfo>> ListCloudTemplatesAsync(CancellationToken ct = default);

    /// <summary>删除云端模板</summary>
    Task<bool> DeleteCloudTemplateAsync(long cloudId, CancellationToken ct = default);
}