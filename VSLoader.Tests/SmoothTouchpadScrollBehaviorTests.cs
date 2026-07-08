using VSLoader.Behaviors;

namespace VSLoader.Tests;

public sealed class SmoothTouchpadScrollBehaviorTests
{
    [Theory]
    [InlineData(120, false)]
    [InlineData(-120, false)]
    [InlineData(30, true)]
    [InlineData(-30, true)]
    [InlineData(0, false)]
    public void IsFineGrainedVerticalDelta_only_treats_small_nonzero_delta_as_touchpad(int delta, bool expected)
    {
        Assert.Equal(expected, SmoothTouchpadScrollBehavior.IsFineGrainedVerticalDelta(delta));
    }

    [Theory]
    [InlineData(50, 20, 1, 0, 100, 30)]
    [InlineData(50, -20, 1, 0, 100, 70)]
    [InlineData(5, 20, 1, 0, 100, 0)]
    [InlineData(95, -20, 1, 0, 100, 100)]
    public void CalculateTargetOffset_applies_delta_and_clamps(
        double current,
        int delta,
        double sensitivity,
        double min,
        double max,
        double expected)
    {
        Assert.Equal(expected, SmoothTouchpadScrollBehavior.CalculateTargetOffset(current, delta, sensitivity, min, max));
    }

    [Fact]
    public void CalculateTargetOffset_supports_low_sensitivity_for_logical_item_scrolling()
    {
        Assert.Equal(48.8, SmoothTouchpadScrollBehavior.CalculateTargetOffset(50, 30, 0.04, 0, 100), precision: 3);
    }

    [Theory]
    [InlineData(50, 20, 1, 0, 100, 70)]
    [InlineData(50, -20, 1, 0, 100, 30)]
    [InlineData(95, 20, 1, 0, 100, 100)]
    [InlineData(5, -20, 1, 0, 100, 0)]
    public void CalculateHorizontalTargetOffset_follows_horizontal_delta_direction(
        double current,
        int delta,
        double sensitivity,
        double min,
        double max,
        double expected)
    {
        Assert.Equal(expected, SmoothTouchpadScrollBehavior.CalculateHorizontalTargetOffset(current, delta, sensitivity, min, max));
    }
}
