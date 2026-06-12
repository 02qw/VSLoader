using VSLoader;

namespace VSLoader.Tests;

public sealed class InitialLayoutRestoreGuardTests
{
    [Fact]
    public void AllowsSavingAfterInitialRestoreCompletes()
    {
        var guard = new InitialLayoutRestoreGuard();

        Assert.False(guard.CanSaveWindowBounds);

        guard.Complete();

        Assert.True(guard.CanSaveWindowBounds);
    }

    [Fact]
    public void CompleteCanBeCalledMoreThanOnce()
    {
        var guard = new InitialLayoutRestoreGuard();

        guard.Complete();
        guard.Complete();

        Assert.True(guard.CanSaveWindowBounds);
    }
}
