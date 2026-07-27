namespace WatermarkFairy.Models;

/// <summary>
/// 水印配置（MVP 范围 B）
/// </summary>
public class WatermarkConfig
{
    public string Name { get; set; } = "默认";

    public string Text { get; set; } = "© Watermark Fairy";

    public string FontFamily { get; set; } = "Microsoft YaHei";

    public double FontSize { get; set; } = 24;

    public string Color { get; set; } = "#FFFFFF";

    public double Opacity { get; set; } = 0.8;

    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;

    public int Margin { get; set; } = 20;

    public bool Stroke { get; set; }

    public string StrokeColor { get; set; } = "#000000";

    public bool Shadow { get; set; }
}

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
    Custom,  // 自由拖拽
}
