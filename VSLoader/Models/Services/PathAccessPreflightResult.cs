namespace VSLoader.Services;

public sealed record PathAccessPreflightResult(bool Success, string? ErrorMessage)
{
    public static PathAccessPreflightResult Ok()
    {
        return new PathAccessPreflightResult(true, null);
    }

    public static PathAccessPreflightResult Fail(string message)
    {
        return new PathAccessPreflightResult(false, message);
    }
}
