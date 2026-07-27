using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 命名规则引擎（M1-1）
/// 支持占位符替换 + 正则替换规则
/// </summary>
public class NamingRuleEngine
{
    /// <summary>占位符 pattern: {key} 或 {key:format}</summary>
    private static readonly Regex PlaceholderRegex = new(
        @"\{(\w+)(?::([^}]+))?\}",
        RegexOptions.Compiled);

    /// <summary>
    /// 应用命名规则
    /// </summary>
    /// <param name="input">输入 pattern（如 "{name}_wm_{n}"）</param>
    /// <param name="context">命名上下文</param>
    /// <param name="rules">额外规则（按 Order 升序应用）</param>
    public string Apply(
        string input,
        NamingContext context,
        IReadOnlyList<NamingRule>? rules = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        // 1. 应用占位符
        var result = ApplyPlaceholders(input, context);

        // 2. 应用正则规则（按 Order 升序）
        if (rules is { Count: > 0 })
        {
            var orderedRules = rules
                .Where(r => r.IsRegex)
                .OrderBy(r => r.Order)
                .ToList();

            foreach (var rule in orderedRules)
            {
                try
                {
                    result = Regex.Replace(
                        result,
                        rule.Pattern,
                        rule.Replacement ?? string.Empty,
                        RegexOptions.None,
                        TimeSpan.FromSeconds(1));
                }
                catch (RegexMatchTimeoutException)
                {
                    throw new NamingRuleException(
                        $"正则规则超时: {rule.Pattern}", default!);
                }
                catch (ArgumentException ex)
                {
                    throw new NamingRuleException(
                        $"正则规则无效: {rule.Pattern} ({ex.Message})", ex);
                }
            }
        }

        return result;
    }

    private string ApplyPlaceholders(string input, NamingContext context)
    {
        return PlaceholderRegex.Replace(input, match =>
        {
            var key = match.Groups[1].Value.ToLowerInvariant();
            var format = match.Groups[2].Success ? match.Groups[2].Value : null;

            return key switch
            {
                "name" => context.OriginalFileName,
                "ext" => context.Extension,
                "date" => FormatDate(context.ProcessedAt, format),
                "time" => context.ProcessedAt.ToString(format ?? "HHmmss"),
                "n" => ApplyNumberFormat(context.Sequence, format),
                "size" => $"{context.ImageWidth}x{context.ImageHeight}",
                "w" => context.ImageWidth.ToString(),
                "h" => context.ImageHeight.ToString(),
                "hash" => ComputeShortHash(context.SourcePath, format),
                _ => match.Value,  // 未知占位符保留原样
            };
        });
    }

    private static string FormatDate(DateTime dt, string? format)
    {
        try
        {
            return format is null ? dt.ToString("yyyy-MM-dd") : dt.ToString(format);
        }
        catch (FormatException ex)
        {
            throw new NamingRuleException($"日期格式无效: {format}", ex);
        }
    }

    /// <summary>
    /// 数字格式化：支持 {n} {n:000} {n:D4} {n:0000}
    /// </summary>
    private static string ApplyNumberFormat(int n, string? format)
    {
        if (format is null) return n.ToString();

        // 全部为 '0' 的格式：按 0 数量补零
        if (format.Length > 0 && format.All(c => c == '0'))
        {
            return n.ToString("D" + format.Length);
        }

        // D 格式（D + 数字）
        if (format.StartsWith('D') && format.Length > 1 &&
            int.TryParse(format.AsSpan(1), out var d))
        {
            return n.ToString("D" + d);
        }

        // 回退到 .NET 标准数字格式
        try
        {
            return n.ToString(format);
        }
        catch (FormatException ex)
        {
            throw new NamingRuleException($"数字格式无效: {format}", ex);
        }
    }

    /// <summary>
    /// 文件短哈希：默认 8 位 MD5 hex，支持 {hash:16} 自定义长度
    /// </summary>
    private string ComputeShortHash(string path, string? format)
    {
        var take = 8;
        if (format is not null && int.TryParse(format, out var len))
        {
            take = Math.Clamp(len, 4, 32);
        }

        if (!File.Exists(path))
        {
            // 文件不存在时返回占位符（不抛异常，让用户看到源文件名问题）
            return new string('0', take);
        }

        using var stream = File.OpenRead(path);
        var hashBytes = MD5.HashData(stream);
        var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hex[..take];
    }
}
