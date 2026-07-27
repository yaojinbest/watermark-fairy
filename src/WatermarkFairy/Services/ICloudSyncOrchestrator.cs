using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 云端同步协调器接口（M3-3）
/// 协调本地 TemplateStore 与云端 ICloudSyncService 的双向同步
///
/// 冲突解决：last-write-wins（PRD v1.0 决议版）
///   - Push: 本地 → 云端，本地覆盖云端
///   - Pull: 云端 → 本地，按 UpdatedAt 比较，云端新则覆盖本地
///
/// 实现：
///   - DefaultCloudSyncOrchestrator（M3-3）：默认实现，使用注入的 ICloudSyncService
///   - 测试可通过 MockCloudSyncService 注入，配合 SQLite in-memory TemplateStore 完成端到端测试
///
/// 协作：
///   - 调用 Attach(store) 订阅 TemplateStore.TemplateChanged，自动 push 云端
///   - 调用 Detach() 解绑
///   - 全量操作（PushAllLocalAsync / PullAllCloudAsync / FullSyncAsync）也可手动触发
/// </summary>
public interface ICloudSyncOrchestrator
{
    /// <summary>当前是否在同步（UI 显示 / 防重入）</summary>
    bool IsSyncing { get; }

    /// <summary>
    /// 绑定到本地 TemplateStore，订阅 TemplateChanged 事件自动 push 云端
    /// 重复 Attach 同一 store 无副作用（先 Detach）
    /// </summary>
    void Attach(TemplateStore store);

    /// <summary>
    /// 解绑当前 store（如果有）
    /// </summary>
    void Detach();

    /// <summary>
    /// 本地全部模板 → 云端 push（按 LocalId 顺序）
    /// 已登录云端才执行；未登录返回 FailedCount > 0 的 SyncBatchResult
    /// </summary>
    Task<SyncBatchResult> PushAllLocalAsync(CancellationToken ct = default);

    /// <summary>
    /// 云端全部模板 → 本地 pull + reconcile（last-write-wins）
    /// 流程：
    ///   1. ListCloudTemplatesAsync 列出云端
    ///   2. DownloadTemplateAsync 每个拉取完整 TemplateRecord
    ///   3. 按 UpdatedAt 比较：云端新 → 本地覆盖；本地新 → 跳过；同时间 → 按 cloud id 优先
    /// </summary>
    Task<SyncBatchResult> PullAllCloudAsync(CancellationToken ct = default);

    /// <summary>
    /// 双向同步（先 PullAllCloudAsync 再 PushAllLocalAsync）
    /// 典型调用场景：登录成功后的自动 sync
    /// </summary>
    Task<SyncBatchResult> FullSyncAsync(CancellationToken ct = default);
}