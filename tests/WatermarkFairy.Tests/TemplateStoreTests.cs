using System.Collections.ObjectModel;
using System.IO;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

public class TemplateStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TemplateStore _store;

    public TemplateStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"wf_template_{Guid.NewGuid():N}.db");
        _store = new TemplateStore(_dbPath);
        _store.Initialize();
    }

    public void Dispose()
    {
        // 清理临时 db 文件
        // 需先 ClearAllPools() 释放 SqliteConnection 文件锁
        // 否则 Windows 下 File.Delete 抛 IOException
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }

    private static WatermarkConfig SampleConfig(string name = "Sample") =>
        new()
        {
            Name = name,
            Layers = new ObservableCollection<WatermarkLayer>
            {
                new TextWatermarkLayer
                {
                    Text = "Hello",
                    FontSize = 32f,
                    Position = WatermarkPosition.BottomRight,
                }
            },
            Output = new OutputOptions { Format = "jpg", Quality = 85 },
        };

    // ============ CRUD ============

    [Fact]
    public void Add_ThenGet_ReturnsSameConfig()
    {
        var id = _store.Add("My Template", SampleConfig("Original"));
        var record = _store.Get(id);
        record.Should().NotBeNull();
        record!.Name.Should().Be("My Template");
        record.Config.Name.Should().Be("Original");
        record.Config.Layers.Should().HaveCount(1);
        record.Config.Layers[0].Should().BeOfType<TextWatermarkLayer>();
    }

    [Fact]
    public void Add_NullOrEmptyName_Throws()
    {
        Action act1 = () => _store.Add("", SampleConfig());
        Action act2 = () => _store.Add("  ", SampleConfig());
        Action act3 = () => _store.Add("Valid", null!);
        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        _store.Get(999).Should().BeNull();
    }

    [Fact]
    public void GetByName_ReturnsCorrectTemplate()
    {
        _store.Add("Alpha", SampleConfig("A"));
        _store.Add("Beta", SampleConfig("B"));
        var beta = _store.GetByName("Beta");
        beta.Should().NotBeNull();
        beta!.Config.Name.Should().Be("B");
    }

    [Fact]
    public void GetByName_NotFound_ReturnsNull()
    {
        _store.GetByName("Nope").Should().BeNull();
    }

    [Fact]
    public void Update_ChangesConfig()
    {
        var id = _store.Add("Test", SampleConfig("Old"));
        var ok = _store.Update(id, "Test", SampleConfig("New"));
        ok.Should().BeTrue();
        var updated = _store.Get(id);
        updated!.Config.Name.Should().Be("New");
    }

    [Fact]
    public void Update_NonExistent_ReturnsFalse()
    {
        _store.Update(999, "Test", SampleConfig()).Should().BeFalse();
    }

    [Fact]
    public void Delete_RemovesTemplate()
    {
        var id = _store.Add("Test", SampleConfig());
        _store.Delete(id).Should().BeTrue();
        _store.Get(id).Should().BeNull();
    }

    [Fact]
    public void Delete_NonExistent_ReturnsFalse()
    {
        _store.Delete(999).Should().BeFalse();
    }

    [Fact]
    public void List_ReturnsAllTemplates_OrderedByUpdatedAtDesc()
    {
        _store.Add("Alpha", SampleConfig());
        Thread.Sleep(10);  // 确保 updated_at 不同
        _store.Add("Beta", SampleConfig());
        Thread.Sleep(10);
        _store.Add("Gamma", SampleConfig());

        var list = _store.List();
        list.Should().HaveCount(3);
        // 最近更新在前（Gamma > Beta > Alpha）
        list[0].Name.Should().Be("Gamma");
        list[1].Name.Should().Be("Beta");
        list[2].Name.Should().Be("Alpha");
    }

    [Fact]
    public void List_Empty_ReturnsEmpty()
    {
        _store.List().Should().BeEmpty();
    }

    [Fact]
    public void Add_Multiple_HaveUniqueIncrementingIds()
    {
        var id1 = _store.Add("First", SampleConfig());
        var id2 = _store.Add("Second", SampleConfig());
        id1.Should().NotBe(id2);
        id2.Should().BeGreaterThan(id1);
    }

    [Fact]
    public void Exists_ReturnsTrueForExisting_FalseOtherwise()
    {
        var id = _store.Add("Test", SampleConfig());
        _store.Exists(id).Should().BeTrue();
        _store.Exists(999).Should().BeFalse();
    }

    // ============ JSON 导入导出 ============

    [Fact]
    public void ExportJson_ReturnsValidJson()
    {
        var id = _store.Add("Test", SampleConfig());
        var json = _store.ExportJson(id);
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Sample");  // SampleConfig 默认 Name
        json.Should().Contain("layers");
        json.Should().Contain("Hello");    // TextWatermarkLayer.Text
        json.Should().Contain("\"type\": \"text\"");  // 多态 discriminator
    }

    [Fact]
    public void ExportJson_NonExistent_Throws()
    {
        Action act = () => _store.ExportJson(999);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ImportJson_RoundtripsConfig()
    {
        var original = SampleConfig("MyConfig");
        var originalJson = System.Text.Json.JsonSerializer.Serialize(original,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        var newId = _store.ImportJson("Imported", originalJson);
        var imported = _store.Get(newId);
        imported!.Config.Name.Should().Be("MyConfig");
        imported.Config.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void ImportJson_EmptyJson_Throws()
    {
        Action act = () => _store.ImportJson("Test", "");
        act.Should().Throw<ArgumentException>();
    }

    // ============ 初始化 ============

    [Fact]
    public void DbPath_DefaultsToAppData()
    {
        // 静态方法检查默认路径
        var path = TemplateStore.DefaultDbPath();
        path.Should().EndWith("templates.db");
        path.Should().Contain("WatermarkFairy");
    }

    [Fact]
    public void DbPath_Constructor_CustomPath()
    {
        // 用 _dbPath（测试构造时传入）覆盖默认
        _store.DbPath.Should().Be(_dbPath);
    }
}