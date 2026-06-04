using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Outline-Pass-Helper. Zeichnet einen dunklen Outline-Ring um eine Entity, um inkonsistente
/// Art-Styles (Vektor-Player + AI-WebP-Bosse + AI-WebP-Enemies) optisch zu vereinheitlichen.
///
/// <para><b>Implementierung (GPU-freundlich):</b> Statt <c>SaveLayer + Dilate-ImageFilter</c> — ein
/// Offscreen-Komposit mit Nachbar-Pixel-Readback, das auf dem Avalonia-Skia-Backend ~1,2 s PRO
/// Sprite kostete (Ursache des periodischen Gameplay-Stutters, live profiliert) — wird der Sprite in
/// 8 Richtungen versetzt in einen Layer gezeichnet, der beim Restore über einen reinen
/// <c>SKColorFilter</c> (Per-Pixel <c>SrcIn</c> → Outline-Farbe) eingefärbt wird. ColorFilter ist
/// HW-beschleunigt und erzwingt KEINEN ImageFilter-Readback.</para>
///
/// <para>Kosten: renderAction wird 8× (Ring) + 1× (Original) aufgerufen. Da renderAction reine
/// HW-Draws macht, sind die zusätzlichen Pässe günstig — der teure Teil war ausschließlich der
/// ImageFilter, nicht die Draw-Anzahl. Tint-Paint + ColorFilter werden gecacht (keine Per-Frame-Allokation).</para>
///
/// <para>Verwendung:
/// <code>
/// OutlineRenderHelper.RenderWithOutline(canvas, c => c.DrawBitmap(sprite, x, y, paint));
/// </code></para>
/// </summary>
public static class OutlineRenderHelper
{
    /// <summary>Standard-Outline-Farbe — sehr dunkler Ton, Stil-übergreifend lesbar.</summary>
    public static readonly SKColor DefaultOutlineColor = new(10, 10, 15);

    /// <summary>Standard-Outline-Breite in Pixel (vor DPI-Skalierung).</summary>
    public const float DefaultRadius = 2f;

    // 8er-Nachbarschaft (Einheits-Offsets), wird mit radius skaliert. Diagonalen leicht reduziert
    // (~1/√2) für einen gleichmäßig dicken Ring.
    private static readonly (float dx, float dy)[] Offsets =
    [
        (-1f, 0f), (1f, 0f), (0f, -1f), (0f, 1f),
        (-0.7f, -0.7f), (0.7f, -0.7f), (-0.7f, 0.7f), (0.7f, 0.7f),
    ];

    private static readonly object _lock = new();
    private static SKPaint? _cachedTintLayerPaint;
    private static SKColorFilter? _cachedColorFilter;
    private static SKColor _cachedColor;

    /// <summary>
    /// Rendert <paramref name="renderAction"/> mit Outline-Ring drumherum.
    /// </summary>
    /// <param name="canvas">Ziel-Canvas.</param>
    /// <param name="renderAction">Zeichnet den Sprite. Wird mehrfach aufgerufen (8× versetzt für den
    /// Ring, 1× normal für das Original).</param>
    /// <param name="outlineColor">Farbe der Outline (Default: sehr dunkles Schwarz-Blau).</param>
    /// <param name="radius">Outline-Breite in Pixel (Default: 2px).</param>
    public static void RenderWithOutline(
        SKCanvas canvas,
        Action<SKCanvas> renderAction,
        SKColor? outlineColor = null,
        float radius = DefaultRadius)
    {
        var color = outlineColor ?? DefaultOutlineColor;
        var tintPaint = GetOrCreateTintPaint(color);

        // Outline-Ring: Sprite 8× versetzt in einen Layer zeichnen; der ColorFilter des Layer-Paints
        // setzt beim Restore alle Pixel auf die Outline-Farbe (kein ImageFilter → kein GPU-Stall).
        canvas.SaveLayer(tintPaint);
        foreach (var (dx, dy) in Offsets)
        {
            canvas.Save();
            canvas.Translate(dx * radius, dy * radius);
            renderAction(canvas);
            canvas.Restore();
        }
        canvas.Restore();

        // Original-Sprite ON TOP des Outline-Rings.
        renderAction(canvas);
    }

    /// <summary>
    /// Gecachter Tint-Layer-Paint (ColorFilter SrcIn → Outline-Farbe). Single-Slot-Cache — vermeidet
    /// die native Per-Frame-Allokation von SKPaint + SKColorFilter im Render-Hot-Path.
    /// </summary>
    private static SKPaint GetOrCreateTintPaint(SKColor color)
    {
        lock (_lock)
        {
            if (_cachedTintLayerPaint == null || _cachedColor != color)
            {
                _cachedTintLayerPaint?.Dispose();
                _cachedColorFilter?.Dispose();
                _cachedColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
                _cachedTintLayerPaint = new SKPaint
                {
                    ColorFilter = _cachedColorFilter,
                    IsAntialias = true,
                };
                _cachedColor = color;
            }
            return _cachedTintLayerPaint;
        }
    }

    /// <summary>App-Shutdown-Cleanup. Gecachte native Skia-Objekte freigeben.</summary>
    public static void DisposeSharedResources()
    {
        lock (_lock)
        {
            _cachedTintLayerPaint?.Dispose();
            _cachedTintLayerPaint = null;
            _cachedColorFilter?.Dispose();
            _cachedColorFilter = null;
        }
    }
}
