using System.IO;
using FluentAssertions;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

public class AppSettingsStoreTests : IDisposable
{
    private readonly string _jsonPath;
    private readonly AppSettingsStore _store;

    public AppSettingsStoreTests()
    {
        _jsonPath = Path.Combine(Path.GetTempPath(),
            $"wf_settings_{Guid.NewGuid():N}.json");
        _store = new AppSettingsStore(_jsonPath);
    }

    public void Dispose()
    {
        if (File.Exists(_jsonPath))
            File.Delete(_jsonPath);
        foreach (var f in Directory.GetFiles(Path.GetTempPath(), "wf_settings_*.json.corrupted.*"))
            File.Delete(f);
    }

    // ============ 加载 ============

    [Fact]
    public void Load_FileNotExists_ReturnsDefaults()
    {
        var settings = _store.Load();
        settings.Theme.Should().Be("system");
        settings.DefaultFontFamily.Should().Be("Microsoft YaHei");
        settings.DefaultFontSize.Should().Be(24f);
        settings.DefaultOutputFormat.Should().Be("auto");
        settings.MaxHistory.Should().Be(10);
        settings.CheckUpdatesOnStartup.Should().BeTrue();
        settings.RecentFolder.Should().BeNull();
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(_jsonPath, "");
        _store.Load().DefaultFontFamily.Should().Be("Microsoft YaHei");
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaults_AndBacksUp()
    {
        File.WriteAllText(_jsonPath, "{invalid json,,,}");
        var settings = _store.Load();
        settings.DefaultFontFamily.Should().Be("Microsoft YaHei");
        File.Exists(_jsonPath).Should().BeFalse();
    }

    // ============ 保存 ============

    [Fact]
    public void Save_ThenLoad_RoundtripsValues()
    {
        var original = new AppSettings
        {
            Theme = "dark",
            DefaultFontFamily = "Arial",
            DefaultFontSize = 36f,
            DefaultColor = "#FF8800",
            DefaultOutputFormat = "png",
            DefaultQuality = 75,
            RecentFolder = @"D:\photos",
            MaxHistory = 20,
            CheckUpdatesOnStartup = false,
        };
        _store.Save(original);

        var loaded = _store.Load();
        loaded.Theme.Should().Be("dark");
        loaded.DefaultFontFamily.Should().Be("Arial");
        loaded.DefaultFontSize.Should().Be(36f);
        loaded.DefaultColor.Should().Be("#FF8800");
        loaded.DefaultOutputFormat.Should().Be("png");
        loaded.DefaultQuality.Should().Be(75);
        loaded.RecentFolder.Should().Be(@"D:\photos");
        loaded.MaxHistory.Should().Be(20);
        loaded.CheckUpdatesOnStartup.Should().BeFalse();
    }

    [Fact]
    public void Save_SetsUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var settings = new AppSettings();
        _store.Save(settings);
        var after = DateTime.UtcNow.AddSeconds(1);

        _store.Load().UpdatedAt.Should().BeOnOrAfter(before)
            .And.BeOnOrBefore(after);
    }

    [Fact]
    public void Save_NullSettings_Throws()
    {
        Action act = () => _store.Save(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ============ 原子写入 ============

    [Fact]
    public void Save_IsAtomic_NoTempFileLeft()
    {
        _store.Save(new AppSettings());
        var tempPath = _jsonPath + ".tmp";
        File.Exists(tempPath).Should().BeFalse();
        File.Exists(_jsonPath).Should().BeTrue();
    }

    [Fact]
    public void Save_OverwriteExisting_ReplacesCleanly()
    {
        _store.Save(new AppSettings { Theme = "light" });
        _store.Save(new AppSettings { Theme = "dark" });
        _store.Load().Theme.Should().Be("dark");
    }

    // ============ Update ============

    [Fact]
    public void Update_MutatesAndPersists()
    {
        _store.Save(new AppSettings { Theme = "light" });
        var updated = _store.Update(s => s.Theme = "dark");
        updated.Theme.Should().Be("dark");
        _store.Load().Theme.Should().Be("dark");
    }

    [Fact]
    public void Update_PreservesOtherFields()
    {
        _store.Save(new AppSettings
        {
            Theme = "light",
            DefaultFontFamily = "Arial",
        });
        _store.Update(s => s.Theme = "dark");
        var loaded = _store.Load();
        loaded.Theme.Should().Be("dark");
        loaded.DefaultFontFamily.Should().Be("Arial");
    }

    [Fact]
    public void Update_NullMutator_Throws()
    {
        Action act = () => _store.Update(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Update_OnNonExistentFile_StartsFromDefaults()
    {
        var updated = _store.Update(s => s.Theme = "dark");
        updated.Theme.Should().Be("dark");
        updated.DefaultFontFamily.Should().Be("Microsoft YaHei");
    }

    // ============ 默认路径 ============

    [Fact]
    public void DefaultJsonPath_EndsWithConfigJson()
    {
        var path = AppSettingsStore.DefaultJsonPath();
        path.Should().EndWith("config.json");
        path.Should().Contain("WatermarkFairy");
    }
}