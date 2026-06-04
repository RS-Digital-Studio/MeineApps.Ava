# Models — Datenmodelle

Immutable `readonly record struct` / `sealed record` (C# 14). Reine Daten, keine Logik außer
trivialen berechneten Properties. Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

| Modell | Zweck |
|--------|-------|
| `GeoLocation` | Lat/Lon/Höhe. `IsNorthernHemisphere`. |
| `SolarPosition` | Azimut/Elevation/Zeit. `Zenith`, `IsDaylight`. |
| `SunTimes` | Auf-/Untergang/Höchststand (UTC), `NoonElevation`, Polartag/-nacht-Flags. |
| `GroundType` (enum) + `GroundTypeExtensions` | Untergrund. `Albedo()` (Literatur-Mittelwerte), `LocKey()` (Lokalisierungs-Key — KEIN deutscher Text). |
| `PanelProfile` (record) | Panel: Nennleistung, bifazial, Kickstand-Winkel. Vordefiniert: `Ps400Bifacial` (fix 35°), `Ps400` (30/40/50/80°), `Generic`. `NearestKickstand`. |
| `AlignmentGoal` (enum) | NowMaximum / TodayYield / AnnualYield / WinterYield. |
| `AlignmentRecommendation` | Soll-Azimut/Tilt/Kickstand + `Goal`. **Keine Erklärung** (UI lokalisiert aus `Goal`). |
| `AlignmentState` | Live: Ist-Azimut/Tilt, signierte Fehler, AOI, `DirectGainFactor`, `SunBehindPanel`, `Quality`. |
| `BifacialAdvice` | Albedo, Gain-Bereich, Steilwinkel-Zuschlag, `Tips` (Lokalisierungs-**Keys**). |
| `PowerSample` | Zeitstempel + Solar-Watt. |

**Sprachneutralität:** Modelle/Enums enthalten keine UI-Texte — `GroundType.LocKey()` und
`BifacialAdvice.Tips` liefern Keys, die der UI-Layer über `GetString` auflöst.
