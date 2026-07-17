namespace VSLoader.Models;

public sealed record GlobalConfigOperationProgress(
    int Value,
    string Message,
    string CurrentItem = "");
