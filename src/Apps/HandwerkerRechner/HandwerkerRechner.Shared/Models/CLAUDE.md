# Models — Domänen-Modelle & Berechnungs-Engine

Enthält die Domänenklassen (reine Daten) und die `CraftEngine` (Berechnungslogik).
Namespace: `HandwerkerRechner.Models`.
Conventions, Architektur und Patterns → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `CraftEngine.cs` | 28 Berechnungsalgorithmen + Result-Records + Enums. Schutz gegen Infinity/NaN. |
| `Project.cs` | Gespeichertes Handwerker-Projekt: GUID-ID, Name, CalculatorType, DataJson, Fotos, Notizen. |
| `ProjectTemplate.cs` | Vorlage mit `TemplateCalculatorEntry`-Liste (Route, CalculatorType, DefaultValues-Dictionary). |
| `MaterialPrice.cs` | Material-Preis mit `DefaultPrice` (Deutschland-Durchschnitt) und `CustomPrice` (Benutzer-Override). `EffectivePrice` wählt automatisch. |
| `Quote.cs` | Angebot: `QuoteItem`-Liste, MwSt/Marge-Berechnung, `QuoteStatus`-Enum, `QuoteItemType`-Enum. |
| `CalculatorCategory.cs` | Enum `CalculatorCategory` (Gruppen) + Enum `CalculatorType` (28 einzelne Rechner). |

## CraftEngine — Design-Entscheidungen

`CraftEngine` ist ein **Singleton** (in DI registriert), zustandslos (kein Mutable-State),
alle Methoden rein funktional. Calculator-VMs injizieren `CraftEngine` und delegieren alle
Berechnungen dorthin — keine `Math.*`-Direktaufrufe in VMs.

### Plausibilitäts-Bounds (Clamp-Methode)

Schützt gegen `double.Infinity`/`NaN` und `OverflowException` bei User-Extremwerten:

```csharp
// IMMER vor Berechnungen auf Plausibilitätsgrenzen prüfen:
roomLength = Clamp(roomLength, Limits.MaxLengthM);      // 10 km
crossSection = Clamp(crossSection, 1000, 0.001);        // mm²
```

Kein explizites Werfen — Werte werden still auf Plausibilitätsgrenze gesetzt.
Division-durch-Null-Schutz als Inline-Guard: `if (area <= 0) area = 0.001`.

### DIN-konforme Algorithmen

- **Treppen (DIN 18065):** Schrittmaßregel `2h + g = 63 cm`. `IsComfortable` (59–65 cm),
  `IsDinCompliant` (Höhe 14–21 cm, Auftritt 21–35 cm).
- **Kabelquerschnitt (DIN VDE 0100-520):** Standardquerschnitte `{1.5, 2.5, 4.0, …, 120.0}` mm².
  3%-Limit (Beleuchtung), 5% für Steckdosen.
- **Drehstrom-Faktor:** `√3` statt `2` wenn `isThreePhase=true` (400V-Drehstrom-Sternpunkt).
  Gilt sowohl für `CalculateVoltageDrop` als auch `CalculateCableSize`.

### Fugenmasse-Formel (GroutResult)

Industrieformel für Verbrauch in kg/m²:

```
consumptionPerSqm = ((L_mm + B_mm) / (L_mm × B_mm)) × Fugenbreite_mm × Fugentiefe_mm × Dichte
```

Alle Maße intern in mm. Die Formel ergibt direkt kg/m² (ohne zusätzlichen /1000-Faktor —
die Einheiten heben sich heraus). 10% Reserve inkludiert. Eimer à 5 kg als Einheit.

### Result-Records

Alle Berechnungsergebnisse als immutable `record`-Typen (`init`-only Properties).
Kein Zustand in Results — Calculator-VMs cachen nur den jeweils letzten Result.

## Rechner-Übersicht (CalculatorType — 28 Einträge)

| Kategorie | Rechner | FREE/PREMIUM |
|-----------|---------|--------------|
| Boden & Wand | Tiles, Wallpaper, Paint, Flooring, ConcreteSlab, ConcreteStrip, ConcreteColumn | FREE |
| Raum/Trockenbau | DrywallFraming, Baseboard | PREMIUM |
| Elektriker | VoltageDrop, PowerCost, OhmsLaw | PREMIUM |
| Schlosser/Metall | MetalWeight, ThreadDrill | PREMIUM |
| Garten & Landschaft | Paving, Soil, PondLiner | PREMIUM |
| Dach & Solar | RoofPitch, RoofTiles, SolarYield | PREMIUM |
| Treppen | Stairs | PREMIUM |
| Putz | Plaster | PREMIUM |
| Estrich | Screed | PREMIUM |
| Dämmung | Insulation | PREMIUM |
| Leitungsquerschnitt | CableSizing | PREMIUM |
| Fugenmasse | Grout | PREMIUM |
| Profi-Werkzeuge | HourlyRate, MaterialCompare, AreaMeasure | PREMIUM |

## Project — DataJson-Cache

`Project.DataJson` enthält die Calculator-Eingaben als JSON. `GetValue<T>(key)` ist gecacht
(`_cachedData`) — vermeidet wiederholte JSON-Deserialisierung bei UI-Bindings.
`SetData()` invalidiert den Cache. `LastModified` wird bei `SetData()` auf `DateTime.UtcNow` gesetzt.

## Quote — Berechnete Properties

Alle Geldwerte in `Quote` sind **berechnete Properties** (kein Persistieren):

```csharp
public double SubtotalNet  => Items.Sum(i => i.Total);
public double MarginAmount => SubtotalNet * MarginPercent / 100;
public double TotalNet     => SubtotalNet + MarginAmount;
public double VatAmount    => TotalNet * VatPercent / 100;
public double TotalGross   => TotalNet + VatAmount;
```

Nur `Items`, `VatPercent`, `MarginPercent` werden persistiert.
