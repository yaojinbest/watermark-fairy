using System.IO;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WatermarkFairy.Models;
using WatermarkFairy.ViewModels;
using Xunit;

namespace WatermarkFairy.Tests;

public class PreviewViewModelTests
{
    private readonly PreviewViewModel _vm = new();

    // ============ 默认 ============

    [Fact]
    public void Constructor_Default_HasExpectedDefaults()
    {
        var vm = new PreviewViewModel();
        vm.SourcePath.Should().BeNull();
        vm.PreviewImage.Should().BeNull();
        vm.IsRendering.Should().BeFalse();
        vm.StatusText.Should().NotBeNullOrEmpty();
        vm.PreviewMaxSize.Should().Be(800);
    }

    // ============ SetSource ============

    [Fact]
    public void SetSource_Null_ClearsPreview()
    {
        _vm.SetSource("/tmp/test.png");
        _vm.SetSource(null);
        _vm.SourcePath.Should().BeNull();
        _vm.PreviewImage.Should().BeNull();
    }

    [Fact]
    public void SetSource_Path_SetsSourcePath_NoPreviewYet()
    {
        _vm.SetSource("/tmp/test.png");
        _vm.SourcePath.Should().Be("/tmp/test.png");
        _vm.PreviewImage.Should().BeNull();
    }

    // ============ TriggerPreview 防抖（无 WPF 渲染）============

    [Fact]
    public async Task TriggerPreview_NoSource_DoesNothing()
    {
        // SourcePath 为空 → 跳过渲染 → 不 set IsRendering
        _vm.TriggerPreview(SampleConfig());
        await Task.Delay(200);
        _vm.PreviewImage.Should().BeNull();
        _vm.StatusText.Should().Be("无图片");
    }

    [Fact]
    public void TriggerPreview_TriggersDebounce()
    {
        // TriggerPreview 应启动异步任务（延时 + 渲染）
        // 这里只验证不会抛异常
        var input = CreateTempImage();
        _vm.SetSource(input);
        var act = () => _vm.TriggerPreview(SampleConfig());
        act.Should().NotThrow();
        File.Delete(input);
    }

    [Fact]
    public async Task TriggerPreview_RapidCalls_DoesNotThrow()
    {
        // 快速的 5 连发不应抛异常
        var input = CreateTempImage();
        _vm.SetSource(input);
        for (var i = 0; i < 5; i++)
            _vm.TriggerPreview(SampleConfig());
        await Task.Delay(200);
        File.Delete(input);
    }

    [Fact]
    public async Task SetSource_CancelsPendingPreview()
    {
        // SetSource 应取消正在等待 debounce 的预览
        var input = CreateTempImage();
        _vm.SetSource(input);
        _vm.TriggerPreview(SampleConfig(), debounceMs: 200);
        _vm.SetSource(null);  // 取消
        await Task.Delay(100);  // 还没到 200ms debounce
        _vm.SourcePath.Should().BeNull();
    }

    // ============ 渲染测试（标记 skip — 需要 WPF STA thread）============

    [Fact(Skip = "需要 WPF STA thread，xUnit 默认 MTA")]
    public async Task TriggerPreview_SetsIsRenderingDuring_RestoresAfter()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);
        _vm.TriggerPreview(SampleConfig(), debounceMs: 50);
        await Task.Delay(2000);
        _vm.IsRendering.Should().BeFalse();
        File.Delete(input);
    }

    [Fact(Skip = "需要 WPF STA thread，BitmapImage 在 MTA 静默返回 null")]
    public async Task TriggerPreview_RapidCalls_OnlyRendersLast()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);
        for (var i = 0; i < 5; i++)
            _vm.TriggerPreview(SampleConfig());
        await Task.Delay(500);
        _vm.PreviewImage.Should().NotBeNull();
        File.Delete(input);
    }

    [Fact(Skip = "需要 WPF STA thread，BitmapImage 在 MTA 静默返回 null")]
    public async Task TriggerPreview_RealConfig_RendersWatermark()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer { Text = "TEST", FontSize = 24f, Color = "#FF0000" }
            }
        };
        _vm.TriggerPreview(config, debounceMs: 50);
        await Task.Delay(300);
        _vm.PreviewImage.Should().NotBeNull();
        _vm.IsRendering.Should().BeFalse();
        File.Delete(input);
    }

    [Fact(Skip = "需要 WPF STA thread，BitmapImage 在 MTA 静默返回 null")]
    public async Task TriggerPreview_CancelsPrevious_OnlyLastRenders()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);
        _vm.TriggerPreview(SampleConfig("First"), debounceMs: 50);
        await Task.Delay(100);
        _vm.TriggerPreview(SampleConfig("Second"), debounceMs: 50);
        await Task.Delay(300);
        _vm.PreviewImage.Should().NotBeNull();
        _vm.IsRendering.Should().BeFalse();
        File.Delete(input);
    }

    // ============ helpers ============

    private static WatermarkConfig SampleConfig(string text = "Test") =>
        new()
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = text,
                    FontSize = 24f,
                    Position = WatermarkPosition.BottomRight,
                }
            }
        };

    private static string CreateTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wf_preview_{Guid.NewGuid():N}.png");
        using var img = new Image<Rgba32>(Configuration.Default, 200, 150);
        img.SaveAsPng(path);
        return path;
    }
}
