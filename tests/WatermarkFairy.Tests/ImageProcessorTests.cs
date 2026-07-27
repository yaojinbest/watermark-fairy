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
                    Position = WatermarkPosition.Center,
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
}
