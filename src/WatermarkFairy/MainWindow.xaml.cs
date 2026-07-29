using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WatermarkFairy.Models;
using WatermarkFairy.ViewModels;

namespace WatermarkFairy;

/// <summary>
/// 主窗口（M1-6 完整化）
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainViewModel ViewModel => _viewModel;

    public MainWindow()
        : this(new MainViewModel())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // 初始化下拉框选项
        InitComboBoxes();
    }

    private void InitComboBoxes()
    {
        // 位置 9 宫格 + Custom
        foreach (WatermarkPosition pos in Enum.GetValues<WatermarkPosition>())
            PositionCombo.Items.Add(pos);

        // 输出格式
        FormatCombo.Items.Add("auto");
        FormatCombo.Items.Add("jpg");
        FormatCombo.Items.Add("png");
        FormatCombo.Items.Add("webp");
    }

    // ============ 文件管理 事件 ============

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp|所有文件|*.*",
            Multiselect = true,
            Title = "选择要添加的图片",
        };
        if (dlg.ShowDialog() != true) return;

        var added = 0;
        foreach (var path in dlg.FileNames)
            if (_viewModel.AddFile(path)) added++;

        _viewModel.StatusText = added > 0
            ? $"已添加 {added} 张图片"
            : "没有新文件被添加";
    }

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "选择图片文件夹",
            Multiselect = false,
        };
        if (dlg.ShowDialog() != true) return;

        var added = _viewModel.AddFolder(dlg.FolderName);
        if (added == 0) _viewModel.StatusText = "文件夹中没有找到支持的图片";
    }

    private void OnClearFiles(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearFiles();
    }

    /// <summary>
    /// ListBox 选中变化 → 同步到 ViewModel.SelectedFile → 触发自动预览
    /// </summary>
    private void OnFileListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectedFile = FileListBox.SelectedItem as string;
    }

    // ============ 应用水印 ============

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (_viewModel.FileList.Count == 0)
        {
            MessageBox.Show("请先添加图片", "Watermark Fairy",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 选输出文件夹
        var dlg = new OpenFolderDialog
        {
            Title = "选择输出文件夹",
        };
        if (dlg.ShowDialog() != true) return;

        var outputFolder = dlg.FolderName;
        await _viewModel.ApplyWatermarkAsync(outputFolder);
    }

    // ============ 拖拽 ============

    private void OnFileListDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileListDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        var added = 0;
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                added += _viewModel.AddFolder(path);
            else if (File.Exists(path))
                if (_viewModel.AddFile(path)) added++;
        }
        _viewModel.StatusText = $"拖拽添加 {added} 项";
    }
}