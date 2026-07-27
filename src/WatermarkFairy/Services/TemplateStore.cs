using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 模板库（M1-4）
/// SQLite 持久化 + JSON 导入导出
/// </summary>
public class TemplateStore
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS templates (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            config_json TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        """;

    private const string CreateIndexSql = """
        CREATE INDEX IF NOT EXISTS idx_templates_name ON templates(name);
        """;

    private readonly string _dbPath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,  // 接受 PascalCase / camelCase 混合输入
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 构造：传入 dbPath 用于测试（temp file），默认 %APPDATA%\WatermarkFairy\templates.db
    /// </summary>
    public TemplateStore(string? dbPath = null)
    {
        _dbPath = dbPath ?? DefaultDbPath();
    }

    public string DbPath => _dbPath;

    /// <summary>默认 db 路径：%APPDATA%\WatermarkFairy\templates.db</summary>
    public static string DefaultDbPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WatermarkFairy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "templates.db");
    }

    /// <summary>
    /// 初始化表（首次运行或新建 db）
    /// </summary>
    public void Initialize()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = CreateTableSql + CreateIndexSql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 添加模板
    /// </summary>
    /// <returns>新模板 id</returns>
    public int Add(string name, WatermarkConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(config);

        var json = JsonSerializer.Serialize(config, JsonOpts);
        var now = DateTime.UtcNow.ToString("O");

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO templates (name, config_json, created_at, updated_at)
            VALUES ($name, $json, $now, $now);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.Parameters.AddWithValue("$now", now);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// 按 id 获取完整模板
    /// </summary>
    public TemplateRecord? Get(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, config_json, created_at, updated_at FROM templates WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadRecord(reader);
    }

    /// <summary>
    /// 按 name 获取完整模板
    /// </summary>
    public TemplateRecord? GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, config_json, created_at, updated_at FROM templates WHERE name = $name";
        cmd.Parameters.AddWithValue("$name", name);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadRecord(reader);
    }

    /// <summary>
    /// 更新模板（name + config）
    /// </summary>
    /// <returns>成功 true，不存在 false</returns>
    public bool Update(int id, string name, WatermarkConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(config);

        var json = JsonSerializer.Serialize(config, JsonOpts);
        var now = DateTime.UtcNow.ToString("O");

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE templates SET name = $name, config_json = $json, updated_at = $now
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.Parameters.AddWithValue("$now", now);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    public bool Delete(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM templates WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// 列出所有模板（轻量元数据）
    /// </summary>
    public IReadOnlyList<TemplateInfo> List()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_at, updated_at FROM templates ORDER BY updated_at DESC";

        var results = new List<TemplateInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new TemplateInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2)),
                DateTime.Parse(reader.GetString(3))));
        }
        return results;
    }

    /// <summary>
    /// 导出为 JSON 字符串
    /// </summary>
    public string ExportJson(int id)
    {
        var record = Get(id) ?? throw new KeyNotFoundException($"模板 id={id} 不存在");
        var json = JsonSerializer.Serialize(record.Config, JsonOpts);
        return json;
    }

    /// <summary>
    /// 从 JSON 字符串导入
    /// </summary>
    /// <returns>新模板 id</returns>
    public int ImportJson(string name, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var config = JsonSerializer.Deserialize<WatermarkConfig>(json, JsonOpts)
            ?? throw new InvalidOperationException("JSON 反序列化失败");
        return Add(name, config);
    }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM templates WHERE id = $id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() != null;
    }

    // ============ 私有方法 ============

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private static TemplateRecord ReadRecord(SqliteDataReader reader)
    {
        var json = reader.GetString(2);
        var config = JsonSerializer.Deserialize<WatermarkConfig>(json, JsonOpts)
            ?? throw new InvalidOperationException("config_json 反序列化失败");
        return new TemplateRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            config,
            DateTime.Parse(reader.GetString(3)),
            DateTime.Parse(reader.GetString(4)));
    }
}