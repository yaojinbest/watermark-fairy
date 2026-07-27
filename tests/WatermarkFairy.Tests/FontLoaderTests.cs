using System.IO;
using FluentAssertions;
using SixLabors.Fonts;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

public class FontLoaderTests
{
    [Fact]
    public void EnsureLoaded_DoesNotThrow()
    {
        // 测试环境：FontLoader 可能从 embedded / disk / none 任意来源加载
        // 确保 EnsureLoaded 不抛异常（embedded 在 WPF 进程，test 进程用 disk/none）
        var act = () => FontLoader.EnsureLoaded();
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureLoaded_IsIdempotent()
    {
        FontLoader.EnsureLoaded();
        var first = FontLoader.Collection;
        FontLoader.EnsureLoaded();
        var second = FontLoader.Collection;

        // 多次调用应返回同一引用
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void LoadedFrom_IsValidSource()
    {
        FontLoader.EnsureLoaded();
        // 可能值：embedded / disk / none
        var source = FontLoader.LoadedFrom;
        source.Should().BeOneOf("embedded", "disk", null);
    }

    [Fact]
    public void FontCollection_LoadFromDiskPath_ThrowsForMissingFile()
    {
        // 验证 FontCollection.Add(path) 对不存在文件抛 FileNotFoundException
        // 用 Path.GetTempPath()（始终存在） + 随机文件名，避免 DirectoryNotFoundException
        var collection = new FontCollection();
        var nonexistent = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.otf");
        var act = () => collection.Add(nonexistent);
        act.Should().Throw<FileNotFoundException>();
    }
}
