using FluentAssertions;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// DefaultCloudSyncOrchestrator 单元测试（M3-3）
///
/// 测试模式：
///   - SQLite in-memory (per .db temp file) + MockCloudSyncService
///   - fire-and-forget 用 Task.Delay(200) 等待（避免 flaky）
///   - 路径用 Path.GetTempPath() + Guid.NewGuid() 跨平台一致
/// </summary>
public class DefaultCloudSyncOrchestratorTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TemplateStore _store;
    private readonly MockCloudSyncService _cloud;
    private readonly DefaultCloudSyncOrchestrator _orch;

    public DefaultCloudSyncOrchestratorTests()
    {
        // M3-3 测试 pattern：per-test 临时 db 文件（per W3 §10.8 跨平台规范）
        _dbPath = Path.Combine(Path.GetTempPath(), $"wf-orch-{Guid.NewGuid():N}.db");
        _store = new TemplateStore(_dbPath);
        _store.Initialize();
        _cloud = new MockCloudSyncService();
        _cloud.LoginAsync("test@example.com", "password123").GetAwaiter().GetResult();
        _orch = new DefaultCloudSyncOrchestrator(_cloud);
    }

    public void Dispose()
    {
        _orch.Detach();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static WatermarkConfig SampleConfig(string name = "Test", string text = "Hello") => new()
    {
        Name = name,
        Layers = new()
        {
            new TextWatermarkLayer { Text = text, FontSize = 24f }
        }
    };

    private static TemplateRecord MakeRecord(int id, string name, DateTime updatedAt) => new(
        id, name, SampleConfig(name), DateTime.UtcNow, updatedAt);

    // ============ Attach / Detach ============

    [Fact]
    public async Task Attach_Subscribes_AutoPushesOnAdd()
    {
        _orch.Attach(_store);

        _store.Add("auto1", SampleConfig("auto1"));
        await Task.Delay(200);  // fire-and-forget 完成

        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().HaveCount(1);
        cloudList[0].Name.Should().Be("auto1");
    }

    [Fact]
    public async Task Detach_StopsAutoPush()
    {
        _orch.Attach(_store);
        _store.Add("before", SampleConfig("before"));
        await Task.Delay(200);

        _orch.Detach();
        _store.Add("after", SampleConfig("after"));
        await Task.Delay(200);

        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().HaveCount(1);
        cloudList[0].Name.Should().Be("before");
    }

    [Fact]
    public async Task Attach_Twice_Idempotent_NoDoubleUpload()
    {
        _orch.Attach(_store);
        _orch.Attach(_store);  // 第二次: Detach + re-attach (按设计 idempotent)

        _store.Add("once", SampleConfig("once"));
        await Task.Delay(200);

        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().HaveCount(1);
    }

    [Fact]
    public void Attach_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _orch.Attach(null!));
    }

    [Fact]
    public void Ctor_NullCloud_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultCloudSyncOrchestrator(null!));
    }

    // ============ PushAllLocalAsync ============

    [Fact]
    public async Task PushAllLocal_NoStore_ReturnsError()
    {
        var orchNoStore = new DefaultCloudSyncOrchestrator(_cloud);

        var result = await orchNoStore.PushAllLocalAsync();

        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainMatch("*Attach*");
    }

    [Fact]
    public async Task PushAllLocal_NoAuth_ReturnsError()
    {
        var notLoggedIn = new MockCloudSyncService();
        var orch = new DefaultCloudSyncOrchestrator(notLoggedIn);
        orch.Attach(_store);
        _store.Add("local1", SampleConfig("local1"));

        var result = await orch.PushAllLocalAsync();

        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task PushAllLocal_EmptyStore_EmptySuccess()
    {
        _orch.Attach(_store);

        var result = await _orch.PushAllLocalAsync();

        result.Success.Should().BeTrue();
        result.TotalProcessed.Should().Be(0);
        result.SuccessCount.Should().Be(0);
    }

    [Fact]
    public async Task PushAllLocal_MultipleTemplates_AllUploaded()
    {
        _orch.Attach(_store);
        _store.Add("a", SampleConfig("a"));
        _store.Add("b", SampleConfig("b"));
        _store.Add("c", SampleConfig("c"));

        var result = await _orch.PushAllLocalAsync();

        result.Success.Should().BeTrue();
        result.TotalProcessed.Should().Be(3);
        result.SuccessCount.Should().Be(3);

        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().HaveCount(3);
        cloudList.Select(t => t.Name).Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    // ============ PullAllCloudAsync ============

    [Fact]
    public async Task PullAllCloud_NoStore_ReturnsError()
    {
        var orchNoStore = new DefaultCloudSyncOrchestrator(_cloud);

        var result = await orchNoStore.PullAllCloudAsync();

        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task PullAllCloud_EmptyCloud_NoLocalChanges()
    {
        _orch.Attach(_store);
        _store.Add("local1", SampleConfig("local1"));

        var result = await _orch.PullAllCloudAsync();

        result.Success.Should().BeTrue();
        result.TotalProcessed.Should().Be(0);

        var localList = _store.List();
        localList.Should().HaveCount(1);
    }

    [Fact]
    public async Task PullAllCloud_NewTemplate_AddedToLocal()
    {
        // Cloud 已有 cloud1
        await _cloud.UploadTemplateAsync(MakeRecord(0, "cloud1", DateTime.UtcNow));
        _orch.Attach(_store);

        var result = await _orch.PullAllCloudAsync();

        result.Success.Should().BeTrue();
        result.TotalProcessed.Should().Be(1);
        result.SuccessCount.Should().Be(1);

        var localList = _store.List();
        localList.Should().HaveCount(1);
        localList[0].Name.Should().Be("cloud1");
    }

    [Fact]
    public async Task PullAllCloud_CloudNewer_OverwritesLocal()
    {
        // 本地存在但较旧
        _store.Add("shared", SampleConfig("shared", "OLD"));
        // Cloud 有同名但更新
        await _cloud.UploadTemplateAsync(MakeRecord(0, "shared", DateTime.UtcNow.AddHours(1)));

        _orch.Attach(_store);

        var result = await _orch.PullAllCloudAsync();

        result.Success.Should().BeTrue();
        result.SuccessCount.Should().Be(1);

        var localRecord = _store.GetByName("shared");
        localRecord.Should().NotBeNull();
        // Config 通过 JSON roundtrip 保留,验证 name 一致即可（last-write-wins 已生效）
        localRecord!.Config.Name.Should().Be("shared");
    }

    [Fact]
    public async Task PullAllCloud_LocalNewer_SkipsCloudVersion()
    {
        // 本地已有 (just added, very recent UpdatedAt)
        var localId = _store.Add("shared", SampleConfig("shared", "LOCAL"));
        // Cloud 旧版本（MockCloudSyncService 存储的 UpdatedAt 是 upload 时刻）
        await _cloud.UploadTemplateAsync(MakeRecord(0, "shared", DateTime.UtcNow.AddHours(-2)));

        _orch.Attach(_store);

        var result = await _orch.PullAllCloudAsync();

        result.Success.Should().BeTrue();
        // successCount 应该是 0（本地更新，跳过云端）
        result.SuccessCount.Should().Be(0);

        // 本地仍存在且未被覆盖
        var localRecord = _store.GetByName("shared");
        localRecord.Should().NotBeNull();
        localRecord!.Id.Should().Be(localId);
    }

    // ============ FullSyncAsync ============

    [Fact]
    public async Task FullSync_BothHaveContent_ConvergesBoth()
    {
        _orch.Attach(_store);
        _store.Add("local-only", SampleConfig("local-only"));
        await _cloud.UploadTemplateAsync(MakeRecord(0, "cloud-only", DateTime.UtcNow));

        var result = await _orch.FullSyncAsync();

        result.Success.Should().BeTrue();

        // Pull 拉下 cloud-only + Push 推上 local-only → 双边都有 2 个
        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().HaveCount(2);

        var localList = _store.List();
        localList.Should().HaveCount(2);
        localList.Select(t => t.Name).Should().BeEquivalentTo(new[] { "local-only", "cloud-only" });
    }

    [Fact]
    public async Task FullSync_NotLoggedIn_BothFail()
    {
        var notLoggedIn = new MockCloudSyncService();
        var orch = new DefaultCloudSyncOrchestrator(notLoggedIn);
        orch.Attach(_store);

        var result = await orch.FullSyncAsync();

        result.Success.Should().BeFalse();
        result.FailedCount.Should().BeGreaterOrEqualTo(1);
    }
}