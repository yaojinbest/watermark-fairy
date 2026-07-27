using FluentAssertions;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using Xunit;

namespace WatermarkFairy.Tests;

public class NamingRuleEngineTests
{
    private readonly NamingRuleEngine _engine = new();
    private readonly DateTime _fixedTime = new(2026, 7, 27, 14, 30, 45);

    private NamingContext Ctx(
        string name = "DSC0001",
        string ext = "jpg",
        int seq = 1,
        int w = 1920,
        int h = 1080,
        string? path = null) => new()
    {
        OriginalFileName = name,
        Extension = ext,
        Sequence = seq,
        ImageWidth = w,
        ImageHeight = h,
        SourcePath = path ?? $"/tmp/{name}.{ext}",
        ProcessedAt = _fixedTime,
    };

    // ============ 基本占位符（7 用例）============

    [Fact]
    public void Name_Placeholder_Replaces()
    {
        _engine.Apply("{name}", Ctx(name: "DSC0001"))
            .Should().Be("DSC0001");
    }

    [Fact]
    public void Ext_Placeholder_Replaces()
    {
        _engine.Apply("{ext}", Ctx(ext: "jpg"))
            .Should().Be("jpg");
    }

    [Fact]
    public void Date_DefaultFormat_YYYYMMDD_Dash()
    {
        _engine.Apply("{date}", Ctx())
            .Should().Be("2026-07-27");
    }

    [Fact]
    public void Time_DefaultFormat_HHmmss()
    {
        _engine.Apply("{time}", Ctx())
            .Should().Be("143045");
    }

    [Fact]
    public void N_Default_Plain()
    {
        _engine.Apply("{n}", Ctx(seq: 42))
            .Should().Be("42");
    }

    [Fact]
    public void Size_Placeholder_WxH()
    {
        _engine.Apply("{size}", Ctx(w: 1920, h: 1080))
            .Should().Be("1920x1080");
    }

    [Fact]
    public void Width_Height_Placeholders()
    {
        _engine.Apply("{w}x{h}", Ctx(w: 1280, h: 720))
            .Should().Be("1280x720");
    }

    // ============ 数字格式（3 用例）============

    [Fact]
    public void N_ZeroPadded_000()
    {
        _engine.Apply("{n:000}", Ctx(seq: 1))
            .Should().Be("001");
    }

    [Fact]
    public void N_ZeroPadded_5Digits()
    {
        _engine.Apply("{n:00000}", Ctx(seq: 42))
            .Should().Be("00042");
    }

    [Fact]
    public void N_DFormat_D4()
    {
        _engine.Apply("{n:D4}", Ctx(seq: 7))
            .Should().Be("0007");
    }

    // ============ 日期格式（2 用例）============

    [Fact]
    public void Date_CustomFormat_yyyyMMdd()
    {
        _engine.Apply("{date:yyyyMMdd}", Ctx())
            .Should().Be("20260727");
    }

    [Fact]
    public void Date_CustomFormat_WithTime()
    {
        _engine.Apply("{date:yyyy-MM-dd_HH-mm}", Ctx())
            .Should().Be("2026-07-27_14-30");
    }

    // ============ 正则规则（3 用例）============

    [Fact]
    public void RegexRule_ReplacesUnderscore()
    {
        var result = _engine.Apply(
            "DSC_0001_IMG",
            Ctx(),
            new[] { new NamingRule { Pattern = @"_", IsRegex = true, Order = 0, Replacement = "-" } });
        result.Should().Be("DSC-0001-IMG");
    }

    [Fact]
    public void RegexRule_OrderedApplication()
    {
        var result = _engine.Apply(
            "abc 123",
            Ctx(),
            new[]
            {
                new NamingRule { Pattern = @"\d+", IsRegex = true, Order = 1, Replacement = "NUM" },
                new NamingRule { Pattern = "[a-z]+", IsRegex = true, Order = 0, Replacement = "LETTER" },
            });
        // Order 升序：先字母→LETTER，再数字→NUM
        result.Should().Be("LETTER NUM");
    }

    [Fact]
    public void RegexRule_EmptyReplacement_RemovesMatch()
    {
        var result = _engine.Apply(
            "DSC 0001",
            Ctx(),
            new[] { new NamingRule { Pattern = @"\s+", IsRegex = true, Order = 0, Replacement = "" } });
        result.Should().Be("DSC0001");
    }

    // ============ 组合（2 用例）============

    [Fact]
    public void Combination_PlaceholdersAndRules()
    {
        var result = _engine.Apply(
            "{name}_wm_{date}_{n:000}",
            Ctx(name: "DSC0001", seq: 5),
            new[] { new NamingRule { Pattern = @"_wm_", IsRegex = true, Order = 0, Replacement = "-WATERMARK-" } });
        result.Should().Be("DSC0001-WATERMARK-2026-07-27_005");
    }

    [Fact]
    public void Combination_AllPlaceholders()
    {
        var result = _engine.Apply(
            "{name}_{w}x{h}_{date}_{n:000}.{ext}",
            Ctx(name: "IMG", ext: "png", seq: 9, w: 800, h: 600));
        result.Should().Be("IMG_800x600_2026-07-27_009.png");
    }

    // ============ 边界（3 用例）============

    [Fact]
    public void Unknown_Placeholder_Preserved()
    {
        _engine.Apply("{unknown}_{name}", Ctx())
            .Should().Be("{unknown}_DSC0001");
    }

    [Fact]
    public void NoRules_ReturnsPlaceholderReplaced()
    {
        _engine.Apply("{name}_{n}", Ctx(seq: 1))
            .Should().Be("DSC0001_1");
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        _engine.Apply("", Ctx())
            .Should().Be("");
    }

    // ============ 异常（2 用例）============

    [Fact]
    public void InvalidRegex_Throws_NamingRuleException()
    {
        var act = () => _engine.Apply(
            "input",
            Ctx(),
            new[] { new NamingRule { Pattern = "[invalid", IsRegex = true, Order = 0 } });
        act.Should().Throw<NamingRuleException>()
            .WithMessage("*正则规则无效*");
    }

    [Fact]
    public void InvalidDateFormat_Throws_NamingRuleException()
    {
        // unterminated single quote in DateTime format string → FormatException
        var act = () => _engine.Apply("{date:yyyy'}", Ctx());
        act.Should().Throw<NamingRuleException>()
            .WithMessage("*日期格式无效*");
    }
}
