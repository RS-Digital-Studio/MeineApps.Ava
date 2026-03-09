namespace RebornSaga.Rendering.Backgrounds;

using RebornSaga.Rendering.Backgrounds.Layers;
using SkiaSharp;

/// <summary>
/// Orchestriert das Multi-Layer Hintergrund-Rendering.
/// RenderBack() vor Charakteren, RenderFront() nach Charakteren.
/// BeginLighting/EndLighting klammern die Charakter-Renderung für Ambient-Tönung.
/// </summary>
public static class BackgroundCompositor
{
    private static SceneDef? _currentScene;
    private static string _currentKey = "";

    /// <summary>
    /// Setzt die aktive Szene per backgroundKey aus dem Story-JSON.
    /// </summary>
    public static void SetScene(string backgroundKey)
    {
        if (_currentKey == backgroundKey) return;
        _currentKey = backgroundKey;
        _currentScene = SceneDefinitions.Get(backgroundKey);
    }

    /// <summary>
    /// Zeichnet alles HINTER den Charakteren: Himmel, Mittelgrund-Elemente, Boden, Punkt-Lichter.
    /// </summary>
    public static void RenderBack(SKCanvas canvas, SKRect bounds, float time)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;

        // 1. Himmel-Gradient
        SkyRenderer.Render(canvas, bounds, scene.Sky);

        // 2. Mittelgrund-Silhouetten
        if (scene.Elements.Length > 0)
            ElementRenderer.Render(canvas, bounds, scene.Elements);

        // 3. Boden
        if (scene.Ground != null)
            GroundRenderer.Render(canvas, bounds, scene.Ground);

        // 4. Punkt-Lichter (hinter Charakteren, erzeugt Lichtkreise auf Boden/Wand)
        if (scene.Lights.Length > 0)
            LightingRenderer.RenderPointLights(canvas, bounds, scene.Lights, time);
    }

    /// <summary>
    /// Beginnt Ambient-Licht-Tönung. Zwischen BeginLighting/EndLighting
    /// werden Charaktere gerendert — sie werden mit getönt.
    /// </summary>
    public static void BeginLighting(SKCanvas canvas)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;
        if (scene.Lights.Length > 0)
            LightingRenderer.BeginAmbient(canvas, scene.Lights);
    }

    /// <summary>Beendet Ambient-Licht-Tönung.</summary>
    public static void EndLighting(SKCanvas canvas)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;
        if (scene.Lights.Length > 0)
            LightingRenderer.EndAmbient(canvas);
    }

    /// <summary>
    /// Zeichnet alles ÜBER den Charakteren: Vordergrund, Partikel.
    /// </summary>
    public static void RenderFront(SKCanvas canvas, SKRect bounds, float time)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;

        // 5. Vordergrund (Gras, Nebel, Äste — über Charakteren)
        if (scene.Foreground.Length > 0)
            ForegroundRenderer.Render(canvas, bounds, scene.Foreground, time);

        // 6. Atmosphärische Partikel (ganz oben)
        if (scene.Particles.Length > 0)
            SceneParticleRenderer.Render(canvas, bounds, scene.Particles, time);
    }

    /// <summary>Gibt alle Ressourcen frei.</summary>
    public static void Cleanup()
    {
        SkyRenderer.Cleanup();
        ElementRenderer.Cleanup();
        GroundRenderer.Cleanup();
        LightingRenderer.Cleanup();
        SceneParticleRenderer.Cleanup();
        ForegroundRenderer.Cleanup();
    }
}
