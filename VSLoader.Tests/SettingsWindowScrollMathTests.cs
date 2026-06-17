using VSLoader.Views;

namespace VSLoader.Tests;

public sealed class SettingsWindowScrollMathTests
{
    [Theory]
    [InlineData(500, 120, 1000, 380)]
    [InlineData(500, -120, 1000, 620)]
    [InlineData(50, 120, 1000, 0)]
    [InlineData(950, -120, 1000, 1000)]
    [InlineData(500, 0, 1000, 500)]
    public void CalculateWheelScrollOffset_clamps_target_offset(double currentOffset, int delta, double scrollableHeight, double expected)
    {
        var result = SettingsWindow.CalculateWheelScrollOffset(currentOffset, delta, scrollableHeight);

        Assert.Equal(expected, result);
    }
}
