using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// M1-3 Phase 2 第四批：硬推 76% → 80%+
/// 进哥决策 B：补 4-5 测试
/// </summary>
public class AdditionalCoveragePhase2C
{
    private readonly ImageProcessor _processor = new();

    private static string OutPath(string ext = "png")
        => Path.Combine(Path.GetTempPath(), $"wf_covC_{Guid.NewGuid():N}.{ext}");

    // ============ Layer 全字段覆盖 ============

    [Fact]
    public void TextWatermarkLayer_AllFieldsSet_RetainsValues()
    {
        var layer = new TextWatermarkLayer
        {
            Text = "Hello",
            FontFamily = "Arial",
            FontSize = 48f,
            Color = "#FF8800",
            Stroke = true,
            StrokeColor = "#000000",
            StrokeWidth = 2.5f,
            Shadow = true,
            Position = WatermarkPosition.MiddleCenter,
            Margin = 50,
            Opacity = 0.5f,
            Rotation = 45,
        };
        layer.Text.Should().Be("Hello");
        layer.FontFamily.Should().Be("Arial");
        layer.FontSize.Should().Be(48f);
        layer.Color.Should().Be("#FF8800");
        layer.Stroke.Should().BeTrue();
        layer.StrokeColor.Should().Be("#000000");
        layer.StrokeWidth.Should().Be(2.5f);
        layer.Shadow.Should().BeTrue();
        layer.Position.Should().Be(WatermarkPosition.MiddleCenter);
        layer.Margin.Should().Be(50);
        layer.Opacity.Should().Be(0.5f);
        layer.Rotation.Should().Be(45);
    }

    [Fact]
    public void ImageWatermarkLayer_AllFieldsSet_RetainsValues()
    {
        var layer = new ImageWatermarkLayer
        {
            ImagePath = "/path/to/logo.png",
            Scale = 0.3f,
            Position = WatermarkPosition.TopCenter,
            Margin = 10,
            Opacity = 0.7f,
            Rotation = 90,
        };
        layer.ImagePath.Should().Be("/path/to/logo.png");
        layer.Scale.Should().Be(0.3f);
        layer.Position.Should().Be(WatermarkPosition.TopCenter);
        layer.Margin.Should().Be(10);
        layer.Opacity.Should().Be(0.7f);
        layer.Rotation.Should().Be(90);
    }

    [Fact]
    public void WatermarkLayer_BaseClass_DefaultValues()
    {
        // 抽象基类 WatermarkLayer 的默认值
        var textLayer = new TextWatermarkLayer();
        textLayer.Position.Should().Be(WatermarkPosition.BottomRight);
        textLayer.Margin.Should().Be(20);
        textLayer.Opacity.Should().Be(1.0f);
        textLayer.Rotation.Should().Be(0);

        var imgLayer = new ImageWatermarkLayer();
        imgLayer.Position.Should().Be(WatermarkPosition.BottomRight);
        imgLayer.Margin.Should().Be(20);
        imgLayer.Opacity.Should().Be(1.0f);
        imgLayer.Rotation.Should().Be(0);
    }

    // ============ ApplyAsync Custom 位置 + 边缘 Scale ============

    [Fact]
    public async Task ApplyAsync_TextWatermark_CustomPosition_Works()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Custom",
                    Position = WatermarkPosition.Custom,
                    FontSize = 16,
                }
            }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        File.Exists(output).Should().BeTrue();
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_ImageWatermark_SmallScale()
    {
        var input = TestImageGenerator.CreateSolid(width: 1000, height: 800);
        var logo = TestImageGenerator.CreateLogo(width: 100, height: 30);
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new ImageWatermarkLayer
                {
                    ImagePath = logo,
                    Scale = 0.1f,  // 小尺寸
                    Position = WatermarkPosition.BottomLeft,
                }
            }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        File.Exists(output).Should().BeTrue();
        File.Delete(input); File.Delete(logo); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_ImageWatermark_LargeScale()
    {
        var input = TestImageGenerator.CreateSolid(width: 1000, height: 800);
        var logo = TestImageGenerator.CreateLogo(width: 200, height: 60);
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new ImageWatermarkLayer
                {
                    ImagePath = logo,
                    Scale = 0.5f,  // 大尺寸
                    Position = WatermarkPosition.Center,  // 用一个未单独测试的位置
                }
            }
        };
        // Center 实际是 MiddleCenter（修正）但 MiddleCenter 之前已测试
        // 这里测的是 scale=0.5 的大尺寸渲染
        // ImageWatermarkLayer 缺 Position.Center → 改用 MiddleCenter
        config.Layers[0].Position = WatermarkPosition.MiddleCenter;
        var result = await _processor.ApplyAsync(input, output, config);
        File.Exists(output).Should().BeTrue();
        File.Delete(input); File.Delete(logo); File.Delete(output);
    }
}