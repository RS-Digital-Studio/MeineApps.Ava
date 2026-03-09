namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;

/// <summary>Zeichnet den Himmel-Gradient (gecacht pro SkyDef + Bounds).</summary>
public static class SkyRenderer
{
    private static readonly SKPaint _paint = new() { IsAntialias = true };
    private static SKShader? _cachedShader;
    private static SkyDef? _cachedDef;
    private static float _cachedWidth;
    private static float _cachedHeight;

    public static void Render(SKCanvas canvas, SKRect bounds, SkyDef sky)
    {
        if (_cachedShader == null || _cachedDef != sky ||
            _cachedWidth != bounds.Width || _cachedHeight != bounds.Height)
        {
            _cachedShader?.Dispose();
            _cachedShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, bounds.Top),
                new SKPoint(bounds.MidX, bounds.Bottom),
                new[] { sky.Top, sky.Mid, sky.Bottom },
                new[] { 0f, sky.MidStop, 1f },
                SKShaderTileMode.Clamp);
            _cachedDef = sky;
            _cachedWidth = bounds.Width;
            _cachedHeight = bounds.Height;
        }

        _paint.Shader = _cachedShader;
        canvas.DrawRect(bounds, _paint);
        _paint.Shader = null;
    }

    public static void Cleanup()
    {
        _cachedShader?.Dispose();
        _cachedShader = null;
        _paint.Dispose();
    }
}
