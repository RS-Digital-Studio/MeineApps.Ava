using Avalonia;
using Avalonia.Labs.Controls;
using BomberBlast.Graphics;

namespace BomberBlast.Controls;

/// <summary>
/// Bindbare SKCanvasView fuer Shop-Upgrade-Icons im ItemsControl.
/// Rendert prozedurale SkiaSharp-Illustrationen pro UpgradeType.
/// </summary>
public class ShopUpgradeIconCanvas : SKCanvasView
{
    public static readonly StyledProperty<int> UpgradeTypeIndexProperty =
        AvaloniaProperty.Register<ShopUpgradeIconCanvas, int>(nameof(UpgradeTypeIndex), -1);

    public static readonly StyledProperty<uint> IconColorArgbProperty =
        AvaloniaProperty.Register<ShopUpgradeIconCanvas, uint>(nameof(IconColorArgb));

    /// <summary>UpgradeType als int-Index (0=StartBombs, 1=StartFire, ...)</summary>
    public int UpgradeTypeIndex
    {
        get => GetValue(UpgradeTypeIndexProperty);
        set => SetValue(UpgradeTypeIndexProperty, value);
    }

    /// <summary>Icon-Farbe als ARGB uint</summary>
    public uint IconColorArgb
    {
        get => GetValue(IconColorArgbProperty);
        set => SetValue(IconColorArgbProperty, value);
    }

    static ShopUpgradeIconCanvas()
    {
        UpgradeTypeIndexProperty.Changed.AddClassHandler<ShopUpgradeIconCanvas>((x, _) => x.InvalidateSurface());
        IconColorArgbProperty.Changed.AddClassHandler<ShopUpgradeIconCanvas>((x, _) => x.InvalidateSurface());
    }

    public ShopUpgradeIconCanvas()
    {
        PaintSurface += OnPaintSurface;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        var bounds = canvas.LocalClipBounds;
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float size = Math.Min(bounds.Width, bounds.Height);

        var color = new SkiaSharp.SKColor(IconColorArgb);
        ShopIconRenderer.Render(canvas, cx, cy, size, UpgradeTypeIndex, color);
    }
}
