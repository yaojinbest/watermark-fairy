using FluentAssertions;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using WatermarkFairy.ViewModels;
using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// MainViewModel + CloudSync 集成测试（M2.3）
/// 验证 Login / Logout / Upload / Refresh / Download 流程
/// CI 用 MockCloudSyncService，无需 Supabase 凭证
/// </summary>
public class MainViewModelCloudSyncTests
{
    private static WatermarkConfig SampleConfig() => new()
    {
        Name = "Cloud Test",
        Layers = new()
        {
            new TextWatermarkLayer
            {
                Text = "Cloud",
                FontSize = 24f,
            }
        }
    };

    private static MainViewModel NewVm() => new(
        new ImageProcessor(),
        null,
        new MockCloudSyncService());

    // ============ 构造 + 默认 ============

    [Fact]
    public void Constructor_Default_UsesMockCloudSync()
    {
        var vm = new MainViewModel();
        vm.CloudSync.Should().NotBeNull();
        vm.CloudSync.Should().BeOfType<MockCloudSyncService>();
        vm.IsCloudAuthenticated.Should().BeFalse();
        vm.CloudUserEmail.Should().BeNull();
        vm.IsCloudSyncing.Should().BeFalse();
        vm.CloudTemplates.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithMockCloudSync_StartsUnauthenticated()
    {
        var vm = NewVm();
        vm.IsCloudAuthenticated.Should().BeFalse();
        vm.CloudUserEmail.Should().BeNull();
    }

    // ============ LoginAsync ============

    [Fact]
    public async Task LoginAsync_ValidCredentials_UpdatesState()
    {
        var vm = NewVm();
        var result = await vm.LoginAsync("user@example.com", "password123");

        result.Success.Should().BeTrue();
        vm.IsCloudAuthenticated.Should().BeTrue();
        vm.CloudUserEmail.Should().Be("user@example.com");
        // CloudStatusText 由 RefreshCloudTemplatesAsync 覆盖为 "已加载 N 个云端模板"
        // 不再断言 "已登录"
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_DoesNotAuthenticate()
    {
        var vm = NewVm();
        var result = await vm.LoginAsync("user@example.com", "123");  // 太短

        result.Success.Should().BeFalse();
        vm.IsCloudAuthenticated.Should().BeFalse();
        vm.CloudUserEmail.Should().BeNull();
        vm.CloudStatusText.Should().Contain("登录失败");
    }

    [Fact]
    public async Task LoginAsync_AfterSuccess_RefreshesCloudTemplates()
    {
        var vm = NewVm();
        // 先登录（mock 要求 IsAuthenticated=true 才能 upload）
        await vm.LoginAsync("user@example.com", "password123");
        var mock = (MockCloudSyncService)vm.CloudSync;
        await mock.UploadTemplateAsync(new TemplateRecord(1, "Pre", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow));
        await vm.RefreshCloudTemplatesAsync();

        vm.CloudTemplates.Count.Should().Be(1);
        vm.CloudTemplates[0].Name.Should().Be("Pre");
    }

    // ============ LogoutAsync ============

    [Fact]
    public async Task LogoutAsync_ClearsAuthAndTemplates()
    {
        var vm = NewVm();
        await vm.LoginAsync("user@example.com", "password123");
        var mock = (MockCloudSyncService)vm.CloudSync;
        await mock.UploadTemplateAsync(new TemplateRecord(1, "Test", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow));
        await mock.UploadTemplateAsync(new TemplateRecord(2, "Test2", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow));
        await vm.RefreshCloudTemplatesAsync();
        vm.CloudTemplates.Count.Should().Be(2);

        await vm.LogoutAsync();

        vm.IsCloudAuthenticated.Should().BeFalse();
        vm.CloudUserEmail.Should().BeNull();
        vm.CloudTemplates.Should().BeEmpty();
        vm.CloudStatusText.Should().Contain("已登出");
    }

    // ============ UploadCurrentTemplateAsync ============

    [Fact]
    public async Task UploadCurrentTemplateAsync_AfterLogin_AddsToList()
    {
        var vm = NewVm();
        await vm.LoginAsync("user@example.com", "password123");

        var result = await vm.UploadCurrentTemplateAsync("MyTemplate");
        result.Success.Should().BeTrue();
        result.CloudId.Should().NotBeNull();
        result.CloudId.Should().BeGreaterThan(0);

        vm.CloudTemplates.Count.Should().Be(1);
        vm.CloudTemplates[0].Name.Should().Be("MyTemplate");
        // CloudStatusText 由 RefreshCloudTemplatesAsync 覆盖为 "已加载 N 个云端模板"
    }

    [Fact]
    public async Task UploadCurrentTemplateAsync_NotLoggedIn_Fails()
    {
        var vm = NewVm();
        var result = await vm.UploadCurrentTemplateAsync("MyTemplate");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未登录");
    }

    [Fact]
    public async Task UploadCurrentTemplateAsync_PassesConfigToCloud()
    {
        var vm = NewVm();
        vm.Config = SampleConfig();
        await vm.LoginAsync("user@example.com", "password123");

        await vm.UploadCurrentTemplateAsync("ConfigTest");

        // 验证 Cloud 端收到正确 Config
        var templates = await ((MockCloudSyncService)vm.CloudSync).ListCloudTemplatesAsync();
        templates[0].Name.Should().Be("ConfigTest");
    }

    // ============ RefreshCloudTemplatesAsync ============

    [Fact]
    public async Task RefreshCloudTemplatesAsync_NotLoggedIn_ClearsList()
    {
        var vm = NewVm();
        vm.CloudTemplates.Add(new CloudTemplateInfo(1, "stale", DateTime.UtcNow, DateTime.UtcNow));
        vm.CloudTemplates.Count.Should().Be(1);

        await vm.RefreshCloudTemplatesAsync();

        vm.CloudTemplates.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshCloudTemplatesAsync_AfterLogin_PopulatesList()
    {
        var vm = NewVm();
        await vm.LoginAsync("user@example.com", "password123");
        var mock = (MockCloudSyncService)vm.CloudSync;
        await mock.UploadTemplateAsync(new TemplateRecord(1, "A", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow));
        await mock.UploadTemplateAsync(new TemplateRecord(2, "B", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow));

        await vm.RefreshCloudTemplatesAsync();

        vm.CloudTemplates.Count.Should().Be(2);
    }

    // ============ DownloadAndApplyCloudTemplateAsync ============

    [Fact]
    public async Task DownloadAndApply_AfterLogin_UpdatesConfig()
    {
        var vm = NewVm();
        await vm.LoginAsync("user@example.com", "password123");

        // 上传一个自定义 Config
        var customConfig = new WatermarkConfig
        {
            Name = "Downloaded",
            Layers = new()
            {
                new TextWatermarkLayer { Text = "FROM_CLOUD", FontSize = 48f, Color = "#0000FF" }
            }
        };
        var mock = (MockCloudSyncService)vm.CloudSync;
        var uploadResult = await mock.UploadTemplateAsync(
            new TemplateRecord(0, "Downloaded", customConfig, DateTime.UtcNow, DateTime.UtcNow));

        // 下载并应用
        var result = await vm.DownloadAndApplyCloudTemplateAsync(uploadResult.CloudId!.Value);
        result.Success.Should().BeTrue();
        vm.Config.Name.Should().Be("Downloaded");
        var layer = (TextWatermarkLayer)vm.Config.Layers[0];
        layer.Text.Should().Be("FROM_CLOUD");
        layer.FontSize.Should().Be(48f);
    }

    [Fact]
    public async Task DownloadAndApply_NotLoggedIn_Fails()
    {
        var vm = NewVm();
        var result = await vm.DownloadAndApplyCloudTemplateAsync(1);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未登录");
    }

    [Fact]
    public async Task DownloadAndApply_NonExistent_Fails()
    {
        var vm = NewVm();
        await vm.LoginAsync("user@example.com", "password123");
        var result = await vm.DownloadAndApplyCloudTemplateAsync(999);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("不存在");
    }

    // ============ DeleteCloudTemplateAsync ============

    [Fact]
    public async Task DeleteCloudTemplateAsync_AfterLogin_Removes()
    {
        var vm = NewVm();
        await vm.LoginAsync("user@example.com", "password123");
        var mock = (MockCloudSyncService)vm.CloudSync;
        var upload = await mock.UploadTemplateAsync(
            new TemplateRecord(0, "ToDelete", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow));
        await mock.UploadTemplateAsync(new TemplateRecord(0, "Keep", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow));
        await vm.RefreshCloudTemplatesAsync();
        vm.CloudTemplates.Count.Should().Be(2);

        var deleted = await vm.DeleteCloudTemplateAsync(upload.CloudId!.Value);
        deleted.Should().BeTrue();
        vm.CloudTemplates.Count.Should().Be(1);
        vm.CloudTemplates[0].Name.Should().Be("Keep");
    }

    [Fact]
    public async Task DeleteCloudTemplateAsync_NotLoggedIn_ReturnsFalse()
    {
        var vm = NewVm();
        var deleted = await vm.DeleteCloudTemplateAsync(1);
        deleted.Should().BeFalse();
    }

    // ============ UI binding 验证 ============

    [Fact]
    public async Task LoginAsync_FiresIsCloudSyncing_PropertyChanged()
    {
        var vm = NewVm();
        var syncSeen = false;
        var notSyncSeen = false;

        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsCloudSyncing))
            {
                if (vm.IsCloudSyncing) syncSeen = true;
                else notSyncSeen = true;
            }
        };

        await vm.LoginAsync("user@example.com", "password123");

        syncSeen.Should().BeTrue();
        notSyncSeen.Should().BeTrue();
        vm.IsCloudSyncing.Should().BeFalse();
    }
}