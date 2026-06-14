using System.Diagnostics;

namespace MeineApps.UI.SkiaSharp.Shaders;

/// <summary>
/// Zentraler Shader-Preloader: Kompiliert SkSL-GPU-Shader vorab während des
/// Loading-Screens, um Jank beim ersten Render zu vermeiden (50-200ms pro Shader
/// auf Android-GPU). Thread-safe durch das ??=-Lazy-Pattern in den Effekt-Klassen.
///
/// <para><b>Selektiv preloaden statt pauschal:</b> <see cref="PreloadAll"/> kompiliert
/// alle 12 Shader (600ms-2.4s auf Android) — das ist nur sinnvoll, wenn eine App auch
/// alle Effekt-Familien rendert. Apps, die nur einzelne Effekte nutzen (direkt oder über
/// MeineApps.UI-Controls wie <c>SkiaGradientRing</c>→Shimmer, <c>LinearProgressVisualization</c>
/// →Shimmer, <c>CardGlowRenderer</c>→Glow), sollen nur die jeweiligen <c>PreloadXxx()</c>-
/// Methoden aufrufen. Apps ohne jeden SkSL-Effekt rufen den Preloader gar nicht auf —
/// jeder Effekt kompiliert sich beim ersten echten Gebrauch ohnehin lazy nach.</para>
/// </summary>
public static class ShaderPreloader
{
    /// <summary>Kompiliert Shimmer + Overlay (<see cref="SkiaShimmerEffect"/>).</summary>
    public static void PreloadShimmer() => SkiaShimmerEffect.Preload();

    /// <summary>Kompiliert EdgeGlow + RadialGlow (<see cref="SkiaGlowEffect"/>).</summary>
    public static void PreloadGlow() => SkiaGlowEffect.Preload();

    /// <summary>Kompiliert WaterWave + BackgroundWave (<see cref="SkiaWaveEffect"/>).</summary>
    public static void PreloadWave() => SkiaWaveEffect.Preload();

    /// <summary>Kompiliert Flame + Ember (<see cref="SkiaFireEffect"/>).</summary>
    public static void PreloadFire() => SkiaFireEffect.Preload();

    /// <summary>Kompiliert HeatShimmer + HeatHaze (<see cref="SkiaHeatShimmerEffect"/>).</summary>
    public static void PreloadHeatShimmer() => SkiaHeatShimmerEffect.Preload();

    /// <summary>Kompiliert Arc + EnergyPulse (<see cref="SkiaElectricArcEffect"/>).</summary>
    public static void PreloadElectricArc() => SkiaElectricArcEffect.Preload();

    /// <summary>
    /// Kompiliert alle 12 SkSL-Shader vorab.
    /// Auf Android kann dies 600ms-2.4s dauern - nur für Apps sinnvoll, die alle
    /// Effekt-Familien rendern (z.B. HandwerkerImperium). Apps mit wenigen Effekten
    /// nutzen die selektiven <c>PreloadXxx()</c>-Methoden.
    /// Thread-safe durch ??= Pattern in den einzelnen Effekt-Klassen.
    /// </summary>
    /// <returns>Dauer der Kompilierung in Millisekunden</returns>
    public static long PreloadAll()
    {
        var sw = Stopwatch.StartNew();

        PreloadShimmer();      // Shimmer + Overlay
        PreloadGlow();         // EdgeGlow + RadialGlow
        PreloadWave();         // WaterWave + BackgroundWave
        PreloadFire();         // Flame + Ember
        PreloadHeatShimmer();  // HeatShimmer + HeatHaze
        PreloadElectricArc();  // Arc + EnergyPulse

        sw.Stop();
        Debug.WriteLine($"[ShaderPreloader] 12 SkSL-Shader kompiliert in {sw.ElapsedMilliseconds}ms");
        return sw.ElapsedMilliseconds;
    }
}
