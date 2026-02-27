using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using BomberBlast.Graphics;
using BomberBlast.Models.Entities;
using BomberBlast.ViewModels;
using SkiaSharp;

namespace BomberBlast.Views;

public partial class DeckView : UserControl
{
    private DeckViewModel? _vm;

    public DeckView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Alte Subscription abmelden
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as DeckViewModel;

        // Neue Subscription anmelden
        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    /// <summary>
    /// Bei Kartenwechsel die Detail-Canvas neu zeichnen.
    /// </summary>
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SelectedCard" or "HasSelectedCard")
            DetailBombCanvas?.InvalidateSurface();
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
    /// PaintSurface-Handler für Deck-Slots (22x22).
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
    /// PaintSurface-Handler für das Karten-Detail-Panel (36x36).
    /// Zeigt das Icon der aktuell ausgewählten Karte.
    /// </summary>
    private void OnDeckDetailPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (_vm?.SelectedCard == null)
            return;

        var bounds = canvas.LocalClipBounds;
        HelpIconRenderer.DrawBombCard(canvas, bounds.MidX, bounds.MidY,
            Math.Min(bounds.Width, bounds.Height), _vm.SelectedCard.BombType);
    }
}
