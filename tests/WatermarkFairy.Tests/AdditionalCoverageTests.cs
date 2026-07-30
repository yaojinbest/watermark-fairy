using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// M1-3 Phase 2 补测：补 80% line coverage
/// 目标：从 67.24% → 80%+
/// </summary>
public class AdditionalCoverageTests
{
    private readonly ImageProcessor _processor = new();

    private static string OutPath(string ext = "png")
        => Path.Combine(Path.GetTempPath(), $"wf_cov_{Guid.NewGuid():N}.{ext}");

    // ============ Output 格式全分支 ============

    [Fact]
    public async Task ApplyAsync_ExplicitPng_OutputIsPng()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("png");
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "png", Quality = 90 }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        result.Format.Should().Be("png");
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_ExplicitWebp_OutputIsWebp()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("webp");
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "webp", Quality = 85 }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        result.Format.Should().Be("webp");
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_AutoFormat_PngExtension()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("png");
        var config = new WatermarkConfig { Output = new OutputOptions { Format = "auto" } };
        var result = await _processor.ApplyAsync(input, output, config);
        result.Format.Should().Be("png");
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_AutoFormat_JpegExtension()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("jpeg");
        var config = new WatermarkConfig { Output = new OutputOptions { Format = "auto" } };
        var result = await _processor.ApplyAsync(input, output, config);
        result.Format.Should().Be("jpg");
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_QualityClamping_LowQuality()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "jpg", Quality = 1 }  // 边界值
        };
        var result = await _processor.ApplyAsync(input, output, config);
        File.Exists(output).Should().BeTrue();
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_QualityClamping_HighQuality()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "jpg", Quality = 100 }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        File.Exists(output).Should().BeTrue();
        File.Delete(input); File.Delete(output);
    }

    // ============ WatermarkResult 字段覆盖 ============

    [Fact]
    public async Task ApplyAsync_ResultContainsAllFields()
    {
        var input = TestImageGenerator.CreateSolid(width: 640, height: 480);
        var output = OutPath("jpg");
        var config = new WatermarkConfig();
        var result = await _processor.ApplyAsync(input, output, config);

        result.InputPath.Should().Be(input);
        result.OutputPath.Should().Be(output);
        result.Format.Should().Be("jpg");
        result.Width.Should().Be(640);
        result.Height.Should().Be(480);
        result.FileSize.Should().BeGreaterThan(0);
        File.Delete(input); File.Delete(output);
    }

    // ============ 错误路径覆盖 ============

    [Fact]
    public async Task ApplyAsync_CancelledToken_Throws()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var config = new WatermarkConfig();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _processor.ApplyAsync(input, output, config, cropRect: null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Delete(input);
    }

    [Fact]
    public void ImageWatermarkLayer_DefaultValues()
    {
        var layer = new ImageWatermarkLayer();
        layer.ImagePath.Should().Be("");
        layer.Scale.Should().Be(0.2f);
        layer.Position.Should().Be(WatermarkPosition.BottomRight);
        layer.Margin.Should().Be(20);
        layer.Opacity.Should().Be(1.0f);
        layer.Rotation.Should().Be(0);
    }

    [Fact]
    public void TextWatermarkLayer_DefaultValues()
    {
        var layer = new TextWatermarkLayer();
        layer.Text.Should().Be("© Watermark Fairy");
        layer.FontFamily.Should().Be("Microsoft YaHei");
        layer.FontSize.Should().Be(24f);
        layer.Color.Should().Be("#FFFFFF");
        layer.Position.Should().Be(WatermarkPosition.BottomRight);
        layer.Stroke.Should().BeFalse();
        layer.Shadow.Should().BeFalse();
    }

    [Fact]
    public void OutputOptions_DefaultValues()
    {
        var opts = new OutputOptions();
        opts.Format.Should().Be("auto");
        opts.Quality.Should().Be(90);
        opts.Overwrite.Should().BeTrue();
    }

    [Fact]
    public void WatermarkConfig_DefaultValues()
    {
        var cfg = new WatermarkConfig();
        cfg.Name.Should().Be("默认");
        cfg.Layers.Should().NotBeNull().And.BeEmpty();
        cfg.Output.Should().NotBeNull();
    }

    [Fact]
    public void WatermarkResult_RecordEquality()
    {
        var r1 = new WatermarkResult("in", "out", "jpg", 100, 100, 1024);
        var r2 = new WatermarkResult("in", "out", "jpg", 100, 100, 1024);
        r1.Should().Be(r2);
    }

    [Fact]
    public void NamingRule_DefaultValues()
    {
        var rule = new NamingRule { Pattern = "test" };
        rule.IsRegex.Should().BeFalse();
        rule.Order.Should().Be(0);
        rule.Replacement.Should().BeNull();
    }
}
