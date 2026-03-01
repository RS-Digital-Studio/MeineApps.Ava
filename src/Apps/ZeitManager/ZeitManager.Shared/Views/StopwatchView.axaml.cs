using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using System.ComponentModel;
using ZeitManager.Graphics;
using ZeitManager.ViewModels;

namespace ZeitManager.Views;

public partial class StopwatchView : UserControl
{
    private DispatcherTimer? _animTimer;
    private float _animTime;

    // ViewModel-Referenz für saubere Event-Abmeldung
    private StopwatchViewModel? _viewModel;

    public StopwatchView()
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
        if (DataContext is StopwatchViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>Selektive Canvas-Invalidierung bei relevanten Property-Änderungen.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not StopwatchViewModel vm) return;

        if (args.PropertyName is nameof(vm.TotalElapsedSeconds) or nameof(vm.IsRunning) or nameof(vm.Laps))
        {
            StopwatchCanvas?.InvalidateSurface();
            UpdateAnimation(vm.IsRunning);
        }
    }

    private void UpdateAnimation(bool isRunning)
    {
        if (isRunning && _animTimer == null)
        {
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animTimer.Tick += (_, _) =>
            {
                _animTime += 0.033f;
                StopwatchCanvas?.InvalidateSurface();
            };
            _animTimer.Start();
        }
        else if (!isRunning && _animTimer != null)
        {
            _animTimer.Stop();
            _animTimer = null;
        }
    }

    private void OnPaintStopwatch(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not StopwatchViewModel vm) return;

        // Direkt die berechnete Property verwenden (kein String-Parsing noetig)
        double elapsedSeconds = vm.TotalElapsedSeconds;

        // Rundenzeiten für Sektor-Darstellung sammeln
        double[]? lapTimesSeconds = null;
        if (vm.Laps.Count > 0)
        {
            lapTimesSeconds = new double[vm.Laps.Count];
            for (int i = 0; i < vm.Laps.Count; i++)
                lapTimesSeconds[i] = vm.Laps[i].LapTime.TotalSeconds;
        }

        StopwatchVisualization.Render(canvas, bounds,
            elapsedSeconds, vm.IsRunning, vm.Laps.Count, _animTime,
            lapTimesSeconds);
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

        _animTimer?.Stop();
        _animTimer = null;
    }
}
