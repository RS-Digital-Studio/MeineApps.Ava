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

        var uniforms = new SKRuntimeEffectUniforms(_edgeEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["glowColor"] = new[] { glowColor.Red / 255f, glowColor.Green / 255f, glowColor.Blue / 255f, glowColor.Alpha / 255f },
            ["glowRadius"] = glowRadius,
            ["pulseSpeed"] = pulseSpeed,
            ["pulseMin"] = pulseMin,
            ["pulseMax"] = pulseMax
        };

        using var shader = _edgeEffect.ToShader(uniforms);
        if (shader == null) return;

        using var paint = new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
            BlendMode = SKBlendMode.SrcOver
        };

        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        canvas.DrawRect(0, 0, bounds.Width, bounds.Height, paint);
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

        var uniforms = new SKRuntimeEffectUniforms(_radialEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["glowColor"] = new[] { glowColor.Red / 255f, glowColor.Green / 255f, glowColor.Blue / 255f, glowColor.Alpha / 255f },
            ["innerRadius"] = innerRadius,
            ["outerRadius"] = outerRadius,
            ["pulseSpeed"] = pulseSpeed
        };

        using var shader = _radialEffect.ToShader(uniforms);
        if (shader == null) return;

        using var paint = new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
            BlendMode = SKBlendMode.SrcOver
        };

        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        canvas.DrawRect(0, 0, bounds.Width, bounds.Height, paint);
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
