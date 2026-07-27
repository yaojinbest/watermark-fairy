using System.Windows;
using Microsoft.Win32;
using WatermarkFairy.ViewModels;

namespace WatermarkFairy;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnSelectImage(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp"
        };
        if (dlg.ShowDialog() == true)
        {
            _viewModel.StatusText = $"已选：{dlg.FileName}";
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("导出功能 M1 实现", "Watermark Fairy",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
