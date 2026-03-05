using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HandwerkerRechner.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    // SkiaSharp Blueprint-Hintergrund
    private readonly BlueprintBackgroundRenderer _backgroundRenderer = new();
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

        var loc = App.Services.GetService<ILocalizationService>();

        // Preloading-Tasks im SplashOverlay konfigurieren
        Splash.PreloadAction = async (reportProgress) =>
        {
            // Schritt 1: SkSL-Shader vorab kompilieren (auf ThreadPool)
            reportProgress(0.0f, loc?.GetString("LoadingShaders") ?? "Grafik-Engine wird vorbereitet...");
            await Task.Run(() => ShaderPreloader.PreloadAll());

            // Schritt 2: History-Service warm machen (erstellt Verzeichnis, liest erste Datei)
            reportProgress(0.50f, loc?.GetString("LoadingDatabase") ?? "Datenbank wird geladen...");
            try
            {
                var historyService = App.Services.GetService<ICalculationHistoryService>();
                if (historyService != null)
                    await historyService.GetAllHistoryAsync(1);
            }
            catch (Exception ex) { Debug.WriteLine($"[SplashPreload] HistoryService: {ex.Message}"); }

            // Schritt 3: Projekte vorladen (via DI statt _vm, da DataContext evtl. noch nicht gesetzt)
            reportProgress(0.70f, loc?.GetString("LoadingProjects") ?? "Projekte werden geladen...");
            try
            {
                var mainVm = App.Services.GetService<MainViewModel>();
                if (mainVm?.ProjectsViewModel != null)
                    await mainVm.ProjectsViewModel.LoadProjectsCommand.ExecuteAsync(null);
            }
            catch (Exception ex) { Debug.WriteLine($"[SplashPreload] Projekte: {ex.Message}"); }

            // Schritt 4: History vorladen
            reportProgress(0.90f, loc?.GetString("LoadingHistory") ?? "Verlauf wird geladen...");
            try
            {
                var mainVm = App.Services.GetService<MainViewModel>();
                if (mainVm?.HistoryViewModel != null)
                    await mainVm.HistoryViewModel.LoadHistoryAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[SplashPreload] History: {ex.Message}"); }

            reportProgress(1.0f, "");
        };
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
            _vm.CelebrationRequested -= OnCelebration;
            _vm.ClipboardRequested -= OnClipboardRequested;
            _vm = null;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.ClipboardRequested -= OnClipboardRequested;
        }

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
            _vm.ClipboardRequested += OnClipboardRequested;

            // Render-Timer einmalig starten wenn VM verfuegbar
            StartRenderTimer();
        }
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
    // Game Juice Events (FloatingText + Celebration + Clipboard)
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

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }

    private async void OnClipboardRequested(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }
        catch (Exception)
        {
            // Clipboard-Fehler still ignorieren
        }
    }
}
