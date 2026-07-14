using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Scratchdeck.Models;

public sealed class ThemeColorField : INotifyPropertyChanged
{
    private string _value;

    public ThemeColorField(string key, string label, string value)
    {
        Key = key;
        Label = label;
        _value = value;
    }

    public string Key { get; }
    public string Label { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewBrush));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public bool IsValid => TryParseColor(Value, out _);

    public Brush PreviewBrush => TryParseColor(Value, out var color)
        ? new SolidColorBrush(color)
        : Brushes.Transparent;

    public event PropertyChangedEventHandler? PropertyChanged;

    private static bool TryParseColor(string? value, out Color color)
    {
        var candidate = value?.Trim();
        if (candidate is null ||
            (candidate.Length != 7 && candidate.Length != 9) ||
            candidate[0] != '#' ||
            candidate.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            color = Colors.Transparent;
            return false;
        }

        try
        {
            if (ColorConverter.ConvertFromString(candidate) is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
        }

        color = Colors.Transparent;
        return false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
