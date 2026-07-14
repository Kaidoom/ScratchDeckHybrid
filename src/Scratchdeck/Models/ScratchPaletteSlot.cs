using System.Windows.Media;

namespace Scratchdeck.Models;

public sealed class ScratchPaletteSlot
{
    public ScratchPaletteSlot(int index, string hex)
    {
        Index = index;
        Hex = hex;
        var brush = new SolidColorBrush(
            ColorConverter.ConvertFromString(hex) is Color color ? color : Colors.White);
        brush.Freeze();
        PreviewBrush = brush;
    }

    public int Index { get; }
    public string Hex { get; }
    public Brush PreviewBrush { get; }
}
