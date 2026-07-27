using System.Windows.Controls;
using WatermarkFairy.Models;
using WatermarkFairy.ViewModels;

namespace WatermarkFairy.Views;

/// <summary>
/// 预览面板 UserControl（M1-7）
/// </summary>
public partial class PreviewPane : UserControl
{
    public static readonly DependencyProperty ConfigProperty =
        DependencyProperty.Register(
            nameof(Config),
            typeof(WatermarkConfig),
            typeof(PreviewPane),
            new PropertyMetadata(null, OnConfigChanged));

    public WatermarkConfig? Config
    {
        get => (WatermarkConfig?)GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    public PreviewViewModel ViewModel { get; }

    public PreviewPane()
        : this(new PreviewViewModel())
    {
    }

    public PreviewPane(PreviewViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>
    /// 设置预览源图片（外部调用，文件列表选中时触发）
    /// </summary>
    public void SetSource(string? path)
    {
        ViewModel.SetSource(path);
    }

    private static void OnConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PreviewPane pane && e.NewValue is WatermarkConfig config)
        {
            pane.ViewModel.TriggerPreview(config);
        }
    }
}