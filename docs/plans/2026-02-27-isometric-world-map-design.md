# Design: Isometrische Weltkarte für HandwerkerImperium

**Datum:** 2026-02-27
**Status:** Genehmigt
**Ziel:** Dashboard (Tab 0) durch eine isometrische 2.5D-Weltkarte ersetzen, die als zentraler Hub dient.

---

## 1. Architektur

### Dateistruktur

```
Graphics/IsometricWorld/
├── IsometricWorldRenderer.cs     Orchestrator (Grid, Camera, Draw-Loop)
├── IsoBuildingRenderer.cs        10 Workshop + 7 Gebäude als echte Iso-Shapes
├── IsoTerrainRenderer.cs         Rauten-Kacheln, Wege, Gras, Dekorationen
├── IsoParticleManager.cs         Gebäude-spezifische Partikel (Struct-Pool)
├── IsoRadialMenu.cs              Pie-Menü bei Tap auf Gebäude
├── IsoCameraSystem.cs            Pan, Pinch-Zoom, Bounds, Smooth-Follow
└── IsoGridHelper.cs              Iso<->Screen Mathe, HitTest, Sortierung
```

### Wiederverwendung bestehender Klassen

| Bestehend | Wiederverwendung |
|-----------|-----------------|
| EasingFunctions.cs | Alle Animationen (Radial-Menü, Camera-Smooth) |
| GameJuiceEngine.cs | RadialBurst bei Tap, CoinsFlyToWallet bei Collect |
| CityProgressionHelper | WorldTier-Logik (Straßen/Deko-Stufen) |
| CityWeatherSystem | Wetter-Overlay über der Weltkarte |
| CraftTextures.cs | Holzmaserung, Grid-Raster für Terrain |
| SkiaShimmerEffect / SkiaGlowEffect | Premium-Gebäude, Gold-Akzente |

### Render-Loop

```
IsometricWorldView.axaml -> SKCanvasView (FullScreen)
  20fps DispatcherTimer (50ms)
    1. IsoCameraSystem.Update(dt)
    2. IsoParticleManager.Update(dt)
    3. CityWeatherSystem.Update(dt)
    4. SKCanvasView.InvalidateSurface()
       -> IsometricWorldRenderer.Render(canvas, bounds, state)
            1. Himmel-Gradient + Tag/Nacht
            2. IsoTerrainRenderer.Draw()       (Boden zuerst)
            3. IsoBuildingRenderer.Draw()       (Gebäude hinten->vorn)
            4. IsoParticleManager.Render()      (Partikel über Gebäuden)
            5. CityWeatherSystem.Render()       (Wetter-Overlay)
            6. IsoRadialMenu.Render()           (UI-Overlay)
            7. HUD (Geld, Level, Schrauben)     (Fester HUD oben)
```

---

## 2. Isometrisches Grid-System

### Spezifikation

- **Grid:** 8x8 Diamond-Grid (erweiterbar auf 10x10 bei Progression)
- **Tile-Größe:** 96x48dp (2:1 Standard-Isometrie)
- **Sortierung:** Painter's Algorithm (aufsteigend nach col + row)

### Kern-Mathe (IsoGridHelper.cs)

```csharp
// Iso -> Screen
static SKPoint IsoToScreen(int col, int row, float tileW = 96f, float tileH = 48f)
{
    float x = (col - row) * tileW / 2f;
    float y = (col + row) * tileH / 2f;
    return new SKPoint(x, y);
}

// Screen -> Iso (Touch-HitTest)
static (int col, int row) ScreenToIso(float sx, float sy, float tileW = 96f, float tileH = 48f)
{
    float col = sx / tileW + sy / tileH;
    float row = sy / tileH - sx / tileW;
    return ((int)MathF.Round(col), (int)MathF.Round(row));
}
```

### Gebäude-Platzierung (fest, nicht frei platzierbar)

```
Zeile 0-1: Starter-Workshops (Carpenter, Plumber, Electrician)
Zeile 2-3: Mid-Game (Painter, Roofer, Contractor)
Zeile 4-5: Late-Game (Architect, GeneralContractor)
Zeile 6-7: Endgame (MasterSmith, InnovationLab)
+ 7 Support-Gebäude verteilt (Canteen, Storage, Office, etc.)
```

---

## 3. Building-Renderer (Herzstück)

### Isometrische 3D-Körper (NICHT Flat-Rectangles)

```
         ____
        /   /|     <- Dach (spezifisch pro Workshop-Typ)
       /___/ |
      |    | |     <- Vorderseite (Workshop-Farbe + Textur)
      |    | /     <- Seitenwand (30% dunkler)
      |____|/      <- Boden-Kante
```

### Proportionen

- **Breite:** 80dp (innerhalb 96dp Kachel, 8dp Rand)
- **Tiefe:** 40dp (halbe Breite, Standard-Iso)
- **Höhe:** 60-120dp (level-abhängig)
- **Seitenversatz:** width * 0.5f (NICHT 15% wie beim gescheiterten Versuch)

### Minimum 25 Draw-Calls pro Gebäude

```
1.  Schatten auf dem Boden (SKPath, Alpha 20%, Offset)
2.  Fundament (Steingrau, 4dp, leicht breiter)
3.  Seitenwand (Gradient dunkel->dunkler)
4.  Seitenwand-Textur (Ziegel/Holz/Putz je nach Typ)
5.  Vorderwand (Gradient hell->dunkel)
6.  Vorderwand-Textur (gleich wie Seite, hellere Variante)
7.  Fenster 1-4 (Rahmen + Glas-Reflektion + nachts leuchtend)
8.  Tür (Rahmen + Griff + Schatten)
9.  Dach-Grundform (Satteldach/Flach/Kuppel/Giebel)
10. Dach-Textur (Ziegel-Muster/Holz/Metall/Glas)
11. Dach-Details (Giebel, Schornstein, Kante)
12. Workshop-Merkmal (Schornstein/Rohre/Antenne/Kran)
13. Workshop-Werkzeug (Kreissäge/Amboss/Rohrzange)
14. Schild/Logo (Workshop-Icon + Name)
15. Umgebungs-Objekte links (Holzstapel/Rohre/etc.)
16. Umgebungs-Objekte rechts (Werkzeug/Material)
17. Boden-Details (Sägemehl/Farbflecken/Schrauben)
18. Weg zum Eingang (Schotter/Pflaster)
19. Animiertes Hauptelement (Rauch/Funken/Wasser/Licht)
20. Animiertes Nebenelement (Kran-Rotation/Tropfen)
21. Worker-Figur 1 (animiert, typ-spezifische Kleidung)
22. Worker-Figur 2-4 (bei mehr Workern)
23. Level-Rahmen (Bronze/Silber/Gold/Diamant ab Lv50)
24. Level-Glow/Aura (ab Lv500, SkiaGlowEffect)
25. Krone + Shimmer (ab Lv1000, SkiaShimmerEffect)
```

### 10 Workshop-Typen als erkennbare Gebäude

| Workshop | Gebäude-Form | Dach | Erkennung | Umgebung |
|----------|-------------|------|-----------|----------|
| Carpenter | Holzscheune, Satteldach | Rote Ziegel, Giebel | Kreissäge, offene Scheunentür | Holzstapel, Sägemehl |
| Plumber | Flachbau, Industriestil | Flachdach mit Rohren | Sichtbare Rohre an Wand | Wasserturm, Ventile |
| Electrician | Industriebau | Flachdach | Hochspannungsmast, LEDs | Kabeltrommel, Warnschild |
| Painter | Buntes Haus, Walmdach | Walmdach | Farbkleckse an Fassade | Farbeimer, Pinsel |
| Roofer | Steiles Giebeldach | Explizite Ziegel-Reihen | Dach dominant, Leiter | Ziegelstapel |
| Contractor | Rohbau/Baustelle | Teilweise offen | Kran, Gerüst | Betonmischer, Schaufel |
| Architect | Moderner Glasbau | Teils Glas | Blaupause im Fenster | Zeichentisch |
| GeneralContr. | Büro, 2 Stockwerke | Elegant, dunkel | Gold-Fassade, Fahne | Luxus-Limousine |
| MasterSmith | Steinerne Schmiede | Stein mit Esse | Feuer-Shader, Amboss | Amboss, Glutbecken |
| InnovationLab | Futuristisch, rund | Kuppel-Dach | Leuchtende Kuppel | Antenne, Zahnräder |

### Level-basierte Evolution

| Level | Visuelles Upgrade |
|-------|------------------|
| 1-49 | Basis-Gebäude (1 Stockwerk) |
| 50-99 | +Bronze-Rahmen, Anbau/Erweiterung sichtbar |
| 100-249 | +Silber-Rahmen, zweites Stockwerk |
| 250-499 | +Gold-Rahmen, volle Dach-Details |
| 500-999 | +Diamant-Rahmen, SkiaGlowEffect Premium-Aura |
| 1000+ | Gold-Krone, volle Pracht, SkiaShimmerEffect |

### Materialien statt Flat-Colors

| Material | Rendering-Technik |
|----------|------------------|
| Holz | Horizontale Linien (3-4) + CraftTextures.DrawWoodGrain() |
| Stein | Ziegelreihen (horizontale + vertikale versetzte Linien) |
| Metall | Vertikaler Gradient hell->dunkel + Glanz-Highlight oben |
| Glas | Semi-transparent (#FFFFFF alpha 30%) + diagonale Reflektion |
| Putz | Leichter Noise (feine Punkte) auf Grundfarbe |
| Ziegel | Reihen abwechselnd versetzt (wie RoofTilingRenderer) |

---

## 4. Terrain-System

### Rauten-Kacheln

- **Gras:** Mehrere Grüntöne per Hash-Diversität, feine Grashalm-Linien
- **Weg/Straße:** Zwischen Gebäuden, Progression per WorldTier
- **Freie Flächen:** Gesperrte Slots mit Silhouette + Schloss

### Progression (CityProgressionHelper-Integration)

| WorldTier | Umgebung |
|-----------|----------|
| 1-2 | Feldweg, wilder Grasboden, karge Bäume |
| 3-4 | Asphaltweg, gepflegtes Gras, Hecken + Bäume |
| 5-6 | Pflasterweg, Bänke + Laternen, Blumenbeete |
| 7+ | Premium-Pflaster, Brunnen, Statuen, Prachtgarten |

### Dekorationen auf leeren Kacheln

- Bäume mit Wind-Schwanken (Sinus-Animation)
- Laternen mit Lichtkegel nachts
- Blumenbeete, Bänke, Brunnen (portiert aus CityProgressionHelper)
- Lieferwagen auf Wegen (animiert)
- Mini-Fußgänger auf Wegen

---

## 5. Camera-System

### IsoCameraSystem

```
OffsetX, OffsetY   -> Pan-Position
Zoom (0.5 - 2.0)   -> Pinch-Zoom
TargetZoom          -> Smooth-Interpolation
```

### Gesten

- **1-Finger-Drag:** Pan (mit Inertia/Deceleration)
- **2-Finger-Pinch:** Zoom (0.5x - 2.0x, Smooth via Lerp)
- **Tap:** HitTest -> Radial-Menü oder Navigation
- **Double-Tap:** Zoom auf Tap-Position

### Verhalten

- Bounds-Clamping (Camera bleibt innerhalb Weltkarte)
- Smooth-Follow bei FocusOnBuilding() mit EaseOutCubic
- SKMatrix-Transformation auf Canvas

---

## 6. Radial-Menü

### 4 Menü-Punkte bei Tap auf freigeschaltetes Gebäude

```
        [Upgrade]
           |
  [Worker] o [MiniGame]
           |
        [Info]
```

1. **Upgrade** (Pfeil-hoch) -> Direktes Level-Up
2. **Worker** (Person) -> WorkerMarket für diesen Workshop
3. **MiniGame** (Spiel) -> Startet MiniGame
4. **Info** (i) -> Workshop-Detail-View

### Animation

- Öffnen: Scale 0->1 mit EaseOutBack, staggered 50ms pro Punkt
- Halbtransparenter dunkler Backdrop
- 4 Kreise (48dp) mit SkiaSharp-Icons
- Schließen bei Tap außerhalb oder nach Auswahl
- Touch-HitTest: Distanz zu Menüpunkt-Zentrum (Radius 24dp)

---

## 7. Partikel & Shader

### Pro Workshop-Typ

| Workshop | Partikel | Shader |
|----------|----------|--------|
| Carpenter | Sägemehl (gelb, aufsteigend) | - |
| Plumber | Wasser-Tropfen (blau, fallend) | - |
| Electrician | Funken (orange-gelb, spritzend) | - |
| Painter | Farbspritzer (bunt, schwebend) | - |
| Roofer | Staub (grau-braun, aufsteigend) | - |
| Contractor | Staub + Mörtel-Tropfen | - |
| Architect | Papier-Fetzen (weiß, wehend) | - |
| GeneralContractor | Gold-Glitzer (aufsteigend) | SkiaShimmerEffect |
| MasterSmith | Funken + Rauch | SkiaFireEffect (SkSL GPU) |
| InnovationLab | Elektro-Funken (cyan) | SkiaGlowEffect |

### Struct-Pool (0 GC-Pressure)

```csharp
struct IsoParticle { float X, Y, VX, VY, Life, MaxLife, Size; uint Color; byte Type; }
// Max 300 Partikel
```

### Tag/Nacht-Zyklus

Portiert aus CityRenderer: Fenster leuchten nachts, Laternen-Lichtkegel, Sonnenauf-/Untergang.

---

## 8. Dashboard-Ersatz & Navigation

### Tab-Struktur

| Tab | Vorher | Nachher |
|-----|--------|---------|
| 0 (Werkstatt) | DashboardView (City-Header + Karten) | **IsometricWorldView** (Vollbild) |
| 1-4 | Unverändert | Unverändert |

### HUD-Overlay (fest, nicht scrollbar)

- **Oben links:** Level + XP-Bar (OdometerRenderer)
- **Oben rechts:** Geld (OdometerRenderer) + Goldschrauben (Shimmer)
- **Oben Mitte:** Banner-Strip (Rush/Lieferant/Worker-Warnung)
- **Unten:** Tab-Bar (GameTabBarRenderer, unverändert)

### Workshop-Karten

Bisherige Dashboard-Karten (Level/Income/Worker) werden in die Workshop-Detail-View verschoben, erreichbar per Radial-Menü -> Info.

---

## 9. Qualitäts-Garantie vs. letzter Versuch

### Was beim letzten Mal fehlte

| Problem | Ursache | Lösung |
|---------|---------|--------|
| Farbige Würfel | Generic DrawIsometricBuilding() mit 5 Draw-Calls | 10 individuelle DrawXxxWorkshop() mit je 25+ Draw-Calls |
| Keine Texturen | DrawRect(flatColor) | Gradient + Linien-Muster (Holz/Stein/Metall) |
| Keine Erkennbarkeit | Nur Farbe unterscheidet Workshops | Form + Material + Werkzeug + Umgebung + Partikel |
| Keine Atmosphäre | Kein Himmel, keine Schatten | Tag/Nacht, Wetter, Schatten, Beleuchtung |
| Kein Boden-Grid | Flache grüne Fläche | Diamond-Kacheln mit Gras-Variation + Wege |
| Proportionen falsch | Gebäude zu groß | 80dp Breite in 96dp Kachel, Höhe 60-120dp |

### Qualitäts-Metriken

- Min. 25 Draw-Calls pro Gebäude (statt 5)
- Min. 2 Texturen pro Gebäude (Wand + Dach)
- Min. 3 Detail-Elemente pro Gebäude (Fenster + Tür + Workshop-Merkmal)
- Min. 2 Umgebungs-Objekte pro Gebäude
- Animierte Partikel pro aktivem Workshop
- Boden-Schatten unter jedem Gebäude
- Level-Evolution visuell sichtbar (5 Stufen)

### Referenz-Qualität

Ziel-Qualität = WorkshopSceneRenderer-Niveau (2000+ Zeilen, ~30 Draw-Calls pro Szene, Materialien, Werkzeuge, Figuren, Partikel, Shader). Diese Qualität wird 1:1 in isometrische Perspektive übertragen.
