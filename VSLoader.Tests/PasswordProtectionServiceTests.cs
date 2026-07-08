using System.Security.Cryptography;
using System.Text;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class PasswordProtectionServiceTests
{
    [Fact]
    public void Protect_keeps_adminui_password_as_plain_text()
    {
        var service = new PasswordProtectionService();

        var stored = service.Protect("admin123");

        Assert.Equal("admin123", stored);
    }

    [Fact]
    public void Unprotect_reads_plain_text_password()
    {
        var service = new PasswordProtectionService();

        var password = service.Unprotect("admin123");

        Assert.Equal("admin123", password);
    }

    [Fact]
    public void Unprotect_still_reads_legacy_protected_password()
    {
        var service = new PasswordProtectionService();
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes("legacy123"), null, DataProtectionScope.CurrentUser);
        var legacyStoredPassword = Convert.ToBase64String(protectedBytes);

        var password = service.Unprotect(legacyStoredPassword);

        Assert.Equal("legacy123", password);
    }
}
