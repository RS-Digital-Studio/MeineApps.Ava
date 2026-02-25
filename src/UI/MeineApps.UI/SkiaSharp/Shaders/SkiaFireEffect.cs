using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// GPU-beschleunigter Feuer-/Flammen-Effekt via SkiaSharp Shader.
/// Erzeugt realistische Flammen mit mehrschichtigem Rauschen und Farbverlauf.
/// Ideal für Schmiedefeuer, Öfen, Lagerfeuer in HandwerkerImperium.
/// </summary>
public static class SkiaFireEffect
{
    // SkSL-Shader: Mehrschichtiges Feuer mit Hash-basiertem Rauschen
    // Farbverlauf: Schwarz -> Rot -> Orange -> Gelb -> Weiß (unten nach oben)
    // 3+ überlagerte Sinuswellen für natürliche Flammenbewegung
    private const string FlameSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float intensity;
        uniform float4 baseColor;
        uniform float4 tipColor;

        // Hash-Funktion für pseudo-zufällige Werte
        float hash(float2 p) {
            return fract(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        // Wert-Rauschen basierend auf Hash (bilinear interpoliert)
        float noise(float2 p) {
            float2 i = floor(p);
            float2 f = fract(p);
            // Kubische Interpolation für weichere Übergänge
            float2 u = f * f * (3.0 - 2.0 * f);

            float a = hash(i);
            float b = hash(i + float2(1.0, 0.0));
            float c = hash(i + float2(0.0, 1.0));
            float d = hash(i + float2(1.0, 1.0));

            return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
        }

        // Fraktales Rauschen (3 Oktaven für Detail)
        float fbm(float2 p) {
            float value = 0.0;
            float amplitude = 0.5;
            float frequency = 1.0;
            // 3 Oktaven für natürliches Flammen-Detail
            for (float i = 0.0; i < 3.0; i += 1.0) {
                value += amplitude * noise(p * frequency);
                frequency *= 2.0;
                amplitude *= 0.5;
            }
            return value;
        }

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Y invertieren: Flammen steigen von unten (y=1) nach oben (y=0)
            float y = 1.0 - uv.y;

            // Horizontale Zentrierung (Flamme in der Mitte breiter)
            float xCenter = abs(uv.x - 0.5) * 2.0;

            // === Flammenform durch überlagerte Sinuswellen ===
            // Welle 1: Langsame Hauptbewegung
            float wave1 = sin(uv.x * 6.0 + iTime * 2.5) * 0.04;
            // Welle 2: Schnellere sekundäre Verwirbelung
            float wave2 = sin(uv.x * 12.0 - iTime * 3.8) * 0.025;
            // Welle 3: Hochfrequente Flacker-Details
            float wave3 = sin(uv.x * 20.0 + iTime * 5.2) * 0.015;
            // Welle 4: Asymmetrische Verwirbelung
            float wave4 = cos(uv.x * 8.5 + iTime * 1.7) * 0.02;

            float waveOffset = (wave1 + wave2 + wave3 + wave4) * intensity;

            // === Rausch-basierte Flammenstruktur ===
            // Rauschen bewegt sich nach oben (simuliert aufsteigende Hitze)
            float2 noiseCoord = float2(uv.x * 4.0, y * 3.0 - iTime * 1.8);
            float n = fbm(noiseCoord);

            // Zweite Rausch-Schicht für Detail
            float2 noiseCoord2 = float2(uv.x * 8.0 + 3.7, y * 5.0 - iTime * 2.5);
            float n2 = fbm(noiseCoord2);

            // Rauschen kombinieren
            float combinedNoise = n * 0.6 + n2 * 0.4;

            // === Flammenform berechnen ===
            // Basis-Höhe: volle Breite unten, schmaler nach oben
            float flameWidth = smoothstep(0.0, 0.3, y) * (1.0 - smoothstep(0.5, 1.0, y));
            float taper = 1.0 - xCenter * (0.5 + y * 1.5);
            taper = clamp(taper, 0.0, 1.0);

            // Flammen-Intensität: Kombination aus Form und Rauschen
            float flame = taper * (combinedNoise + waveOffset);
            flame *= intensity;

            // Oberer Rand: unregelmäßige Flammenspitzen durch Rauschen
            float tipNoise = noise(float2(uv.x * 10.0, iTime * 2.0));
            float flameTip = smoothstep(0.6 + tipNoise * 0.3, 0.2, y);
            flame *= flameTip;

            // Schwelle für Sichtbarkeit
            flame = smoothstep(0.1, 0.6, flame);

            // === Farbverlauf: Schwarz -> Rot -> Orange -> Gelb -> Weiß ===
            float4 black = float4(0.0, 0.0, 0.0, 0.0);
            float4 darkRed = float4(0.6, 0.05, 0.0, 1.0);
            float4 red = float4(baseColor.rgb, 1.0);
            float4 orange = float4(1.0, 0.55, 0.0, 1.0);
            float4 yellow = float4(1.0, 0.85, 0.15, 1.0);
            float4 white = float4(tipColor.rgb, 1.0);

            // Farbe basierend auf Flammen-Intensität mischen
            float4 fireColor = black;
            if (flame > 0.0) {
                fireColor = mix(black, darkRed, smoothstep(0.0, 0.15, flame));
                fireColor = mix(fireColor, red, smoothstep(0.15, 0.35, flame));
                fireColor = mix(fireColor, orange, smoothstep(0.35, 0.55, flame));
                fireColor = mix(fireColor, yellow, smoothstep(0.55, 0.75, flame));
                fireColor = mix(fireColor, white, smoothstep(0.75, 0.95, flame));
            }

            // === Basis-Glut am Boden ===
            float emberGlow = smoothstep(0.3, 0.0, y) * (0.6 + 0.4 * sin(iTime * 3.0));
            emberGlow *= (1.0 - xCenter * 0.8);
            emberGlow *= intensity;
            float4 emberColor = float4(baseColor.rgb * 0.8, 1.0);
            fireColor = mix(fireColor, emberColor, emberGlow * 0.5 * (1.0 - flame));

            // Alpha aus Flammen-Intensität und Glut
            float alpha = max(flame, emberGlow * 0.4);
            alpha = clamp(alpha, 0.0, 1.0);

            return half4(fireColor.rgb, alpha);
        }
    ";

    // SkSL-Shader: Glühende Kohlen/Glut für die Schmiedebasis
    // Pulsierendes Rot/Orange-Leuchten mit Riss-Muster
    private const string EmberSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 glowColor;
        uniform float intensity;

        // Hash für Riss-Muster
        float hash(float2 p) {
            return fract(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        // Zellulares Rauschen (Voronoi-ähnlich) für Riss-Textur
        float cellNoise(float2 p) {
            float2 i = floor(p);
            float2 f = fract(p);
            float minDist = 1.0;

            // 3x3 Nachbarschaft durchsuchen
            for (float y = -1.0; y <= 1.0; y += 1.0) {
                for (float x = -1.0; x <= 1.0; x += 1.0) {
                    float2 neighbor = float2(x, y);
                    float2 point = float2(hash(i + neighbor),
                                         hash(i + neighbor + float2(31.7, 17.3)));
                    float2 diff = neighbor + point - f;
                    float dist = length(diff);
                    minDist = min(minDist, dist);
                }
            }
            return minDist;
        }

        // Wert-Rauschen
        float noise(float2 p) {
            float2 i = floor(p);
            float2 f = fract(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = hash(i);
            float b = hash(i + float2(1.0, 0.0));
            float c = hash(i + float2(0.0, 1.0));
            float d = hash(i + float2(1.0, 1.0));
            return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
        }

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // === Riss-/Kohle-Muster via zellularem Rauschen ===
            float cells = cellNoise(uv * 6.0);

            // Risse leuchten stärker (kleine Distanz = Riss)
            float crackGlow = smoothstep(0.3, 0.05, cells);

            // === Pulsierendes Grundleuchten ===
            // Mehrere überlagerte Puls-Frequenzen für organisches Glühen
            float pulse1 = 0.5 + 0.5 * sin(iTime * 1.2);
            float pulse2 = 0.5 + 0.5 * sin(iTime * 2.7 + 1.5);
            float pulse3 = 0.5 + 0.5 * sin(iTime * 0.8 + 3.0);
            float pulse = pulse1 * 0.5 + pulse2 * 0.3 + pulse3 * 0.2;

            // Lokale Puls-Variation (verschiedene Stellen pulsieren unterschiedlich)
            float localPhase = noise(uv * 3.0 + float2(iTime * 0.3, 0.0));
            float localPulse = 0.5 + 0.5 * sin(iTime * 1.5 + localPhase * 6.28);
            pulse = mix(pulse, localPulse, 0.4);

            // === Gesamte Glut-Intensität ===
            float glow = (0.3 + 0.7 * crackGlow) * pulse * intensity;

            // Hitze-Spots (hellere Stellen, die wandern)
            float hotspot = noise(uv * 4.0 + float2(iTime * 0.2, iTime * 0.15));
            hotspot = smoothstep(0.5, 0.8, hotspot) * 0.4;
            glow += hotspot * intensity;

            glow = clamp(glow, 0.0, 1.0);

            // === Farbverlauf: Dunkelrot -> Rot -> Orange -> Gelborange ===
            float4 darkEmber = float4(glowColor.rgb * 0.2, 1.0);
            float4 midEmber = float4(glowColor.rgb, 1.0);
            float4 hotEmber = float4(
                min(glowColor.r + 0.3, 1.0),
                min(glowColor.g + 0.2, 1.0),
                glowColor.b * 0.5,
                1.0
            );

            float4 color = mix(darkEmber, midEmber, smoothstep(0.0, 0.5, glow));
            color = mix(color, hotEmber, smoothstep(0.5, 0.9, glow));

            // Alpha: Grundleuchten auch bei niedriger Intensität sichtbar
            float alpha = smoothstep(0.0, 0.15, glow) * glowColor.a;

            return half4(color.rgb, alpha);
        }
    ";

    private static SKRuntimeEffect? _flameEffect;
    private static SKRuntimeEffect? _emberEffect;

    /// <summary>
    /// Zeichnet realistische Flammen mit mehrschichtigem Rauschen.
    /// Farbverlauf von Schwarz über Rot/Orange/Gelb bis zur Spitzenfarbe.
    /// </summary>
    /// <param name="canvas">Zeichenfläche</param>
    /// <param name="bounds">Zeichenbereich (Flammen steigen von unten auf)</param>
    /// <param name="time">Aktuelle Zeit in Sekunden (fortlaufend)</param>
    /// <param name="intensity">Flammen-Intensität 0.0-1.0 (Default: 0.8)</param>
    /// <param name="baseColor">Basis-Flammenfarbe, typisch Rot (Default: #FF3300)</param>
    /// <param name="tipColor">Flammenspitzen-Farbe, typisch Weiß/Gelb (Default: #FFFFEE)</param>
    public static void DrawFlames(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        float intensity = 0.8f,
        SKColor? baseColor = null,
        SKColor? tipColor = null)
    {
        _flameEffect ??= SKRuntimeEffect.CreateShader(FlameSksl, out _);
        if (_flameEffect == null) return;

        var bColor = baseColor ?? new SKColor(0xFF, 0x33, 0x00); // Kräftiges Rot-Orange
        var tColor = tipColor ?? new SKColor(0xFF, 0xFF, 0xEE);  // Warmes Weiß

        var uniforms = new SKRuntimeEffectUniforms(_flameEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["intensity"] = Math.Clamp(intensity, 0f, 1f),
            ["baseColor"] = new[] { bColor.Red / 255f, bColor.Green / 255f, bColor.Blue / 255f, bColor.Alpha / 255f },
            ["tipColor"] = new[] { tColor.Red / 255f, tColor.Green / 255f, tColor.Blue / 255f, tColor.Alpha / 255f }
        };

        using var shader = _flameEffect.ToShader(uniforms);
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
    /// Zeichnet glühende Kohlen/Glut mit pulsierendem Leuchten und Riss-Muster.
    /// Ideal für die Schmiedebasis unter den Flammen.
    /// </summary>
    /// <param name="canvas">Zeichenfläche</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="time">Aktuelle Zeit in Sekunden (fortlaufend)</param>
    /// <param name="glowColor">Glut-Farbe (Default: Dunkelrot-Orange #CC3300)</param>
    /// <param name="intensity">Glut-Intensität 0.0-1.0 (Default: 0.7)</param>
    public static void DrawEmbers(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        SKColor? glowColor = null,
        float intensity = 0.7f)
    {
        _emberEffect ??= SKRuntimeEffect.CreateShader(EmberSksl, out _);
        if (_emberEffect == null) return;

        var color = glowColor ?? new SKColor(0xCC, 0x33, 0x00, 220); // Dunkelrot-Orange

        var uniforms = new SKRuntimeEffectUniforms(_emberEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["glowColor"] = new[] { color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f },
            ["intensity"] = Math.Clamp(intensity, 0f, 1f)
        };

        using var shader = _emberEffect.ToShader(uniforms);
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
    /// Schmiede-Feuer-Preset: Intensives Feuer mit gelb-weißen Spitzen.
    /// Für die Meisterschmied-Esse in HandwerkerImperium.
    /// </summary>
    public static void DrawForgeFlame(SKCanvas canvas, SKRect bounds, float time)
    {
        // Glut-Basis (unteres Drittel)
        var emberBounds = new SKRect(
            bounds.Left,
            bounds.Top + bounds.Height * 0.7f,
            bounds.Right,
            bounds.Bottom);
        DrawEmbers(canvas, emberBounds, time,
            glowColor: new SKColor(0xDD, 0x44, 0x00, 230),
            intensity: 0.85f);

        // Intensive Flammen mit heißen weiß-gelben Spitzen
        DrawFlames(canvas, bounds, time,
            intensity: 0.95f,
            baseColor: new SKColor(0xFF, 0x44, 0x00),  // Kräftiges Orange-Rot
            tipColor: new SKColor(0xFF, 0xFF, 0xDD));   // Heißes Weiß-Gelb
    }

    /// <summary>
    /// Lagerfeuer-Preset: Ruhigeres Feuer mit orangen Spitzen.
    /// Für den Schreiner-Ofen oder dekorative Feuer.
    /// </summary>
    public static void DrawCampfire(SKCanvas canvas, SKRect bounds, float time)
    {
        // Dezente Glut-Basis (unteres Viertel)
        var emberBounds = new SKRect(
            bounds.Left,
            bounds.Top + bounds.Height * 0.75f,
            bounds.Right,
            bounds.Bottom);
        DrawEmbers(canvas, emberBounds, time,
            glowColor: new SKColor(0xAA, 0x22, 0x00, 200),
            intensity: 0.6f);

        // Ruhigere Flammen mit warmen Orange-Spitzen
        DrawFlames(canvas, bounds, time,
            intensity: 0.6f,
            baseColor: new SKColor(0xCC, 0x22, 0x00),  // Dunkles Rot
            tipColor: new SKColor(0xFF, 0xCC, 0x44));   // Warmes Orange-Gelb
    }
}
