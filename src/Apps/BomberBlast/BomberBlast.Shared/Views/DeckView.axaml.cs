using Avalonia.Controls;
using Avalonia.Labs.Controls;
using BomberBlast.Graphics;
using BomberBlast.Models.Entities;
using BomberBlast.ViewModels;
using SkiaSharp;

namespace BomberBlast.Views;

public partial class DeckView : UserControl
{
    public DeckView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PaintSurface-Handler für Karten im Sammlung-Grid.
    /// Rendert SkiaSharp Bomben-Icons - auch für gesperrte Karten (Silhouette).
    /// </summary>
    private void OnDeckCardPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (sender is not SKCanvasView view || view.Tag is not CardDisplayItem card)
            return;

        var bounds = canvas.LocalClipBounds;
        HelpIconRenderer.DrawBombCard(canvas, bounds.MidX, bounds.MidY,
            Math.Min(bounds.Width, bounds.Height), card.BombType);
    }

    /// <summary>
    /// PaintSurface-Handler für Deck-Slots (16x16).
    /// Zeigt Mini-Bomben-Icons in belegten Slots.
    /// </summary>
    private void OnDeckSlotPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (sender is not SKCanvasView view || view.Tag is not DeckSlotItem slot)
            return;

        if (slot.IsEmpty) return;

        var bounds = canvas.LocalClipBounds;
        HelpIconRenderer.DrawBombCard(canvas, bounds.MidX, bounds.MidY,
            Math.Min(bounds.Width, bounds.Height), slot.BombType);
    }

    /// <summary>
    /// PaintSurface-Handler für das Karten-Detail-Panel (24x24).
    /// Zeigt das Icon der aktuell ausgewählten Karte.
    /// </summary>
    private void OnDeckDetailPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (DataContext is not DeckViewModel vm || vm.SelectedCard == null)
            return;

        var bounds = canvas.LocalClipBounds;
        HelpIconRenderer.DrawBombCard(canvas, bounds.MidX, bounds.MidY,
            Math.Min(bounds.Width, bounds.Height), vm.SelectedCard.BombType);
    }
}
