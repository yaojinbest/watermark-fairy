using System.Threading.Tasks;
using FluentAssertions;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using WatermarkFairy.ViewModels;
using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// MainViewModel ICommand 测试（M3-2）
/// 验证 LoginCommand / LogoutCommand / UploadCurrentCommand / RefreshCloudCommand 等 CanExecute 行为
/// </summary>
public class MainViewModelCommandsTests
{
    private static MainViewModel NewVm(MockCloudSyncService? mock = null)
    {
        mock ??= new MockCloudSyncService();
        return new MainViewModel(new ImageProcessor(), null, mock);
    }

    // ============ CanLogin / LoginCommand ============

    [Fact]
    public void CanLogin_EmptyEmailAndPassword_False()
    {
        var vm = NewVm();
        vm.CanLogin.Should().BeFalse();
    }

    [Fact]
    public void CanLogin_EmailButEmptyPassword_False()
    {
        var vm = NewVm();
        vm.LoginEmail = "user@example.com";
        vm.LoginPassword = "";
        vm.CanLogin.Should().BeFalse();
    }

    [Fact]
    public void CanLogin_BothFilled_True()
    {
        var vm = NewVm();
        vm.LoginEmail = "user@example.com";
        vm.LoginPassword = "password";
        vm.CanLogin.Should().BeTrue();
    }

    [Fact]
    public void LoginCommand_CanExecute_BothFilled_True()
    {
        var vm = NewVm();
        vm.LoginEmail = "user@example.com";
        vm.LoginPassword = "password";
        vm.LoginCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task LoginCommand_Execute_CallsLoginAsync()
    {
        var vm = NewVm();
        vm.LoginEmail = "user@example.com";
        vm.LoginPassword = "password";

        await vm.LoginCommand.ExecuteAsync(null);

        vm.IsCloudAuthenticated.Should().BeTrue();
        vm.CloudUserEmail.Should().Be("user@example.com");
    }

    [Fact]
    public async Task LoginCommand_Execute_ClearsPassword()
    {
        var vm = NewVm();
        vm.LoginEmail = "user@example.com";
        vm.LoginPassword = "password123";

        await vm.LoginCommand.ExecuteAsync(null);

        vm.LoginPassword.Should().BeNullOrEmpty();
    }

    // ============ CanLoggedIn / LogoutCommand ============

    [Fact]
    public void CanLoggedIn_NotAuthenticated_False()
    {
        var vm = NewVm();
        vm.IsCloudAuthenticated.Should().BeFalse();
        vm.CanLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task CanLoggedIn_AfterRealLogin_True()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        vm.IsCloudAuthenticated.Should().BeTrue();
        vm.CanLoggedIn.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutCommand_Execute_CallsLogout()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        vm.IsCloudAuthenticated.Should().BeTrue();

        await vm.LogoutCommand.ExecuteAsync(null);

        vm.IsCloudAuthenticated.Should().BeFalse();
        vm.CloudUserEmail.Should().BeNullOrEmpty();
    }

    // ============ CanLoggedIn 影响其他命令 ============

    [Fact]
    public void LogoutCommand_CanExecute_NotAuthenticated_False()
    {
        var vm = NewVm();
        vm.IsCloudAuthenticated.Should().BeFalse();
        vm.LogoutCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void UploadCurrentCommand_CanExecute_NotAuthenticated_False()
    {
        var vm = NewVm();
        vm.UploadCurrentCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task UploadCurrentCommand_CanExecute_AfterLogin_True()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        vm.UploadCurrentCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void RefreshCloudCommand_CanExecute_NotAuthenticated_False()
    {
        var vm = NewVm();
        vm.RefreshCloudCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task RefreshCloudCommand_CanExecute_AfterLogin_True()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        vm.RefreshCloudCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshCloudCommand_Execute_AfterLogin_PopulatesList()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        var mock = (MockCloudSyncService)vm.CloudSync;
        await mock.UploadTemplateAsync(new TemplateRecord(0, "X", new WatermarkConfig(), DateTime.UtcNow, DateTime.UtcNow));

        await vm.RefreshCloudCommand.ExecuteAsync(null);

        vm.CloudTemplates.Count.Should().Be(1);
    }

    [Fact]
    public async Task DownloadCloudCommand_Execute_WithNull_DoesNothing()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        await vm.DownloadCloudCommand.ExecuteAsync(null);
        // 不抛异常
        vm.CloudTemplates.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadCloudCommand_Execute_WithTemplate_Downloads()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        var mock = (MockCloudSyncService)vm.CloudSync;
        var customConfig = new WatermarkConfig
        {
            Name = "Downloaded",
            Layers = new() { new TextWatermarkLayer { Text = "CLOUD" } }
        };
        var upload = await mock.UploadTemplateAsync(new TemplateRecord(0, "Downloaded", customConfig, DateTime.UtcNow, DateTime.UtcNow));

        await vm.DownloadCloudCommand.ExecuteAsync(new CloudTemplateInfo(upload.CloudId!.Value, "Downloaded", DateTime.UtcNow, DateTime.UtcNow));

        vm.Config.Name.Should().Be("Downloaded");
    }

    [Fact]
    public async Task DeleteCloudCommand_Execute_WithNull_DoesNothing()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        await vm.DeleteCloudCommand.ExecuteAsync(null);
        vm.CloudTemplates.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCloudCommand_Execute_WithTemplate_Deletes()
    {
        var vm = NewVm();
        await vm.LoginCommand.ExecuteAsync(null);
        var mock = (MockCloudSyncService)vm.CloudSync;
        var upload = await mock.UploadTemplateAsync(new TemplateRecord(0, "Delete", new WatermarkConfig(), DateTime.UtcNow, DateTime.UtcNow));
        await vm.RefreshCloudCommand.ExecuteAsync(null);
        vm.CloudTemplates.Count.Should().Be(1);

        await vm.DeleteCloudCommand.ExecuteAsync(upload.CloudId!.Value);

        vm.CloudTemplates.Should().BeEmpty();
    }
}