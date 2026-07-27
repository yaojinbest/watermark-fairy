using System.Text.Json.Serialization;

namespace WatermarkFairy.Models;

/// <summary>
/// 水印位置（9 宫格 + 自由拖拽）
/// </summary>
public enum WatermarkPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    Custom,  // 自由拖拽（M1-7 预览阶段支持）
}

/// <summary>
/// 水印图层基类
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextWatermarkLayer), "text")]
[JsonDerivedType(typeof(ImageWatermarkLayer), "image")]
public abstract class WatermarkLayer
{
    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;

    /// <summary>边距（像素）</summary>
    public int Margin { get; set; } = 20;

    /// <summary>不透明度（0.0-1.0）</summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>旋转角度（顺时针，0-360）</summary>
    public int Rotation { get; set; } = 0;
}

/// <summary>
/// 文字水印图层
/// </summary>
public class TextWatermarkLayer : WatermarkLayer
{
    public string Text { get; set; } = "© Watermark Fairy";

    public string FontFamily { get; set; } = "Microsoft YaHei";

    public float FontSize { get; set; } = 24f;

    /// <summary>Hex 颜色（如 #FFFFFF）</summary>
    public string Color { get; set; } = "#FFFFFF";

    public bool Stroke { get; set; } = false;

    public string StrokeColor { get; set; } = "#000000";

    public float StrokeWidth { get; set; } = 1.0f;

    public bool Shadow { get; set; } = false;
}

/// <summary>
/// 图片水印图层（logo）
/// </summary>
public class ImageWatermarkLayer : WatermarkLayer
{
    /// <summary>logo 图片路径</summary>
    public string ImagePath { get; set; } = "";

    /// <summary>缩放比例（占原图宽度）</summary>
    public float Scale { get; set; } = 0.2f;
}
