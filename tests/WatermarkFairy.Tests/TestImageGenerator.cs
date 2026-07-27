using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WatermarkFairy.Tests;

/// <summary>
/// 测试图生成器（决策 B：代码生成测试图）
/// </summary>
public static class TestImageGenerator
{
    /// <summary>
    /// 生成纯色测试图
    /// </summary>
    public static string CreateSolid(
        int width = 800,
        int height = 600,
        byte r = 100,
        byte g = 150,
        byte b = 200,
        string format = "png")
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"wf_test_{Guid.NewGuid():N}.{format}");

        using var image = new Image<Rgba32>(Configuration.Default, width, height);
        image.Mutate(c => c.Fill(new Rgba32(r, g, b, 255)));
        image.Save(path);

        return path;
    }

    /// <summary>
    /// 生成渐变测试图（彩虹对角线）
    /// </summary>
    public static string CreateGradient(int width = 800, int height = 600)
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"wf_test_grad_{Guid.NewGuid():N}.png");

        using var image = new Image<Rgba32>(Configuration.Default, width, height);
        image.Mutate(c => c.Fill(new Rgba32(50, 50, 50, 255)));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var r = (byte)(x * 255 / width);
                var g = (byte)(y * 255 / height);
                image[x, y] = new Rgba32(r, g, 128, 255);
            }
        }
        image.Save(path);
        return path;
    }

    /// <summary>
    /// 生成 logo 测试图（白底黑点）
    /// </summary>
    public static string CreateLogo(int width = 200, int height = 60)
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"wf_logo_{Guid.NewGuid():N}.png");

        using var image = new Image<Rgba32>(Configuration.Default, width, height);
        image.Mutate(c => c.Fill(new Rgba32(255, 255, 255, 255)));

        for (int y = 10; y < 50; y++)
        {
            for (int x = 20; x < 180; x++)
            {
                image[x, y] = new Rgba32(0, 0, 0, 255);
            }
        }
        image.Save(path);
        return path;
    }
}
