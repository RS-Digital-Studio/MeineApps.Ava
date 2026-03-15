using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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

    // Farben fuer Status-Punkt
    private static readonly SolidColorBrush RedBrush = new(Color.Parse("#EF4444"));
    private static readonly SolidColorBrush GreenBrush = new(Color.Parse("#10B981"));
    private static readonly SolidColorBrush AmberBrush = new(Color.Parse("#F59E0B"));

    // Farben fuer Modus-Buttons
    private static readonly SolidColorBrush PrimaryBrush = new(Color.Parse("#3B82F6"));
    private static readonly SolidColorBrush LossBrush = new(Color.Parse("#EF4444"));
    private static readonly SolidColorBrush CardBrush = new(Color.Parse("#363650"));
    private static readonly SolidColorBrush WhiteBrush = new(Color.Parse("#FFFFFF"));
    private static readonly SolidColorBrush MutedBrush = new(Color.Parse("#94A3B8"));

    // Farben fuer P&L
    private static readonly SolidColorBrush ProfitBrush = new(Color.Parse("#10B981"));
    private static readonly SolidColorBrush DefaultTextBrush = new(Color.Parse("#E2E8F0"));

    public DashboardView()
    {
        InitializeComponent();

        DataContext = App.Services.GetRequiredService<DashboardViewModel>();

        if (DataContext is DashboardViewModel dashVm)
        {
            _vm = dashVm;

            // BtcCandles-Aenderungen invalidieren den Canvas
            dashVm.BtcCandles.CollectionChanged += (_, _) =>
            {
                if (this.FindControl<SKCanvasView>("BtcChartCanvas") is { } canvas)
                    canvas.InvalidateSurface();
            };

            // Equity-Aenderungen invalidieren den Canvas
            dashVm.EquityData.CollectionChanged += (_, _) =>
            {
                if (this.FindControl<SKCanvasView>("EquityCanvas") is { } canvas)
                    canvas.InvalidateSurface();
            };

            // Property-Aenderungen abonnieren fuer dynamische UI-Updates
            dashVm.PropertyChanged += OnViewModelPropertyChanged;

            // Initiale Zuweisung
            UpdateStatusDot();
            UpdateModeButtons();
            UpdatePnlColors();
            UpdateBtcChangeColor();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DashboardViewModel.BotStatusColor):
                UpdateStatusDot();
                break;
            case nameof(DashboardViewModel.IsPaperMode):
                UpdateModeButtons();
                break;
            case nameof(DashboardViewModel.UnrealizedPnl):
            case nameof(DashboardViewModel.TotalPnl):
                UpdatePnlColors();
                break;
            case nameof(DashboardViewModel.BtcPriceChange):
                UpdateBtcChangeColor();
                break;
        }
    }

    /// <summary>
    /// Aktualisiert die Farbe des Status-Punktes basierend auf BotStatusColor.
    /// </summary>
    private void UpdateStatusDot()
    {
        if (_vm == null) return;
        var dot = this.FindControl<Ellipse>("StatusDot");
        if (dot == null) return;

        dot.Fill = _vm.BotStatusColor switch
        {
            "#10B981" => GreenBrush,
            "#F59E0B" => AmberBrush,
            _ => RedBrush
        };
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
            liveBtn.BorderBrush = new SolidColorBrush(Color.Parse("#3F3F5C"));
        }
        else
        {
            paperBtn.Background = CardBrush;
            paperBtn.Foreground = MutedBrush;
            paperBtn.BorderBrush = new SolidColorBrush(Color.Parse("#3F3F5C"));
            liveBtn.Background = LossBrush;
            liveBtn.Foreground = WhiteBrush;
            liveBtn.BorderBrush = LossBrush;
        }
    }

    /// <summary>
    /// Aktualisiert die P&L-TextBlock-Farben: gruen wenn positiv, rot wenn negativ.
    /// </summary>
    private void UpdatePnlColors()
    {
        if (_vm == null) return;

        var unrealizedText = this.FindControl<TextBlock>("UnrealizedPnlText");
        if (unrealizedText != null)
        {
            unrealizedText.Foreground = _vm.UnrealizedPnl >= 0 ? ProfitBrush : LossBrush;
        }

        var totalText = this.FindControl<TextBlock>("TotalPnlText");
        if (totalText != null)
        {
            totalText.Foreground = _vm.TotalPnl >= 0 ? ProfitBrush : LossBrush;
        }
    }

    /// <summary>
    /// Aktualisiert die BTC-Aenderungs-Farbe: gruen wenn positiv, rot wenn negativ.
    /// </summary>
    private void UpdateBtcChangeColor()
    {
        if (_vm == null) return;
        var changeText = this.FindControl<TextBlock>("BtcChangeText");
        if (changeText != null)
        {
            changeText.Foreground = _vm.BtcPriceChange >= 0 ? ProfitBrush : LossBrush;
        }
    }

    private void OnEquityPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds; // NICHT e.Info.Width/Height (DPI-Problem!)
        var data = _vm.EquityData.ToList();

        EquityChartRenderer.Render(canvas, bounds, data, _vm.Balance > 0 ? _vm.Balance : 10000m);
    }

    private void OnBtcChartPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm == null) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds; // NICHT e.Info.Width/Height (DPI-Problem!)
        BtcPriceChartRenderer.Render(canvas, bounds, _vm.BtcCandles.ToList());
    }
}
