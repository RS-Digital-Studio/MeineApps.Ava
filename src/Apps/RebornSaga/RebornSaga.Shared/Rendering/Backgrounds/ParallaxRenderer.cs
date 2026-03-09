namespace RebornSaga.Rendering.Backgrounds;

using SkiaSharp;

/// <summary>
/// 4-Layer Parallax-System für Tiefe in Dialogszenen und Overworld.
/// Jeder Layer hat eine eigene Scroll-Geschwindigkeit (SpeedMultiplier).
/// Hintere Layer scrollen langsamer als vordere (Tiefenillusion).
/// </summary>
public class ParallaxRenderer
{
    /// <summary>
    /// Ein einzelner Parallax-Layer mit Geschwindigkeitsmultiplikator und Render-Callback.
    /// </summary>
    public class Layer
    {
        /// <summary>Geschwindigkeitsfaktor relativ zum Basis-Scroll (0.0 = statisch, 1.0 = volle Geschwindigkeit).</summary>
        public float SpeedMultiplier { get; set; }

        /// <summary>Render-Callback: (Canvas, Bounds, Offset) => zeichne Layer-Inhalt.</summary>
        public Action<SKCanvas, SKRect, float>? RenderAction { get; set; }
    }

    private readonly List<Layer> _layers = new();
    private float _scrollOffset;

    /// <summary>Aktueller Scroll-Offset (kumuliert über Zeit).</summary>
    public float ScrollOffset => _scrollOffset;

    /// <summary>
    /// Fügt einen Layer hinzu. Reihenfolge = Render-Reihenfolge (erster Layer = hinterster).
    /// </summary>
    /// <param name="speedMultiplier">0.0 = statisch, 0.5 = halbe Geschwindigkeit, 1.0 = volle Geschwindigkeit.</param>
    /// <param name="renderAction">Callback zum Zeichnen des Layer-Inhalts.</param>
    public void AddLayer(float speedMultiplier, Action<SKCanvas, SKRect, float> renderAction)
    {
        _layers.Add(new Layer
        {
            SpeedMultiplier = speedMultiplier,
            RenderAction = renderAction
        });
    }

    /// <summary>
    /// Entfernt alle Layer (z.B. bei Szenen-Wechsel).
    /// </summary>
    public void ClearLayers()
    {
        _layers.Clear();
        _scrollOffset = 0f;
    }

    /// <summary>
    /// Scroll-Offset aktualisieren. Jeden Frame aufrufen.
    /// </summary>
    /// <param name="deltaTime">Vergangene Zeit seit letztem Frame in Sekunden.</param>
    /// <param name="scrollSpeed">Basis-Scroll-Geschwindigkeit in Pixel/Sekunde.</param>
    public void Update(float deltaTime, float scrollSpeed = 10f)
    {
        _scrollOffset += scrollSpeed * deltaTime;
    }

    /// <summary>
    /// Setzt den Scroll-Offset direkt (z.B. für Kamera-gebundenes Parallax).
    /// </summary>
    public void SetOffset(float offset)
    {
        _scrollOffset = offset;
    }

    /// <summary>
    /// Alle Layer von hinten nach vorne rendern. Jeder Layer wird um seinen
    /// individuellen Offset (scrollOffset * speedMultiplier) verschoben.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            var offset = _scrollOffset * layer.SpeedMultiplier;

            canvas.Save();
            canvas.Translate(offset, 0);
            layer.RenderAction?.Invoke(canvas, bounds, offset);
            canvas.Restore();
        }
    }
}
