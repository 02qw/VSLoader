using System.Security.Cryptography;
using System.Text;

namespace VSLoader.Services;

public sealed class PasswordProtectionService
{
    public string Protect(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return string.Empty;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var protectedBytes = ProtectedData.Protect(passwordBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return string.Empty;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedPassword);
            var passwordBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(passwordBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
