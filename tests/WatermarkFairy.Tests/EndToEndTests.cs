using System.IO;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

/// <summary>
/// 端到端集成测试（M1-8）
/// 完整流程：选图 → 配置水印 → 渲染 → 导出 → 验证
/// </summary>
[Trait("Category", "Integration")]
public class EndToEndTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ImageProcessor _processor = new();

    public EndToEndTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wf_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    // ============ 单文件流程 ============

    [Fact]
    public async Task EndToEnd_SingleFile_TextWatermark_ProducesJpgOutput()
    {
        var input = CreateTestImage(800, 600);
        var output = Path.Combine(_tempDir, "output.jpg");

        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "EndToEnd Test",
                    FontSize = 32f,
                    Color = "#FF0000",
                    Position = WatermarkPosition.BottomRight,
                }
            }
        };

        var result = await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();
        result.Format.Should().Be("jpg");
        result.Width.Should().Be(800);
        result.Height.Should().Be(600);

        // 验证输出可被 ImageSharp 读回
        using var img = Image.Load(output);
        img.Width.Should().Be(800);
        img.Height.Should().Be(600);
    }

    [Fact]
    public async Task EndToEnd_SingleFile_PngOutput_Extension()
    {
        var input = CreateTestImage(400, 300);
        var output = Path.Combine(_tempDir, "out.png");

        await _processor.ApplyAsync(input, output, new WatermarkConfig());

        File.Exists(output).Should().BeTrue();
        // 验证 PNG 格式（PNG magic 头 89 50 4E 47）
        var bytes = new byte[8];
        using var fs = File.OpenRead(output);
        await fs.ReadAsync(bytes, 0, 8);
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50); // P
        bytes[2].Should().Be(0x4E); // N
        bytes[3].Should().Be(0x47); // G
    }

    [Fact]
    public async Task EndToEnd_SingleFile_WebpOutput()
    {
        var input = CreateTestImage(400, 300);
        var output = Path.Combine(_tempDir, "out.webp");

        await _processor.ApplyAsync(input, output, new WatermarkConfig());

        File.Exists(output).Should().BeTrue();
        // RIFF 头（WebP）
        var bytes = new byte[12];
        using var fs = File.OpenRead(output);
        await fs.ReadAsync(bytes, 0, 12);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        System.Text.Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WEBP");
    }

    // ============ 多文件批量流程 ============

    [Fact]
    public async Task EndToEnd_BatchFiles_AllWatermarked()
    {
        // 模拟 MainViewModel.ApplyWatermarkAsync 批量场景
        var inputDir = Path.Combine(_tempDir, "input");
        var outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(inputDir);

        var files = new[]
        {
            CreateTestImageIn(inputDir, "a.png", 200, 150),
            CreateTestImageIn(inputDir, "b.png", 300, 200),
            CreateTestImageIn(inputDir, "c.png", 400, 300),
        };

        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "BATCH",
                    FontSize = 24f,
                    Position = WatermarkPosition.MiddleCenter,
                }
            }
        };

        foreach (var file in files)
        {
            var output = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(file)}_watermarked.jpg");
            await _processor.ApplyAsync(file, output, config);
        }

        // 验证 3 个输出都生成
        var outputs = Directory.GetFiles(outputDir, "*.jpg");
        outputs.Length.Should().Be(3);
    }

    // ============ 命名规则集成 ============

    [Fact]
    public async Task EndToEnd_NamingRule_AppliedToOutput()
    {
        // 验证 NamingRuleEngine + ImageProcessor 集成
        var rule = new NamingRule
        {
            Pattern = "E2E_{n:000}_{name}",
            IsRegex = false,
            Order = 0,
        };

        var input = CreateTestImage(400, 300);

        for (var i = 0; i < 3; i++)
        {
            var ruleCtx = new NamingContext
            {
                OriginalFileName = Path.GetFileNameWithoutExtension(input),
                Extension = "jpg",
                Sequence = i + 1,
                ImageWidth = 400,
                ImageHeight = 300,
                SourcePath = input,
            };
            var outputName = new NamingRuleEngine().Apply(rule.Pattern, ruleCtx);
            var outputPath = Path.Combine(_tempDir, outputName);
            await _processor.ApplyAsync(input, outputPath, new WatermarkConfig());

            File.Exists(outputPath).Should().BeTrue();
        }

        // 验证生成 E2E_001_*.jpg / E2E_002_*.jpg / E2E_003_*.jpg
        var numberedFiles = Directory.GetFiles(_tempDir, "E2E_*.jpg");
        numberedFiles.Length.Should().Be(3);
    }

    // ============ 模板集成（save → load → apply）==========

    [Fact]
    public async Task EndToEnd_TemplateStore_SaveLoadApply()
    {
        // 完整流程：保存模板 → 加载模板 → 应用
        var templateStore = new TemplateStore(Path.Combine(_tempDir, "templates.db"));
        templateStore.Initialize();

        var originalConfig = new WatermarkConfig
        {
            Name = "Brand",
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "© Company",
                    FontSize = 36f,
                    Color = "#000000",
                    Position = WatermarkPosition.BottomRight,
                    Margin = 30,
                }
            }
        };

        // 1. 保存模板
        var templateId = templateStore.Add("Brand Template", originalConfig);
        templateId.Should().BeGreaterThan(0);

        // 2. 加载模板
        var loaded = templateStore.Get(templateId);
        loaded.Should().NotBeNull();
        loaded!.Config.Layers[0].Should().BeOfType<TextWatermarkLayer>();

        // 3. 应用模板到图片
        var input = CreateTestImage(800, 600);
        var output = Path.Combine(_tempDir, "with_template.jpg");
        var result = await _processor.ApplyAsync(input, output, loaded.Config);

        File.Exists(output).Should().BeTrue();
        result.Width.Should().Be(800);
    }

    // ============ AppSettings 集成 ============

    [Fact]
    public async Task EndToEnd_AppSettings_DefaultsUsedAsConfig()
    {
        // 验证 AppSettings 的默认值可以作为 ImageProcessor 的 Config
        var settingsStore = new AppSettingsStore(Path.Combine(_tempDir, "config.json"));
        var settings = settingsStore.Load();

        // 模拟用户用 AppSettings 默认值构造 Config
        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "Settings Test",
                    FontFamily = settings.DefaultFontFamily,
                    FontSize = settings.DefaultFontSize,
                    Color = settings.DefaultColor,
                    Position = WatermarkPosition.BottomRight,
                }
            },
            Output = new OutputOptions
            {
                Format = settings.DefaultOutputFormat,
                Quality = settings.DefaultQuality,
            }
        };

        var input = CreateTestImage(600, 400);
        var output = Path.Combine(_tempDir, "settings_applied.jpg");
        await _processor.ApplyAsync(input, output, config);

        File.Exists(output).Should().BeTrue();

        // 验证 settings 也持久化
        settingsStore.Update(s => s.DefaultQuality = 50);
        settingsStore.Load().DefaultQuality.Should().Be(50);
    }

    // ============ 端到端：模拟 MainViewModel.ApplyWatermarkAsync ============

    [Fact]
    public async Task EndToEnd_FullPipeline_LikeMainViewModel()
    {
        // 模拟 MainViewModel.ApplyWatermarkAsync 的完整流程
        var inputDir = Path.Combine(_tempDir, "in");
        var outputDir = Path.Combine(_tempDir, "out");
        Directory.CreateDirectory(inputDir);

        var files = new[]
        {
            CreateTestImageIn(inputDir, "photo1.png", 640, 480),
            CreateTestImageIn(inputDir, "photo2.png", 800, 600),
            CreateTestImageIn(inputDir, "photo3.png", 1024, 768),
        };

        var config = new WatermarkConfig
        {
            Layers = new()
            {
                new TextWatermarkLayer
                {
                    Text = "© Studio",
                    FontSize = 32f,
                    Color = "#FFFFFF",
                    Position = WatermarkPosition.BottomRight,
                    Opacity = 0.8f,
                }
            },
            Output = new OutputOptions { Format = "jpg", Quality = 85 }
        };

        var processed = 0;
        var failed = 0;
        foreach (var file in files)
        {
            try
            {
                var outputPath = Path.Combine(outputDir,
                    $"{Path.GetFileNameWithoutExtension(file)}_watermarked.jpg");
                await _processor.ApplyAsync(file, outputPath, config);
                processed++;
            }
            catch
            {
                failed++;
            }
        }

        // 验证 3 张图全部处理成功
        processed.Should().Be(3);
        failed.Should().Be(0);

        // 验证输出目录结构
        var outputs = Directory.GetFiles(outputDir, "*.jpg");
        outputs.Length.Should().Be(3);
        foreach (var output in outputs)
        {
            // 验证每个输出文件大小 > 0
            new FileInfo(output).Length.Should().BeGreaterThan(0);
        }
    }

    // ============ 错误处理集成 ============

    [Fact]
    public async Task EndToEnd_InvalidInputPath_ThrowsFileNotFound()
    {
        var config = new WatermarkConfig();
        var act = async () => await _processor.ApplyAsync(
            "/nonexistent/path/to/image.jpg",
            Path.Combine(_tempDir, "out.jpg"),
            config);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task EndToEnd_NullConfig_ThrowsArgumentNull()
    {
        var input = CreateTestImage(400, 300);
        var act = async () => await _processor.ApplyAsync(input, Path.Combine(_tempDir, "out.jpg"), null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ============ 性能冒烟测试（CI 能跑通）==========

    [Fact]
    public async Task EndToEnd_SmallImage_CompletesQuickly()
    {
        // 冒烟测试：确保小图端到端 < 5s
        var input = CreateTestImage(640, 480);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _processor.ApplyAsync(input, Path.Combine(_tempDir, "perf.jpg"), new WatermarkConfig());
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    // ============ helpers ============

    private string CreateTestImage(int w, int h)
    {
        var path = Path.Combine(_tempDir, $"input_{Guid.NewGuid():N}.png");
        using var img = new Image<Rgba32>(Configuration.Default, w, h);
        img.Mutate(c => c.Fill(new Rgba32(180, 180, 200, 255)));
        img.SaveAsPng(path);
        return path;
    }

    private string CreateTestImageIn(string dir, string name, int w, int h)
    {
        var path = Path.Combine(dir, name);
        using var img = new Image<Rgba32>(Configuration.Default, w, h);
        img.Mutate(c => c.Fill(new Rgba32(200, 200, 220, 255)));
        img.SaveAsPng(path);
        return path;
    }
}