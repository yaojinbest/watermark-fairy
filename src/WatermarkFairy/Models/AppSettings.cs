namespace WatermarkFairy.Models;

/// <summary>
/// 应用配置（M1-5）
/// 持久化主题 / 默认水印参数 / 最近目录 / 历史记录上限
/// </summary>
public class AppSettings
{
    /// <summary>主题：light / dark / system</summary>
    public string Theme { get; set; } = "system";

    /// <summary>默认字体</summary>
    public string DefaultFontFamily { get; set; } = "Microsoft YaHei";

    /// <summary>默认字号</summary>
    public float DefaultFontSize { get; set; } = 24f;

    /// <summary>默认颜色（Hex）</summary>
    public string DefaultColor { get; set; } = "#FFFFFF";

    /// <summary>默认输出格式</summary>
    public string DefaultOutputFormat { get; set; } = "auto";

    /// <summary>默认输出质量</summary>
    public int DefaultQuality { get; set; } = 90;

    /// <summary>最近打开的文件夹</summary>
    public string? RecentFolder { get; set; }

    /// <summary>最大历史记录数</summary>
    public int MaxHistory { get; set; } = 10;

    /// <summary>启动时检查更新</summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>最后一次更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}