# Services — Engine + Plattform-Services

Alle Services sind Singleton (DI). Engine-Services sind plattformneutral + unit-getestet;
Plattform-Services haben Interface + Mock (Shared) + Android-Impl (`SunSeeker.Android/Services/`).
Generische Service-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Engine (rein, testbar — keine Plattform-API, keine Lokalisierung)

| Interface / Klasse | Zweck |
|--------------------|-------|
| `ISolarPositionService` / `SolarPositionService` | Sonnenstand (Azimut/Elevation), Sonnenzeiten, Tagesbahn. NOAA Solar Calculator (Meeus) inkl. Zeitgleichung + atmosphärischer Refraktion. < 0,01° (1800-2100). |
| `IAlignmentService` / `AlignmentService` | Soll-Ausrichtung je `AlignmentGoal` (Faustformeln solarpaneltilt.com) + Einfallswinkel/cosine-loss (PVPMC/Sandia). Hängt von `ISolarPositionService` ab. |
| `IBifacialService` / `BifacialService` | Albedo → Mehrertrags-Bereich, Steilwinkel-Zuschlag, Tipp-**Keys**. |
| `SunMath` (internal static) | Deg/Rad, Winkel-Normalisierung, Julianisches Datum (Meeus). |
| `SunArProjection` (static) | Projiziert eine Welt-Richtung (Azimut/Elevation) auf den Kamera-Bildschirm (FOV + Roll) — für das AR-Sonnenbahn-Overlay. Lineare gnomonische Näherung, unit-getestet. Genutzt von `SunSeeker.Android/Ar/`. |

**Sprachneutralität:** Die Engine gibt KEINE fertigen UI-Texte zurück — `AlignmentRecommendation`
hat keine Erklärung (das VM lokalisiert per `Goal`), `BifacialService.GetAdvice().Tips` liefert
Lokalisierungs-**Keys** (z.B. `BifacialTipDarkGround`). So bleibt sie testbar + übersetzbar.

## Plattform-Services (Interface + Mock + Android)

| Interface | Mock (Desktop) | Android (`SunSeeker.Android/Services/`) |
|-----------|----------------|------------------------------------------|
| `ILocationService` | `MockLocationService` | `AndroidLocationService` (`LocationManager`) |
| `IHeadingService` | `MockHeadingService` | `AndroidHeadingService` (`SensorManager`) |
| `IAnkerMonitorService` | `MockAnkerMonitorService` (Watt aus Sonnenstand) | `AnkerMonitorService` (echte Cloud + MQTT, plattformneutral) → [Anker/CLAUDE.md](Anker/CLAUDE.md) |

`AnkerMonitorService` (in `Anker/`) ist die produktive Live-Watt-Anbindung der C2000 Gen 2; ohne
hinterlegte Zugangsdaten fällt sie auf den `MockAnkerMonitorService` als Demo zurück. Details +
Gotchas (Google-OAuth-Passwort, a6-TLV, mTLS) → [Anker/CLAUDE.md](Anker/CLAUDE.md).

`IHeadingService.SetLocation` wird vom `AlignViewModel` bei Standortänderung aufgerufen (Missweisung).

---

## Solar-Mathematik (Kurz-Referenz)

- **Sonnenstand**: NOAA/Meeus, `SolarPositionService` — Julian Day → Deklination + Zeitgleichung →
  Stundenwinkel → Zenit/Elevation/Azimut (+ Refraktion). `GetDayArc` für die Sonnenbahn.
- **Soll-Tilt** (DE-Faustformeln): Jahr `lat·0,76+3,1`, Saison Apr–Okt `lat·0,94−17,0` (2:1-Mittel
  Sommer-/Übergangsformel), Winter `lat·0,875+19,2`, Heute `90−Mittagselevation`,
  Jetzt = Zenitwinkel der aktuellen Sonne. Azimut = äquatorwärts.
- **AOI**: `cos(AOI)=sin(elev)·cos(tilt)+cos(elev)·sin(tilt)·cos(sonnenAz−panelAz)`. Direkt-Ertrag ∝ cos(AOI).
- **Bifazial**: Albedo dominiert; Gain als BEREICH (`albedo·0,20`…`albedo·0,45`, Deckel 0,30),
  Steilwinkel-Zuschlag 0…11° linear über Albedo 0,20…0,85.
