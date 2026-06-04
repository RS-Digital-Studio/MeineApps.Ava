using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Outline-Pass-Helper (.4 .
///
/// <para>
/// Vereinheitlicht den Look ueber inkonsistente Art-Styles (Vektor-Player + AI-WebP-Bosse +
/// AI-WebP-Enemies sehen aus wie drei verschiedene Spiele in einem Frame). Outline um jede
/// Entity gibt einen gemeinsamen visuellen Anker.
/// </para>
///
/// <para>
/// Mechanik:
/// <list type="number">
/// <item>SaveLayer mit Dilate-ImageFilter + ColorFilter (faerbt alles outlineColor)</item>
/// <item>renderAction(canvas) wird in diesen Layer gezeichnet</item>
/// <item>Restore: ergibt eingefaerbten, dilatierten Sprite auf Canvas (= Outline-Ring)</item>
/// <item>renderAction wird ein zweites Mal aufgerufen — Original-Sprite landet ON TOP</item>
/// </list>
/// </para>
///
/// <para>
/// Performance: renderAction wird zweimal gerendert. Pro Outline-Entity ~2x DrawCalls.
/// Empfohlen fuer 5-10 Entities pro Frame. Filter-Cache vermeidet Dilate-Allokation.
/// </para>
///
/// <para>
/// Verwendung:
/// <code>
/// OutlineRenderHelper.RenderWithOutline(canvas, c =>
/// {
///     // Hier den Sprite zeichnen — wird zweimal aufgerufen.
///     c.DrawBitmap(bossSprite, x, y, paint);
/// }, outlineColor: new SKColor(10, 10, 15), radius: 2f);
/// </code>
/// </para>
/// </summary>
public static class OutlineRenderHelper
{
    /// <summary>Standard-Outline-Farbe — sehr dunkler Ton, Stil-uebergreifend lesbar.</summary>
    public static readonly SKColor DefaultOutlineColor = new(10, 10, 15);

    /// <summary>Standard-Outline-Breite in Pixel (vor DPI-Skalierung).</summary>
    public const float DefaultRadius = 2f;

    private static readonly object _filterLock = new();
    private static SKImageFilter? _cachedDilateFilter;
    private static float _cachedRadius = -1f;

    // Outline-Paint + ColorFilter werden gecacht (analog zum Dilate-Filter). Beide sind NATIVE,
    // finalisierbare SkiaSharp-Objekte — pro Frame neu zu allokieren erzeugte ~2 finalisierbare
    // Objekte je Outline-Entity (Gegner/Spieler) PRO Frame und damit Gen0/Gen1-GC-Druck im
    // Render-Hot-Path (sichtbarer periodischer Stutter ab dem ersten gerenderten Frame).
    private static SKPaint? _cachedOutlinePaint;
    private static SKColorFilter? _cachedColorFilter;
    private static SKColor _cachedPaintColor;
    private static float _cachedPaintRadius = -1f;

    /// <summary>
    /// Rendert die <paramref name="renderAction"/> mit Outline-Ring drumherum.
    /// </summary>
    /// <param name="canvas">Ziel-Canvas.</param>
    /// <param name="renderAction">Zeichnet den Sprite. Wird 2x aufgerufen — innerhalb eines
    /// SaveLayer fuer den Outline-Pass und dann normal fuer den Original-Pass.</param>
    /// <param name="outlineColor">Farbe der Outline (Default: sehr dunkles Schwarz-Blau).</param>
    /// <param name="radius">Outline-Breite in Pixel (Default: 2px).</param>
    public static void RenderWithOutline(
        SKCanvas canvas,
        Action<SKCanvas> renderAction,
        SKColor? outlineColor = null,
        float radius = DefaultRadius)
    {
        var color = outlineColor ?? DefaultOutlineColor;
        var outlinePaint = GetOrCreateOutlinePaint(color, radius);

        // Pass 1: Outline (dilatierter, eingefärbter Sprite)
        canvas.SaveLayer(outlinePaint);
        renderAction(canvas);
        canvas.Restore();

        // Pass 2: Original-Sprite ON TOP des Outline-Rings
        renderAction(canvas);
    }

    /// <summary>
    /// Gibt den gecachten Outline-Paint (inkl. ColorFilter + Dilate-ImageFilter) zurück.
    /// Single-Slot-Cache (outlineColor/radius sind in der Praxis konstant) — vermeidet die
    /// native Per-Frame-Allokation von SKPaint + SKColorFilter im Render-Hot-Path.
    /// </summary>
    private static SKPaint GetOrCreateOutlinePaint(SKColor color, float radius)
    {
        lock (_filterLock)
        {
            // GetOrCreateDilateFilter kann den gecachten Dilate-Filter bei Radius-Wechsel ersetzen
            // (und den alten disposen) — der Referenz-Check unten schützt den gecachten Paint dann
            // vor einem use-after-dispose seines ImageFilters.
            var dilateFilter = GetOrCreateDilateFilter(radius);
            if (_cachedOutlinePaint == null
                || _cachedPaintColor != color
                || Math.Abs(_cachedPaintRadius - radius) > 0.01f
                || !ReferenceEquals(_cachedOutlinePaint.ImageFilter, dilateFilter))
            {
                _cachedOutlinePaint?.Dispose();
                _cachedColorFilter?.Dispose();
                _cachedColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
                _cachedOutlinePaint = new SKPaint
                {
                    ImageFilter = dilateFilter,
                    ColorFilter = _cachedColorFilter,
                    IsAntialias = true,
                };
                _cachedPaintColor = color;
                _cachedPaintRadius = radius;
            }
            return _cachedOutlinePaint;
        }
    }

    /// <summary>
    /// Gibt den gecachten Dilate-Filter fuer den gegebenen Radius zurueck.
    /// Pro Process eine Cache-Slot — wechselt der Radius, wird der Filter neu erzeugt.
    /// </summary>
    private static SKImageFilter GetOrCreateDilateFilter(float radius)
    {
        lock (_filterLock)
        {
            if (_cachedDilateFilter == null || Math.Abs(_cachedRadius - radius) > 0.01f)
            {
                _cachedDilateFilter?.Dispose();
                _cachedDilateFilter = SKImageFilter.CreateDilate(radius, radius);
                _cachedRadius = radius;
            }
            return _cachedDilateFilter;
        }
    }

    /// <summary>
    /// Ruft das beim App-Shutdown auf um den gecachten ImageFilter freizugeben.
    /// Optional — der Filter ueberlebt sonst bis zum Process-Ende.
    /// </summary>
    public static void DisposeSharedResources()
    {
        lock (_filterLock)
        {
            _cachedOutlinePaint?.Dispose();
            _cachedOutlinePaint = null;
            _cachedColorFilter?.Dispose();
            _cachedColorFilter = null;
            _cachedPaintRadius = -1f;
            _cachedDilateFilter?.Dispose();
            _cachedDilateFilter = null;
            _cachedRadius = -1f;
        }
    }
}
