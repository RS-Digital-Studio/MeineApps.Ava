# SunSeeker — Solarpanel-Ausrichtung (Anker SOLIX C2000 / PS400 Bifazial)

Privates Projekt (nicht im Play Store). Richtet ein mobiles Solarpanel optimal zur Sonne aus
und maximiert den bifazialen Mehrertrag. Berechnet den Sonnenstand vollstaendig offline,
empfiehlt Himmelsrichtung + Neigung je Nutzungsziel und bewertet die Ist-Ausrichtung live
(Einfallswinkel / cosine-loss). Speziell fuer das **bifaziale** Anker PS400: Untergrund-/Albedo-
Hinweise, Steilwinkel-Empfehlung und Rueckseiten-Freiheit.

| Aspekt | Wert |
|--------|------|
| Plattformen | Desktop (Entwicklung/Mock) + Android (geplant: Samsung Galaxy S25 Ultra) |
| Vertrieb | Privat, kein AdMob/IAP |
| Ziel-Hardware | Anker SOLIX PS400 (Bifazial, fester 35-Grad-Kickstand) + C2000 Gen 2 Powerstation |

Generische Build-Befehle, Conventions, Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Architektur-Ueberblick

Drei Projekte (analog SmartMeasure), ViewModel-First, kein Service-Locator. Aktuell sind
**Shared + Desktop** umgesetzt; **Android** (Host + Sensoren) folgt.

```
SunSeeker.Android (geplant) ┐
                            ├─> SunSeeker.Shared ──> MeineApps.Core.Ava  (Preferences, Localization, ViewLocator)
SunSeeker.Desktop ──────────┘                    └─> MeineApps.UI        (SkiaSharp-Helpers)
```

Composition-Flow: Host (`Program.cs` / geplant `AndroidApp`) → `SunSeeker.Shared/App.axaml.cs`
(Factory-Properties → DI-Build → `MainViewModel`) → `MainView`. Desktop nutzt `MockLocationService`
statt echtem GPS. Plattform-Factories werden LAZY im Resolve-Lambda ausgewertet (Avalonia-12-
Android-Reihenfolge → [Haupt-CLAUDE.md](../../../CLAUDE.md), Abschnitt „Platform-Factory-Registrierung").

---

## Kern-Engine (plattformneutral, testbar)

Die gesamte Solar-Mathematik ist reine Berechnung in `SunSeeker.Shared/Services/` — ohne
Plattform-API, vollstaendig unit-getestet (`tests/SunSeeker.Tests`).

| Service | Zweck |
|---------|-------|
| `ISolarPositionService` / `SolarPositionService` | Sonnenstand (Azimut/Elevation), Sonnenzeiten, Tagesbahn. NOAA Solar Calculator (Meeus-basiert), inkl. Zeitgleichung + atmosphaerischer Refraktion. Genauigkeit < 0,01 Grad (1800-2100). |
| `IAlignmentService` / `AlignmentService` | Soll-Ausrichtung je `AlignmentGoal` + Live-Einfallswinkel (cosine-loss, PVPMC/Sandia-Formel). |
| `IBifacialService` / `BifacialService` | Albedo, Mehrertrags-Bereich, Steilwinkel-Zuschlag, Aufstell-Tipps. |
| `ILocationService` / `MockLocationService` | Position. Android-Impl (FusedLocation/GPS) folgt; Desktop = fester Standort. |
| `SunMath` (internal static) | Geteilte Winkel-/Zeit-Mathematik (Deg/Rad, Normalisierung, Julianisches Datum). |

### Solar-Domaenenwissen (Fakten hinter der Engine)

- **Optimaler Azimut**: aequatorwaerts (Sued auf der Nord-, Nord auf der Suedhalbkugel). Abweichung
  ist ertragstolerant (±15° < 1 % Verlust). Geografisch Sued, nicht magnetisch (Missweisung beachten).
- **Optimaler Tilt (DE, ~52°)**: Jahresertrag ~30-43° (FLACHER als der Breitengrad — Diffuslicht im
  Winter), Winter ~63-65°, Sommer ~25°. Faustformeln aus solarpaneltilt.com (in `AlignmentService`).
- **Einfallswinkel (AOI)**: `cos(AOI) = sin(elev)·cos(tilt) + cos(elev)·sin(tilt)·cos(sonnenAz − panelAz)`.
  Direkt-Ertrag ∝ cos(AOI); negativ = Sonne hinter dem Panel.
- **Bifazial**: Mehrertrag haengt v.a. von der Albedo des Untergrunds ab („doppelte Albedo ≈
  doppelter Mehrertrag"). Hoehere Albedo → steilere Neigung optimal (Rueckseite sieht mehr Boden).
  Mehrertrag wird als BEREICH angegeben, nie als Punktwert (Pseudo-Genauigkeit vermeiden).

### `AlignmentGoal`

| Modus | Soll-Ausrichtung |
|-------|------------------|
| `NowMaximum` | Direkt auf die aktuelle Sonne (Azimut = Sonnen-Azimut, Neigung = Zenitwinkel). Fuer mobiles Panel. |
| `TodayYield` | Sued, Neigung senkrecht zur heutigen Mittagssonne. |
| `AnnualYield` | Sued, flacher Festwinkel (Jahresoptimum). |
| `WinterYield` | Sued, steiler Winterwinkel. |

Bei festem Kickstand (PS400) snappt `PanelProfile.NearestKickstand` auf den naechsten realen
Standwinkel (PS400 Bifazial: fix 35°; PS400 mono: 30/40/50/80°).

---

## Anker-Hardware-Anbindung (geplant)

Die C2000 Gen 2 (A1783) hat BLE + WLAN; **keine offizielle API**. Live-Solar-Watt sind ueber
Ankers Cloud-MQTT erreichbar (inoffiziell, reverse-engineered — `thomluther/anker-solix-api`,
PV-Feld `a6` = `dc_input_power`/Watt). Anker-Account noetig, jederzeit von Anker kappbar. Die
lokale BLE-Lib unterstuetzt die C2000 Gen 2 nicht. Eine .NET-Reimplementierung (Cloud-Login +
MQTT-Subscribe) ist der risikoaermere Weg gegenueber eigenem BLE-Reverse-Engineering.

---

## Noch nicht implementiert

- **SunSeeker.Android**: Host (`AndroidApp`/`MainActivity`), Resources/Icons/Theme,
  `AndroidLocationService` (FusedLocationProvider), `AndroidHeadingService` (Magnetometer +
  `GeomagneticField`-Missweisung + Neigung via Gravity-Sensor).
- **Sonnenkompass**: Azimut ueber GPS + Zeit + Sonnenstand (Nutzer visiert die Sonne/den Schatten
  an) — robuster als der Magnetkompass, der eigentliche USP.
- **Live-Ausricht-UI**: SkiaSharp-Kompassring (Ist vs. Soll-Azimut) + Neigungs-Anzeige + haptisches
  Einrast-Feedback. Tab-Navigation (`MainViewModel` ist aktuell ein Dashboard).
- **Anker-Live-Watt**: `IAnkerMonitorService` (MQTT-Cloud) + Ertrags-Logging/History.
- **AR-Sonnenbahn**: ARCore-Overlay der Sonnenbahn + Verschattungs-Check (Brueckenmuster aus
  SmartMeasure `Ar/` wiederverwendbar).
- **Lokalisierung**: UI-Strings sind aktuell deutsch hardcodiert; RESX (6 Sprachen) folgt.

---

## Build

```bash
dotnet build src/Apps/SunSeeker/SunSeeker.Desktop
dotnet run   --project src/Apps/SunSeeker/SunSeeker.Desktop
dotnet test  tests/SunSeeker.Tests
```

---

## Verweise

- DI/MVVM/DateTime/Thread-Safety, Naming, Localization, Avalonia-12-Patterns → [Haupt-CLAUDE.md](../../../CLAUDE.md)
- SkiaSharp/Rendering → [MeineApps.UI](../../../UI/MeineApps.UI/CLAUDE.md)
- Avalonia/MVVM/Android-Framework-Fallstricke → [MeineApps.Core.Ava](../../../Libraries/MeineApps.Core.Ava/CLAUDE.md)
- SmartMeasure (Vorlage fuer GPS/ARCore/BLE/Sensoren) → [SmartMeasure](../SmartMeasure/CLAUDE.md)
