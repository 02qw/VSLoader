using System.Text;
using System.Security.Cryptography;

namespace VSLoader.Services;

public sealed class PasswordProtectionService
{
    public string Protect(string password)
    {
        return password ?? string.Empty;
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
            return protectedPassword;
        }
    }
}
