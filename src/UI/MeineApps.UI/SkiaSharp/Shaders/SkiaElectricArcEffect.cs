using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// GPU-beschleunigter Elektro-Lichtbogen-Effekt via SkiaSharp Shader.
/// Erzeugt animierte Blitze/Lichtbögen zwischen zwei Punkten mit Glow und Flicker.
/// Ideal für Elektriker-Werkstatt, InnovationLab, Tesla-Coil-Visualisierungen.
/// </summary>
public static class SkiaElectricArcEffect
{
    // SkSL-Shader: Elektrischer Lichtbogen mit Zickzack-Pfad, Verzweigungen und Glow
    private const string ArcSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float2 startPos;
        uniform float2 endPos;
        uniform float4 arcColor;
        uniform float intensity;

        // Hash-basiertes Rauschen (deterministisch, kein Random)
        float hash(float2 p) {
            return fract(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        // 1D-Hash fuer Segment-Offsets
        float hash1(float n) {
            return fract(sin(n * 127.1) * 43758.5453);
        }

        // Berechnet Abstand eines Punktes zu einem Liniensegment (Start-Ende)
        float segmentDist(float2 p, float2 a, float2 b) {
            float2 pa = p - a;
            float2 ba = b - a;
            float t = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
            return length(pa - ba * t);
        }

        // Erzeugt einen Zickzack-Pfad und gibt minimalen Abstand zum Punkt zurueck
        // seed: Startwert fuer Hash-Variation, segments: Anzahl Segmente
        float arcPath(float2 uv, float2 start, float2 end, float seed, int segments, float deviation) {
            float minDist = 999.0;
            float2 dir = end - start;
            float2 perp = normalize(float2(-dir.y, dir.x));

            float2 prev = start;
            for (int i = 1; i < segments; i++) {
                float t = float(i) / float(segments);
                // Position auf der Geraden
                float2 linePos = start + dir * t;
                // Zufaellige Abweichung senkrecht zur Richtung
                float offset = (hash(float2(seed + float(i) * 0.37, iTime * 12.0 + seed)) * 2.0 - 1.0) * deviation;
                float2 current = linePos + perp * offset;
                // Minimaler Abstand zum Segment
                float d = segmentDist(uv, prev, current);
                minDist = min(minDist, d);
                prev = current;
            }
            // Letztes Segment zum Endpunkt
            float d = segmentDist(uv, prev, end);
            minDist = min(minDist, d);

            return minDist;
        }

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Flicker-Effekt: Schnelle Intensitaetsschwankung (~10Hz)
            float flicker = 0.3 + 0.7 * abs(sin(iTime * 31.4159 + hash1(floor(iTime * 10.0)) * 6.28));
            float totalIntensity = intensity * flicker;

            // Seitenverhältnis-Korrektur fuer gleichmaessigen Glow
            float aspect = iResolution.x / iResolution.y;
            float2 uvAspect = float2(uv.x * aspect, uv.y);
            float2 startAspect = float2(startPos.x * aspect, startPos.y);
            float2 endAspect = float2(endPos.x * aspect, endPos.y);

            // Haupt-Lichtbogen (12 Segmente, mittlere Abweichung)
            float arcLen = length(endAspect - startAspect);
            float dev = arcLen * 0.12;
            float mainDist = arcPath(uvAspect, startAspect, endAspect, 1.0, 12, dev);

            // Verzweigungs-Arcs (2-3 kuerzere Seitenarme)
            float branchDist = 999.0;
            for (int b = 0; b < 3; b++) {
                // Abzweigpunkt auf dem Hauptpfad (bei 30%, 55%, 75%)
                float bt = 0.3 + float(b) * 0.225;
                float2 branchStart = mix(startAspect, endAspect, bt);
                // Zufaellige Abweichung fuer Abzweigpunkt
                float bSeed = float(b) * 3.7 + 10.0;
                float bOffset = (hash(float2(bSeed, iTime * 8.0)) * 2.0 - 1.0) * dev;
                float2 perpDir = normalize(float2(-(endAspect.y - startAspect.y), endAspect.x - startAspect.x));
                branchStart += perpDir * bOffset * 0.5;

                // Endpunkt der Verzweigung (seitlich versetzt)
                float bAngle = (hash(float2(bSeed + 5.0, floor(iTime * 6.0))) * 2.0 - 1.0);
                float bLen = arcLen * (0.15 + hash(float2(bSeed + 2.0, floor(iTime * 4.0))) * 0.2);
                float2 branchEnd = branchStart + perpDir * bAngle * bLen;

                float bd = arcPath(uvAspect, branchStart, branchEnd, bSeed + 20.0, 6, dev * 0.7);
                // Verzweigungen etwas schwaecher
                branchDist = min(branchDist, bd * 1.3);
            }

            float combinedDist = min(mainDist, branchDist);

            // Aeusserer Glow (breit, farbig)
            float outerGlow = exp(-combinedDist * combinedDist * 800.0) * 0.6;
            // Innerer Kern (schmal, hell/weiss)
            float innerCore = exp(-combinedDist * combinedDist * 8000.0) * 1.0;

            // Farb-Komposition: Aeusserer Glow in Bogenfarbe, Kern weiss
            float3 glowCol = arcColor.rgb * outerGlow * totalIntensity;
            float3 coreCol = float3(1.0, 1.0, 1.0) * innerCore * totalIntensity;
            float3 finalCol = glowCol + coreCol;

            float alpha = clamp((outerGlow + innerCore) * totalIntensity, 0.0, 1.0);

            return half4(finalCol, alpha * arcColor.a);
        }
    ";

    // SkSL-Shader: Energie-Puls der entlang eines horizontalen Pfades wandert
    private const string EnergyPulseSksl = @"
        uniform float2 iResolution;
        uniform float iTime;
        uniform float4 pulseColor;
        uniform float pulseSpeed;
        uniform float trailLength;

        // Hash-Rauschen fuer Puls-Variation
        float hash(float2 p) {
            return fract(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        half4 main(float2 fragCoord) {
            float2 uv = fragCoord / iResolution;

            // Puls-Position wandert von links nach rechts (0.0 -> 1.0, Schleife)
            float pulsePos = fract(iTime * pulseSpeed);

            // Vertikale Mitte des Pfades
            float centerY = 0.5;
            // Leichte vertikale Wellung fuer organischen Effekt
            float waveY = centerY + sin(uv.x * 12.0 + iTime * 3.0) * 0.02;

            // Abstand zum Pfad (vertikal)
            float pathDist = abs(uv.y - waveY);

            // Pfad-Linie (duenner Streifen)
            float pathLine = smoothstep(0.015, 0.003, pathDist);

            // Puls-Distanz (horizontal zum Puls-Punkt)
            float pulseDist = uv.x - pulsePos;

            // Haupt-Puls (heller Punkt)
            float pulseGlow = exp(-pulseDist * pulseDist * 200.0) * exp(-pathDist * pathDist * 2000.0);

            // Nachleuchten-Schweif (hinter dem Puls)
            float trail = 0.0;
            if (pulseDist < 0.0 && pulseDist > -trailLength) {
                float trailFactor = 1.0 + pulseDist / trailLength;
                trailFactor = clamp(trailFactor, 0.0, 1.0);
                trail = trailFactor * exp(-pathDist * pathDist * 1500.0) * 0.7;
            }
            // Schweif auch ueber Wrap-Around (Puls am Anfang, Schweif am Ende)
            float wrapDist = uv.x - (pulsePos + 1.0);
            if (wrapDist < 0.0 && wrapDist > -trailLength) {
                float trailFactor = 1.0 + wrapDist / trailLength;
                trailFactor = clamp(trailFactor, 0.0, 1.0);
                trail = max(trail, trailFactor * exp(-pathDist * pathDist * 1500.0) * 0.7);
            }

            // Subtile Energie-Partikel entlang des Pfades
            float sparkle = hash(float2(floor(uv.x * 40.0), floor(iTime * 15.0))) * 0.3;
            sparkle *= smoothstep(0.02, 0.005, pathDist);
            sparkle *= smoothstep(trailLength, 0.0, abs(pulseDist));

            // Basis-Pfad-Glow (sehr dezent, zeigt den Kabel-Verlauf)
            float basePath = pathLine * 0.08 * pulseColor.a;

            // Gesamt-Intensitaet
            float totalGlow = pulseGlow + trail + sparkle + basePath;

            // Weisser Kern im Puls-Zentrum
            float coreWhite = exp(-pulseDist * pulseDist * 800.0) * exp(-pathDist * pathDist * 5000.0);
            float3 col = mix(pulseColor.rgb, float3(1.0, 1.0, 1.0), coreWhite * 0.7);

            float alpha = clamp(totalGlow, 0.0, 1.0) * pulseColor.a;

            return half4(col * totalGlow, alpha);
        }
    ";

    private static SKRuntimeEffect? _arcEffect;
    private static SKRuntimeEffect? _pulseEffect;

    /// <summary>
    /// Zeichnet einen elektrischen Lichtbogen zwischen zwei Punkten.
    /// Start/End-Koordinaten sind normalisiert (0-1) relativ zum Bounds-Bereich.
    /// </summary>
    /// <param name="canvas">Zeichenfläche</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="time">Aktuelle Zeit in Sekunden (fortlaufend)</param>
    /// <param name="startX">Startpunkt X (0-1, normalisiert)</param>
    /// <param name="startY">Startpunkt Y (0-1, normalisiert)</param>
    /// <param name="endX">Endpunkt X (0-1, normalisiert)</param>
    /// <param name="endY">Endpunkt Y (0-1, normalisiert)</param>
    /// <param name="arcColor">Farbe des Lichtbogens (Default: Elektroblau)</param>
    /// <param name="intensity">Intensität 0.0-1.0 (Default: 0.8)</param>
    public static void DrawArc(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        float startX = 0.2f,
        float startY = 0.5f,
        float endX = 0.8f,
        float endY = 0.5f,
        SKColor? arcColor = null,
        float intensity = 0.8f)
    {
        _arcEffect ??= SKRuntimeEffect.CreateShader(ArcSksl, out _);
        if (_arcEffect == null) return;

        var color = arcColor ?? new SKColor(0x44, 0xBB, 0xFF, 255); // Elektroblau

        var uniforms = new SKRuntimeEffectUniforms(_arcEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["startPos"] = new[] { startX, startY },
            ["endPos"] = new[] { endX, endY },
            ["arcColor"] = new[] { color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f },
            ["intensity"] = intensity
        };

        using var shader = _arcEffect.ToShader(uniforms);
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
    /// Zeichnet einen leuchtenden Energie-Puls der horizontal entlang eines Pfades wandert.
    /// Ideal fuer Kabel-Energiefluss-Visualisierung.
    /// </summary>
    /// <param name="canvas">Zeichenfläche</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="time">Aktuelle Zeit in Sekunden (fortlaufend)</param>
    /// <param name="pulseColor">Farbe des Pulses (Default: Cyan)</param>
    /// <param name="speed">Geschwindigkeit des Pulses (Default: 1.5)</param>
    /// <param name="trailLength">Länge des Nachleuchten-Schweifs (0.05-0.5, Default: 0.2)</param>
    public static void DrawEnergyPulse(
        SKCanvas canvas,
        SKRect bounds,
        float time,
        SKColor? pulseColor = null,
        float speed = 1.5f,
        float trailLength = 0.2f)
    {
        _pulseEffect ??= SKRuntimeEffect.CreateShader(EnergyPulseSksl, out _);
        if (_pulseEffect == null) return;

        var color = pulseColor ?? new SKColor(0x22, 0xD3, 0xEE, 255); // Cyan

        var uniforms = new SKRuntimeEffectUniforms(_pulseEffect)
        {
            ["iResolution"] = new[] { bounds.Width, bounds.Height },
            ["iTime"] = time,
            ["pulseColor"] = new[] { color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f },
            ["pulseSpeed"] = speed,
            ["trailLength"] = Math.Clamp(trailLength, 0.05f, 0.5f)
        };

        using var shader = _pulseEffect.ToShader(uniforms);
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
    /// Preset: Heller weisser Blitz (hohe Intensität, schnelles Flackern).
    /// Ideal fuer dramatische Blitz-Effekte, Kurzschluss-Visualisierung.
    /// </summary>
    public static void DrawLightning(SKCanvas canvas, SKRect bounds, float time)
    {
        DrawArc(canvas, bounds, time,
            startX: 0.15f, startY: 0.2f,
            endX: 0.85f, endY: 0.8f,
            arcColor: new SKColor(0xEE, 0xEE, 0xFF, 255),
            intensity: 1.0f);
    }

    /// <summary>
    /// Preset: Blau-violetter Tesla-Coil-Bogen (moderate Intensität, mehrere Verzweigungen).
    /// Ideal fuer InnovationLab, Forschungs-Visualisierungen.
    /// </summary>
    public static void DrawTeslaCoil(SKCanvas canvas, SKRect bounds, float time)
    {
        // Haupt-Bogen (Blau-Violett)
        DrawArc(canvas, bounds, time,
            startX: 0.5f, startY: 0.15f,
            endX: 0.2f, endY: 0.85f,
            arcColor: new SKColor(0x88, 0x66, 0xFF, 230),
            intensity: 0.7f);

        // Zweiter Bogen (leicht versetzt, Cyan)
        DrawArc(canvas, bounds, time * 1.3f,
            startX: 0.5f, startY: 0.15f,
            endX: 0.8f, endY: 0.85f,
            arcColor: new SKColor(0x44, 0xBB, 0xFF, 200),
            intensity: 0.5f);
    }

    /// <summary>
    /// Kompiliert beide SkSL-Shader vorab (Arc + EnergyPulse).
    /// Aufruf während Loading-Screen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void Preload()
    {
        _arcEffect ??= SKRuntimeEffect.CreateShader(ArcSksl, out _);
        _pulseEffect ??= SKRuntimeEffect.CreateShader(EnergyPulseSksl, out _);
    }
}
