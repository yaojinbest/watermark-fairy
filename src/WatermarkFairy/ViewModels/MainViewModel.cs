using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatermarkFairy.Models;
using WatermarkFairy.Services;

namespace WatermarkFairy.ViewModels;

/// <summary>
/// 主视图模型（M1-6 完整化）
/// 左控制 + 中预览 + 右文件列表 + 底部状态
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ImageProcessor _processor;
    private readonly TemplateStore? _templateStore;

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
    private string _statusText = "就绪 · M1-6 阶段";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _outputFolder = "";

    /// <summary>待处理文件列表（ObservableCollection 适配 WPF 双向绑定）</summary>
    public ObservableCollection<string> FileList { get; } = new();

    public MainViewModel()
        : this(new ImageProcessor(), null)
    {
    }

    public MainViewModel(ImageProcessor processor, TemplateStore? templateStore)
    {
        _processor = processor;
        _templateStore = templateStore;
    }

    /// <summary>
    /// 是否有待处理文件
    /// </summary>
    public bool HasFiles => FileList.Count > 0;

    /// <summary>
    /// 文件数（用于 UI 绑定）
    /// </summary>
    public int FileCount => FileList.Count;

    // ============ 文件管理 ============

    /// <summary>
    /// 添加单个文件
    /// </summary>
    public bool AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        if (FileList.Contains(path)) return false;
        FileList.Add(path);
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(HasFiles));
        StatusText = $"已添加 {Path.GetFileName(path)}（共 {FileList.Count}）";
        return true;
    }

    /// <summary>
    /// 添加文件夹（递归扫描图片）
    /// </summary>
    public int AddFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return 0;

        var added = 0;
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff" };
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (!extensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
            if (FileList.Contains(file)) continue;
            FileList.Add(file);
            added++;
        }
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(HasFiles));
        StatusText = added > 0
            ? $"从文件夹添加 {added} 张图片（当前共 {FileList.Count}）"
            : "文件夹中没有找到支持的图片";
        return added;
    }

    /// <summary>
    /// 移除文件
    /// </summary>
    public bool RemoveFile(string path)
    {
        var removed = FileList.Remove(path);
        if (removed)
        {
            OnPropertyChanged(nameof(FileCount));
            OnPropertyChanged(nameof(HasFiles));
            StatusText = $"已移除（剩余 {FileList.Count}）";
        }
        return removed;
    }

    /// <summary>
    /// 清空文件列表
    /// </summary>
    public void ClearFiles()
    {
        if (FileList.Count == 0) return;
        FileList.Clear();
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(HasFiles));
        StatusText = "已清空文件列表";
    }

    // ============ 模板集成 ============

    /// <summary>
    /// 加载模板（替换 Config）
    /// </summary>
    public bool LoadTemplate(TemplateRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Config = record.Config;
        StatusText = $"已加载模板 {record.Name}";
        return true;
    }

    // ============ 应用水印 ============

    /// <summary>
    /// 应用水印到所有文件
    /// </summary>
    public async Task ApplyWatermarkAsync(string outputFolder, CancellationToken ct = default)
    {
        if (FileList.Count == 0)
        {
            StatusText = "请先添加图片";
            return;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            outputFolder = Path.Combine(Path.GetTempPath(), "wf_output");
        }
        Directory.CreateDirectory(outputFolder);

        IsProcessing = true;
        ProgressPercent = 0;
        var snapshot = FileList.ToList();
        var total = snapshot.Count;
        StatusText = $"开始处理 {total} 张图片...";

        try
        {
            var processed = 0;
            var failed = 0;
            foreach (var file in snapshot)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var outputPath = Path.Combine(
                        outputFolder,
                        $"{Path.GetFileNameWithoutExtension(file)}_watermarked.jpg");
                    await _processor.ApplyAsync(file, outputPath, Config, ct);
                    processed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    System.Diagnostics.Debug.WriteLine($"Failed: {file}: {ex.Message}");
                }
                ProgressPercent = (int)((processed + failed) * 100.0 / total);
                StatusText = $"已处理 {processed}/{total}（失败 {failed}）";
            }
            StatusText = failed == 0
                ? $"完成！共处理 {total} 张图片到 {outputFolder}"
                : $"完成！{processed} 成功 / {failed} 失败 → {outputFolder}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        finally
        {
            IsProcessing = false;
            ProgressPercent = 0;
        }
    }
}