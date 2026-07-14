using Scratchdeck.Services;

namespace Scratchdeck.Tests;

public sealed class SyntaxHighlightingServiceTests
{
    [Fact]
    public void EveryAdvertisedMode_CreatesAValidDefinition()
    {
        foreach (var mode in SyntaxHighlightingService.Modes)
        {
            var definition = SyntaxHighlightingService.GetDefinition(mode);
            if (mode == "Plain Text")
            {
                Assert.Null(definition);
            }
            else
            {
                Assert.NotNull(definition);
                Assert.Equal(mode, definition.Name);
            }
        }
    }
}
