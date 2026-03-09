namespace RebornSaga.Rendering.Characters;

using RebornSaga.Services;
using SkiaSharp;

/// <summary>
/// Fassade für das Charakter-Rendering. Positioniert Charaktere im Dialog-Kontext
/// (links/rechts/center). Rendert ausschließlich AI-generierte Sprites.
/// KEIN Fallback auf prozedurales Rendering — ohne Sprites wird nichts gezeichnet.
/// </summary>
public static class CharacterRenderer
{
    // SpriteCache für AI-generierte Charakter-Sprites
    private static SpriteCache? _spriteCache;

    // Gecachte Paints für inaktive Charakter-Abdunkelung
    private static readonly SKColorFilter _dimFilter = SKColorFilter.CreateColorMatrix(new float[]
    {
        0.6f, 0, 0, 0, 0,
        0, 0.6f, 0, 0, 0,
        0, 0, 0.6f, 0, 0,
        0, 0, 0, 0.8f, 0
    });
    private static readonly SKPaint _dimLayerPaint = new() { ColorFilter = _dimFilter };

    /// <summary>
    /// Initialisiert das Sprite-Rendering mit dem SpriteCache.
    /// Muss nach DI-Container-Aufbau aufgerufen werden.
    /// </summary>
    public static void Initialize(SpriteCache spriteCache)
    {
        _spriteCache = spriteCache;
    }

    /// <summary>
    /// Zeichnet einen Charakter als Halbkörper-Portrait (für Dialoge).
    /// Position: "left", "right" oder "center".
    /// </summary>
    public static void DrawPortrait(SKCanvas canvas, SKRect bounds, CharacterDefinition def,
        Pose pose, Emotion emotion, string position, float time, bool isActive = true)
    {
        if (_spriteCache == null) return;

        // Aktiver Charakter: 15% größer + leicht nach oben versetzt
        var activeBoost = isActive ? 1.15f : 0.95f;
        var activeYOffset = isActive ? -bounds.Height * 0.02f : bounds.Height * 0.01f;

        // Position berechnen
        var portraitW = bounds.Width * 0.35f;
        float cx = position switch
        {
            "left" => bounds.Left + portraitW * 0.6f,
            "right" => bounds.Right - portraitW * 0.6f,
            _ => bounds.MidX
        };

        // Charakter-Position (Kopf im oberen Drittel)
        var cy = bounds.Height * 0.35f + activeYOffset;
        var scale = bounds.Width / 400f * activeBoost;

        // Inaktive Charaktere leicht abdunkeln
        if (!isActive)
            canvas.SaveLayer(_dimLayerPaint);

        SpriteCharacterRenderer.Draw(canvas, def.Id, pose, emotion, cx, cy, scale, time, _spriteCache);

        if (!isActive)
            canvas.Restore();
    }

    /// <summary>
    /// Zeichnet einen Charakter in voller Größe (für Klassenwahl, Status-Screen).
    /// </summary>
    public static void DrawFullBody(SKCanvas canvas, SKRect bounds, CharacterDefinition def,
        Pose pose, Emotion emotion, float time)
    {
        if (_spriteCache == null) return;

        var cx = bounds.MidX;
        var cy = bounds.Top + bounds.Height * 0.45f;
        var scale = bounds.Width / 220f;

        SpriteCharacterRenderer.Draw(canvas, def.Id, pose, emotion, cx, cy, scale, time, _spriteCache);
    }

    /// <summary>
    /// Zeichnet ein kleines Charakter-Icon (für Save-Slots, Inventar).
    /// </summary>
    public static void DrawIcon(SKCanvas canvas, float cx, float cy, float size,
        CharacterDefinition def, Emotion emotion, float time)
    {
        if (_spriteCache == null) return;

        var scale = size / 70f;
        SpriteCharacterRenderer.Draw(canvas, def.Id, Pose.Standing, emotion, cx, cy, scale, time, _spriteCache);
    }

    /// <summary>
    /// Gibt statische Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _dimFilter.Dispose();
        _dimLayerPaint.Dispose();
    }
}
