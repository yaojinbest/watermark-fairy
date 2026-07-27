using System.IO;
using System.Text.Json;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 应用配置持久化（M1-5）
/// JSON 文件存储 · 默认路径 %APPDATA%\WatermarkFairy\config.json
/// </summary>
public class AppSettingsStore
{
    private readonly string _jsonPath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public AppSettingsStore(string? jsonPath = null)
    {
        _jsonPath = jsonPath ?? DefaultJsonPath();
    }

    public string JsonPath => _jsonPath;

    /// <summary>默认配置路径：%APPDATA%\WatermarkFairy\config.json</summary>
    public static string DefaultJsonPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WatermarkFairy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "config.json");
    }

    /// <summary>
    /// 加载配置（文件不存在时返回默认值）
    /// </summary>
    public AppSettings Load()
    {
        if (!File.Exists(_jsonPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_jsonPath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts)
                ?? new AppSettings();
        }
        catch (JsonException)
        {
            BackupCorrupted();
            return new AppSettings();
        }
    }

    /// <summary>
    /// 保存配置（原子写入：temp + rename）
    /// </summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.UpdatedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(settings, JsonOpts);
        var dir = Path.GetDirectoryName(_jsonPath)!;
        Directory.CreateDirectory(dir);

        var tempPath = _jsonPath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(_jsonPath))
            File.Replace(tempPath, _jsonPath, null);
        else
            File.Move(tempPath, _jsonPath);
    }

    /// <summary>
    /// 读 + 修改 + 写
    /// </summary>
    public AppSettings Update(Action<AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        var settings = Load();
        mutate(settings);
        Save(settings);
        return settings;
    }

    private void BackupCorrupted()
    {
        try
        {
            var backup = _jsonPath + ".corrupted." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            File.Move(_jsonPath, backup);
        }
        catch
        {
            // 备份失败不抛异常（best effort）
        }
    }
}