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
/// 图像处理服务（骨架阶段：占位实现，M1 完整实现）
/// </summary>
public class ImageProcessor
{
    /// <summary>
    /// 应用文字水印并导出
    /// </summary>
    public async Task ApplyTextWatermarkAsync(
        string inputPath,
        string outputPath,
        WatermarkConfig config,
        CancellationToken ct = default)
    {
        // 骨架阶段：只做文件复制，不做真实水印渲染
        // M1 阶段完整实现 SixLabors.ImageSharp 文字水印
        await Task.Run(() =>
        {
            var img = Image.Load(inputPath);
            // TODO M1: DrawTextWatermark(img, config)
            img.Save(outputPath);
        }, ct);
    }

    private void DrawTextWatermark(Image image, WatermarkConfig config)
    {
        // TODO M1: SixLabors.ImageSharp.Drawing 完整文字水印实现
        // 参考：https://docs.sixlabors.com/articles/imagesharp/drawingtext.html
        throw new NotImplementedException("M1 阶段实现");
    }

    private (float x, float y) CalcPosition(int imgW, int imgH, float textW, float textH, WatermarkConfig config)
    {
        int m = config.Margin;
        var pos = config.Position;
        return pos switch
        {
            WatermarkPosition.TopLeft => (m, m),
            WatermarkPosition.TopCenter => ((imgW - textW) / 2, m),
            WatermarkPosition.TopRight => (imgW - textW - m, m),
            WatermarkPosition.MiddleLeft => (m, (imgH - textH) / 2),
            WatermarkPosition.MiddleCenter => ((imgW - textW) / 2, (imgH - textH) / 2),
            WatermarkPosition.MiddleRight => (imgW - textW - m, (imgH - textH) / 2),
            WatermarkPosition.BottomLeft => (m, imgH - textH - m),
            WatermarkPosition.BottomCenter => ((imgW - textW) / 2, imgH - textH - m),
            WatermarkPosition.BottomRight => (imgW - textW - m, imgH - textH - m),
            _ => (m, m),
        };
    }
}
