using Avalonia.Controls;
using Avalonia.Labs.Controls;
using BomberBlast.Graphics;
using BomberBlast.Models.Collection;
using BomberBlast.ViewModels;
using SkiaSharp;

namespace BomberBlast.Views;

public partial class CollectionView : UserControl
{
    public CollectionView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PaintSurface-Handler für Sammlungs-Items (34x34 Icons).
    /// Rendert echte SkiaSharp-Grafiken statt generischer MaterialIcons.
    /// </summary>
    private void OnCollectionItemPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (sender is not SKCanvasView view || view.Tag is not CollectionDisplayItem item)
            return;

        // Nicht-entdeckte Items: Nichts zeichnen (Lock-Icon wird per MaterialIcon angezeigt)
        if (!item.IsDiscovered) return;

        var bounds = canvas.LocalClipBounds;
        float cx = bounds.MidX, cy = bounds.MidY;
        float size = Math.Min(bounds.Width, bounds.Height);

        switch (item.Category)
        {
            case CollectionCategory.Enemies when item.EnemyType.HasValue:
                HelpIconRenderer.DrawEnemy(canvas, cx, cy, size, item.EnemyType.Value);
                break;
            case CollectionCategory.Bosses when item.BossType.HasValue:
                HelpIconRenderer.DrawBoss(canvas, cx, cy, size, item.BossType.Value);
                break;
            case CollectionCategory.PowerUps when item.PowerUpType.HasValue:
                HelpIconRenderer.DrawPowerUp(canvas, cx, cy, size, item.PowerUpType.Value);
                break;
            case CollectionCategory.BombCards when item.BombType.HasValue:
                HelpIconRenderer.DrawBombCard(canvas, cx, cy, size, item.BombType.Value);
                break;
        }
    }

    /// <summary>
    /// PaintSurface-Handler für das Detail-Panel (36x36 Icon).
    /// Nutzt SelectedEntry aus dem ViewModel-DataContext.
    /// </summary>
    private void OnCollectionDetailPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (sender is not SKCanvasView view || view.Tag is not CollectionEntry entry)
            return;

        if (!entry.IsDiscovered) return;

        var bounds = canvas.LocalClipBounds;
        float cx = bounds.MidX, cy = bounds.MidY;
        float size = Math.Min(bounds.Width, bounds.Height);

        switch (entry.Category)
        {
            case CollectionCategory.Enemies when entry.EnemyType.HasValue:
                HelpIconRenderer.DrawEnemy(canvas, cx, cy, size, entry.EnemyType.Value);
                break;
            case CollectionCategory.Bosses when entry.BossType.HasValue:
                HelpIconRenderer.DrawBoss(canvas, cx, cy, size, entry.BossType.Value);
                break;
            case CollectionCategory.PowerUps when entry.PowerUpType.HasValue:
                HelpIconRenderer.DrawPowerUp(canvas, cx, cy, size, entry.PowerUpType.Value);
                break;
            case CollectionCategory.BombCards when entry.BombType.HasValue:
                HelpIconRenderer.DrawBombCard(canvas, cx, cy, size, entry.BombType.Value);
                break;
        }
    }
}
