using FluentAssertions;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using WatermarkFairy.ViewModels;
using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// MainViewModel + Orchestrator 端到端集成测试（M3-3）
///
/// 测试链路：MainViewModel.LoginAsync → Orchestrator.Attach → FullSyncAsync
/// 验证：登录后双向 sync 工作正常，登出后停止自动 push
///
/// 测试模式：
///   - 真实 TemplateStore (per-test 临时 SQLite 文件)
///   - MockCloudSyncService (无需 owner 凭证)
///   - MainViewModel DI 注入三件套
/// </summary>
public class MainViewModelOrchestratorIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TemplateStore _store;
    private readonly MockCloudSyncService _cloud;
    private readonly MainViewModel _vm;

    public MainViewModelOrchestratorIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"wf-e2e-{Guid.NewGuid():N}.db");
        _store = new TemplateStore(_dbPath);
        _store.Initialize();
        _cloud = new MockCloudSyncService();
        _vm = new MainViewModel(new ImageProcessor(), _store, _cloud);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static WatermarkConfig SampleConfig(string name) => new()
    {
        Name = name,
        Layers = new()
        {
            new TextWatermarkLayer { Text = name, FontSize = 24f }
        }
    };

    private static TemplateRecord MakeRecord(int id, string name, DateTime updatedAt) => new(
        id, name, SampleConfig(name), DateTime.UtcNow, updatedAt);

    // ============ LoginAsync → Orchestrator.Attach + FullSyncAsync ============

    [Fact]
    public async Task LoginAsync_PullsExistingCloudTemplateToLocal()
    {
        // 准备：cloud 已有 cloud-only (登录前 MockCloudService.IsAuthenticated = false)
        await _cloud.LoginAsync("cloud@example.com", "password123");
        await _cloud.UploadTemplateAsync(MakeRecord(0, "cloud-only", DateTime.UtcNow));
        await _cloud.LogoutAsync();
        _cloud.IsAuthenticated.Should().BeFalse();

        // VM 登录 → Orchestrator.Attach + FullSync
        var result = await _vm.LoginAsync("vm@example.com", "password123");
        result.Success.Should().BeTrue();

        // 验证：local 应有 cloud-only
        var localList = _store.List();
        localList.Should().Contain(t => t.Name == "cloud-only");
    }

    [Fact]
    public async Task LoginAsync_PushesExistingLocalTemplatesToCloud()
    {
        // 准备：local 已有 local-only
        _store.Add("local-only", SampleConfig("local-only"));

        // VM 登录 → Orchestrator.Attach + FullSync
        await _vm.LoginAsync("vm@example.com", "password123");

        // 验证：cloud 应有 local-only
        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().Contain(t => t.Name == "local-only");
    }

    [Fact]
    public async Task LoginAsync_BothHaveContent_ConvergesBoth()
    {
        // 准备：cloud 有 cloud-only (登录前准备)
        await _cloud.LoginAsync("cloud@example.com", "password123");
        await _cloud.UploadTemplateAsync(MakeRecord(0, "cloud-only", DateTime.UtcNow));
        await _cloud.LogoutAsync();

        // local 有 local-only
        _store.Add("local-only", SampleConfig("local-only"));

        // VM 登录 → FullSync
        await _vm.LoginAsync("vm@example.com", "password123");

        // 验证：双边都有两个
        var localList = _store.List();
        localList.Select(t => t.Name).Should().BeEquivalentTo(new[] { "local-only", "cloud-only" });

        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Select(t => t.Name).Should().BeEquivalentTo(new[] { "local-only", "cloud-only" });
    }

    // ============ LogoutAsync → Orchestrator.Detach ============

    [Fact]
    public async Task LogoutAsync_StopsAutoPushToCloud()
    {
        // VM 登录 + 等待 initial sync 完成
        await _vm.LoginAsync("vm@example.com", "password123");
        await Task.Delay(200);

        // VM 登出
        await _vm.LogoutAsync();
        _vm.IsCloudAuthenticated.Should().BeFalse();

        // 登出后 store 变更不应触发 cloud upload
        _store.Add("after-logout", SampleConfig("after-logout"));
        await Task.Delay(200);

        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().NotContain(t => t.Name == "after-logout");
    }

    // ============ 登录后 store 变更 → 自动 push ============

    [Fact]
    public async Task AddLocalTemplate_AfterLogin_AutoPushedToCloud()
    {
        await _vm.LoginAsync("vm@example.com", "password123");
        await Task.Delay(200);

        _store.Add("auto-pushed", SampleConfig("auto-pushed"));
        await Task.Delay(200);

        var cloudList = await _cloud.ListCloudTemplatesAsync();
        cloudList.Should().Contain(t => t.Name == "auto-pushed");
    }

    [Fact]
    public async Task UpdateLocalTemplate_AfterLogin_CloudHasUpdatedVersion()
    {
        await _vm.LoginAsync("vm@example.com", "password123");
        await Task.Delay(200);

        _store.Add("to-update", SampleConfig("to-update"));
        await Task.Delay(200);

        // 更新本地
        var localRecord = _store.GetByName("to-update");
        _store.Update(localRecord!.Id, "to-update", SampleConfig("to-update-UPDATED"));
        await Task.Delay(200);

        // Cloud 应有 to-update
        var cloudList = await _cloud.ListCloudTemplatesAsync();
        var cloudItem = cloudList.FirstOrDefault(t => t.Name == "to-update");
        cloudItem.Should().NotBeNull();

        // 下载 cloud 版本,确认是 updated
        var download = await _cloud.DownloadTemplateAsync(cloudItem!.CloudId);
        download.Success.Should().BeTrue();
        download.Template!.Config.Layers.First().Text.Should().Be("to-update-UPDATED");
    }
}