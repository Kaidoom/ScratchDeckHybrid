using System.IO;
using System.Windows.Ink;

namespace Scratchdeck.Services;

/// <summary>
/// Converts WPF ink strokes to and from a JSON-friendly Base64 ISF payload.
/// </summary>
public static class InkStrokeSerializationService
{
    public static string Serialize(StrokeCollection? strokes)
    {
        if (strokes is null || strokes.Count == 0)
        {
            return string.Empty;
        }

        using var stream = new MemoryStream();
        strokes.Save(stream, compress: true);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static StrokeCollection Deserialize(string? encodedStrokes)
    {
        if (string.IsNullOrWhiteSpace(encodedStrokes))
        {
            return new StrokeCollection();
        }

        try
        {
            var bytes = Convert.FromBase64String(encodedStrokes);
            if (bytes.Length == 0)
            {
                return new StrokeCollection();
            }

            using var stream = new MemoryStream(bytes, writable: false);
            return new StrokeCollection(stream);
        }
        catch (FormatException)
        {
            return new StrokeCollection();
        }
        catch (ArgumentException)
        {
            return new StrokeCollection();
        }
        catch (IOException)
        {
            return new StrokeCollection();
        }
        catch (InvalidOperationException)
        {
            return new StrokeCollection();
        }
        catch (NotSupportedException)
        {
            return new StrokeCollection();
        }
    }
}
