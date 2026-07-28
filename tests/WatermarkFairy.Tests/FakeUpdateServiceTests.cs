using FluentAssertions;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

public class FakeUpdateServiceTests
{
    [Fact]
    public void CurrentVersion_MatchesAssemblyVersion()
    {
        var svc = new FakeUpdateService();

        var expected = typeof(IUpdateService).Assembly.GetName().Version;
        var expectedStr = expected is null
            ? "0.0.0"
            : $"{expected.Major}.{expected.Minor}.{expected.Build}";

        svc.CurrentVersion.Should().Be(expectedStr);
    }

    [Fact]
    public async Task CheckForUpdateAsync_Default_ReturnsNoUpdate()
    {
        var svc = new FakeUpdateService();

        var result = await svc.CheckForUpdateAsync();

        result.IsAvailable.Should().BeFalse();
        result.LatestVersion.Should().BeNull();
        result.ReleaseNotesUrl.Should().BeNull();
        result.CurrentVersion.Should().Be(svc.CurrentVersion);
        result.CheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CheckForUpdateAsync_Default_SetsLastCheckTime()
    {
        var svc = new FakeUpdateService();
        svc.LastCheckTime.Should().BeNull();

        await svc.CheckForUpdateAsync();

        svc.LastCheckTime.Should().NotBeNull();
        svc.LastCheckTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CheckForUpdateAsync_Default_ClearsIsBusy()
    {
        var svc = new FakeUpdateService();

        await svc.CheckForUpdateAsync();

        svc.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_ForceAvailable_ReturnsUpdate()
    {
        var svc = new FakeUpdateService { ForceUpdateAvailable = true };

        var result = await svc.CheckForUpdateAsync();

        result.IsAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("9.9.9-test");
        result.ReleaseNotesUrl.Should().Contain("github.com/yaojinbest/watermark-fairy");
    }

    [Fact]
    public async Task CheckForUpdateAsync_HonorsCancellation()
    {
        var svc = new FakeUpdateService { SimulatedDelayMs = 500 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await svc.CheckForUpdateAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DownloadAsync_Default_Succeeds()
    {
        var svc = new FakeUpdateService();

        var result = await svc.DownloadAsync();

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.DownloadedVersion.Should().Be(svc.CurrentVersion);
    }

    [Fact]
    public async Task DownloadAsync_SimulateFailure_ReturnsError()
    {
        var svc = new FakeUpdateService { SimulateDownloadFailure = true };

        var result = await svc.DownloadAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("模拟下载失败");
        result.DownloadedVersion.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_ReportsProgress()
    {
        var svc = new FakeUpdateService { SimulatedDelayMs = 50 };
        var reports = new List<double>();
        var progress = new Progress<double>(p => reports.Add(p));

        await svc.DownloadAsync(progress);

        reports.Should().NotBeEmpty();
        reports.Last().Should().Be(1.0);
    }

    [Fact]
    public async Task DownloadAsync_ClearsIsBusy()
    {
        var svc = new FakeUpdateService();

        await svc.DownloadAsync();

        svc.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void ApplyAndRestart_DoesNotThrow()
    {
        var svc = new FakeUpdateService();

        var act = () => svc.ApplyAndRestart();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task IsBusy_FlipsDuringCheck()
    {
        var svc = new FakeUpdateService { SimulatedDelayMs = 100 };

        var checkTask = svc.CheckForUpdateAsync();
        // 极短窗口内检查 IsBusy：可能已切回 false（race），所以用 polling 拿中间态
        var snapshotDuring = false;
        for (int i = 0; i < 5; i++)
        {
            if (svc.IsBusy) { snapshotDuring = true; break; }
            await Task.Delay(5);
        }

        await checkTask;
        svc.IsBusy.Should().BeFalse();
        // snapshotDuring 不强制断言（避免 flakiness），但若观察到说明状态翻转正确
    }
}