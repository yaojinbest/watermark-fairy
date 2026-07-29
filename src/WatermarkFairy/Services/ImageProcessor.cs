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
using PointF = SixLabors.ImageSharp.PointF;

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
/// 文字水印走 ImageSharp Drawing.Processing
/// 图片 logo 水印走手动 alpha 合成（避开 DrawImage API 不一致问题）
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

        if (File.Exists(outputPath) && !config.Output.Overwrite)
            throw new IOException($"输出文件已存在且未启用覆盖: {outputPath}");

        var format = ResolveFormat(outputPath, config.Output);

        using var image = await Image.LoadAsync<Rgba32>(inputPath, ct);
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
    /// 应用水印到内存（不落盘）· v0.1.1 预览用
    /// 返回渲染好的 Image&lt;Rgba32&gt;，**调用方负责 Dispose**
    /// </summary>
    public async Task<Image<Rgba32>> ApplyToImageAsync(
        string inputPath,
        WatermarkConfig config,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(config);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("输入图片不存在", inputPath);

        // 不使用 using：返回给调用方，调用方负责 Dispose
        var image = await Image.LoadAsync<Rgba32>(inputPath, ct);
        ApplyLayers(image, config.Layers);
        return image;
    }

    /// <summary>
    /// 加载原图（不应用水印）· v0.2.2 预览浮层用
    /// 返回原图 Image&lt;Rgba32&gt;，**调用方负责 Dispose**
    /// </summary>
    public async Task<Image<Rgba32>> LoadOriginalAsync(
        string inputPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("输入图片不存在", inputPath);

        var image = await Image.LoadAsync<Rgba32>(inputPath, ct);
        return image;
    }

    /// <summary>
    /// 计算水印在画布上的矩形（像素）· v0.2.2 预览浮层用
    /// 预览浮层和导出共用同一算法,口径天然一致
    /// </summary>
    public (float x, float y, float w, float h) CalcBounds(
        int imgW, int imgH, float layerW, float layerH,
        WatermarkPosition position, int margin, int offsetX = 0, int offsetY = 0)
    {
        var (x, y) = CalcPosition(imgW, imgH, layerW, layerH, position, margin, offsetX, offsetY);
        return (x, y, layerW, layerH);
    }

    /// <summary>
    /// 测量文字渲染尺寸（像素）· v0.2.2 预览浮层用
    /// 同一字体同一字号,绘制结果一致
    /// </summary>
    public (float width, float height) MeasureTextSize(string text, string fontFamily, float fontSize)
    {
        if (string.IsNullOrEmpty(text)) return (0, 0);
        var ff = ResolveFontFamily(fontFamily);
        var font = ff.CreateFont(fontSize);
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        return (size.Width, size.Height);
    }

    /// <summary>
    /// 应用所有图层（按顺序叠加）
    /// </summary>
    public void ApplyLayers(Image<Rgba32> image, IReadOnlyList<WatermarkLayer> layers)
    {
        if (layers is null || layers.Count == 0) return;

        // 文字图层走 Mutate（DrawText API 稳定）
        // 图片图层走手动合成（DrawImage API 不稳）
        foreach (var layer in layers)
        {
            switch (layer)
            {
                case TextWatermarkLayer text:
                    image.Mutate(ctx => ApplyTextLayer(ctx, image, text));
                    break;
                case ImageWatermarkLayer img:
                    ApplyImageLayer(image, img);
                    break;
            }
        }
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
            layer.Position, layer.Margin, layer.OffsetX, layer.OffsetY);

        var opts = new RichTextOptions(font)
        {
            Origin = new PointF(x, y),
        };

        ctx.DrawText(opts, layer.Text, color);
    }

    /// <summary>
    /// 图片 logo 水印：手动 alpha 合成
    /// 避开 SixLabors.ImageSharp 3.x 的 DrawImage API 签名不一致问题
    /// </summary>
    private void ApplyImageLayer(Image<Rgba32> baseImage, ImageWatermarkLayer layer)
    {
        if (string.IsNullOrWhiteSpace(layer.ImagePath))
            throw new ArgumentException("水印图片路径不能为空", nameof(layer));

        if (!File.Exists(layer.ImagePath))
            throw new FileNotFoundException("水印图片不存在", layer.ImagePath);

        using var logo = Image.Load<Rgba32>(layer.ImagePath);

        var targetW = Math.Max(1, (int)(baseImage.Width * layer.Scale));
        var ratio = logo.Height / (float)logo.Width;
        var targetH = Math.Max(1, (int)(targetW * ratio));

        logo.Mutate(c => c.Resize(targetW, targetH));

        var (x, y) = CalcPosition(
            baseImage.Width, baseImage.Height,
            targetW, targetH,
            layer.Position, layer.Margin, layer.OffsetX, layer.OffsetY);

        int dx = (int)x;
        int dy = (int)y;
        float opacity = Math.Clamp(layer.Opacity, 0f, 1f);

        // 边界裁剪
        int startX = Math.Max(0, dx);
        int startY = Math.Max(0, dy);
        int endX = Math.Min(baseImage.Width, dx + targetW);
        int endY = Math.Min(baseImage.Height, dy + targetH);

        for (int py = startY; py < endY; py++)
        {
            int sy = py - dy;
            for (int px = startX; px < endX; px++)
            {
                int sx = px - dx;

                var srcPixel = logo[sx, sy];
                var destPixel = baseImage[px, py];

                // 标准 Porter-Duff "over" 合成
                float sa = (srcPixel.A / 255f) * opacity;
                float da = destPixel.A / 255f;
                float oa = sa + da * (1 - sa);
                if (oa < 0.001f) continue;

                byte r = (byte)Math.Clamp(
                    (srcPixel.R * sa + destPixel.R * da * (1 - sa)) / oa, 0, 255);
                byte g = (byte)Math.Clamp(
                    (srcPixel.G * sa + destPixel.G * da * (1 - sa)) / oa, 0, 255);
                byte b = (byte)Math.Clamp(
                    (srcPixel.B * sa + destPixel.B * da * (1 - sa)) / oa, 0, 255);
                byte a = (byte)Math.Clamp(oa * 255, 0, 255);

                baseImage[px, py] = new Rgba32(r, g, b, a);
            }
        }
    }

    private static (float x, float y) CalcPosition(
        int imgW, int imgH, float layerW, float layerH,
        WatermarkPosition position, int margin, int offsetX = 0, int offsetY = 0)
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
            WatermarkPosition.Custom => (offsetX, offsetY),
            _ => (margin, margin),
        };
    }

    /// <summary>
    /// 字体解析：思源黑体（embedded）> 用户指定 > 系统 fallback 链 > 系统首套
    /// </summary>
    private static FontFamily ResolveFontFamily(string name)
    {
        // 1. 思源黑体（embedded 优先，M1-2.1 patch）
        // FontCollection.Families 是 IEnumerable<FontFamily>，无 Count 属性 / [] 索引
        var shsCollection = FontLoader.Collection;
        if (shsCollection is { } collection && collection.Families.Any())
        {
            return collection.Families.First();
        }

        // 2. 用户指定字体
        if (TryGetFontFamily(name) is { } family)
            return family;

        // 3. Fallback 链（系统字体）
        foreach (var fallback in new[] { "Microsoft YaHei", "Microsoft YaHei UI",
                                          "SimHei", "SimSun", "Segoe UI",
                                          "DejaVu Sans", "Arial" })
        {
            if (TryGetFontFamily(fallback) is { } f)
                return f;
        }

        // 4. 末位 fallback：系统首套字体
        var families = SystemFonts.Families;
        if (!families.Any())
            throw new InvalidOperationException("系统无任何可用字体");
        return families.First();
    }

    /// <summary>
    /// 安全获取字体：捕获 FontFamilyNotFoundException
    /// （SixLabors.Fonts 2.0.4 的 SystemFonts.Get 不返回 null，直接抛异常）
    /// </summary>
    private static FontFamily? TryGetFontFamily(string name)
    {
        try
        {
            return SystemFonts.Get(name);
        }
        catch (FontFamilyNotFoundException)
        {
            return null;
        }
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
