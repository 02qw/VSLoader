namespace VSLoader.Models;

public static class ContextMenuBuiltInActionIds
{
    public const string OpenVsCode = "builtin.open-vscode";
    public const string OpenWebUi = "builtin.open-webui";
    public const string OpenAdminUi = "builtin.open-adminui";
    public const string DownloadAdminUiLink = "builtin.download-adminui-link";

    public static IReadOnlyList<string> All { get; } =
    [
        OpenVsCode,
        OpenWebUi,
        OpenAdminUi,
        DownloadAdminUiLink
    ];

    public static string GetDisplayName(string actionId)
    {
        return actionId switch
        {
            OpenVsCode => "VSCode",
            OpenWebUi => "WebUI",
            OpenAdminUi => "AdminUI",
            DownloadAdminUiLink => "获取AdminUI连接",
            _ => actionId
        };
    }
}
