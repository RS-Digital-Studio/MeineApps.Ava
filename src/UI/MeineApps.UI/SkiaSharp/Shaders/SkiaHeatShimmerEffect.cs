using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// GPU-beschleunigter Hitze-Flimmer-Effekt via SkiaSharp Shader.
/// Erzeugt atmosphaerische Hitze-Verzerrung und Waermeschleier.
/// Ideal fuer Schmieden, heisse Oberflaechen, Wuesten-Szenen, Ofen-Effekte.
/// </summary>
public static class SkiaHeatShimmerEffect
{
    // SkSL-Shader: Atmosphaerische Hitze-Verzerrung mit UV-Displacement
    // Erzeugt flimmernde Luft ueber heissen Oberflaechen
    private const string HeatShimmerSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float2 heatSource;
        uniform float intensity;
        uniform float distortionStrength;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Abstand zur Hitzequelle (normalisiert)
            float distToSource = distance(uv, heatSource);

            // Intensitaet nimmt mit Entfernung zur Quelle ab
            // Staerker nahe der Quelle, verschwindet weiter weg
            float falloff = 1.0 - smoothstep(0.0, 0.8, distToSource);
            falloff = falloff * falloff;

            // Vertikaler Gradient: Hitze steigt nach oben auf
            // Staerker unterhalb der Quelle, schwaecher darueber
            float verticalFade = smoothstep(0.0, heatSource.y + 0.3, uv.y);

            // Kombinierte Intensitaet
            float localIntensity = falloff * verticalFade * intensity;

            // Mehrschichtige Sinus-Verzerrung fuer natuerliches Flimmern
            float wave1 = sin(uv.y * 40.0 + iTime * 4.5) * 0.5;
            float wave2 = sin(uv.y * 25.0 - iTime * 3.2 + uv.x * 10.0) * 0.3;
            float wave3 = sin(uv.x * 30.0 + iTime * 5.8 + uv.y * 15.0) * 0.2;

            // UV-Displacement (horizontale Verzerrung, staerker nahe Hitzequelle)
            float displaceX = (wave1 + wave2 + wave3) * distortionStrength * localIntensity;
            float displaceY = (wave2 - wave3 * 0.5) * distortionStrength * localIntensity * 0.3;

            // Verzerrte UV-Koordinaten
            float2 distortedUV = uv + float2(displaceX, displaceY);

            // Schnelle Sinus-Modulation fuer intensives Luftflimmern
            float shimmer1 = sin(distortedUV.y * 60.0 + iTime * 7.0) * 0.5 + 0.5;
            float shimmer2 = sin(distortedUV.x * 45.0 - iTime * 5.5) * 0.5 + 0.5;
            float shimmerMix = shimmer1 * shimmer2;

            // Warme Farbtonung (Orange/Gelb-Overlay)
            float3 warmTint = float3(1.0, 0.7, 0.3);

            // Semi-transparentes Overlay - Hauptbereiche fast unsichtbar,
            // nur nahe der Hitzequelle sichtbare Verzerrung
            float alpha = localIntensity * shimmerMix * 0.35;

            // Leichtes Aufhellen nahe der Quelle (Hitzeglut)
            float glowNearSource = falloff * falloff * 0.15 * intensity;
            float3 color = warmTint * (alpha + glowNearSource);

            float totalAlpha = clamp(alpha + glowNearSource, 0.0, 0.6);

            return half4(color, totalAlpha);
        }
    ";

    // SkSL-Shader: Horizontale Hitze-Schlieren fuer Hintergruende
    // Einfachere, breitflaechige Waermewellen die nach oben treiben
    private const string HeatHazeSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 hazeColor;
        uniform float waveCount;
        uniform float speed;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Mehrere horizontale Wellenlinien die nach oben driften
            float haze = 0.0;

            for (float i = 0.0; i < 10.0; i += 1.0) {
                if (i >= waveCount) break;

                // Jede Welle hat eigene Frequenz und Phase
                float freq = 3.0 + i * 1.5;
                float phase = i * 1.7;
                float waveSpeed = speed * (0.6 + i * 0.15);

                // Wellenposition (driftet langsam nach oben)
                float drift = fract(iTime * waveSpeed * 0.03 + phase * 0.3);
                float waveY = drift;

                // Horizontale Wellenlinie
                float wave = sin(uv.x * freq * 6.2832 + iTime * waveSpeed + phase);
                wave = wave * 0.02;

                // Abstand zur Wellenlinie
                float distToWave = abs(uv.y - waveY - wave);

                // Gauss-foermige Sichtbarkeit (schmale, weiche Linien)
                float lineIntensity = smoothstep(0.04, 0.0, distToWave);

                // Staerker am unteren Rand (Hitzequelle)
                float bottomFade = (1.0 - uv.y) * 0.7 + 0.3;
                lineIntensity *= bottomFade;

                haze += lineIntensity;
            }

            // Sinus-Modulation fuer zusaetzliches Flackern
            float flicker = 0.7 + 0.3 * sin(iTime * 3.0 + uv.x * 10.0);
            haze *= flicker;

            // Haze-Intensitaet begrenzen
            haze = clamp(haze, 0.0, 1.0);

            // Warme Farbgebung mit semi-transparentem Overlay
            float alpha = haze * hazeColor.a * 0.5;

            return half4(hazeColor.rgb * alpha, alpha);
        }
    ";

    private static SKRuntimeEffect? _shimmerEffect;
    private static SKRuntimeEffect? _hazeEffect;

    /// <summary>
    /// Zeichnet einen atmosphaerischen Hitze-Flimmer-Effekt.
    /// UV-Displacement erzeugt sichtbare Luft-Verzerrung nahe der Hitzequelle.
    /// </summary>
    /// <param name="canvas">Zeichenflaeche</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="time">Aktuelle Zeit in Sekunden (fortlaufend)</param>
    /// <param name="sourceX">X-Position der Hitzequelle (0.0-1.0, normalisiert)</param>
    /// <param name="sourceY">Y-Position der Hitzequelle (0.0-1.0, normalisiert, 0=oben)</param>
    /// <param name="intensity">Effekt-Intensitaet (0.0-1.0)</param>
    /// <param name="distortion">Staerke der UV-Verzerrung (0.01-0.05 empfohlen)</param>
    public static void DrawHeatShimmer(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        float sourceX = 0.5f,
        float sourceY = 0.8f,
        float intensity = 0.6f,
        float distortion = 0.02f)
    {
        _shimmerEffect ??= SKRuntimeEffect.CreateShader(HeatShimmerSksl, out _);
        if (_shimmerEffect == null) return;

        var uniforms = new SKRuntimeEffectUniforms(_shimmerEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["heatSource"] = new[] { sourceX, sourceY },
            ["intensity"] = Math.Clamp(intensity, 0f, 1f),
            ["distortionStrength"] = distortion
        };

        using var shader = _shimmerEffect.ToShader(uniforms);
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
    /// Zeichnet horizontale Hitze-Schlieren als Hintergrund-Overlay.
    /// Warme, wellenfoermige Linien die langsam nach oben treiben.
    /// </summary>
    /// <param name="canvas">Zeichenflaeche</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="time">Aktuelle Zeit in Sekunden (fortlaufend)</param>
    /// <param name="hazeColor">Farbe des Waermeschleiers (null = warmes Orange)</param>
    /// <param name="waveCount">Anzahl der Wellenlinien (3-10 empfohlen)</param>
    /// <param name="speed">Animations-Geschwindigkeit (0.5-3.0 empfohlen)</param>
    public static void DrawHeatHaze(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        SKColor? hazeColor = null,
        int waveCount = 6,
        float speed = 1.0f)
    {
        _hazeEffect ??= SKRuntimeEffect.CreateShader(HeatHazeSksl, out _);
        if (_hazeEffect == null) return;

        var color = hazeColor ?? new SKColor(0xFF, 0x8C, 0x00, 120); // Warmes Orange

        var uniforms = new SKRuntimeEffectUniforms(_hazeEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["hazeColor"] = new[] { color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f },
            ["waveCount"] = (float)Math.Clamp(waveCount, 1, 10),
            ["speed"] = speed
        };

        using var shader = _hazeEffect.ToShader(uniforms);
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
    /// Schmiede-Hitze-Preset: Intensive Hitze-Verzerrung von der Mitte-unten.
    /// Starke Distortion, hohe Intensitaet, ideal fuer Schmiede-Szenen.
    /// </summary>
    public static void DrawForgeHeat(SKCanvas canvas, SKRect bounds, float time)
    {
        // Starke Hitze vom unteren Zentrum (Schmiede-Feuer)
        DrawHeatShimmer(canvas, bounds, time,
            sourceX: 0.5f,
            sourceY: 0.85f,
            intensity: 0.9f,
            distortion: 0.04f);

        // Zusaetzliche Hitze-Schlieren fuer atmosphaerische Tiefe
        DrawHeatHaze(canvas, bounds, time,
            hazeColor: new SKColor(0xFF, 0x6B, 0x00, 80), // Intensives Orange
            waveCount: 8,
            speed: 1.5f);
    }

    /// <summary>
    /// Sanfte-Hitze-Preset: Subtiles Flimmern fuer warme Umgebungen.
    /// Niedrige Intensitaet, sanfte Wellen, ideal fuer Hintergrund-Atmosphaere.
    /// </summary>
    public static void DrawSoftHeat(SKCanvas canvas, SKRect bounds, float time)
    {
        DrawHeatShimmer(canvas, bounds, time,
            sourceX: 0.5f,
            sourceY: 0.7f,
            intensity: 0.3f,
            distortion: 0.01f);

        DrawHeatHaze(canvas, bounds, time,
            hazeColor: new SKColor(0xFF, 0xA5, 0x00, 50), // Dezentes Bernstein
            waveCount: 4,
            speed: 0.6f);
    }

    /// <summary>
    /// Kompiliert beide SkSL-Shader vorab (HeatShimmer + HeatHaze).
    /// Aufruf während Loading-Screen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void Preload()
    {
        _shimmerEffect ??= SKRuntimeEffect.CreateShader(HeatShimmerSksl, out _);
        _hazeEffect ??= SKRuntimeEffect.CreateShader(HeatHazeSksl, out _);
    }
}
