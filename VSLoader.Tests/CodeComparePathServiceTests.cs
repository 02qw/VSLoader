using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class CodeComparePathServiceTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "VSLoader.Tests",
        Guid.NewGuid().ToString("N"));

    public CodeComparePathServiceTests()
    {
        Directory.CreateDirectory(rootPath);
    }

    [Fact]
    public void Resolve_combines_workspace_root_and_source_module_name()
    {
        var modulePath = Path.Combine(rootPath, "eap-sic-AMX-Sintering");
        Directory.CreateDirectory(modulePath);

        var success = CodeComparePathService.TryResolveLocalModulePath(
            rootPath,
            "eap-sic-AMX-Sintering",
            out var resolvedPath,
            out var errorMessage);

        Assert.True(success, errorMessage);
        Assert.Equal(modulePath, resolvedPath);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("..\\other")]
    [InlineData("folder\\module")]
    [InlineData("C:\\module")]
    public void Resolve_rejects_unsafe_module_names(string moduleName)
    {
        var success = CodeComparePathService.TryResolveLocalModulePath(
            rootPath,
            moduleName,
            out _,
            out var errorMessage);

        Assert.False(success);
        Assert.Contains("安全", errorMessage);
    }

    [Theory]
    [InlineData(@"config\deo", @"config\deo")]
    [InlineData("config/deo", @"config\deo")]
    public void NormalizeScope_accepts_relative_module_scope(string scope, string expected)
    {
        Assert.True(CodeComparePathService.TryNormalizeScope(scope, out var normalized, out var error));
        Assert.Equal(expected, normalized);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(@"..\config")]
    [InlineData(@"C:\config")]
    public void NormalizeScope_rejects_paths_outside_module(string scope)
    {
        Assert.False(CodeComparePathService.TryNormalizeScope(scope, out _, out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }
}
