namespace VSLoader.Models;

public sealed class ContextMenuCapabilityExecutionResult
{
    private ContextMenuCapabilityExecutionResult()
    {
    }

    public bool Success { get; init; }

    public bool Cancelled { get; init; }

    public bool Started { get; init; }

    public bool TimedOut { get; init; }

    public int? ExitCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public bool OutputTruncated { get; init; }

    public static ContextMenuCapabilityExecutionResult Ok(
        string message = "执行成功。",
        int? exitCode = null,
        string standardOutput = "",
        string standardError = "",
        bool started = true,
        bool outputTruncated = false)
    {
        return new ContextMenuCapabilityExecutionResult
        {
            Success = true,
            Started = started,
            ExitCode = exitCode,
            Message = message,
            StandardOutput = standardOutput,
            StandardError = standardError,
            OutputTruncated = outputTruncated
        };
    }

    public static ContextMenuCapabilityExecutionResult Fail(
        string message,
        int? exitCode = null,
        string standardOutput = "",
        string standardError = "",
        bool started = false,
        bool timedOut = false,
        bool outputTruncated = false)
    {
        return new ContextMenuCapabilityExecutionResult
        {
            Success = false,
            Started = started,
            TimedOut = timedOut,
            ExitCode = exitCode,
            Message = message,
            StandardOutput = standardOutput,
            StandardError = standardError,
            OutputTruncated = outputTruncated
        };
    }

    public static ContextMenuCapabilityExecutionResult Cancel(string message = "操作已取消。")
    {
        return new ContextMenuCapabilityExecutionResult
        {
            Success = false,
            Cancelled = true,
            Message = message
        };
    }
}
