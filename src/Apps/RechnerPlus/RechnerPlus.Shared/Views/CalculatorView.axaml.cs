using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MeineApps.Core.Ava.Services;
using MeineApps.UI.SkiaSharp;
using RechnerPlus.Graphics;
using RechnerPlus.ViewModels;
using SkiaSharp;

namespace RechnerPlus.Views;

public partial class CalculatorView : UserControl
{
    private CalculatorViewModel? _currentVm;
    private Point _swipeStart;
    private bool _isSwiping;
    private bool _autoSwitchedToScientific;
    private bool _isLandscapeLayout;
    private const double SwipeThreshold = 40;

    // Gecachte FindControl-Ergebnisse (befüllt in OnAttachedToVisualTree)
    private Border? _burstOverlay;
    private Border? _displayBorder;
    private Button? _equalsButton;
    private Border? _functionGraphBorder;

    // VFD-Flicker-Animation
    private DispatcherTimer? _vfdTimer;
    private float _vfdAnimTime;

    // Result-Burst-Animation
    private DispatcherTimer? _burstTimer;
    private float _burstProgress;
    private const float BurstDuration = 0.5f; // Sekunden
    private const float BurstStep = 33f / 1000f; // ~30fps

    // Funktionsgraph Auto-Hide
    private DispatcherTimer? _graphHideTimer;

    // Error-Shake Animation
    private DispatcherTimer? _shakeTimer;
    private int _shakeStep;
    private static readonly double[] ShakeOffsets = [0, 4, -4, 3, -3, 2, -2, 0];

    // Copy-Feedback Animation
    private DispatcherTimer? _copyFeedbackTimer;

    public CalculatorView()
    {
        InitializeComponent();
        Focusable = true;
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;

        // Swipe-to-Backspace auf Display-Border (wird im Constructor registriert,
        // Cache-Befüllung erst in OnAttachedToVisualTree wenn Controls sicher vorhanden sind)
        this.AttachedToVisualTree += (_, _) =>
        {
            if (_displayBorder != null) return; // Bereits gecacht
            _displayBorder = this.FindControl<Border>("DisplayBorder");
            if (_displayBorder != null)
            {
                _displayBorder.PointerPressed += OnDisplayPointerPressed;
                _displayBorder.PointerReleased += OnDisplayPointerReleased;
            }
        };

        // VFD-Flicker-Timer (subtiles ~7Hz Flackern) - auch für Funktionsgraph-Glow
        _vfdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _vfdTimer.Tick += (_, _) =>
        {
            // Keine Canvas-Invalidierung wenn View nicht sichtbar (z.B. anderer Tab aktiv)
            if (!IsEffectivelyVisible) return;

            _vfdAnimTime += 0.033f;
            VfdCanvas?.InvalidateSurface();

            // Funktionsgraph-Canvas mitaktualisieren (Glow-Pulsierung)
            if (_currentVm?.ShowFunctionGraph == true)
                FunctionGraphCanvas?.InvalidateSurface();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();
        _vfdTimer?.Start();

        // Controls einmalig cachen (FindControl ist teuer bei wiedeholten Aufrufen)
        _burstOverlay ??= this.FindControl<Border>("BurstOverlay");
        _equalsButton ??= this.FindControl<Button>("EqualsButton");
        _functionGraphBorder ??= this.FindControl<Border>("FunctionGraphBorder");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _vfdTimer?.Stop();
        _burstTimer?.Stop();
        _graphHideTimer?.Stop();
        _shakeTimer?.Stop();
        _copyFeedbackTimer?.Stop();
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
            _currentVm.PropertyChanged -= OnVmPropertyChanged;
            _currentVm.FunctionGraphChanged -= OnFunctionGraphChanged;
            _currentVm.ErrorShakeRequested -= OnErrorShakeRequested;
            _currentVm.CopyFeedbackRequested -= OnCopyFeedbackRequested;
        }

        _currentVm = DataContext as CalculatorViewModel;

        if (_currentVm != null)
        {
            _currentVm.ClipboardCopyRequested += OnClipboardCopy;
            _currentVm.ClipboardPasteRequested += OnClipboardPaste;
            _currentVm.ShareRequested += OnShare;
            _currentVm.CalculationCompleted += OnCalculationCompleted;
            _currentVm.PropertyChanged += OnVmPropertyChanged;
            _currentVm.FunctionGraphChanged += OnFunctionGraphChanged;
            _currentVm.ErrorShakeRequested += OnErrorShakeRequested;
            _currentVm.CopyFeedbackRequested += OnCopyFeedbackRequested;
        }
    }

    /// <summary>
    /// Bei Display-Änderungen VFD-Canvas neu zeichnen.
    /// </summary>
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalculatorViewModel.Display) or nameof(CalculatorViewModel.HasError))
        {
            VfdCanvas?.InvalidateSurface();
        }
    }

    #region VFD-Display (SkiaSharp)

    /// <summary>
    /// Zeichnet den VFD 7-Segment-Display.
    /// </summary>
    private void OnPaintVfd(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(10, 10, 10)); // Fast-schwarz

        if (_currentVm == null) return;

        var bounds = canvas.LocalClipBounds;
        var displayText = _currentVm.Display ?? "0";
        var hasError = _currentVm.HasError;

        VfdDisplayVisualization.Render(canvas, bounds, displayText, hasError, _vfdAnimTime);
    }

    #endregion

    #region Result-Burst (SkiaSharp)

    /// <summary>
    /// Startet die Burst-Animation bei Berechnung.
    /// </summary>
    private void StartBurstAnimation()
    {
        _burstProgress = 0;
        // Gecachtes Feld nutzen statt FindControl
        if (_burstOverlay != null) _burstOverlay.IsVisible = true;

        // Timer einmalig erstellen, bei weiteren Aufrufen nur Stop/Start
        if (_burstTimer == null)
        {
            _burstTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _burstTimer.Tick += (_, _) =>
            {
                _burstProgress += BurstStep / BurstDuration;
                if (_burstProgress >= 1f)
                {
                    _burstProgress = 0;
                    _burstTimer?.Stop();
                    // Gecachtes Feld nutzen statt FindControl
                    if (_burstOverlay != null) _burstOverlay.IsVisible = false;
                }
                BurstCanvas?.InvalidateSurface();
            };
        }
        else
        {
            _burstTimer.Stop();
        }
        _burstTimer.Start();
    }

    /// <summary>
    /// Zeichnet den Ergebnis-Burst-Effekt.
    /// </summary>
    private void OnPaintBurst(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_burstProgress <= 0 || _burstProgress >= 1) return;

        var bounds = canvas.LocalClipBounds;
        var burstColor = SkiaThemeHelper.Primary;
        ResultBurstVisualization.Render(canvas, bounds, _burstProgress, burstColor);
    }

    #endregion

    #region Funktionsgraph (SkiaSharp)

    /// <summary>
    /// Wird aufgerufen wenn sich die aktive Funktion im ViewModel ändert.
    /// </summary>
    private void OnFunctionGraphChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Gecachtes Feld nutzen statt FindControl
            var graphBorder = _functionGraphBorder;
            if (graphBorder == null) return;

            if (_currentVm?.ShowFunctionGraph == true)
            {
                // Graph einblenden
                graphBorder.Opacity = 1;
                graphBorder.MaxHeight = 200;
                FunctionGraphCanvas?.InvalidateSurface();

                // Auto-Hide Timer starten/neustarten (5 Sekunden)
                if (_graphHideTimer == null)
                {
                    _graphHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    _graphHideTimer.Tick += (_, _) =>
                    {
                        _graphHideTimer?.Stop();
                        // Graph ausblenden via ViewModel
                        _currentVm?.ClearFunctionGraph();
                    };
                }
                else
                {
                    _graphHideTimer.Stop();
                }
                _graphHideTimer.Start();
            }
            else
            {
                // Graph ausblenden (Transition übernimmt die Animation)
                graphBorder.Opacity = 0;
                graphBorder.MaxHeight = 0;
                _graphHideTimer?.Stop();
            }
        });
    }

    /// <summary>
    /// Zeichnet den Funktionsgraph.
    /// </summary>
    private void OnPaintFunctionGraph(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(10, 10, 10)); // Gleicher Hintergrund wie VFD

        if (_currentVm?.ActiveFunction == null || _currentVm.ActiveFunctionName == null) return;

        var bounds = canvas.LocalClipBounds;
        FunctionGraphVisualization.Render(
            canvas, bounds,
            _currentVm.ActiveFunction,
            _currentVm.ActiveFunctionName,
            _currentVm.FunctionGraphCurrentX,
            _vfdAnimTime);
    }

    #endregion

    #region Error-Shake Animation

    /// <summary>
    /// Startet die Shake-Animation auf dem DisplayBorder bei Fehlern.
    /// </summary>
    private void OnErrorShakeRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Gecachtes Feld nutzen statt FindControl
            var border = _displayBorder;
            if (border == null) return;

            // TranslateTransform sicherstellen
            if (border.RenderTransform is not TranslateTransform translateTransform)
            {
                translateTransform = new TranslateTransform();
                border.RenderTransform = translateTransform;
            }

            _shakeStep = 0;
            // Timer einmalig erstellen, bei weiteren Aufrufen nur Stop/Start
            if (_shakeTimer == null)
            {
                _shakeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300.0 / ShakeOffsets.Length) };
                _shakeTimer.Tick += (_, _) =>
                {
                    if (_shakeStep >= ShakeOffsets.Length)
                    {
                        _shakeTimer?.Stop();
                        // Sicherstellen dass die Position zurückgesetzt ist
                        if (border.RenderTransform is TranslateTransform tt)
                            tt.X = 0;
                        return;
                    }

                    if (border.RenderTransform is TranslateTransform t)
                        t.X = ShakeOffsets[_shakeStep];

                    _shakeStep++;
                };
            }
            else
            {
                _shakeTimer.Stop();
            }
            _shakeTimer.Start();
        });
    }

    #endregion

    #region Copy-Feedback Animation

    // Gespeicherte Original-Werte für Copy-Feedback-Restore
    private IBrush? _copyIconOriginalForeground;
    private double _copyIconOriginalOpacity;

    /// <summary>
    /// Zeigt visuelles Feedback beim Kopieren (Copy-Icon grün aufleuchten + zurück).
    /// </summary>
    private void OnCopyFeedbackRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // CopyIcon hat x:Name im AXAML
            if (CopyIcon == null) return;

            _copyIconOriginalForeground = CopyIcon.Foreground;
            _copyIconOriginalOpacity = CopyIcon.Opacity;
            CopyIcon.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));
            CopyIcon.Opacity = 1.0;

            // Timer einmalig erstellen, bei weiteren Aufrufen nur Stop/Start
            if (_copyFeedbackTimer == null)
            {
                _copyFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _copyFeedbackTimer.Tick += (_, _) =>
                {
                    _copyFeedbackTimer?.Stop();
                    if (CopyIcon == null) return;
                    CopyIcon.Foreground = _copyIconOriginalForeground;
                    CopyIcon.Opacity = _copyIconOriginalOpacity;
                };
            }
            else
            {
                _copyFeedbackTimer.Stop();
            }
            _copyFeedbackTimer.Start();
        });
    }

    #endregion

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
        // Spalte 0 (40%): Display, FunctionGraph, ModeSelector, ScientificPanel, Memory+Freiraum
        // Spalte 1 (60%): BasicGrid (gesamte Höhe via RowSpan)
        // FunctionGraph wird im Landscape ausgeblendet (Platz sparen)
        rootGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*");
        rootGrid.ColumnDefinitions = new ColumnDefinitions("2*,3*");
        rootGrid.ColumnSpacing = 8;

        var display = this.FindControl<Panel>("DisplayPanel");
        var graphBorder = this.FindControl<Border>("FunctionGraphBorder");
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

        // FunctionGraph: links, Zeile 1 (kompakter im Landscape)
        if (graphBorder != null)
        {
            Grid.SetRow(graphBorder, 1);
            Grid.SetColumn(graphBorder, 0);
            Grid.SetColumnSpan(graphBorder, 1);
        }

        // SKCanvasView im Graph kompakter
        if (FunctionGraphCanvas != null)
            FunctionGraphCanvas.Height = 100;

        // Mode Selector: links, Zeile 2
        if (mode != null)
        {
            Grid.SetRow(mode, 2);
            Grid.SetColumn(mode, 0);
            Grid.SetColumnSpan(mode, 1);
        }

        // Scientific Panel: links, Zeile 3
        if (scientific != null)
        {
            Grid.SetRow(scientific, 3);
            Grid.SetColumn(scientific, 0);
            Grid.SetColumnSpan(scientific, 1);
            scientific.RowDefinitions = new RowDefinitions("*,*,*,*");
            scientific.ColumnSpacing = 2;
            scientific.RowSpacing = 2;
        }

        // Memory Row: links, Zeile 4 (*-Zeile), am unteren Rand
        if (memory != null)
        {
            Grid.SetRow(memory, 4);
            Grid.SetColumn(memory, 0);
            Grid.SetColumnSpan(memory, 1);
            memory.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        }

        // Basic Grid: rechts, über alle 5 Zeilen (gesamte Höhe)
        if (basic != null)
        {
            Grid.SetRow(basic, 0);
            Grid.SetColumn(basic, 1);
            Grid.SetColumnSpan(basic, 1);
            Grid.SetRowSpan(basic, 5);
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
        rootGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*");
        rootGrid.ColumnDefinitions = new ColumnDefinitions();
        rootGrid.ColumnSpacing = 0;

        var display = this.FindControl<Panel>("DisplayPanel");
        var graphBorder = this.FindControl<Border>("FunctionGraphBorder");
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

        // FunctionGraph: Zeile 1
        if (graphBorder != null)
        {
            Grid.SetRow(graphBorder, 1);
            Grid.SetColumn(graphBorder, 0);
            Grid.SetColumnSpan(graphBorder, 1);
        }

        // SKCanvasView Höhe zurücksetzen
        if (FunctionGraphCanvas != null)
            FunctionGraphCanvas.Height = 140;

        // Mode Selector: Zeile 2
        if (mode != null)
        {
            Grid.SetRow(mode, 2);
            Grid.SetColumn(mode, 0);
            Grid.SetColumnSpan(mode, 1);
        }

        // Scientific Panel: Zeile 3, Auto-Rows zurücksetzen
        if (scientific != null)
        {
            Grid.SetRow(scientific, 3);
            Grid.SetColumn(scientific, 0);
            Grid.SetColumnSpan(scientific, 1);
            scientific.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");
            scientific.ColumnSpacing = 6;
            scientific.RowSpacing = 6;
        }

        // Memory Row: Zeile 4
        if (memory != null)
        {
            Grid.SetRow(memory, 4);
            Grid.SetColumn(memory, 0);
            Grid.SetColumnSpan(memory, 1);
            memory.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        // Basic Grid: Zeile 5
        if (basic != null)
        {
            Grid.SetRow(basic, 5);
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

    private Task OnShare(string text)
    {
        // Natives Share-Sheet (Android) oder Clipboard-Fallback (Desktop)
        UriLauncher.ShareText(text, "RechnerPlus");
        return Task.CompletedTask;
    }

    #endregion

    #region Ergebnis-Animation

    private void OnCalculationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // VFD-Display invalidieren (zeigt neues Ergebnis)
            VfdCanvas?.InvalidateSurface();

            // Result-Burst-Animation starten
            StartBurstAnimation();

            // Equals-Button: Kurzer Weiß-Flash (100ms) via CSS-Klasse
            // (Kein direktes Background-Setzen, da das den Style-Setter neutralisiert)
            // Gecachtes Feld nutzen statt FindControl
            var equalsBtn = _equalsButton;
            if (equalsBtn != null)
            {
                equalsBtn.Classes.Add("Flashing");

                DispatcherTimer.RunOnce(() =>
                {
                    equalsBtn.Classes.Remove("Flashing");
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
