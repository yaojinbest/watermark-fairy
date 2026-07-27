using System.Text.Json.Serialization;

namespace WatermarkFairy.Models;

/// <summary>
/// 模板元数据（轻量，列表展示用）
/// </summary>
public sealed record TemplateInfo(
    int Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// 模板完整记录（含 WatermarkConfig JSON）
/// </summary>
public sealed record TemplateRecord(
    int Id,
    string Name,
    [property: JsonInclude] WatermarkConfig Config,
    DateTime CreatedAt,
    DateTime UpdatedAt);