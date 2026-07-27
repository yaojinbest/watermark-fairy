using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using WatermarkFairy.Models;
using WatermarkFairy.Services;

namespace WatermarkFairy.ViewModels;

/// <summary>
/// 预览视图模型（M1-7）
/// 实时预览（debounce 100ms）+ 多图切换
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    private readonly PreviewRenderer _renderer;
    private CancellationTokenSource? _debounceCts;

    [ObservableProperty]
    private string? _sourcePath;

    [ObservableProperty]
    private BitmapSource? _previewImage;

    [ObservableProperty]
    private bool _isRendering;

    [ObservableProperty]
    private string _statusText = "预览区 · M1-7 阶段";

    [ObservableProperty]
    private int _previewMaxSize = 800;

    public PreviewViewModel()
        : this(new PreviewRenderer())
    {
    }

    public PreviewViewModel(PreviewRenderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>
    /// 触发预览（debounce 100ms）
    /// 同一时间窗内多次调用只渲染最后一次
    /// </summary>
    public void TriggerPreview(WatermarkConfig config, int debounceMs = 100)
    {
        ArgumentNullException.ThrowIfNull(config);

        // 取消之前的 debounce
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;

        _ = DebounceAndRenderAsync(config, debounceMs, ct);
    }

    private async Task DebounceAndRenderAsync(WatermarkConfig config, int debounceMs, CancellationToken ct)
    {
        try
        {
            // 防抖
            await Task.Delay(debounceMs, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SourcePath) || !File.Exists(SourcePath))
        {
            PreviewImage = null;
            StatusText = "无图片";
            return;
        }

        IsRendering = true;
        StatusText = $"渲染预览：{Path.GetFileName(SourcePath)}";

        try
        {
            var result = await _renderer.RenderAsync(SourcePath, config, PreviewMaxSize, ct);
            if (!ct.IsCancellationRequested)
            {
                PreviewImage = result;
                StatusText = result != null
                    ? $"已渲染（{PreviewMaxSize}px 限制）"
                    : "渲染失败";
            }
        }
        catch (OperationCanceledException)
        {
            // 静默忽略取消
        }
        catch (Exception ex)
        {
            StatusText = $"渲染错误：{ex.Message}";
        }
        finally
        {
            IsRendering = false;
        }
    }

    /// <summary>
    /// 同步设置源图片（不触发预览）
    /// </summary>
    public void SetSource(string? path)
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        SourcePath = path;
        PreviewImage = null;
        StatusText = path == null ? "预览区 · M1-7 阶段" : $"已选：{Path.GetFileName(path)}";
    }
}