using System.IO;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 预览渲染服务（M1-7）
/// 把 WatermarkConfig 应用到原图，输出 WPF BitmapSource
/// </summary>
public class PreviewRenderer
{
    /// <summary>
    /// 渲染预览图：原图 + WatermarkConfig → BitmapSource (PNG format)
    /// </summary>
    /// <param name="sourcePath">原图路径</param>
    /// <param name="config">水印配置</param>
    /// <param name="previewMaxSize">预览图最大边（默认 800）</param>
    /// <param name="ct">取消 token</param>
    /// <returns>WPF BitmapSource（UI Image 控件可直接显示）</returns>
    public async Task<BitmapSource?> RenderAsync(
        string sourcePath,
        WatermarkConfig config,
        CropRect? cropRect = null,
        int previewMaxSize = 800,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return null;
        ArgumentNullException.ThrowIfNull(config);

        // 加载原图（异步 I/O）
        using var image = await Image.LoadAsync<Rgba32>(sourcePath, ct);

        // v0.3.2 裁剪（先 crop 原图，再 resize 到预览尺寸）
        if (cropRect is { } cr)
            image.Mutate(ctx => ctx.Crop(new Rectangle(cr.X, cr.Y, cr.Width, cr.Height)));

        // 缩放到预览尺寸（避免 UI 渲染大图卡顿）
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(previewMaxSize, previewMaxSize),
            Mode = ResizeMode.Max,
        }));

        // 应用水印
        var processor = new ImageProcessor();
        // ImageProcessor.ApplyAsync 需要文件 I/O，预览用 ApplyLayers 走内存
        processor.ApplyLayers(image, config.Layers);

        // 转 BitmapSource
        return ToBitmapSource(image);
    }

    /// <summary>
    /// 渲染内存中的图片（不写文件）
    /// </summary>
    public BitmapSource? RenderInMemory(
        Image<Rgba32> image,
        WatermarkConfig config)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(config);

        var processor = new ImageProcessor();
        processor.ApplyLayers(image, config.Layers);
        return ToBitmapSource(image);
    }

    private static BitmapSource? ToBitmapSource(Image<Rgba32> image)
    {
        try
        {
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            ms.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;  // 立即解码（关闭 stream 后仍可用）
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();  // 跨线程访问安全
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}