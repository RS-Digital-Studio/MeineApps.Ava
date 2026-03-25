namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;
using System;

/// <summary>
/// Beleuchtungs-System: Ambient-Tönung (ColorFilter) und Punkt-Lichtquellen (radiale Gradienten).
/// Ambient wird als SaveLayer über Charaktere + Foreground gelegt.
/// </summary>
public static class LightingRenderer
{
    private static readonly SKPaint _lightPaint = new() { IsAntialias = true };
    private static readonly SKPaint _ambientPaint = new();

    // Gecachter Ambient-Filter
    private static SKColorFilter? _cachedAmbientFilter;
    private static SKColor _cachedAmbientColor;
    private static float _cachedAmbientIntensity;

    // Shader-Cache fuer Punkt-Lichtquellen (vermeidet CreateRadialGradient pro Licht pro Frame)
    // Key: quantisierte Position (2px) + Radius + Farbe+Alpha
    private struct LightShaderKey : IEquatable<LightShaderKey>
    {
        public int Cx, Cy, Radius;
        public uint ColorWithAlpha;

        public bool Equals(LightShaderKey other)
            => Cx == other.Cx && Cy == other.Cy && Radius == other.Radius && ColorWithAlpha == other.ColorWithAlpha;

        public override int GetHashCode() => HashCode.Combine(Cx, Cy, Radius, ColorWithAlpha);
    }

    private static readonly Dictionary<LightShaderKey, SKShader> _lightShaderCache = new();
    private static int _lightShaderSceneGeneration;

    // Flag ob BeginAmbient ein SaveLayer gemacht hat
    private static bool _ambientActive;

    /// <summary>
    /// Beginnt den Ambient-Light-Layer. Alles zwischen BeginAmbient/EndAmbient wird getönt.
    /// </summary>
    public static void BeginAmbient(SKCanvas canvas, LightDef[] lights)
    {
        _ambientActive = false;

        // Finde erstes Ambient-Licht
        LightDef? ambient = null;
        foreach (var l in lights)
            if (l.Type == LightType.Ambient) { ambient = l; break; }

        if (ambient == null || ambient.Intensity <= 0.001f) return;

        // ColorFilter cachen
        if (_cachedAmbientFilter == null || _cachedAmbientColor != ambient.Color ||
            Math.Abs(_cachedAmbientIntensity - ambient.Intensity) > 0.001f)
        {
            _cachedAmbientFilter?.Dispose();
            var i = ambient.Intensity;
            var r = ambient.Color.Red / 255f;
            var g = ambient.Color.Green / 255f;
            var b = ambient.Color.Blue / 255f;
            // Mische Originalfarbe mit Lichtfarbe
            _cachedAmbientFilter = SKColorFilter.CreateColorMatrix(new float[]
            {
                1f - i + i * r, 0, 0, 0, 0,
                0, 1f - i + i * g, 0, 0, 0,
                0, 0, 1f - i + i * b, 0, 0,
                0, 0, 0, 1, 0
            });
            _cachedAmbientColor = ambient.Color;
            _cachedAmbientIntensity = ambient.Intensity;
        }

        _ambientPaint.ColorFilter = _cachedAmbientFilter;
        canvas.SaveLayer(_ambientPaint);
        _ambientActive = true;
    }

    /// <summary>Beendet den Ambient-Light-Layer.</summary>
    public static void EndAmbient(SKCanvas canvas)
    {
        if (_ambientActive)
        {
            canvas.Restore();
            _ambientActive = false;
        }
    }

    /// <summary>
    /// Zeichnet Punkt-Lichtquellen (additive radiale Gradienten).
    /// Shader werden gecacht (quantisiert auf 2px) und bei Szenen-Wechsel invalidiert.
    /// </summary>
    public static void RenderPointLights(SKCanvas canvas, SKRect bounds, LightDef[] lights, float time)
    {
        foreach (var light in lights)
        {
            if (light.Type != LightType.PointLight) continue;

            var cx = bounds.Left + bounds.Width * light.X;
            var cy = bounds.Top + bounds.Height * light.Y;
            var radius = light.Radius;

            // Flacker-Effekt
            if (light.Flickers)
                radius *= 0.85f + MathF.Sin(time * 5f) * 0.1f + MathF.Sin(time * 13f) * 0.05f;

            var alpha = (byte)(light.Intensity * 255f *
                (light.Flickers ? 0.8f + MathF.Sin(time * 3f) * 0.2f : 1f));

            // Quantisierten Cache-Key berechnen (2px Toleranz)
            var key = new LightShaderKey
            {
                Cx = (int)(cx / 2f) * 2,
                Cy = (int)(cy / 2f) * 2,
                Radius = (int)(radius / 2f) * 2,
                ColorWithAlpha = (uint)((light.Color.Red << 24) | (light.Color.Green << 16) | (light.Color.Blue << 8) | alpha)
            };

            if (!_lightShaderCache.TryGetValue(key, out var shader))
            {
                shader = SKShader.CreateRadialGradient(
                    new SKPoint(cx, cy), radius,
                    new[] { light.Color.WithAlpha(alpha), SKColors.Transparent },
                    SKShaderTileMode.Clamp);
                _lightShaderCache[key] = shader;
            }

            _lightPaint.Shader = shader;
            canvas.DrawRect(bounds, _lightPaint);
            _lightPaint.Shader = null;
        }
    }

    /// <summary>
    /// Invalidiert den Shader-Cache (bei Szenen-Wechsel aufrufen).
    /// </summary>
    public static void InvalidateShaderCache()
    {
        foreach (var shader in _lightShaderCache.Values)
            shader.Dispose();
        _lightShaderCache.Clear();
        _lightShaderSceneGeneration++;
    }

    public static void Cleanup()
    {
        InvalidateShaderCache();
        _cachedAmbientFilter?.Dispose();
        _cachedAmbientFilter = null;
        _lightPaint.Dispose();
        _ambientPaint.Dispose();
    }
}
