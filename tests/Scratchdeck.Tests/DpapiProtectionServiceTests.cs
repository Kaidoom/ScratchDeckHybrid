using Scratchdeck.Services;

namespace Scratchdeck.Tests;

public sealed class DpapiProtectionServiceTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsForCurrentWindowsUser()
    {
        var service = new DpapiProtectionService();
        const string plainText = "local secret / 42 / ∆";

        var protectedText = service.Protect(plainText);
        var restoredText = service.Unprotect(protectedText);

        Assert.NotEqual(plainText, protectedText);
        Assert.Equal(plainText, restoredText);
    }
}
