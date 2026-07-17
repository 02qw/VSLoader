using System.Diagnostics;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class WebCapabilityExecutor
{
    private readonly ContextMenuUrlTemplateService urlTemplateService;
    private readonly Func<string, bool> openUrl;

    public WebCapabilityExecutor()
        : this(new ContextMenuUrlTemplateService(), OpenWithDefaultBrowser)
    {
    }

    internal WebCapabilityExecutor(ContextMenuUrlTemplateService urlTemplateService, Func<string, bool> openUrl)
    {
        this.urlTemplateService = urlTemplateService;
        this.openUrl = openUrl;
    }

    public Task<ContextMenuCapabilityExecutionResult> ExecuteAsync(
        ContextMenuCapabilityDefinition definition,
        ContextMenuCapabilityExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ContextMenuCapabilityExecutionResult.Cancel());
        }

        try
        {
            var result = urlTemplateService.Build(definition.Web?.UrlTemplate, context);
            if (!result.Success)
            {
                return Task.FromResult(ContextMenuCapabilityExecutionResult.Fail(result.ErrorMessage));
            }

            return Task.FromResult(openUrl(result.Url)
                ? ContextMenuCapabilityExecutionResult.Ok("网页已使用系统默认浏览器打开。")
                : ContextMenuCapabilityExecutionResult.Fail("系统默认浏览器未能启动。"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ContextMenuCapabilityExecutionResult.Fail($"打开网页失败：{ex.Message}"));
        }
    }

    private static bool OpenWithDefaultBrowser(string url)
    {
        var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return process is not null;
    }
}
