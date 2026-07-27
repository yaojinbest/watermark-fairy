using FluentAssertions;
using WatermarkFairy.Models;
using Xunit;

namespace WatermarkFairy.Tests;

public class WatermarkConfigTests
{
    [Fact]
    public void DefaultConfig_ShouldHaveSensibleDefaults()
    {
        var config = new WatermarkConfig();

        config.Text.Should().Be("© Watermark Fairy");
        config.FontSize.Should().Be(24);
        config.Position.Should().Be(WatermarkPosition.BottomRight);
        config.Opacity.Should().Be(0.8);
    }

    [Theory]
    [InlineData(WatermarkPosition.TopLeft)]
    [InlineData(WatermarkPosition.BottomRight)]
    [InlineData(WatermarkPosition.MiddleCenter)]
    public void Position_ShouldBeSettable(WatermarkPosition pos)
    {
        var config = new WatermarkConfig { Position = pos };
        config.Position.Should().Be(pos);
    }
}
