using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Scratchdeck.Models;

public sealed class TabDocument : INotifyPropertyChanged
{
    private string _title = "Untitled";
    private string _content = string.Empty;
    private string _syntaxMode = "Plain Text";
    private string _scratchData = string.Empty;
    private string _scratchBrushColor = "#19D9F0";
    private double _scratchBrushSize = 6;
    private bool _showLineNumbers = true;
    private bool _isProtected;
    private bool _isRenaming;
    private bool _isScratchMode;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }

    public string SyntaxMode
    {
        get => _syntaxMode;
        set => SetField(ref _syntaxMode, value);
    }

    public string ScratchData
    {
        get => _scratchData;
        set => SetField(ref _scratchData, value);
    }

    public string ScratchBrushColor
    {
        get => _scratchBrushColor;
        set => SetField(ref _scratchBrushColor, value);
    }

    public double ScratchBrushSize
    {
        get => _scratchBrushSize;
        set => SetField(ref _scratchBrushSize, value);
    }

    public bool IsScratchMode
    {
        get => _isScratchMode;
        set => SetField(ref _isScratchMode, value);
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set => SetField(ref _showLineNumbers, value);
    }

    public bool IsProtected
    {
        get => _isProtected;
        set => SetField(ref _isProtected, value);
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetField(ref _isRenaming, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
