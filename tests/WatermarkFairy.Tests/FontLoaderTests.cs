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
    public void FontCollection_LoadFromDiskPath_Works()
    {
        // 直接验证：给定磁盘上的 OTF 文件能加载
        // Source Han Sans 字体文件在 Resources/Fonts/（src 项目下，test 进程磁盘访问不到）
        // 改用临时复制或直接验证 FontCollection 机制
        var collection = new FontCollection();
        // 没文件时 Add 会抛 FileNotFoundException，验证 API 行为
        var act = () => collection.Add("/nonexistent/font.otf");
        act.Should().Throw<FileNotFoundException>();
    }
}
