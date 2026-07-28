using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WatermarkFairy.Models;

/// <summary>
/// 水印配置（M1-2 完整版 + v0.1.1 ObservableObject for auto preview）
/// </summary>
public partial class WatermarkConfig : ObservableObject
{
    [ObservableProperty]
    private string _name = "默认";

    /// <summary>水印图层（按列表顺序叠加）</summary>
    [ObservableProperty]
    private ObservableCollection<WatermarkLayer> _layers = new();

    /// <summary>输出选项</summary>
    [ObservableProperty]
    private OutputOptions _output = new();
}

/// <summary>
/// 输出选项
/// </summary>
public partial class OutputOptions : ObservableObject
{
    /// <summary>输出格式：auto / jpg / png / webp</summary>
    [ObservableProperty]
    private string _format = "auto";

    /// <summary>JPG / WebP 质量（1-100）</summary>
    [ObservableProperty]
    private int _quality = 90;

    /// <summary>是否覆盖已存在文件</summary>
    [ObservableProperty]
    private bool _overwrite = true;
}