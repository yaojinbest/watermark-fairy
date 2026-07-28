using System.Collections.Concurrent;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 云端同步协调器默认实现（M3-3）
/// 协调本地 TemplateStore 与云端 ICloudSyncService 的双向同步
///
/// 自动同步（Attach 后）：
///   - Added/Updated → 自动 push 云端（fire-and-forget，错误吞掉不阻塞 UI）
///   - Deleted → 若有 cloud id 映射则删除云端（fire-and-forget）
///
/// 全量同步（手动触发）：
///   - PushAllLocalAsync / PullAllCloudAsync / FullSyncAsync
///   - 典型场景：登录成功后的 FullSyncAsync（拉取 + 推送 双向 reconcile）
///
/// 冲突解决：last-write-wins（PRD v1.0 决议版）
///   - 云端 UpdatedAt 更新 → 覆盖本地
///   - 本地 UpdatedAt 更新 → 跳过
///   - 同时间 → 云端赢
///
/// 已知限制（M3-3 MVP）：
///   - local_id → cloud_id 映射仅 in-memory，重启清空
///   - 下次 PullAllCloudAsync 按 name 重新 reconcile
///   - 持久化映射（SQLite 列）是 v1.1 任务，不阻塞 M3-3 完工
/// </summary>
public class DefaultCloudSyncOrchestrator : ICloudSyncOrchestrator
{
    private readonly ICloudSyncService _cloud;

    // ConcurrentDictionary 防 OnTemplateChanged 多线程触发 race condition
    private readonly ConcurrentDictionary<int, long> _localToCloudId = new();

    // B1 CI-fix (2026-07-28): PullAllCloudAsync 内部 _store.Add/Update 会触发 TemplateChanged
    // → OnTemplateChanged 自动 push → FullSyncAsync 中 Pull + Push 重复上传 → cloud 数 ≠ 期望
    // 抑制 auto-push 让 Pull 阶段的 Add/Update 不触发 push（push 留给显式 PushAllLocalAsync）
    private bool _suppressAutoPush;

    private TemplateStore? _store;

    public bool IsSyncing { get; private set; }

    public DefaultCloudSyncOrchestrator(ICloudSyncService cloud)
    {
        ArgumentNullException.ThrowIfNull(cloud);
        _cloud = cloud;
    }

    public void Attach(TemplateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Detach();
        _store = store;
        _store.TemplateChanged += OnTemplateChanged;
    }

    public void Detach()
    {
        if (_store != null)
        {
            _store.TemplateChanged -= OnTemplateChanged;
            _store = null;
        }
        _localToCloudId.Clear();
    }

    /// <summary>
    /// 事件处理器：Added/Updated → push；Deleted → 若有映射则删云端
    /// </summary>
    private void OnTemplateChanged(TemplateChangedEventArgs e)
    {
        if (!_cloud.IsAuthenticated) return;

        // B1 CI-fix: PullAllCloudAsync 内部抑制 auto-push，避免 Pull+Push 重复上传
        if (_suppressAutoPush) return;

        switch (e.Kind)
        {
            case TemplateChangeKind.Deleted:
                if (_localToCloudId.TryGetValue(e.LocalId, out var cloudId))
                {
                    _ = SafeDeleteCloudAsync(cloudId);
                    _localToCloudId.TryRemove(e.LocalId, out _);
                }
                break;

            case TemplateChangeKind.Added:
            case TemplateChangeKind.Updated:
                _ = SafePushAsync(e.LocalId);
                break;
        }
    }

    private async Task SafePushAsync(int localId)
    {
        try
        {
            if (_store == null) return;
            var record = _store.Get(localId);
            if (record == null) return;
            IsSyncing = true;
            var result = await _cloud.UploadTemplateAsync(record);
            if (result.Success && result.CloudId.HasValue)
            {
                _localToCloudId[localId] = result.CloudId.Value;
            }
        }
        catch
        {
            // M3-3 简化：fire-and-forget 错误吞掉，不阻塞 UI
            // TODO: 接 Serilog ILogger 记录（M3-4+）
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private async Task SafeDeleteCloudAsync(long cloudId)
    {
        try
        {
            IsSyncing = true;
            await _cloud.DeleteCloudTemplateAsync(cloudId);
        }
        catch
        {
            // 同上
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public async Task<SyncBatchResult> PushAllLocalAsync(CancellationToken ct = default)
    {
        if (_store == null)
            return new SyncBatchResult(false, 0, 0, 1, new[] { "Orchestrator 未 Attach store" });
        if (!_cloud.IsAuthenticated)
            return new SyncBatchResult(false, 0, 0, 1, new[] { "未登录云端" });

        IsSyncing = true;
        var errors = new List<string>();
        var successCount = 0;
        try
        {
            var templates = _store.List();
            foreach (var info in templates)
            {
                ct.ThrowIfCancellationRequested();
                var record = _store.Get(info.Id);
                if (record == null)
                {
                    errors.Add($"local id={info.Id}: 找不到完整记录");
                    continue;
                }

                var result = await _cloud.UploadTemplateAsync(record, ct);
                if (result.Success && result.CloudId.HasValue)
                {
                    _localToCloudId[info.Id] = result.CloudId.Value;
                    successCount++;
                }
                else
                {
                    errors.Add($"local id={info.Id} ({info.Name}): {result.ErrorMessage ?? "未知错误"}");
                }
            }
            return new SyncBatchResult(errors.Count == 0, templates.Count, successCount, errors.Count, errors);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public async Task<SyncBatchResult> PullAllCloudAsync(CancellationToken ct = default)
    {
        if (_store == null)
            return new SyncBatchResult(false, 0, 0, 1, new[] { "Orchestrator 未 Attach store" });
        if (!_cloud.IsAuthenticated)
            return new SyncBatchResult(false, 0, 0, 1, new[] { "未登录云端" });

        IsSyncing = true;
        var errors = new List<string>();
        var successCount = 0;
        var processed = 0;
        try
        {
            var cloudTemplates = await _cloud.ListCloudTemplatesAsync(ct);
            processed = cloudTemplates.Count;

            // B1 CI-fix: 抑制 Pull 阶段 Add/Update 触发的 auto-push，避免 FullSync 中 Pull+Push 重复上传
            _suppressAutoPush = true;
            try
            {
                foreach (var cloudInfo in cloudTemplates)
                {
                    ct.ThrowIfCancellationRequested();
                    var download = await _cloud.DownloadTemplateAsync(cloudInfo.CloudId, ct);
                    if (!download.Success || download.Template == null)
                    {
                        errors.Add($"cloud id={cloudInfo.CloudId} ({cloudInfo.Name}): {download.ErrorMessage ?? "下载失败"}");
                        continue;
                    }

                    var cloudRecord = download.Template;
                    var localByName = _store.GetByName(cloudRecord.Name);

                    bool shouldOverwrite;
                    if (localByName == null)
                    {
                        // 本地不存在 → 拉取
                        shouldOverwrite = true;
                    }
                    else if (cloudInfo.UpdatedAt > localByName.UpdatedAt)
                    {
                        // 云端更新 → 覆盖本地
                        shouldOverwrite = true;
                    }
                    else if (cloudInfo.UpdatedAt < localByName.UpdatedAt)
                    {
                        // 本地更新 → 跳过
                        shouldOverwrite = false;
                    }
                    else
                    {
                        // 同时间 → last-write-wins，云端赢
                        shouldOverwrite = true;
                    }

                    if (shouldOverwrite)
                    {
                        if (localByName == null)
                        {
                            var newId = _store.Add(cloudRecord.Name, cloudRecord.Config);
                            _localToCloudId[newId] = cloudInfo.CloudId;
                        }
                        else
                        {
                            _store.Update(localByName.Id, cloudRecord.Name, cloudRecord.Config);
                            _localToCloudId[localByName.Id] = cloudInfo.CloudId;
                        }
                        successCount++;
                    }
                }
            }
            finally
            {
                _suppressAutoPush = false;
            }
            return new SyncBatchResult(errors.Count == 0, processed, successCount, errors.Count, errors);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public async Task<SyncBatchResult> FullSyncAsync(CancellationToken ct = default)
    {
        // 先 Pull 后 Push：避免本地未推送被云端覆盖
        var pull = await PullAllCloudAsync(ct);
        var push = await PushAllLocalAsync(ct);
        var combinedErrors = pull.Errors.Concat(push.Errors).ToList();
        return new SyncBatchResult(
            pull.Success && push.Success,
            pull.TotalProcessed + push.TotalProcessed,
            pull.SuccessCount + push.SuccessCount,
            pull.FailedCount + push.FailedCount,
            combinedErrors);
    }
}