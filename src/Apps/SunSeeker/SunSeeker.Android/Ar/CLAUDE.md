# Ar — AR-Sonnenbahn-Overlay (native Kamera-Activity)

Blendet die Tagesbahn der Sonne + die aktuelle Sonnenposition über das Live-Kamerabild ein.
**Kein ARCore-Tracking** — die Sonnenrichtung ist eine reine Funktion aus Ort + Zeit
(`ISolarPositionService`); die Bildposition ergibt sich aus der Geräteorientierung + Sichtfeld.
Android-Patterns → [../CLAUDE.md](../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `SunArActivity.cs` | `AppCompatActivity` (Theme `MyTheme.Fullscreen`). CameraX-Vorschau (`PreviewView`, Muster wie FitnessRechner) + Rotationsvektor-Sensor + Sonnendaten aus `App.Services`. Fordert CAMERA-Permission selbst an. |
| `SunArOverlayView.cs` | Transparentes `View`-Overlay: zeichnet Tagesbahn (Punkte + Polylinie), Sonnen-Glow, Auf-/Untergangs-Marker, Rand-Pfeil bei Off-Screen-Sonne, Hinweis. Android-Canvas, Paints gecacht. |

Die Projektions-Mathematik (Welt-Richtung → Bildschirm) liegt **testbar in Shared**:
`SunSeeker.Shared/Services/SunArProjection.cs`.

---

## Daten- und Steuerfluss

```
DashboardView "Sonne in der Kamera" → DashboardViewModel.OpenSunArCommand
  → App.LaunchSunAr (Hook, gesetzt in MainActivity vor base.OnCreate)
  → StartActivity(SunArActivity)
SunArActivity.OnCreate: Ort + Sonnenbahn + Auf-/Untergang aus App.Services → Overlay füllen
Rotationsvektor-Sensor (OnSensorChanged): Kamera-Azimut/Elevation/Roll → Overlay → PostInvalidate
5s-Timer: aktuelle Sonnenposition aktualisieren
```

## Sensor → Blickrichtung (geräte-abhängig fein zu justieren)

- `GetRotationMatrixFromVector` → `RemapCoordinateSystem(R, Axis.X, Axis.Z, …)` (Kamera zeigt nach vorn,
  Standard-AR-Remap) → `GetOrientation` → `[0]`=Gier (Azimut), `[1]`=Pitch, `[2]`=Roll.
- Azimut wird per `GeomagneticField.Declination` von magnetisch auf geografisch (true north) korrigiert
  (die Engine liefert Azimute relativ zu true north).
- `CameraElevation = -pitch` (Kamera nach oben → positiv). Vorzeichen von Pitch/Roll sind die
  geräte-getunten Stellen — die **Projektion** (Shared) ist unit-getestet, das Sensor-Mapping wird am
  Gerät verifiziert.

## Gotchas

- **Namespace-Kollisionen** (App-Namespace `SunSeeker.Android` verdeckt das SDK-`Android`):
  `global::Android.Hardware.Axis.X/.Z` voll qualifizieren; `Path` per Alias auf `Android.Graphics.Path`;
  `AndroidX.Camera.Core.Preview` voll qualifizieren (kollidiert mit `Android.Hardware.Preview`).
- **CAMERA-Permission** wird von der Activity selbst angefragt; ohne Grant → `Finish()`. Manifest:
  `CAMERA` + `uses-feature camera required=false` (App läuft auch ohne).
- **FOV** ist mit 62° (horizontal) angenähert; vertikal aus dem Seitenverhältnis abgeleitet. Für ein
  Marker-Overlay genau genug; bei Bedarf aus Camera2-`CameraCharacteristics` verfeinern.
