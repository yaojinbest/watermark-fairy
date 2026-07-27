namespace WatermarkFairy.Models;

/// <summary>
/// 本地模板变更类型（M3-3）
/// 用于 TemplateStore.TemplateChanged 事件，CloudSyncOrchestrator 据此自动 push 云端
/// </summary>
public enum TemplateChangeKind
{
    Added,
    Updated,
    Deleted
}

/// <summary>
/// 本地模板变更事件载荷（M3-3）
/// </summary>
/// <param name="Kind">变更类型（Added/Updated/Deleted）</param>
/// <param name="LocalId">本地模板 id（Deleted 时仍带 id 便于云端清理）</param>
/// <param name="Name">变更时的模板名（Deleted 时为 null）</param>
/// <param name="OccurredAt">UTC 时间戳</param>
public sealed record TemplateChangedEventArgs(
    TemplateChangeKind Kind,
    int LocalId,
    string? Name,
    DateTime OccurredAt);

/// <summary>
/// 批量同步结果（M3-3）
/// 用于 PushAllLocalAsync / PullAllCloudAsync / FullSyncAsync 的聚合返回
/// </summary>
/// <param name="Success">是否完全成功（FailedCount == 0 时为 true）</param>
/// <param name="TotalProcessed">尝试处理的模板总数</param>
/// <param name="SuccessCount">成功的模板数</param>
/// <param name="FailedCount">失败的模板数</param>
/// <param name="Errors">失败条目（模板 id / 云 id + 错误消息）</param>
public sealed record SyncBatchResult(
    bool Success,
    int TotalProcessed,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> Errors)
{
    /// <summary>空结果（无处理项）</summary>
    public static SyncBatchResult Empty { get; } = new(true, 0, 0, 0, Array.Empty<string>());
}