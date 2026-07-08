using VSLoader.Views;

namespace VSLoader.Tests;

public sealed class FactoryMapStatusTextTests
{
    [Fact]
    public void FormatDebugStatusText_uses_chinese_parameter_names()
    {
        var text = FactoryMapWindow.FormatDebugStatusText("设备：53  |  连线：40", 1918, 1033, 1.229, "2080x1168");

        Assert.Equal("设备：53  |  连线：40 | 视口:1918x1033 | 缩放:1.229 | 边界:2080x1168", text);
        Assert.DoesNotContain("vp:", text);
        Assert.DoesNotContain("scale:", text);
        Assert.DoesNotContain("bounds:", text);
    }
}
