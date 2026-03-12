using System.Text.RegularExpressions;
using HandwerkerImperium.Services;
using SkiaSharp;

namespace HandwerkerImperium.Icons;

/// <summary>
/// SkiaSharp-Renderer fuer GameIcons auf SKCanvas-Zeichenflaechen.
/// AI-Icons: Vollfarbig gerendert (warme Cartoon-Farben).
/// UI-Icons (Pfeile etc.): Getintet via SKColorFilter.CreateBlendMode(color, SrcIn).
/// </summary>
public static class GameIconRenderer
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GameIconKind, string> _iconPathMap = new();
    private static IGameAssetService? _assetService;

    // Gecachter Paint fuer Bitmap-Rendering (vermeidet GC bei 30fps)
    private static readonly SKPaint _bitmapPaint = new() { IsAntialias = true };
    private static readonly SKPaint _fullColorPaint = new() { IsAntialias = true };
    private static SKColor _lastTintColor;
    private static SKColorFilter? _lastTintFilter;

    // Tintable-Icons werden zentral in GameIcon.TintableIcons verwaltet (keine Duplikation)

    /// <summary>
    /// Initialisiert den Renderer mit dem Asset-Service fuer Bitmap-Icons.
    /// </summary>
    public static void Initialize(IGameAssetService? assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// Zeichnet ein Icon innerhalb eines Rechtecks.
    /// UI-Icons werden per paint.Color getintet, AI-Icons vollfarbig.
    /// </summary>
    public static void Draw(SKCanvas canvas, GameIconKind kind, SKRect bounds, SKPaint paint)
    {
        var bitmap = TryGetBitmap(kind);
        if (bitmap == null) return;

        if (GameIcon.TintableIcons.Contains(kind))
            DrawBitmapTinted(canvas, bitmap, bounds, paint.Color);
        else
            canvas.DrawBitmap(bitmap, bounds, _fullColorPaint);
    }

    /// <summary>
    /// Convenience-Methode: Icon zentriert bei (centerX, centerY) mit Groesse (size).
    /// </summary>
    public static void DrawAt(SKCanvas canvas, GameIconKind kind, float centerX, float centerY, float size, SKPaint paint)
    {
        var half = size / 2f;
        Draw(canvas, kind, new SKRect(centerX - half, centerY - half, centerX + half, centerY + half), paint);
    }

    /// <summary>
    /// Zeichnet ein Bitmap-Icon vollfarbig (ohne Tinting).
    /// Gibt false zurueck wenn kein Bitmap verfuegbar ist.
    /// </summary>
    public static bool DrawFullColor(SKCanvas canvas, GameIconKind kind, SKRect bounds)
    {
        var bitmap = TryGetBitmap(kind);
        if (bitmap == null) return false;

        canvas.DrawBitmap(bitmap, bounds);
        return true;
    }

    /// <summary>
    /// Zeichnet ein Bitmap-Icon vollfarbig bei (centerX, centerY) mit Groesse (size).
    /// Gibt false zurueck wenn kein Bitmap verfuegbar ist.
    /// </summary>
    public static bool DrawFullColorAt(SKCanvas canvas, GameIconKind kind, float centerX, float centerY, float size)
    {
        var half = size / 2f;
        return DrawFullColor(canvas, kind, new SKRect(centerX - half, centerY - half, centerX + half, centerY + half));
    }

    /// <summary>
    /// Zeichnet Bitmap mit Farb-Tinting via ColorFilter (SrcIn).
    /// Ersetzt Icon-Farben mit tintColor, behaelt Alpha-Kanal.
    /// </summary>
    public static void DrawBitmapTinted(SKCanvas canvas, SKBitmap bitmap, SKRect bounds, SKColor tintColor)
    {
        // KEIN Dispose hier - GetTintFilter verwaltet den Cache selbst.
        // Disposed man hier, wird bei gleicher Farbe der gecachte Filter zerstört.
        _bitmapPaint.ColorFilter = GetTintFilter(tintColor);
        canvas.DrawBitmap(bitmap, bounds, _bitmapPaint);
    }

    /// <summary>
    /// Gibt true zurueck wenn fuer dieses Icon ein Bitmap verfuegbar ist.
    /// </summary>
    public static bool HasBitmap(GameIconKind kind)
    {
        return TryGetBitmap(kind) != null;
    }

    private static SKBitmap? TryGetBitmap(GameIconKind kind)
    {
        if (_assetService == null || kind == GameIconKind.None) return null;

        var path = GetIconAssetPath(kind);
        var bmp = _assetService.GetBitmap(path);
        if (bmp == null)
            _ = _assetService.LoadBitmapAsync(path);
        return bmp;
    }

    /// <summary>
    /// Konvertiert GameIconKind zu Asset-Pfad (PascalCase -> snake_case).
    /// ArrowDown -> icons/arrow_down.webp
    /// </summary>
    private static string GetIconAssetPath(GameIconKind kind)
    {
        return _iconPathMap.GetOrAdd(kind, k =>
        {
            var name = Regex.Replace(k.ToString(), "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
            return $"icons/{name}.webp";
        });
    }

    /// <summary>
    /// Gecachter ColorFilter fuer Tinting (vermeidet Allokation bei gleicher Farbe).
    /// </summary>
    private static SKColorFilter GetTintFilter(SKColor color)
    {
        if (color == _lastTintColor && _lastTintFilter != null)
            return _lastTintFilter;

        _lastTintColor = color;
        _lastTintFilter?.Dispose();
        _lastTintFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
        return _lastTintFilter;
    }

    /// <summary>
    /// Gibt den gecachten ColorFilter frei.
    /// Static readonly Paints (_bitmapPaint, _fullColorPaint) werden NICHT disposed -
    /// sie leben bis Prozessende und ein Dispose wuerde nachfolgende Draw-Aufrufe crashen.
    /// </summary>
    public static void Cleanup()
    {
        _lastTintFilter?.Dispose();
        _lastTintFilter = null;
    }
}
