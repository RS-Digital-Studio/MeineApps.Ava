using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels;
using SkiaSharp;
using System;

namespace HandwerkerRechner.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    // SkiaSharp Blueprint-Hintergrund
    private readonly BlueprintBackgroundRenderer _backgroundRenderer = new();
    // Lifecycle: Start in OnDataContextChanged (StartRenderTimer), Stop + Tick-Unsubscribe + null
    // in OnDetachedFromVisualTree. DispatcherTimer implementiert kein IDisposable — Stop() genügt.
    private DispatcherTimer? _renderTimer;
    private float _renderTime;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Shader + ViewModel werden bereits in HandwerkerRechnerLoadingPipeline geladen.
        // History und Projekte werden lazy beim Tab-Wechsel geladen (SelectHistoryTab/SelectProjectsTab).
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Render-Timer stoppen und Renderer freigeben
        if (_renderTimer != null)
        {
            _renderTimer.Stop();
            _renderTimer.Tick -= OnRenderTimerTick;
            _renderTimer = null;
        }
        _backgroundRenderer.Dispose();

        // Events sauber abmelden bei Entfernung aus dem Visual Tree
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.ClipboardRequested -= OnClipboardRequested;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.ClipboardRequested -= OnClipboardRequested;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.ClipboardRequested += OnClipboardRequested;

            // Render-Timer einmalig starten wenn VM verfuegbar
            StartRenderTimer();

            // Auf CurrentPage-Wechsel reagieren: Render-Timer pausieren wenn Calculator offen
            // (Hintergrund ist verdeckt - 5fps × 5 Layer GPU-Last sparen)
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            UpdateRenderTimerState();
    }

    /// <summary>
    /// Pausiert den Background-Render-Timer wenn ein Calculator offen ist
    /// (vom CalculatorOverlay verdeckt → keine GPU-Arbeit nötig).
    /// </summary>
    private void UpdateRenderTimerState()
    {
        if (_renderTimer == null || _vm == null) return;

        bool calculatorOpen = !string.IsNullOrEmpty(_vm.CurrentPage);
        if (calculatorOpen && _renderTimer.IsEnabled)
            _renderTimer.Stop();
        else if (!calculatorOpen && !_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    // =====================================================================
    // Render-Timer (~5fps fuer animierten Blueprint-Hintergrund)
    // =====================================================================

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) }; // ~5fps
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        _renderTime += 0.2f;
        _backgroundRenderer.Update(0.2f);
        BackgroundCanvas?.InvalidateSurface();
    }

    // =====================================================================
    // SkiaSharp Paint-Handler
    // =====================================================================

    /// <summary>
    /// Zeichnet den animierten Blueprint-Hintergrund (5 Layer).
    /// </summary>
    private void OnBackgroundPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _backgroundRenderer.Render(canvas, bounds, _renderTime);
    }

    // =====================================================================
    // Game Juice Events (FloatingText + Clipboard)
    // =====================================================================

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            _ => Color.Parse("#3B82F6")
        };

        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;

        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), h * 0.4, color, 16);
    }

    private async void OnClipboardRequested(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                // Avalonia 12: DataTransfer + DataTransferItem (SetTextAsync entfernt)
                var data = new Avalonia.Input.DataTransfer();
                data.Add(Avalonia.Input.DataTransferItem.CreateText(text));
                await clipboard.SetDataAsync(data);
            }
        }
        catch (Exception)
        {
            // Clipboard-Fehler still ignorieren
        }
    }
}
