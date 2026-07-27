using System.IO;
using FluentAssertions;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

public class MockCloudSyncServiceTests
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

    // ============ 认证 ============

    [Fact]
    public async Task LoginAsync_ValidCredentials_Succeeds()
    {
        var svc = new MockCloudSyncService();
        var result = await svc.LoginAsync("user@example.com", "password123");

        result.Success.Should().BeTrue();
        result.UserEmail.Should().Be("user@example.com");
        svc.IsAuthenticated.Should().BeTrue();
        svc.CurrentUserEmail.Should().Be("user@example.com");
    }

    [Fact]
    public async Task LoginAsync_EmptyEmail_Fails()
    {
        var svc = new MockCloudSyncService();
        var result = await svc.LoginAsync("", "password123");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("邮箱");
    }

    [Fact]
    public async Task LoginAsync_EmptyPassword_Fails()
    {
        var svc = new MockCloudSyncService();
        var result = await svc.LoginAsync("user@example.com", "");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("密码");
    }

    [Fact]
    public async Task LoginAsync_ShortPassword_Fails()
    {
        var svc = new MockCloudSyncService();
        var result = await svc.LoginAsync("user@example.com", "123");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("6");
    }

    [Fact]
    public async Task LogoutAsync_ClearsAuth()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");
        await svc.LogoutAsync();
        svc.IsAuthenticated.Should().BeFalse();
        svc.CurrentUserEmail.Should().BeNull();
    }

    // ============ 上传 / 下载 roundtrip ============

    [Fact]
    public async Task UploadDownload_Roundtrips_Config()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");

        var originalRecord = new TemplateRecord(
            1, "Roundtrip", SampleConfig(),
            DateTime.UtcNow, DateTime.UtcNow);

        var upload = await svc.UploadTemplateAsync(originalRecord);
        upload.Success.Should().BeTrue();
        upload.CloudId.Should().NotBeNull();
        upload.CloudId.Should().BeGreaterThan(0);

        var download = await svc.DownloadTemplateAsync(upload.CloudId!.Value);
        download.Success.Should().BeTrue();
        download.Template.Should().NotBeNull();
        download.Template!.Name.Should().Be("Roundtrip");
        download.Template.Config.Name.Should().Be("Cloud Test");
    }

    [Fact]
    public async Task UploadAsync_NotLoggedIn_Fails()
    {
        var svc = new MockCloudSyncService();
        var record = new TemplateRecord(1, "Test", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow);
        var result = await svc.UploadTemplateAsync(record);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未登录");
    }

    [Fact]
    public async Task UploadAsync_SimulatedFailure_Fails()
    {
        var svc = new MockCloudSyncService { SimulateUploadFailure = true };
        await svc.LoginAsync("user@example.com", "password123");
        var record = new TemplateRecord(1, "Test", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow);
        var result = await svc.UploadTemplateAsync(record);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("模拟失败");
    }

    [Fact]
    public async Task DownloadAsync_NonExistent_Fails()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");
        var result = await svc.DownloadTemplateAsync(999);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("不存在");
    }

    // ============ 列表 ============

    [Fact]
    public async Task ListAsync_Empty_ReturnsEmpty()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");
        var list = await svc.ListCloudTemplatesAsync();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_AfterUploads_ReturnsAll()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");

        for (var i = 0; i < 3; i++)
        {
            var rec = new TemplateRecord(i, $"Template {i}", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow);
            await svc.UploadTemplateAsync(rec);
        }

        var list = await svc.ListCloudTemplatesAsync();
        list.Count.Should().Be(3);
        list.Select(t => t.Name).Should().Contain(new[] { "Template 0", "Template 1", "Template 2" });
    }

    [Fact]
    public async Task ListAsync_NotLoggedIn_ReturnsEmpty()
    {
        var svc = new MockCloudSyncService();
        var list = await svc.ListCloudTemplatesAsync();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_OrderedByUpdatedAtDesc()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");

        var rec1 = new TemplateRecord(1, "First", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow);
        await svc.UploadTemplateAsync(rec1);
        await Task.Delay(10);
        var rec2 = new TemplateRecord(2, "Second", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow);
        await svc.UploadTemplateAsync(rec2);

        var list = await svc.ListCloudTemplatesAsync();
        list[0].Name.Should().Be("Second");  // 最新在前
        list[1].Name.Should().Be("First");
    }

    // ============ 删除 ============

    [Fact]
    public async Task DeleteAsync_Existing_RemovesAndReturnsTrue()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");
        var rec = new TemplateRecord(1, "Test", SampleConfig(), DateTime.UtcNow, DateTime.UtcNow);
        var upload = await svc.UploadTemplateAsync(rec);
        var deleted = await svc.DeleteCloudTemplateAsync(upload.CloudId!.Value);
        deleted.Should().BeTrue();

        var list = await svc.ListCloudTemplatesAsync();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ReturnsFalse()
    {
        var svc = new MockCloudSyncService();
        await svc.LoginAsync("user@example.com", "password123");
        var deleted = await svc.DeleteCloudTemplateAsync(999);
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NotLoggedIn_ReturnsFalse()
    {
        var svc = new MockCloudSyncService();
        var deleted = await svc.DeleteCloudTemplateAsync(1);
        deleted.Should().BeFalse();
    }

    // ============ 连接测试 ============

    [Fact]
    public async Task TestConnectionAsync_AlwaysSucceeds_Mock()
    {
        var svc = new MockCloudSyncService();
        (await svc.TestConnectionAsync()).Should().BeTrue();
    }

    // ============ 隔离测试（不共享状态）============

    [Fact]
    public async Task TwoInstances_IndependentState()
    {
        var svc1 = new MockCloudSyncService();
        var svc2 = new MockCloudSyncService();

        await svc1.LoginAsync("a@b.com", "password");

        svc1.IsAuthenticated.Should().BeTrue();
        svc2.IsAuthenticated.Should().BeFalse();  // 独立
    }
}