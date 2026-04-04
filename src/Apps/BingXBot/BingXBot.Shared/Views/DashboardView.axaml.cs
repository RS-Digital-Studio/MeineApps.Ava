using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using BingXBot.Graphics;
using BingXBot.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System.ComponentModel;

namespace BingXBot.Views;

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

        DataContext = App.Services.GetRequiredService<DashboardViewModel>();
        DetachedFromVisualTree += OnDetached;

        if (DataContext is DashboardViewModel dashVm)
        {
            _vm = dashVm;

            // BtcCandles-Änderungen invalidieren den Canvas (delegiert an BtcTicker Sub-VM)
            dashVm.BtcTicker.BtcCandles.CollectionChanged += (_, _) =>
            {
                if (this.FindControl<SKCanvasView>("BtcChartCanvas") is { } canvas)
                    canvas.InvalidateSurface();
            };

            // Equity-Änderungen invalidieren den Canvas
            dashVm.EquityData.CollectionChanged += (_, _) =>
            {
                if (this.FindControl<SKCanvasView>("EquityCanvas") is { } canvas)
                    canvas.InvalidateSurface();
            };

            // Property-Änderungen abonnieren fuer dynamische UI-Updates
            dashVm.PropertyChanged += OnViewModelPropertyChanged;
            dashVm.BtcTicker.PropertyChanged += OnBtcTickerPropertyChanged;

            // Initiale Zuweisung (nur Modus-Buttons + BTC-Farbe, Rest via AXAML-Bindings)
            UpdateModeButtons();
            UpdateBtcChangeColor();
        }
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
        var bounds = canvas.LocalClipBounds; // NICHT e.Info.Width/Height (DPI-Problem!)
        BtcPriceChartRenderer.Render(canvas, bounds, _vm.BtcTicker.BtcCandles.ToList());
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.BtcTicker.PropertyChanged -= OnBtcTickerPropertyChanged;
        }
    }
}
