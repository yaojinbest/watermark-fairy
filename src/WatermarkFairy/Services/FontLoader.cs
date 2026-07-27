using System.IO;
using System.Reflection;
using SixLabors.Fonts;

namespace WatermarkFairy.Services;

/// <summary>
/// 思源黑体加载器（M1-2.1 patch）
/// 优先从 WPF 嵌入资源加载；fallback 到输出目录磁盘文件
/// </summary>
public static class FontLoader
{
    private static readonly object _lock = new();
    private static FontCollection? _collection;
    private static string? _loadedFrom;

    /// <summary>当前加载的字体集（未加载返 null）</summary>
    public static FontCollection? Collection
    {
        get
        {
            EnsureLoaded();
            return _collection;
        }
    }

    /// <summary>加载来源描述（"embedded" / "disk" / "none"）</summary>
    public static string? LoadedFrom => _loadedFrom;

    public static void EnsureLoaded()
    {
        if (_collection != null) return;
        lock (_lock)
        {
            if (_collection != null) return;

            var collection = LoadFromResource();
            _loadedFrom = collection != null ? "embedded" : null;

            collection ??= LoadFromDisk();
            if (collection != null) _loadedFrom = "disk";

            _collection = collection;
        }
    }

    /// <summary>
    /// 从 WPF 嵌入资源加载（manifest resource）
    /// Resource 名为 "WatermarkFairy.Resources.Fonts.SourceHanSansSC-Regular.otf"
    /// （由 .csproj 的 Resource Include 路径推导）
    /// </summary>
    private static FontCollection? LoadFromResource()
    {
        try
        {
            var assembly = typeof(FontLoader).Assembly;
            var resourceName = "WatermarkFairy.Resources.Fonts.SourceHanSansSC-Regular.otf";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            var collection = new FontCollection();
            collection.Add(stream);
            return collection;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fallback：从输出目录磁盘文件加载
    /// 路径：AppContext.BaseDirectory/Resources/Fonts/SourceHanSansSC-Regular.otf
    /// </summary>
    private static FontCollection? LoadFromDisk()
    {
        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Resources", "Fonts", "SourceHanSansSC-Regular.otf");
            if (!File.Exists(path)) return null;

            var collection = new FontCollection();
            collection.Add(path);
            return collection;
        }
        catch
        {
            return null;
        }
    }
}
