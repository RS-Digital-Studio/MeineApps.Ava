using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class ResearchView : UserControl
{
    // ═══════════════════════════════════════════════════════════════════════
    // RENDERER
    // ═══════════════════════════════════════════════════════════════════════

    private readonly ResearchLabRenderer _labRenderer = new();
    private readonly ResearchActiveRenderer _activeRenderer = new();
    private readonly ResearchTabRenderer _tabRenderer = new();
    private readonly ResearchBranchBannerRenderer _bannerRenderer = new();
    private readonly ResearchTreeRenderer _treeRenderer = new();
    private readonly ResearchCelebrationRenderer _celebrationRenderer = new();
    private readonly ResearchBackgroundRenderer _bgRenderer = new();

    // ═══════════════════════════════════════════════════════════════════════
    // CANVAS-REFERENZEN
    // ═══════════════════════════════════════════════════════════════════════

    private SKCanvasView? _headerCanvas;
    private SKCanvasView? _activeResearchCanvas;
    private SKCanvasView? _tabCanvas;
    private SKCanvasView? _bannerCanvas;
    private SKCanvasView? _treeCanvas;
    private SKCanvasView? _treeBackgroundCanvas;
    private SKCanvasView? _celebrationCanvas;

    // ═══════════════════════════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════════════════════════

    private DispatcherTimer? _renderTimer;
    private ResearchViewModel? _vm;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private float _currentDelta;

    /// <summary>
    /// Letzte bekannte Bounds des TreeCanvas (für Touch-HitTest DPI-Skalierung).
    /// </summary>
    private SKRect _lastTreeBounds;

    // ═══════════════════════════════════════════════════════════════════════
    // DIRTY-FLAGS (vermeidet unnötige Canvas-Invalidierungen)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Header muss neu gezeichnet werden (aktive Forschung mit Animationen oder erster Render).
    /// Ohne aktive Forschung sind die Hintergrund-Animationen eingefroren.
    /// </summary>
    private bool _headerDirty = true;

    /// <summary>
    /// Baum-Canvas muss neu gezeichnet werden (Forschung gestartet/abgeschlossen, Daten geändert).
    /// </summary>
    private bool _treeDataDirty = true;

    /// <summary>
    /// Tab-Canvas muss neu gezeichnet werden (Branch-Wechsel).
    /// </summary>
    private bool _tabDirty = true;

    /// <summary>
    /// Banner-Canvas muss neu gezeichnet werden (Branch-Wechsel).
    /// </summary>
    private bool _bannerDirty = true;

    public ResearchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _renderTimer?.Stop();
        _renderTimer = null;
        _labRenderer.Dispose();
        _activeRenderer.Dispose();
        _tabRenderer.Dispose();
        _bannerRenderer.Dispose();
        _treeRenderer.Dispose();
        _celebrationRenderer.Dispose();
        _bgRenderer.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATACONTEXT-VERDRAHTUNG
    // ═══════════════════════════════════════════════════════════════════════

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes VM abmelden, Events lösen
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.CelebrationRequested -= OnCelebrationRequested;
            _vm = null;
        }

        if (DataContext is ResearchViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.CelebrationRequested += OnCelebrationRequested;

            // Canvas-Referenzen finden
            _headerCanvas = this.FindControl<SKCanvasView>("ResearchCanvas");
            _activeResearchCanvas = this.FindControl<SKCanvasView>("ActiveResearchCanvas");
            _tabCanvas = this.FindControl<SKCanvasView>("TabCanvas");
            _bannerCanvas = this.FindControl<SKCanvasView>("BranchBannerCanvas");
            _treeCanvas = this.FindControl<SKCanvasView>("TreeCanvas");
            _treeBackgroundCanvas = this.FindControl<SKCanvasView>("TreeBackgroundCanvas");
            _celebrationCanvas = this.FindControl<SKCanvasView>("CelebrationCanvas");

            // PaintSurface-Handler registrieren (erst -= dann += gegen Doppelregistrierung)
            if (_headerCanvas != null) { _headerCanvas.PaintSurface -= OnHeaderPaintSurface; _headerCanvas.PaintSurface += OnHeaderPaintSurface; }
            if (_activeResearchCanvas != null) { _activeResearchCanvas.PaintSurface -= OnActivePaintSurface; _activeResearchCanvas.PaintSurface += OnActivePaintSurface; }
            if (_tabCanvas != null) { _tabCanvas.PaintSurface -= OnTabPaintSurface; _tabCanvas.PaintSurface += OnTabPaintSurface; }
            if (_bannerCanvas != null) { _bannerCanvas.PaintSurface -= OnBannerPaintSurface; _bannerCanvas.PaintSurface += OnBannerPaintSurface; }
            if (_treeBackgroundCanvas != null) { _treeBackgroundCanvas.PaintSurface -= OnTreeBackgroundPaintSurface; _treeBackgroundCanvas.PaintSurface += OnTreeBackgroundPaintSurface; }
            if (_treeCanvas != null)
            {
                _treeCanvas.PaintSurface -= OnTreePaintSurface;
                _treeCanvas.PaintSurface += OnTreePaintSurface;
                // Tunnel-Routing damit Touch VOR dem ScrollViewer ankommt
                _treeCanvas.RemoveHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnTreePointerPressed);
                _treeCanvas.AddHandler(
                    Avalonia.Input.InputElement.PointerPressedEvent,
                    OnTreePointerPressed,
                    Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
            if (_celebrationCanvas != null) { _celebrationCanvas.PaintSurface -= OnCelebrationPaintSurface; _celebrationCanvas.PaintSurface += OnCelebrationPaintSurface; }

            // TreeCanvas-Höhe berechnen
            UpdateTreeCanvasHeight();

            StartRenderLoop();
        }
        else
        {
            StopRenderLoop();
        }
    }

    /// <summary>
    /// Reagiert auf ViewModel-Property-Änderungen (Tab-Wechsel, Forschungsstatus etc.)
    /// und setzt die entsprechenden Dirty-Flags für selektive Canvas-Invalidierung.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ResearchViewModel.SelectedTab):
                // Branch-Wechsel: Alle abhängigen Canvas als dirty markieren
                _tabDirty = true;
                _bannerDirty = true;
                _treeDataDirty = true;
                _headerDirty = true;
                UpdateTreeCanvasHeight();
                break;

            case nameof(ResearchViewModel.SelectedBranch):
                // Daten haben sich geändert
                _treeDataDirty = true;
                _bannerDirty = true;
                UpdateTreeCanvasHeight();
                break;

            case nameof(ResearchViewModel.HasActiveResearch):
                // Forschungsstatus geändert: Header + Baum müssen aktualisiert werden
                _headerDirty = true;
                _treeDataDirty = true;
                break;

            case nameof(ResearchViewModel.ActiveResearchProgress):
                // Fortschritt geändert: Header zeigt Fortschrittsbalken
                _headerDirty = true;
                break;
        }
    }

    /// <summary>
    /// Berechnet und setzt die Höhe des TreeCanvas basierend auf der Anzahl der Items.
    /// </summary>
    private void UpdateTreeCanvasHeight()
    {
        if (_treeCanvas == null || _vm == null) return;

        var items = _vm.SelectedBranch;
        if (items.Count > 0)
        {
            float height = ResearchTreeRenderer.CalculateTotalHeight(items.Count);
            _treeCanvas.Height = height;
            // Background-Canvas auf dieselbe Höhe setzen
            if (_treeBackgroundCanvas != null) _treeBackgroundCanvas.Height = height;
        }
        else
        {
            _treeCanvas.Height = 200; // Fallback
            if (_treeBackgroundCanvas != null) _treeBackgroundCanvas.Height = 200;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDER-LOOP (30 fps)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // 30fps
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    private void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        // Delta einmal pro Tick berechnen, damit alle Canvas das gleiche Delta bekommen
        var now = DateTime.UtcNow;
        _currentDelta = Math.Min((float)(now - _lastRenderTime).TotalSeconds, 0.1f);
        _lastRenderTime = now;

        bool hasActiveResearch = _vm?.HasActiveResearch == true;

        // Header: Bei aktiver Forschung immer (Animationen: Funken, Fortschrittsbalken),
        // ansonsten nur wenn dirty (erster Render, Tab-Wechsel etc.)
        if (hasActiveResearch || _headerDirty)
        {
            _headerCanvas?.InvalidateSurface();
            _headerDirty = false;
        }

        // Tab-Canvas: Nur bei Branch-Wechsel (statische Darstellung mit animiertem Unterstrich)
        if (_tabDirty)
        {
            _tabCanvas?.InvalidateSurface();
            _tabDirty = false;
        }

        // Banner-Canvas: Nur bei Branch-Wechsel (animierte Branch-Szene)
        if (_bannerDirty)
        {
            _bannerCanvas?.InvalidateSurface();
            _bannerDirty = false;
        }

        // Baum-Canvas + Hintergrund: Nur bei Datenänderung (Forschung gestartet/abgeschlossen)
        if (_treeDataDirty)
        {
            _treeBackgroundCanvas?.InvalidateSurface();
            _treeCanvas?.InvalidateSurface();
            _treeDataDirty = false;
        }

        // Aktive Forschung: Immer invalidieren wenn sichtbar (Timer-Animation)
        if (hasActiveResearch)
        {
            _activeResearchCanvas?.InvalidateSurface();
        }

        // Celebration: Nur wenn aktiv (Confetti-Animation)
        if (_celebrationRenderer.IsActive)
        {
            _celebrationCanvas?.InvalidateSurface();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PAINT-SURFACE HANDLER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Header: Forschungslabor-Hintergrund (animierte Laborszene).
    /// </summary>
    private void OnHeaderPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        float delta = CalculateDelta();

        bool hasActive = _vm?.HasActiveResearch ?? false;
        float progress = (float)(_vm?.ActiveResearchProgress ?? 0.0);

        _labRenderer.Render(canvas, bounds, hasActive, progress, delta);
    }

    /// <summary>
    /// Aktive Forschung: Reagenzglas mit Animation, Countdown, Fortschritt.
    /// </summary>
    private void OnActivePaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        if (_vm == null || !_vm.HasActiveResearch) return;

        float delta = CalculateDelta();
        var branch = _vm.ActiveResearch?.Branch ?? _vm.SelectedTab;

        _activeRenderer.Render(canvas, bounds,
            _vm.ActiveResearchName,
            _vm.ActiveResearchTimeRemaining,
            (float)_vm.ActiveResearchProgress,
            branch,
            delta,
            _vm.ResearchRunningLabel);
    }

    /// <summary>
    /// Tab-Leiste: 3 Tabs mit animiertem Unterstrich.
    /// </summary>
    private void OnTabPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        if (_vm == null) return;

        float delta = CalculateDelta();

        // Tab-Labels direkt verwenden (Emoji-Migration abgeschlossen)
        _tabRenderer.Render(canvas, bounds, _vm.SelectedTab,
            _vm.ToolsBranchLabel, _vm.ManagementBranchLabel, _vm.MarketingBranchLabel, delta);
    }

    /// <summary>
    /// Branch-Banner: Animierte Szene + Fortschrittsanzeige.
    /// </summary>
    private void OnBannerPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        if (_vm == null) return;

        float delta = CalculateDelta();

        // Erforschte Items zählen
        var items = _vm.SelectedBranch;
        int researchedCount = items.Count(i => i.IsResearched);
        int totalCount = items.Count;

        // Branch-Name ohne Emoji-Prefix (SkiaSharp kann Unicode-Emojis nicht rendern)
        string branchLabel = _vm.SelectedTab switch
        {
            ResearchBranch.Tools => _vm.ToolsBranchLabel,
            ResearchBranch.Management => _vm.ManagementBranchLabel,
            ResearchBranch.Marketing => _vm.MarketingBranchLabel,
            _ => ""
        };
        // Emoji-Prefix entfernen (Format: "🔧 Werkzeuge" → "Werkzeuge")
        string branchName = branchLabel.Length > 2 && char.IsSurrogate(branchLabel[0])
            ? branchLabel[2..].TrimStart()
            : branchLabel;

        _bannerRenderer.Render(canvas, bounds, _vm.SelectedTab,
            branchName, researchedCount, totalCount, delta);
    }

    /// <summary>
    /// Tree-Hintergrund: Blaupause-Grid mit Zahnrad-Wasserzeichen und Vignette.
    /// </summary>
    private void OnTreeBackgroundPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        _bgRenderer.Render(canvas, bounds);
    }

    /// <summary>
    /// Research-Tree: 2D-Baum-Netzwerk mit Icons, Verbindungslinien, Fortschritt.
    /// </summary>
    private void OnTreePaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        // Bounds für Touch-HitTest speichern
        _lastTreeBounds = bounds;

        if (_vm == null) return;

        float delta = CalculateDelta();
        var items = _vm.SelectedBranch;

        if (items.Count > 0)
        {
            _treeRenderer.Render(canvas, bounds, items, _vm.SelectedTab, delta);
        }
    }

    /// <summary>
    /// Celebration: Goldene Glow-Ringe + Confetti + Bonus-Text (über allem).
    /// </summary>
    private void OnCelebrationPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        if (!_celebrationRenderer.IsActive) return;

        float delta = CalculateDelta();
        _celebrationRenderer.Render(canvas, bounds, delta);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOUCH-HANDLING (TreeCanvas → HitTest → Forschung starten)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Touch auf dem TreeCanvas → HitTest → wenn ein startbarer Node getroffen wird, Forschung starten.
    /// </summary>
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null || _treeCanvas == null) return;

        var items = _vm.SelectedBranch;
        if (items.Count == 0) return;

        // Pointer-Position relativ zum TreeCanvas
        var pos = e.GetPosition(_treeCanvas);

        // DPI-Skalierung: Render-Bounds / Control-Bounds
        float scaleX = _lastTreeBounds.Width / (float)_treeCanvas.Bounds.Width;
        float scaleY = _lastTreeBounds.Height / (float)_treeCanvas.Bounds.Height;

        float tapX = (float)pos.X * scaleX;
        float tapY = (float)pos.Y * scaleY;

        // HitTest im Renderer
        string? hitId = _treeRenderer.HitTest(tapX, tapY, items, _lastTreeBounds.MidX, _lastTreeBounds.Top);

        if (!string.IsNullOrEmpty(hitId))
        {
            // Forschung starten
            _vm.StartResearchCommand.Execute(hitId);
            e.Handled = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CELEBRATION API (vom ViewModel aufrufbar)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird vom ViewModel gefeuert wenn eine Forschung abgeschlossen wird.
    /// </summary>
    private void OnCelebrationRequested(object? sender, (ResearchBranch Branch, string BonusText) args)
    {
        TriggerCelebration(args.Branch, args.BonusText);
    }

    /// <summary>
    /// Startet die Celebration-Animation (aufgerufen wenn eine Forschung abgeschlossen wird).
    /// </summary>
    public void TriggerCelebration(ResearchBranch branch, string bonusText)
    {
        _celebrationRenderer.StartCelebration(branch, bonusText);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die im aktuellen Tick berechnete Delta-Zeit zurück.
    /// Wird einmal pro Tick in OnRenderTick berechnet, damit alle Canvas
    /// das gleiche Delta bekommen (vorher bekam die letzte Canvas ~0ms).
    /// </summary>
    private float CalculateDelta() => _currentDelta;

}
