using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using BingXBot.Graphics;
using BingXBot.ViewModels;
using Material.Icons.Avalonia;
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
    private static readonly SolidColorBrush InactiveBorderBrush = new(Color.Parse("#3F3F5C"));

    // Farben fuer P&L
    private static readonly SolidColorBrush ProfitBrush = new(Color.Parse("#10B981"));
    private static readonly SolidColorBrush DefaultTextBrush = new(Color.Parse("#E2E8F0"));

    public DashboardView()
    {
        InitializeComponent();

        DataContext = App.Services.GetRequiredService<DashboardViewModel>();
        DetachedFromVisualTree += OnDetached;

        if (DataContext is DashboardViewModel dashVm)
        {
            _vm = dashVm;

            // BtcCandles-Aenderungen invalidieren den Canvas (delegiert an BtcTicker Sub-VM)
            dashVm.BtcTicker.BtcCandles.CollectionChanged += (_, _) =>
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
            dashVm.BtcTicker.PropertyChanged += OnBtcTickerPropertyChanged;

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
            case nameof(DashboardViewModel.BotStatusState):
                UpdateStatusDot();
                break;
            case nameof(DashboardViewModel.IsPaperMode):
                UpdateModeButtons();
                break;
            case nameof(DashboardViewModel.UnrealizedPnl):
            case nameof(DashboardViewModel.TotalPnl):
                UpdatePnlColors();
                break;
        }
    }

    private void OnBtcTickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BtcTickerViewModel.BtcPriceChange))
            UpdateBtcChangeColor();
    }

    /// <summary>
    /// Aktualisiert die Farbe des Status-Punktes basierend auf BotStatusColor.
    /// </summary>
    private void UpdateStatusDot()
    {
        if (_vm == null) return;
        var dot = this.FindControl<Ellipse>("StatusDot");
        if (dot == null) return;

        dot.Fill = _vm.BotStatusState switch
        {
            BingXBot.Core.Enums.BotState.Running => GreenBrush,
            BingXBot.Core.Enums.BotState.Paused or BingXBot.Core.Enums.BotState.Starting => AmberBrush,
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
    /// Aktualisiert die P&L-TextBlock-Farben und Pfeil-Icons: gruen/hoch wenn positiv, rot/runter wenn negativ.
    /// </summary>
    private void UpdatePnlColors()
    {
        if (_vm == null) return;

        var unrealizedText = this.FindControl<TextBlock>("UnrealizedPnlText");
        if (unrealizedText != null)
        {
            unrealizedText.Foreground = _vm.UnrealizedPnl >= 0 ? ProfitBrush : LossBrush;
        }

        var unrealizedIcon = this.FindControl<MaterialIcon>("UnrealizedPnlIcon");
        if (unrealizedIcon != null)
        {
            unrealizedIcon.Kind = _vm.UnrealizedPnl >= 0
                ? Material.Icons.MaterialIconKind.ArrowUp
                : Material.Icons.MaterialIconKind.ArrowDown;
            unrealizedIcon.Foreground = _vm.UnrealizedPnl >= 0 ? ProfitBrush : LossBrush;
        }

        var totalText = this.FindControl<TextBlock>("TotalPnlText");
        if (totalText != null)
        {
            totalText.Foreground = _vm.TotalPnl >= 0 ? ProfitBrush : LossBrush;
        }

        var totalIcon = this.FindControl<MaterialIcon>("TotalPnlIcon");
        if (totalIcon != null)
        {
            totalIcon.Kind = _vm.TotalPnl >= 0
                ? Material.Icons.MaterialIconKind.ArrowUp
                : Material.Icons.MaterialIconKind.ArrowDown;
            totalIcon.Foreground = _vm.TotalPnl >= 0 ? ProfitBrush : LossBrush;
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
            changeText.Foreground = _vm.BtcTicker.BtcPriceChange >= 0 ? ProfitBrush : LossBrush;
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
