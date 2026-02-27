# Isometrische Weltkarte - Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Dashboard (Tab 0) in HandwerkerImperium durch eine isometrische 2.5D-Weltkarte ersetzen, die als zentraler Hub dient.

**Architecture:** Neuer Unterordner `Graphics/IsometricWorld/` mit 7 Renderer-Klassen. Orchestrator (`IsometricWorldRenderer`) ruft Grid-Helper, Terrain, Buildings, Partikel, Wetter und Radial-Menü in korrekter Reihenfolge auf. Neue `IsometricWorldView` ersetzt `DashboardView` in Tab 0. Bestehende Klassen (EasingFunctions, GameJuiceEngine, CityWeatherSystem, CityProgressionHelper, CraftTextures, Shader-Effekte) werden wiederverwendet.

**Tech Stack:** Avalonia 11.3.11, SkiaSharp 3.119.2, CommunityToolkit.Mvvm 8.4.0, .NET 10

**Design-Dokument:** `docs/plans/2026-02-27-isometric-world-map-design.md`

**Basis-Pfad:** `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/`

---

## Task 1: IsoGridHelper - Isometrische Mathematik

**Files:**
- Create: `Graphics/IsometricWorld/IsoGridHelper.cs`

Das Fundament: Iso-zu-Screen und Screen-zu-Iso Konvertierung, HitTest, Sortierung.

**Step 1: Klasse anlegen**

```csharp
using SkiaSharp;

namespace HandwerkerImperium.Graphics.IsometricWorld;

/// <summary>
/// Isometrische Mathematik: Grid-Koordinaten <-> Screen-Koordinaten.
/// Diamond-Grid mit 2:1 Verhältnis (Standard-Isometrie).
/// </summary>
public static class IsoGridHelper
{
    public const float TileWidth = 96f;
    public const float TileHeight = 48f;
    public const int GridCols = 8;
    public const int GridRows = 8;

    /// <summary>
    /// Konvertiert Grid-Position (col, row) zu Screen-Position (Pixel).
    /// Mittelpunkt der Raute.
    /// </summary>
    public static SKPoint IsoToScreen(int col, int row)
    {
        float x = (col - row) * TileWidth / 2f;
        float y = (col + row) * TileHeight / 2f;
        return new SKPoint(x, y);
    }

    /// <summary>
    /// Konvertiert Screen-Position (Pixel) zu Grid-Position (col, row).
    /// Berücksichtigt Camera-Offset und Zoom.
    /// </summary>
    public static (int col, int row) ScreenToIso(float screenX, float screenY)
    {
        float col = screenX / TileWidth + screenY / TileHeight;
        float row = screenY / TileHeight - screenX / TileWidth;
        return ((int)MathF.Round(col), (int)MathF.Round(row));
    }

    /// <summary>
    /// Gibt die 4 Eckpunkte einer isometrischen Rauten-Kachel zurück.
    /// Für Boden-Rendering und Polygon-HitTest.
    /// </summary>
    public static SKPoint[] GetTileDiamond(int col, int row)
    {
        var center = IsoToScreen(col, row);
        float hw = TileWidth / 2f;
        float hh = TileHeight / 2f;
        return
        [
            new SKPoint(center.X, center.Y - hh),       // Oben
            new SKPoint(center.X + hw, center.Y),        // Rechts
            new SKPoint(center.X, center.Y + hh),        // Unten
            new SKPoint(center.X - hw, center.Y),        // Links
        ];
    }

    /// <summary>
    /// Prüft ob ein Screen-Punkt innerhalb einer isometrischen Raute liegt.
    /// Verwendet Kreuzprodukt-Methode für konvexes Polygon.
    /// </summary>
    public static bool IsPointInTile(float px, float py, int col, int row)
    {
        var diamond = GetTileDiamond(col, row);
        return IsPointInConvexPolygon(px, py, diamond);
    }

    /// <summary>
    /// Sortierindex für Painter's Algorithm: Gebäude hinten zuerst rendern.
    /// </summary>
    public static int GetDrawOrder(int col, int row) => col + row;

    /// <summary>
    /// Berechnet die Gesamt-Bounds der Weltkarte in Screen-Koordinaten.
    /// </summary>
    public static SKRect GetWorldBounds()
    {
        // Äußerste Punkte des 8x8 Grids berechnen
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int c = 0; c < GridCols; c++)
        {
            for (int r = 0; r < GridRows; r++)
            {
                var pts = GetTileDiamond(c, r);
                foreach (var p in pts)
                {
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
            }
        }

        // Padding für Gebäude-Höhe (max 120dp nach oben)
        return new SKRect(minX - 20, minY - 140, maxX + 20, maxY + 20);
    }

    private static bool IsPointInConvexPolygon(float px, float py, SKPoint[] polygon)
    {
        bool positive = false, negative = false;
        for (int i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            float cross = (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);
            if (cross > 0) positive = true;
            if (cross < 0) negative = true;
            if (positive && negative) return false;
        }
        return true;
    }
}
```

**Step 2: Build prüfen**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
```

**Step 3: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/IsometricWorld/IsoGridHelper.cs
git commit -m "feat(HandwerkerImperium): IsoGridHelper - isometrische Grid-Mathematik"
```

---

## Task 2: IsoCameraSystem - Pan, Zoom, Smooth-Follow

**Files:**
- Create: `Graphics/IsometricWorld/IsoCameraSystem.cs`

**Step 1: Klasse anlegen**

```csharp
using SkiaSharp;

namespace HandwerkerImperium.Graphics.IsometricWorld;

/// <summary>
/// Kamera-System für die isometrische Weltkarte.
/// Unterstützt Pan (1-Finger-Drag), Pinch-Zoom, Smooth-Follow und Bounds-Clamping.
/// </summary>
public class IsoCameraSystem
{
    // Aktuelle Position
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public float Zoom { get; private set; } = 1.0f;

    // Smooth-Follow Ziel
    private float _targetOffsetX, _targetOffsetY;
    private float _targetZoom = 1.0f;
    private bool _isFollowing;

    // Inertia (nach Finger-Loslassen)
    private float _velocityX, _velocityY;
    private const float Friction = 0.92f;
    private const float MinVelocity = 0.5f;

    // Zoom-Limits
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 2.0f;

    // Welt-Bounds (für Clamping)
    private SKRect _worldBounds;
    private SKRect _viewBounds;

    public IsoCameraSystem()
    {
        _worldBounds = IsoGridHelper.GetWorldBounds();
    }

    /// <summary>
    /// Pan um delta Pixel (1-Finger-Drag). Setzt Inertia-Geschwindigkeit.
    /// </summary>
    public void Pan(float dx, float dy)
    {
        _isFollowing = false;
        OffsetX += dx / Zoom;
        OffsetY += dy / Zoom;
        _velocityX = dx / Zoom;
        _velocityY = dy / Zoom;
        ClampToBounds();
    }

    /// <summary>
    /// Zoom um Faktor (Pinch-Geste). Zoomt Richtung Pinch-Mittelpunkt.
    /// </summary>
    public void PinchZoom(float scaleDelta, float focusX, float focusY)
    {
        _isFollowing = false;
        float oldZoom = Zoom;
        _targetZoom = Math.Clamp(_targetZoom * scaleDelta, MinZoom, MaxZoom);

        // Zoom Richtung Fokuspunkt
        float zoomChange = _targetZoom / oldZoom;
        _targetOffsetX = focusX - (focusX - OffsetX) * zoomChange;
        _targetOffsetY = focusY - (focusY - OffsetY) * zoomChange;
    }

    /// <summary>
    /// Smooth-Scroll zu einem bestimmten Gebäude auf dem Grid.
    /// </summary>
    public void FocusOnBuilding(int col, int row, SKRect viewBounds)
    {
        var screenPos = IsoGridHelper.IsoToScreen(col, row);
        _targetOffsetX = viewBounds.MidX - screenPos.X * Zoom;
        _targetOffsetY = viewBounds.MidY - screenPos.Y * Zoom;
        _isFollowing = true;
        _velocityX = 0;
        _velocityY = 0;
    }

    /// <summary>
    /// Zentriert die Kamera auf die Mitte der Weltkarte.
    /// </summary>
    public void CenterOnWorld(SKRect viewBounds)
    {
        _viewBounds = viewBounds;
        var worldCenter = new SKPoint(_worldBounds.MidX, _worldBounds.MidY);
        OffsetX = viewBounds.MidX - worldCenter.X * Zoom;
        OffsetY = viewBounds.MidY - worldCenter.Y * Zoom;
        _targetOffsetX = OffsetX;
        _targetOffsetY = OffsetY;
    }

    /// <summary>
    /// Update pro Frame: Smooth-Interpolation + Inertia + Zoom.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Smooth-Zoom
        if (MathF.Abs(Zoom - _targetZoom) > 0.001f)
            Zoom = Zoom + (_targetZoom - Zoom) * Math.Min(deltaTime * 8f, 1f);
        else
            Zoom = _targetZoom;

        if (_isFollowing)
        {
            // Smooth-Follow mit EaseOutCubic-artiger Interpolation
            float t = Math.Min(deltaTime * 5f, 1f);
            OffsetX += (_targetOffsetX - OffsetX) * t;
            OffsetY += (_targetOffsetY - OffsetY) * t;

            if (MathF.Abs(OffsetX - _targetOffsetX) < 0.5f &&
                MathF.Abs(OffsetY - _targetOffsetY) < 0.5f)
            {
                OffsetX = _targetOffsetX;
                OffsetY = _targetOffsetY;
                _isFollowing = false;
            }
        }
        else
        {
            // Inertia (nach Finger-Loslassen)
            if (MathF.Abs(_velocityX) > MinVelocity || MathF.Abs(_velocityY) > MinVelocity)
            {
                OffsetX += _velocityX * deltaTime * 10f;
                OffsetY += _velocityY * deltaTime * 10f;
                _velocityX *= Friction;
                _velocityY *= Friction;
            }
        }

        ClampToBounds();
    }

    /// <summary>
    /// Erzeugt die SKMatrix-Transformation für canvas.SetMatrix().
    /// </summary>
    public SKMatrix GetTransformMatrix()
    {
        return SKMatrix.CreateScaleTranslation(Zoom, Zoom, OffsetX, OffsetY);
    }

    /// <summary>
    /// Konvertiert Screen-Touch-Position zu Welt-Koordinaten (berücksichtigt Zoom+Offset).
    /// </summary>
    public SKPoint ScreenToWorld(float screenX, float screenY)
    {
        return new SKPoint(
            (screenX - OffsetX) / Zoom,
            (screenY - OffsetY) / Zoom);
    }

    /// <summary>
    /// Stoppt Inertia und Follow-Animation sofort.
    /// </summary>
    public void StopMotion()
    {
        _velocityX = 0;
        _velocityY = 0;
        _isFollowing = false;
    }

    private void ClampToBounds()
    {
        // Kamera darf nicht zu weit von der Welt weg scrollen
        float worldW = _worldBounds.Width * Zoom;
        float worldH = _worldBounds.Height * Zoom;
        float viewW = _viewBounds.Width;
        float viewH = _viewBounds.Height;

        if (viewW > 0 && viewH > 0)
        {
            float minX = viewW - worldW - _worldBounds.Left * Zoom;
            float maxX = -_worldBounds.Left * Zoom;
            float minY = viewH - worldH - _worldBounds.Top * Zoom;
            float maxY = -_worldBounds.Top * Zoom;

            if (worldW < viewW) { OffsetX = (viewW - worldW) / 2f - _worldBounds.Left * Zoom; }
            else { OffsetX = Math.Clamp(OffsetX, minX, maxX); }

            if (worldH < viewH) { OffsetY = (viewH - worldH) / 2f - _worldBounds.Top * Zoom; }
            else { OffsetY = Math.Clamp(OffsetY, minY, maxY); }
        }
    }
}
```

**Step 2: Build + Commit**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/IsometricWorld/IsoCameraSystem.cs
git commit -m "feat(HandwerkerImperium): IsoCameraSystem - Pan, Zoom, Smooth-Follow"
```

---

## Task 3: IsoTerrainRenderer - Boden-Kacheln, Wege, Dekoration

**Files:**
- Create: `Graphics/IsometricWorld/IsoTerrainRenderer.cs`

Rendert das isometrische Boden-Grid: Gras-Rauten mit Farbvariation, Wege zwischen Gebäuden, Dekorationen auf leeren Kacheln.

**Referenz-Dateien:**
- `Graphics/CityProgressionHelper.cs` - WorldTier-Logik, Dekorations-Typen, Farben
- `Graphics/CraftTextures.cs` - Holzmaserung, Grid-Raster

**Kern-Inhalt:**
- Enum `TileType { Grass, Path, Empty, Water, Locked }`
- `int[,] TileMap` (8x8) - wird von `IsometricWorldRenderer` mit Gebäude-Layout befüllt
- `DrawTile(canvas, col, row, tileType, worldTier, nightDim, time)` - Einzelne Raute
- `DrawDecorations(canvas, col, row, worldTier, nightDim, time)` - Bäume, Laternen, Blumen
- `DrawAllTiles(canvas, TileMap, worldTier, nightDim, time)` - Alle 64 Kacheln sortiert
- Gras: 4 Grüntöne per deterministischem Hash (`(col * 7919 + row * 137) % 4`)
- Weg: Braun/Grau je WorldTier (Feldweg→Asphalt→Pflaster→Premium)
- Static readonly SKPaint für jede Boden-Farbe (0 GC)
- Grashalm-Linien (3-5 pro Kachel, Sinus-Wind-Animation)

**Step 1: Implementierung** (vollständiger Code wird beim Ausführen geschrieben)

**Step 2: Build + Commit**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
git commit -m "feat(HandwerkerImperium): IsoTerrainRenderer - Boden-Grid mit Gras, Wegen, Dekoration"
```

---

## Task 4: IsoBuildingRenderer - 10 Workshop-Gebäude (HERZSTÜCK)

**Files:**
- Create: `Graphics/IsometricWorld/IsoBuildingRenderer.cs`

Das ist die größte und wichtigste Datei. Jeder Workshop-Typ bekommt eine eigene Draw-Methode mit 25+ Draw-Calls.

**Referenz-Dateien:**
- `Graphics/WorkshopSceneRenderer.cs` (~2000 Zeilen) - Detail-Qualität als Vorlage
- `Graphics/CityBuildingShapes.cs` (755 Zeilen) - Workshop-Farben, Fenster-Animation, Level-Rahmen
- `Graphics/CraftTextures.cs` - Holzmaserung

**Architektur:**

```
IsoBuildingRenderer
├── DrawBuilding(canvas, col, row, type, level, workerCount, nightDim, time)
│   ├── DrawGroundShadow()           // Weicher ovaler Schatten
│   ├── DrawBuildingBody()           // 3D-Körper mit Gradient+Textur
│   │   ├── DrawSideWall()           // 30% dunkler + Material-Textur
│   │   ├── DrawFrontWall()          // Gradient + Material-Textur
│   │   ├── DrawWindows()            // 2-4 Fenster mit Reflektion/Nacht-Glow
│   │   ├── DrawDoor()               // Rahmen + Griff
│   │   └── DrawFundament()          // Steingrau, 4dp
│   ├── DrawRoof()                   // Typ-spezifisch (Sattel/Flach/Kuppel/Giebel)
│   ├── DrawWorkshopIdentity()       // Typ-spezifisches Merkmal + Schild
│   ├── DrawSurroundings()           // Umgebungs-Objekte (Holzstapel, Werkzeug)
│   ├── DrawWorkers()                // 1-4 animierte Mini-Figuren
│   └── DrawLevelIndicator()         // Rahmen + Glow + Krone
├── DrawLockedSlot(canvas, col, row) // Silhouette + Schloss
└── DrawSupportBuilding()            // 7 Gebäude-Typen (Canteen, Storage, etc.)
```

**Kern-Prinzipien:**
- JEDE Wand bekommt einen vertikalen Gradient (hell→dunkel), KEINE Flat-Colors
- JEDE Wand bekommt Textur-Linien (Holz=horizontal, Stein=versetzt, Putz=fein)
- JEDES Gebäude hat min. 2 Fenster mit Rahmen + Glas-Reflektion
- JEDES Gebäude hat ein Workshop-spezifisches Erkennungsmerkmal
- JEDES Gebäude hat Umgebungs-Objekte die zum Handwerk passen
- Isometrischer 3D-Körper: Seitenversatz = width * 0.5f (NICHT 15%)
- Gebäude-Breite: 80dp in 96dp Kachel. Höhe: 60-120dp (level-abhängig)
- Static readonly SKPaint[] für alle Materialien (0 GC pro Frame)
- Farben aus CityBuildingShapes.GetWorkshopColor() übernehmen

**Aufgeteilt in Sub-Steps:**

### Step 4a: Basis-Struktur + generischer Iso-Körper

Klasse anlegen mit:
- Alle static readonly SKPaint Felder (Wand, Seite, Dach, Fenster, Tür, Text, Schatten)
- `DrawIsoBody(canvas, x, y, width, depth, height, frontColor, sideColor)` - Generischer 3D-Quader
- `DrawGroundShadow(canvas, x, y, width, depth)` - Ovaler Schatten
- `DrawFundament(canvas, x, y, width, depth)` - Steingrau Fundament
- `DrawWallTexture(canvas, path, MaterialType)` - Holz/Stein/Putz/Metall/Glas Linien
- `DrawWindow(canvas, x, y, w, h, nightDim, time)` - Einzelnes Fenster mit Reflektion
- `DrawDoor(canvas, x, y, w, h)` - Tür mit Rahmen
- `DrawLevelFrame(canvas, bounds, level)` - Bronze/Silber/Gold/Diamant Rahmen

### Step 4b: Carpenter (Holzscheune)

```
DrawCarpenterWorkshop(canvas, baseX, baseY, level, workerCount, nightDim, time)
- Satteldach mit roten Ziegel-Reihen + Giebel-Dreieck
- Holz-Maserung (CraftTextures.DrawWoodGrain()) auf Wänden
- Große offene Scheunentür (dunkles Innere sichtbar)
- 2 Fenster mit Kreuzrahmen
- Schornstein rechts mit Rauch-Partikel-Platzhalter
- Schild: Säge-Icon über Tür
- Umgebung: Holzstapel links (3 Bretter), Kreissäge rechts
- Sägemehl am Boden (beige Punkte)
```

### Step 4c: Plumber (Installations-Flachbau)

```
DrawPlumberWorkshop(...)
- Flachdach mit sichtbaren Rohren
- Putz-Textur auf Wänden (helles Blau/Türkis-Töne)
- Rohre an der Seitenwand (3 horizontale, verschiedene Durchmesser)
- Wasserturm auf Dach (kleiner Zylinder)
- Ventile + Absperrhähne als Details
- Umgebung: Rohrstapel, Werkzeugkasten
```

### Step 4d: Electrician (Industrie-Halle)

```
DrawElectricianWorkshop(...)
- Flachdach mit Kabelkanal
- Metall-Textur (vertikaler Gradient + Glanz-Highlight)
- Hochspannungsmast daneben (Gitter-Struktur)
- Blinkende LED am Eingang (Sinus-Puls)
- Warnschild (gelbes Dreieck mit Blitz)
- Umgebung: Kabeltrommel, Sicherungskasten an Wand
```

### Step 4e: Painter (Buntes Haus)

```
DrawPainterWorkshop(...)
- Walmdach (4 Schrägen)
- Farbkleckse an Fassade (3-4 zufällige Farbtupfer)
- Regenbogen-Streifen über der Tür
- Palette + Pinsel als Schild
- Umgebung: Farbeimer (3 in verschiedenen Farben), Leiter
```

### Step 4f: Roofer (Steiles Giebeldach)

```
DrawRooferWorkshop(...)
- Dominantes steiles Giebeldach (Ziegel-Reihen explizit sichtbar)
- Leiter an der Seite angelehnt
- Hammer + Nägel als Details
- Umgebung: Ziegelstapel, Dachlatten
```

### Step 4g: Contractor (Baustelle)

```
DrawContractorWorkshop(...)
- Rohbau-Optik (offene Mauerstellen, Gerüst)
- Kran daneben (Gitter-Arm, Haken, Seil)
- Bauhelm auf einem Pfosten
- Betonmischer-Silhouette
- Umgebung: Sandhaufen, Schaufel, Absperrband
```

### Step 4h: Architect (Moderner Glasbau)

```
DrawArchitectWorkshop(...)
- Teils Glas-Fassade (semi-transparente Panels mit Reflektion)
- Blaupause im großen Fenster sichtbar
- Saubere Linien, minimalistisch
- Umgebung: Zeichentisch mit Lampe, Modell-Haus
```

### Step 4i: GeneralContractor (Bürogebäude)

```
DrawGeneralContractorWorkshop(...)
- 2-stöckig (doppelte Höhe, Stockwerk-Linie)
- Gold-Fassade-Akzente (Shimmer)
- Elegantes dunkles Dach
- Fahne auf Dach (Wind-Animation)
- Umgebung: Luxus-Limousine (klein, schwarz), Blumenkübel
```

### Step 4j: MasterSmith (Steinerne Schmiede)

```
DrawMasterSmithWorkshop(...)
- Stein-Textur (Fugen-Linien, grob)
- Esse-Schornstein mit Feuer-Glow (SkiaFireEffect Platzhalter)
- Amboss vor der Tür (Metallglanz)
- Glutbecken (oranges Leuchten)
- Umgebung: Amboss, Werkzeug-Rack, Kohle-Haufen
```

### Step 4k: InnovationLab (Futuristischer Bau)

```
DrawInnovationLabWorkshop(...)
- Rund-Dach / Kuppel (Halbkreis-Bogen)
- Leuchtende Kuppel (SkiaGlowEffect, cyan)
- Antenne auf Dach
- Zahnrad-Logo an Fassade
- Umgebung: Satellit-Schüssel, Reaktor-Kern (klein, leuchtend)
```

### Step 4l: Support-Gebäude + Locked Slots

```
DrawSupportBuilding(canvas, col, row, BuildingType, level, nightDim, time)
- 7 Typen: Canteen, Storage, Office, Showroom, TrainingCenter, VehicleFleet, WorkshopExtension
- Einfachere Geometrie als Workshops (15-20 Draw-Calls)
- Eigene Farben und Erkennungsmerkmale pro Typ

DrawLockedSlot(canvas, col, row)
- Graue Silhouette eines generischen Gebäudes
- Schloss-Symbol (goldener Kreis + Bügel)
- "Lv. X" Text darunter
```

**Step 4m: Build + Commit**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
git commit -m "feat(HandwerkerImperium): IsoBuildingRenderer - 10 Workshop-Typen + 7 Support-Gebäude"
```

---

## Task 5: IsoParticleManager - Gebäude-spezifische Partikel

**Files:**
- Create: `Graphics/IsometricWorld/IsoParticleManager.cs`

**Referenz:** `Graphics/GameJuiceEngine.cs` (Struct-Pool-Pattern, 610 Zeilen)

**Kern-Inhalt:**
- `struct IsoParticle` (X, Y, VX, VY, Life, MaxLife, Size, Color, Type) - 0 GC
- `IsoParticle[300]` Array-Pool
- `SpawnWorkshopParticles(col, row, WorkshopType, time)` - Typ-spezifisch
- `Update(deltaTime)` - Physik (Schwerkraft, Wind, Fade)
- `Render(canvas)` - Alle aktiven Partikel zeichnen
- 10 Partikel-Typen: Sägemehl, Wasser, Funken, Farbe, Staub, Papier, Gold, Rauch, Elektro, Dampf
- Deterministische Spawn-Position relativ zum Gebäude (z.B. Schornstein, Esse)
- LCG-Random wie in GameJuiceEngine (kein `Random.Shared` im Render-Loop)

**Step 1: Implementierung**

**Step 2: Build + Commit**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
git commit -m "feat(HandwerkerImperium): IsoParticleManager - Struct-Pool Partikel für 10 Workshop-Typen"
```

---

## Task 6: IsoRadialMenu - Pie-Menü bei Gebäude-Tap

**Files:**
- Create: `Graphics/IsometricWorld/IsoRadialMenu.cs`

**Referenz:** `Graphics/EasingFunctions.cs` (EaseOutBack für Bounce-Animation)

**Kern-Inhalt:**
- `Open(float worldX, float worldY, WorkshopType, int workshopIndex)` - Menü öffnen
- `Close()` - Menü schließen (mit Fade-Out)
- `HitTest(float worldX, float worldY) → RadialMenuAction?` - Welcher Menüpunkt getroffen
- `Update(deltaTime)` - Animations-State
- `Render(canvas)` - 4 Kreise + Icons + Backdrop
- Enum `RadialMenuAction { Upgrade, Workers, MiniGame, Info }`
- 4 Menüpunkte auf Kreis (90°-Intervalle): Oben=Upgrade, Rechts=MiniGame, Unten=Info, Links=Workers
- Animation: Scale 0→1 mit EaseOutBack, staggered 50ms pro Punkt
- Halbtransparenter dunkler Kreis-Backdrop (Alpha 60%)
- Verbindungslinie vom Gebäude zum Menü-Zentrum
- SkiaSharp-Icons (Pfeil-hoch, Person, Spiel-Controller, Info-i) - wie WorkshopMiniIcons
- Radius: 60dp vom Zentrum, Menüpunkt-Kreise: 24dp Radius
- Static readonly SKPaint für Backdrop, Icons, Text

**Step 1: Implementierung**

**Step 2: Build + Commit**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
git commit -m "feat(HandwerkerImperium): IsoRadialMenu - Pie-Menü mit 4 Aktionen"
```

---

## Task 7: IsometricWorldRenderer - Orchestrator

**Files:**
- Create: `Graphics/IsometricWorld/IsometricWorldRenderer.cs`

Koordiniert alle Subsysteme in der richtigen Reihenfolge.

**Kern-Inhalt:**

```csharp
public class IsometricWorldRenderer
{
    // Subsysteme
    private readonly IsoCameraSystem _camera = new();
    private readonly IsoTerrainRenderer _terrain = new();
    private readonly IsoBuildingRenderer _buildings = new();
    private readonly IsoParticleManager _particles = new();
    private readonly IsoRadialMenu _radialMenu = new();
    private readonly CityWeatherSystem _weather = new();

    // Welt-Layout: Welches Gebäude steht auf welcher Kachel
    private readonly GridCell[,] _grid = new GridCell[8, 8];

    // HUD-Renderer (Geld, Level, Schrauben)
    private readonly OdometerRenderer _moneyOdometer = new();
    private readonly OdometerRenderer _screwsOdometer = new();

    public IsoCameraSystem Camera => _camera;
    public IsoRadialMenu RadialMenu => _radialMenu;

    /// <summary>
    /// Initialisiert das Grid-Layout basierend auf dem GameState.
    /// Wird einmal beim Start und nach jedem Workshop-Kauf aufgerufen.
    /// </summary>
    public void InitializeGrid(GameState state) { ... }

    /// <summary>
    /// Update pro Frame (20fps = 50ms Intervall).
    /// </summary>
    public void Update(float deltaTime, GameState state) { ... }

    /// <summary>
    /// Rendert die komplette isometrische Welt.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, GameState state, float time) { ... }

    /// <summary>
    /// Touch-HitTest: Prüft ob ein Gebäude oder Radial-Menü-Punkt getroffen wurde.
    /// </summary>
    public IsoTapResult HitTest(float screenX, float screenY) { ... }
}

public struct GridCell
{
    public GridCellType Type;      // Empty, Workshop, Building, Locked, Decoration
    public WorkshopType? Workshop;
    public BuildingType? Building;
    public int Level;
    public int WorkerCount;
}

public enum GridCellType { Empty, Workshop, Building, Locked, Decoration }

public struct IsoTapResult
{
    public IsoTapTarget Target;
    public WorkshopType? WorkshopType;
    public BuildingType? BuildingType;
    public int Index;
    public RadialMenuAction? MenuAction;
}

public enum IsoTapTarget { None, Workshop, Building, Locked, RadialMenu }
```

**Render-Reihenfolge:**
1. Himmel-Gradient + Tag/Nacht
2. `canvas.SetMatrix(_camera.GetTransformMatrix())`
3. `_terrain.DrawAllTiles()` - Boden
4. `_buildings.Draw()` für jede belegte Kachel, sortiert nach DrawOrder
5. `_particles.Render()` - Partikel
6. `_weather.Render()` - Wetter-Overlay
7. `canvas.ResetMatrix()` - Zurück zu Screen-Space
8. `_radialMenu.Render()` - UI-Overlay (Screen-Space)
9. HUD: Geld + Level + Schrauben (oben, Screen-Space)

**Step 1: Implementierung**

**Step 2: Build + Commit**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
git commit -m "feat(HandwerkerImperium): IsometricWorldRenderer - Orchestrator für alle Subsysteme"
```

---

## Task 8: IsometricWorldView - Neue View als Dashboard-Ersatz

**Files:**
- Create: `Views/IsometricWorldView.axaml`
- Create: `Views/IsometricWorldView.axaml.cs`
- Modify: `Views/MainView.axaml` (Zeile 78: DashboardView → IsometricWorldView)

**IsometricWorldView.axaml:**
- Grid mit einer SKCanvasView (Fullscreen) + FloatingTextOverlay darüber
- Kein ScrollViewer (Camera-System stattdessen)
- DataContext = MainViewModel (wie DashboardView)

**IsometricWorldView.axaml.cs:**
- 20fps DispatcherTimer (wie DashboardView.axaml.cs Zeile 460-467)
- `OnPaintSurface` → `_worldRenderer.Render()`
- Touch-Handling:
  - `PointerPressed` → Start Pan oder HitTest
  - `PointerMoved` → Pan (wenn Drag)
  - `PointerReleased` → Tap (wenn kein Drag) oder Pan-Ende
  - DPI-Skalierung (wie DashboardView.axaml.cs Zeile 287-293)
- GameJuiceEngine Integration (wie DashboardView.axaml.cs Zeile 96-98)
- FloatingText-Events verdrahten (wie DashboardView.axaml.cs Zeile 93-94)
- Radial-Menü-Aktionen → MainViewModel-Commands delegieren:
  - Upgrade → `_vm.UpgradeWorkshopCommand.Execute(workshopType)`
  - Workers → `_vm.NavigateToWorkerMarket(workshopType)`
  - MiniGame → `_vm.StartMiniGame(workshopType)`
  - Info → `_vm.NavigateToWorkshopDetail(workshopType)`

**MainView.axaml Änderung (Zeile 78):**
```xml
<!-- Vorher: -->
<views:DashboardView IsVisible="{Binding IsDashboardActive}" />
<!-- Nachher: -->
<views:IsometricWorldView IsVisible="{Binding IsDashboardActive}" />
```

DashboardView bleibt als Datei erhalten (Fallback), wird nur nicht mehr in MainView referenziert.

**Step 1: Implementierung**

**Step 2: Build + Commit**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
git commit -m "feat(HandwerkerImperium): IsometricWorldView als Dashboard-Ersatz (Tab 0)"
```

---

## Task 9: Integration + Feinschliff

**Files:**
- Modify: `ViewModels/MainViewModel.cs` - Neue Commands/Properties für Radial-Menü-Aktionen
- Modify: `Views/MainView.axaml.cs` - Event-Verdrahtung

**Kern-Punkte:**
- MainViewModel: `NavigateToWorkshopDetail(WorkshopType)`, `NavigateToWorkerMarket(WorkshopType)` als Commands
- FloatingText-Events auf die neue View umleiten
- GameJuiceEngine auf IsometricWorldView verfügbar machen
- Banner-Strip (Rush/Lieferant/Worker-Warnung) als HUD-Element auf der Weltkarte
- CityWeatherSystem.SetWeatherByMonth() beim ersten Render

**Step 1: Implementierung + Integration**

**Step 2: Gesamten Build prüfen**

```bash
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln
```

**Step 3: Commit**

```bash
git commit -m "feat(HandwerkerImperium): Isometrische Weltkarte Integration + Radial-Menü-Aktionen"
```

---

## Task 10: CLAUDE.md aktualisieren

**Files:**
- Modify: `src/Apps/HandwerkerImperium/CLAUDE.md`
- Modify: `F:\Meine_Apps_Ava\CLAUDE.md` (falls nötig)

Dokumentation der neuen IsometricWorld-Architektur, Dateistruktur, Renderer-Klassen.

---

## Ausführungsreihenfolge & Abhängigkeiten

```
Task 1: IsoGridHelper          (keine Abhängigkeit)
Task 2: IsoCameraSystem        (braucht: Task 1)
Task 3: IsoTerrainRenderer     (braucht: Task 1)
Task 4: IsoBuildingRenderer    (braucht: Task 1) ← GRÖSSTE AUFGABE
Task 5: IsoParticleManager     (braucht: Task 1)
Task 6: IsoRadialMenu          (keine Abhängigkeit)
Task 7: IsometricWorldRenderer (braucht: Task 1-6)
Task 8: IsometricWorldView     (braucht: Task 7)
Task 9: Integration            (braucht: Task 8)
Task 10: Dokumentation         (braucht: Task 9)
```

**Parallel möglich:** Tasks 2, 3, 4, 5, 6 können parallel nach Task 1 gestartet werden.

**Geschätzter Aufwand:** Task 4 (IsoBuildingRenderer) ist mit Abstand am größten (~2000-3000 Zeilen, 10 individuelle Workshop-Renderer + 7 Support-Gebäude + Locked Slots + Level-Evolution). Alle anderen Tasks sind 100-600 Zeilen.
