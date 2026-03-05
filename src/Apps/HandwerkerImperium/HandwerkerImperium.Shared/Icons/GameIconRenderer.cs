using SkiaSharp;

namespace HandwerkerImperium.Icons;

/// <summary>
/// SkiaSharp-Renderer fuer GameIcons auf bestehenden SKCanvas-Zeichenflaechen.
/// Fuer Icons innerhalb von SkiaSharp Game-Canvases.
/// </summary>
public static class GameIconRenderer
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GameIconKind, SKPath?> _pathCache = new();

    public static void Draw(SKCanvas canvas, GameIconKind kind, SKRect bounds, SKPaint paint)
    {
        var path = GetOrCreatePath(kind);
        if (path == null) return;

        var pathBounds = path.Bounds;
        if (pathBounds.Width < 0.1f || pathBounds.Height < 0.1f) return;

        var scaleX = bounds.Width / pathBounds.Width;
        var scaleY = bounds.Height / pathBounds.Height;
        var scale = Math.Min(scaleX, scaleY);

        canvas.Save();
        canvas.Translate(
            bounds.MidX - pathBounds.MidX * scale,
            bounds.MidY - pathBounds.MidY * scale);
        canvas.Scale(scale);
        canvas.DrawPath(path, paint);
        canvas.Restore();
    }

    public static void DrawAt(SKCanvas canvas, GameIconKind kind, float centerX, float centerY, float size, SKPaint paint)
    {
        var half = size / 2f;
        Draw(canvas, kind, new SKRect(centerX - half, centerY - half, centerX + half, centerY + half), paint);
    }

    private static SKPath? GetOrCreatePath(GameIconKind kind)
    {
        return _pathCache.GetOrAdd(kind, static k =>
        {
            var pathData = GameIconPaths.GetPathData(k);
            if (pathData == null) return null;

            var skPathData = pathData;
            if (skPathData.StartsWith("F0 ") || skPathData.StartsWith("F1 "))
                skPathData = skPathData[3..];

            var path = SKPath.ParseSvgPathData(skPathData);
            if (pathData.StartsWith("F0 "))
                path.FillType = SKPathFillType.EvenOdd;

            return path;
        });
    }

    public static void Cleanup()
    {
        foreach (var path in _pathCache.Values)
            path?.Dispose();
        _pathCache.Clear();
    }
}
