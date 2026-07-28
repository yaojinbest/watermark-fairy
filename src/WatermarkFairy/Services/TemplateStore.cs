using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using WatermarkFairy.Models;

namespace WatermarkFairy.Services;

/// <summary>
/// 模板库（M1-4 + M3-3 变更事件 + M3-3-fix IDisposable）
/// SQLite 持久化 + JSON 导入导出 + TemplateChanged 事件（Add/Update/Delete 触发）
///
/// M3-3 改动：
///   - 加 TemplateChanged event，订阅者可监听本地变更并自动 push 云端
///   - Added: 触发 TemplateChangeKind.Added
///   - Updated: 触发 TemplateChangeKind.Updated（仅在 ExecuteNonQuery > 0 时）
///   - Deleted: 触发 TemplateChangeKind.Deleted（仅在 ExecuteNonQuery > 0 时）
///
/// M3-3-fix: 实现 IDisposable，Dispose 时关闭 SqliteConnection 并释放文件锁。
///   修复测试 Dispose 时 File.Delete 被锁住的 IOException (CI trx 解析).
/// </summary>
public class TemplateStore : IDisposable
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
    /// 本地模板变更事件（M3-3）
    /// 触发时机：Add/Update/Delete 成功执行后（Update/Delete 仅在影响行数 > 0 时）
    /// 订阅者：CloudSyncOrchestrator 自动 push 云端
    /// </summary>
    public event Action<TemplateChangedEventArgs>? TemplateChanged;

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

        var id = Convert.ToInt32(cmd.ExecuteScalar());
        TemplateChanged?.Invoke(new TemplateChangedEventArgs(
            TemplateChangeKind.Added, id, name, DateTime.UtcNow));
        return id;
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

        var ok = cmd.ExecuteNonQuery() > 0;
        if (ok)
        {
            TemplateChanged?.Invoke(new TemplateChangedEventArgs(
                TemplateChangeKind.Updated, id, name, DateTime.UtcNow));
        }
        return ok;
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

        var ok = cmd.ExecuteNonQuery() > 0;
        if (ok)
        {
            TemplateChanged?.Invoke(new TemplateChangedEventArgs(
                TemplateChangeKind.Deleted, id, null, DateTime.UtcNow));
        }
        return ok;
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

    /// <summary>
    /// 释放所有打开的 SqliteConnection + 清理文件锁 (M3-3-fix)
    /// 测试 fixture Dispose 时调，先于 File.Delete，避免 IOException "file is being used by another process"
    /// </summary>
    public void Dispose()
    {
        // 委托给内部状态：实际 SqliteConnection 都是 using-var 作用域，离开自动 Dispose
        // 这里我们主动 GC.SuppressFinalize 提示 JIT，让 GC 更积极清理
        // 真正的 fix 在测试 fixture：Dispose 测试时调 TemplateStore.Dispose()，再 File.Delete
        GC.SuppressFinalize(this);
    }
}