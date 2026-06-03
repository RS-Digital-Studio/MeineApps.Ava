using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using BingXBot.Graphics;
using BingXBot.ViewModels;
using SkiaSharp;
using System.ComponentModel;

namespace BingXBot.Views;

/// <summary>
/// Dashboard-View. DataContext wird vom ViewLocator gesetzt (ContentControl-Binding an MainViewModel.Dashboard).
/// Alle VM-Subscriptions erfolgen über DataContextChanged — so bleibt die View unabhängig vom DI-Container.
/// </summary>
public partial class DashboardView : UserControl
{
    private DashboardViewModel? _vm;

    // Farben fuer Modus-Buttons
    private static readonly SolidColorBrush PrimaryBrush = new(Color.Parse("#3B82F6"));
    private static readonly SolidColorBrush LossBrush = new(Color.Parse("#EF4444"));
    private static readonly SolidColorBrush CardBrush = new(Color.Parse("#363650"));
    private static readonly SolidColorBrush WhiteBrush = new(Color.Parse("#FFFFFF"));
    private static readonly SolidColorBrush MutedBrush = new(Color.Parse("#94A3B8"));
    private static readonly SolidColorBrush InactiveBorderBrush = new(Color.Parse("#3F3F5C"));

    // Farben fuer BTC-Change
    private static readonly SolidColorBrush ProfitBrush = new(Color.Parse("#10B981"));

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetached;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // Alte Subscriptions sauber abmelden (bei VM-Wechsel)
        UnsubscribeFromVm();

        if (DataContext is DashboardViewModel dashVm)
        {
            _vm = dashVm;

            // CollectionChanged-Handler als benannte Methoden (für saubere Abmeldung in OnDetached)
            dashVm.BtcTicker.BtcCandles.CollectionChanged += OnBtcCandlesChanged;
            dashVm.BtcTicker.TradeMarkers.CollectionChanged += OnTradeMarkersChanged;
            dashVm.EquityData.CollectionChanged += OnEquityDataChanged;

            // Property-Änderungen abonnieren fuer dynamische UI-Updates
            dashVm.PropertyChanged += OnViewModelPropertyChanged;
            dashVm.BtcTicker.PropertyChanged += OnBtcTickerPropertyChanged;
            dashVm.WidgetCanvasInvalidationRequested += OnWidgetCanvasInvalidation;

            // Initiale Zuweisung (nur Modus-Buttons + BTC-Farbe, Rest via AXAML-Bindings)
            UpdateModeButtons();
            UpdateBtcChangeColor();
        }
    }

    // Benannte Handler für saubere Abmeldung
    private void OnBtcCandlesChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (this.FindControl<SKCanvasView>("BtcChartCanvas") is { } c) c.InvalidateSurface();
    }
    private void OnTradeMarkersChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (this.FindControl<SKCanvasView>("BtcChartCanvas") is { } c) c.InvalidateSurface();
    }
    private void OnEquityDataChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (this.FindControl<SKCanvasView>("EquityCanvas") is { } c) c.InvalidateSurface();
        // Widgets die von Equity/Trade-Daten abhängen auch aktualisieren
        if (this.FindControl<SKCanvasView>("DrawdownCanvas") is { } d) d.InvalidateSurface();
        if (this.FindControl<SKCanvasView>("PnlCalendarCanvas") is { } p) p.InvalidateSurface();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // StatusDot + PnL-Farben werden jetzt via AXAML-Bindings gesteuert
            case nameof(DashboardViewModel.IsPaperMode):
                UpdateModeButtons();
                break;
        }
    }

    private void OnBtcTickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BtcTickerViewModel.BtcPriceChange))
            UpdateBtcChangeColor();
        // ActiveOverlay oder Indikator-Änderung → Chart neu zeichnen
        if (e.PropertyName is nameof(BtcTickerViewModel.ActiveOverlay) or nameof(BtcTickerViewModel.Indicators))
        {
            if (this.FindControl<SKCanvasView>("BtcChartCanvas") is { } canvas)
                canvas.InvalidateSurface();
        }
    }

    /// <summary>
    /// Aktualisiert die Modus-Buttons: aktiver Button farbig, inaktiver grau.
    /// Paper aktiv = blau, Live aktiv = rot (Warnung: echtes Geld).
    /// </summary>
    private void UpdateModeButtons()
    {
        if (_vm == null) return;
        var paperBtn = this.FindControl<Button>("PaperButton");
        var liveBtn = this.FindControl<Button>("LiveButton");
        if (paperBtn == null || liveBtn == null) return;

        if (_vm.IsPaperMode)
        {
            paperBtn.Background = PrimaryBrush;
            paperBtn.Foreground = WhiteBrush;
            paperBtn.BorderBrush = PrimaryBrush;
            liveBtn.Background = CardBrush;
            liveBtn.Foreground = MutedBrush;
            liveBtn.BorderBrush = InactiveBorderBrush;
        }
        else
        {
            paperBtn.Background = CardBrush;
            paperBtn.Foreground = MutedBrush;
            paperBtn.BorderBrush = InactiveBorderBrush;
            liveBtn.Background = LossBrush;
            liveBtn.Foreground = WhiteBrush;
            liveBtn.BorderBrush = LossBrush;
        }
    }

    /// <summary>
    /// Aktualisiert die BTC-Änderungs-Farbe: grün wenn positiv, rot wenn negativ.
    /// </summary>
    private void UpdateBtcChangeColor()
    {
        if (_vm == null) return;
        var changeText = this.FindControl<TextBlock>("BtcChangeText");
        if (changeText != null)
        {
            changeText.Foreground = _vm.BtcTicker.BtcPriceChange >= 0 ? ProfitBrush : LossBrush;
        }
    }

    /// <summary>
    /// Invalidiert Widget-Canvases wenn DailyPnl aktualisiert wurde.
    /// Wird vom ViewModel nach Mutation auf dem UI-Thread aufgerufen.
    /// </summary>
    private void OnWidgetCanvasInvalidation()
    {
        if (this.FindControl<SKCanvasView>("PnlCalendarCanvas") is { } p) p.InvalidateSurface();
        if (this.FindControl<SKCanvasView>("CorrelationCanvas") is { } c) c.InvalidateSurface();
    }

    private void OnEquityPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds; // NICHT e.Info.Width/Height (DPI-Problem!)
        var data = _vm.EquityData.ToList();

        EquityChartRenderer.Render(canvas, bounds, data, _vm.Balance > 0 ? _vm.Balance : 10_000m);
    }

    private void OnBtcChartPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        var markers = _vm.BtcTicker.TradeMarkers.Count > 0 ? _vm.BtcTicker.TradeMarkers.ToList() : null;

        _vm.BtcTicker.ChartRenderer.Render(canvas, bounds, _vm.BtcTicker.BtcCandles.ToList(),
            markers, _vm.BtcTicker.ActiveOverlay, _vm.BtcTicker.Indicators);
    }

    private void OnDrawdownPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        DrawdownChartRenderer.Render(canvas, bounds, _vm.EquityData.ToList(), _vm.Balance > 0 ? _vm.Balance : 10_000m);
    }

    private void OnPnlCalendarPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        PnlCalendarRenderer.Render(canvas, bounds, _vm.DailyPnl);
    }

    private void OnCorrelationPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        CorrelationMatrixRenderer.Render(canvas, bounds, _vm.CorrelationSymbols, _vm.CorrelationMatrix);
    }

    // ═══ Chart-Interaktion: Crosshair, Zoom/Pan ═══

    private void OnChartPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_vm == null || sender is not SKCanvasView canvas) return;
        var pos = e.GetPosition(canvas);
        var state = _vm.BtcTicker.ChartRenderer.State;
        state.ShowCrosshair = true;
        state.CrosshairX = (float)pos.X;
        state.CrosshairY = (float)pos.Y;

        // Pan: Wenn Drag aktiv, Viewport verschieben
        if (state.IsDragging)
        {
            var dx = (float)pos.X - state.DragStartX;
            var candlesShift = -(int)(dx / Math.Max(_vm.BtcTicker.ChartRenderer.CandleWidth, 1f));
            var total = _vm.BtcTicker.BtcCandles.Count;
            var vis = state.ViewEnd - state.ViewStart;
            state.ViewStart = Math.Clamp(state.DragStartViewStart + candlesShift, 0, Math.Max(0, total - vis));
            state.ViewEnd = Math.Min(total, state.ViewStart + vis);
        }

        canvas.InvalidateSurface();
    }

    private void OnChartPointerExited(object? sender, PointerEventArgs e)
    {
        if (_vm == null || sender is not SKCanvasView canvas) return;
        _vm.BtcTicker.ChartRenderer.State.ShowCrosshair = false;
        _vm.BtcTicker.ChartRenderer.State.IsDragging = false;
        canvas.InvalidateSurface();
    }

    private void OnChartPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || sender is not SKCanvasView canvas) return;
        var pos = e.GetPosition(canvas);
        var state = _vm.BtcTicker.ChartRenderer.State;
        state.IsDragging = true;
        state.DragStartX = (float)pos.X;
        state.DragStartViewStart = state.ViewStart;
    }

    private void OnChartPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_vm == null) return;
        _vm.BtcTicker.ChartRenderer.State.IsDragging = false;
    }

    private void OnChartPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_vm == null || sender is not SKCanvasView canvas) return;
        // Scroll-Event konsumieren damit die Seite nicht mitscrollt
        e.Handled = true;
        var delta = e.Delta.Y > 0 ? -5 : 5; // Hochscrollen = Reinzoomen (weniger Candles)
        _vm.BtcTicker.ChartRenderer.State.Zoom(delta, _vm.BtcTicker.BtcCandles.Count);
        canvas.InvalidateSurface();
    }

    /// <summary>Klick auf Position-Card → Chart wechselt zum Symbol + SK-Overlay.</summary>
    private void PositionCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || sender is not Border border || border.DataContext is not PositionDisplayItem pos) return;
        _ = _vm.SelectPositionCommand.ExecuteAsync(pos);
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeFromVm();
    }

    private void UnsubscribeFromVm()
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.BtcTicker.PropertyChanged -= OnBtcTickerPropertyChanged;
            _vm.WidgetCanvasInvalidationRequested -= OnWidgetCanvasInvalidation;
            _vm.BtcTicker.BtcCandles.CollectionChanged -= OnBtcCandlesChanged;
            _vm.BtcTicker.TradeMarkers.CollectionChanged -= OnTradeMarkersChanged;
            _vm.EquityData.CollectionChanged -= OnEquityDataChanged;
            _vm = null;
        }
    }
}
