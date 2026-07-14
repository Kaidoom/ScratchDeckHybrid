using Scratchdeck.Services;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

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

    [Fact]
    public void EveryAdvertisedMode_HighlightsUnexpectedMixedContentWithoutThrowing()
    {
        const string content = """
            <video aria-label="GIF" style="max-width: 317px; width: 100%;" tabindex="-1">
            # PowerShell-like content inside an HTML-shaped note
            **markdown** [link](https://example.test) $value = @{ enabled = $true }
            { "unterminated": "string with <!-- mixed tokens and __ markers" }
            """;

        foreach (var mode in SyntaxHighlightingService.Modes.Where(mode => mode != "Plain Text"))
        {
            var exception = Record.Exception(() =>
            {
                var definition = SyntaxHighlightingService.GetDefinition(mode)!;
                var document = new TextDocument(content);
                using var highlighter = new DocumentHighlighter(document, definition);
                for (var line = 1; line <= document.LineCount; line++)
                {
                    highlighter.HighlightLine(line);
                }
            });

            Assert.True(exception is null, $"{mode} failed to highlight mixed content: {exception}");
        }
    }
}
