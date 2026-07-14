namespace Scratchdeck.Services;

public interface IProtectionService
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
