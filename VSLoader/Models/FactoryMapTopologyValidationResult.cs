namespace VSLoader.Models;

public sealed class FactoryMapTopologyValidationResult
{
    public FactoryMapTopologyValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
