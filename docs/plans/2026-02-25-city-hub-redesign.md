# HandwerkerImperium: City-Hub Redesign

**Datum**: 25. Februar 2026
**Status**: Design genehmigt, Implementierung ausstehend
**Ansatz**: Parallel-Entwicklung (neuer CityHubRenderer neben bestehendem CityRenderer)

---

## Zusammenfassung

Das gesamte UI von HandwerkerImperium wird auf ein Stadt-Hub-Konzept umgestellt. Die Stadt wird zum zentralen interaktiven Bildschirm. Spieler navigieren durch Tippen auf Gebaeude statt ueber Buttons/Tabs. Jedes Feature bekommt ein physisches Gebaeude in der Stadt.

---

## Kern-Entscheidungen

| Aspekt | Entscheidung |
|--------|-------------|
| Stadtansicht | Vollbild, isometrisch (2.5D) |
| Navigation | Freies Zoom + Pan (Pinch + Drag, stufenlos 0.5x-3.0x) |
| Gebaeude-Tap | Inline-Popup direkt am Gebaeude |
| Popup-Inhalt | Info + Quick-Actions + Mini-Status (Upgrade, Auftrag, Details) |
| Nicht-Workshop-Features | Eigene Gebaeude + minimales HUD |
| Stadtwachstum | Wachsende Stadt mit 6 Zonen |
| Tab-Bar | Reduziert auf 3 Tabs: Stadt, Shop, Mehr |
| Ad-Banner | NICHT in der Stadt-Ansicht, nur in Vollbild-Views |
| Umsetzung | Parallel-Entwicklung mit Feature-Flag |

---

## Architektur

### Neue Dateien

```
Graphics/CityHub/
  CityHubRenderer.cs        Haupt-Renderer (Vollbild, isometrisch)
  CityHubCamera.cs          Kamera: Zoom, Pan, Boundary-Clamping, Smooth-Lerp, Inertia
  CityHubGrid.cs            Isometrisches Grid (World<->Screen, Tile 80x40)
  CityHubBuilding.cs        Einzelnes Gebaeude: Position, Groesse, Typ, HitTest-Polygon
  CityHubZone.cs            Stadtviertel: Freischalt-Bedingung, Gebaeude-Liste, Terrain
  CityHubPopup.cs           Inline-Popup: Rendern + Button-Interaktion
  CityHubHud.cs             HUD-Overlay: Geld, XP, Level, Settings
  CityHubInputHandler.cs    Touch-Verarbeitung: Tap, Drag, Pinch, Button-HitTest
  CityHubTerrain.cs         Boden, Strassen, Wege, Dekoration, Wasser
  CityHubWorkers.cs         Arbeiter auf Strassen (Pfad-Animation)

Views/
  CityHubView.axaml         Neue View (ersetzt Dashboard als Hauptansicht)
  CityHubView.axaml.cs      Code-Behind (Touch-Events, Render-Loop 30fps)

Models/
  CityHubCameraState.cs     Kamera-Position/Zoom fuer Restore
  CityHubZoneDefinition.cs  Zone-Layout-Daten
  CityHubLayoutData.cs      Gebaeude-Grid-Positionen
```

### Datenfluss

```
Touch-Event (PointerPressed/Moved/Released)
  -> CityHubInputHandler
     -> Pinch  -> CityHubCamera.Zoom(delta)
     -> Drag   -> CityHubCamera.Pan(dx, dy)
     -> Tap Popup-Button -> ViewModel-Command
     -> Tap Gebaeude     -> CityHubPopup.Show(building)
     -> Tap HUD          -> Navigation/Action

Render-Loop (30fps DispatcherTimer)
  -> CityHubRenderer.Render(canvas, bounds, gameState, deltaTime)
     -> canvas.Save() + camera.ApplyTransform(canvas)
     -> CityHubTerrain.Render()
     -> CityHubZone[].Render()
     -> CityHubBuilding[].Render()  (sortiert nach Y fuer Verdeckung)
     -> Partikel/Wetter/Ambient
     -> canvas.Restore()
     -> CityHubPopup.Render()       (Screen-Space)
     -> CityHubHud.Render()         (Screen-Space)
```

### Integration mit bestehendem Code

ViewModels bleiben unveraendert. Nur der Navigationsweg aendert sich:

```csharp
// MainViewModel - neue Properties/Methoden
IsCityHubActive            // default true wenn Feature-Flag an
CityHubState               // Kamera, Popup, Zonen-State
OnBuildingTapped(id)       // -> Popup oeffnen
OnPopupAction(action, id)  // -> Upgrade/Auftrag/Details
ReturnToCity()             // -> Alle Views schliessen, Stadt zeigen
```

Feature-Flag: `UseCityHub` in MainViewModel. false = alter CityRenderer, true = neuer CityHubRenderer.

---

## Isometrisches Grid

```
World-Space:                Screen-Space:
  (0,0) (1,0) (2,0)              <>
  (0,1) (1,1) (2,1)   ->      <>    <>
  (0,2) (1,2) (2,2)        <>    <>    <>

Tile-Groesse: 80x40 Pixel (2:1 Ratio)

WorldToScreen(gx, gy):
  sx = (gx - gy) * tileHalfWidth
  sy = (gx + gy) * tileHalfHeight

ScreenToWorld(sx, sy):
  gx = (sx / tileHalfW + sy / tileHalfH) / 2
  gy = (sy / tileHalfH - sx / tileHalfW) / 2
```

Depth-Sorting: `buildings.OrderBy(b => b.GridY).ThenBy(b => b.GridX)`

---

## Stadtzonen (6 Zonen)

### Zone 1: "Handwerker-Gasse" (Freischalt: Sofort)
- Schreinerei (Workshop, 2x2)
- Rathaus (Feature, 3x3) - Statistiken, Achievements, Einstellungen
- Kleiner Markt (Feature, 2x2) - Shop Basis
- Werkstatt-Erweiterung (Building, 2x2)
- Brunnen (Deko, 1x1) - Wasser-Shader

### Zone 2: "Rohr- & Strom-Viertel" (Freischalt: Level 8)
- Klempnerei (Workshop, 2x2)
- Elektrik (Workshop, 2x2)
- Buerogebaeude (Building, 2x2) - Office
- Trainingscenter (Building, 2x2)

### Zone 3: "Kreativ-Bezirk" (Freischalt: Level 22)
- Malerei (Workshop, 2x2)
- Dachdecker (Workshop, 2x2)
- Festplatz (Feature, 3x2) - Events, BattlePass, Lucky Spin, Daily Rewards
- Ausstellungsraum (Building, 2x2) - Showroom

### Zone 4: "Bau-Imperium" (Freischalt: Level 60)
- Bauunternehmer (Workshop, 3x2)
- Generalunternehmer (Workshop, 3x2)
- Fuhrpark (Building, 2x2) - VehicleFleet
- Lager (Building, 2x2) - Storage
- Forschungslabor (Feature, 3x3) - Research

### Zone 5: "Prestige-Bezirk" (Freischalt: Prestige 1)
- Architekturbuero (Workshop, 3x2)
- Meisterschmiede (Workshop, 2x2)
- Innovationslabor (Workshop, 3x2)
- Gildenhaus (Feature, 4x3) - Guild
- Meister-Akademie (Feature, 3x2) - Manager-System

### Zone 6: "Marktplatz-Erweiterung" (Freischalt: Prestige 2)
- Grosser Markt (Feature, 3x3) - Shop erweitert
- Arbeitermarkt (Feature, 3x2) - Worker Market
- Prestige-Shop (Feature, 2x2)

**Gesamt: 26 interaktive Gebaeude + Deko-Elemente**

### Zonen-Freischaltung visuell
1. Nebel lichtet sich (SkSL Fog-Shader, Opacity 1.0 -> 0.0, 2s)
2. Gebaeude "wachsen" aus dem Boden (Y-Offset, gestaffelt)
3. Strassen-Verbindung baut sich auf (Pflastersteine einzeln)
4. Confetti + Celebration-Sound
5. Kamera zoomt automatisch auf neue Zone

---

## Gebaeude-Detailgrad

### Layer-Aufbau pro Gebaeude (7 Layer)

```
Layer 7: Dach-Effekte     (Rauch, Funken, Antennen, Wetter)
Layer 6: Dach             (Typ-spezifisch: Ziegel, Kuppel, Giebel)
Layer 5: Obere Etage      (Fenster mit Innenbeleuchtung, Schilder)
Layer 4: Hauptfassade     (Tueren, Schaufenster, Werkzeug, Arbeiter)
Layer 3: Fundament        (Stufen, Kellerfenster, Materialien)
Layer 2: Umgebung         (Vorgarten, Werkzeuge, Kisten, Gehweg)
Layer 1: Schatten         (Dynamisch nach Tageszeit)
Layer 0: Boden-Tile       (Pflaster, Gras, Kies)
```

### Permanente Animationen pro Gebaeude

| Gebaeude | Animationen |
|----------|------------|
| Schreinerei | Rauch aus Schornstein, Saegeblatt dreht, Holzspaene-Partikel, Laterne flackert nachts |
| Klempnerei | Wasser tropft, Rohre dampfen (Heat-Shimmer), Neon-Schrift pulsiert |
| Elektrik | Blitzableiter-Funken (Electric-Arc), LED-Schild flackert, Kabel schwingen |
| Malerei | Farbkleckse wechseln Farbe (Hue-Shift), Regenbogen-Reflexion |
| Dachdecker | Ziegel-Wind-Wobble, Arbeiter auf Dach haemmert |
| Bauunternehmer | Mini-Kran dreht, Betonmischer-Animation, Bauplan flattert |
| Generaluntern. | Gold-Shimmer-Fassade, Flaggen wehen, Limousine parkt |
| Architekturbuero | Hologramm-Blueprint schwebt + rotiert, Glasfassade spiegelt |
| Meisterschmiede | Esse glueht (Fire-Shader), Amboss-Funken, Hitze-Flimmern, Haemmern |
| Innovationslabor | Tesla-Coil (Electric-Arc), violette Kuppel pulsiert, Dampf, Zahnraeder |
| Rathaus | Uhrturm-Zeiger bewegen sich, Flagge weht, Tauben auf Dach |
| Forschungslabor | Reagenzglas-Blasen, Zahnraeder drehen, Gluehbirne blinkt |
| Gildenhaus | Fackeln brennen, Wappen-Banner weht, Eichentuer |
| Festplatz | Wimpel wehen, Karussell dreht, Luftballons steigen, Lichterkette nachts |
| Markt | Marktstände-Daecher wehen, Waren ein/ausgeladen, NPCs stoebern |
| Arbeitermarkt | Warteschlange, Gesucht-Schilder, Arbeiter winken |

### Event-Animationen (bei Upgrade, Auftragsabschluss etc.)
- Schreinerei: Geruest -> neues Stockwerk waechst
- Klempnerei: Wasser-Fontaene Celebration
- Elektrik: Lichter gehen sequenziell an
- Malerei: Fassade "frisch gestrichen" Wipe
- Meisterschmiede: Schwert geschmiedet (Glow -> Form -> Abkuehlung)
- Forschungslabor: Eureka-Blitz + Gluehbirne
- Festplatz: Riesenrad + Feuerwerk bei Event
- Markt: Muenzen fliegen zum HUD

---

## Shader-Einsatz

| Shader | Status | Gebaeude |
|--------|--------|---------|
| Fire | Bestehend | Meisterschmiede, Schreinerei, Gildenhaus |
| Electric Arc | Bestehend | Elektrik, Innovationslabor |
| Heat Shimmer | Bestehend | Klempnerei, Meisterschmiede, Schreinerei |
| Shimmer/Glow | Bestehend | Generaluntern., Markt, Rathaus |
| Water Ripple | NEU | Brunnen, Klempnerei, Kanal |
| Hologramm | NEU | Architekturbuero, Innovationslabor |
| Nebel/Fog | NEU | Gesperrte Zonen, Morgen-Atmosphaere |
| Wind/Cloth | NEU | Flaggen, Banner, Wimpel, Markisen |
| Neon-Glow | NEU | Elektrik, Festplatz, Innovationslabor |
| Procedural Holz | NEU | Schreinerei, Dachdecker, Werkstatt-Erweiterung |

---

## Tag/Nacht-Zyklus

- **Tag (06-18h)**: Fenster dunkel, volle Saettigung, Schatten, Arbeiter draussen
- **Daemmerung (18-20h / 05-06h)**: Fenster leuchten warmweiss, Laternen staffeln sich ein
- **Nacht (20-05h)**: Fenster warm beleuchtet (individuell), Laternen voll an mit Licht-Radius, Gebaeude gedimmt, Esse/Fackeln dominant, Sterne + Mond

---

## Inline-Popup

Erscheint ueber dem Gebaeude mit Dreieck-Zeiger. Holz-Textur-Hintergrund.

Inhalt (Workshop): Name + Level, Einkommen/s, Arbeiter-Anzahl/Max + Stimmung, Auftrags-Fortschritt + Restzeit, 3 Buttons: Upgrade (Preis), Auftrag starten, Details.

Varianten fuer Rathaus, Forschungslabor, Gildenhaus, Festplatz, Markt, Support-Gebaeude, gesperrte Gebaeude.

Animation: Scale 0->1 + Opacity (EaseOutBack, 200ms ein / EaseInCubic, 120ms aus).

---

## HUD (Screen-Space)

- **Obere Leiste (48dp)**: Settings-Zahnrad, Geld (Gold-Shimmer), Gems, Level + XP-Bar, Event-Timer
- **Geld-Display (mittig-unten)**: OdometerRenderer mit rollenden Ziffern, Gold-Flash
- **Tab-Bar (3 Tabs)**: Stadt, Shop, Mehr (klappt Menue auf: Statistiken, Einstellungen, Hilfe)
- **Kein Ad-Banner** in der Stadt-Ansicht

---

## Transitions (Stadt <-> Vollbild-View)

### Hinein (750ms gesamt)
1. Popup schliesst sich (120ms)
2. Kamera zoomt auf Gebaeude (300ms) + Blur 0->4px
3. Gebaeude "oeffnet sich" - typ-spezifische Animation (200ms)
4. Crossfade zum Vollbild-View (250ms)

### Zurueck (600ms gesamt)
1. View faded aus (200ms)
2. Stadt erscheint, gezoomt, Blur 4->0px (200ms)
3. Kamera zoomt zurueck auf vorherige Position (400ms)

---

## Performance-Budget (30fps = 33ms pro Frame)

| Komponente | Budget |
|-----------|--------|
| Terrain + Grid | ~3ms (gecachte Tiles, nur sichtbare) |
| Gebaeude (max 26) | ~8ms (nur sichtbare, LOD bei Zoom-Out) |
| Partikel + Animationen | ~4ms (Pools, Structs, kein GC) |
| Shader-Effekte | ~3ms (GPU) |
| Popup + HUD | ~2ms (Screen-Space) |
| **Gesamt** | **~20ms (13ms Headroom)** |

### LOD (Level of Detail)
- Zoom > 2.0x: Voller Detail
- Zoom 1.0-2.0x: Normal (Hauptlayer, wichtigste Animationen)
- Zoom < 1.0x: Reduziert (keine Fenster-Details, keine Strassen-Arbeiter)

---

## Feature-Flag

```csharp
// MainViewModel
public bool UseCityHub { get; set; }
// false = alter CityRenderer + DashboardView (Fallback)
// true  = neuer CityHubRenderer + CityHubView
```

Alter Code bleibt vollstaendig erhalten bis der neue Hub stabil ist.
