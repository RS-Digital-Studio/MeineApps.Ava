# Immersives Hintergrund-System — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ersetze monolithischen BackgroundRenderer durch ein datengetriebenes Kompositions-System mit 14 feingranularen Szenen, Multi-Layer-Rendering, Beleuchtung und Charakter-Integration.

**Architecture:** C#-Record-Definitionen (`SceneDef`) beschreiben Szenen deklarativ. 6 spezialisierte Layer-Renderer zeichnen die Komponenten. `BackgroundCompositor` orchestriert alles mit `RenderBack()`/`RenderFront()` Split um Charaktere herum.

**Tech Stack:** SkiaSharp (SKCanvas, SKPaint, SKShader, SKColorFilter, SKMaskFilter), C# records, .NET 10

**Base Path:** `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/`

---

### Task 1: SceneDef Datenmodell

**Files:**
- Create: `Rendering/Backgrounds/SceneDef.cs`

**Step 1: Records und Enums erstellen**

```csharp
namespace RebornSaga.Rendering.Backgrounds;

using SkiaSharp;

// --- Enums ---

public enum ElementType
{
    ConiferTree, DeciduousTree, Bush, DeadTree, Willow,
    House, Well, Fence,
    Rock, Boulder, Stump, Log,
    Pillar, Arch, BrokenWall,
    Bookshelf, Table, Barrel,
    Throne, Banner, SwordInGround,
    Railing, RuneCircle,
    GeometricFragment
}

public enum GroundType { Grass, Stone, Wood, Sand, Snow, Water }

public enum LightType { Ambient, PointLight }

public enum ParticleStyle
{
    Firefly, Spark, Dust, Leaf, Snowflake,
    MagicOrb, Ember, Star, ScanLine, GlitchLine,
    Smoke, RingOrbit
}

public enum ForegroundStyle { GrassBlade, Fog, Branch, Cobweb, LightRay }

// --- Records ---

/// <summary>Komplette Szenen-Definition aus der Komposition aller Layer.</summary>
public record SceneDef
{
    public required SkyDef Sky { get; init; }
    public ElementDef[] Elements { get; init; } = [];
    public GroundDef? Ground { get; init; }
    public LightDef[] Lights { get; init; } = [];
    public ParticleDef[] Particles { get; init; } = [];
    public ForegroundDef[] Foreground { get; init; } = [];
}

/// <summary>Himmel-Gradient (3 Farben vertikal).</summary>
public record SkyDef(SKColor Top, SKColor Mid, SKColor Bottom, float MidStop = 0.5f);

/// <summary>Silhouetten-Element im Mittelgrund (hinter Charakteren).</summary>
public record ElementDef(
    ElementType Type,
    int Count,
    SKColor Color,
    float MinHeight,
    float MaxHeight,
    float YBase = 1f,
    float Spacing = 0f
);

/// <summary>Boden-Band am unteren Rand.</summary>
public record GroundDef(GroundType Type, SKColor Color, SKColor? AccentColor = null, float Height = 0.15f);

/// <summary>Lichtquelle (Ambient oder Punkt).</summary>
public record LightDef(
    LightType Type,
    SKColor Color,
    float Intensity = 0.15f,
    float X = 0.5f,
    float Y = 0.3f,
    float Radius = 80f,
    bool Flickers = false
);

/// <summary>Atmosphaerische Partikel.</summary>
public record ParticleDef(ParticleStyle Style, int Count, SKColor Color, byte Alpha = 60);

/// <summary>Vordergrund-Element (ueber Charakteren, nur unterer Bereich).</summary>
public record ForegroundDef(ForegroundStyle Style, SKColor Color, byte Alpha = 30, float MaxY = 0.6f);
```

**Step 2: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```
Expected: 0 Fehler

---

### Task 2: SkyRenderer + GroundRenderer

**Files:**
- Create: `Rendering/Backgrounds/Layers/SkyRenderer.cs`
- Create: `Rendering/Backgrounds/Layers/GroundRenderer.cs`

**Step 1: SkyRenderer — Vertikaler 3-Farben-Gradient mit Caching**

```csharp
namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;

/// <summary>Zeichnet den Himmel-Gradient (gecacht pro SkyDef + Bounds).</summary>
public static class SkyRenderer
{
    private static readonly SKPaint _paint = new() { IsAntialias = true };
    private static SKShader? _cachedShader;
    private static SkyDef? _cachedDef;
    private static SKRect _cachedBounds;

    public static void Render(SKCanvas canvas, SKRect bounds, SkyDef sky)
    {
        if (_cachedShader == null || _cachedDef != sky ||
            _cachedBounds.Width != bounds.Width || _cachedBounds.Height != bounds.Height)
        {
            _cachedShader?.Dispose();
            _cachedShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, bounds.Top),
                new SKPoint(bounds.MidX, bounds.Bottom),
                new[] { sky.Top, sky.Mid, sky.Bottom },
                new[] { 0f, sky.MidStop, 1f },
                SKShaderTileMode.Clamp);
            _cachedDef = sky;
            _cachedBounds = bounds;
        }

        _paint.Shader = _cachedShader;
        canvas.DrawRect(bounds, _paint);
        _paint.Shader = null;
    }

    public static void Cleanup()
    {
        _cachedShader?.Dispose();
        _cachedShader = null;
        _paint.Dispose();
    }
}
```

**Step 2: GroundRenderer — Boden mit Textur-Andeutungen**

6 Bodentypen: Gras (Halme oben), Stone (Fugen-Linien), Wood (Planken), Sand (Punkte), Snow (blaue Schatten), Water (Wellen).

```csharp
namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;
using System;

/// <summary>Zeichnet den Boden am unteren Rand mit texturierten Details.</summary>
public static class GroundRenderer
{
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _detailPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPath _path = new();

    public static void Render(SKCanvas canvas, SKRect bounds, GroundDef ground)
    {
        var groundTop = bounds.Bottom - bounds.Height * ground.Height;
        var groundRect = new SKRect(bounds.Left, groundTop, bounds.Right, bounds.Bottom);

        // Basis-Fuellung
        _fillPaint.Color = ground.Color;
        canvas.DrawRect(groundRect, _fillPaint);

        // Oberkante: leichter Gradient-Uebergang (nicht harter Schnitt)
        var fadeH = bounds.Height * 0.03f;
        _fillPaint.Color = ground.Color.WithAlpha(0);
        using var fadeShader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.MidX, groundTop - fadeH),
            new SKPoint(bounds.MidX, groundTop + fadeH),
            new[] { ground.Color.WithAlpha(0), ground.Color },
            SKShaderTileMode.Clamp);
        _fillPaint.Shader = fadeShader;
        canvas.DrawRect(bounds.Left, groundTop - fadeH, bounds.Width, fadeH * 2, _fillPaint);
        _fillPaint.Shader = null;

        // Detail-Textur je nach Typ
        var accent = ground.AccentColor ?? DarkenColor(ground.Color, 0.7f);
        _detailPaint.Color = accent.WithAlpha(60);
        _detailPaint.StrokeWidth = 1f;

        switch (ground.Type)
        {
            case GroundType.Grass:
                // Grashalme an der Oberkante
                for (float x = bounds.Left; x < bounds.Right; x += 8f)
                {
                    var h = 4f + MathF.Sin(x * 0.3f) * 3f;
                    canvas.DrawLine(x, groundTop, x + 2f, groundTop - h, _detailPaint);
                }
                break;

            case GroundType.Stone:
                // Horizontale + vertikale Fugen
                for (float y = groundTop + 12f; y < bounds.Bottom; y += 18f)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, _detailPaint);
                for (float x = bounds.Left + 30f; x < bounds.Right; x += 60f)
                {
                    var row = (int)((x - bounds.Left) / 60f);
                    var yOff = row % 2 == 0 ? 0f : 9f;
                    canvas.DrawLine(x, groundTop + yOff, x, groundTop + yOff + 18f, _detailPaint);
                }
                break;

            case GroundType.Wood:
                // Planken-Linien
                for (float y = groundTop + 8f; y < bounds.Bottom; y += 14f)
                    canvas.DrawLine(bounds.Left, y, bounds.Right, y, _detailPaint);
                break;

            case GroundType.Sand:
                // Subtile Punkte
                _fillPaint.Color = accent.WithAlpha(30);
                for (float x = bounds.Left + 5f; x < bounds.Right; x += 12f)
                    for (float y = groundTop + 5f; y < bounds.Bottom; y += 10f)
                        canvas.DrawCircle(x + MathF.Sin(y) * 3f, y, 1f, _fillPaint);
                break;

            case GroundType.Snow:
                // Blaue Schatten-Flecken
                _fillPaint.Color = new SKColor(0x80, 0x90, 0xC0, 20);
                for (int i = 0; i < 6; i++)
                {
                    var sx = bounds.Left + bounds.Width * (i * 0.18f + 0.02f);
                    canvas.DrawOval(sx, groundTop + bounds.Height * ground.Height * 0.5f,
                        bounds.Width * 0.08f, bounds.Height * ground.Height * 0.3f, _fillPaint);
                }
                break;

            case GroundType.Water:
                // Wellenlinien
                _detailPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 25);
                _detailPaint.StrokeWidth = 1.5f;
                _path.Rewind();
                _path.MoveTo(bounds.Left, groundTop + 3f);
                for (float x = bounds.Left; x < bounds.Right; x += 20f)
                    _path.QuadTo(x + 10f, groundTop, x + 20f, groundTop + 3f);
                canvas.DrawPath(_path, _detailPaint);
                break;
        }
    }

    public static void Cleanup()
    {
        _fillPaint.Dispose();
        _detailPaint.Dispose();
        _path.Dispose();
    }

    private static SKColor DarkenColor(SKColor c, float f) => new(
        (byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f), c.Alpha);
}
```

**Step 3: Build verifizieren**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 4: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/
git commit -m "feat(RebornSaga): SceneDef Datenmodell + SkyRenderer + GroundRenderer"
```

---

### Task 3: ElementRenderer — Silhouetten

**Files:**
- Create: `Rendering/Backgrounds/Layers/ElementRenderer.cs`

Zeichnet Silhouetten-Elemente im Mittelgrund (hinter Charakteren). Jeder `ElementType` hat eine eigene Draw-Methode. Elemente werden gleichmaessig verteilt mit leichter Groessen-Variation.

**Zentrale Render-Methode:**
```csharp
public static void Render(SKCanvas canvas, SKRect bounds, ElementDef[] elements)
{
    foreach (var elem in elements)
    {
        for (int i = 0; i < elem.Count; i++)
        {
            var x = bounds.Left + bounds.Width * (elem.Spacing > 0
                ? elem.Spacing * i + elem.Spacing * 0.5f
                : (i + 0.5f) / elem.Count);
            var h = bounds.Height * (elem.MinHeight +
                (elem.MaxHeight - elem.MinHeight) * (0.5f + MathF.Sin(i * 1.7f) * 0.5f));
            var baseY = bounds.Top + bounds.Height * elem.YBase;
            _fillPaint.Color = elem.Color;
            DrawElement(canvas, elem.Type, x, baseY, h, bounds.Width / elem.Count * 0.6f);
        }
    }
}
```

**Element-Typen implementieren (je 10-20 Zeilen):**
- `ConiferTree`: Dreieck-Stapel (2 uebereinander) + Stamm-Rechteck
- `DeciduousTree`: Kreis-Krone + Stamm-Rechteck
- `Bush`: Flaches Oval, breiter als hoch
- `DeadTree`: Dünner Stamm + 2-3 seitliche Äste (Linien)
- `Willow`: Krone + haengende Linien nach unten
- `House`: Rechteck + Dreieck-Dach + 1-2 Fenster (AccentColor)
- `Well`: Kurzer Zylinder + Dach-Dreieck
- `Fence`: Vertikale Linien + horizontale Verbindung
- `Rock`: Unregelmäßiges Polygon (5 Punkte)
- `Boulder`: Grosses Oval + Outline
- `Stump`: Breites niedriges Rechteck + Oval oben
- `Log`: Horizontales Oval mit Linie (Rinde)
- `Pillar`: Hoher schmaler Rect + Kapitell (breiteres Rect oben)
- `Arch`: 2 Pillar + Halbkreis oben
- `BrokenWall`: Rechteck mit gezackter Oberkante
- `Bookshelf`: Hoher Rect + 4-5 horizontale Linien (Regalboeden)
- `Table`: Horizontaler Rect + 2 Beine
- `Barrel`: Oval + 2 horizontale Linien
- `Throne`: Hohe Rueckenlehne + Sitzflaeche + 2 Armlehnen
- `Banner`: Vertikaler Rect + dreieckiger unterer Abschluss, AccentColor-Streifen
- `SwordInGround`: Schmales Dreieck (Klinge) + Querstange + Griff
- `Railing`: Wie Fence, aber eleganter (duennere Linien)
- `RuneCircle`: Kreis + 4 innere Segmente
- `GeometricFragment`: Zufaelliges Dreieck, leicht rotiert

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/Layers/ElementRenderer.cs
git commit -m "feat(RebornSaga): ElementRenderer mit 24 Silhouetten-Typen"
```

---

### Task 4: LightingRenderer

**Files:**
- Create: `Rendering/Backgrounds/Layers/LightingRenderer.cs`

Zwei Mechanismen:
1. **Ambient**: `SaveLayer()` + `SKColorFilter.CreateColorMatrix()` — toent alles (Charaktere + Vordergrund) einheitlich
2. **PointLight**: Additive radiale Gradienten — lokale Lichtquellen (Fackeln, Feuer)

```csharp
namespace RebornSaga.Rendering.Backgrounds.Layers;

using SkiaSharp;
using System;

/// <summary>
/// Beleuchtungs-System: Ambient-Toenung (ColorFilter) und Punkt-Lichtquellen (radiale Gradienten).
/// Ambient wird als SaveLayer ueber Charaktere + Foreground gelegt.
/// </summary>
public static class LightingRenderer
{
    private static readonly SKPaint _lightPaint = new() { IsAntialias = true };
    private static readonly SKPaint _ambientPaint = new();

    // Gecachter Ambient-Filter
    private static SKColorFilter? _cachedAmbientFilter;
    private static SKColor _cachedAmbientColor;
    private static float _cachedAmbientIntensity;

    /// <summary>
    /// Beginnt den Ambient-Light-Layer. Alles zwischen BeginAmbient/EndAmbient wird getoent.
    /// Rufe dies VOR Charakteren auf.
    /// </summary>
    public static void BeginAmbient(SKCanvas canvas, LightDef[] lights)
    {
        // Finde erstes Ambient-Licht
        LightDef? ambient = null;
        foreach (var l in lights)
            if (l.Type == LightType.Ambient) { ambient = l; break; }

        if (ambient == null) return;

        // ColorFilter cachen
        if (_cachedAmbientFilter == null || _cachedAmbientColor != ambient.Color ||
            _cachedAmbientIntensity != ambient.Intensity)
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
    }

    /// <summary>Beendet den Ambient-Light-Layer.</summary>
    public static void EndAmbient(SKCanvas canvas, LightDef[] lights)
    {
        // Nur Restore wenn BeginAmbient ein SaveLayer gemacht hat
        foreach (var l in lights)
            if (l.Type == LightType.Ambient) { canvas.Restore(); return; }
    }

    /// <summary>
    /// Zeichnet Punkt-Lichtquellen (additive radiale Gradienten).
    /// Kann vor ODER nach Charakteren gerufen werden (additiv).
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

            using var shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), radius,
                new[] { light.Color.WithAlpha(alpha), SKColors.Transparent },
                SKShaderTileMode.Clamp);
            _lightPaint.Shader = shader;
            canvas.DrawRect(bounds, _lightPaint);
            _lightPaint.Shader = null;
        }
    }

    public static void Cleanup()
    {
        _cachedAmbientFilter?.Dispose();
        _lightPaint.Dispose();
        _ambientPaint.Dispose();
    }
}
```

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/Layers/LightingRenderer.cs
git commit -m "feat(RebornSaga): LightingRenderer mit Ambient-Toenung und Punkt-Licht"
```

---

### Task 5: SceneParticleRenderer

**Files:**
- Create: `Rendering/Backgrounds/Layers/SceneParticleRenderer.cs`

Leichtgewichtiger Partikel-Renderer fuer atmosphaerische Effekte. Kein vollstaendiges Partikel-System — stattdessen deterministische Animation basierend auf `time` und Index (kein State, kein Array).

**Partikel-Typen:**
- `Firefly`: Kleine leuchtende Punkte, langsame Sinus-Drift, pulsierender Alpha
- `Spark`: Aufwaerts, schnell, verblasst, orange
- `Dust`: Sehr langsam schwebend, kaum sichtbar
- `Leaf`: Faellt diagonal, leichtes Pendeln
- `Snowflake`: Faellt langsam, seitlicher Drift
- `MagicOrb`: Kreisfoermige Orbit-Bewegung, leuchtend
- `Ember`: Wie Spark aber langsamer, groesser
- `Star`: Statisch, Twinkle-Alpha
- `ScanLine`: Horizontale Linien (fuer SystemVoid)
- `GlitchLine`: Zufaellige horizontale Segmente (Dreamworld)
- `Smoke`: Langsam aufsteigend, grosse Kreise, niedrig-Alpha
- `RingOrbit`: Kreise die um Zentrum rotieren (Title)

Jeder Partikeltyp: ~15-20 Zeilen. Position berechnet aus `time * speed + index * phase_offset`. Kein Heap-State.

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/Layers/SceneParticleRenderer.cs
git commit -m "feat(RebornSaga): SceneParticleRenderer mit 12 Partikel-Typen"
```

---

### Task 6: ForegroundRenderer

**Files:**
- Create: `Rendering/Backgrounds/Layers/ForegroundRenderer.cs`

Zeichnet Elemente UEBER den Charakteren, aber nur im unteren Bereich (Safezone). Max Alpha 40%.

**Typen:**
- `GrassBlade`: Hohe Grashalme vom unteren Rand aufwaerts, leichtes Wind-Sway
- `Fog`: Halbtransparenter Gradient-Streifen, langsam wandernd
- `Branch`: Blaetter/Aeste von den oberen Ecken (nur am Rand, nie mittig)
- `Cobweb`: Feine Linien in Ecken (Dungeon)
- `LightRay`: Diagonale Lichtstraehnen von oben (durch Blaetterdach / Fenster)

Jeder Typ achtet auf `MaxY` — nichts wird oberhalb dieser Y-Position gezeichnet.

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/Layers/ForegroundRenderer.cs
git commit -m "feat(RebornSaga): ForegroundRenderer mit Safezone-System"
```

---

### Task 7: BackgroundCompositor

**Files:**
- Create: `Rendering/Backgrounds/BackgroundCompositor.cs`

Orchestrator der alle Layer-Renderer koordiniert. Haelt ein `Dictionary<string, SceneDef>` fuer backgroundKey-Lookup.

```csharp
namespace RebornSaga.Rendering.Backgrounds;

using RebornSaga.Rendering.Backgrounds.Layers;
using SkiaSharp;

/// <summary>
/// Orchestriert das Multi-Layer Hintergrund-Rendering.
/// RenderBack() vor Charakteren, RenderFront() nach Charakteren.
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
    /// Beginnt Ambient-Licht-Toenung. Zwischen BeginLighting/EndLighting
    /// werden Charaktere gerendert → sie werden mit getoent.
    /// </summary>
    public static void BeginLighting(SKCanvas canvas)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;
        if (scene.Lights.Length > 0)
            LightingRenderer.BeginAmbient(canvas, scene.Lights);
    }

    /// <summary>Beendet Ambient-Licht-Toenung.</summary>
    public static void EndLighting(SKCanvas canvas)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;
        if (scene.Lights.Length > 0)
            LightingRenderer.EndAmbient(canvas, scene.Lights);
    }

    /// <summary>
    /// Zeichnet alles UEBER den Charakteren: Vordergrund, Partikel.
    /// </summary>
    public static void RenderFront(SKCanvas canvas, SKRect bounds, float time)
    {
        var scene = _currentScene ?? SceneDefinitions.Default;

        // 5. Vordergrund (Gras, Nebel, Aeste — ueber Charakteren)
        if (scene.Foreground.Length > 0)
            ForegroundRenderer.Render(canvas, bounds, scene.Foreground, time);

        // 6. Atmosphaerische Partikel (ganz oben)
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
```

**Render-Reihenfolge in Szenen:**
```
compositor.RenderBack(canvas, bounds, time);   // Sky + Elements + Ground + PointLights
compositor.BeginLighting(canvas);              // SaveLayer mit AmbientLight ColorFilter
  // ... Charaktere rendern ...
compositor.EndLighting(canvas);                // Restore (Ambient-Toenung angewendet)
compositor.RenderFront(canvas, bounds, time);  // Foreground + Particles
```

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/BackgroundCompositor.cs
git commit -m "feat(RebornSaga): BackgroundCompositor Orchestrator"
```

---

### Task 8: SceneDefinitions (14 Szenen)

**Files:**
- Create: `Rendering/Backgrounds/SceneDefinitions.cs`

Alle 14 Szenen als statische `SceneDef`-Instanzen. `Get(backgroundKey)` liefert die passende Definition.

Jede Szene hat eine individuelle Farbpalette, passende Elemente, Partikel und Beleuchtung:

| Szene | Sky-Palette | Elemente | Ground | Licht | Partikel | Foreground |
|-------|-------------|----------|--------|-------|----------|------------|
| SystemVoid | Schwarz | — | — | Blau ambient (0.1) | ScanLine | — |
| Title | Dunkelblau-Schwarz | — | — | Dunkel ambient | RingOrbit, Star | — |
| ForestDay | Gruen-Dunkelgruen | ConiferTree(7), Bush(4) | Grass | Warm ambient, PointLight(Sonne, oben-rechts) | Leaf(8), Dust(5) | GrassBlade, LightRay |
| ForestNight | Dunkelblau-Schwarz | ConiferTree(7), Bush(3) | Grass (dunkel) | Blau ambient (0.2) | Firefly(15) | Fog |
| Campfire | Dunkelorange-Schwarz | Log(3), Rock(4), DeadTree(2) | Grass (dunkel) | Orange PointLight(Mitte-unten, flackert), Dunkel ambient | Spark(12), Ember(6) | Fog |
| VillageSquare | Abendhimmel Orange-Lila | House(4), Well(1), Fence(2) | Stone | Warm ambient (0.15) | Smoke(3), Dust(4) | — |
| VillageTavern | Braun-Dunkelbraun | Barrel(3), Table(2), Bookshelf(1) | Wood | PointLight(Kerzen, 3x, flackert), Warm ambient | Dust(8) | — |
| DungeonHalls | Grau-Schwarz | Pillar(4), BrokenWall(2) | Stone (dunkel) | PointLight(Fackeln, 2x, flackert), Dunkel ambient | Dust(6) | Cobweb |
| DungeonBoss | Rot-Schwarz | Pillar(6), Arch(1), BrokenWall(3) | Stone (rot-getönt) | Rot ambient (0.2), PointLight(rot, Mitte) | Ember(10), MagicOrb(4) | Fog (rot) |
| TowerLibrary | Lila-Dunkelblau | Bookshelf(4), Table(2) | Wood | PointLight(Kerzen, 2x), Lila ambient (0.1) | MagicOrb(6), Dust(4) | LightRay |
| TowerSummit | Nacht-Lila-Schwarz | Railing(3), RuneCircle(2) | Stone | Lila ambient (0.15) | Star(20), MagicOrb(5) | — |
| Battlefield | Rot-Orange-Schwarz | SwordInGround(5), Banner(2), BrokenWall(3) | Sand (dunkel) | Rot-Orange ambient (0.2) | Ember(8), Smoke(4) | Fog (rot-orange) |
| CastleHall | Gold-Dunkelbraun | Pillar(6), Banner(4), Throne(1) | Stone (poliert) | PointLight(Fackeln, 3x), Warm ambient | Dust(5) | LightRay |
| Dreamworld | HSL-Animation (Spezial) | GeometricFragment(8) | — | Wechselnder ambient | GlitchLine(10), MagicOrb(5) | — |

**Hinweis Dreamworld:** Sky ist ein Spezialfall — die Farben aendern sich per `time`. Kann als spezieller SkyDef mit Flag oder als Override in BackgroundCompositor gehandhabt werden.

**Dictionary-Mapping:**
```csharp
private static readonly Dictionary<string, SceneDef> _scenes = new(StringComparer.OrdinalIgnoreCase)
{
    ["systemVoid"] = SystemVoid,
    ["title"] = Title,
    ["forest"] = ForestDay,         // Rueckwaertskompatibel
    ["forestDay"] = ForestDay,
    ["forestNight"] = ForestNight,
    ["campfire"] = Campfire,        // Fehlender Key endlich gemappt!
    ["village"] = VillageSquare,    // Rueckwaertskompatibel
    ["villageSquare"] = VillageSquare,
    ["villageTavern"] = VillageTavern,
    ["dungeon"] = DungeonHalls,     // Rueckwaertskompatibel
    ["dungeonHalls"] = DungeonHalls,
    ["dungeonBoss"] = DungeonBoss,
    ["tower"] = TowerLibrary,       // Rueckwaertskompatibel
    ["towerLibrary"] = TowerLibrary,
    ["towerSummit"] = TowerSummit,
    ["battlefield"] = Battlefield,
    ["castle"] = CastleHall,
    ["castleHall"] = CastleHall,
    ["dreamworld"] = Dreamworld,
};

public static SceneDef Get(string key) =>
    _scenes.TryGetValue(key, out var def) ? def : Default;

public static SceneDef Default => ForestDay;
```

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Backgrounds/SceneDefinitions.cs
git commit -m "feat(RebornSaga): 14 SceneDefinitions mit Rueckwaertskompatibilitaet"
```

---

### Task 9: Szenen-Integration

**Files:**
- Modify: `Scenes/DialogueScene.cs`
- Modify: `Scenes/BattleScene.cs`
- Modify: `Scenes/ClassSelectScene.cs`
- Modify: `Scenes/TitleScene.cs`
- Modify: `Scenes/SaveSlotScene.cs`

**DialogueScene (Kern-Aenderung):**

```csharp
// Vorher:
private SceneBackground _background = SceneBackground.Village;
public void SetBackground(SceneBackground bg) => _background = bg;

// Nachher:
private string _backgroundKey = "village";
public void SetBackground(string key) { _backgroundKey = key; BackgroundCompositor.SetScene(key); }

// In PresentCurrentNode():
// Vorher: if (Enum.TryParse<SceneBackground>(node.BackgroundKey, true, out var bg)) SetBackground(bg);
// Nachher: if (!string.IsNullOrEmpty(node.BackgroundKey)) SetBackground(node.BackgroundKey);

// In Render():
// Vorher:
BackgroundRenderer.Render(canvas, bounds, _background, _time);
foreach (var speaker in _activeSpeakers)
    CharacterRenderer.DrawPortrait(...);

// Nachher:
BackgroundCompositor.RenderBack(canvas, bounds, _time);
BackgroundCompositor.BeginLighting(canvas);
foreach (var speaker in _activeSpeakers)
    CharacterRenderer.DrawPortrait(...);
BackgroundCompositor.EndLighting(canvas);
BackgroundCompositor.RenderFront(canvas, bounds, _time);
```

**BattleScene:** Gleicher Split-Pattern. `BackgroundCompositor.SetScene("battlefield")`.

**ClassSelectScene + SaveSlotScene + TitleScene:** Kein Split noetig (keine Charakter-Integration). Einfach `BackgroundCompositor.SetScene()` + `BackgroundCompositor.RenderBack()` verwenden.

**SceneBackground Enum + alter BackgroundRenderer:** Koennen entfernt werden nachdem alle Szenen migriert sind. Oder als Deprecated markieren.

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add src/Apps/RebornSaga/RebornSaga.Shared/Scenes/
git commit -m "feat(RebornSaga): Szenen auf BackgroundCompositor migriert"
```

---

### Task 10: Cleanup + CLAUDE.md

**Files:**
- Delete or deprecate: `Rendering/Backgrounds/BackgroundRenderer.cs` (alter monolithischer Renderer)
- Modify: `src/Apps/RebornSaga/CLAUDE.md` — Rendering-System Sektion aktualisieren

**CLAUDE.md Aenderungen:**
- Hintergruende-Sektion: BackgroundCompositor + Layer-System dokumentieren
- 14 Szenen-Tabelle
- Split-Rendering Pattern (RenderBack/BeginLighting/EndLighting/RenderFront)
- SceneDefinitions Dictionary-Mapping erwaehnen

**Step: Build + Commit**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
git add -A
git commit -m "feat(RebornSaga): Immersives Hintergrund-System komplett, alte BackgroundRenderer entfernt"
```
