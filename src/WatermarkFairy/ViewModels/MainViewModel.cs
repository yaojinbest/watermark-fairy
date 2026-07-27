using CommunityToolkit.Mvvm.ComponentModel;
using WatermarkFairy.Models;

namespace WatermarkFairy.ViewModels;

/// <summary>
/// 主视图模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private WatermarkConfig _config = new();

    [ObservableProperty]
    private string _statusText = "准备就绪 · M0 立项阶段";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _isProcessing;
}
