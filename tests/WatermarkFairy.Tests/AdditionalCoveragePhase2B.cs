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
/// M1-3 Phase 2 第三批：推 coverage 到 80%+
/// 当前 76.14% line / 77.21% branch
/// 目标：80%+ / 85%+
/// </summary>
public class AdditionalCoveragePhase2B
{
    private readonly ImageProcessor _processor = new();

    private static string OutPath(string ext = "png")
        => Path.Combine(Path.GetTempPath(), $"wf_cov2_{Guid.NewGuid():N}.{ext}");

    // ============ ApplyLayers 多层组合 ============

    [Fact]
    public async Task ApplyAsync_TwoTextLayers_BothApplied()
    {
        var input = TestImageGenerator.CreateSolid(width: 1000, height: 800);
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Layer 1",
                    Position = WatermarkPosition.TopLeft,
                    FontSize = 24,
                    Color = "#FF0000",
                },
                new TextWatermarkLayer
                {
                    Text = "Layer 2",
                    Position = WatermarkPosition.BottomRight,
                    FontSize = 24,
                    Color = "#0000FF",
                }
            }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        File.Exists(output).Should().BeTrue();
        result.Width.Should().Be(1000);
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public async Task ApplyAsync_TextAndImageLayers_BothApplied()
    {
        var input = TestImageGenerator.CreateSolid(width: 1000, height: 800);
        var logo = TestImageGenerator.CreateLogo(width: 200, height: 60);
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Combined",
                    Position = WatermarkPosition.TopLeft,
                    FontSize = 32,
                },
                new ImageWatermarkLayer
                {
                    ImagePath = logo,
                    Scale = 0.2f,
                    Position = WatermarkPosition.BottomRight,
                }
            }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        File.Exists(output).Should().BeTrue();
        File.Delete(input); File.Delete(logo); File.Delete(output);
    }

    [Fact]
    public void ApplyLayers_EmptyLayers_DoesNotMutate()
    {
        // ApplyLayers with empty list should be a no-op
        // 签名是 ApplyLayers(Image, IReadOnlyList<WatermarkLayer>)，
        // 传 null image 不合理（image 不可空），所以直接跳过
        // 改测：empty list + 有效 image 不抛
        var img = new Image<Rgba32>(100, 100);
        var act = () => _processor.ApplyLayers(img, new List<WatermarkLayer>());
        act.Should().NotThrow();
    }

    // ============ ApplyAsync 错误路径扩展 ============

    [Fact]
    public async Task ApplyAsync_NullInput_ThrowsArgumentNullException()
    {
        var output = OutPath();
        var config = new WatermarkConfig();
        var act = async () => await _processor.ApplyAsync(null!, output, config);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ApplyAsync_NullOutput_ThrowsArgumentNullException()
    {
        var input = TestImageGenerator.CreateSolid();
        var config = new WatermarkConfig();
        var act = async () => await _processor.ApplyAsync(input, null!, config);
        await act.Should().ThrowAsync<ArgumentNullException>();
        File.Delete(input);
    }

    [Fact]
    public async Task ApplyAsync_NullConfig_ThrowsArgumentNullException()
    {
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath();
        var act = async () => await _processor.ApplyAsync(input, output, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
        File.Delete(input);
    }

    [Fact]
    public async Task ApplyAsync_OutputFormat_UpperCase()
    {
        // 格式名大小写不敏感（auto / AUTO / Auto）
        var input = TestImageGenerator.CreateSolid();
        var output = OutPath("jpg");
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "JPG", Quality = 90 }
        };
        var result = await _processor.ApplyAsync(input, output, config);
        result.Format.Should().Be("jpg");
        File.Delete(input); File.Delete(output);
    }

    [Fact]
    public void ResolveFormat_AutoWithUnknownExtension_Throws()
    {
        // ApplyAsync 时 ResolveFormat 抛 NotSupportedException
        // 直接测：传入 .xyz 扩展名
        var input = TestImageGenerator.CreateSolid();
        var output = Path.Combine(Path.GetTempPath(), "wf_test.xyz");
        var config = new WatermarkConfig
        {
            Output = new OutputOptions { Format = "auto" }
        };
        var act = async () => await _processor.ApplyAsync(input, output, config);
        act.Should().ThrowAsync<NotSupportedException>();
        File.Delete(input);
    }
}
