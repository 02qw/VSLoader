using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class CriticalInputOverlayServiceTests
{
    [Fact]
    public void ApplyNoActivateExtendedStyle_adds_no_activate_flag_without_removing_existing_flags()
    {
        var handle = new IntPtr(123);
        var existingStyle = 0x00000080;
        var setStyles = new List<int>();

        var updatedStyle = CriticalInputOverlayService.ApplyNoActivateExtendedStyle(
            handle,
            (actualHandle, index) =>
            {
                Assert.Equal(handle, actualHandle);
                Assert.Equal(CriticalInputOverlayService.ExtendedWindowStyleIndex, index);
                return existingStyle;
            },
            (actualHandle, index, style) =>
            {
                Assert.Equal(handle, actualHandle);
                Assert.Equal(CriticalInputOverlayService.ExtendedWindowStyleIndex, index);
                setStyles.Add(style);
                return style;
            });

        var expectedStyle = existingStyle | CriticalInputOverlayService.NoActivateExtendedWindowStyle;
        Assert.Equal(expectedStyle, updatedStyle);
        Assert.Equal([expectedStyle], setStyles);
    }

    [Fact]
    public void ApplyNoActivateExtendedStyle_ignores_empty_handle()
    {
        var getCalled = false;
        var setCalled = false;

        var updatedStyle = CriticalInputOverlayService.ApplyNoActivateExtendedStyle(
            IntPtr.Zero,
            (_, _) =>
            {
                getCalled = true;
                return 0;
            },
            (_, _, style) =>
            {
                setCalled = true;
                return style;
            });

        Assert.Equal(0, updatedStyle);
        Assert.False(getCalled);
        Assert.False(setCalled);
    }
}
