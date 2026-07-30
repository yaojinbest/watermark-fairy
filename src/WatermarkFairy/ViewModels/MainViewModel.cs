using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using WB = System.Windows.Media;

namespace WatermarkFairy.ViewModels;

/// <summary>
/// 主视图模型（M1-6 完整化 + v0.1.1 Preview/Export/Font/Color）
/// 左控制 + 中预览 + 右文件列表 + 底部状态 + 命令
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ImageProcessor _processor;
    private readonly TemplateStore? _templateStore;

    // v0.1.1 auto-preview debounce 令牌
    private CancellationTokenSource? _previewCts;

    [ObservableProperty]
    private WatermarkConfig _config = new()
    {
        Name = "默认",
        Layers = new ObservableCollection<WatermarkLayer>
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
    private string _statusText = "就绪 · M3 阶段";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportWatermarkCommand))]
    private string _outputFolder = "";

    /// <summary>待处理文件列表（ObservableCollection 适配 WPF 双向绑定）</summary>
    public ObservableCollection<string> FileList { get; } = new();

    /// <summary>v0.1.1 预览图像（绑定到 MainWindow 中部 Image.Source）</summary>
    [ObservableProperty]
    private BitmapImage? _previewImageSource;

    /// <summary>当前选中的待处理文件（ListBox 选中项），null = 未选中</summary>
    [ObservableProperty]
    private string? _selectedFile;

    /// <summary>选中变化 → 同步当前裁剪 + 触发预览重渲（auto-preview 链路）</summary>
    partial void OnSelectedFileChanged(string? value)
    {
        CurrentCropRect = value is null ? null : GetCropRect(value);
        ClearCropCommand.NotifyCanExecuteChanged();
        TriggerAutoPreview();
    }

    /// <summary>当前裁剪变化 → 同步 ClearCropCommand 可用状态</summary>
    partial void OnCurrentCropRectChanged(CropRect? value) =>
        ClearCropCommand.NotifyCanExecuteChanged();

    /// <summary>v0.2.2 当前预览原图宽度（像素，RenderTransform 缩放基准）</summary>
    [ObservableProperty]
    private int _originalImageWidth;

    /// <summary>v0.2.2 当前预览原图高度（像素）</summary>
    [ObservableProperty]
    private int _originalImageHeight;

    /// <summary>v0.2.2 水印浮层 X 坐标（原始像素，9 宫格或 Custom 都用这个）</summary>
    [ObservableProperty]
    private double _watermarkLeft;

    /// <summary>v0.2.2 水印浮层 Y 坐标（原始像素）</summary>
    [ObservableProperty]
    private double _watermarkTop;

    /// <summary>v0.2.3 当前水印渲染尺寸宽（像素，用于拖拽 clamp 到图片区内）</summary>
    [ObservableProperty]
    private double _watermarkLayerWidth;

    /// <summary>v0.2.3 当前水印渲染尺寸高（像素）</summary>
    [ObservableProperty]
    private double _watermarkLayerHeight;

    // ============ v0.3.2 裁剪（per-image）============

    /// <summary>每张图片的裁剪矩形字典（per-image 存储）</summary>
    private readonly Dictionary<string, CropRect> _cropRects = new();

    /// <summary>当前选中文件的裁剪矩形（null = 不裁剪）</summary>
    [ObservableProperty]
    private CropRect? _currentCropRect;

    /// <summary>是否启用裁剪模式（按钮开关）</summary>
    [ObservableProperty]
    private bool _isCropEnabled;

    /// <summary>拖框中（避免拖框过程触发 auto-preview）</summary>
    [ObservableProperty]
    private bool _isCropDragging;

    /// <summary>v0.1.1 系统字体列表（绑定到字体 ComboBox）</summary>
    public IReadOnlyList<string> SystemFonts { get; } = WB.Fonts.SystemFontFamilies
        .Select(f => f.Source)
        .OrderBy(n => n)
        .ToList();

    /// <summary>v0.1.1 16 色预设色（绑定到颜色 WrapPanel）</summary>
    public IReadOnlyList<string> PresetColors { get; } = new[]
    {
        "#000000", "#FFFFFF", "#FF0000", "#00FF00", "#0000FF", "#FFFF00",
        "#00FFFF", "#FF00FF", "#FF8800", "#FF00AA", "#8800FF", "#00AAFF",
        "#888888", "#444444", "#CCCCCC", "#8B4513"
    };

    public MainViewModel()
        : this(new ImageProcessor(), null)
    {
    }

    public MainViewModel(ImageProcessor processor, TemplateStore? templateStore = null)
    {
        _processor = processor;
        _templateStore = templateStore;

        // v0.1.1 auto-preview: 订阅 Config + Layers + Output + FileList 变化
        Config.PropertyChanged += OnConfigOrOutputChanged;
        Config.Output.PropertyChanged += OnConfigOrOutputChanged;
        HookLayerPropertyChanged();
        FileList.CollectionChanged += OnFileListChanged;
    }

    /// <summary>
    /// 是否有待处理文件
    /// </summary>
    public bool HasFiles => FileList.Count > 0;

    /// <summary>
    /// 文件数（用于 UI 绑定）
    /// </summary>
    public int FileCount => FileList.Count;

    /// <summary>v0.1.1 导出按钮 CanExecute：有文件 + 已选输出目录</summary>
    public bool CanExport => HasFiles && !string.IsNullOrWhiteSpace(OutputFolder);

    // ============ ICommand（v0.1.1 Preview/Export）============

    /// <summary>v0.1.1 输出目录选择（OpenFolderDialog）</summary>
    [RelayCommand]
    private void PickOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择输出目录",
            InitialDirectory = string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder
        };
        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
            StatusText = $"输出目录已设置：{OutputFolder}";
        }
    }

    /// <summary>v0.1.1 导出水印到 OutputFolder</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportWatermarkAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            StatusText = "请先选择输出目录";
            return;
        }
        await ApplyWatermarkAsync(OutputFolder);
    }

    /// <summary>v0.1.1 选中预设色（WrapPanel Color swatches）</summary>
    [RelayCommand]
    private void PickPresetColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        if (Config.Layers.Count == 0) return;
        // pattern variable 捕获类型，确保编译器收窄为 TextWatermarkLayer（基类无 Color 属性）
        if (Config.Layers[0] is not TextWatermarkLayer textLayer) return;
        textLayer.Color = hex;
    }

    /// <summary>v0.3.3 选中描边预设色（8×2 swatches，仅 Stroke=true 时显示）</summary>
    [RelayCommand]
    private void PickPresetStrokeColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        if (Config.Layers.Count == 0) return;
        if (Config.Layers[0] is not TextWatermarkLayer textLayer) return;
        textLayer.StrokeColor = hex;
    }

    /// <summary>v0.3.3 选中背景色预设色（8×2 swatches，仅 HasBackground=true 时显示）</summary>
    [RelayCommand]
    private void PickPresetBackgroundColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        if (Config.Layers.Count == 0) return;
        if (Config.Layers[0] is not TextWatermarkLayer textLayer) return;
        textLayer.BackgroundColor = hex;
    }

    // ============ v0.1.1 auto-preview 订阅 ============

    private void OnConfigOrOutputChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Config 整体替换（如 LoadTemplate）或 Output 任一字段变化 → 触发预览
        if (sender == Config && e.PropertyName == nameof(WatermarkConfig.Layers))
        {
            // Layers 引用变化时重新挂订阅
            HookLayerPropertyChanged();
        }

        // v0.3.3.6 layer 属性变化 → 通知 PreviewBackgroundBrush + push undo snapshot
        if (sender is TextWatermarkLayer)
        {
            OnPropertyChanged(nameof(PreviewBackgroundBrush));
            PushUndoSnapshot();
        }

        TriggerAutoPreview();
        RecomputeWatermarkBounds();  // v0.2.2 水印浮层位置随 Config 变化
    }

    /// <summary>
    /// v0.2.2 重算水印浮层位置（基于 OriginalImageWidth/Height + 当前 Layer 0）
    /// 预览浮层和 ImageProcessor.CalcBounds 共用同一算法,口径一致
    /// </summary>
    private void RecomputeWatermarkBounds()
    {
        if (OriginalImageWidth <= 0 || OriginalImageHeight <= 0) return;
        if (Config.Layers.Count == 0) return;
        if (Config.Layers[0] is not TextWatermarkLayer textLayer) return;

        var (layerW, layerH) = _processor.MeasureTextLayerSize(textLayer);
        var (x, y, _, _) = _processor.CalcBounds(
            OriginalImageWidth, OriginalImageHeight,
            layerW, layerH,
            textLayer.Position, textLayer.Margin, textLayer.OffsetX, textLayer.OffsetY);
        WatermarkLayerWidth = layerW;   // v0.2.3 暴露给拖拽 clamp 用
        WatermarkLayerHeight = layerH;
        WatermarkLeft = x;
        WatermarkTop = y;
    }

    /// <summary>
    /// v0.2.3 设置水印位置,限制在图片区内(0 <= left <= imgW - layerW, 0 <= top <= imgH - layerH)
    /// 拖拽时调,Margin=0 因为水印边缘贴图边不算出界
    /// </summary>
    public void SetWatermarkPosition(double left, double top)
    {
        var maxLeft = Math.Max(0, OriginalImageWidth - WatermarkLayerWidth);
        var maxTop = Math.Max(0, OriginalImageHeight - WatermarkLayerHeight);
        WatermarkLeft = Math.Clamp(left, 0, maxLeft);
        WatermarkTop = Math.Clamp(top, 0, maxTop);
    }

    /// <summary>
    /// v0.2.2.2 拖拽结束 → 同步当前 WatermarkLeft/Top 到 Config (Position=Custom + OffsetX/Y)
    /// 拖拽期间只改浮层 Canvas.Left/Top(实时跟随),松手才写 Config(触发 ImageProcessor 重渲)
    ///
    /// BUG fix v2 (2026-07-29 21:53 owner 反馈): 之前 v0.2.2.1 只换了顺序没解决根本问题
    /// —— set OffsetX 触发 RecomputeWatermarkBounds,此时 Position 还是默认 BottomRight,
    /// CalcBounds 走 BottomRight 分支忽略 OffsetY → WatermarkTop 被重置为 BottomRight Y →
    /// 后续 step 读 WatermarkTop = 重置值(不是用户 Y)→ OffsetY 被写成 BottomRight Y → 上下回弹。
    /// 水平 OK 是因为 BottomRight 分支同时把 WatermarkLeft 也重置了,但被覆盖时机不同。
    /// 彻底 fix: 临时取消订阅 + 局部变量读,任何中间 PropertyChanged 都不触发重算。
    /// </summary>
    public void SyncWatermarkToConfig()
    {
        if (Config.Layers.Count == 0) return;
        var layer = Config.Layers[0];

        // 1) 先读后写（局部变量避免被任何中间重置影响）
        var left = (int)Math.Round(WatermarkLeft);
        var top = (int)Math.Round(WatermarkTop);

        // 2) 临时取消订阅（避免 set OffsetX/Y 触发 RecomputeWatermarkBounds 把 WatermarkLeft/Top 重置到底层 Position 的位置）
        layer.PropertyChanged -= OnConfigOrOutputChanged;
        try
        {
            layer.Position = WatermarkPosition.Custom;
            layer.OffsetX = left;
            layer.OffsetY = top;
        }
        finally
        {
            // 3) 恢复订阅,手动触发一次重算（订阅期间没触发）
            layer.PropertyChanged += OnConfigOrOutputChanged;
            RecomputeWatermarkBounds();
        }
    }

    private void HookLayerPropertyChanged()
    {
        // 简单实现：仅订阅 Layers[0]（MVP UI 仅编辑第 1 层）
        if (Config.Layers.Count > 0 && Config.Layers[0] is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += OnConfigOrOutputChanged;
        }
    }

    private void OnFileListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // v0.3.3.3 文件列表变化时自动选中第一个（让 crop / 单文件预览等依赖 SelectedFile 的功能默认可用）
        if (SelectedFile is null && FileList.Count > 0)
        {
            SelectedFile = FileList[0];
        }

        TriggerAutoPreview();
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(CanExport));
    }

    private void TriggerAutoPreview()
    {
        // 取消上一次未完成的预览 → debounce 150ms 后重新生成
        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        _ = DebouncedPreviewAsync(cts.Token);
    }

    private async Task DebouncedPreviewAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct);
            await RegeneratePreviewAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // 被新的预览请求取消
        }
    }

    private async Task RegeneratePreviewAsync(CancellationToken ct)
    {
        // 优先级：选中文件 > 第一个文件 > 无文件
        string? firstFile = SelectedFile;
        if (string.IsNullOrEmpty(firstFile) && FileList.Count > 0)
        {
            firstFile = FileList[0];
        }
        if (string.IsNullOrEmpty(firstFile))
        {
            PreviewImageSource = null;
            return;
        }

        if (!File.Exists(firstFile))
        {
            StatusText = $"文件不存在：{firstFile}";
            return;
        }

        try
        {
            // v0.2.2 加载原图（不烘焙水印，水印由 Canvas 浮层渲染）
            using var image = await _processor.LoadOriginalAsync(firstFile, GetCropRect(firstFile), ct);
            OriginalImageWidth = image.Width;
            OriginalImageHeight = image.Height;
            RecomputeWatermarkBounds();

            using var ms = new MemoryStream();
            await image.SaveAsync(ms, new JpegEncoder { Quality = 80 }, ct);
            ms.Position = 0;

            // 必须在 UI 线程上设置 BitmapImage（从 stream 创建）
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();  // 跨线程安全

            PreviewImageSource = bitmap;
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            StatusText = $"预览失败：{ex.Message}";
        }
    }

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
        OnPropertyChanged(nameof(CanExport));  // v0.1.1
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
        OnPropertyChanged(nameof(CanExport));  // v0.1.1
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
            _cropRects.Remove(path);  // v0.3.2 同步移除裁剪
            if (SelectedFile == path) CurrentCropRect = null;
            OnPropertyChanged(nameof(FileCount));
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(CanExport));  // v0.1.1
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
        _cropRects.Clear();  // v0.3.2 同步清除裁剪字典
        CurrentCropRect = null;
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(CanExport));  // v0.1.1
        StatusText = "已清空文件列表";
    }

    // ============ 裁剪管理（v0.3.2 per-image）============

    /// <summary>获取文件的裁剪矩形（不存在返回 null）</summary>
    public CropRect? GetCropRect(string filePath) =>
        _cropRects.TryGetValue(filePath, out var r) ? r : null;

    /// <summary>设置文件的裁剪矩形（同步 CurrentCropRect + 触发预览重渲）</summary>
    public void SetCropRect(string filePath, CropRect rect)
    {
        _cropRects[filePath] = rect;
        if (filePath == SelectedFile)
        {
            CurrentCropRect = rect;
            TriggerAutoPreview();
        }
    }

    /// <summary>清除文件的裁剪矩形</summary>
    public void ClearCropRect(string filePath)
    {
        if (_cropRects.Remove(filePath) && filePath == SelectedFile)
        {
            CurrentCropRect = null;
            TriggerAutoPreview();
        }
    }

    /// <summary>清除所有裁剪（批量重置）</summary>
    public void ClearAllCrops()
    {
        if (_cropRects.Count == 0) return;
        _cropRects.Clear();
        CurrentCropRect = null;
        TriggerAutoPreview();
        StatusText = "已清除所有裁剪";
    }

    /// <summary>重置当前选中文件的裁剪（RelayCommand，XAML 绑定）</summary>
    [RelayCommand(CanExecute = nameof(CanClearCrop))]
    private void ClearCrop()
    {
        if (SelectedFile is { } path)
        {
            ClearCropRect(path);
            StatusText = "已重置当前裁剪";
        }
    }

    /// <summary>ClearCropCommand 可用条件：选中文件 + 该文件有裁剪</summary>
    public bool CanClearCrop => SelectedFile is not null && GetCropRect(SelectedFile) is not null;

    // ============ v0.3.3.6 背景色预览 Brush（含 Opacity）============

    /// <summary>v0.3.3.6 preview 端背景色 Brush（SolidColorBrush + Opacity）</summary>
    public System.Windows.Media.Brush? PreviewBackgroundBrush
    {
        get
        {
            if (Config.Layers.Count == 0 || Config.Layers[0] is not TextWatermarkLayer tl) return null;
            if (!tl.HasBackground) return null;
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tl.BackgroundColor)!;
                var brush = new System.Windows.Media.SolidColorBrush(color) { Opacity = tl.BackgroundOpacity };
                brush.Freeze();
                return brush;
            }
            catch { return null; }
        }
    }

    // ============ v0.3.3.6 撤回 (Undo) ============

    /// <summary>v0.3.3.6 undo 历史栈（最近 → 最旧）</summary>
    private readonly Stack<WatermarkConfig> _undoStack = new();

    /// <summary>v0.3.3.7 redo 历史栈（最近 → 最旧）</summary>
    private readonly Stack<WatermarkConfig> _redoStack = new();

    /// <summary>v0.3.3.6 debounce CTS（避免连续拖动产生 100 个 snapshot）</summary>
    private CancellationTokenSource? _undoDebounceCts;

    /// <summary>v0.3.3.6 undo debounce 间隔（毫秒）</summary>
    private const int UndoDebounceMs = 500;

    private void PushUndoSnapshot()
    {
        _undoDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _undoDebounceCts = cts;
        _ = DebouncedPushSnapshotAsync(cts.Token);
    }

    private async Task DebouncedPushSnapshotAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(UndoDebounceMs, ct);
            var snapshot = Config.Clone();
            _undoStack.Push(snapshot);
            // v0.3.3.7 新操作让 redo stack 无效
            _redoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        // v0.3.3.7 先存当前到 redo（让 Redo 能撤销这次 Undo）
        _redoStack.Push(Config.Clone());
        var snapshot = _undoStack.Pop();
        RestoreFromSnapshot(snapshot);

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PreviewBackgroundBrush));
        TriggerAutoPreview();
        RecomputeWatermarkBounds();
    }

    public bool CanUndo => _undoStack.Count > 0;

    // ============ v0.3.3.7 恢复 (Redo) ============

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        // 先存当前到 undo（让 Undo 能撤销这次 Redo）
        _undoStack.Push(Config.Clone());
        var snapshot = _redoStack.Pop();
        RestoreFromSnapshot(snapshot);

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PreviewBackgroundBrush));
        TriggerAutoPreview();
        RecomputeWatermarkBounds();
    }

    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>v0.3.3.7 Undo / Redo 共用：把 snapshot 字段还原到当前 layer[0]</summary>
    private void RestoreFromSnapshot(WatermarkConfig snapshot)
    {
        if (Config.Layers.Count == 0 || Config.Layers[0] is not TextWatermarkLayer cur) return;
        if (snapshot.Layers.Count == 0 || snapshot.Layers[0] is not TextWatermarkLayer saved) return;

        cur.Text = saved.Text;
        cur.FontFamily = saved.FontFamily;
        cur.FontSize = saved.FontSize;
        cur.Color = saved.Color;
        cur.Position = saved.Position;
        cur.Margin = saved.Margin;
        cur.Opacity = saved.Opacity;
        cur.Rotation = saved.Rotation;
        cur.OffsetX = saved.OffsetX;
        cur.OffsetY = saved.OffsetY;
        cur.Stroke = saved.Stroke;
        cur.StrokeColor = saved.StrokeColor;
        cur.StrokeWidth = saved.StrokeWidth;
        cur.HasBackground = saved.HasBackground;
        cur.BackgroundColor = saved.BackgroundColor;
        cur.BackgroundPadding = saved.BackgroundPadding;
        cur.BackgroundOpacity = saved.BackgroundOpacity;
    }

    // ============ 模板集成 ============

    /// <summary>
    /// 加载模板（替换 Config）
    /// </summary>
    public bool LoadTemplate(TemplateRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        // 解绑旧 Config 订阅，挂新 Config
        Config.PropertyChanged -= OnConfigOrOutputChanged;
        Config.Output.PropertyChanged -= OnConfigOrOutputChanged;
        if (Config.Layers.Count > 0 && Config.Layers[0] is INotifyPropertyChanged oldInpc)
            oldInpc.PropertyChanged -= OnConfigOrOutputChanged;

        Config = record.Config;
        Config.PropertyChanged += OnConfigOrOutputChanged;
        Config.Output.PropertyChanged += OnConfigOrOutputChanged;
        HookLayerPropertyChanged();

        StatusText = $"已加载模板 {record.Name}";
        TriggerAutoPreview();
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
                    await _processor.ApplyAsync(file, outputPath, Config, GetCropRect(file), ct);
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