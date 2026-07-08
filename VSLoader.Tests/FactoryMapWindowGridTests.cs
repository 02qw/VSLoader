using System.Windows;
using VSLoader.Views;

namespace VSLoader.Tests;

public sealed class FactoryMapWindowGridTests
{
    [Fact]
    public void GridSizes_FollowSnapGridSize()
    {
        Assert.Equal(10, FactoryMapWindow.MapGridSize);
        Assert.Equal(50, FactoryMapWindow.MapMajorGridSize);
    }

    [Fact]
    public void ToSquareBounds_WideBoundsExpandsHeightAndKeepsCenter()
    {
        var square = FactoryMapWindow.ToSquareBounds(new Rect(10, 20, 200, 80));

        Assert.Equal(square.Width, square.Height);
        Assert.Equal(110, square.Left + square.Width / 2);
        Assert.Equal(60, square.Top + square.Height / 2);
    }

    [Fact]
    public void ToSquareBounds_TallBoundsExpandsWidthAndKeepsCenter()
    {
        var square = FactoryMapWindow.ToSquareBounds(new Rect(10, 20, 80, 200));

        Assert.Equal(square.Width, square.Height);
        Assert.Equal(50, square.Left + square.Width / 2);
        Assert.Equal(120, square.Top + square.Height / 2);
    }

    [Fact]
    public void CalculateMapCanvasSize_EditModeAddsRightAndBottomBuffer()
    {
        var size = FactoryMapWindow.CalculateMapCanvasSize(
            baseWidth: 580,
            baseHeight: 360,
            contentRight: 250,
            contentBottom: 160,
            isEditMode: true);

        Assert.Equal(750, size.Width);
        Assert.Equal(660, size.Height);
    }

    [Fact]
    public void CalculateMapCanvasSize_BrowseModeKeepsCompactPadding()
    {
        var size = FactoryMapWindow.CalculateMapCanvasSize(
            baseWidth: 580,
            baseHeight: 360,
            contentRight: 700,
            contentBottom: 500,
            isEditMode: false);

        Assert.Equal(728, size.Width);
        Assert.Equal(528, size.Height);
    }
}
