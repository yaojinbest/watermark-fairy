namespace WatermarkFairy.Models;

/// <summary>
/// 图片裁剪矩形（v0.3.2，per-image 原图像素坐标）
/// </summary>
public record CropRect(int X, int Y, int Width, int Height);