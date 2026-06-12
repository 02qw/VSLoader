using System.Windows;
using VSLoader.Models;

namespace VSLoader.Tests;

public sealed class FactoryMapViewportFitCalculatorTests
{
    [Fact]
    public void Calculate_FitsWideContentInsideSmallViewport()
    {
        var result = FactoryMapViewportFitCalculator.Calculate(
            viewportWidth: 605,
            viewportHeight: 609,
            contentBounds: new Rect(32, 1, 1248, 841),
            padding: 36);

        Assert.True(result.Scale < 1);
        Assert.True(result.Scale > 0);

        var visibleLeft = 32 * result.Scale + result.OffsetX;
        var visibleRight = (32 + 1248) * result.Scale + result.OffsetX;
        var visibleTop = 1 * result.Scale + result.OffsetY;
        var visibleBottom = (1 + 841) * result.Scale + result.OffsetY;

        Assert.True(visibleLeft >= 0);
        Assert.True(visibleTop >= 0);
        Assert.True(visibleRight <= 605);
        Assert.True(visibleBottom <= 609);
    }
}
