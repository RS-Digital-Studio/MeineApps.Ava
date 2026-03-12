namespace RebornSaga.Rendering.Backgrounds;

using RebornSaga.Rendering.Backgrounds.Layers;
using RebornSaga.Services;
using SkiaSharp;

/// <summary>
/// Orchestriert das Multi-Layer Hintergrund-Rendering.
/// Hybrid: AI-generierter Hintergrund als Basis + prozedurale SkiaSharp-Overlays (Partikel, Licht).
/// RenderBack() vor Charakteren, RenderFront() nach Charakteren.
/// BeginLighting/EndLighting klammern die Charakter-Renderung für Ambient-Tönung.
/// </summary>
public static class BackgroundCompositor
{
    private static SceneDef? _currentScene;
    private static string _currentKey = "";
    private static SpriteCache? _spriteCache;

    // Gepoolter Paint für AI-Hintergrund-Rendering
    private static readonly SKPaint _bgBitmapPaint = new() { IsAntialias = true };

    /// <summary>
    /// Initialisiert den SpriteCache für AI-generierte Hintergründe.
    /// Muss nach DI-Container-Aufbau aufgerufen werden.
    /// </summary>
    public static void SetSpriteCache(SpriteCache spriteCache)
    {
        _spriteCache = spriteCache;
    }

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
    /// Zeichnet alles HINTER den Charakteren.
    /// Hybrid-Rendering: AI-Hintergrund als Basis, darüber prozedurale Licht-Effekte.
    /// Ohne AI-Assets wird auf prozedurale Hintergründe zurückgegriffen.
    /// </summary>
    public static void RenderBack(SKCanvas canvas, SKRect bounds, float time)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;

        // AI-generierter Hintergrund als Basis (wenn verfügbar)
        var aiBg = _spriteCache?.GetBackground(_currentKey);
        if (aiBg != null)
        {
            // AI-Bild als Vollbild-Basis (Cover-Fit: Seitenverhältnis beibehalten)
            var srcRect = new SKRect(0, 0, aiBg.Width, aiBg.Height);
            var srcAspect = (float)aiBg.Width / aiBg.Height;
            var dstAspect = bounds.Width / bounds.Height;
            SKRect drawRect;

            if (srcAspect > dstAspect)
            {
                // Bild ist breiter als Ziel: oben/unten croppen
                var cropWidth = aiBg.Height * dstAspect;
                var cropX = (aiBg.Width - cropWidth) / 2f;
                srcRect = new SKRect(cropX, 0, cropX + cropWidth, aiBg.Height);
                drawRect = bounds;
            }
            else
            {
                // Bild ist höher als Ziel: links/rechts croppen
                var cropHeight = aiBg.Width / dstAspect;
                var cropY = (aiBg.Height - cropHeight) / 2f;
                srcRect = new SKRect(0, cropY, aiBg.Width, cropY + cropHeight);
                drawRect = bounds;
            }

            canvas.DrawBitmap(aiBg, srcRect, drawRect, _bgBitmapPaint);

            // Nur Punkt-Lichter darüber (atmosphärische Effekte bleiben)
            if (scene.Lights.Length > 0)
                LightingRenderer.RenderPointLights(canvas, bounds, scene.Lights, time);
        }
        else
        {
            // Prozeduraler Fallback (bis AI-Assets heruntergeladen sind)
            // 1. Himmel-Gradient
            SkyRenderer.Render(canvas, bounds, scene.Sky);

            // 2. Mittelgrund-Silhouetten
            if (scene.Elements.Length > 0)
                ElementRenderer.Render(canvas, bounds, scene.Elements);

            // 3. Boden
            if (scene.Ground != null)
                GroundRenderer.Render(canvas, bounds, scene.Ground);

            // 4. Punkt-Lichter
            if (scene.Lights.Length > 0)
                LightingRenderer.RenderPointLights(canvas, bounds, scene.Lights, time);
        }
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
