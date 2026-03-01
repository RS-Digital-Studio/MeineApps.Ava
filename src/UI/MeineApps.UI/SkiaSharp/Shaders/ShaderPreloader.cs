using System.Diagnostics;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// Zentraler Shader-Preloader: Kompiliert alle 12 SkSL-GPU-Shader vorab.
/// Sollte während des Loading-Screens aufgerufen werden um Jank beim
/// ersten Render zu vermeiden (50-200ms pro Shader auf Android-GPU).
/// </summary>
public static class ShaderPreloader
{
    /// <summary>
    /// Kompiliert alle 12 SkSL-Shader vorab.
    /// Auf Android kann dies 600ms-2.4s dauern - ideal während Loading-Screen.
    /// Thread-safe durch ??= Pattern in den einzelnen Effekt-Klassen.
    /// </summary>
    /// <returns>Dauer der Kompilierung in Millisekunden</returns>
    public static long PreloadAll()
    {
        var sw = Stopwatch.StartNew();

        SkiaShimmerEffect.Preload();    // Shimmer + Overlay
        SkiaGlowEffect.Preload();       // EdgeGlow + RadialGlow
        SkiaWaveEffect.Preload();        // WaterWave + BackgroundWave
        SkiaFireEffect.Preload();        // Flame + Ember
        SkiaHeatShimmerEffect.Preload(); // HeatShimmer + HeatHaze
        SkiaElectricArcEffect.Preload(); // Arc + EnergyPulse

        sw.Stop();
        Debug.WriteLine($"[ShaderPreloader] 12 SkSL-Shader kompiliert in {sw.ElapsedMilliseconds}ms");
        return sw.ElapsedMilliseconds;
    }
}
