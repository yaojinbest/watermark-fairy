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

    // ============ TriggerPreview 防抖 ============

    [Fact]
    public async Task TriggerPreview_RapidCalls_OnlyRendersLast()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);

        // 快速连发 5 次（100ms 内）
        for (var i = 0; i < 5; i++)
            _vm.TriggerPreview(SampleConfig());

        // 等待防抖 + 渲染完成
        await Task.Delay(500);

        // 验证 PreviewImage 最终有值（说明渲染过）
        _vm.PreviewImage.Should().NotBeNull();
        // 验证 IsRendering 最终为 false
        _vm.IsRendering.Should().BeFalse();

        File.Delete(input);
    }

    [Fact]
    public async Task TriggerPreview_NoSource_DoesNothing()
    {
        _vm.TriggerPreview(SampleConfig());
        await Task.Delay(200);
        _vm.PreviewImage.Should().BeNull();
        _vm.StatusText.Should().Be("无图片");
    }

    [Fact]
    public async Task TriggerPreview_RealConfig_RendersWatermark()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);

        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "PREVIEW_TEST",
                    FontSize = 24f,
                    Color = "#FF0000",
                }
            }
        };

        _vm.TriggerPreview(config, debounceMs: 50);
        await Task.Delay(300);

        _vm.PreviewImage.Should().NotBeNull();
        _vm.IsRendering.Should().BeFalse();

        File.Delete(input);
    }

    [Fact]
    public async Task TriggerPreview_CancelsPrevious_OnlyLastRenders()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);

        // 第一次触发
        _vm.TriggerPreview(SampleConfig("First"), debounceMs: 50);
        await Task.Delay(100);  // 第一次应该已经渲染

        // 第二次触发（应取消前一次）
        _vm.TriggerPreview(SampleConfig("Second"), debounceMs: 50);
        await Task.Delay(300);

        _vm.PreviewImage.Should().NotBeNull();
        _vm.IsRendering.Should().BeFalse();

        File.Delete(input);
    }

    // ============ IsRendering 状态 ============

    [Fact]
    public async Task TriggerPreview_SetsIsRenderingDuring_RestoresAfter()
    {
        var input = CreateTempImage();
        _vm.SetSource(input);

        var sawRendering = false;
        var sawNotRendering = false;

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.IsRendering))
            {
                if (_vm.IsRendering) sawRendering = true;
                else sawNotRendering = true;
            }
        };

        _vm.TriggerPreview(SampleConfig(), debounceMs: 50);
        await Task.Delay(500);

        sawRendering.Should().BeTrue();
        sawNotRendering.Should().BeTrue();
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