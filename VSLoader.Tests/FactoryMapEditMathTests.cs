using System.Windows;
using VSLoader.Models;

namespace VSLoader.Tests;

public sealed class FactoryMapEditMathTests
{
    [Theory]
    [InlineData(327.42, 330)]
    [InlineData(324.2, 320)]
    [InlineData(325, 330)]
    public void SnapToGrid_rounds_to_nearest_grid(double input, double expected)
    {
        Assert.Equal(expected, FactoryMapEditMath.SnapToGrid(input, 10));
    }

    [Fact]
    public void ClampAndSnapToGrid_never_returns_negative_position()
    {
        Assert.Equal(0, FactoryMapEditMath.ClampAndSnapToGrid(-3, 10));
    }

    [Fact]
    public void RectIntersects_returns_true_when_selection_touches_node()
    {
        var selection = new Rect(90, 90, 80, 80);
        var node = new Rect(150, 150, 150, 58);

        Assert.True(FactoryMapEditMath.RectIntersects(selection, node));
    }

    [Fact]
    public void RectIntersects_returns_false_when_rectangles_are_separated()
    {
        var selection = new Rect(0, 0, 50, 50);
        var node = new Rect(150, 150, 150, 58);

        Assert.False(FactoryMapEditMath.RectIntersects(selection, node));
    }

    [Fact]
    public void ApplySnappedDelta_moves_multiple_nodes_and_preserves_relative_spacing()
    {
        var positions = new Dictionary<string, Point>
        {
            ["A"] = new(100, 100),
            ["B"] = new(200, 100)
        };

        var result = FactoryMapEditMath.ApplySnappedDelta(positions, new Vector(35, 15), 10);

        Assert.Equal(new Point(140, 120), result["A"]);
        Assert.Equal(new Point(240, 120), result["B"]);
    }
}
