using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Scratchdeck.Models;
using Scratchdeck.Services;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace Scratchdeck;

public partial class MainWindow : Window
{
    private readonly WorkspaceState _state;
    private readonly WorkspacePersistenceService _persistence;
    private CancellationTokenSource? _autosaveCancellation;
    private TabDocument? _activeTab;
    private TabDocument? _draggedTab;
    private WpfPoint _dragStart;
    private string _renameOriginalTitle = string.Empty;
    private bool _isLoadingEditor;
    private bool _isReady;
    private bool _isClosing;
    private bool _allowClose;

    public MainWindow(WorkspaceState state, WorkspacePersistenceService persistence)
    {
        _state = state;
        _persistence = persistence;
        _state.Normalize();

        InitializeComponent();

        _isLoadingEditor = true;
        DataContext = _state;
        SyntaxCombo.ItemsSource = SyntaxHighlightingService.Modes;
        ThemeCombo.ItemsSource = ThemeService.Themes;
        ThemeCombo.SelectedItem = _state.Theme;
        TabsList.SelectedIndex = _state.SelectedTabIndex;
        Topmost = _state.Topmost;
        PinToggle.IsChecked = _state.Topmost;
        WrapToggle.IsChecked = _state.AutoWrap;

        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = 4;
        Editor.Options.EnableTextDragDrop = true;
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.AllowScrollBelowDocument = true;
        ApplyWordWrap();
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateEditorStatus();

        _activeTab = _state.Tabs[_state.SelectedTabIndex];
        LoadActiveTab();
        _isLoadingEditor = false;
        UpdateEditorTheme();
        UpdateEditorStatus();
        UpdateWindowStateGlyph();

        Loaded += (_, _) =>
        {
            _isReady = true;
            Editor.Focus();
        };
    }

    public void RestoreAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        var wasTopmost = Topmost;
        Topmost = true;
        Topmost = wasTopmost;
        Focus();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyWindowPlacement();
    }

    private void ApplyWindowPlacement()
    {
        Width = _state.Window.Width;
        Height = _state.Window.Height;

        var work = MonitorWorkAreaService.GetNearestWorkingArea(
            _state.Window.Left,
            _state.Window.Top,
            Width,
            Height);
        var workLeft = work.Left;
        var workTop = work.Top;
        var workWidth = work.Width;
        var workHeight = work.Height;

        if (double.IsNaN(_state.Window.Left) || double.IsNaN(_state.Window.Top))
        {
            Left = workLeft + Math.Max(0, (workWidth - Width) / 2);
            Top = workTop + Math.Max(0, (workHeight - Height) / 2);
        }
        else
        {
            Left = Math.Clamp(_state.Window.Left, workLeft, Math.Max(workLeft, workLeft + workWidth - Width));
            Top = Math.Clamp(_state.Window.Top, workTop, Math.Max(workTop, workTop + workHeight - Height));
        }

        if (_state.Window.WasMaximized)
        {
            Dispatcher.BeginInvoke(() => WindowState = WindowState.Maximized, DispatcherPriority.Loaded);
        }
    }

    private void Window_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && e.Key == Key.T)
        {
            CreateNewTab();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.W)
        {
            CloseTab(_activeTab);
            e.Handled = true;
        }
        else if (modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Tab)
        {
            CycleTabs(modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            OpenSearch();
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
        {
            PinToggle.IsChecked = !(PinToggle.IsChecked ?? false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchPanel.Visibility == Visibility.Visible)
        {
            CloseSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            FindMatch(!modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    private void CreateNewTab()
    {
        var tab = new TabDocument { Title = GetNewTabTitle() };
        _state.Tabs.Add(tab);
        TabsList.SelectedItem = tab;
        ScheduleAutosave();
        Dispatcher.BeginInvoke(Editor.Focus, DispatcherPriority.Input);
    }

    private string GetNewTabTitle()
    {
        var index = 1;
        string title;
        do
        {
            title = $"NOTE {index++:00}";
        }
        while (_state.Tabs.Any(tab => tab.Title.Equals(title, StringComparison.OrdinalIgnoreCase)));

        return title;
    }

    private void CloseTab(TabDocument? tab)
    {
        if (tab is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(tab.Content))
        {
            var result = MessageBox.Show(
                this,
                $"Close '{tab.Title}'? Its content will be removed from the workspace.",
                "Close tab",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var index = _state.Tabs.IndexOf(tab);
        _state.Tabs.Remove(tab);
        if (_state.Tabs.Count == 0)
        {
            _state.Tabs.Add(new TabDocument { Title = "QUICK NOTE" });
        }

        TabsList.SelectedIndex = Math.Min(index, _state.Tabs.Count - 1);
        ScheduleAutosave();
    }

    private void CycleTabs(int direction)
    {
        if (_state.Tabs.Count < 2)
        {
            return;
        }

        var next = (TabsList.SelectedIndex + direction + _state.Tabs.Count) % _state.Tabs.Count;
        TabsList.SelectedIndex = next;
        TabsList.ScrollIntoView(TabsList.SelectedItem);
    }

    private void LoadActiveTab()
    {
        if (_activeTab is null)
        {
            return;
        }

        _isLoadingEditor = true;
        Editor.Text = _activeTab.Content;
        Editor.ShowLineNumbers = _activeTab.ShowLineNumbers;
        SyntaxCombo.SelectedItem = _activeTab.SyntaxMode;
        LineNumbersToggle.IsChecked = _activeTab.ShowLineNumbers;
        ProtectedToggle.IsChecked = _activeTab.IsProtected;
        ApplySyntaxHighlighting();
        Editor.CaretOffset = 0;
        _isLoadingEditor = false;
        UpdateEditorStatus();
        UpdateEmptyHint();
    }

    private void ApplySyntaxHighlighting()
    {
        if (_activeTab is null)
        {
            return;
        }

        try
        {
            if (SyntaxHighlightingService.TryGetDefinition(
                    _activeTab.SyntaxMode,
                    Editor.Text,
                    out var definition,
                    out _))
            {
                Editor.SyntaxHighlighting = definition;
                ModeStatus.Text = string.Empty;
                SyntaxCombo.ToolTip = null;
                return;
            }
        }
        catch
        {
            // The fallback below keeps editing available even if AvalonEdit rejects a definition.
        }

        try
        {
            Editor.SyntaxHighlighting = null;
        }
        catch
        {
            // Plain text is the final safe state; avoid surfacing a renderer exception.
        }

        ModeStatus.Text = "HIGHLIGHTING OFF";
        SyntaxCombo.ToolTip = "Highlighting is unavailable for this content. Editing continues as plain text.";
    }

    private void ApplyWordWrap()
    {
        Editor.WordWrap = _state.AutoWrap;
        Editor.HorizontalScrollBarVisibility = _state.AutoWrap
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }

    private void UpdateEditorTheme()
    {
        if (TryFindResource("SelectionBrush") is Brush selection)
        {
            Editor.TextArea.SelectionBrush = selection;
        }

        if (TryFindResource("EditorForegroundBrush") is Brush foreground)
        {
            Editor.TextArea.Caret.CaretBrush = foreground;
        }
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_isLoadingEditor || _activeTab is null)
        {
            return;
        }

        _activeTab.Content = Editor.Text;
        UpdateEditorStatus();
        UpdateEmptyHint();
        ScheduleAutosave();
    }

    private void UpdateEditorStatus()
    {
        var line = Editor.TextArea.Caret.Line;
        var column = Editor.TextArea.Caret.Column;
        CursorStatus.Text = $"LN {line}  COL {column}";
        CharacterStatus.Text = $"{Editor.Text.Length:N0} CHARS";
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = string.IsNullOrEmpty(Editor.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScheduleAutosave()
    {
        if (!_isReady || _isClosing)
        {
            return;
        }

        _autosaveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _autosaveCancellation = cancellation;
        SetSaveState("PENDING", "AccentBrush");
        SaveAfterDelayAsync(cancellation.Token);
    }

    private async void SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(400, cancellationToken);
            UpdatePlacementState();
            SetSaveState("SAVING", "AccentBrush");
            await _persistence.SaveAsync(_state, cancellationToken);
            SetSaveState("AUTOSAVED", "SuccessBrush");
        }
        catch (OperationCanceledException)
        {
            // A newer edit restarted the debounce window.
        }
        catch
        {
            SetSaveState("SAVE ERROR", "DangerBrush");
        }
    }

    private void SetSaveState(string text, string brushResource)
    {
        SaveStatus.Text = text;
        SaveIndicator.SetResourceReference(Shape.FillProperty, brushResource);
    }

    private void UpdatePlacementState()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;

        if (bounds.Width >= MinWidth && bounds.Height >= MinHeight)
        {
            _state.Window.Left = bounds.Left;
            _state.Window.Top = bounds.Top;
            _state.Window.Width = bounds.Width;
            _state.Window.Height = bounds.Height;
        }

        _state.Window.WasMaximized = WindowState == WindowState.Maximized;
        _state.SelectedTabIndex = Math.Max(0, TabsList.SelectedIndex);
        _state.Topmost = Topmost;
    }

    private void OpenSearch()
    {
        SearchPanel.Visibility = Visibility.Visible;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void CloseSearch()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        SearchFeedback.Text = string.Empty;
        Editor.Focus();
    }

    private void FindMatch(bool forward, bool restart = false)
    {
        var query = SearchBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            SearchFeedback.Text = string.Empty;
            return;
        }

        var text = Editor.Text;
        if (text.Length == 0)
        {
            SearchFeedback.Text = "NONE";
            return;
        }

        int index;
        if (forward)
        {
            var start = restart ? 0 : Math.Min(text.Length, Editor.SelectionStart + Editor.SelectionLength);
            index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0 && start > 0)
            {
                index = text.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            var start = restart
                ? text.Length - 1
                : Math.Min(text.Length - 1, Math.Max(0, Editor.SelectionStart - 1));
            index = text.LastIndexOf(query, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0 && start < text.Length - 1)
            {
                index = text.LastIndexOf(query, text.Length - 1, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (index < 0)
        {
            SearchFeedback.Text = "NONE";
            return;
        }

        SearchFeedback.Text = string.Empty;
        Editor.Select(index, query.Length);
        var line = Editor.Document.GetLineByOffset(index).LineNumber;
        Editor.ScrollToLine(line);
    }

    private void TabsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingEditor || TabsList.SelectedItem is not TabDocument selected)
        {
            return;
        }

        if (_activeTab is not null)
        {
            _activeTab.Content = Editor.Text;
        }

        _activeTab = selected;
        _state.SelectedTabIndex = TabsList.SelectedIndex;
        LoadActiveTab();
        ScheduleAutosave();
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e) => CreateNewTab();

    private void TabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TabDocument tab })
        {
            CloseTab(tab);
            e.Handled = true;
        }
    }

    private void TabHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 ||
            FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null ||
            sender is not FrameworkElement { DataContext: TabDocument tab })
        {
            return;
        }

        _renameOriginalTitle = tab.Title;
        tab.IsRenaming = true;
        Dispatcher.BeginInvoke(() =>
        {
            if (TabsList.ItemContainerGenerator.ContainerFromItem(tab) is ListBoxItem container &&
                FindVisualChild<TextBox>(container, "RenameTextBox") is { } textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }, DispatcherPriority.Input);
        e.Handled = true;
    }

    private void RenameTextBox_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: TabDocument tab })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitRename(tab);
            Editor.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tab.Title = _renameOriginalTitle;
            tab.IsRenaming = false;
            Editor.Focus();
            e.Handled = true;
        }
    }

    private void RenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: TabDocument { IsRenaming: true } tab })
        {
            CommitRename(tab);
        }
    }

    private void CommitRename(TabDocument tab)
    {
        tab.Title = string.IsNullOrWhiteSpace(tab.Title) ? _renameOriginalTitle : tab.Title.Trim();
        if (string.IsNullOrWhiteSpace(tab.Title))
        {
            tab.Title = "Untitled";
        }
        tab.IsRenaming = false;
        ScheduleAutosave();
    }

    private void TabsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(TabsList);
        _draggedTab = null;
        var source = e.OriginalSource as DependencyObject;
        if (FindAncestor<Button>(source) is not null || FindAncestor<TextBox>(source) is not null)
        {
            return;
        }

        if (FindAncestor<ListBoxItem>(source) is { DataContext: TabDocument tab })
        {
            _draggedTab = tab;
        }
    }

    private void TabsList_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_draggedTab is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var point = e.GetPosition(TabsList);
        if (Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(TabsList, new DataObject(typeof(TabDocument), _draggedTab), DragDropEffects.Move);
        _draggedTab = null;
    }

    private void TabsList_Drop(object sender, WpfDragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TabDocument)) ||
            e.Data.GetData(typeof(TabDocument)) is not TabDocument dragged)
        {
            return;
        }

        var oldIndex = _state.Tabs.IndexOf(dragged);
        var targetContainer = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        var targetIndex = targetContainer?.DataContext is TabDocument target
            ? _state.Tabs.IndexOf(target)
            : _state.Tabs.Count - 1;

        if (targetContainer is not null && e.GetPosition(targetContainer).X > targetContainer.ActualWidth / 2)
        {
            targetIndex++;
        }

        if (oldIndex < targetIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Clamp(targetIndex, 0, _state.Tabs.Count - 1);
        if (oldIndex >= 0 && oldIndex != targetIndex)
        {
            _state.Tabs.Move(oldIndex, targetIndex);
            TabsList.SelectedItem = dragged;
            ScheduleAutosave();
        }

        e.Handled = true;
    }

    private void SyntaxCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingEditor || _activeTab is null || SyntaxCombo.SelectedItem is not string mode)
        {
            return;
        }

        _activeTab.SyntaxMode = mode;
        ApplySyntaxHighlighting();
        ScheduleAutosave();
    }

    private void LineNumbersToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingEditor || _activeTab is null)
        {
            return;
        }

        _activeTab.ShowLineNumbers = LineNumbersToggle.IsChecked ?? false;
        Editor.ShowLineNumbers = _activeTab.ShowLineNumbers;
        ScheduleAutosave();
    }

    private void ProtectedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingEditor || _activeTab is null)
        {
            return;
        }

        _activeTab.IsProtected = ProtectedToggle.IsChecked ?? false;
        ScheduleAutosave();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingEditor || ThemeCombo.SelectedItem is not string theme)
        {
            return;
        }

        _state.Theme = theme;
        ThemeService.Apply(theme);
        UpdateEditorTheme();
        ApplySyntaxHighlighting();
        ScheduleAutosave();
    }

    private void PinToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingEditor)
        {
            return;
        }

        Topmost = PinToggle.IsChecked ?? false;
        _state.Topmost = Topmost;
        ScheduleAutosave();
    }

    private void WrapToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingEditor)
        {
            return;
        }

        _state.AutoWrap = WrapToggle.IsChecked ?? false;
        ApplyWordWrap();
        ScheduleAutosave();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FindMatch(forward: true, restart: true);

    private void SearchBox_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindMatch(!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseSearch();
            e.Handled = true;
        }
    }

    private void SearchPreviousButton_Click(object sender, RoutedEventArgs e) => FindMatch(forward: false);
    private void SearchNextButton_Click(object sender, RoutedEventArgs e) => FindMatch(forward: true);
    private void SearchCloseButton_Click(object sender, RoutedEventArgs e) => CloseSearch();

    private void MinimiseButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximiseButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PlacementChanged(object? sender, EventArgs e)
    {
        if (_isReady && WindowState != WindowState.Minimized)
        {
            UpdatePlacementState();
            ScheduleAutosave();
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowStateGlyph();
        Window_PlacementChanged(sender, e);
    }

    private void UpdateWindowStateGlyph()
    {
        MaximiseButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        MaximiseButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximise";
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _autosaveCancellation?.Cancel();
        UpdatePlacementState();
        SetSaveState("SAVING", "AccentBrush");
        try
        {
            await _persistence.SaveAsync(_state);
            SetSaveState("AUTOSAVED", "SuccessBrush");
        }
        catch
        {
            SetSaveState("SAVE ERROR", "DangerBrush");
        }

        _allowClose = true;
        Close();
    }

    private void Window_Closed(object? sender, EventArgs e) => System.Windows.Application.Current.Shutdown();

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && typed.Name == name)
            {
                return typed;
            }

            var descendant = FindVisualChild<T>(child, name);
            if (descendant is not null)
            {
                return descendant;
            }
        }
        return null;
    }
}
