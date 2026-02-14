using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RechnerPlus.ViewModels;

namespace RechnerPlus.Views;

public partial class CalculatorView : UserControl
{
    private CalculatorViewModel? _currentVm;
    private Point _swipeStart;
    private bool _isSwiping;
    private bool _autoSwitchedToScientific;
    private bool _isLandscapeLayout;
    private const double SwipeThreshold = 40;

    public CalculatorView()
    {
        InitializeComponent();
        Focusable = true;
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;

        // Swipe-to-Backspace auf Display-Border
        var displayBorder = this.FindControl<Border>("DisplayBorder");
        if (displayBorder != null)
        {
            displayBorder.PointerPressed += OnDisplayPointerPressed;
            displayBorder.PointerReleased += OnDisplayPointerReleased;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        HandleLandscapeDetection(e.NewSize.Width, e.NewSize.Height);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden um Memory Leak zu vermeiden
        if (_currentVm != null)
        {
            _currentVm.ClipboardCopyRequested -= OnClipboardCopy;
            _currentVm.ClipboardPasteRequested -= OnClipboardPaste;
            _currentVm.ShareRequested -= OnShare;
            _currentVm.CalculationCompleted -= OnCalculationCompleted;
        }

        _currentVm = DataContext as CalculatorViewModel;

        if (_currentVm != null)
        {
            _currentVm.ClipboardCopyRequested += OnClipboardCopy;
            _currentVm.ClipboardPasteRequested += OnClipboardPaste;
            _currentVm.ShareRequested += OnShare;
            _currentVm.CalculationCompleted += OnCalculationCompleted;
        }
    }

    #region Landscape = Scientific Mode

    private void HandleLandscapeDetection(double width, double height)
    {
        if (_currentVm == null || height <= 0) return;

        var isLandscape = width > height;

        if (isLandscape && _currentVm.IsBasicMode)
        {
            // Landscape → automatisch Scientific
            _currentVm.CurrentMode = CalculatorMode.Scientific;
            _autoSwitchedToScientific = true;
        }
        else if (!isLandscape && _autoSwitchedToScientific)
        {
            // Portrait → zurück zu Basic (nur wenn automatisch gewechselt)
            _currentVm.CurrentMode = CalculatorMode.Basic;
            _autoSwitchedToScientific = false;
        }

        // Layout umschalten
        if (isLandscape && !_isLandscapeLayout)
            ApplyLandscapeLayout();
        else if (!isLandscape && _isLandscapeLayout)
            ApplyPortraitLayout();
    }

    private void ApplyLandscapeLayout()
    {
        _isLandscapeLayout = true;
        var rootGrid = this.FindControl<Grid>("RootGrid");
        if (rootGrid == null) return;

        rootGrid.Classes.Add("Landscape");
        rootGrid.Margin = new Thickness(8, 4, 8, 4);

        // Landscape 2-Spalten-Layout:
        // Spalte 0 (40%): Display, ModeSelector, ScientificPanel, Memory+Freiraum
        // Spalte 1 (60%): BasicGrid (gesamte Hoehe via RowSpan=4)
        // Letzte Zeile * damit BasicGrid rechts die volle Hoehe nutzt
        rootGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*");
        rootGrid.ColumnDefinitions = new ColumnDefinitions("2*,3*");
        rootGrid.ColumnSpacing = 8;

        var display = this.FindControl<Border>("DisplayBorder");
        var mode = this.FindControl<Grid>("ModeSelector");
        var scientific = this.FindControl<Grid>("ScientificPanel");
        var memory = this.FindControl<Grid>("MemoryRowGrid");
        var basic = this.FindControl<Grid>("BasicGrid");

        // Display: links oben, Zeile 0
        if (display != null)
        {
            Grid.SetRow(display, 0);
            Grid.SetColumn(display, 0);
            Grid.SetColumnSpan(display, 1);
        }

        // Mode Selector: links, Zeile 1
        if (mode != null)
        {
            Grid.SetRow(mode, 1);
            Grid.SetColumn(mode, 0);
            Grid.SetColumnSpan(mode, 1);
        }

        // Scientific Panel: links, Zeile 2 (fuellt verfuegbaren Platz)
        if (scientific != null)
        {
            Grid.SetRow(scientific, 2);
            Grid.SetColumn(scientific, 0);
            Grid.SetColumnSpan(scientific, 1);
            scientific.RowDefinitions = new RowDefinitions("*,*,*");
            scientific.ColumnSpacing = 2;
            scientific.RowSpacing = 2;
        }

        // Memory Row: links, Zeile 3 (*-Zeile), am unteren Rand
        if (memory != null)
        {
            Grid.SetRow(memory, 3);
            Grid.SetColumn(memory, 0);
            Grid.SetColumnSpan(memory, 1);
            memory.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        }

        // Basic Grid: rechts, ueber alle 4 Zeilen (gesamte Hoehe)
        if (basic != null)
        {
            Grid.SetRow(basic, 0);
            Grid.SetColumn(basic, 1);
            Grid.SetColumnSpan(basic, 1);
            Grid.SetRowSpan(basic, 4);
            basic.RowSpacing = 2;
            basic.ColumnSpacing = 2;
        }
    }

    private void ApplyPortraitLayout()
    {
        _isLandscapeLayout = false;
        var rootGrid = this.FindControl<Grid>("RootGrid");
        if (rootGrid == null) return;

        rootGrid.Classes.Remove("Landscape");
        rootGrid.Margin = new Thickness(16);
        rootGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*");
        rootGrid.ColumnDefinitions = new ColumnDefinitions();
        rootGrid.ColumnSpacing = 0;

        var display = this.FindControl<Border>("DisplayBorder");
        var mode = this.FindControl<Grid>("ModeSelector");
        var scientific = this.FindControl<Grid>("ScientificPanel");
        var memory = this.FindControl<Grid>("MemoryRowGrid");
        var basic = this.FindControl<Grid>("BasicGrid");

        // Display: oben, Zeile 0
        if (display != null)
        {
            Grid.SetRow(display, 0);
            Grid.SetColumn(display, 0);
            Grid.SetColumnSpan(display, 1);
        }

        // Mode Selector: Zeile 1
        if (mode != null)
        {
            Grid.SetRow(mode, 1);
            Grid.SetColumn(mode, 0);
            Grid.SetColumnSpan(mode, 1);
        }

        // Scientific Panel: Zeile 2, Auto-Rows zuruecksetzen
        if (scientific != null)
        {
            Grid.SetRow(scientific, 2);
            Grid.SetColumn(scientific, 0);
            Grid.SetColumnSpan(scientific, 1);
            scientific.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
            scientific.ColumnSpacing = 6;
            scientific.RowSpacing = 6;
        }

        // Memory Row: Zeile 3
        if (memory != null)
        {
            Grid.SetRow(memory, 3);
            Grid.SetColumn(memory, 0);
            Grid.SetColumnSpan(memory, 1);
            memory.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        // Basic Grid: Zeile 4
        if (basic != null)
        {
            Grid.SetRow(basic, 4);
            Grid.SetColumn(basic, 0);
            Grid.SetColumnSpan(basic, 1);
            Grid.SetRowSpan(basic, 1);
            basic.RowSpacing = 8;
            basic.ColumnSpacing = 8;
        }
    }

    #endregion

    #region Swipe-to-Backspace auf Display

    private void OnDisplayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _swipeStart = e.GetPosition(this);
        _isSwiping = true;
    }

    private void OnDisplayPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSwiping) return;
        _isSwiping = false;

        var end = e.GetPosition(this);
        var deltaX = end.X - _swipeStart.X;
        var deltaY = end.Y - _swipeStart.Y;

        // Nur horizontale Swipes nach links erkennen
        if (deltaX < -SwipeThreshold && Math.Abs(deltaX) > Math.Abs(deltaY) * 1.5)
        {
            _currentVm?.BackspaceCommand.Execute(null);
        }
    }

    #endregion

    #region Clipboard

    private async Task OnClipboardCopy(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(text);
    }

    private async Task OnClipboardPaste()
    {
        if (_currentVm == null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null) return;
#pragma warning disable CS0618 // GetTextAsync ist veraltet, TryGetTextAsync braucht IAsyncDataTransfer
        var text = await topLevel.Clipboard.GetTextAsync();
#pragma warning restore CS0618
        _currentVm.PasteValue(text);
    }

    private async Task OnShare(string text)
    {
        // Auf Desktop: In Zwischenablage kopieren als Fallback
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(text);
    }

    #endregion

    #region Ergebnis-Animation

    private void OnCalculationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Display: Fade+Scale-Animation
            var displayText = this.FindControl<TextBlock>("DisplayText");
            if (displayText != null)
            {
                displayText.Opacity = 0.3;
                displayText.RenderTransform = new ScaleTransform(0.96, 0.96);

                DispatcherTimer.RunOnce(() =>
                {
                    displayText.Opacity = 1;
                    displayText.RenderTransform = new ScaleTransform(1, 1);
                }, TimeSpan.FromMilliseconds(16));
            }

            // Equals-Button: Kurzer Weiß-Flash (100ms)
            var equalsBtn = this.FindControl<Button>("EqualsButton");
            if (equalsBtn != null)
            {
                var originalBg = equalsBtn.Background;
                equalsBtn.Background = new SolidColorBrush(Colors.White, 0.3);

                DispatcherTimer.RunOnce(() =>
                {
                    equalsBtn.Background = originalBg;
                }, TimeSpan.FromMilliseconds(100));
            }
        });
    }

    #endregion

    #region Keyboard

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_currentVm == null) return;

        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0:
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _currentVm.InputDigitCommand.Execute("0");
                else
                    _currentVm.InputParenthesisCommand.Execute(")"); // Shift+0 = )
                break;
            case Key.D1 or Key.NumPad1: _currentVm.InputDigitCommand.Execute("1"); break;
            case Key.D2 or Key.NumPad2: _currentVm.InputDigitCommand.Execute("2"); break;
            case Key.D3 or Key.NumPad3: _currentVm.InputDigitCommand.Execute("3"); break;
            case Key.D4 or Key.NumPad4: _currentVm.InputDigitCommand.Execute("4"); break;
            case Key.D5 or Key.NumPad5: _currentVm.InputDigitCommand.Execute("5"); break;
            case Key.D6 or Key.NumPad6: _currentVm.InputDigitCommand.Execute("6"); break;
            case Key.D7 or Key.NumPad7: _currentVm.InputDigitCommand.Execute("7"); break;
            case Key.D8 or Key.NumPad8:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _currentVm.InputOperatorCommand.Execute("\u00d7");
                else
                    _currentVm.InputDigitCommand.Execute("8");
                break;
            case Key.D9 or Key.NumPad9:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _currentVm.InputParenthesisCommand.Execute("("); // Shift+9 = (
                else
                    _currentVm.InputDigitCommand.Execute("9");
                break;
            case Key.C:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    _currentVm.CopyDisplayCommand.Execute(null);
                else return;
                break;
            case Key.V:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    _currentVm.PasteFromClipboardCommand.Execute(null);
                else return;
                break;
            case Key.S:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    _currentVm.ShareDisplayCommand.Execute(null);
                else return;
                break;
            case Key.Z:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    _currentVm.UndoCommand.Execute(null);
                else return;
                break;
            case Key.Y:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    _currentVm.RedoCommand.Execute(null);
                else return;
                break;
            case Key.Add: _currentVm.InputOperatorCommand.Execute("+"); break;
            case Key.Subtract: _currentVm.InputOperatorCommand.Execute("\u2212"); break;
            case Key.Multiply: _currentVm.InputOperatorCommand.Execute("\u00d7"); break;
            case Key.Divide: _currentVm.InputOperatorCommand.Execute("\u00f7"); break;
            case Key.OemPlus:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _currentVm.InputOperatorCommand.Execute("+");
                else
                    _currentVm.CalculateCommand.Execute(null);
                break;
            case Key.OemMinus: _currentVm.InputOperatorCommand.Execute("\u2212"); break;
            case Key.Enter: _currentVm.CalculateCommand.Execute(null); break;
            case Key.Back: _currentVm.BackspaceCommand.Execute(null); break;
            case Key.Delete: _currentVm.ClearEntryCommand.Execute(null); break;
            case Key.Escape: _currentVm.ClearCommand.Execute(null); break;
            case Key.OemPeriod or Key.Decimal: _currentVm.InputDecimalCommand.Execute(null); break;
            case Key.OemComma: _currentVm.InputDecimalCommand.Execute(null); break;
            case Key.Oem2: _currentVm.InputOperatorCommand.Execute("\u00f7"); break; // / key
            default: return;
        }

        e.Handled = true;
    }

    #endregion
}
