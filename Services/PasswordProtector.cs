using System.Security.Cryptography;
using System.Text;

namespace VmeMusic.Services;

public sealed class PasswordProtector
{
    private const string PlainPrefix = "plain:";
    private const string DpapiPrefix = "dpapi:";

    public string Protect(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "";
        }

        if (OperatingSystem.IsWindows())
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return DpapiPrefix + Convert.ToBase64String(protectedBytes);
        }

        return PlainPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
    }

    public string Unprotect(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return "";
        }

        if (protectedPassword.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                return "";
            }

            var payload = Convert.FromBase64String(protectedPassword[DpapiPrefix.Length..]);
            var bytes = ProtectedData.Unprotect(payload, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }

        if (protectedPassword.StartsWith(PlainPrefix, StringComparison.Ordinal))
        {
            var payload = Convert.FromBase64String(protectedPassword[PlainPrefix.Length..]);
            return Encoding.UTF8.GetString(payload);
        }

        return "";
    }
}
