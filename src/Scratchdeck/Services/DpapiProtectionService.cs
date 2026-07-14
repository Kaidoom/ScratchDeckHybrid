using System.Security.Cryptography;
using System.Text;

namespace Scratchdeck.Services;

public sealed class DpapiProtectionService : IProtectionService
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("Scratchdeck.Workspace.v1");

    public string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, OptionalEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedText)
    {
        var bytes = Convert.FromBase64String(protectedText);
        var plainBytes = ProtectedData.Unprotect(bytes, OptionalEntropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
