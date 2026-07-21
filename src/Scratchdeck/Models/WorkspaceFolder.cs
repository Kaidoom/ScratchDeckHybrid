using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Scratchdeck.Models;

public sealed class WorkspaceFolder : INotifyPropertyChanged
{
    public const string DefaultFolderColor = "#19D9F0";

    private string _title = "Untitled Folder";
    private string _folderColor = DefaultFolderColor;
    private Brush _folderColorBrush = CreateFrozenBrush(DefaultFolderColor);
    private int _selectedTabIndex;
    private bool _isRenaming;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public ObservableCollection<TabDocument> Tabs { get; set; } = [];

    public string FolderColor
    {
        get => _folderColor;
        set
        {
            if (_folderColor == value)
            {
                return;
            }

            _folderColor = value;
            _folderColorBrush = CreateFrozenBrush(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FolderColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FolderColorBrush)));
        }
    }

    public Brush FolderColorBrush => _folderColorBrush;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
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

    private static Brush CreateFrozenBrush(string? value)
    {
        Color color;
        try
        {
            color = ColorConverter.ConvertFromString(value) is Color parsed
                ? parsed
                : Color.FromRgb(0x19, 0xD9, 0xF0);
        }
        catch (FormatException)
        {
            color = Color.FromRgb(0x19, 0xD9, 0xF0);
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
