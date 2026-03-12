namespace RebornSaga.Rendering.Characters;

using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Fassade für das Charakter-Rendering. Positioniert Charaktere im Dialog-Kontext
/// (links/rechts/center). Rendert ausschließlich AI-generierte Sprites.
/// KEIN Fallback auf prozedurales Rendering — ohne Sprites wird nichts gezeichnet.
/// Weiche Kanten per gecachtem Gradient-Bitmap (kein SaveLayer per Frame).
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

    /// <summary>Referenz-Breite der AI-generierten Sprites (1248px nach Pipeline-Resize).</summary>
    private const float SpriteReferenceWidth = 1248f;

    /// <summary>Referenz-Höhe der AI-generierten Sprites (1824px nach Pipeline-Resize).</summary>
    private const float SpriteReferenceHeight = 1824f;

    /// <summary>
    /// Referenz-Content-Höhe für Skalierung (Durchschnitt der stehenden Posen ≈ 1700px).
    /// FESTER Wert statt per-Sprite, damit die Skalierung nicht zwischen Emotionen springt.
    /// Content-Analyse zeigt: Stehende Posen haben 1495-1752px Content-Höhe.
    /// </summary>
    private const float ReferenceContentHeight = 1700f;

    /// <summary>
    /// Referenz-Content-Breite für Skalierung (Durchschnitt ≈ 750px).
    /// FESTER Wert damit die Breite nicht zwischen Posen springt.
    /// </summary>
    private const float ReferenceContentWidth = 750f;

    // Gecachte Vignette-Maske (Alpha-Bitmap, einmal erstellt, wiederverwendet)
    private static SKBitmap? _vignetteMask;
    private static int _vignetteMaskW, _vignetteMaskH;
    private static readonly SKPaint _maskPaint = new() { BlendMode = SKBlendMode.DstIn, IsAntialias = true };

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
    /// Content-aware: Skaliert und positioniert basierend auf dem sichtbaren Inhalt
    /// des Sprites (nicht der vollen 1248x1824 Leinwand). Dadurch bleiben Charaktere
    /// unabhängig vom Framing des Sprites konsistent groß und zentriert.
    /// Position: "left", "right" oder "center".
    /// </summary>
    public static void DrawPortrait(SKCanvas canvas, SKRect bounds, CharacterDefinition def,
        Pose pose, Emotion emotion, string position, float time, bool isActive = true)
    {
        if (_spriteCache == null) return;

        var activeBoost = isActive ? 1.10f : 0.95f;
        var activeYOffset = isActive ? -bounds.Height * 0.02f : bounds.Height * 0.01f;

        // FESTE Skalierung basierend auf Referenz-Content-Höhe (nicht per-Sprite!)
        // Verhindert Größen-Springen zwischen Emotionen/Overlays.
        // Content soll 65% der Bildschirmhöhe einnehmen (vorher 50% → zu klein)
        var scale = bounds.Height * 0.65f / ReferenceContentHeight * activeBoost;

        // Breitenbegrenzung basierend auf fester Referenz-Content-Breite
        var maxScaleW = bounds.Width * 0.55f / ReferenceContentWidth;
        if (scale > maxScaleW) scale = maxScaleW;

        // Gesamt-Sprite darf nicht über Bildschirmgrenzen hinausragen
        var maxSpriteScale = bounds.Width * 0.85f / SpriteReferenceWidth;
        if (scale > maxSpriteScale) scale = maxSpriteScale;

        // Horizontale Zielposition
        var portraitW = bounds.Width * 0.40f;
        float targetCx = position switch
        {
            "left" => bounds.Left + portraitW * 0.55f,
            "right" => bounds.Right - portraitW * 0.55f,
            _ => bounds.MidX
        };

        // Vertikale Zielposition: Content-Unterkante knapp über der Dialogbox
        var targetContentBottom = bounds.Height * 0.70f + activeYOffset;

        // Content-Bounds NUR für Positionierung (nicht Skalierung!)
        // Damit der sichtbare Charakter-Content zentriert und korrekt platziert wird
        var cb = _spriteCache.GetSpriteContentBounds(def.Id, pose, emotion);
        var contentMidX = (cb.Left + cb.Right) / 2f;
        var drawCx = targetCx - (contentMidX - SpriteReferenceWidth / 2f) * scale;
        var drawCy = targetContentBottom - ((float)cb.Bottom - SpriteReferenceHeight / 2f) * scale;

        // Inaktive Charaktere leicht abdunkeln
        if (!isActive)
            canvas.SaveLayer(_dimLayerPaint);

        // Sprite mit Vignette-Maske zeichnen (weiche Kanten, kein Flicker)
        DrawSpriteWithVignette(canvas, def.Id, pose, emotion, drawCx, drawCy, scale, time, bounds);

        if (!isActive)
            canvas.Restore();
    }

    /// <summary>
    /// Zeichnet einen Charakter in voller Größe (für Klassenwahl, Status-Screen).
    /// Content-aware: Skaliert basierend auf sichtbarem Inhalt.
    /// </summary>
    public static void DrawFullBody(SKCanvas canvas, SKRect bounds, CharacterDefinition def,
        Pose pose, Emotion emotion, float time)
    {
        if (_spriteCache == null) return;

        // FESTE Skalierung basierend auf Referenz-Content-Höhe
        // Content soll 75% der Bildschirmhöhe einnehmen
        var scale = bounds.Height * 0.75f / ReferenceContentHeight;

        // Breitenbegrenzung basierend auf fester Referenz-Content-Breite
        var maxScaleW = bounds.Width * 0.65f / ReferenceContentWidth;
        if (scale > maxScaleW) scale = maxScaleW;

        // Content-Bounds NUR für Positionierung
        var cb = _spriteCache.GetSpriteContentBounds(def.Id, pose, emotion);
        var contentMidX = (cb.Left + cb.Right) / 2f;
        var contentMidY = (cb.Top + cb.Bottom) / 2f;
        var drawCx = bounds.MidX - (contentMidX - SpriteReferenceWidth / 2f) * scale;
        var drawCy = bounds.Top + bounds.Height * 0.50f - (contentMidY - SpriteReferenceHeight / 2f) * scale;

        // Sprite mit Vignette-Maske
        DrawSpriteWithVignette(canvas, def.Id, pose, emotion, drawCx, drawCy, scale, time, bounds);
    }

    /// <summary>
    /// Zeichnet ein kleines Charakter-Icon (für Save-Slots, Inventar).
    /// Icons sind klein genug — keine Rand-Maske nötig.
    /// </summary>
    public static void DrawIcon(SKCanvas canvas, float cx, float cy, float size,
        CharacterDefinition def, Emotion emotion, float time)
    {
        if (_spriteCache == null) return;

        var scale = size / SpriteReferenceWidth;
        SpriteCharacterRenderer.Draw(canvas, def.Id, Pose.Standing, emotion, cx, cy, scale, time, _spriteCache);
    }

    /// <summary>
    /// Zeichnet ein Sprite mit gecachter Vignette-Maske für weiche Kanten.
    /// Die Maske wird einmal erstellt und pro Frame wiederverwendet (kein Flicker).
    /// </summary>
    private static void DrawSpriteWithVignette(SKCanvas canvas, string charId,
        Pose pose, Emotion emotion, float cx, float cy, float scale, float time, SKRect bounds)
    {
        // Sprite-Größe berechnen (inkl. Breathing-Headroom)
        var spriteW = SpriteReferenceWidth * scale;
        var spriteH = SpriteReferenceHeight * scale;

        // Großzügiges Clip-Rect (etwas größer als Sprite für Breathing-Bewegung)
        var clipRect = new SKRect(
            cx - spriteW * 0.55f,  // 10% extra Platz
            cy - spriteH * 0.55f,
            cx + spriteW * 0.55f,
            cy + spriteH * 0.55f);

        // Auf Screen-Bounds begrenzen
        clipRect.Intersect(bounds);

        // SaveLayer mit begrenztem Rect (schneller als Fullscreen-Layer)
        canvas.SaveLayer(clipRect, null);

        // Sprite zeichnen (inkl. Breathing, Blinzeln, Mund-Animation)
        SpriteCharacterRenderer.Draw(canvas, charId, pose, emotion, cx, cy, scale, time, _spriteCache!);

        // Gecachte Vignette-Maske darüber (DstIn: nur wo Maske weiß ist bleibt sichtbar)
        var maskW = (int)MathF.Max(64, clipRect.Width);
        var maskH = (int)MathF.Max(64, clipRect.Height);
        var mask = GetOrCreateVignette(maskW, maskH);
        canvas.DrawBitmap(mask, new SKRect(0, 0, mask.Width, mask.Height), clipRect, _maskPaint);

        canvas.Restore();
    }

    /// <summary>
    /// Erstellt oder wiederverwendet eine Vignette-Maske als Alpha-Bitmap.
    /// Die Maske hat weiche Ränder (Ellipse mit Gradient) und wird gecacht.
    /// Wird nur neu erstellt wenn sich die Größe signifikant ändert (>20%).
    /// </summary>
    private static SKBitmap GetOrCreateVignette(int w, int h)
    {
        // Nur neu erstellen wenn Größe sich signifikant geändert hat
        if (_vignetteMask != null &&
            Math.Abs(_vignetteMaskW - w) < w * 0.2f &&
            Math.Abs(_vignetteMaskH - h) < h * 0.2f)
        {
            return _vignetteMask;
        }

        _vignetteMask?.Dispose();

        // Feste Maskengröße (kleiner als Sprite, wird skaliert — spart Speicher)
        var mw = Math.Min(w, 256);
        var mh = Math.Min(h, 384);

        _vignetteMask = new SKBitmap(mw, mh, SKColorType.Alpha8, SKAlphaType.Premul);
        using var maskCanvas = new SKCanvas(_vignetteMask);
        maskCanvas.Clear(SKColors.Transparent);

        // Radiale Gradient-Ellipse: Zentrum voll sichtbar (weiß), Ränder transparent
        using var paint = new SKPaint { IsAntialias = true };
        var cx = mw * 0.5f;
        var cy = mh * 0.5f;

        // Matrix für Ellipse (breiter als hoch)
        var matrix = SKMatrix.CreateScale(1f, (float)mh / mw);
        matrix = SKMatrix.CreateTranslation(cx, cy).PreConcat(
            SKMatrix.CreateScale(1f, (float)mh / mw).PreConcat(
                SKMatrix.CreateTranslation(-cx, -cy)));

        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy),
            cx,  // Radius = halbe Breite
            new SKColor[]
            {
                new(255, 255, 255, 255),  // Zentrum: voll sichtbar
                new(255, 255, 255, 255),  // 65% Radius: noch voll
                new(255, 255, 255, 180),  // 80%: leicht transparent
                new(255, 255, 255, 0)     // Rand: vollständig transparent
            },
            new float[] { 0f, 0.65f, 0.85f, 1f },
            SKShaderTileMode.Clamp,
            matrix);

        paint.Shader = shader;
        maskCanvas.DrawRect(0, 0, mw, mh, paint);

        _vignetteMaskW = w;
        _vignetteMaskH = h;

        return _vignetteMask;
    }

    /// <summary>
    /// Gibt statische Ressourcen frei.
    /// </summary>
    public static void Cleanup()
    {
        _dimFilter.Dispose();
        _dimLayerPaint.Dispose();
        _maskPaint.Dispose();
        _vignetteMask?.Dispose();
        _vignetteMask = null;
    }
}
