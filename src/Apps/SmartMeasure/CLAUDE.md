# SmartMeasure — 3D-Grundstücksvermessung + Gartenplanung

Privates Projekt (nicht im Play Store). Erfassung ausschließlich per **AR-Kamera**
(±5–50 cm, ohne Zusatz-Hardware): Man geht durch den Garten, setzt Punkte, zeichnet
Konturen → 3D-Geländemodell + 2D-Gartenplan. Export nach Blender, GeoJSON, DXF, KMZ,
CSV, PDF.

> Die frühere RTK-GPS-Stab-Integration (BLE-Rover, NTRIP, Stakeout-Tab, RTK-AR-Fusion,
> ArUco-Referenzmarker) wurde vollständig entfernt — reaktivierbar über den Git-Tag
> `smartmeasure-rtk-pre-removal`. Die DIY-Hardware selbst ist im Memory `smartmeasure.md`
> dokumentiert.

| Aspekt | Wert |
|--------|------|
| Plattformen | Desktop (Entwicklung/Mock) + Android (Samsung Galaxy S25 Ultra) |
| Min SDK | 26 (Android 8.0) |
| ARCore-Paket | Vapolia.Google.ARCore 1.47.1 |

Generische Build-Befehle, Conventions, Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator. Kein AdMob/IAP (privates Projekt).

```
SmartMeasure.Android ┐
                     ├─> SmartMeasure.Shared ──> MeineApps.Core.Ava  (Preferences, Localization, ViewLocator)
SmartMeasure.Desktop ┘                       └─> MeineApps.UI        (SkiaSharp-Helpers, Behaviors)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `SmartMeasure.Shared/App.axaml.cs`
(Factory-Properties → DI-Build → `MainViewModel`) → ViewLocator löst die 7 Views.
Desktop nutzt `MockArCaptureService` statt echter Hardware.

### Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Doku |
|---------|------|
| Composition Root, DI, Factory-Reihenfolge, Lifecycle | [SmartMeasure.Shared](SmartMeasure.Shared/CLAUDE.md) |
| Android-Host, AR-Brücke, Permissions, FileProvider | [SmartMeasure.Android](SmartMeasure.Android/CLAUDE.md) |
| Desktop-Host, Mock-Modus | [SmartMeasure.Desktop](SmartMeasure.Desktop/CLAUDE.md) |
| ViewModels (Navigation, Messung, Terrain, ...) | [Shared/ViewModels](SmartMeasure.Shared/ViewModels/CLAUDE.md) |
| Views (AXAML, Touch, Lazy-Map, SKCanvasView-Pattern) | [Shared/Views](SmartMeasure.Shared/Views/CLAUDE.md) |
| Services (Geo-Algorithmen, Export, AR-Math) + Gotchas | [Shared/Services](SmartMeasure.Shared/Services/CLAUDE.md) |
| Models (SQLite-Entities, TerrainMesh, AR-Typen) | [Shared/Models](SmartMeasure.Shared/Models/CLAUDE.md) |
| SkiaSharp-Renderer (Terrain, GardenPlan, Thumbnail) + Farbpalette | [Shared/Graphics](SmartMeasure.Shared/Graphics/CLAUDE.md) |

Diese Datei trägt nur, was **app-übergreifend** ist: die übergreifenden Datenflüsse und
die ARCore-Capture-Activity (UX + AR-Features). Service-/Renderer-/Algorithmus-Detail und
die Gotcha-Tabellen leben in den jeweiligen Unterordner-Dateien (siehe Doku-Karte) — hier
nicht wiederholt.

---

## Übergreifende Datenflüsse

### AR → Terrain Transfer

```
ArCaptureActivity → ConsumeLastResult → AndroidArCaptureService → TCS
  → SurveyViewModel.ArCaptureCompleted-Event
  → MainViewModel: ArTransferService.TransferToProjectAsync
    → RotateAndProject (ARCore +Z = hinten — Rotations-Formel siehe Services-CLAUDE.md)
    → IGeoidService für Höhen-Korrektur
    → ProjectService.AddPointAsync + AddGardenElementAsync
```

### Projekt-Load (Batch, NICHT iterativ)

```
ProjectsView.OpenProject → MainViewModel lädt aus DB
  → MeasurementService.ReplacePoints (EIN PointsReset-Event!)
  → TerrainViewModel.RecalculateMesh (1× für N Punkte, nicht N×)
  → GardenPlanViewModel.LoadElementsFromProjectAsync
```

`ReplacePoints` + `PointsReset`-Event verhindert O(N²)-Triangulation beim Load.

### Export-Pattern

```
ProjectsViewModel.ExportXxxAsync
  → Datei in IAppPaths.ExportFolder schreiben
  → FileExportReady-Event mit Pfad
  → MainViewModel → MessageRequested + ExportBanner-State
  → MainActivity: Share-Intent (FileProvider) oder Open-Intent (MIME-Type)
```

---

## ARCore-Capture-Activity (Android)

`ArCaptureActivity` ist eine native `AppCompatActivity` (kein Avalonia), als `partial class`
über drei Files verteilt (Datei-/Verantwortungs-Trennung). Brücke ins Shared-Projekt via
`TaskCompletionSource<ArCaptureResult?>` in `AndroidArCaptureService` (Factory-Wiring →
[Android-CLAUDE.md](SmartMeasure.Android/CLAUDE.md)). TCS-Lock-Pattern + Status-Enum
`IArCaptureService.LastCompletionStatus` (`Success | UserCancelled | Error`) + `LastError`
erlauben dem UI-Layer, User-Abbruch von echten Fehlern zu trennen (`SurveyViewModel` zeigt
unterschiedliche Meldungen je Status).

### Layout (3 Schichten)

```
FrameLayout
├── GLSurfaceView          OpenGL ES 3.0 Kamera-Preview (ArBackgroundRenderer)
├── ArPointOverlayView     Transparenter Canvas (Punkte, Linien, Auswahl, gesamtes HUD)
└── Native Toolbar          7 Icon+Label-Buttons (VectorDrawables Resources/drawable/ic_ar_*):
                            Punkt · Fläche · Schließen · Zurück · Vor · Mehr · Fertig.
                            "Fertig" = grüner CTA, aktiver Modus = Akzent (Farb-Konstanten
                            ToolbarAccent/Inactive/Cta, an das Overlay-Design-System angeglichen).
                            "Mehr" = PopupMenu (Maßband, Tachymeter, Löschen,
                            Bodenraster ein/aus, Screenshot, Aufnahme, Hilfe).
                            KEINE Emojis/Unicode als UI-Text.
```

Das gesamte HUD (Banner, Pillen, Footer, Modus-Chip, Stats, Readiness-Badge, Empty-State)
wird im Canvas über **ein** Glas-Panel-Primitiv gezeichnet — `ArPointOverlayView.Design.cs`
hält die semantischen Farb-Tokens (Klasse `C`), Typo-Schnitte und `DrawPanel`/`DrawStatusDot`.
Status wird über die **Border-/Dot-Farbe** codiert (Ampel Good/Medium/Poor), nicht über
vollflächige Knallpanels. Modus + Punkt-Zähler laufen über den Canvas-Modus-Chip
(`DrawModeChip`, gespeist aus `BuildModeChipLabel`) — **keine** nativen TextViews mehr.

### Lokalisierung (`ArOverlayLabels`)

Die Activity hat keine Avalonia-DI. Lokalisierte Strings werden einmalig in `OnCreate` via
`LoadLocalizedLabels()` aus `AppStrings.*` gelesen und als `ArOverlayLabels`-Record in jedem
`ArOverlayState`-Snapshot mitgegeben. Sprachwechsel mid-AR-Session passiert nicht
(Modal-Fullscreen) → ein Snapshot pro Session genügt.

### Capture-Modi (`CaptureMode`)

| Mode | Verhalten |
|------|-----------|
| `Point` | Einzelne Messpunkte ins Projekt + Undo-Stack + Foto-Annotation. |
| `Contour` | Aktive Kontur (Weg/Beet/Mauer/...) — Mehrfach-Tap + `CloseActiveContour` mit Bowditch-Correction + Foto-Annotation pro Punkt. |
| `Rectangle` | Geführte 3-Punkt-Rechteck-/Quadrat-Erfassung: zwei Tipps spannen die Basiskante auf, der dritte legt die Tiefe fest. `ArRectangleBuilder` (Shared, testbar) erzwingt rechte Winkel im Grundriss (X/Z) und snappt bei ~10 % Toleranz auf ein Quadrat; Höhen werden auf die Ebene durch die drei Messpunkte projiziert. Ergebnis ist eine geschlossene `ArContour` (Typ aus dem Flächen-Dialog). Anchors der Ecken werden detacht (starre Form, kein Drift-Verzug). Live-Vorschau (Polygon + Länge/Tiefe/Fläche + Quadrat-Indikator) im Overlay. Einstieg über den **Flächen**-Button → erster Dialog-Eintrag „Rechteck / Quadrat". |
| `TapeMeasure` | Ad-hoc-Distanz. Eigener Buffer `_tapeMeasurePoints`, kein Projekt-Save, kein Undo, kein Foto. Long-Press auf Maß-Button = Reset. Footer zeigt Σ Strecken-Summe. |
| `TotalStation` | Stationierung an der Geospatial-/VPS-Position + Radial-Projektion (Distanz + Bearing + Pitch → Lat/Lon) via `ITotalStationService`. |

### Marker-Overlays

- **Site-Marker** (`IArCaptureService.SetSitePoints`): bestehende Projekt-`SurveyPoints` werden
  vor Session-Start übergeben. Sobald Geospatial-Tracking aktiv ist, erzeugt
  `CreatePendingSiteAnchors` Earth-Anchors (max 2/Frame). Render als dezente graue Kreise — neue
  Punkte landen im selben Koordinatensystem.

### ARCore-Features aktiv

| Feature | Zweck |
|---------|-------|
| `ArAnchorManager` | Drift-Kompensation: Anchor pro gesetztem Punkt, RefreshAnchors pro Frame |
| `ArPoseSampler` (Shared.Services) | Multi-Frame-Averaging (15 Samples / 800 ms), Median + ±3σ-Outlier-Filter |
| `ArStabilityMonitor` (in `ArAnchorManager.cs`) | EMA über Gyro + Accel, StabilityScore 0..1, Block bei < 0,6 |
| `ArPrecisionHelpers` | Depth-Sanity, Depth-Fallback (Instant-Placement), Ground-Plane, Heading-Extraktion, Semantic-Label, Sky-Check. Math-Helfer delegiert an `ArMathHelpers` (Shared) |
| `ArSnapEngine` (Shared.Services) | Vertex (15 cm), Right-Angle (5°), Parallel (3°), Extension (10 cm) |
| Geospatial API (VPS) | `earth.CameraGeospatialPose` → Heading ±5° statt ±15–30° (Metall-immun) |
| Earth-Anchors | Persistent über Session-Ende via VPS re-lokalisierbar — Recovery-Restore queued Punkte für Re-Attach sobald Earth-Tracking aktiv |
| Raw Depth + Confidence | Pixel mit Confidence > 0,3 (Random-Noise-Filter) |
| Scene Semantics | `SemanticMode.Enabled` — Sky + Instant-Placement-Kombi wird abgelehnt, sonst Label in `ArPoint.SemanticLabel` |
| Light-Estimation | `LightEstimate.PixelIntensity` — Helligkeits-Sprung > 40 % bricht laufendes Sampling ab (2 s Cooldown) |
| Session Recovery | State in SharedPreferences nach jedem Punkt, max 30 Min alt (nur nicht-abgeschlossene Sessions; bei "Fertig" geloescht) |
| Vorlade-Punkte | Bestehende Projekt-Punkte werden beim AR-Start GEO-UNABHAENGIG relativ als `ArPoint.IsPreloaded` in `_points` geladen (Bridge `SetPreloadPoints`), gedaempft + "Lage relativ" gekennzeichnet. Gehen NIE ins Result/Recovery und sind aus allen Mess-Berechnungen + Snap ausgeschlossen (Korruptions-/Duplikat-Schutz). Ergaenzt die Earth-Anchor-Site-Marker (die VPS brauchen) |
| Screenshot/Recording | In die Galerie via MediaStore (`Pictures/SmartMeasure` / `Movies/SmartMeasure`, `MediaStoreGallery`), nicht mehr app-intern. Recording cache-then-copy (App-Cache → Galerie nach Stop/OnPause). `SetAutoStopOnPause(true)` |

**Bewusst NICHT aktiviert:** Cloud Anchors (kostenpflichtig — Earth-Anchor-Cache ist Default),
Shared Camera/Camera2 (Vapolia-Binding unvollständig).

### Bowditch-Korrektur

Bei Kontur-Close: Schlussfehler-Vektor proportional zur Distanz auf alle Zwischenpunkte
verteilen. Nur aktiv bei 1 cm–2 m Schlussfehler (kleiner: unnötig, größer: Fehler-Detection).

### Foto-Annotation pro Punkt

Bei jedem AR-Punkt (Point + Contour, NICHT TapeMeasure) macht `CapturePhotoForPoint` via
`PixelCopy.Request` einen JPEG-Snapshot des reinen Kamera-Frames (ohne Overlay) und legt ihn in
`IAppPaths.PhotosFolder` ab (`pt_<timestamp>_<guid>.jpg`, Quality 80, ~200 KB). `ArPoint.PhotoPath`
wird sofort gesetzt, der Disk-Write läuft asynchron → PDF-Bericht muss `File.Exists` prüfen.
Pfad wandert durch `ArTransferService` in `SurveyPoint.PhotoPath`.

### Confidence-Formel

```
confidence =
    Hit-Quality     (0.1 Instant / 0.2 Point / 0.3 Plane)
  + StdDev          (0.3 wenn σ=0, linear auf 0 bei σ=5 cm)
  + Stability       (0.2 × StabilityScore)
  + Anchor-Bonus    (+0.2 wenn Anchor erstellt)
→ max 1.0
```

### UX-Features (AR-Modus)

| Feature | Beschreibung |
|---------|-------------|
| Bestätigungs-Dialoge | Löschen + Fertig fragen vor destruktiver Aktion (`ConfirmDeleteSelectedPoint`, `ConfirmFinishCapture`) |
| Sound beim Punkt-Setzen | `MediaActionSound.SHUTTER_CLICK` zusätzlich zur Vibration. SharedPreferences-Key `ar.sound.enabled` (Default an). Toggle im Help-Dialog. |
| 3D-Punkt-Darstellung | `DrawPoints` zeichnet räumlich: Painter-Tiefensortierung (fern→nah), perspektivische Marker-Skalierung (0,45×–1,9× um 2,5 m Referenz), Bodenschatten-Ellipse + Höhen-Stab zur Bodenprojektion, Confidence-Ampel-Ring (grün/gelb/rot) statt `~/?`-Zeichen, ΔH am Stab-Kopf. Tiefe + Bodenprojektion kommen aus `WorldToScreen` (liefert Clip-Tiefe) + `ProjectPointsToScreen` (groundX/groundY/worldY je Punkt) |
| Pop-Animation neuer Punkte | 250 ms Scale-Easing in `DrawPoints` — junge Punkte (< 250 ms alt) starten 2.2× groß, schrumpfen mit Ease-Out-Quadratic |
| Boden-Raster (3D-Anker) | 1-m-Gitter auf der Ground-Plane, GL-seitig segmentweise projiziert + distanz-gecullt (`ProjectGroundGrid`, alloc-frei via Struct-Closure), Tiefen-Fade im `DrawGroundGrid`. Toggle im Mehr-Menü, Pref `ar.grid.enabled` (Default an). Verankert die Szene räumlich |
| Plastische Flächen | Geschlossene Konturen (Typ-Farbe), aktive Kontur (Akzent) und Rechteck-Vorschau (grün/orange je Quadrat-Snap) mit vertikalem Tiefen-Gradient (`FillPolygonGradient`) statt flacher Füllung |
| Modus/Schritt-Chip | Permanenter Glas-Chip oben mittig (`DrawModeChip`): Modus-Titel + nächster Schritt/Fortschritt (`BuildModeChipLabel`). Führt durch geführte Modi ("1. Ecke → 2. Ecke → Tiefe"), zeigt Kontur-Typ. Ersetzt native Modus-/Zähler-TextViews |
| Crosshair-Punktsetzung | Punkte werden immer am Crosshair (Bildmitte) gesetzt, nicht an der Tap-Position — passend zu den am Crosshair angezeigten Live-Distanzen (`HandleTouchUp` → `PlaceNewPoint(viewport/2)`) |
| Off-Screen-Distanz | Liegt der Vorpunkt außerhalb des Bildes, zeigt `DrawOffScreenLiveSegment` Distanz/ΔH am Crosshair + Rand-Pfeil zur Richtung (`LiveSegmentActive` + `LiveSegmentOffScreenDirectionDeg` aus `BuildOverlayState`) |
| Farbcodierte Hinweise | Transient-Hinweise nach Schweregrad (`TransientSeverity` Info/Success/Warning) → Panel-Ton + Status-Dot. `ShowTransientHint` hat optionalen Severity-Parameter (Default Info, atomar via Record-Feld) |
| Dialoge mit Status-/Typ-Dots | Kontur-/Rechteck-Typ-Dialoge zeigen farbigen Typ-Punkt je Eintrag (`DotListAdapter`, Farbe via `ArPointOverlayView.GetContourTypeColor`); Readiness-Dialog mit Status-Dots (grün/rot/bernstein/grau) als nicht-klickbare Zeilen (`BuildDotRow`) |
| Tooltips auf Toolbar-Buttons | Long-Press zeigt `Button.TooltipText` (API 26+) |
| Coach-Marks beim 1. AR-Start | Show-once Dialog (Crosshair/Workflow/Toolbar). Key `ar.coachmarks.shown`. "Später nochmal" lässt Pref unverändert → nächster Start zeigt erneut |
| Persistente System-Banner | `ArOverlayState.ThermalWarning` + `BatteryWarning` als persistente Top-Banner unter dem Tracking-Banner (vs. TransientHint-Fade) |
| Live-Footer-Bar | Über der Toolbar mit Punkte/Länge/Fläche in großer Schrift (`ArPointOverlayView.DrawLiveFooter`) |
| Live-Segment ("Gummiband") | Beim Punkt-/Kontur-Zeichnen: gestrichelte Linie vom zuletzt gesetzten Punkt zum Crosshair + schwebende Pille mit **Horizontaldistanz** (groß), **ΔH** + **Steigung %** (klein), HitQuality-gefärbt. Reticle-Weltpos wird in `BuildHitInfo` gespeichert → `BuildOverlayState` rechnet `Distance2DTo` (horizontal) / `DistanceTo` (schräg) / Y-Delta. Felder: `ArOverlayState.ShowLiveSegment` + `LiveSegment{FromScreen,Horizontal,Slope,HeightDelta}`. Render: `ArPointOverlayView.DrawRubberBand`/`DrawValuePill`. Frustum-geclippt (kein Springen). Distanzen < 1 m in cm (`FormatMeters`) |
| Kontur-Segment-Labels | Gesetzte aktive-Kontur-Segmente zeigen ihre horizontale Welt-Distanz zwischen den Punkten (`ActiveContourSegmentMeters` vom GL-Thread, gerendert in `DrawInterPointDistances` — früher leerer Stub) |
| Readiness-Badge Tap | Badge oben links klickbar (`ReadinessBadgeBounds`). Detail-Dialog mit Checkliste je Condition (Stabilität / Kompass / Planes / GPS / Geospatial / Tracking-Continuity) |
| Recovery-Bestätigungs-Dialog | "X Punkte aus letzter Sitzung wiederherstellen?" mit Wiederherstellen/Verwerfen — statt Auto-Restore. Earth-Anchors parallel re-attached |

### S25-Ultra-Spezifika

- `LightEstimationMode.EnvironmentalHdr` wenn RAM ≥ 8 GB
- Multi-Sample-Count: 15 (High-End) / 10 (Normal) / 5 (Thermal Severe)
- `PowerManager.CurrentThermalStatus` alle 60 Frames prüfen
- `OnApplyWindowInsets` liest Punch-Hole-Cutout → `ArOverlayState.TopInsetPixels`

---

## App-spezifische Conventions

### Mock-Modus (Desktop-Entwicklung)

`MockArCaptureService` ersetzt ARCore (deterministischer Seed, 12×8-m-Grundstück,
optional simuliertes Geospatial/VPS) — Details → [Desktop-CLAUDE.md](SmartMeasure.Desktop/CLAUDE.md).

### Thread-Safety (AR-Activity-spezifisch)

- `_dataLock` in `ArCaptureActivity` für alle Zugriffe auf `_points`, `_contours`, `_activeContour`.
  Undo/Redo-Actions halten Lock-Reference + setzen Lock bei Mutation.
- `_frameLock` für `_lastFrame` (GL-Thread schreibt, UI-Thread liest).
- `RunOnUiThread` für alle Overlay-State-Updates.

### Android-Build

- `OperatingSystem.IsAndroidVersionAtLeast(31)` statt `Build.VERSION.SdkInt` (Static-Analyzer).
- `SupportedOSPlatformVersion=26` im csproj.
- `RunAOTCompilation=false` + `AndroidEnableProguard=false` (Mapsui/NTS brauchen Reflection).
- `ArCaptureActivity` Theme: `@style/MyTheme.Fullscreen` (AppCompat, NICHT `android:Theme.Black`).
- `global::Android.Content.Res.…` voll qualifizieren (Namespace-Kollision App vs. Android-SDK → CS0234).

---

## Build

```bash
dotnet build src/Apps/SmartMeasure/SmartMeasure.Shared
dotnet run   --project src/Apps/SmartMeasure/SmartMeasure.Desktop
dotnet build src/Apps/SmartMeasure/SmartMeasure.Android
```

---

## Verweise

- Frühere RTK-Hardware (Stückliste, Firmware, BLE-Profil): Memory `smartmeasure.md` +
  Git-Tag `smartmeasure-rtk-pre-removal`
- DI/MVVM/DateTime/Thread-Safety, Naming, Localization: [Haupt-CLAUDE.md](../../../CLAUDE.md)
- SkiaSharp/Rendering-Gotchas: [MeineApps.UI](../../../UI/MeineApps.UI/CLAUDE.md)
- Avalonia/MVVM/Android-Framework-Fallstricke: [MeineApps.Core.Ava](../../../Libraries/MeineApps.Core.Ava/CLAUDE.md)
