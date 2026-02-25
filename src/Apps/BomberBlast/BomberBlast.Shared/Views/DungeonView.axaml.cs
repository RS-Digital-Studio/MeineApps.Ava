using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BomberBlast.Graphics;
using BomberBlast.Models.Dungeon;
using BomberBlast.ViewModels;
using SkiaSharp;

namespace BomberBlast.Views;

/// <summary>
/// Code-Behind für DungeonView: SkiaSharp Node-Map Rendering + Touch-Interaktion.
/// AnimationsTimer (~30fps) für pulsierende Nodes, PointerPressed für Node-Auswahl.
/// </summary>
public partial class DungeonView : UserControl
{
    private DungeonViewModel? _vm;
    private DispatcherTimer? _mapTimer;
    private DateTime _mapTimerStart;
    private float _mapAnimTime;

    public DungeonView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _vm = DataContext as DungeonViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DungeonViewModel.IsMapSelection))
                {
                    if (_vm.IsMapSelection)
                        StartMapTimer();
                    else
                        StopMapTimer();
                }

                // MapData geändert → Canvas-Höhe aktualisieren
                if (args.PropertyName == nameof(DungeonViewModel.MapData))
                    UpdateMapCanvasHeight();
            };
        }
    }

    protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_vm?.IsMapSelection == true)
        {
            UpdateMapCanvasHeight();
            StartMapTimer();
        }
    }

    protected override void OnUnloaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        StopMapTimer();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MAP-ANIMATIONS-TIMER (~30fps für pulsierende Nodes)
    // ═══════════════════════════════════════════════════════════════════════

    private void StartMapTimer()
    {
        _mapTimerStart = DateTime.UtcNow;
        _mapAnimTime = 0f;

        if (_mapTimer == null)
        {
            _mapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30fps
            _mapTimer.Tick += OnMapTimerTick;
        }

        _mapTimer.Start();
        UpdateMapCanvasHeight();
        MapCanvas?.InvalidateSurface();
    }

    private void StopMapTimer()
    {
        _mapTimer?.Stop();
    }

    private void OnMapTimerTick(object? sender, EventArgs e)
    {
        _mapAnimTime = (float)(DateTime.UtcNow - _mapTimerStart).TotalSeconds;
        MapCanvas?.InvalidateSurface();
    }

    /// <summary>
    /// Setzt die Höhe des MapCanvas basierend auf der Anzahl der Map-Reihen.
    /// </summary>
    private void UpdateMapCanvasHeight()
    {
        if (MapCanvas == null || _vm?.MapData == null) return;
        int rowCount = _vm.MapData.Rows.Count;
        if (rowCount == 0) return;

        float height = DungeonMapRenderer.GetMapHeight(rowCount);
        MapCanvas.Height = Math.Max(height, 200);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SKCANVASVIEW PAINT
    // ═══════════════════════════════════════════════════════════════════════

    private void MapCanvas_PaintSurface(object? sender, Avalonia.Labs.Controls.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        if (_vm?.MapData == null || _vm.MapData.Rows.Count == 0) return;

        DungeonMapRenderer.Render(
            canvas,
            bounds.Width,
            bounds.Height,
            _vm.MapData,
            _vm.MapCurrentFloor,
            _mapAnimTime,
            scrollOffset: 0); // Kein manueller Scroll-Offset, ScrollViewer handelt das
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOUCH / CLICK → NODE-AUSWAHL
    // ═══════════════════════════════════════════════════════════════════════

    private void MapCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm?.MapData == null || !_vm.IsMapSelection) return;
        if (MapCanvas == null) return;

        var point = e.GetPosition(MapCanvas);

        // DPI-Skalierung: Proportionale Umrechnung (Control-Bounds → Render-Bounds)
        float scaleX = (float)(MapCanvas.Bounds.Width > 0
            ? MapCanvas.Bounds.Width / MapCanvas.Bounds.Width
            : 1.0);
        float scaleY = (float)(MapCanvas.Bounds.Height > 0
            ? MapCanvas.Bounds.Height / MapCanvas.Bounds.Height
            : 1.0);

        float tapX = (float)point.X * scaleX;
        float tapY = (float)point.Y * scaleY;

        // Hit-Test: Welcher Node wurde getippt?
        var hitNode = DungeonMapRenderer.HitTestNode(
            tapX, tapY,
            _vm.MapData,
            (float)MapCanvas.Bounds.Width);

        if (hitNode != null)
        {
            _vm.SelectMapNodeCommand.Execute(hitNode);
        }
    }
}
