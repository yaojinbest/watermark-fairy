using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

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
/// 水印图层基类（v0.1.1 ObservableObject for auto preview 嵌套属性变更订阅）
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextWatermarkLayer), "text")]
[JsonDerivedType(typeof(ImageWatermarkLayer), "image")]
public abstract partial class WatermarkLayer : ObservableObject
{
    [ObservableProperty]
    private WatermarkPosition _position = WatermarkPosition.BottomRight;

    /// <summary>边距（像素）</summary>
    [ObservableProperty]
    private int _margin = 20;

    /// <summary>不透明度（0.0-1.0）</summary>
    [ObservableProperty]
    private float _opacity = 1.0f;

    /// <summary>旋转角度（顺时针，0-360）</summary>
    [ObservableProperty]
    private int _rotation = 0;

    /// <summary>自由拖拽 X 偏移（像素，相对原图左上角，Position=Custom 时生效）</summary>
    [ObservableProperty]
    private int _offsetX = 20;

    /// <summary>自由拖拽 Y 偏移（像素，相对原图左上角，Position=Custom 时生效）</summary>
    [ObservableProperty]
    private int _offsetY = 20;
}

/// <summary>
/// 文字水印图层
/// </summary>
public partial class TextWatermarkLayer : WatermarkLayer
{
    [ObservableProperty]
    private string _text = "© Watermark Fairy";

    [ObservableProperty]
    private string _fontFamily = "Microsoft YaHei";

    [ObservableProperty]
    private float _fontSize = 24f;

    /// <summary>Hex 颜色（如 #FFFFFF）</summary>
    [ObservableProperty]
    private string _color = "#FFFFFF";

    [ObservableProperty]
    private bool _stroke = false;

    [ObservableProperty]
    private string _strokeColor = "#000000";

    [ObservableProperty]
    private float _strokeWidth = 1.0f;

    [ObservableProperty]
    private bool _shadow = false;

    /// <summary>背景色开关（v0.3.1）</summary>
    [ObservableProperty]
    private bool _hasBackground = false;

    /// <summary>背景色 Hex（如 #FFFF00）</summary>
    [ObservableProperty]
    private string _backgroundColor = "#FFFF00";

    /// <summary>背景色 padding（像素，0–20）</summary>
    [ObservableProperty]
    private int _backgroundPadding = 4;

    /// <summary>背景色透明度（0–1，v0.3.3.4 独立于 Opacity 让 bg 可单独控制）</summary>
    [ObservableProperty]
    private float _backgroundOpacity = 1.0f;
}

/// <summary>
/// 图片水印图层（logo）
/// </summary>
public partial class ImageWatermarkLayer : WatermarkLayer
{
    /// <summary>logo 图片路径</summary>
    [ObservableProperty]
    private string _imagePath = "";

    /// <summary>缩放比例（占原图宽度）</summary>
    [ObservableProperty]
    private float _scale = 0.2f;
}