using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
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
    private readonly ThemeService _themes;
    private readonly ScratchUndoHistory _scratchUndoHistory = new();
    private readonly ObservableCollection<ScratchPaletteSlot> _scratchPaletteSlots = [];
    private readonly double[] _scratchBrushSizes = [2, 4, 6, 10, 14, 20, 28, 40];
    private CancellationTokenSource? _autosaveCancellation;
    private TabDocument? _activeTab;
    private TabDocument? _draggedTab;
    private WpfPoint _dragStart;
    private string _renameOriginalTitle = string.Empty;
    private List<ThemeColorField> _appThemeFields = [];
    private List<ThemeColorField> _codeThemeFields = [];
    private string? _editingAppThemeId;
    private string? _editingCodeThemeId;
    private bool _isLoadingEditor;
    private bool _isLoadingThemes;
    private bool _isLoadingScratch;
    private bool _isShiftEraseActive;
    private bool _isAltEraseActive;
    private bool _isScratchPointerDown;
    private bool _scratchEraseSnapshotRecorded;
    private bool _scratchEditingModeUpdatePending;
    private bool _isReady;
    private bool _isClosing;
    private bool _allowClose;

    public MainWindow(
        WorkspaceState state,
        WorkspacePersistenceService persistence,
        ThemeService themes)
    {
        _state = state;
        _persistence = persistence;
        _themes = themes;
        _state.Normalize();

        InitializeComponent();

        _isLoadingEditor = true;
        _isLoadingThemes = true;
        DataContext = _state;
        SyntaxCombo.ItemsSource = SyntaxHighlightingService.Modes;
        ScratchBrushSizeCombo.ItemsSource = _scratchBrushSizes;
        ScratchPaletteList.ItemsSource = _scratchPaletteSlots;
        RefreshScratchPalette();
        RefreshThemeControls();
        ThemeFileStatus.Text = System.IO.Path.GetFileName(_themes.ThemesFilePath);
        ThemeFileStatus.ToolTip = _themes.ThemesFilePath;
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
        _isLoadingThemes = false;
        UpdateEditorTheme();
        UpdateEditorStatus();
        UpdateWindowStateGlyph();

        Loaded += (_, _) =>
        {
            _isReady = true;
            FocusActiveSurface();
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
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (_activeTab?.IsScratchMode == true &&
            modifiers == ModifierKeys.Control &&
            key == Key.Z &&
            !ScratchHexColorBox.IsKeyboardFocusWithin)
        {
            UndoLastScratchAction();
            e.Handled = true;
        }
        else if (_activeTab?.IsScratchMode == true &&
                 key is Key.LeftShift or Key.RightShift)
        {
            SyncScratchEraseModifierState();
        }
        else if (_activeTab?.IsScratchMode == true &&
                 key is Key.LeftAlt or Key.RightAlt)
        {
            SyncScratchEraseModifierState();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && key == Key.T)
        {
            CreateNewTab();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && key == Key.W)
        {
            CloseTab(_activeTab);
            e.Handled = true;
        }
        else if (modifiers.HasFlag(ModifierKeys.Control) && key == Key.Tab)
        {
            CycleTabs(modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && key == Key.F && _activeTab?.IsScratchMode != true)
        {
            OpenSearch();
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.P)
        {
            PinToggle.IsChecked = !(PinToggle.IsChecked ?? false);
            e.Handled = true;
        }
        else if (key == Key.Escape && ScratchColorPopup.IsOpen)
        {
            ScratchColorPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (key == Key.Escape && ThemePanel.Visibility == Visibility.Visible)
        {
            CloseThemePanel();
            e.Handled = true;
        }
        else if (key == Key.Escape && SearchPanel.Visibility == Visibility.Visible)
        {
            CloseSearch();
            e.Handled = true;
        }
        else if (key == Key.F3 && _activeTab?.IsScratchMode != true)
        {
            FindMatch(!modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyUp(object sender, WpfKeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftShift or Key.RightShift)
        {
            SyncScratchEraseModifierState();
            return;
        }

        if (_activeTab?.IsScratchMode == true && key is Key.LeftAlt or Key.RightAlt)
        {
            SyncScratchEraseModifierState();
            e.Handled = true;
        }
    }

    private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.StylusDevice is null)
        {
            QueueScratchPointerGestureEnd();
        }
    }

    private void Window_PreviewStylusUp(object sender, StylusEventArgs e) => QueueScratchPointerGestureEnd();

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (ScratchCanvas.IsMouseCaptured)
        {
            ScratchCanvas.ReleaseMouseCapture();
        }
        if (ScratchCanvas.IsStylusCaptured)
        {
            ScratchCanvas.ReleaseStylusCapture();
        }
        CancelScratchPointerGesture();
        _isShiftEraseActive = false;
        _isAltEraseActive = false;
        UpdateScratchEditingMode();
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

        if (ReferenceEquals(tab, _activeTab))
        {
            CommitActiveTab();
        }

        if (!string.IsNullOrEmpty(tab.Content) || !string.IsNullOrEmpty(tab.ScratchData))
        {
            var result = MessageBox.Show(
                this,
                $"Close '{tab.Title}'? Its text and scratch content will be removed from the workspace.",
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
        _scratchUndoHistory.Forget(tab.Id);
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
        LoadScratchSurface();
        ApplySyntaxHighlighting();
        Editor.CaretOffset = 0;
        ApplyTabSurfaceMode();
        _isLoadingEditor = false;
        UpdateEditorStatus();
        UpdateEmptyHint();
    }

    private void LoadScratchSurface()
    {
        if (_activeTab is null)
        {
            return;
        }

        _isLoadingScratch = true;
        ScratchCanvas.Strokes = InkStrokeSerializationService.Deserialize(_activeTab.ScratchData);
        _activeTab.ScratchBrushSize = _scratchBrushSizes.Contains(_activeTab.ScratchBrushSize)
            ? _activeTab.ScratchBrushSize
            : ScratchPaletteService.DefaultBrushSize;
        ScratchBrushSizeCombo.SelectedItem = _activeTab.ScratchBrushSize;
        var paletteIndex = _state.ScratchPalette.FindIndex(color =>
            color.Equals(_activeTab.ScratchBrushColor, StringComparison.OrdinalIgnoreCase));
        ScratchPaletteList.SelectedIndex = paletteIndex >= 0 ? paletteIndex : 0;
        ApplyScratchDrawingAttributes();
        _isLoadingScratch = false;
    }

    private void CommitActiveTab()
    {
        if (_activeTab is null)
        {
            return;
        }

        _activeTab.Content = Editor.Text;
        try
        {
            _activeTab.ScratchData = InkStrokeSerializationService.Serialize(ScratchCanvas.Strokes);
        }
        catch
        {
            SetSaveState("DRAW ERROR", "DangerBrush");
        }
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

        if (TryFindResource("EditorCaretBrush") is Brush foreground)
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
        EmptyHint.Visibility = _activeTab?.IsScratchMode != true && string.IsNullOrEmpty(Editor.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyTabSurfaceMode()
    {
        var scratchMode = _activeTab?.IsScratchMode == true;
        var wasLoading = _isLoadingEditor;
        _isLoadingEditor = true;
        TextSurfaceModeRadio.IsChecked = !scratchMode;
        ScratchSurfaceModeRadio.IsChecked = scratchMode;
        _isLoadingEditor = wasLoading;

        Editor.Visibility = scratchMode ? Visibility.Collapsed : Visibility.Visible;
        ScratchCanvas.Visibility = scratchMode ? Visibility.Visible : Visibility.Collapsed;
        TextToolsPanel.Visibility = scratchMode ? Visibility.Collapsed : Visibility.Visible;
        ScratchToolsPanel.Visibility = scratchMode ? Visibility.Visible : Visibility.Collapsed;
        CursorStatus.Visibility = scratchMode ? Visibility.Collapsed : Visibility.Visible;
        CharacterStatus.Visibility = scratchMode ? Visibility.Collapsed : Visibility.Visible;
        ModeStatus.Visibility = scratchMode ? Visibility.Collapsed : Visibility.Visible;
        ScratchHintStatus.Visibility = scratchMode ? Visibility.Visible : Visibility.Collapsed;
        if (scratchMode)
        {
            _isShiftEraseActive = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            _isAltEraseActive = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            SearchPanel.Visibility = Visibility.Collapsed;
            SearchFeedback.Text = string.Empty;
        }
        else
        {
            _isShiftEraseActive = false;
            _isAltEraseActive = false;
        }
        UpdateScratchEditingMode();
        UpdateEmptyHint();
    }

    private void TextSurfaceModeRadio_Checked(object sender, RoutedEventArgs e) => SetTabSurfaceMode(false);

    private void ScratchSurfaceModeRadio_Checked(object sender, RoutedEventArgs e) => SetTabSurfaceMode(true);

    private void SetTabSurfaceMode(bool scratchMode)
    {
        if (_isLoadingEditor || _activeTab is null || _activeTab.IsScratchMode == scratchMode)
        {
            return;
        }

        CancelScratchPointerGesture();
        CommitActiveTab();
        _activeTab.IsScratchMode = scratchMode;
        ApplyTabSurfaceMode();
        ScheduleAutosave();
        Dispatcher.BeginInvoke(FocusActiveSurface, DispatcherPriority.Input);
    }

    private void FocusActiveSurface()
    {
        if (_activeTab?.IsScratchMode == true)
        {
            ScratchCanvas.Focus();
        }
        else
        {
            Editor.Focus();
        }
    }

    private void ScratchCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_isLoadingScratch || _activeTab is null)
        {
            return;
        }

        _scratchUndoHistory.Record(_activeTab.Id, _activeTab.ScratchData);
        CommitActiveTab();
        ScheduleAutosave();
    }

    private void ScratchCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.StylusDevice is null)
        {
            BeginScratchPointerGesture();
        }
    }

    private void ScratchCanvas_PreviewStylusDown(object sender, StylusDownEventArgs e) => BeginScratchPointerGesture();

    private void ScratchCanvas_LostMouseCapture(object sender, WpfMouseEventArgs e)
    {
        if (e.StylusDevice is null)
        {
            QueueScratchPointerGestureEnd();
        }
    }

    private void ScratchCanvas_LostStylusCapture(object sender, StylusEventArgs e) => QueueScratchPointerGestureEnd();

    private void ScratchCanvas_StrokeErasing(object sender, InkCanvasStrokeErasingEventArgs e)
    {
        if (_isLoadingScratch || _activeTab is null)
        {
            return;
        }

        if (!_isScratchPointerDown)
        {
            BeginScratchPointerGesture();
        }

        if (!_scratchEraseSnapshotRecorded)
        {
            CommitActiveTab();
            _scratchUndoHistory.Record(_activeTab.Id, _activeTab.ScratchData);
            _scratchEraseSnapshotRecorded = true;
        }
    }

    private void ScratchCanvas_StrokeErased(object sender, RoutedEventArgs e)
    {
        if (_isLoadingScratch || _activeTab is null)
        {
            return;
        }

        CommitActiveTab();
        ScheduleAutosave();
    }

    private void UndoLastScratchAction()
    {
        if (_activeTab is null || _isScratchPointerDown)
        {
            return;
        }

        CancelScratchPointerGesture();
        if (_scratchUndoHistory.TryPop(_activeTab.Id, out var snapshot))
        {
            _isLoadingScratch = true;
            try
            {
                ScratchCanvas.Strokes = InkStrokeSerializationService.Deserialize(snapshot);
            }
            finally
            {
                _isLoadingScratch = false;
            }
        }
        else if (ScratchCanvas.Strokes.Count > 0)
        {
            // A restored workspace has no in-memory action history. Preserve the
            // original single-stroke undo behavior as a useful fallback.
            ScratchCanvas.Strokes.RemoveAt(ScratchCanvas.Strokes.Count - 1);
        }
        else
        {
            return;
        }

        CommitActiveTab();
        ScheduleAutosave();
    }

    private void UpdateScratchEditingMode()
    {
        var scratchMode = _activeTab?.IsScratchMode == true;
        var editingMode = !scratchMode
            ? InkCanvasEditingMode.Ink
            : _isShiftEraseActive
                ? InkCanvasEditingMode.EraseByPoint
                : _isAltEraseActive
                    ? InkCanvasEditingMode.EraseByStroke
                    : InkCanvasEditingMode.Ink;

        if (_isScratchPointerDown && ScratchCanvas.EditingMode != editingMode)
        {
            _scratchEditingModeUpdatePending = true;
            return;
        }

        _scratchEditingModeUpdatePending = false;
        ScratchCanvas.EditingMode = editingMode;
        ScratchHintStatus.Text = editingMode switch
        {
            InkCanvasEditingMode.EraseByPoint =>
                $"SHIFT: BRUSH ERASE  {_activeTab?.ScratchBrushSize ?? ScratchPaletteService.DefaultBrushSize:0.#} PX   •   CTRL+Z  UNDO",
            InkCanvasEditingMode.EraseByStroke =>
                "ALT: STROKE ERASE   •   CTRL+Z  UNDO",
            _ => "CTRL+Z  UNDO   •   HOLD SHIFT  BRUSH ERASE   •   HOLD ALT  STROKE ERASE"
        };
    }

    private void SyncScratchEraseModifierState()
    {
        _isShiftEraseActive = _activeTab?.IsScratchMode == true &&
                              (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
        _isAltEraseActive = _activeTab?.IsScratchMode == true &&
                            (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt));
        UpdateScratchEditingMode();
    }

    private void BeginScratchPointerGesture()
    {
        if (_isScratchPointerDown || _isLoadingScratch || _activeTab?.IsScratchMode != true)
        {
            return;
        }

        _isScratchPointerDown = true;
        _scratchEraseSnapshotRecorded = false;
    }

    private void QueueScratchPointerGestureEnd()
    {
        if (_isScratchPointerDown || _scratchEditingModeUpdatePending)
        {
            Dispatcher.BeginInvoke(FinishScratchPointerGesture, DispatcherPriority.Input);
        }
    }

    private void FinishScratchPointerGesture()
    {
        _isScratchPointerDown = false;
        _scratchEraseSnapshotRecorded = false;
        _scratchEditingModeUpdatePending = false;
        UpdateScratchEditingMode();
    }

    private void CancelScratchPointerGesture()
    {
        _isScratchPointerDown = false;
        _scratchEraseSnapshotRecorded = false;
        _scratchEditingModeUpdatePending = false;
    }

    private void ClearScratchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null || ScratchCanvas.Strokes.Count == 0)
        {
            return;
        }

        CancelScratchPointerGesture();
        CommitActiveTab();
        _scratchUndoHistory.Record(_activeTab.Id, _activeTab.ScratchData);
        ScratchCanvas.Strokes.Clear();
        CommitActiveTab();
        ScheduleAutosave();
        ScratchCanvas.Focus();
    }

    private void ScratchBrushSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingScratch || _isLoadingEditor || _activeTab is null ||
            ScratchBrushSizeCombo.SelectedItem is not double size)
        {
            return;
        }

        _activeTab.ScratchBrushSize = size;
        ApplyScratchDrawingAttributes();
        UpdateScratchEditingMode();
        ScheduleAutosave();
    }

    private void ScratchColorButton_Click(object sender, RoutedEventArgs e)
    {
        ScratchColorError.Text = string.Empty;
        ScratchColorPopup.IsOpen = !ScratchColorPopup.IsOpen;
    }

    private void ScratchPaletteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingScratch || _activeTab is null ||
            ScratchPaletteList.SelectedItem is not ScratchPaletteSlot slot)
        {
            return;
        }

        SetScratchBrushColor(slot.Hex);
    }

    private void AddScratchColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ThemeService.IsValidColor(ScratchHexColorBox.Text))
        {
            ScratchColorError.Text = "USE #RRGGBB OR #AARRGGBB";
            ScratchHexColorBox.SetResourceReference(TextBox.BorderBrushProperty, "DangerBrush");
            return;
        }

        var hex = ScratchHexColorBox.Text.Trim().ToUpperInvariant();
        var slotIndex = ScratchPaletteList.SelectedIndex;
        if (slotIndex < 0 || slotIndex >= ScratchPaletteService.SlotCount)
        {
            slotIndex = ScratchPaletteService.FirstCustomSlot;
        }

        _state.ScratchPalette[slotIndex] = hex;
        _scratchPaletteSlots[slotIndex] = new ScratchPaletteSlot(slotIndex, hex);
        _isLoadingScratch = true;
        ScratchPaletteList.SelectedIndex = slotIndex;
        _isLoadingScratch = false;
        ScratchColorError.Text = string.Empty;
        ScratchHexColorBox.SetResourceReference(TextBox.BorderBrushProperty, "BorderBrush");
        SetScratchBrushColor(hex);
    }

    private void SetScratchBrushColor(string hex)
    {
        if (_activeTab is null)
        {
            return;
        }

        _activeTab.ScratchBrushColor = hex;
        ScratchHexColorBox.Text = hex;
        ApplyScratchDrawingAttributes();
        ScheduleAutosave();
    }

    private void ApplyScratchDrawingAttributes()
    {
        if (_activeTab is null)
        {
            return;
        }

        var color = ColorConverter.ConvertFromString(_activeTab.ScratchBrushColor) is Color parsed
            ? parsed
            : Colors.Cyan;
        ScratchCanvas.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = color,
            Width = _activeTab.ScratchBrushSize,
            Height = _activeTab.ScratchBrushSize,
            StylusTip = StylusTip.Ellipse,
            FitToCurve = true,
            IgnorePressure = true
        };
        ScratchCanvas.EraserShape = new EllipseStylusShape(
            _activeTab.ScratchBrushSize,
            _activeTab.ScratchBrushSize);
        var swatch = new SolidColorBrush(color);
        swatch.Freeze();
        ScratchColorSwatch.Background = swatch;
        ScratchColorButton.ToolTip = $"Brush color {_activeTab.ScratchBrushColor}";
        ScratchHexColorBox.Text = _activeTab.ScratchBrushColor;
    }

    private void RefreshScratchPalette()
    {
        _isLoadingScratch = true;
        _scratchPaletteSlots.Clear();
        for (var index = 0; index < _state.ScratchPalette.Count; index++)
        {
            _scratchPaletteSlots.Add(new ScratchPaletteSlot(index, _state.ScratchPalette[index]));
        }
        _isLoadingScratch = false;
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
            CommitActiveTab();
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
        if (_activeTab?.IsScratchMode == true)
        {
            return;
        }

        SearchPanel.Visibility = Visibility.Visible;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void CloseSearch()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        SearchFeedback.Text = string.Empty;
        FocusActiveSurface();
    }

    private void FindMatch(bool forward, bool restart = false)
    {
        if (_activeTab?.IsScratchMode == true)
        {
            return;
        }

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
            CancelScratchPointerGesture();
            CommitActiveTab();
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

    private void AppThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingThemes || AppThemeCombo.SelectedItem is not AppThemeDefinition theme)
        {
            return;
        }

        _state.AppThemeId = theme.Id;
        _themes.ApplyAppTheme(theme.Id);
        if (ThemePanel.Visibility == Visibility.Visible)
        {
            LoadAppThemeEditor(theme);
            AppThemeEditorCombo.SelectedItem = theme;
        }
        ScheduleAutosave();
    }

    private void CodeThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingThemes || CodeThemeCombo.SelectedItem is not CodeThemeDefinition theme)
        {
            return;
        }

        _state.CodeThemeId = theme.Id;
        _themes.ApplyCodeTheme(theme.Id);
        UpdateEditorTheme();
        ApplySyntaxHighlighting();
        if (ThemePanel.Visibility == Visibility.Visible)
        {
            LoadCodeThemeEditor(theme);
            CodeThemeEditorCombo.SelectedItem = theme;
        }
        ScheduleAutosave();
    }

    private void RefreshThemeControls(string? appEditorId = null, string? codeEditorId = null)
    {
        var wasLoading = _isLoadingThemes;
        _isLoadingThemes = true;

        AppThemeCombo.ItemsSource = null;
        CodeThemeCombo.ItemsSource = null;
        AppThemeEditorCombo.ItemsSource = null;
        CodeThemeEditorCombo.ItemsSource = null;

        AppThemeCombo.ItemsSource = _themes.AppThemes;
        CodeThemeCombo.ItemsSource = _themes.CodeThemes;
        AppThemeEditorCombo.ItemsSource = _themes.AppThemes;
        CodeThemeEditorCombo.ItemsSource = _themes.CodeThemes;

        var activeApp = _themes.FindAppTheme(_state.AppThemeId) ?? _themes.AppThemes[0];
        var activeCode = _themes.FindCodeTheme(_state.CodeThemeId) ?? _themes.CodeThemes[0];
        var editorApp = _themes.FindAppTheme(appEditorId) ?? activeApp;
        var editorCode = _themes.FindCodeTheme(codeEditorId) ?? activeCode;

        AppThemeCombo.SelectedItem = activeApp;
        CodeThemeCombo.SelectedItem = activeCode;
        AppThemeEditorCombo.SelectedItem = editorApp;
        CodeThemeEditorCombo.SelectedItem = editorCode;
        LoadAppThemeEditor(editorApp);
        LoadCodeThemeEditor(editorCode);

        _isLoadingThemes = wasLoading;
    }

    private void AppThemeEditorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoadingThemes && AppThemeEditorCombo.SelectedItem is AppThemeDefinition theme)
        {
            LoadAppThemeEditor(theme);
            SetThemeEditorStatus("APP THEME LOADED", "MutedTextBrush");
        }
    }

    private void CodeThemeEditorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoadingThemes && CodeThemeEditorCombo.SelectedItem is CodeThemeDefinition theme)
        {
            LoadCodeThemeEditor(theme);
            SetThemeEditorStatus("CODE THEME LOADED", "MutedTextBrush");
        }
    }

    private void LoadAppThemeEditor(AppThemeDefinition theme)
    {
        _editingAppThemeId = theme.Id;
        AppThemeTitleBox.Text = theme.Title;
        AppThemeFontFamilyBox.Text = theme.FontFamily;
        AppThemeFontSizeBox.Text = theme.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
        _appThemeFields = CreateAppThemeFields(theme.Colors);
        AppThemeFieldsList.ItemsSource = _appThemeFields;
    }

    private void LoadCodeThemeEditor(CodeThemeDefinition theme)
    {
        _editingCodeThemeId = theme.Id;
        CodeThemeTitleBox.Text = theme.Title;
        CodeThemeFontFamilyBox.Text = theme.FontFamily;
        CodeThemeFontSizeBox.Text = theme.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
        _codeThemeFields = CreateCodeThemeFields(theme.Colors);
        CodeThemeFieldsList.ItemsSource = _codeThemeFields;
    }

    private void OpenThemePanelButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshThemeControls();
        ThemePanelScrim.Visibility = Visibility.Visible;
        ThemePanel.Visibility = Visibility.Visible;
        SetThemeEditorStatus("READY", "MutedTextBrush");
    }

    private void CloseThemePanelButton_Click(object sender, RoutedEventArgs e) => CloseThemePanel();

    private void CancelThemePanelButton_Click(object sender, RoutedEventArgs e) => CloseThemePanel();

    private void ThemePanelScrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => CloseThemePanel();

    private void CloseThemePanel()
    {
        ThemePanel.Visibility = Visibility.Collapsed;
        ThemePanelScrim.Visibility = Visibility.Collapsed;
        FocusActiveSurface();
    }

    private void NewAppThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var source = _themes.FindAppTheme(_state.AppThemeId) ?? _themes.AppThemes[0];
        _editingAppThemeId = null;
        _isLoadingThemes = true;
        AppThemeEditorCombo.SelectedIndex = -1;
        _isLoadingThemes = false;
        AppThemeTitleBox.Text = $"{source.Title} Custom";
        AppThemeFontFamilyBox.Text = source.FontFamily;
        AppThemeFontSizeBox.Text = source.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
        _appThemeFields = CreateAppThemeFields(source.Colors);
        AppThemeFieldsList.ItemsSource = _appThemeFields;
        SetThemeEditorStatus("NEW APP THEME", "AccentBrush");
        AppThemeTitleBox.Focus();
        AppThemeTitleBox.SelectAll();
    }

    private void NewCodeThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var source = _themes.FindCodeTheme(_state.CodeThemeId) ?? _themes.CodeThemes[0];
        _editingCodeThemeId = null;
        _isLoadingThemes = true;
        CodeThemeEditorCombo.SelectedIndex = -1;
        _isLoadingThemes = false;
        CodeThemeTitleBox.Text = $"{source.Title} Custom";
        CodeThemeFontFamilyBox.Text = source.FontFamily;
        CodeThemeFontSizeBox.Text = source.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
        _codeThemeFields = CreateCodeThemeFields(source.Colors);
        CodeThemeFieldsList.ItemsSource = _codeThemeFields;
        SetThemeEditorStatus("NEW CODE THEME", "SecondaryAccentBrush");
        CodeThemeTitleBox.Focus();
        CodeThemeTitleBox.SelectAll();
    }

    private async void SaveThemePanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AppThemeTitleBox.Text))
        {
            SetThemeEditorStatus("APP TITLE REQUIRED", "DangerBrush");
            return;
        }
        if (string.IsNullOrWhiteSpace(CodeThemeTitleBox.Text))
        {
            SetThemeEditorStatus("CODE TITLE REQUIRED", "DangerBrush");
            return;
        }
        if (!TryReadAppThemeColors(out var colors))
        {
            SetThemeEditorStatus("INVALID APP COLOR", "DangerBrush");
            return;
        }
        if (!TryReadCodeThemeColors(out var codeColors))
        {
            SetThemeEditorStatus("INVALID CODE COLOR", "DangerBrush");
            return;
        }
        if (!ThemeService.IsValidFontFamily(AppThemeFontFamilyBox.Text))
        {
            SetThemeEditorStatus("APP FONT FAMILY REQUIRED", "DangerBrush");
            return;
        }
        if (!TryParseFontSize(AppThemeFontSizeBox.Text, out var appFontSize) ||
            !ThemeService.IsValidAppFontSize(appFontSize))
        {
            SetThemeEditorStatus("APP FONT SIZE: 8–16", "DangerBrush");
            return;
        }
        if (!ThemeService.IsValidFontFamily(CodeThemeFontFamilyBox.Text))
        {
            SetThemeEditorStatus("CODE FONT FAMILY REQUIRED", "DangerBrush");
            return;
        }
        if (!TryParseFontSize(CodeThemeFontSizeBox.Text, out var codeFontSize) ||
            !ThemeService.IsValidCodeFontSize(codeFontSize))
        {
            SetThemeEditorStatus("CODE FONT SIZE: 8–32", "DangerBrush");
            return;
        }

        ThemeSaveButton.IsEnabled = false;
        ThemeCancelButton.IsEnabled = false;
        SetThemeEditorStatus("SAVING…", "MutedTextBrush");
        try
        {
            var saved = await _themes.UpsertThemesAsync(
                new AppThemeDefinition
                {
                    Id = _editingAppThemeId ?? string.Empty,
                    Title = AppThemeTitleBox.Text.Trim(),
                    FontFamily = AppThemeFontFamilyBox.Text.Trim(),
                    FontSize = appFontSize,
                    Colors = colors
                },
                new CodeThemeDefinition
                {
                    Id = _editingCodeThemeId ?? string.Empty,
                    Title = CodeThemeTitleBox.Text.Trim(),
                    FontFamily = CodeThemeFontFamilyBox.Text.Trim(),
                    FontSize = codeFontSize,
                    Colors = codeColors
                });
            _state.AppThemeId = saved.AppTheme.Id;
            _state.CodeThemeId = saved.CodeTheme.Id;
            _themes.ApplyAppTheme(saved.AppTheme.Id);
            _themes.ApplyCodeTheme(saved.CodeTheme.Id);
            UpdateEditorTheme();
            ApplySyntaxHighlighting();
            RefreshThemeControls(saved.AppTheme.Id, saved.CodeTheme.Id);
            ScheduleAutosave();
            CloseThemePanel();
        }
        catch (Exception ex)
        {
            SetThemeEditorStatus($"SAVE FAILED: {ex.Message}", "DangerBrush");
        }
        finally
        {
            ThemeSaveButton.IsEnabled = true;
            ThemeCancelButton.IsEnabled = true;
        }
    }

    private void SetThemeEditorStatus(string text, string brushResource)
    {
        ThemeEditorStatus.Text = text;
        ThemeEditorStatus.SetResourceReference(TextBlock.ForegroundProperty, brushResource);
    }

    private static List<ThemeColorField> CreateAppThemeFields(AppThemeColors colors) =>
    [
        new(nameof(colors.Background), "Background", colors.Background),
        new(nameof(colors.Surface), "Surface", colors.Surface),
        new(nameof(colors.RaisedSurface), "Raised surface", colors.RaisedSurface),
        new(nameof(colors.Border), "Border", colors.Border),
        new(nameof(colors.OuterEdgeTop), "Outer edge / top", colors.OuterEdgeTop),
        new(nameof(colors.OuterEdgeBottom), "Outer edge / bottom", colors.OuterEdgeBottom),
        new(nameof(colors.PrimaryAccent), "Primary accent", colors.PrimaryAccent),
        new(nameof(colors.SecondaryAccent), "Secondary accent", colors.SecondaryAccent),
        new(nameof(colors.Text), "Text", colors.Text),
        new(nameof(colors.MutedText), "Muted text", colors.MutedText),
        new(nameof(colors.SubtleText), "Subtle text", colors.SubtleText),
        new(nameof(colors.Danger), "Danger", colors.Danger),
        new(nameof(colors.Success), "Success", colors.Success)
    ];

    private static List<ThemeColorField> CreateCodeThemeFields(CodeThemeColors colors) =>
    [
        new(nameof(colors.Background), "Background", colors.Background),
        new(nameof(colors.Foreground), "Foreground", colors.Foreground),
        new(nameof(colors.Selection), "Selection", colors.Selection),
        new(nameof(colors.Keyword), "Keyword", colors.Keyword),
        new(nameof(colors.Type), "Type / tag", colors.Type),
        new(nameof(colors.String), "String", colors.String),
        new(nameof(colors.Number), "Number", colors.Number),
        new(nameof(colors.Comment), "Comment", colors.Comment),
        new(nameof(colors.LineNumber), "Line number", colors.LineNumber),
        new(nameof(colors.Caret), "Caret", colors.Caret)
    ];

    private bool TryReadAppThemeColors(out AppThemeColors colors)
    {
        colors = new AppThemeColors();
        if (_appThemeFields.Any(field => !field.IsValid))
        {
            return false;
        }

        colors.Background = ThemeValue(_appThemeFields, nameof(colors.Background));
        colors.Surface = ThemeValue(_appThemeFields, nameof(colors.Surface));
        colors.RaisedSurface = ThemeValue(_appThemeFields, nameof(colors.RaisedSurface));
        colors.Border = ThemeValue(_appThemeFields, nameof(colors.Border));
        colors.OuterEdgeTop = ThemeValue(_appThemeFields, nameof(colors.OuterEdgeTop));
        colors.OuterEdgeBottom = ThemeValue(_appThemeFields, nameof(colors.OuterEdgeBottom));
        colors.PrimaryAccent = ThemeValue(_appThemeFields, nameof(colors.PrimaryAccent));
        colors.SecondaryAccent = ThemeValue(_appThemeFields, nameof(colors.SecondaryAccent));
        colors.Text = ThemeValue(_appThemeFields, nameof(colors.Text));
        colors.MutedText = ThemeValue(_appThemeFields, nameof(colors.MutedText));
        colors.SubtleText = ThemeValue(_appThemeFields, nameof(colors.SubtleText));
        colors.Danger = ThemeValue(_appThemeFields, nameof(colors.Danger));
        colors.Success = ThemeValue(_appThemeFields, nameof(colors.Success));
        return true;
    }

    private bool TryReadCodeThemeColors(out CodeThemeColors colors)
    {
        colors = new CodeThemeColors();
        if (_codeThemeFields.Any(field => !field.IsValid))
        {
            return false;
        }

        colors.Background = ThemeValue(_codeThemeFields, nameof(colors.Background));
        colors.Foreground = ThemeValue(_codeThemeFields, nameof(colors.Foreground));
        colors.Selection = ThemeValue(_codeThemeFields, nameof(colors.Selection));
        colors.Keyword = ThemeValue(_codeThemeFields, nameof(colors.Keyword));
        colors.Type = ThemeValue(_codeThemeFields, nameof(colors.Type));
        colors.String = ThemeValue(_codeThemeFields, nameof(colors.String));
        colors.Number = ThemeValue(_codeThemeFields, nameof(colors.Number));
        colors.Comment = ThemeValue(_codeThemeFields, nameof(colors.Comment));
        colors.LineNumber = ThemeValue(_codeThemeFields, nameof(colors.LineNumber));
        colors.Caret = ThemeValue(_codeThemeFields, nameof(colors.Caret));
        return true;
    }

    private static string ThemeValue(IEnumerable<ThemeColorField> fields, string key) =>
        fields.First(field => field.Key == key).Value.Trim().ToUpperInvariant();

    private static bool TryParseFontSize(string value, out double fontSize) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out fontSize) ||
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize);

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

    private void Editor_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
        if (position is null)
        {
            return;
        }

        var offset = Math.Clamp(Editor.Document.GetOffset(position.Value.Location), 0, Editor.Document.TextLength);
        var selectionStart = Editor.SelectionStart;
        var selectionEnd = selectionStart + Editor.SelectionLength;
        var clickedInsideSelection = Editor.SelectionLength > 0 && offset >= selectionStart && offset < selectionEnd;
        if (!clickedInsideSelection)
        {
            Editor.Select(offset, 0);
            Editor.CaretOffset = offset;
        }
        Editor.Focus();
    }

    private void EditorContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        CutMenuItem.IsEnabled = Editor.SelectionLength > 0;
        CopyMenuItem.IsEnabled = Editor.SelectionLength > 0;
        SelectAllMenuItem.IsEnabled = Editor.Text.Length > 0;
        try
        {
            PasteMenuItem.IsEnabled = System.Windows.Clipboard.ContainsText();
        }
        catch
        {
            PasteMenuItem.IsEnabled = false;
        }
    }

    private void CutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        Editor.Cut();
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        Editor.Copy();
    }

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        Editor.Paste();
    }

    private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        Editor.SelectAll();
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
        CommitActiveTab();
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
