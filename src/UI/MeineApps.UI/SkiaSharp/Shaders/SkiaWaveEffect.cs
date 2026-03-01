using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// GPU-beschleunigter Wellen-Effekt via SkiaSharp Shader.
/// Erzeugt animierte Wellen (Wasser, Flüssigkeit, Hintergrund-Distortion).
/// Ideal für Wasserglas-Effekte, Hintergründe, flüssige Übergänge.
/// </summary>
public static class SkiaWaveEffect
{
    // SkSL-Shader: Animierte Wasserwellen-Füllung
    private const string WaterWaveSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 waterColor;
        uniform float4 deepColor;
        uniform float fillLevel;
        uniform float waveAmplitude;
        uniform float waveFrequency;
        uniform float waveSpeed;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Wellen-Offset (zwei überlagerte Sinuswellen für natürlichen Effekt)
            float wave1 = sin(uv.x * waveFrequency + iTime * waveSpeed) * waveAmplitude;
            float wave2 = sin(uv.x * waveFrequency * 1.7 - iTime * waveSpeed * 0.8) * waveAmplitude * 0.5;
            float wave = wave1 + wave2;

            // Füllstand (0=leer, 1=voll, von unten nach oben)
            float waterLine = 1.0 - fillLevel + wave;

            // Unter der Wasserlinie = Wasserfarbe
            if (uv.y > waterLine) {
                // Tiefe-Gradient (dunkler je tiefer)
                float depth = (uv.y - waterLine) / (1.0 - waterLine + 0.001);
                depth = clamp(depth, 0.0, 1.0);
                half4 surfaceCol = half4(waterColor);
                half4 deepCol = half4(deepColor);
                half4 color = mix(surfaceCol, deepCol, depth * 0.6);

                // Caustic-Lichteffekt (Lichtbrechung auf Wasseroberfläche)
                float caustic = 0.5 + 0.5 * sin(uv.x * 20.0 + iTime * 2.0) * sin(uv.y * 15.0 - iTime * 1.5);
                caustic = caustic * (1.0 - depth) * 0.15;
                color.rgb += caustic;

                return color;
            }

            // Über der Wasserlinie = transparent
            return half4(0.0);
        }
    ";

    // SkSL-Shader: Einfache Hintergrundwellen (dekorativ)
    private const string BackgroundWaveSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 color1;
        uniform float4 color2;
        uniform float waves;
        uniform float amplitude;
        uniform float speed;

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Mehrere überlagerte Wellen
            float wave = 0.0;
            for (float i = 1.0; i <= waves; i += 1.0) {
                float freq = i * 2.0 + 1.0;
                float amp = amplitude / i;
                wave += sin(uv.x * freq + iTime * speed * (0.5 + i * 0.3)) * amp;
            }

            // Welle in Y-Richtung
            float waveY = 0.5 + wave;

            // Gradient basierend auf Wellen-Position
            float blend = smoothstep(waveY - 0.1, waveY + 0.1, uv.y);

            half4 c1 = half4(color1);
            half4 c2 = half4(color2);

            return mix(c1, c2, blend);
        }
    ";

    private static SKRuntimeEffect? _waterEffect;
    private static SKRuntimeEffect? _bgWaveEffect;

    /// <summary>
    /// Zeichnet animierte Wasserfüllung mit Wellen und Caustics.
    /// </summary>
    /// <param name="canvas">Zeichenfläche</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="time">Aktuelle Zeit (fortlaufend)</param>
    /// <param name="fillLevel">Füllstand 0.0-1.0 (0=leer, 1=voll)</param>
    /// <param name="waterColor">Wasserfarbe an der Oberfläche</param>
    /// <param name="deepColor">Wasserfarbe in der Tiefe</param>
    /// <param name="waveAmplitude">Wellenhöhe (0.01-0.05 empfohlen)</param>
    public static void DrawWaterFill(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        float fillLevel,
        SKColor? waterColor = null,
        SKColor? deepColor = null,
        float waveAmplitude = 0.02f,
        float waveFrequency = 8f,
        float waveSpeed = 2f)
    {
        _waterEffect ??= SKRuntimeEffect.CreateShader(WaterWaveSksl, out _);
        if (_waterEffect == null) return;

        var water = waterColor ?? new SKColor(0x22, 0xD3, 0xEE, 180); // Cyan
        var deep = deepColor ?? new SKColor(0x06, 0x4E, 0x7B, 200); // Dunkelblau

        var uniforms = new SKRuntimeEffectUniforms(_waterEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["waterColor"] = new[] { water.Red / 255f, water.Green / 255f, water.Blue / 255f, water.Alpha / 255f },
            ["deepColor"] = new[] { deep.Red / 255f, deep.Green / 255f, deep.Blue / 255f, deep.Alpha / 255f },
            ["fillLevel"] = Math.Clamp(fillLevel, 0f, 1f),
            ["waveAmplitude"] = waveAmplitude,
            ["waveFrequency"] = waveFrequency,
            ["waveSpeed"] = waveSpeed
        };

        using var shader = _waterEffect.ToShader(uniforms);
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
    /// Zeichnet dekorative Hintergrundwellen (für Header, Trennlinien).
    /// </summary>
    public static void DrawBackgroundWaves(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        SKColor color1,
        SKColor color2,
        int waveCount = 3,
        float amplitude = 0.08f,
        float speed = 1.5f)
    {
        _bgWaveEffect ??= SKRuntimeEffect.CreateShader(BackgroundWaveSksl, out _);
        if (_bgWaveEffect == null) return;

        var uniforms = new SKRuntimeEffectUniforms(_bgWaveEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["color1"] = new[] { color1.Red / 255f, color1.Green / 255f, color1.Blue / 255f, color1.Alpha / 255f },
            ["color2"] = new[] { color2.Red / 255f, color2.Green / 255f, color2.Blue / 255f, color2.Alpha / 255f },
            ["waves"] = (float)waveCount,
            ["amplitude"] = amplitude,
            ["speed"] = speed
        };

        using var shader = _bgWaveEffect.ToShader(uniforms);
        if (shader == null) return;

        using var paint = new SKPaint
        {
            Shader = shader,
            IsAntialias = true
        };

        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        canvas.DrawRect(0, 0, bounds.Width, bounds.Height, paint);
        canvas.Restore();
    }

    /// <summary>
    /// Kompiliert beide SkSL-Shader vorab (WaterWave + BackgroundWave).
    /// Aufruf während Loading-Screen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void Preload()
    {
        _waterEffect ??= SKRuntimeEffect.CreateShader(WaterWaveSksl, out _);
        _bgWaveEffect ??= SKRuntimeEffect.CreateShader(BackgroundWaveSksl, out _);
    }
}
