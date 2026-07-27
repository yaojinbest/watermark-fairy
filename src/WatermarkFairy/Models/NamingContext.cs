namespace WatermarkFairy.Models;

/// <summary>
/// 命名上下文（占位符解析所需的所有信息）
/// </summary>
public sealed class NamingContext
{
    /// <summary>原始文件名（不含扩展名）</summary>
    public required string OriginalFileName { get; init; }

    /// <summary>扩展名（不含点）</summary>
    public required string Extension { get; init; }

    /// <summary>当前序号（从 1 开始）</summary>
    public required int Sequence { get; init; }

    /// <summary>图像宽度（像素）</summary>
    public required int ImageWidth { get; init; }

    /// <summary>图像高度（像素）</summary>
    public required int ImageHeight { get; init; }

    /// <summary>源文件完整路径</summary>
    public required string SourcePath { get; init; }

    /// <summary>处理时间</summary>
    public DateTime ProcessedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// 命名规则（占位符 pattern + 正则）
/// </summary>
public sealed class NamingRule
{
    /// <summary>pattern：占位符 pattern（如 "{name}_wm_{n}"）或正则表达式</summary>
    public required string Pattern { get; init; }

    /// <summary>是否正则规则（false = 占位符 pattern）</summary>
    public bool IsRegex { get; init; }

    /// <summary>规则应用顺序（升序）</summary>
    public int Order { get; init; }

    /// <summary>正则替换的目标（仅 IsRegex=true 使用）</summary>
    public string? Replacement { get; init; }
}

/// <summary>
/// 命名规则解析异常
/// </summary>
public class NamingRuleException : Exception
{
    public NamingRuleException(string message) : base(message) { }
    public NamingRuleException(string message, Exception inner) : base(message, inner) { }
}
