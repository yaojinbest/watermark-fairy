using CommunityToolkit.Mvvm.ComponentModel;
using WatermarkFairy.Models;

namespace WatermarkFairy.ViewModels;

/// <summary>
/// 主视图模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private WatermarkConfig _config = new()
    {
        Name = "默认",
        Layers = new List<WatermarkLayer>
        {
            new TextWatermarkLayer
            {
                Text = "© Watermark Fairy",
                FontFamily = "Microsoft YaHei",
                FontSize = 24f,
                Color = "#FFFFFF",
                Position = WatermarkPosition.BottomRight,
                Margin = 20,
                Opacity = 0.8f,
            }
        },
        Output = new OutputOptions
        {
            Format = "auto",
            Quality = 90,
            Overwrite = true,
        }
    };

    [ObservableProperty]
    private string _statusText = "M1-2 ImageProcessor 完整实现阶段";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _isProcessing;
}
