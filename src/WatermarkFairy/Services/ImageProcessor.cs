using System.Drawing;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 水印应用结果
/// </summary>
public sealed record WatermarkResult(
    string InputPath,
    string OutputPath,
    string Format,
    int Width,
    int Height,
    long FileSize);

/// <summary>
/// 图像处理服务（M1-2 完整实现）
/// 支持文字水印 + 图片 logo 水印 + 9 宫格位置 + 输出格式转换
/// </summary>
public class ImageProcessor
{
    /// <summary>
    /// 应用水印并保存到输出路径
    /// </summary>
    public async Task<WatermarkResult> ApplyAsync(
        string inputPath,
        string outputPath,
        WatermarkConfig config,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(config);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("输入图片不存在", inputPath);

        // 检查覆盖
        if (File.Exists(outputPath) && !config.Output.Overwrite)
            throw new IOException($"输出文件已存在且未启用覆盖: {outputPath}");

        var format = ResolveFormat(outputPath, config.Output);

        using var image = await Image.LoadAsync(inputPath, ct);
        ApplyLayers(image, config.Layers);
        await SaveImageAsync(image, outputPath, format, config.Output.Quality, ct);

        return new WatermarkResult(
            inputPath,
            outputPath,
            format,
            image.Width,
            image.Height,
            new FileInfo(outputPath).Length);
    }

    /// <summary>
    /// 应用所有图层（按顺序叠加）
    /// </summary>
    public void ApplyLayers(Image image, IReadOnlyList<WatermarkLayer> layers)
    {
        if (layers is null || layers.Count == 0) return;

        image.Mutate(ctx =>
        {
            foreach (var layer in layers)
            {
                switch (layer)
                {
                    case TextWatermarkLayer text:
                        ApplyTextLayer(ctx, image, text);
                        break;
                    case ImageWatermarkLayer img:
                        ApplyImageLayer(ctx, image, img);
                        break;
                }
            }
        });
    }

    private void ApplyTextLayer(IImageProcessingContext ctx, Image image, TextWatermarkLayer layer)
    {
        var color = ParseColor(layer.Color, layer.Opacity);
        var fontFamily = ResolveFontFamily(layer.FontFamily);
        var font = fontFamily.CreateFont(layer.FontSize);

        var textSize = TextMeasurer.MeasureSize(layer.Text, new TextOptions(font));
        var (x, y) = CalcPosition(
            image.Width, image.Height,
            textSize.Width, textSize.Height,
            layer.Position, layer.Margin);

        var opts = new RichTextOptions(font)
        {
            Origin = new PointF(x, y),
        };

        ctx.DrawText(opts, layer.Text, color);
    }

    private void ApplyImageLayer(IImageProcessingContext ctx, Image image, ImageWatermarkLayer layer)
    {
        if (string.IsNullOrWhiteSpace(layer.ImagePath))
            throw new ArgumentException("水印图片路径不能为空", nameof(layer));

        if (!File.Exists(layer.ImagePath))
            throw new FileNotFoundException("水印图片不存在", layer.ImagePath);

        using var logo = Image.Load(layer.ImagePath);

        // 按 Scale 计算目标尺寸（保持 logo 纵横比）
        var targetW = Math.Max(1, (int)(image.Width * layer.Scale));
        var ratio = logo.Height / (float)logo.Width;
        var targetH = Math.Max(1, (int)(targetW * ratio));

        logo.Mutate(c => c.Resize(targetW, targetH));

        var (x, y) = CalcPosition(
            image.Width, image.Height,
            targetW, targetH,
            layer.Position, layer.Margin);

        ctx.DrawImage(logo, new Point(x, y), layer.Opacity);
    }

    private static (float x, float y) CalcPosition(
        int imgW, int imgH, float layerW, float layerH,
        WatermarkPosition position, int margin)
    {
        return position switch
        {
            WatermarkPosition.TopLeft => (margin, margin),
            WatermarkPosition.TopCenter => ((imgW - layerW) / 2, margin),
            WatermarkPosition.TopRight => (imgW - layerW - margin, margin),
            WatermarkPosition.MiddleLeft => (margin, (imgH - layerH) / 2),
            WatermarkPosition.MiddleCenter => ((imgW - layerW) / 2, (imgH - layerH) / 2),
            WatermarkPosition.MiddleRight => (imgW - layerW - margin, (imgH - layerH) / 2),
            WatermarkPosition.BottomLeft => (margin, imgH - layerH - margin),
            WatermarkPosition.BottomCenter => ((imgW - layerW) / 2, imgH - layerH - margin),
            WatermarkPosition.BottomRight => (imgW - layerW - margin, imgH - layerH - margin),
            WatermarkPosition.Custom => (margin, margin),  // M1-7 预览阶段覆盖
            _ => (margin, margin),
        };
    }

    /// <summary>
    /// 字体解析：优先指定字体，fallback 链
    /// M1-2.1 patch 计划：打包思源黑体作为优先 fallback
    /// </summary>
    private static FontFamily ResolveFontFamily(string name)
    {
        // 1. 尝试精确名
        if (SystemFonts.Get(name) is { } family)
            return family;

        // 2. Fallback 链（Windows 字体优先）
        foreach (var fallback in new[] { "Microsoft YaHei", "Microsoft YaHei UI",
                                          "SimHei", "SimSun", "Segoe UI",
                                          "DejaVu Sans", "Arial" })
        {
            if (SystemFonts.Get(fallback) is { } f)
                return f;
        }

        // 3. 兜底：取系统首套字体
        return SystemFonts.Families.First();
    }

    private static string ResolveFormat(string outputPath, OutputOptions options)
    {
        if (!string.Equals(options.Format, "auto", StringComparison.OrdinalIgnoreCase))
            return options.Format.ToLowerInvariant();

        var ext = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "jpg" or "jpeg" => "jpg",
            "png" => "png",
            "webp" => "webp",
            _ => throw new NotSupportedException(
                $"无法从扩展名识别输出格式: {ext}（请显式指定 Output.Format）"),
        };
    }

    private static async Task SaveImageAsync(
        Image image, string outputPath, string format, int quality, CancellationToken ct)
    {
        switch (format)
        {
            case "jpg":
                await image.SaveAsync(outputPath,
                    new JpegEncoder { Quality = Math.Clamp(quality, 1, 100) }, ct);
                break;
            case "png":
                await image.SaveAsync(outputPath, new PngEncoder(), ct);
                break;
            case "webp":
                await image.SaveAsync(outputPath,
                    new WebpEncoder { Quality = Math.Clamp(quality, 1, 100) }, ct);
                break;
            default:
                throw new NotSupportedException($"不支持的输出格式: {format}");
        }
    }

    private static Rgba32 ParseColor(string hex, float opacity)
    {
        var c = ColorTranslator.FromHtml(hex);
        var alpha = (byte)(255 * Math.Clamp(opacity, 0f, 1f));
        return new Rgba32(c.R, c.G, c.B, alpha);
    }
}
