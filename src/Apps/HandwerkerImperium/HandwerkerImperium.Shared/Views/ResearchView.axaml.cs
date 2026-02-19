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

    // ═══════════════════════════════════════════════════════════════════════
    // CANVAS-REFERENZEN
    // ═══════════════════════════════════════════════════════════════════════

    private SKCanvasView? _headerCanvas;
    private SKCanvasView? _activeResearchCanvas;
    private SKCanvasView? _tabCanvas;
    private SKCanvasView? _bannerCanvas;
    private SKCanvasView? _treeCanvas;
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

    public ResearchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
            _celebrationCanvas = this.FindControl<SKCanvasView>("CelebrationCanvas");

            // PaintSurface-Handler registrieren (erst -= dann += gegen Doppelregistrierung)
            if (_headerCanvas != null) { _headerCanvas.PaintSurface -= OnHeaderPaintSurface; _headerCanvas.PaintSurface += OnHeaderPaintSurface; }
            if (_activeResearchCanvas != null) { _activeResearchCanvas.PaintSurface -= OnActivePaintSurface; _activeResearchCanvas.PaintSurface += OnActivePaintSurface; }
            if (_tabCanvas != null) { _tabCanvas.PaintSurface -= OnTabPaintSurface; _tabCanvas.PaintSurface += OnTabPaintSurface; }
            if (_bannerCanvas != null) { _bannerCanvas.PaintSurface -= OnBannerPaintSurface; _bannerCanvas.PaintSurface += OnBannerPaintSurface; }
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
    /// Reagiert auf ViewModel-Property-Änderungen (Tab-Wechsel → Höhe neu berechnen).
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResearchViewModel.SelectedTab) ||
            e.PropertyName == nameof(ResearchViewModel.SelectedBranch))
        {
            UpdateTreeCanvasHeight();
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
        }
        else
        {
            _treeCanvas.Height = 200; // Fallback
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDER-LOOP (20 fps)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
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

        // Alle Canvas-Elemente invalidieren
        _headerCanvas?.InvalidateSurface();
        _tabCanvas?.InvalidateSurface();
        _bannerCanvas?.InvalidateSurface();
        _treeCanvas?.InvalidateSurface();

        // Aktive Forschung nur wenn sichtbar
        if (_vm?.HasActiveResearch == true)
        {
            _activeResearchCanvas?.InvalidateSurface();
        }

        // Celebration nur wenn aktiv
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
            delta);
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

        // Tab-Labels ohne Emoji-Prefix (der Renderer zeichnet eigene Icons)
        string toolsLabel = _vm.ToolsBranchLabel;
        string mgmtLabel = _vm.ManagementBranchLabel;
        string mktgLabel = _vm.MarketingBranchLabel;

        // Emoji-Prefix entfernen (z.B. "🔧 Werkzeuge" → "Werkzeuge")
        toolsLabel = StripEmojiPrefix(toolsLabel);
        mgmtLabel = StripEmojiPrefix(mgmtLabel);
        mktgLabel = StripEmojiPrefix(mktgLabel);

        _tabRenderer.Render(canvas, bounds, _vm.SelectedTab,
            toolsLabel, mgmtLabel, mktgLabel, delta);
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

        // Branch-Name (ohne Emoji)
        string branchName = _vm.SelectedTab switch
        {
            ResearchBranch.Tools => StripEmojiPrefix(_vm.ToolsBranchLabel),
            ResearchBranch.Management => StripEmojiPrefix(_vm.ManagementBranchLabel),
            ResearchBranch.Marketing => StripEmojiPrefix(_vm.MarketingBranchLabel),
            _ => ""
        };

        _bannerRenderer.Render(canvas, bounds, _vm.SelectedTab,
            branchName, researchedCount, totalCount, delta);
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

    /// <summary>
    /// Entfernt Emoji-Prefix aus Tab-Labels (z.B. "🔧 Werkzeuge" → "Werkzeuge").
    /// </summary>
    private static string StripEmojiPrefix(string label)
    {
        if (string.IsNullOrEmpty(label)) return label;

        // Emoji + Leerzeichen entfernen (Emojis sind Unicode > U+1F000 oder spezielle Zeichen)
        int spaceIdx = label.IndexOf(' ');
        if (spaceIdx > 0 && spaceIdx < 4)
        {
            return label[(spaceIdx + 1)..];
        }

        return label;
    }
}
