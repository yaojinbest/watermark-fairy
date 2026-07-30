using System.IO;
using FluentAssertions;
using SixLabors.ImageSharp;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

public class ImageProcessorTests
{
    private readonly ImageProcessor _processor = new();

    private static string OutPath(string ext = "png")
        => Path.Combine(Path.GetTempPath(), $"wf_out_{Guid.NewGuid():N}.{ext}");

    // ============ 文字水印（9 宫格）============

    [Theory]
    [InlineData(WatermarkPosition.TopLeft)]
    [InlineData(WatermarkPosition.TopCenter)]
    [InlineData(WatermarkPosition.TopRight)]
    [InlineData(WatermarkPosition.MiddleLeft)]
    [InlineData(WatermarkPosition.MiddleCenter)]
    [InlineData(WatermarkPosition.MiddleRight)]
    [InlineData(WatermarkPosition.BottomLeft)]
    [InlineData(WatermarkPosition.BottomCenter)]
    [InlineData(WatermarkPosition.BottomRight)]
    public async Task TextWatermark_AllPositions_OutputExistsAndValid(WatermarkPosition pos)
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer { Text = "TEST", Position = pos, FontSize = 32, Color = "#FF0000" }
            }
        };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Width.Should().Be(800);
        result.Height.Should().Be(600);

        // 验证输出文件可被 ImageSharp 读回
        using var image = Image.Load(output);
        image.Width.Should().Be(800);
        image.Height.Should().Be(600);

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task TextWatermark_WithOpacity_AppliesAlpha()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "半透明",
                    Position = WatermarkPosition.BottomRight,
                    FontSize = 48,
                    Color = "#FFFFFF",
                    Opacity = 0.5f,
                }
            }
        };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Format.Should().Be("png");

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task TextWatermark_CustomFontFamily_FallsBackToSystem()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Fallback Test",
                    FontFamily = "NonExistentFont_12345",
                    Position = WatermarkPosition.MiddleCenter,
                    FontSize = 32,
                }
            }
        };

        // 不存在的字体应该 fallback 到系统字体（不抛异常）
        var act = async () => await _processor.ApplyAsync(input, output, config);
        await act.Should().NotThrowAsync();

        File.Delete(input);
        File.Delete(output);
    }

    // ============ 图片水印 ============

    [Fact]
    public async Task ImageWatermark_AppliesLogo()
    {
        var input = TestImageGenerator.CreateSolid(width: 1000, height: 800);
        var logo = TestImageGenerator.CreateLogo(width: 200, height: 60);
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new ImageWatermarkLayer
                {
                    ImagePath = logo,
                    Scale = 0.2f,
                    Position = WatermarkPosition.BottomRight,
                    Opacity = 0.7f,
                }
            }
        };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Format.Should().Be("png");

        File.Delete(input);
        File.Delete(logo);
        File.Delete(output);
    }

    [Fact]
    public async Task ImageWatermark_NonExistentLogo_Throws()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new ImageWatermarkLayer
                {
                    ImagePath = "/nonexistent/path/logo.png",
                    Scale = 0.2f,
                }
            }
        };

        var act = async () => await _processor.ApplyAsync(input, output, config);
        await act.Should().ThrowAsync<FileNotFoundException>();

        File.Delete(input);
    }

    // ============ 输出格式 ============

    [Theory]
    [InlineData("jpg", "jpg")]
    [InlineData("jpeg", "jpg")]
    [InlineData("png", "png")]
    [InlineData("webp", "webp")]
    public async Task Output_AutoFormat_FromExtension(string ext, string expected)
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath(ext);
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "auto", Quality = 85 },
        };

        var result = await _processor.ApplyAsync(input, output, config);

        result.Format.Should().Be(expected);
        File.Exists(output).Should().BeTrue();

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task Output_ExplicitJpg_QualityApplied()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "jpg", Quality = 50 },
        };

        var result = await _processor.ApplyAsync(input, output, config);

        result.Format.Should().Be("jpg");
        var info = new FileInfo(output);
        info.Length.Should().BeGreaterThan(0);

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task Output_AutoFormat_UnknownExtension_Throws()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("xyz");
        var config = new WatermarkConfig { Output = new OutputOptions { Format = "auto" } };

        var act = async () => await _processor.ApplyAsync(input, output, config);
        await act.Should().ThrowAsync<NotSupportedException>();

        File.Delete(input);
    }

    // ============ 错误处理 ============

    [Fact]
    public async Task Input_NotFound_Throws()
    {
        var output = OutPath();
        var config = new WatermarkConfig();

        var act = async () => await _processor.ApplyAsync("/nonexistent/img.jpg", output, config);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Output_Exists_NotOverwrite_Throws()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        File.WriteAllText(output, "existing");  // pre-create

        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Overwrite = false },
        };

        var act = async () => await _processor.ApplyAsync(input, output, config);
        await act.Should().ThrowAsync<IOException>();

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task EmptyLayers_NoWatermark_OutputStillValid()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig { Layers = new() };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Width.Should().Be(800);

        File.Delete(input);
        File.Delete(output);
    }

    // ============ v0.3.1 描边 + 背景色 ============

    [Fact]
    public async Task TextWatermark_WithStroke_AppliesPenAndFill()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Stroke",
                    Position = WatermarkPosition.BottomRight,
                    FontSize = 48,
                    Color = "#FFFF00",
                    Stroke = true,
                    StrokeColor = "#000000",
                    StrokeWidth = 2.5f,
                }
            }
        };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Format.Should().Be("png");

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task TextWatermark_WithBackground_AppliesFillRectangle()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "BG",
                    Position = WatermarkPosition.BottomRight,
                    FontSize = 48,
                    Color = "#FFFFFF",
                    HasBackground = true,
                    BackgroundColor = "#0000FF",
                    BackgroundPadding = 8,
                }
            }
        };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Format.Should().Be("png");

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task TextWatermark_WithStrokeAndBackground_Combined()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Combo",
                    Position = WatermarkPosition.MiddleCenter,
                    FontSize = 48,
                    Color = "#FFFF00",
                    Stroke = true,
                    StrokeColor = "#000000",
                    StrokeWidth = 1.5f,
                    HasBackground = true,
                    BackgroundColor = "#FF00FF",
                    BackgroundPadding = 6,
                }
            }
        };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Format.Should().Be("png");

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public void MeasureTextLayerSize_IncludesStrokeAndBackgroundPadding()
    {
        var layer = new TextWatermarkLayer
        {
            Text = "Size Test",
            FontFamily = "Microsoft YaHei",
            FontSize = 24,
        };

        // 0 padding baseline = 纯文本尺寸
        var (w0, h0) = _processor.MeasureTextLayerSize(layer);
        var (textW, textH) = _processor.MeasureTextSize(layer.Text, layer.FontFamily, layer.FontSize);
        w0.Should().Be(textW);
        h0.Should().Be(textH);

        // 加描边 strokeWidth=4 → Math.Ceiling(4/2)=2 → 两边 2+2=4 padding
        layer.Stroke = true;
        layer.StrokeWidth = 4.0f;
        var (w1, h1) = _processor.MeasureTextLayerSize(layer);
        w1.Should().Be(textW + 4);
        h1.Should().Be(textH + 4);

        // 加背景 padding=10 → Math.Max(2,10)=10 → 两边 10+10=20 padding
        layer.HasBackground = true;
        layer.BackgroundPadding = 10;
        var (w2, h2) = _processor.MeasureTextLayerSize(layer);
        w2.Should().Be(textW + 20);
        h2.Should().Be(textH + 20);
    }

    // ============ v0.3.2 裁剪 ============

    [Fact]
    public async Task ApplyAsync_WithCropRect_OutputIsCropped()
    {
        var input = TestImageGenerator.CreateSolid();  // 800x600
        var output = OutPath();
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Cropped",
                    Position = WatermarkPosition.BottomRight,
                    FontSize = 32,
                    Color = "#FF0000",
                }
            }
        };
        // crop 400×300 区域从 (100, 100) 开始
        var crop = new CropRect(X: 100, Y: 100, Width: 400, Height: 300);

        var result = await _processor.ApplyAsync(input, output, config, crop);

        File.Exists(output).Should().BeTrue();
        result.Width.Should().Be(400);   // 裁剪后宽度
        result.Height.Should().Be(300);  // 裁剪后高度

        using var image = Image.Load(output);
        image.Width.Should().Be(400);
        image.Height.Should().Be(300);

        File.Delete(input);
        File.Delete(output);
    }

    [Fact]
    public async Task ApplyToImageAsync_WithCropRect_ReturnsCroppedImage()
    {
        var input = TestImageGenerator.CreateSolid();  // 800x600
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Cropped",
                    Position = WatermarkPosition.BottomRight,
                    FontSize = 24,
                }
            }
        };
        var crop = new CropRect(X: 0, Y: 0, Width: 500, Height: 400);

        using var image = await _processor.ApplyToImageAsync(input, config, crop);

        image.Width.Should().Be(500);
        image.Height.Should().Be(400);

        File.Delete(input);
    }

    [Fact]
    public async Task LoadOriginalAsync_WithCropRect_ReturnsCroppedImage()
    {
        var input = TestImageGenerator.CreateSolid();  // 800x600
        var crop = new CropRect(X: 200, Y: 150, Width: 300, Height: 200);

        using var image = await _processor.LoadOriginalAsync(input, crop);

        image.Width.Should().Be(300);
        image.Height.Should().Be(200);

        File.Delete(input);
    }

    [Fact]
    public async Task ApplyAsync_NullCropRect_OutputIsFullSize()
    {
        var input = TestImageGenerator.CreateSolid();  // 800x600
        var output = OutPath();
        var config = new WatermarkConfig();

        var result = await _processor.ApplyAsync(input, output, config, cropRect: null);

        result.Width.Should().Be(800);   // 不裁剪
        result.Height.Should().Be(600);

        File.Delete(input);
        File.Delete(output);
    }
}
