# SunSeeker.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Kein AdMob/IAP** — privates Projekt. Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia-Bootstrap einmal pro Prozess. `WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity`. Setzt `App.LocationServiceFactory` + `App.HeadingServiceFactory` VOR `base.OnCreate`, fragt Location-Permission an, startet GPS nach Grant. |
| `Services/AndroidLocationService.cs` | `ILocationService` via nativem `LocationManager` (GPS + Network). Kein Google Play Services — Kilometer-Genauigkeit genügt fürs Sonnenstand. `ILocationListener`. |
| `Services/AndroidHeadingService.cs` | `IHeadingService` via `SensorManager`: `RotationVector` → Azimut der Display-Normale + Neigung, `GeomagneticField`-Missweisung (`SetLocation`), Accuracy-Status. `ISensorEventListener`. |
| `AndroidManifest.xml` | Permissions: Internet, ACCESS_FINE/COARSE_LOCATION. AD_ID entfernt (privat). |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar` (Edge-to-Edge) + `MyTheme.Fullscreen` (für spätere AR-Kamera). |
| `Resources/mipmap-*` | App-Icon (aktuell **Platzhalter** von SmartMeasure — eigenes Sonnen-Icon offen). |

---

## MainActivity — Reihenfolge in `OnCreate`

**VOR `base.OnCreate`** (DI-Build läuft auf Application-Ebene davor — siehe Shared-CLAUDE.md):

```
_locationService = new AndroidLocationService(this)
_headingService  = new AndroidHeadingService(this)
App.LocationServiceFactory = _ => _locationService
App.HeadingServiceFactory  = _ => _headingService
```

**NACH `base.OnCreate`:** `RequestLocationPermissionIfNeeded()` (natives `CheckSelfPermission`/
`RequestPermissions`, kein AndroidX nötig). Nach Grant: `_locationService.Start()`.

---

## Sensor-Konventionen

- **Position:** nativer `LocationManager` (GPS + Network, 5 s / 25 m). `Java.Lang.SecurityException`
  vor Permission-Grant abfangen.
- **Heading:** Geräte-Z-Achse (Display-Normale) in Weltkoordinaten aus der Rotationsmatrix
  (3. Spalte); Azimut = `atan2(zx, zy)`, Neigung = `atan2(horizontal, |zz|)`. Bei flacher Lage
  (horizontale Projektion < 0,12) ist der Azimut unzuverlässig. Magnetisch → geografisch via
  `GeomagneticField.Declination`. Auf ~20 Hz gedrosselt.
- Sensor-Events kommen vom Sensor-Thread → Konsument (`AlignViewModel`) marshallt via `Dispatcher.UIThread.Post`.

---

## Build

```bash
dotnet build   src/Apps/SunSeeker/SunSeeker.Android
dotnet build   src/Apps/SunSeeker/SunSeeker.Android -t:Run   # auf angeschlossenes Gerät
```
