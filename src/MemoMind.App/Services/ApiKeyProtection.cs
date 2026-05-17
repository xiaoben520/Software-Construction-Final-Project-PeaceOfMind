using System.Security.Cryptography;
using System.Text;

namespace MemoMind.App.Services;

public static class ApiKeyProtection
{
    private const string DpapiPrefix = "DPAPI:";
    private static readonly byte[] Entropy = "MemoMind.ApiKey.2026"u8.ToArray();

    public static string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return plainText;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return DpapiPrefix + Convert.ToBase64String(encryptedBytes);
    }

    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
            return protectedText;

        if (!protectedText.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            return protectedText;

        try
        {
            var base64 = protectedText[DpapiPrefix.Length..];
            var encryptedBytes = Convert.FromBase64String(base64);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // If decryption fails (e.g. copied from another machine), return empty
            return string.Empty;
        }
    }
}
