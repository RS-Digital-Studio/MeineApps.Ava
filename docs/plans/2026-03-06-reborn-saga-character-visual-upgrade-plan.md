# Charakter-Visual-Upgrade Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Charakter-Rendering von primitiven Flat-Color-Geometrien zu stilisiertem Anime-Look upgraden — mit Gradienten, Schattierung, natürlicheren Proportionen und Detail-Rendering.

**Architecture:** 7 spezialisierte Renderer-Klassen + zentrales Layout-System ersetzen die monolithische CharacterParts-Klasse. Jeder Renderer ist für ein Körperteil verantwortlich und arbeitet mit einem shared `CharacterLayout` struct für konsistente Positionierung.

**Tech Stack:** SkiaSharp (SKCanvas, SKPaint, SKPath, SKShader, SKMaskFilter), .NET 10, Avalonia 11.3

**Design-Dokument:** `docs/plans/2026-03-06-reborn-saga-character-visual-upgrade-design.md`

**Hinweis:** Kein TDD möglich (visuelles Rendering). Verifikation: `dotnet build` + Desktop-App starten + visuell prüfen.

---

### Task 1: CharacterLayout — Zentrales Positionssystem

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterLayout.cs`

**Step 1: CharacterLayout erstellen**

Erstelle die Datei mit folgendem Inhalt. Der Struct berechnet alle Ankerpunkte relativ zu `(cx, cy, scale)`. Die Werte basieren auf den bestehenden Proportionen in CharacterParts.cs (headW=28, headH=34, eyeOffsetX=10, etc.) und werden für das Upgrade leicht angepasst.

```csharp
namespace RebornSaga.Rendering.Characters;

/// <summary>
/// Render-Modus bestimmt Detailgrad und sichtbare Körperteile.
/// </summary>
public enum RenderMode
{
    Portrait,   // Brust aufwärts (Dialoge) — mehr Gesichtsdetail
    FullBody,   // Ganzer Körper (Klassenwahl, Status)
    Icon        // Nur Kopf (Save-Slots, Inventar)
}

/// <summary>
/// Zentrale Positionsberechnung für alle Charakter-Renderer.
/// Wird einmal pro DrawCharacter()-Aufruf berechnet (struct = kein Heap).
/// Alle Renderer arbeiten mit diesem Layout statt rohen (cx, cy, scale).
/// </summary>
public readonly struct CharacterLayout
{
    // Kopf
    public readonly float HeadCenterX;
    public readonly float HeadCenterY;
    public readonly float HeadWidth;   // Halbe Breite des Kopf-Ovals
    public readonly float HeadHeight;  // Halbe Höhe des Kopf-Ovals

    // Hals
    public readonly float NeckTopY;    // Oberkante Hals (= untere Kinn-Linie)
    public readonly float NeckBottomY; // Unterkante Hals (= Schulter-Ansatz)
    public readonly float NeckWidth;   // Halbe Breite des Halses

    // Schultern
    public readonly float ShoulderLeftX;
    public readonly float ShoulderRightX;
    public readonly float ShoulderY;

    // Augen
    public readonly float EyeY;
    public readonly float EyeOffsetX;  // Abstand der Augen von der Mitte
    public readonly float EyeSize;     // Basis-Größe eines Auges

    // Nase + Mund
    public readonly float NoseY;
    public readonly float MouthY;

    // Ohren
    public readonly float EarY;

    // Körper
    public readonly float BodyTop;     // Oberkante Körper (unter Schultern)
    public readonly float BodyBottom;  // Unterkante (abhängig von RenderMode)
    public readonly float BodyWidth;   // Halbe Breite auf Schulterhöhe

    // Meta
    public readonly float Scale;
    public readonly RenderMode Mode;

    private CharacterLayout(float cx, float cy, float scale, RenderMode mode)
    {
        Scale = scale;
        Mode = mode;

        // Kopf — leicht größer als bisherige 28x34 für bessere Proportionen
        HeadCenterX = cx;
        HeadCenterY = cy;
        HeadWidth = 30 * scale;
        HeadHeight = 36 * scale;

        // Augen — im oberen Drittel des Gesichts
        EyeY = cy - 3 * scale;
        EyeOffsetX = 11 * scale;
        EyeSize = 5.5f * scale;

        // Nase — knapp unter den Augen
        NoseY = cy + 6 * scale;

        // Mund — unteres Drittel des Gesichts
        MouthY = cy + 14 * scale;

        // Ohren — auf Augenhöhe
        EarY = cy - 2 * scale;

        // Hals — kurzer Übergang zwischen Kinn und Schultern
        NeckTopY = cy + HeadHeight * 0.65f;
        NeckBottomY = cy + HeadHeight * 0.9f;
        NeckWidth = 8 * scale;

        // Schultern
        ShoulderY = NeckBottomY;
        var shoulderSpan = 38 * scale;
        ShoulderLeftX = cx - shoulderSpan * 0.55f;
        ShoulderRightX = cx + shoulderSpan * 0.55f;

        // Körper
        BodyTop = ShoulderY;
        BodyWidth = shoulderSpan * 0.55f;
        BodyBottom = mode switch
        {
            RenderMode.Portrait => cy + 90 * scale,   // Bis Brust/Bauch
            RenderMode.FullBody => cy + 130 * scale,   // Bis Hüfte
            _ => cy + HeadHeight                        // Icon: kein Körper
        };
    }

    /// <summary>
    /// Berechnet das Layout für einen Charakter.
    /// </summary>
    public static CharacterLayout Calculate(float cx, float cy, float scale, RenderMode mode = RenderMode.Portrait)
    {
        return new CharacterLayout(cx, cy, scale, mode);
    }
}
```

**Step 2: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler, 0 Warnungen

**Step 3: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterLayout.cs
git commit -m "RebornSaga: CharacterLayout Ankerpunkt-System hinzugefügt"
```

---

### Task 2: FaceRenderer — Anime-Gesicht mit Shading

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/FaceRenderer.cs`

**Step 1: FaceRenderer erstellen**

Erstelle das Verzeichnis `Renderers/` und die Datei. Der FaceRenderer zeichnet:
- Kopfform als Bezier-Path (nicht DrawOval) mit leicht eckigerem Kinn
- Haut-Shading per radialem Gradient (heller in der Mitte, dunkler an den Rändern)
- Wangen-Highlight (subtiler heller Fleck)
- Nase (minimalistischer Anime-Stil: kleiner L-Strich)
- Ohren (nur sichtbar bei HairStyle 0 oder 3, sonst von Haaren verdeckt)
- Mund (alle 6 Emotionen, verbessert gegenüber bisherigem DrawMouth)
- Kinn-Definition (subtile Schattenlinie)

Schlüssel-Techniken:
- Kopfform: `SKPath` mit `MoveTo/CubicTo` statt `DrawOval`. Breite Stirn, schmales Kinn.
- Haut-Shading: `SKShader.CreateRadialGradient` von SkinColor (Zentrum) zu DarkenColor(SkinColor, 0.85f) (Rand). Wird als statischer Shader gecacht und nur bei Scale-Änderung invalidiert.
- Wangen-Highlight: Kleiner Kreis mit LightenColor(SkinColor, 0.15f) und Alpha 60, auf beiden Wangen.
- Nase: Zwei kurze Linien (vertikaler Strich + kleiner Haken nach rechts) in DarkenColor(SkinColor, 0.7f).
- Ohren: Kleine C-förmige Paths links und rechts, in SkinColor mit dunklerer Outline.
- Mund: Verbesserte Versionen der 6 Emotionen aus dem bisherigen DrawMouth.
  - Happy: Offener U-Bogen, weißes Inneres (Zähne), rosa Zungen-Andeutung
  - Angry: Breiter Strich, leichte Wellung, Zähne-Linie
  - Surprised: Großes Oval, rosa Inneres
  - Sad: Leicht geöffnet nach unten
  - Determined: Asymmetrisches Grinsen
  - Neutral: Dezenter Strich

Alle Paints und Paths sind `private static readonly` (gepooled). Keine per-Frame Allokation.

```csharp
namespace RebornSaga.Rendering.Characters.Renderers;

using SkiaSharp;
using System;

/// <summary>
/// Zeichnet das Anime-Gesicht: Kopfform, Haut-Shading, Nase, Ohren, Mund, Kinn.
/// Alle SKPaint/SKPath statisch gepooled.
/// </summary>
public static class FaceRenderer
{
    private static readonly SKPaint _skinPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _shadingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _highlightPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _mouthFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPath _headPath = new();
    private static readonly SKPath _earPath = new();
    private static readonly SKPath _mouthPath = new();

    // Gecachter Haut-Shader (invalidiert bei Scale-Änderung)
    private static float _cachedScale;
    private static float _cachedCx;
    private static float _cachedCy;
    private static SKShader? _skinShader;
    private static SKColor _cachedSkinColor;

    /// <summary>
    /// Zeichnet das komplette Gesicht (Kopf, Ohren, Nase, Mund).
    /// </summary>
    public static void Draw(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def, Emotion emotion)
    {
        DrawHeadShape(canvas, layout, def);
        DrawSkinShading(canvas, layout, def);
        DrawCheekHighlights(canvas, layout, def);

        // Ohren nur bei kurzem Haar sichtbar (Style 0=kurz, 3=wild)
        if (def.HairStyle is 0 or 3)
            DrawEars(canvas, layout, def);

        DrawNose(canvas, layout, def);
        DrawChinLine(canvas, layout, def);
        DrawMouth(canvas, layout, def, emotion);
    }

    private static void DrawHeadShape(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def)
    {
        var cx = layout.HeadCenterX;
        var cy = layout.HeadCenterY;
        var w = layout.HeadWidth;
        var h = layout.HeadHeight;

        // Kopfform als Bezier-Path: breite Stirn, weiche Wangen, schmales Kinn
        _headPath.Rewind();
        _headPath.MoveTo(cx, cy - h);                                       // Oberkopf Mitte
        _headPath.CubicTo(cx + w * 1.1f, cy - h,                           // Rechte Stirn (breit)
                          cx + w, cy - h * 0.2f,                             // Rechte Wange oben
                          cx + w * 0.85f, cy + h * 0.2f);                   // Rechte Wange
        _headPath.CubicTo(cx + w * 0.7f, cy + h * 0.6f,                    // Rechter Kiefer
                          cx + w * 0.35f, cy + h * 0.9f,                    // Kinn rechts
                          cx, cy + h * 0.95f);                               // Kinnspitze
        _headPath.CubicTo(cx - w * 0.35f, cy + h * 0.9f,                   // Kinn links
                          cx - w * 0.7f, cy + h * 0.6f,                     // Linker Kiefer
                          cx - w * 0.85f, cy + h * 0.2f);                   // Linke Wange
        _headPath.CubicTo(cx - w, cy - h * 0.2f,                            // Linke Wange oben
                          cx - w * 1.1f, cy - h,                             // Linke Stirn (breit)
                          cx, cy - h);                                        // Zurück zum Oberkopf
        _headPath.Close();

        // Haut-Füllung
        _skinPaint.Color = def.SkinColor;
        canvas.DrawPath(_headPath, _skinPaint);

        // Kopf-Outline
        _linePaint.Color = DarkenColor(def.SkinColor, 0.55f);
        _linePaint.StrokeWidth = 1.5f * layout.Scale;
        canvas.DrawPath(_headPath, _linePaint);
    }

    private static void DrawSkinShading(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def)
    {
        // Haut-Gradient nur neu erstellen wenn sich Parameter geändert haben
        if (_skinShader == null || _cachedScale != layout.Scale ||
            _cachedCx != layout.HeadCenterX || _cachedCy != layout.HeadCenterY ||
            _cachedSkinColor != def.SkinColor)
        {
            _skinShader?.Dispose();
            _skinShader = SKShader.CreateRadialGradient(
                new SKPoint(layout.HeadCenterX, layout.HeadCenterY - layout.HeadHeight * 0.15f),
                layout.HeadWidth * 1.3f,
                new[] { def.SkinColor.WithAlpha(0), DarkenColor(def.SkinColor, 0.82f).WithAlpha(80) },
                SKShaderTileMode.Clamp);
            _cachedScale = layout.Scale;
            _cachedCx = layout.HeadCenterX;
            _cachedCy = layout.HeadCenterY;
            _cachedSkinColor = def.SkinColor;
        }

        // Shading nur innerhalb der Kopfform (Clip)
        canvas.Save();
        canvas.ClipPath(_headPath);
        _shadingPaint.Shader = _skinShader;
        canvas.DrawRect(
            layout.HeadCenterX - layout.HeadWidth * 1.5f,
            layout.HeadCenterY - layout.HeadHeight * 1.5f,
            layout.HeadWidth * 3f,
            layout.HeadHeight * 3f, _shadingPaint);
        _shadingPaint.Shader = null;
        canvas.Restore();
    }

    private static void DrawCheekHighlights(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def)
    {
        // Subtiler heller Wangenfleck (Anime-typisch)
        var cheekColor = LightenColor(def.SkinColor, 0.15f).WithAlpha(50);
        _highlightPaint.Color = cheekColor;
        var cheekSize = 5f * layout.Scale;
        var cheekY = layout.EyeY + layout.EyeSize * 1.8f;
        canvas.DrawOval(layout.HeadCenterX - layout.EyeOffsetX * 0.9f, cheekY, cheekSize, cheekSize * 0.6f, _highlightPaint);
        canvas.DrawOval(layout.HeadCenterX + layout.EyeOffsetX * 0.9f, cheekY, cheekSize, cheekSize * 0.6f, _highlightPaint);
    }

    private static void DrawEars(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def)
    {
        var earSize = 8 * layout.Scale;
        var earX = layout.HeadWidth * 0.9f;

        for (int side = -1; side <= 1; side += 2)
        {
            var ex = layout.HeadCenterX + earX * side;

            _earPath.Rewind();
            _earPath.MoveTo(ex, layout.EarY - earSize * 0.5f);
            _earPath.CubicTo(
                ex + 5 * layout.Scale * side, layout.EarY - earSize * 0.3f,
                ex + 6 * layout.Scale * side, layout.EarY + earSize * 0.3f,
                ex, layout.EarY + earSize * 0.5f);
            _earPath.Close();

            _skinPaint.Color = def.SkinColor;
            canvas.DrawPath(_earPath, _skinPaint);

            // Ohr-Inneres (dunkler)
            _linePaint.Color = DarkenColor(def.SkinColor, 0.75f);
            _linePaint.StrokeWidth = 1f * layout.Scale;
            canvas.DrawPath(_earPath, _linePaint);
        }
    }

    private static void DrawNose(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def)
    {
        // Minimalistischer Anime-Stil: kleiner L-Strich
        _linePaint.Color = DarkenColor(def.SkinColor, 0.65f);
        _linePaint.StrokeWidth = 1.2f * layout.Scale;

        var nx = layout.HeadCenterX;
        var ny = layout.NoseY;
        var ns = 3f * layout.Scale;

        // Vertikaler Strich + kleiner Haken
        canvas.DrawLine(nx, ny - ns, nx, ny + ns * 0.5f, _linePaint);
        canvas.DrawLine(nx, ny + ns * 0.5f, nx + ns * 0.4f, ny + ns * 0.3f, _linePaint);
    }

    private static void DrawChinLine(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def)
    {
        // Subtile Schattenlinie unter dem Mund für Kinn-Definition
        _linePaint.Color = DarkenColor(def.SkinColor, 0.8f).WithAlpha(60);
        _linePaint.StrokeWidth = 1f * layout.Scale;
        var chinY = layout.MouthY + 8 * layout.Scale;
        var chinW = 6 * layout.Scale;
        canvas.DrawLine(layout.HeadCenterX - chinW, chinY,
                        layout.HeadCenterX + chinW, chinY, _linePaint);
    }

    private static void DrawMouth(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def, Emotion emotion)
    {
        var cx = layout.HeadCenterX;
        var my = layout.MouthY;
        var mw = 6.5f * layout.Scale;
        var s = layout.Scale;

        _linePaint.Color = DarkenColor(def.SkinColor, 0.45f);
        _linePaint.StrokeWidth = 1.5f * s;
        _mouthPath.Rewind();

        switch (emotion)
        {
            case Emotion.Neutral:
                // Dezenter leicht gebogener Strich
                _mouthPath.MoveTo(cx - mw * 0.4f, my);
                _mouthPath.QuadTo(cx, my + 1f * s, cx + mw * 0.4f, my);
                canvas.DrawPath(_mouthPath, _linePaint);
                break;

            case Emotion.Happy:
                // Offener lächelnder Mund
                _mouthPath.MoveTo(cx - mw, my);
                _mouthPath.QuadTo(cx, my + mw * 1.1f, cx + mw, my);
                _mouthPath.Close();
                // Weißer Innenraum (Zähne)
                _mouthFillPaint.Color = SKColors.White;
                canvas.DrawPath(_mouthPath, _mouthFillPaint);
                canvas.DrawPath(_mouthPath, _linePaint);
                // Rosa Zungen-Andeutung unten
                _mouthFillPaint.Color = new SKColor(0xE8, 0x8B, 0x8B, 160);
                canvas.DrawOval(cx, my + mw * 0.6f, mw * 0.4f, mw * 0.25f, _mouthFillPaint);
                break;

            case Emotion.Angry:
                // Zusammengebissene Zähne
                _mouthPath.MoveTo(cx - mw * 0.8f, my);
                _mouthPath.LineTo(cx + mw * 0.8f, my);
                _linePaint.StrokeWidth = 2f * s;
                canvas.DrawPath(_mouthPath, _linePaint);
                // Zähne-Linie (vertikale Striche)
                _linePaint.StrokeWidth = 0.8f * s;
                _linePaint.Color = DarkenColor(def.SkinColor, 0.35f);
                for (float tx = cx - mw * 0.5f; tx <= cx + mw * 0.5f; tx += 3f * s)
                    canvas.DrawLine(tx, my - 1.5f * s, tx, my + 1.5f * s, _linePaint);
                break;

            case Emotion.Sad:
                // Leicht geöffneter Mund nach unten
                _mouthPath.MoveTo(cx - mw * 0.5f, my);
                _mouthPath.QuadTo(cx, my - mw * 0.4f, cx + mw * 0.5f, my);
                canvas.DrawPath(_mouthPath, _linePaint);
                break;

            case Emotion.Surprised:
                // Großes offenes Oval
                _mouthFillPaint.Color = new SKColor(0xD0, 0x70, 0x70, 180);
                canvas.DrawOval(cx, my + 2 * s, mw * 0.5f, mw * 0.75f, _mouthFillPaint);
                _linePaint.StrokeWidth = 1.5f * s;
                _linePaint.Color = DarkenColor(def.SkinColor, 0.4f);
                canvas.DrawOval(cx, my + 2 * s, mw * 0.5f, mw * 0.75f, _linePaint);
                break;

            case Emotion.Determined:
                // Asymmetrisches Grinsen
                _mouthPath.MoveTo(cx - mw * 0.5f, my + 1f * s);
                _mouthPath.QuadTo(cx, my + 2.5f * s, cx + mw * 0.7f, my - 1f * s);
                _linePaint.StrokeWidth = 2f * s;
                canvas.DrawPath(_mouthPath, _linePaint);
                break;
        }
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _skinPaint.Dispose();
        _shadingPaint.Dispose();
        _highlightPaint.Dispose();
        _linePaint.Dispose();
        _mouthFillPaint.Dispose();
        _headPath.Dispose();
        _earPath.Dispose();
        _mouthPath.Dispose();
        _skinShader?.Dispose();
    }

    // --- Hilfs-Methoden ---
    private static SKColor DarkenColor(SKColor c, float f) => new(
        (byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f), c.Alpha);

    private static SKColor LightenColor(SKColor c, float a) => new(
        (byte)Math.Min(255, c.Red + (255 - c.Red) * a),
        (byte)Math.Min(255, c.Green + (255 - c.Green) * a),
        (byte)Math.Min(255, c.Blue + (255 - c.Blue) * a), c.Alpha);
}
```

**Step 2: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler

**Step 3: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/FaceRenderer.cs
git commit -m "RebornSaga: FaceRenderer mit Anime-Gesicht, Shading, Nase, Ohren, Mund"
```

---

### Task 3: EyeRenderer — Mehrstufige Anime-Augen

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/EyeRenderer.cs`

**Step 1: EyeRenderer erstellen**

Der EyeRenderer implementiert das wichtigste Element des Anime-Looks:
- Mehrschichtige Iris (äußerer Ring dunkel, mittlerer Ring EyeColor, innerer Ring hell)
- Ovale Pupille, leicht nach oben versetzt
- 2 weiße Reflektionspunkte (groß oben-rechts, klein unten-links)
- Wimpern als dickere obere Lidrand-Linie mit Aufwärts-Schwung
- Brauen für ALLE 6 Emotionen (Form, Dicke, Position emotions-abhängig)
- Blinzel-Animation (weiches Schließen statt abrupter Linie)
- Leuchtende Augen (Nihilus etc.) mit pulsierendem Glow

Schlüssel-Technik für die Iris: Drei gestapelte Ovale mit abnehmender Größe:
1. Äußerer Ring: DarkenColor(EyeColor, 0.5f) — dunkler Rand
2. Mittlerer Ring: EyeColor — Hauptfarbe
3. Innerer Ring: LightenColor(EyeColor, 0.4f) — heller Kern
Plus Pupille (dunkel) und Reflektionen (weiß).

Brauen-System: Jede Emotion definiert 4 Werte pro Braue:
- `innerY` (inneres Ende, nahe Nase)
- `outerY` (äußeres Ende)
- `thickness` (Strichdicke)
- `curve` (Krümmung des QuadTo-Kontrollpunkts)

```csharp
namespace RebornSaga.Rendering.Characters.Renderers;

using SkiaSharp;
using System;

/// <summary>
/// Zeichnet mehrstufige Anime-Augen: Iris-Gradient, Pupille, Reflektionen, Brauen, Blinzeln.
/// Statisch gepoolte Paints.
/// </summary>
public static class EyeRenderer
{
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPath _lidPath = new();
    private static readonly SKPath _browPath = new();

    private static readonly SKMaskFilter _eyeGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    /// <summary>
    /// Zeichnet beide Augen + Brauen basierend auf Emotion und Zeit (Blinzeln).
    /// </summary>
    public static void Draw(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def,
        Emotion emotion, float time)
    {
        // Blinzel-Zyklus (~4s, Blinzeln dauert 0.15s)
        var blinkCycle = time % 4f;
        var blinkProgress = blinkCycle > 3.85f
            ? Math.Clamp((blinkCycle - 3.85f) / 0.15f, 0f, 1f)
            : 0f;
        // Weiches Auf/Zu: Sinus-Kurve für natürlicheres Blinzeln
        var lidClose = blinkProgress > 0.5f ? 1f - (blinkProgress - 0.5f) * 2f : blinkProgress * 2f;

        for (int side = -1; side <= 1; side += 2)
        {
            var ex = layout.HeadCenterX + layout.EyeOffsetX * side;
            var ey = layout.EyeY;
            var size = layout.EyeSize;
            var s = layout.Scale;

            // Blinzeln: Augen schließen (Lid senkt sich)
            if (lidClose > 0.8f && emotion != Emotion.Surprised)
            {
                // Geschlossene Augen — gebogene Linie statt Oval
                _strokePaint.Color = DarkenColor(def.SkinColor, 0.4f);
                _strokePaint.StrokeWidth = 2f * s;
                _lidPath.Rewind();
                _lidPath.MoveTo(ex - size * 0.8f, ey);
                _lidPath.QuadTo(ex, ey + size * 0.3f, ex + size * 0.8f, ey);
                canvas.DrawPath(_lidPath, _strokePaint);
                // Brauen auch bei geschlossenen Augen zeichnen
                DrawBrow(canvas, layout, def, emotion, ex, ey, size, side);
                continue;
            }

            var narrow = emotion == Emotion.Angry;
            var eyeW = size * (narrow ? 0.7f : 0.9f);
            var eyeH = size * (narrow ? 0.8f : 1.1f);

            // Happy-Emotion: halb-geschlossene lächelnde Augen
            if (emotion == Emotion.Happy)
            {
                _strokePaint.Color = def.EyeColor;
                _strokePaint.StrokeWidth = 2.5f * s;
                _lidPath.Rewind();
                _lidPath.MoveTo(ex - eyeW * 0.8f, ey + eyeH * 0.15f);
                _lidPath.QuadTo(ex, ey - eyeH * 0.5f, ex + eyeW * 0.8f, ey + eyeH * 0.15f);
                canvas.DrawPath(_lidPath, _strokePaint);
                DrawBrow(canvas, layout, def, emotion, ex, ey, size, side);
                continue;
            }

            // --- Vollständiges Auge ---

            // 1. Weißer Augapfel
            _fillPaint.Color = SKColors.White;
            canvas.DrawOval(ex, ey, eyeW, eyeH, _fillPaint);

            // 2. Äußerer Iris-Ring (dunkel)
            _fillPaint.Color = DarkenColor(def.EyeColor, 0.5f);
            canvas.DrawOval(ex, ey + size * 0.05f, eyeW * 0.7f, eyeH * 0.75f, _fillPaint);

            // 3. Mittlerer Iris-Ring (Hauptfarbe)
            _fillPaint.Color = def.EyeColor;
            canvas.DrawOval(ex, ey + size * 0.05f, eyeW * 0.55f, eyeH * 0.6f, _fillPaint);

            // 4. Innerer heller Kern
            _fillPaint.Color = LightenColor(def.EyeColor, 0.4f);
            canvas.DrawOval(ex, ey - size * 0.05f, eyeW * 0.3f, eyeH * 0.35f, _fillPaint);

            // 5. Pupille (dunkel, oval)
            _fillPaint.Color = new SKColor(0x0A, 0x0A, 0x12);
            canvas.DrawOval(ex, ey + size * 0.02f, eyeW * 0.25f, eyeH * 0.35f, _fillPaint);

            // 6. Reflektionen — DAS Anime-Kennzeichen
            _fillPaint.Color = SKColors.White;
            // Großer Reflex oben-rechts
            canvas.DrawOval(ex + eyeW * 0.25f, ey - eyeH * 0.3f, eyeW * 0.18f, eyeH * 0.15f, _fillPaint);
            // Kleiner Reflex unten-links
            canvas.DrawCircle(ex - eyeW * 0.15f, ey + eyeH * 0.25f, eyeW * 0.08f, _fillPaint);

            // 7. Oberer Lidrand (Wimpern) — dickere Linie mit Schwung
            _strokePaint.Color = DarkenColor(def.SkinColor, 0.25f);
            _strokePaint.StrokeWidth = 2.5f * s;
            _lidPath.Rewind();
            _lidPath.MoveTo(ex - eyeW * 1.1f, ey - eyeH * 0.3f);
            _lidPath.QuadTo(ex, ey - eyeH * 1.05f, ex + eyeW * 1.1f, ey - eyeH * 0.2f);
            canvas.DrawPath(_lidPath, _strokePaint);

            // 8. Auge Outline
            _strokePaint.Color = DarkenColor(def.SkinColor, 0.4f);
            _strokePaint.StrokeWidth = 1f * s;
            canvas.DrawOval(ex, ey, eyeW, eyeH, _strokePaint);

            // Brauen
            DrawBrow(canvas, layout, def, emotion, ex, ey, size, side);
        }

        // Leuchtende Augen (z.B. Nihilus)
        if (def.HasGlowingEyes)
        {
            var pulse = 0.7f + MathF.Sin(time * 2.5f) * 0.3f;
            _glowPaint.Color = def.EyeColor.WithAlpha((byte)(60 * pulse));
            _glowPaint.MaskFilter = _eyeGlow;
            canvas.DrawCircle(layout.HeadCenterX - layout.EyeOffsetX, layout.EyeY,
                layout.EyeSize * 1.8f, _glowPaint);
            canvas.DrawCircle(layout.HeadCenterX + layout.EyeOffsetX, layout.EyeY,
                layout.EyeSize * 1.8f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }
    }

    private static void DrawBrow(SKCanvas canvas, CharacterLayout layout, CharacterDefinition def,
        Emotion emotion, float ex, float ey, float eyeSize, int side)
    {
        var s = layout.Scale;
        var browY = ey - eyeSize * 1.4f;
        float innerOffset, outerOffset, thickness, curve;

        switch (emotion)
        {
            case Emotion.Neutral:
                innerOffset = 0; outerOffset = 0; thickness = 2f; curve = -2f * s;
                break;
            case Emotion.Happy:
                innerOffset = -2f * s; outerOffset = -3f * s; thickness = 1.8f; curve = -3f * s;
                break;
            case Emotion.Angry:
                innerOffset = 4f * s; outerOffset = -2f * s; thickness = 3f; curve = 2f * s;
                break;
            case Emotion.Sad:
                innerOffset = -3f * s; outerOffset = 3f * s; thickness = 1.8f; curve = -1f * s;
                break;
            case Emotion.Surprised:
                innerOffset = -5f * s; outerOffset = -5f * s; thickness = 2f; curve = -4f * s;
                break;
            case Emotion.Determined:
                innerOffset = 2f * s; outerOffset = 0; thickness = 2.5f; curve = 0;
                break;
            default:
                innerOffset = 0; outerOffset = 0; thickness = 2f; curve = 0;
                break;
        }

        _strokePaint.Color = def.HairColor;
        _strokePaint.StrokeWidth = thickness * s;

        var innerX = ex - eyeSize * 0.4f * side;
        var outerX = ex + eyeSize * 0.9f * side;

        _browPath.Rewind();
        _browPath.MoveTo(innerX, browY + innerOffset);
        _browPath.QuadTo(ex + eyeSize * 0.3f * side, browY + curve, outerX, browY + outerOffset);
        canvas.DrawPath(_browPath, _strokePaint);
    }

    public static void Cleanup()
    {
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _glowPaint.Dispose();
        _lidPath.Dispose();
        _browPath.Dispose();
        _eyeGlow.Dispose();
    }

    private static SKColor DarkenColor(SKColor c, float f) => new(
        (byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f), c.Alpha);

    private static SKColor LightenColor(SKColor c, float a) => new(
        (byte)Math.Min(255, c.Red + (255 - c.Red) * a),
        (byte)Math.Min(255, c.Green + (255 - c.Green) * a),
        (byte)Math.Min(255, c.Blue + (255 - c.Blue) * a), c.Alpha);
}
```

**Step 2: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler

**Step 3: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/EyeRenderer.cs
git commit -m "RebornSaga: EyeRenderer mit Iris-Gradient, Reflektionen, Brauen, Blinzeln"
```

---

### Task 4: HairRenderer — Volumen und Bewegung

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/HairRenderer.cs`

**Step 1: HairRenderer erstellen**

Der HairRenderer hat zwei öffentliche Methoden:
- `DrawBack()` — Haare hinter dem Körper (lange Haare, Zopf-Ende)
- `DrawFront()` — Pony/Strähnen vor dem Gesicht

Verbesserungen gegenüber dem bisherigen DrawHair:
- Mehr Volumen (Haare stehen über dem Kopf, nicht bündig)
- Highlight-Strähnen (LightenColor 35%) als separate leichte Linien
- Schatten-Bereiche (DarkenColor 25%) an Unterseite und Kopfnähe
- Pony (Bangs) als separate Strähnen über der Stirn
- Wind-Animation mit unterschiedlicher Intensität pro Strähne
- DrawBack/DrawFront Split für korrekte Z-Order

4 Styles:
- Style 0 (Kurz): Enganliegend mit leichtem Volumen oben, kurze Seitensträhnen
- Style 1 (Lang-glatt): Fließend über Schultern, Strähnchen, Highlights
- Style 2 (Zopf): Oberteil wie kurz + geflochtener Zopf hinten
- Style 3 (Wild/stachelig): Spitze Strähnen in alle Richtungen, maximales Volumen

Implementierung: Gleiche Path-Rewind()-Technik wie bisher. Jeder Style hat seine eigene Draw-Logik. Highlights und Schatten werden als zusätzliche semi-transparente Strokes über/unter der Basis gezeichnet.

**Pony-System:**
- `def.HasBangs`: Ob Pony gezeichnet wird
- `def.BangStyle`: 0=gerade (horizontale Strähnen über Stirn), 1=seitlich (geschwungene Strähnen nach einer Seite), 2=lose Strähnen (2-3 einzelne Strähnen)
- Pony wird IMMER in DrawFront() gezeichnet (vor dem Gesicht, nach den Augen)

Hinweis: Jeder der 4 Haar-Styles hat DrawBack und DrawFront. Bei Style 0 (kurz) gibt es kein DrawBack. Bei Style 1 (lang) ist DrawBack der Großteil der Haare. Pony kommt bei allen Styles in DrawFront wenn `HasBangs = true`.

Die Datei wird ca. 280-350 Zeilen. Genaue Bezier-Kontrollpunkte müssen visuell iteriert werden, die Basis aus den bisherigen CharacterParts-Werten übernehmen und verbessern.

**Step 2: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler

**Step 3: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/HairRenderer.cs
git commit -m "RebornSaga: HairRenderer mit Volumen, Highlights, Pony, Wind-Animation"
```

---

### Task 5: BodyRenderer + ClothingRenderer — Oberkörper

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/BodyRenderer.cs`
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/ClothingRenderer.cs`

**Step 1: BodyRenderer erstellen**

Der BodyRenderer zeichnet:
- Hals (zylindrisch, Hautfarbe mit Schattierung)
- Schultern (natürliche Bezier-Kurven, runde Form)
- Arme (einfach, leicht angewinkelt, Portrait: nur Oberarm, FullBody: mit Händen)
- Körper-Silhouette (3 BodyTypes) mit OutfitColor-Gradient (heller oben, dunkler unten)

Schlüssel-Technik: Der Körper wird als ein zusammenhängender Path gezeichnet der Schultern, Torso und (im FullBody-Modus) Hüften umfasst. Darüber ein vertikaler Gradient mit `SKShader.CreateLinearGradient` für den Outfit-Shading-Effekt.

Arme: Einfache Bezier-Kurven von den Schultern nach unten, leicht nach außen gebogen. Im Portrait-Modus enden sie am unteren Bildrand. Im FullBody-Modus enden sie mit einfachen Faust-/Handformen (Kreise mit angedeuteten Fingern).

**Step 2: ClothingRenderer erstellen**

Der ClothingRenderer zeichnet Details ÜBER dem Body:
- Kragen: V-Ausschnitt (BodyType 0), Stehkragen (BodyType 1), Kapuze (BodyType 2)
- Gürtel/Schärpe: Breiter Streifen in OutfitAccent mit Schnallen-Andeutung
- Stoff-Falten: 2-3 DarkenColor-Strokes über dem Torso für Tiefe
- Schulterpolster (BodyType 1): Verbesserter Look mit Rundung und Highlight
- Roben-Saum (BodyType 2): Wellenförmiger unterer Rand

**Step 3: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler

**Step 4: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/BodyRenderer.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/ClothingRenderer.cs
git commit -m "RebornSaga: BodyRenderer + ClothingRenderer mit Hals, Armen, Outfit-Details"
```

---

### Task 6: AccessoryRenderer + CharacterEffects

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/AccessoryRenderer.cs`
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/CharacterEffects.cs`

**Step 1: AccessoryRenderer erstellen**

Verbesserte Waffen:
- **Schwert (AccessoryType 0):** Klinge als Path mit Metall-Gradient (Grau-Silber-Grau), ornamentierte Parierstange (OutfitAccent mit kleinem Juwel), Leder-Griff mit Wicklung (diagonale Linien), wandernder Glanz-Highlight
- **Stab (AccessoryType 1):** Holz-Schaft mit feinen Längslinien-Muster, Kristall-Spitze als Polygon mit innerem Glow (OutfitAccent), Facetten-Andeutung durch helle Linien auf dem Kristall
- **Dolche (AccessoryType 2):** Geschwungene Klinge (Bezier statt Dreieck), Leder-Griff-Wicklung, OutfitAccent Parierstange
- **Alle Waffen:** Subtiler wandernder Highlight-Punkt (weißer Kreis mit niedrigem Alpha, Position = `sin(time) * Klingenlänge`)

**Step 2: CharacterEffects erstellen**

Effekte die vor/nach dem Charakter gezeichnet werden:
- `DrawShadow(canvas, layout)` — Ovaler Boden-Schatten. Nur im FullBody-Modus. `DrawOval` mit `SKColor(0,0,0,25)` unter den Füßen.
- `DrawHologram(canvas, layout, def, time)` — Für IsHolographic Charaktere (SystemAria):
  - Horizontale Scan-Lines (alle 4px, sehr niedrig Alpha)
  - Periodischer Flicker (alle ~3s für 0.1s, Alpha reduziert)
  - Blaue Kontur-Linie um den ganzen Charakter
- `DrawAura(canvas, layout, def, time)` — Wenn `def.AuraColor != null`:
  - Radialer Glow um den Charakter
  - Pulsierend über time (Sinus)
  - MaskFilter.CreateBlur für weichen Rand

**Step 3: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler

**Step 4: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/AccessoryRenderer.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/Renderers/CharacterEffects.cs
git commit -m "RebornSaga: AccessoryRenderer + CharacterEffects mit Waffen-Glanz, Schatten, Aura"
```

---

### Task 7: CharacterParts Refactoring + CharacterDefinitions Update

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterParts.cs` (komplett umschreiben)
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterDefinitions.cs` (neue Properties)

**Step 1: CharacterDefinition erweitern**

Neue Properties zu `CharacterDefinition` hinzufügen (Zeile 20-22 einfügen):

```csharp
public bool HasBangs { get; init; }      // Pony ja/nein
public int BangStyle { get; init; }      // 0=gerade, 1=seitlich, 2=strähnenartig
public SKColor? AuraColor { get; init; } // Optionale Aura-Farbe
```

Alle 10 Definitionen in `CharacterDefinitions` mit den neuen Properties aktualisieren:

| Charakter | HasBangs | BangStyle | AuraColor |
|-----------|----------|-----------|-----------|
| Protagonist_Sword | true | 2 | null |
| Protagonist_Mage | true | 1 | null |
| Protagonist_Assassin | false | 0 | null |
| Aria | true | 1 | null |
| Aldric | false | 0 | null |
| Kael | true | 2 | null |
| Luna | true | 0 | null |
| Vex | false | 0 | null |
| SystemAria | true | 1 | null |
| Nihilus | false | 0 | `new SKColor(0x8B, 0x00, 0x00)` |
| Xaroth | true | 1 | `new SKColor(0xCC, 0x00, 0x00)` |

**Step 2: CharacterParts zum Orchestrator umbauen**

`CharacterParts.cs` komplett ersetzen. Die Klasse wird zur dünnen Fassade:

```csharp
namespace RebornSaga.Rendering.Characters;

using RebornSaga.Rendering.Characters.Renderers;
using SkiaSharp;

/// <summary>
/// Orchestriert das Charakter-Rendering. Ruft die spezialisierten Renderer
/// in der korrekten Reihenfolge auf.
/// </summary>
public static class CharacterParts
{
    /// <summary>
    /// Zeichnet einen kompletten Charakter mit allen Komponenten.
    /// </summary>
    public static void DrawCharacter(SKCanvas canvas, CharacterDefinition def, Emotion emotion,
        float cx, float cy, float scale, float time,
        RenderMode mode = RenderMode.Portrait)
    {
        var layout = CharacterLayout.Calculate(cx, cy, scale, mode);

        // 1. Boden-Schatten (nur FullBody)
        CharacterEffects.DrawShadow(canvas, layout);

        // 2. Haare hinter dem Körper
        HairRenderer.DrawBack(canvas, layout, def, time);

        // 3. Körper + Kleidung (nicht im Icon-Modus)
        if (mode != RenderMode.Icon)
        {
            BodyRenderer.Draw(canvas, layout, def);
            ClothingRenderer.Draw(canvas, layout, def);
            AccessoryRenderer.Draw(canvas, layout, def, time);
        }

        // 4. Gesicht
        FaceRenderer.Draw(canvas, layout, def, emotion);

        // 5. Augen
        EyeRenderer.Draw(canvas, layout, def, emotion, time);

        // 6. Haare vor dem Gesicht (Pony)
        HairRenderer.DrawFront(canvas, layout, def, time);

        // 7. Effekte (Hologramm, Aura)
        CharacterEffects.DrawAura(canvas, layout, def, time);
    }

    // Legacy-Methoden für CharacterRenderer.DrawIcon() Kompatibilität
    // (DrawIcon ruft aktuell DrawHead, DrawHair, DrawEyes, DrawMouth direkt auf)
    public static void DrawHead(SKCanvas canvas, CharacterDefinition def, float cx, float cy, float scale)
    {
        var layout = CharacterLayout.Calculate(cx, cy, scale, RenderMode.Icon);
        FaceRenderer.Draw(canvas, layout, def, Emotion.Neutral);
    }

    public static void DrawHair(SKCanvas canvas, CharacterDefinition def, float cx, float cy, float scale, float time)
    {
        var layout = CharacterLayout.Calculate(cx, cy, scale, RenderMode.Icon);
        HairRenderer.DrawBack(canvas, layout, def, time);
        HairRenderer.DrawFront(canvas, layout, def, time);
    }

    public static void DrawEyes(SKCanvas canvas, CharacterDefinition def, Emotion emotion,
        float cx, float cy, float scale, float time)
    {
        var layout = CharacterLayout.Calculate(cx, cy, scale, RenderMode.Icon);
        EyeRenderer.Draw(canvas, layout, def, emotion, time);
    }

    public static void DrawMouth(SKCanvas canvas, CharacterDefinition def, Emotion emotion,
        float cx, float cy, float scale)
    {
        // Mund ist jetzt Teil von FaceRenderer — wird bereits in DrawHead gezeichnet
        // Diese Methode bleibt als No-Op für Abwärtskompatibilität
    }

    /// <summary>Gibt alle Renderer-Ressourcen frei.</summary>
    public static void Cleanup()
    {
        FaceRenderer.Cleanup();
        EyeRenderer.Cleanup();
        HairRenderer.Cleanup();
        BodyRenderer.Cleanup();
        ClothingRenderer.Cleanup();
        AccessoryRenderer.Cleanup();
        CharacterEffects.Cleanup();
    }
}
```

**Step 3: CharacterRenderer aktualisieren**

In `CharacterRenderer.cs` den `DrawCharacter`-Aufruf um den RenderMode ergänzen:

- `DrawPortrait` (Zeile 57): `CharacterParts.DrawCharacter(canvas, def, emotion, cx, cy, scale, time, RenderMode.Portrait);`
- `DrawFullBody` (Zeile 73): `CharacterParts.DrawCharacter(canvas, def, emotion, cx, cy, scale, time, RenderMode.FullBody);`
- `DrawIcon` (Zeilen 83-88): Bleibt wie bisher (ruft die Legacy-Methoden auf) oder wird auf `CharacterParts.DrawCharacter(..., RenderMode.Icon)` umgestellt.

**Step 4: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Desktop` (wenn vorhanden)
Expected: 0 Fehler

**Step 5: Commit**

```bash
git add -A src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/
git commit -m "RebornSaga: CharacterParts zum Orchestrator refactored, Definitionen aktualisiert"
```

---

### Task 8: Visuelles Tuning + CLAUDE.md Update

**Files:**
- Modify: Alle Renderer in `Renderers/` (Koordinaten-Feintuning)
- Modify: `src/Apps/RebornSaga/CLAUDE.md`

**Step 1: Desktop-App starten und visuell prüfen**

Run: `dotnet run --project src/Apps/RebornSaga/RebornSaga.Desktop`

Prüfpunkte:
- [ ] Gesichtsform sieht anime-typisch aus (nicht zu rund, nicht zu eckig)
- [ ] Augen haben sichtbare Iris-Schichten und Reflektionspunkte
- [ ] Brauen sind bei allen 6 Emotionen sichtbar und unterschiedlich
- [ ] Haare haben Volumen (stehen über dem Kopf)
- [ ] Pony-Strähnen fallen natürlich über die Stirn
- [ ] Hals verbindet Kopf und Körper nahtlos
- [ ] Arme sind sichtbar an den Seiten
- [ ] Outfit-Gradient gibt dem Körper Tiefe
- [ ] Waffen haben Metall-Glanz
- [ ] Hologramm-Effekt funktioniert bei SystemAria
- [ ] Aktiver/Inaktiver Sprecher-Unterschied ist klar sichtbar
- [ ] Fullbody-Modus in Klassenwahl sieht proportional aus
- [ ] Icon-Modus in Save-Slots zeigt erkennbaren Kopf

**Step 2: Koordinaten und Werte anpassen**

Basierend auf der visuellen Prüfung: Bezier-Kontrollpunkte, Farb-Alphas, Gradient-Positionen, Stroke-Widths fine-tunen. Dies ist ein iterativer Prozess — mehrere Durchläufe (Ändern → Build → Prüfen) sind normal.

Typische Anpassungen:
- Kopfform: CubicTo-Kontrollpunkte in FaceRenderer.DrawHeadShape
- Augenproportionen: eyeW/eyeH Multiplikatoren in EyeRenderer
- Haar-Volumen: Offset-Werte über dem Kopf in HairRenderer
- Schulterbreite: shoulderSpan in CharacterLayout
- Waffen-Position: Offsets in AccessoryRenderer

**Step 3: CLAUDE.md aktualisieren**

In `src/Apps/RebornSaga/CLAUDE.md` den Rendering-System Abschnitt aktualisieren:

- Charakter-Rendering Abschnitt erweitern:
  - CharacterLayout struct erklären (RenderMode, Ankerpunkte)
  - 7 Renderer auflisten mit Verantwortlichkeit
  - Render-Reihenfolge dokumentieren
  - Neue CharacterDefinition Properties (HasBangs, BangStyle, AuraColor) dokumentieren

**Step 4: Build verifizieren**

Run: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
Expected: 0 Fehler

**Step 5: Commit**

```bash
git add -A src/Apps/RebornSaga/
git commit -m "RebornSaga: Charakter-Visual-Upgrade — visuelles Tuning + CLAUDE.md Update"
```

---

## Zusammenfassung

| Task | Beschreibung | Geschätzte Dateien |
|------|--------------|--------------------|
| 1 | CharacterLayout Ankerpunkt-System | 1 neue Datei |
| 2 | FaceRenderer (Kopf, Nase, Ohren, Mund) | 1 neue Datei |
| 3 | EyeRenderer (Anime-Augen, Brauen, Blinzeln) | 1 neue Datei |
| 4 | HairRenderer (4 Styles, Pony, Highlights) | 1 neue Datei |
| 5 | BodyRenderer + ClothingRenderer | 2 neue Dateien |
| 6 | AccessoryRenderer + CharacterEffects | 2 neue Dateien |
| 7 | Integration (CharacterParts + Definitions) | 3 geänderte Dateien |
| 8 | Visuelles Tuning + Dokumentation | Alle Renderer + CLAUDE.md |

**Gesamte neue Dateien:** 8
**Geänderte Dateien:** 4 (CharacterParts.cs, CharacterDefinitions.cs, CharacterRenderer.cs, CLAUDE.md)
