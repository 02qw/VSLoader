namespace VSLoader.Services;

public enum AdminUiAutoLoginStatus
{
    InputSubmitted,
    FocusLostBeforeInput,
    FocusLostBeforeEnter,
    TimedOut,
    PasswordEmpty,
    InputFailed
}

public sealed class AdminUiAutoPasteResult
{
    private AdminUiAutoPasteResult(
        AdminUiAutoLoginStatus status,
        string message,
        ForegroundWindowInfo? matchedWindow)
    {
        Status = status;
        Message = message;
        MatchedWindow = matchedWindow;
    }

    public AdminUiAutoLoginStatus Status { get; }

    public bool Success => Status == AdminUiAutoLoginStatus.InputSubmitted;

    public string Message { get; }

    public ForegroundWindowInfo? MatchedWindow { get; }

    public static AdminUiAutoPasteResult InputSubmitted(ForegroundWindowInfo matchedWindow)
    {
        return new AdminUiAutoPasteResult(
            AdminUiAutoLoginStatus.InputSubmitted,
            "AdminUI 登录信息已自动填写并确认。",
            matchedWindow);
    }

    public static AdminUiAutoPasteResult FocusLostBeforeInput(ForegroundWindowInfo matchedWindow)
    {
        return new AdminUiAutoPasteResult(
            AdminUiAutoLoginStatus.FocusLostBeforeInput,
            "登录窗口已失焦，已停止自动登录。",
            matchedWindow);
    }

    public static AdminUiAutoPasteResult FocusLostBeforeEnter(ForegroundWindowInfo matchedWindow)
    {
        return new AdminUiAutoPasteResult(
            AdminUiAutoLoginStatus.FocusLostBeforeEnter,
            "密码已填写，但登录窗口已失焦，未发送确认键。",
            matchedWindow);
    }

    public static AdminUiAutoPasteResult TimedOut()
    {
        return new AdminUiAutoPasteResult(
            AdminUiAutoLoginStatus.TimedOut,
            "未检测到前台 AdminUI 登录窗口。",
            null);
    }

    public static AdminUiAutoPasteResult PasswordEmpty()
    {
        return new AdminUiAutoPasteResult(
            AdminUiAutoLoginStatus.PasswordEmpty,
            "自动输入密码失败：密码为空。",
            null);
    }

    public static AdminUiAutoPasteResult InputFailed(string message, ForegroundWindowInfo? matchedWindow = null)
    {
        return new AdminUiAutoPasteResult(
            AdminUiAutoLoginStatus.InputFailed,
            message,
            matchedWindow);
    }
}
