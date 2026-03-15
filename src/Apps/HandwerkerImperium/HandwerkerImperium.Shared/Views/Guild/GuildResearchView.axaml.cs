using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Models;
using HandwerkerImperium.ViewModels;
using Avalonia.Labs.Controls;
using SkiaSharp;

namespace HandwerkerImperium.Views.Guild;

public partial class GuildResearchView : UserControl
{
    private GuildViewModel? _guildVm;
    private readonly GuildResearchBackgroundRenderer _bgRenderer = new();
    private readonly GuildResearchTreeRenderer _treeRenderer = new();
    private readonly GuildHallHeaderRenderer _headerRenderer = new();
    // Gecachte Liste für Render-Loop (vermeidet ToList() pro Frame bei 30fps)
    private List<GuildResearchDisplay> _cachedItems = [];
    private object? _lastGuildResearchRef;
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _treeCanvas;
    private SKCanvasView? _headerCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private DateTime _lastHeaderRenderTime = DateTime.UtcNow;

    // Letzte bekannte Bounds für HitTest
    private SKRect _lastBounds;

    // ═══════════════════════════════════════════════════════════════════════
    // DIRTY-FLAGS (vermeidet unnötige Canvas-Invalidierungen bei 30fps)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Header muss neu gezeichnet werden (bei aktiver Forschung wegen Fackel-Animationen,
    /// oder bei Daten-Änderung/Tab-Wechsel).
    /// </summary>
    private bool _headerDirty = true;

    /// <summary>
    /// Forschungsbaum muss neu gezeichnet werden (Collection geändert, Forschung abgeschlossen).
    /// </summary>
    private bool _treeDirty = true;

    public GuildResearchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) =>
        {
            StopRenderLoop();
            _bgRenderer.Dispose();
            _treeRenderer.Dispose();
            _headerRenderer.Dispose();
        };

        // Timer pausieren wenn View nicht sichtbar ist (Tab-Wechsel, Sub-Navigation)
        // Analog zu WorkshopView und ResearchView: spart ~30 InvalidateSurface/s
        PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty)
            {
                if (IsVisible && _guildVm != null && _renderTimer == null)
                {
                    // View wieder sichtbar: Dirty-Flags setzen fuer sofortigen Render
                    _headerDirty = true;
                    _treeDirty = true;
                    StartRenderLoop();
                }
                else if (!IsVisible && _renderTimer != null)
                {
                    // View versteckt: Timer stoppen
                    _renderTimer.Stop();
                    _renderTimer = null;
                }
            }
        };
    }

    private void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _renderTimer?.Stop();

        // Altes VM abmelden
        if (_guildVm != null)
        {
            _guildVm.PropertyChanged -= OnGuildVmPropertyChanged;
            _guildVm = null;
        }

        if (DataContext is GuildViewModel vm)
        {
            _guildVm = vm;
            _guildVm.PropertyChanged += OnGuildVmPropertyChanged;
            _treeCanvas = this.FindControl<SKCanvasView>("TreeCanvas");
            _headerCanvas = this.FindControl<SKCanvasView>("HeaderCanvas");

            // Beim Binden: Alles als dirty markieren für initialen Render
            _headerDirty = true;
            _treeDirty = true;

            StartRenderLoop();
        }
    }

    /// <summary>
    /// Reagiert auf ViewModel-Property-Änderungen und setzt Dirty-Flags
    /// statt blind alle Canvases zu invalidieren.
    /// </summary>
    private void OnGuildVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GuildViewModel.GuildResearch):
                // Collection wurde neu geladen → Baum + Header müssen aktualisiert werden
                _treeDirty = true;
                _headerDirty = true;
                break;

            case nameof(GuildViewModel.HasActiveResearch):
                // Forschungsstatus geändert → Header (Fortschrittsanzeige) + Baum (Status-Icons)
                _headerDirty = true;
                _treeDirty = true;
                break;

            case nameof(GuildViewModel.ActiveResearchCountdown):
                // Countdown-Text hat sich geändert → Header neu zeichnen
                _headerDirty = true;
                break;

            case nameof(GuildViewModel.IsResearchContributeDialogVisible):
                // Dialog-Status geändert → Baum neu zeichnen (Highlight des ausgewählten Items)
                _treeDirty = true;
                break;
        }
    }

    private int _countdownRefreshCounter;

    private void StartRenderLoop()
    {
        _renderTimer?.Stop();
        _countdownRefreshCounter = 0;
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // 30fps
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    /// <summary>
    /// Render-Tick mit Dirty-Flag-Prüfung (analog zu ResearchView).
    /// Ohne aktive Forschung und ohne Daten-Änderung: KEIN Render → 0 InvalidateSurface()/s.
    /// Bei aktiver Forschung: Header wird für Fackel-Animationen kontinuierlich invalidiert.
    /// </summary>
    private void OnRenderTick(object? sender, EventArgs e)
    {
        bool hasActiveResearch = _guildVm?.HasActiveResearch == true;

        // Header: Bei aktiver Forschung immer (Fackel-Animationen, Fortschrittsring),
        // ansonsten nur wenn dirty (erster Render, Daten-Änderung)
        if (hasActiveResearch || _headerDirty)
        {
            _headerCanvas?.InvalidateSurface();
            _headerDirty = false;
        }

        // Baum: Nur bei Daten-Änderung (Collection-Wechsel, Forschung abgeschlossen)
        if (_treeDirty)
        {
            _treeCanvas?.InvalidateSurface();
            _treeDirty = false;
        }

        // Countdown alle ~1s aktualisieren (30 Ticks × 33ms ≈ 1000ms)
        _countdownRefreshCounter++;
        if (_countdownRefreshCounter >= 30)
        {
            _countdownRefreshCounter = 0;
            _guildVm?.RefreshActiveResearchCountdown();
        }
    }

    private void OnHeaderPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        var now = DateTime.UtcNow;
        float deltaTime = (float)(now - _lastHeaderRenderTime).TotalSeconds;
        _lastHeaderRenderTime = now;

        _headerRenderer.Render(canvas, bounds, deltaTime);
    }

    private void OnTreePaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        // Delta-Time berechnen
        var now = DateTime.UtcNow;
        float deltaTime = (float)(now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;

        // Forschungsdaten aus ViewModel holen (nur bei Collection-Wechsel neu erstellen)
        var currentRef = _guildVm?.GuildResearch;
        if (currentRef != null && !ReferenceEquals(currentRef, _lastGuildResearchRef))
        {
            _cachedItems = currentRef.ToList();
            _lastGuildResearchRef = currentRef;
        }
        var items = _cachedItems;

        // Canvas-Höhe an Baum-Größe anpassen
        float treeHeight = GuildResearchTreeRenderer.CalculateTotalHeight();
        float requiredHeight = Math.Max(treeHeight, bounds.Height);
        if (_treeCanvas != null && Math.Abs(_treeCanvas.Height - requiredHeight) > 1)
        {
            _treeCanvas.Height = requiredHeight;
        }

        // Bounds für den gesamten Canvas (inkl. Scroll-Bereich)
        var renderBounds = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + requiredHeight);
        _lastBounds = renderBounds;

        // 1. Hintergrund (Pergament-Textur)
        _bgRenderer.Render(canvas, renderBounds);

        // 2. Forschungsbaum
        if (items.Count > 0)
        {
            _treeRenderer.Render(canvas, renderBounds, items, deltaTime);
        }
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_guildVm == null || _treeCanvas == null) return;

        var point = e.GetPosition(_treeCanvas);

        // DPI-Skalierung: _lastBounds (SkiaSharp) / Avalonia Bounds
        float boundsW = (float)_treeCanvas.Bounds.Width;
        float boundsH = (float)_treeCanvas.Bounds.Height;
        if (boundsW < 1 || boundsH < 1) return;
        float scaleX = _lastBounds.Width / boundsW;
        float scaleY = _lastBounds.Height / boundsH;
        float tapX = (float)point.X * scaleX;
        float tapY = (float)point.Y * scaleY;

        var items = _cachedItems;
        if (items.Count == 0) return;

        // HitTest über TreeRenderer
        int hitIndex = _treeRenderer.HitTest(tapX, tapY, _lastBounds.MidX, _lastBounds.Top, items.Count);
        if (hitIndex >= 0 && hitIndex < items.Count)
        {
            var item = items[hitIndex];
            // Nur aktive (nicht gesperrte, nicht abgeschlossene) Items können beigetragen werden
            if (item.IsActive && !item.IsCompleted)
            {
                _guildVm.ShowResearchContributeDialogCommand.Execute(item);
            }
        }
    }

    private void OnDialogBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        // Klick auf dunklen Hintergrund schließt den Dialog
        if (e.Source is Border border && border.Background != null)
        {
            _guildVm?.CancelResearchContributeCommand.Execute(null);
        }
    }
}
