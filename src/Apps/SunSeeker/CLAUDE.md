# SunSeeker — Solarpanel-Ausrichtung (Anker SOLIX C2000 / PS400 Bifazial)

Privates Projekt (nicht im Play Store). Richtet ein mobiles Solarpanel optimal zur Sonne aus
und maximiert den bifazialen Mehrertrag. Berechnet den Sonnenstand vollständig offline,
empfiehlt Himmelsrichtung + Neigung je Nutzungsziel, bewertet die Ist-Ausrichtung live
(Einfallswinkel / cosine-loss), zeigt die Tagesbahn der Sonne und die Live-Solar-Leistung.
Speziell für das **bifaziale** Anker PS400: Untergrund-/Albedo-Hinweise, Steilwinkel-Empfehlung
und Rückseiten-Freiheit.

| Aspekt | Wert |
|--------|------|
| Plattformen | Desktop (Entwicklung/Mock) + Android (Samsung Galaxy S25 Ultra) |
| Vertrieb | Privat, kein AdMob/IAP |
| Ziel-Hardware | Anker SOLIX PS400 (Bifazial, fester 35-Grad-Kickstand) + C2000 Gen 2 Powerstation |

Generische Build-Befehle, Conventions, Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Architektur-Überblick

Drei Projekte (analog SmartMeasure), ViewModel-First, kein Service-Locator.

```
SunSeeker.Android ┐
                  ├─> SunSeeker.Shared ──> MeineApps.Core.Ava  (ViewLocator, UriLauncher, ...)
SunSeeker.Desktop ┘                    └─> MeineApps.UI        (SkiaSharp-Helpers)
```

Composition-Flow: Host (`Program.cs` / `AndroidApp`+`MainActivity`) → `SunSeeker.Shared/App.axaml.cs`
(Factory-Properties → DI-Build → `MainViewModel`) → `MainView`. Desktop nutzt die Mock-Services
(kein GPS/Sensor/Anker). Plattform-Factories (Location, Heading, Anker) werden LAZY im
Resolve-Lambda ausgewertet (Avalonia-12-Android-Reihenfolge → [Haupt-CLAUDE.md](../../../CLAUDE.md),
Abschnitt „Platform-Factory-Registrierung").

### Doku-Karte (Pyramide)

| Bereich | Doku |
|---------|------|
| Composition Root, DI, Lifecycle, Namespaces | [SunSeeker.Shared](SunSeeker.Shared/CLAUDE.md) |
| Android-Host, Sensoren, Permissions, Manifest | [SunSeeker.Android](SunSeeker.Android/CLAUDE.md) |
| Desktop-Host, Mock-Modus | [SunSeeker.Desktop](SunSeeker.Desktop/CLAUDE.md) |
| Engine + Plattform-Services | [Shared/Services](SunSeeker.Shared/Services/CLAUDE.md) |
| Navigator + Tab-VMs (Lifecycle, Lokalisierung) | [Shared/ViewModels](SunSeeker.Shared/ViewModels/CLAUDE.md) |
| AXAML-Views, SKCanvasView, `{loc:Translate}` | [Shared/Views](SunSeeker.Shared/Views/CLAUDE.md) |
| Datenmodelle (Records, sprachneutrale Keys) | [Shared/Models](SunSeeker.Shared/Models/CLAUDE.md) |
| SkiaSharp-Renderer (Kompass, Sonnenbahn, Power) | [Shared/Graphics](SunSeeker.Shared/Graphics/CLAUDE.md) |

### Navigation (Tabs)

`MainViewModel` ist der Navigator mit drei Tabs (Tab-Bar unten, Wrapper-Panel-Pattern für den
Child-DataContext): **Ausrichten** (`AlignViewModel`, Default), **Leistung** (`LivePowerViewModel`),
**Übersicht** (`DashboardViewModel`). Die sensor-/monitor-gebundenen Tabs (Ausrichten, Leistung)
starten/stoppen ihre Hardware bei Tab-Wechsel (`Activate`/`Deactivate`) — spart Akku.

---

## Kern-Engine (plattformneutral, testbar)

Reine Berechnung in `SunSeeker.Shared/Services/` — ohne Plattform-API, unit-getestet
(`tests/SunSeeker.Tests`, 35 Tests).

| Service | Zweck |
|---------|-------|
| `ISolarPositionService` / `SolarPositionService` | Sonnenstand (Azimut/Elevation), Sonnenzeiten, Tagesbahn. NOAA Solar Calculator (Meeus-basiert), inkl. Zeitgleichung + atmosphärischer Refraktion. < 0,01 Grad (1800-2100). |
| `IAlignmentService` / `AlignmentService` | Soll-Ausrichtung je `AlignmentGoal` + Live-Einfallswinkel (cosine-loss, PVPMC/Sandia). |
| `IBifacialService` / `BifacialService` | Albedo, Mehrertrags-Bereich, Steilwinkel-Zuschlag, Aufstell-Tipps. |
| `SunMath` (internal static) | Geteilte Winkel-/Zeit-Mathematik (Deg/Rad, Normalisierung, Julianisches Datum). |

### Plattform-Services (Interface + Mock + Android)

| Interface | Mock (Desktop) | Android | Zweck |
|-----------|----------------|---------|-------|
| `ILocationService` | `MockLocationService` (fester Standort) | `AndroidLocationService` (nativer `LocationManager`) | Position für Sonnenstand + Missweisung. Bewusst kein Google Play Services — Kilometer-Genauigkeit genügt. |
| `IHeadingService` | `MockHeadingService` | `AndroidHeadingService` (`RotationVector` + `GeomagneticField` + Neigung) | Geräte-Azimut (Display-Normale) + Neigung. Missweisung via `SetLocation`. |
| `IAnkerMonitorService` | `MockAnkerMonitorService` (Watt aus Sonnenstand) | — (echte MQTT-Anbindung offen) | Live-Solar-Eingangsleistung. |

### SkiaSharp-Renderer (`Graphics/`)

| Renderer | Zweck |
|----------|-------|
| `SunCompassRenderer` | Ausricht-Kompass: Ring (Nord oben), Sonne (elevations-abhängiger Glow), grüner Soll-Marker, qualitätsgefärbter Panel-Pfeil, Neigungs-Bogen, Quality-Glow. |
| `SunPathRenderer` | Sonnenbahn-Diagramm: Tagesbahn (Elevation über Azimut O/S/W), Horizont, aktuelle Position. |
| `PowerChartRenderer` | Live-Watt-Trend (Fläche + Linie + Watt-Gitter). |

Renderer-Anbindung: `labs:SKCanvasView` im AXAML, Code-Behind abonniert ein VM-`*InvalidateRequested`-Event
und ruft `Render(canvas, LocalClipBounds, …)` im `PaintSurface`-Handler (Handler-Dedup im
`DataContextChanged`). IMMER `LocalClipBounds`, nie `e.Info.Width/Height` (DPI).

### Solar-Domänenwissen (Fakten hinter der Engine)

- **Optimaler Azimut**: äquatorwärts (Süd auf der Nord-, Nord auf der Südhalbkugel). Abweichung
  ist ertragstolerant (±15° < 1 % Verlust). Geografisch Süd, nicht magnetisch (Missweisung beachten).
- **Optimaler Tilt (DE, ~52°)**: Jahresertrag ~30-43° (FLACHER als der Breitengrad — Diffuslicht im
  Winter), Winter ~63-65°, Sommer ~25°. Faustformeln aus solarpaneltilt.com (in `AlignmentService`).
- **Einfallswinkel (AOI)**: `cos(AOI) = sin(elev)·cos(tilt) + cos(elev)·sin(tilt)·cos(sonnenAz − panelAz)`.
  Direkt-Ertrag ∝ cos(AOI); negativ = Sonne hinter dem Panel.
- **Bifazial**: Mehrertrag hängt v.a. von der Albedo des Untergrunds ab („doppelte Albedo ≈
  doppelter Mehrertrag"). Höhere Albedo → steilere Neigung optimal. Mehrertrag als BEREICH, nie Punktwert.

### `AlignmentGoal`

| Modus | Soll-Ausrichtung |
|-------|------------------|
| `NowMaximum` | Direkt auf die aktuelle Sonne (Azimut = Sonnen-Azimut, Neigung = Zenitwinkel). Für mobiles Panel. |
| `TodayYield` | Süd, Neigung senkrecht zur heutigen Mittagssonne. |
| `AnnualYield` | Süd, flacher Festwinkel (Jahresoptimum). |
| `WinterYield` | Süd, steiler Winterwinkel. |

Bei festem Kickstand snappt `PanelProfile.NearestKickstand` auf den nächsten realen Standwinkel
(PS400 Bifazial: fix 35°; PS400 mono: 30/40/50/80°).

### Ausricht-Konvention (AlignView)

Das Handy wird flach mit dem Bildschirm an die Panel-**Vorderseite** (zur Sonne) gehalten →
Display-Normale = Panel-Normale, also `PanelAzimuth = HeadingReading.DeviceAzimuth`. Bei zu flacher
Lage ist der Azimut instabil (`AzimuthReliable = false`) → die UI fordert zum Neigen auf.

---

## Anker-Hardware-Anbindung (Live-Watt)

Die C2000 Gen 2 (A1783) hat BLE + WLAN; **keine offizielle API**. Live-Solar-Watt sind über
Ankers Cloud-MQTT erreichbar (inoffiziell, reverse-engineered — `thomluther/anker-solix-api`,
PV-Feld `a6` = `dc_input_power`/Watt). Anker-Account nötig, jederzeit von Anker kappbar. Die
lokale BLE-Lib unterstützt die C2000 Gen 2 nicht. Eine .NET-Reimplementierung (Cloud-Login +
MQTT-Subscribe) ist der risikoärmere Weg. Bis dahin liefert `MockAnkerMonitorService` einen
physikalisch plausiblen Verlauf aus dem Sonnenstand (UI weist via `IsSimulated` darauf hin).

---

## Offene Punkte

- **Echte Anker-Live-Watt-Anbindung**: `AndroidAnkerMonitorService` (Cloud-Login + MQTT-Subscribe,
  `MQTTnet`). Braucht Anker-Account-Zugangsdaten + das Gerät zum Protokoll-Test. Reiner
  Service-Austausch (Factory `App.AnkerMonitorServiceFactory`).
- **Kamera-AR-Sonnenbahn**: optionales „Sonne durch die Kamera"-Overlay. Braucht kein ARCore-Tracking
  (Sonne = Richtung), aber eine native Kamera-Activity + Sensor-Projektion. Gerätegebunden. Das
  2D-Sonnenbahn-Diagramm (Übersicht-Tab) deckt den Kern-Nutzen bereits offline ab.
- **PS400-Winkel verifizieren**: bestätigen, ob die bifaziale PS400-Variante wirklich nur 35° fix kann.

App-Icon: eigenes adaptives Vektor-Icon (goldene Sonne auf Dämmerungs-Verlauf, `Resources/drawable/appicon_*.xml`).

## Lokalisierung

6 Sprachen (DE/EN/ES/FR/IT/PT) via `AppStrings.*.resx` (neutral = EN). Statische Labels in den
Views via `{loc:Translate Key=...}`, dynamische in den VMs via `ILocalizationService.GetString`.
Engine bleibt sprachneutral (Enum-/Tipp-**Keys**, keine UI-Texte). Kein Laufzeit-Sprachwechsel-UI
(Gerätesprache beim Start). Test-Abdeckung: `LocalizationTests` (Einbettung, Vollständigkeit,
Platzhalter-Erhalt).

---

## Build

```bash
dotnet build src/Apps/SunSeeker/SunSeeker.Desktop
dotnet run   --project src/Apps/SunSeeker/SunSeeker.Desktop
dotnet build src/Apps/SunSeeker/SunSeeker.Android
dotnet test  tests/SunSeeker.Tests
```

---

## Verweise

- DI/MVVM/DateTime/Thread-Safety, Naming, Avalonia-12-Patterns → [Haupt-CLAUDE.md](../../../CLAUDE.md)
- SkiaSharp/Rendering → [MeineApps.UI](../../../UI/MeineApps.UI/CLAUDE.md)
- Avalonia/MVVM/Android-Framework-Fallstricke → [MeineApps.Core.Ava](../../../Libraries/MeineApps.Core.Ava/CLAUDE.md)
- SmartMeasure (Vorlage für GPS/ARCore/BLE/Sensoren) → [SmartMeasure](../SmartMeasure/CLAUDE.md)
