namespace WatermarkFairy.Models;

/// <summary>
/// 云端模板元数据（M2）
/// </summary>
public sealed record CloudTemplateInfo(
    long CloudId,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// 登录结果（M2）
/// </summary>
public sealed record CloudAuthResult(
    bool Success,
    string? UserEmail = null,
    string? ErrorMessage = null);

/// <summary>
/// 上传结果（M2）
/// </summary>
public sealed record CloudUploadResult(
    bool Success,
    long? CloudId = null,
    string? ErrorMessage = null);

/// <summary>
/// 下载结果（M2）
/// </summary>
public sealed record CloudDownloadResult(
    bool Success,
    TemplateRecord? Template = null,
    string? ErrorMessage = null);