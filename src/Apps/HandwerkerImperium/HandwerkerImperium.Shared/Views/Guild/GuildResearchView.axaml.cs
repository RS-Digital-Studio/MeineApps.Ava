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
    private DispatcherTimer? _renderTimer;
    private SKCanvasView? _treeCanvas;
    private SKCanvasView? _headerCanvas;
    private DateTime _lastRenderTime = DateTime.UtcNow;
    private DateTime _lastHeaderRenderTime = DateTime.UtcNow;

    // Letzte bekannte Bounds für HitTest
    private SKRect _lastBounds;

    public GuildResearchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _renderTimer?.Stop();

        if (_guildVm != null)
            _guildVm = null;

        if (DataContext is GuildViewModel vm)
        {
            _guildVm = vm;
            _treeCanvas = this.FindControl<SKCanvasView>("TreeCanvas");
            _headerCanvas = this.FindControl<SKCanvasView>("HeaderCanvas");
            StartRenderLoop();
        }
    }

    private void StartRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) }; // 20fps
        _renderTimer.Tick += (_, _) =>
        {
            _headerCanvas?.InvalidateSurface();
            _treeCanvas?.InvalidateSurface();
        };
        _renderTimer.Start();
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

        // Forschungsdaten aus ViewModel holen
        List<GuildResearchDisplay> items = [];
        if (_guildVm?.GuildResearch != null)
            items = _guildVm.GuildResearch.ToList();

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

        var items = _guildVm.GuildResearch?.ToList();
        if (items == null || items.Count == 0) return;

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
