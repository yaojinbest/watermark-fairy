namespace WatermarkFairy.Models;

/// <summary>
/// 水印配置（M1-2 完整版：多层 + 输出选项）
/// </summary>
public class WatermarkConfig
{
    public string Name { get; set; } = "默认";

    /// <summary>水印图层（按列表顺序叠加）</summary>
    public List<WatermarkLayer> Layers { get; set; } = new();

    /// <summary>输出选项</summary>
    public OutputOptions Output { get; set; } = new();
}

/// <summary>
/// 输出选项
/// </summary>
public class OutputOptions
{
    /// <summary>输出格式：auto / jpg / png / webp</summary>
    public string Format { get; set; } = "auto";

    /// <summary>JPG / WebP 质量（1-100）</summary>
    public int Quality { get; set; } = 90;

    /// <summary>是否覆盖已存在文件</summary>
    public bool Overwrite { get; set; } = true;
}
