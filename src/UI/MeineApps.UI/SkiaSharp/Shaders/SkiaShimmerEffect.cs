using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// GPU-beschleunigter Shimmer-Effekt via SkiaSharp Shader.
/// Erzeugt einen wandernden Glanzstreifen über beliebige Flächen.
/// Ideal für Premium-Badges, Gold-Elemente, Loading-Platzhalter.
/// </summary>
public static class SkiaShimmerEffect
{
    // SkSL-Shader: Diagonaler Glanzstreifen der über die Fläche wandert
    private const string ShimmerSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 baseColor;
        uniform float4 shimmerColor;
        uniform float stripWidth;
        uniform float speed;
        uniform float angle;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Diagonale Position berechnen (basierend auf Winkel)
            float diag = uv.x * cos(angle) + uv.y * sin(angle);

            // Wandernde Position
            float pos = fract(iTime * speed);

            // Streifen-Intensität (Gauss-ähnlich)
            float dist = abs(diag - pos);
            float intensity = smoothstep(stripWidth, 0.0, dist);

            // Basis-Farbe mit Shimmer mischen
            half4 base = half4(baseColor);
            half4 shimmer = half4(shimmerColor);
            return mix(base, shimmer, intensity * shimmer.a);
        }
    ";

    private static SKRuntimeEffect? _effect;
    private static SKRuntimeEffect? _overlayEffect;

    // Overlay-Shader: Nur der Shimmer-Glanz (transparent wo kein Shimmer)
    private const string ShimmerOverlaySksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 shimmerColor;
        uniform float stripWidth;
        uniform float speed;
        uniform float angle;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;
            float diag = uv.x * cos(angle) + uv.y * sin(angle);
            float pos = fract(iTime * speed);
            float dist = abs(diag - pos);
            float intensity = smoothstep(stripWidth, 0.0, dist);
            return half4(shimmerColor.rgb, shimmerColor.a * intensity);
        }
    ";

    /// <summary>
    /// Erstellt einen Shimmer-Shader-Paint für den gegebenen Bereich.
    /// </summary>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="time">Aktuelle Zeit in Sekunden (fortlaufend)</param>
    /// <param name="baseColor">Grundfarbe der Fläche</param>
    /// <param name="shimmerColor">Farbe des Glanzstreifens (mit Alpha für Intensität)</param>
    /// <param name="stripWidth">Breite des Streifens (0.05-0.2 empfohlen)</param>
    /// <param name="speed">Geschwindigkeit (0.3-1.0 empfohlen)</param>
    /// <param name="angleDegrees">Winkel des Streifens in Grad (45° = diagonal)</param>
    public static SKPaint? CreateShimmerPaint(
        SKRect bounds,
        float time,
        SKColor baseColor,
        SKColor shimmerColor,
        float stripWidth = 0.1f,
        float speed = 0.4f,
        float angleDegrees = 30f)
    {
        _effect ??= SKRuntimeEffect.CreateShader(ShimmerSksl, out _);
        if (_effect == null) return null;

        var uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["baseColor"] = new[] { baseColor.Red / 255f, baseColor.Green / 255f, baseColor.Blue / 255f, baseColor.Alpha / 255f },
            ["shimmerColor"] = new[] { shimmerColor.Red / 255f, shimmerColor.Green / 255f, shimmerColor.Blue / 255f, shimmerColor.Alpha / 255f },
            ["stripWidth"] = stripWidth,
            ["speed"] = speed,
            ["angle"] = angleDegrees * MathF.PI / 180f
        };

        var shader = _effect.ToShader(uniforms);
        if (shader == null) return null;

        return new SKPaint
        {
            Shader = shader,
            IsAntialias = true
        };
    }

    /// <summary>
    /// Erstellt einen Overlay-Shimmer (transparent + Glanz).
    /// Zum Überlagern auf bestehende Elemente.
    /// </summary>
    public static SKPaint? CreateOverlayPaint(
        SKRect bounds,
        float time,
        SKColor shimmerColor,
        float stripWidth = 0.1f,
        float speed = 0.4f,
        float angleDegrees = 30f)
    {
        _overlayEffect ??= SKRuntimeEffect.CreateShader(ShimmerOverlaySksl, out _);
        if (_overlayEffect == null) return null;

        var uniforms = new SKRuntimeEffectUniforms(_overlayEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["shimmerColor"] = new[] { shimmerColor.Red / 255f, shimmerColor.Green / 255f, shimmerColor.Blue / 255f, shimmerColor.Alpha / 255f },
            ["stripWidth"] = stripWidth,
            ["speed"] = speed,
            ["angle"] = angleDegrees * MathF.PI / 180f
        };

        var shader = _overlayEffect.ToShader(uniforms);
        if (shader == null) return null;

        return new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
            BlendMode = SKBlendMode.SrcOver
        };
    }

    /// <summary>
    /// Zeichnet einen Shimmer-Effekt als Overlay auf einem bestehenden Canvas-Bereich.
    /// Einfache Hilfsmethode für häufigen Gebrauch.
    /// </summary>
    public static void DrawShimmerOverlay(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        SKColor? shimmerColor = null,
        float stripWidth = 0.1f,
        float speed = 0.4f)
    {
        var color = shimmerColor ?? SKColors.White.WithAlpha(80);
        using var paint = CreateOverlayPaint(bounds, time, color, stripWidth, speed);
        if (paint != null)
        {
            canvas.DrawRect(bounds, paint);
        }
    }

    /// <summary>
    /// Gold-Shimmer-Preset für Premium-Elemente.
    /// </summary>
    public static void DrawGoldShimmer(SKCanvas canvas, SKRect bounds, float time)
    {
        DrawShimmerOverlay(canvas, bounds, time,
            shimmerColor: new SKColor(0xFF, 0xE0, 0x80, 100),
            stripWidth: 0.12f,
            speed: 0.35f);
    }

    /// <summary>
    /// Premium-Shimmer-Preset (Blau/Violett).
    /// </summary>
    public static void DrawPremiumShimmer(SKCanvas canvas, SKRect bounds, float time)
    {
        DrawShimmerOverlay(canvas, bounds, time,
            shimmerColor: new SKColor(0xA7, 0x8B, 0xFA, 90),
            stripWidth: 0.15f,
            speed: 0.3f);
    }

    /// <summary>
    /// Kompiliert beide SkSL-Shader vorab (Shimmer + Overlay).
    /// Aufruf während Loading-Screen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void Preload()
    {
        _effect ??= SKRuntimeEffect.CreateShader(ShimmerSksl, out _);
        _overlayEffect ??= SKRuntimeEffect.CreateShader(ShimmerOverlaySksl, out _);
    }
}
