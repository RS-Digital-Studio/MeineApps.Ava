using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using System.ComponentModel;
using ZeitManager.Graphics;
using ZeitManager.ViewModels;

namespace ZeitManager.Views;

public partial class TimerView : UserControl
{
    private DispatcherTimer? _renderTimer;
    private float _animTime;

    // ViewModel-Referenz für saubere Event-Abmeldung
    private TimerViewModel? _viewModel;

    public TimerView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Alten Handler abmelden
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        // Neuen Handler anmelden
        if (DataContext is TimerViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>Reagiert auf ViewModel-Änderungen und steuert die Render-Loop.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not TimerViewModel vm) return;

        if (args.PropertyName is nameof(vm.ShowVisualization))
        {
            // Render-Loop starten/stoppen je nach Sichtbarkeit
            if (vm.ShowVisualization)
                StartRenderLoop();
            else
                StopRenderLoop();
        }
    }

    /// <summary>Rendert die Timer-Visualisierung mit Fluessigkeits-Effekt via SkiaSharp.</summary>
    private void OnPaintTimerVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not TimerViewModel vm) return;
        if (!vm.ShowVisualization) return;

        TimerVisualization.Render(
            canvas, bounds,
            vm.VisualizationProgress,
            vm.VisualizationIsRunning,
            vm.VisualizationIsFinished,
            vm.VisualizationRemainingFormatted,
            vm.VisualizationTimerName,
            _animTime,
            vm.VisualizationRemainingSeconds);
    }

    /// <summary>Startet die Render-Loop (~30fps) fuer die Timer-Visualisierung.</summary>
    private void StartRenderLoop()
    {
        // Nur Timer stoppen, nicht die ganze Loop nullen
        _renderTimer?.Stop();

        // Partikel-State zurücksetzen (statische Felder überleben Timer-Wechsel)
        TimerVisualization.Reset();
        _animTime = 0f;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += (_, _) =>
        {
            _animTime += 0.033f;
            TimerVisCanvas?.InvalidateSurface();
        };
        _renderTimer.Start();
    }

    /// <summary>Stoppt die Render-Loop.</summary>
    private void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Event-Handler abmelden (Memory-Leak verhindern)
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        StopRenderLoop();
    }
}
