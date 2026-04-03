using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// GPU-beschleunigter pulsierender Glow-Effekt via SkiaSharp Shader.
/// Erzeugt einen animierten leuchtenden Rand um Elemente.
/// Ideal für aktive Timer, Premium-Buttons, Highlight-Effekte.
/// </summary>
public static class SkiaGlowEffect
{
    // SkSL-Shader: Pulsierender Glow-Ring/Aura
    private const string GlowSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 glowColor;
        uniform float glowRadius;
        uniform float pulseSpeed;
        uniform float pulseMin;
        uniform float pulseMax;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;
            float2 center = float2(0.5, 0.5);

            // Abstand zum nächsten Rand (0 am Rand, 0.5 in der Mitte)
            float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));

            // Pulsierender Glow-Radius
            float pulse = pulseMin + (pulseMax - pulseMin) * (0.5 + 0.5 * sin(iTime * pulseSpeed));

            // Glow-Intensität (stark am Rand, verschwindet nach innen)
            float intensity = smoothstep(pulse, 0.0, edgeDist);

            return half4(glowColor.rgb, glowColor.a * intensity);
        }
    ";

    // SkSL-Shader: Radialer Glow (kreisförmig von der Mitte)
    private const string RadialGlowSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 glowColor;
        uniform float innerRadius;
        uniform float outerRadius;
        uniform float pulseSpeed;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;
            float2 center = float2(0.5, 0.5);

            float dist = distance(uv, center) * 2.0;

            // Pulsierender Radius
            float pulse = 0.5 + 0.5 * sin(iTime * pulseSpeed);
            float inner = innerRadius + pulse * 0.05;
            float outer = outerRadius + pulse * 0.1;

            // Glow zwischen innerem und äußerem Radius
            float intensity = 1.0 - smoothstep(inner, outer, dist);

            // Außerhalb komplett transparent
            if (dist > outer) return half4(0.0);

            // Innerhalb komplett transparent (Donut-Form)
            if (dist < inner) return half4(0.0);

            // Glow-Ring
            float ringDist = abs(dist - (inner + outer) * 0.5) / ((outer - inner) * 0.5);
            float ringIntensity = 1.0 - ringDist * ringDist;

            return half4(glowColor.rgb, glowColor.a * ringIntensity * (0.6 + 0.4 * pulse));
        }
    ";

    private static SKRuntimeEffect? _edgeEffect;
    private static SKRuntimeEffect? _radialEffect;

    // Gecachte Paint-Objekte (vermeiden native SKPaint-Allokation pro Frame)
    private static readonly SKPaint _edgePaint = new() { IsAntialias = true, BlendMode = SKBlendMode.SrcOver };
    private static readonly SKPaint _radialPaint = new() { IsAntialias = true, BlendMode = SKBlendMode.SrcOver };

    // Gecachte Uniform-Arrays (vermeiden Array-Allokation pro Frame)
    private static readonly float[] _edgeResolution = new float[2];
    private static readonly float[] _edgeColor = new float[4];
    private static readonly float[] _radialResolution = new float[2];
    private static readonly float[] _radialColor = new float[4];

    /// <summary>
    /// Zeichnet einen pulsierenden Glow-Rand um einen Bereich.
    /// </summary>
    public static void DrawEdgeGlow(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        SKColor glowColor,
        float glowRadius = 0.08f,
        float pulseSpeed = 2.0f,
        float pulseMin = 0.02f,
        float pulseMax = 0.08f)
    {
        _edgeEffect ??= SKRuntimeEffect.CreateShader(GlowSksl, out _);
        if (_edgeEffect == null) return;

        _edgeResolution[0] = bounds.Width;
        _edgeResolution[1] = bounds.Height;
        _edgeColor[0] = glowColor.Red / 255f;
        _edgeColor[1] = glowColor.Green / 255f;
        _edgeColor[2] = glowColor.Blue / 255f;
        _edgeColor[3] = glowColor.Alpha / 255f;

        var uniforms = new SKRuntimeEffectUniforms(_edgeEffect)
        {
            ["iResolution"] = _edgeResolution,
            ["iTime"] = time,
            ["glowColor"] = _edgeColor,
            ["glowRadius"] = glowRadius,
            ["pulseSpeed"] = pulseSpeed,
            ["pulseMin"] = pulseMin,
            ["pulseMax"] = pulseMax
        };

        _edgePaint.Shader?.Dispose();
        _edgePaint.Shader = _edgeEffect.ToShader(uniforms);
        if (_edgePaint.Shader == null) return;

        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        canvas.DrawRect(0, 0, bounds.Width, bounds.Height, _edgePaint);
        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet einen pulsierenden kreisförmigen Glow-Ring.
    /// </summary>
    public static void DrawRadialGlow(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        SKColor glowColor,
        float innerRadius = 0.6f,
        float outerRadius = 1.0f,
        float pulseSpeed = 2.0f)
    {
        _radialEffect ??= SKRuntimeEffect.CreateShader(RadialGlowSksl, out _);
        if (_radialEffect == null) return;

        _radialResolution[0] = bounds.Width;
        _radialResolution[1] = bounds.Height;
        _radialColor[0] = glowColor.Red / 255f;
        _radialColor[1] = glowColor.Green / 255f;
        _radialColor[2] = glowColor.Blue / 255f;
        _radialColor[3] = glowColor.Alpha / 255f;

        var uniforms = new SKRuntimeEffectUniforms(_radialEffect)
        {
            ["iResolution"] = _radialResolution,
            ["iTime"] = time,
            ["glowColor"] = _radialColor,
            ["innerRadius"] = innerRadius,
            ["outerRadius"] = outerRadius,
            ["pulseSpeed"] = pulseSpeed
        };

        _radialPaint.Shader?.Dispose();
        _radialPaint.Shader = _radialEffect.ToShader(uniforms);
        if (_radialPaint.Shader == null) return;

        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        canvas.DrawRect(0, 0, bounds.Width, bounds.Height, _radialPaint);
        canvas.Restore();
    }

    /// <summary>
    /// Success-Glow-Preset (Grün, langsam pulsierend).
    /// </summary>
    public static void DrawSuccessGlow(SKCanvas canvas, SKRect bounds, float time)
    {
        DrawEdgeGlow(canvas, bounds, time,
            SkiaThemeHelper.Success.WithAlpha(150),
            pulseSpeed: 1.5f, pulseMin: 0.01f, pulseMax: 0.06f);
    }

    /// <summary>
    /// Warning-Glow-Preset (Amber/Orange, schneller pulsierend).
    /// </summary>
    public static void DrawWarningGlow(SKCanvas canvas, SKRect bounds, float time)
    {
        DrawEdgeGlow(canvas, bounds, time,
            SkiaThemeHelper.Warning.WithAlpha(180),
            pulseSpeed: 3.0f, pulseMin: 0.02f, pulseMax: 0.1f);
    }

    /// <summary>
    /// Premium-Glow-Preset (Gold, elegant pulsierend).
    /// </summary>
    public static void DrawPremiumGlow(SKCanvas canvas, SKRect bounds, float time)
    {
        DrawEdgeGlow(canvas, bounds, time,
            new SKColor(0xFF, 0xD7, 0x00, 120),
            pulseSpeed: 1.2f, pulseMin: 0.02f, pulseMax: 0.07f);
    }

    /// <summary>
    /// Kompiliert beide SkSL-Shader vorab (EdgeGlow + RadialGlow).
    /// Aufruf während Loading-Screen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void Preload()
    {
        _edgeEffect ??= SKRuntimeEffect.CreateShader(GlowSksl, out _);
        _radialEffect ??= SKRuntimeEffect.CreateShader(RadialGlowSksl, out _);
    }
}
