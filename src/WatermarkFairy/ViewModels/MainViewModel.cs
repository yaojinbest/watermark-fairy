using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WatermarkFairy.Models;
using WatermarkFairy.Services;

namespace WatermarkFairy.ViewModels;

/// <summary>
/// 主视图模型（M1-6 完整化 + M2.3 CloudSync 集成 + M3-2 ICommand）
/// 左控制 + 中预览 + 右文件列表 + 底部状态 + 云端同步 + 命令
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ImageProcessor _processor;
    private readonly TemplateStore? _templateStore;
    private readonly ICloudSyncService _cloudSync;

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
    private string _statusText = "就绪 · M3 阶段";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private bool _isCloudAuthenticated;

    [ObservableProperty]
    private string? _cloudUserEmail;

    [ObservableProperty]
    private bool _isCloudSyncing;

    [ObservableProperty]
    private string _cloudStatusText = "未连接云端";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLogin))]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string? _loginEmail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLogin))]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string? _loginPassword;

    /// <summary>待处理文件列表（ObservableCollection 适配 WPF 双向绑定）</summary>
    public ObservableCollection<string> FileList { get; } = new();

    /// <summary>云端模板列表（ObservableCollection 适配 WPF 双向绑定）</summary>
    public ObservableCollection<CloudTemplateInfo> CloudTemplates { get; } = new();

    /// <summary>当前云端同步服务（可绑给 UI）</summary>
    public ICloudSyncService CloudSync => _cloudSync;

    public MainViewModel()
        : this(new ImageProcessor(), null, null)
    {
    }

    public MainViewModel(ImageProcessor processor, TemplateStore? templateStore, ICloudSyncService? cloudSync = null)
    {
        _processor = processor;
        _templateStore = templateStore;
        _cloudSync = cloudSync ?? new MockCloudSyncService();

        // 同步 cloud 初始状态
        _isCloudAuthenticated = _cloudSync.IsAuthenticated;
        _cloudUserEmail = _cloudSync.CurrentUserEmail;
    }

    /// <summary>
    /// 是否有待处理文件
    /// </summary>
    public bool HasFiles => FileList.Count > 0;

    /// <summary>
    /// 文件数（用于 UI 绑定）
    /// </summary>
    public int FileCount => FileList.Count;

    /// <summary>LoginCommand 的 CanExecute：邮箱/密码都填了 + 未在同步中</summary>
    public bool CanLogin =>
        !IsCloudSyncing
        && !string.IsNullOrWhiteSpace(LoginEmail)
        && !string.IsNullOrWhiteSpace(LoginPassword);

    /// <summary>已登录 + 未在同步中（可操作云端）</summary>
    public bool CanLoggedIn => IsCloudAuthenticated && !IsCloudSyncing;

    // ============ ICommand（M3-2 绑定用）============

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(LoginEmail) || string.IsNullOrWhiteSpace(LoginPassword)) return;
        var email = LoginEmail;
        var pwd = LoginPassword;
        LoginPassword = null;  // 清空密码（安全）
        await LoginAsync(email, pwd);
    }

    [RelayCommand(CanExecute = nameof(CanLoggedIn))]
    private async Task Logout()
    {
        await LogoutAsync();
    }

    [RelayCommand(CanExecute = nameof(CanLoggedIn))]
    private async Task UploadCurrentAsync()
    {
        var name = Config.Name ?? "Untitled";
        await UploadCurrentTemplateAsync(name);
    }

    [RelayCommand(CanExecute = nameof(CanLoggedIn))]
    private async Task RefreshCloudAsync()
    {
        await RefreshCloudTemplatesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanLoggedIn))]
    private async Task DownloadCloudAsync(CloudTemplateInfo? template)
    {
        if (template == null) return;
        await DownloadAndApplyCloudTemplateAsync(template.CloudId);
    }

    [RelayCommand(CanExecute = nameof(CanLoggedIn))]
    private async Task DeleteCloudAsync(CloudTemplateInfo? template)
    {
        if (template == null) return;
        await DeleteCloudTemplateAsync(template.CloudId);
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

    // ============ 云端同步（M2.3）============

    /// <summary>
    /// 登录云端
    /// </summary>
    public async Task<CloudAuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        IsCloudSyncing = true;
        CloudStatusText = "登录中...";
        try
        {
            var result = await _cloudSync.LoginAsync(email, password, ct);
            IsCloudAuthenticated = result.Success;
            CloudUserEmail = result.UserEmail;
            CloudStatusText = result.Success
                ? $"已登录：{result.UserEmail}"
                : $"登录失败：{result.ErrorMessage}";
            if (result.Success)
            {
                await RefreshCloudTemplatesAsync(ct);
            }
            return result;
        }
        finally
        {
            IsCloudSyncing = false;
        }
    }

    /// <summary>
    /// 登出云端
    /// </summary>
    public async Task LogoutAsync()
    {
        IsCloudSyncing = true;
        try
        {
            await _cloudSync.LogoutAsync();
            IsCloudAuthenticated = false;
            CloudUserEmail = null;
            CloudTemplates.Clear();
            CloudStatusText = "已登出";
        }
        finally
        {
            IsCloudSyncing = false;
        }
    }

    /// <summary>
    /// 上传当前 Config 为云端模板
    /// </summary>
    public async Task<CloudUploadResult> UploadCurrentTemplateAsync(string name, CancellationToken ct = default)
    {
        if (!IsCloudAuthenticated)
        {
            CloudStatusText = "请先登录";
            return new CloudUploadResult(false, ErrorMessage: "未登录");
        }

        IsCloudSyncing = true;
        CloudStatusText = $"上传 {name}...";
        try
        {
            var record = new TemplateRecord(
                Id: 0,
                Name: name,
                Config: Config,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow);
            var result = await _cloudSync.UploadTemplateAsync(record, ct);
            CloudStatusText = result.Success
                ? $"上传成功：{name} (id={result.CloudId})"
                : $"上传失败：{result.ErrorMessage}";
            if (result.Success)
            {
                await RefreshCloudTemplatesAsync(ct);
            }
            return result;
        }
        finally
        {
            IsCloudSyncing = false;
        }
    }

    /// <summary>
    /// 刷新云端模板列表
    /// </summary>
    public async Task RefreshCloudTemplatesAsync(CancellationToken ct = default)
    {
        if (!IsCloudAuthenticated)
        {
            CloudTemplates.Clear();
            CloudStatusText = "未登录";
            return;
        }

        IsCloudSyncing = true;
        try
        {
            var list = await _cloudSync.ListCloudTemplatesAsync(ct);
            CloudTemplates.Clear();
            foreach (var t in list) CloudTemplates.Add(t);
            CloudStatusText = $"已加载 {list.Count} 个云端模板";
        }
        finally
        {
            IsCloudSyncing = false;
        }
    }

    /// <summary>
    /// 下载并应用云端模板
    /// </summary>
    public async Task<CloudDownloadResult> DownloadAndApplyCloudTemplateAsync(long cloudId, CancellationToken ct = default)
    {
        if (!IsCloudAuthenticated)
        {
            return new CloudDownloadResult(false, ErrorMessage: "未登录");
        }

        IsCloudSyncing = true;
        try
        {
            var result = await _cloudSync.DownloadTemplateAsync(cloudId, ct);
            if (result.Success && result.Template != null)
            {
                LoadTemplate(result.Template);
                CloudStatusText = $"已下载并应用：{result.Template.Name}";
            }
            else
            {
                CloudStatusText = $"下载失败：{result.ErrorMessage}";
            }
            return result;
        }
        finally
        {
            IsCloudSyncing = false;
        }
    }

    /// <summary>
    /// 删除云端模板
    /// </summary>
    public async Task<bool> DeleteCloudTemplateAsync(long cloudId, CancellationToken ct = default)
    {
        if (!IsCloudAuthenticated) return false;
        IsCloudSyncing = true;
        try
        {
            var result = await _cloudSync.DeleteCloudTemplateAsync(cloudId, ct);
            if (result)
            {
                await RefreshCloudTemplatesAsync(ct);
            }
            return result;
        }
        finally
        {
            IsCloudSyncing = false;
        }
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