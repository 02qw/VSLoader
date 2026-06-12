namespace VSLoader;

internal sealed class InitialLayoutRestoreGuard
{
    private bool isRestoring = true;

    public bool CanSaveWindowBounds => !isRestoring;

    public void Complete()
    {
        isRestoring = false;
    }
}
