namespace Scratchdeck.Models;

public sealed class WindowPlacement
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 860;
    public double Height { get; set; } = 480;
    public bool WasMaximized { get; set; }
}
