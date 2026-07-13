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
| Ziel-Hardware | Anker SOLIX PS400 (Bifazial, stufenlos verstellbarer Standwinkel) + C2000 Gen 2 Powerstation |

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
| `IAnkerMonitorService` | `MockAnkerMonitorService` (Demo: Watt aus Sonnenstand) | `AnkerMonitorService` (Shared, plattformneutral — echte Cloud + mTLS-MQTT) | Live-Solar-Eingangsleistung; Demo-Fallback ohne Zugangsdaten. Details → [Services/Anker](SunSeeker.Shared/Services/Anker/CLAUDE.md). |

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
| `SeasonYield` | Süd, flacher Saisonwinkel fürs Sommerhalbjahr (April–Oktober, Panel bleibt die Saison stehen). |
| `AnnualYield` | Süd, flacher Festwinkel (Jahresoptimum). |
| `WinterYield` | Süd, steiler Winterwinkel. |

Bei festem Kickstand snappt `PanelProfile.NearestKickstand` auf den nächsten realen Standwinkel
(PS400 mono: 30/40/50/80°). Stufenlos verstellbare Panels (PS400 Bifazial, generisch) übernehmen den
Optimum-Winkel direkt. Das ViewModel zielt immer auf das Optimum (`TargetTilt`); bei festen Winkeln
erklärt der Hang-Hinweis, die Differenz über die Aufstell-Neigung zu holen (→ [ViewModels](SunSeeker.Shared/ViewModels/CLAUDE.md)).

### Ausricht-Konvention (AlignView)

Das Handy wird flach mit dem Bildschirm an die Panel-**Vorderseite** (zur Sonne) gehalten →
Display-Normale = Panel-Normale, also `PanelAzimuth = HeadingReading.DeviceAzimuth`. Bei zu flacher
Lage ist der Azimut instabil (`AzimuthReliable = false`) → die UI fordert zum Neigen auf.

---

## Anker-Hardware-Anbindung (Live-Watt)

Die C2000 Gen 2 (A1783) hat **keine offizielle API**; Live-Solar-Watt kommen über Ankers
Cloud-MQTT (inoffiziell, reverse-engineered — `thomluther/anker-solix-api`). Implementiert in
`SunSeeker.Shared/Services/Anker/` (`AnkerMonitorService`): Login (ECDH/AES) → Geräteliste →
mTLS-MQTT (Port 8883) → A1783-Feld `a6/04` = DC-Eingang/Watt. Zugangsdaten gibt der Nutzer im
Leistungs-Tab ein; ohne Zugangsdaten läuft der `MockAnkerMonitorService` als Demo (`IsSimulated`).
Architektur, Flow + Gotchas (Google-OAuth-Passwort, a6-TLV, mTLS, Trigger) →
[Services/Anker/CLAUDE.md](SunSeeker.Shared/Services/Anker/CLAUDE.md).

---

## AR-Sonnenbahn-Overlay (Kamera)

„Sonne durch die Kamera": blendet Tagesbahn + aktuelle Sonnenposition über das Live-Kamerabild ein.
Kein ARCore-Tracking — Sonnenrichtung aus Ort + Zeit, Bildposition aus Rotationsvektor-Sensor + FOV.
Native CameraX-Activity in `SunSeeker.Android/Ar/`, testbare Projektion in
`SunSeeker.Shared/Services/SunArProjection.cs`. Einstieg: Button im Übersicht-Tab (nur Android, via
`App.LaunchSunAr`-Hook). Details → [Ar/CLAUDE.md](SunSeeker.Android/Ar/CLAUDE.md).

---

## Offene Punkte

- Keine offenen Hardware-Punkte. (PS400 Bifazial verifiziert: **stufenlos verstellbarer** Standwinkel,
  kein fester 35°-Kickstand.)

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
