using SkiaSharp;

namespace BomberBlast.Input;

/// <summary>
/// Gemeinsamer Renderer fuer Bomb- und Detonator-Buttons (alle Input-Handler)
/// </summary>
public static class BombButtonRenderer
{
    /// <summary>
    /// Zeichnet den Bomb-Button (roter Kreis mit Bomben-Icon)
    /// </summary>
    public static void RenderBombButton(SKCanvas canvas, float cx, float cy, float radius,
        bool isPressed, byte alpha, SKPaint bgPaint, SKPaint bombPaint, SKPaint fusePaint, SKPath fusePath)
    {
        // Hintergrund
        bgPaint.Color = isPressed
            ? new SKColor(255, 100, 100, alpha)
            : new SKColor(255, 50, 50, alpha);
        canvas.DrawCircle(cx, cy, radius, bgPaint);

        // Bomben-Icon
        bombPaint.Color = new SKColor(0, 0, 0, alpha);
        float bombSize = radius * 0.5f;
        canvas.DrawCircle(cx, cy + bombSize * 0.1f, bombSize, bombPaint);

        // Lunte
        fusePaint.Color = new SKColor(255, 200, 0, alpha);
        fusePath.Reset();
        fusePath.MoveTo(cx, cy - bombSize);
        fusePath.QuadTo(
            cx + bombSize * 0.3f, cy - bombSize - 10,
            cx + bombSize * 0.5f, cy - bombSize - 5);
        canvas.DrawPath(fusePath, fusePaint);

        // Funke
        canvas.DrawCircle(cx + bombSize * 0.5f, cy - bombSize - 5, 4, bombPaint);
    }

    /// <summary>
    /// Zeichnet den Detonator-Button (blauer Kreis mit Blitz-Icon)
    /// </summary>
    public static void RenderDetonatorButton(SKCanvas canvas, float cx, float cy, float radius,
        bool isPressed, byte alpha, SKPaint bgPaint, SKPaint iconPaint)
    {
        // Hintergrund (blau)
        bgPaint.Color = isPressed
            ? new SKColor(100, 150, 255, alpha)
            : new SKColor(50, 100, 220, alpha);
        canvas.DrawCircle(cx, cy, radius, bgPaint);

        // Blitz-Icon (Detonator-Symbol)
        iconPaint.Color = new SKColor(255, 255, 255, alpha);
        float s = radius * 0.4f;
        // Blitz-Zickzack
        canvas.DrawLine(cx + s * 0.1f, cy - s, cx - s * 0.3f, cy, iconPaint);
        canvas.DrawLine(cx - s * 0.3f, cy, cx + s * 0.2f, cy + s * 0.1f, iconPaint);
        canvas.DrawLine(cx + s * 0.2f, cy + s * 0.1f, cx - s * 0.1f, cy + s, iconPaint);
    }
}
