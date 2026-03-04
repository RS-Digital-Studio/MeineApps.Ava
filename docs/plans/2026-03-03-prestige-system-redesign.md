# HandwerkerImperium Prestige-System Redesign

**Datum:** 2026-03-03
**Status:** Entwurf
**Scope:** Vollstaendige Ueberarbeitung (Ansatz 3)

---

## 1. Kritische Bugs

### 1.1 Legende-Worker-Bug (Feature ist kaputt)

**Problem:** `PrestigeService.ResetProgress()` setzt Workshops auf `[Carpenter + StartWorker]` (Zeile 262-268) BEVOR `KeepsBestWorkers()` (Zeile 398-409) die alten Workshop-Worker durchsucht. Die alten Worker sind schon weg.

**Fix:** Vor dem Workshop-Reset die besten Worker pro Workshop-Typ speichern, dann nach dem Reset in den neuen Carpenter zuweisen (oder spaeter freischalten wenn der alte Workshop-Typ wieder gekauft wird).

```csharp
// VOR Workshop-Reset: Beste Worker pro Typ merken
Dictionary<WorkshopType, Worker>? keptWorkers = null;
if (tier.KeepsBestWorkers() && state.Workshops.Count > 0)
{
    keptWorkers = new();
    foreach (var ws in state.Workshops)
    {
        var best = ws.Workers.MaxBy(w => w.Efficiency);
        if (best != null)
            keptWorkers[ws.Type] = best;
    }
}

// ... Workshop-Reset ...

// NACH Workshop-Reset: Gehaltene Worker in den Carpenter stecken
// (oder in GameState.KeptPrestigeWorkers speichern fuer spaeteres Zuweisen)
if (keptWorkers != null)
{
    var carpenter = state.Workshops.First();
    foreach (var (type, worker) in keptWorkers)
    {
        if (type == WorkshopType.Carpenter)
        {
            // Carpenter-Worker ersetzen (wenn besser als Start-Worker)
            if (worker.Efficiency > carpenter.Workers[0].Efficiency)
                carpenter.Workers[0] = worker;
        }
        else
        {
            // Fuer andere Typen: In Reserve speichern
            state.Prestige.KeptWorkers ??= new();
            state.Prestige.KeptWorkers[type.ToString()] = worker;
        }
    }
}
```

**Neues Feld in PrestigeData:**
```csharp
[JsonPropertyName("keptWorkers")]
public Dictionary<string, Worker>? KeptWorkers { get; set; }
```

**Workshop-Kauf:** Beim Freischalten eines Workshops pruefen ob ein KeptWorker existiert und diesen statt des Standard-Workers zuweisen.

### 1.2 Falsche Kommentare (3 Stellen)

| Datei:Zeile | Aktuell | Korrektur |
|-------------|---------|-----------|
| `PrestigeService.cs:8-10` | "3-tier prestige system (Bronze / Silver / Gold)" | "7-Tier Prestige-System (Bronze bis Legende)" |
| `StatisticsViewModel.cs:234` | "Prestige (3-Tier System)" | "Prestige (7-Tier System)" |
| `PrestigeService.cs:95` | "Cap bei 20x" | "Cap bei 200x" |

### 1.3 CLAUDE.md Korrektur

`HandwerkerImperium/CLAUDE.md` Zeile 22: "Soft-Cap ab 2.0x" aendern zu "Hard-Cap bei 200x".

### 1.4 PotentialBonus ohne Tier-Multiplikator

**Problem:** `StatisticsViewModel.cs:241` zeigt `+{potentialPoints} PP` mit Base-PP, nicht den tatsaechlichen Tier-gewichteten Wert.

**Fix:**
```csharp
int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());
PotentialBonus = $"+{tierPoints} PP ({highestTier.GetLocalizationKey()})";
```

---

## 2. Dialog-Redesign (Gewinne-First-Psychologie)

### 2.1 Prestige-Bestaetigung: Neue Struktur

**Aktuell:** Warning-dominiert, Verluste prominent.
**Neu:** Gewinne-dominiert, Verluste sekundaer.

```
+------------------------------------------+
|     [Tier-Icon]  GOLD PRESTIGE           |
|                                          |
|  Du erhaeltst:                           |
|  +100 Prestige-Punkte (x4)              |
|  +50% permanenter Einkommens-Bonus       |
|  Forschung bleibt erhalten!              |
|  Prestige-Shop bleibt!                   |
|                                          |
|  ~ 50% schnellerer Fortschritt           |
|                                          |
|  ----------------------------------------|
|  Wird zurueckgesetzt:                    |
|  Level, Geld, Worker, Workshops          |
|  ----------------------------------------|
|                                          |
|  [  Prestige durchfuehren  ]  (Primaer)  |
|  [  Abbrechen              ]  (Sekundaer) |
+------------------------------------------+
```

**Aenderungen:**
- Ueberschrift: Tier-Name statt "Prestige"
- Gewinne OBEN, gross und positiv formatiert
- Speed-Up prominent anzeigen (macht den Vorteil greifbar)
- Verluste UNTEN, kompakt in einer Zeile
- Button-Text positiv: "Prestige durchfuehren" statt neutrales "Bestaetigen"

### 2.2 Prestige-nicht-verfuegbar: Fortschritt zeigen

**Aktuell:** "Du benoetigst Level 30 (aktuell Level 17)" - trocken.
**Neu:** Fortschrittsbalken + motivierender Text.

```
+------------------------------------------+
|  Prestige noch nicht verfuegbar          |
|                                          |
|  Naechstes Prestige: Bronze              |
|  Level 17 / 30  [========----] 57%      |
|                                          |
|  Noch 13 Level bis zum Bronze Prestige!  |
|  Tipp: Stelle mehr Arbeiter ein und      |
|  erforsche bessere Werkzeuge.            |
+------------------------------------------+
```

---

## 3. Tier-Roadmap (SkiaSharp-Visualisierung)

### 3.1 Horizontale Tier-Leiste

Eine visuell ansprechende Fortschritts-Leiste die alle 7 Tiers zeigt. Platzierung: Oberhalb des Prestige-Shops im Statistik-Tab.

```
[Bronze]--[Silver]--[Gold]--[Platin]--[Diamant]--[Meister]--[Legende]
  1x         2x       1x     (locked)  (locked)   (locked)   (locked)
```

**Renderer:** `PrestigeRoadmapRenderer.cs` in `Graphics/`

**Visuals:**
- Jeder Tier als Kreis/Medaille mit Tier-Farbe und Icon
- Abgeschlossene Tiers: Voll ausgefuellt, Count darunter (z.B. "3x")
- Aktuell verfuegbar: Pulsierender Glow
- Noch gesperrt: Grau/transparent mit Lock-Icon
- Verbindungslinien zwischen Tiers (gefuellt bis zum aktuellen Stand)
- Antippen eines Tiers zeigt Tooltip mit: Level-Anforderung, benoetigte Vorgaenger, Belohnungen (PP-Multiplikator, Permanent-Bonus, was bleibt erhalten)

**Dimensionen:** 48dp Hoehe, volle Breite, in `<Border Classes="Card">` gewrappt.

### 3.2 Tier-Detail-Popup (bei Tap auf Tier)

```
+---------------------------+
|  [Icon]  PLATIN           |
|  Level 500 erforderlich   |
|  2x Gold Prestige noetig  |
|                           |
|  Belohnungen:             |
|  8x Prestige-Punkte       |
|  +100% Permanent-Bonus    |
|                           |
|  Behaelt zusaetzlich:     |
|  + Meisterwerkzeuge       |
+---------------------------+
```

---

## 4. Prestige-Shop Kategorisierung

### 4.1 Kategorien (4 Gruppen)

| Kategorie | Icon | Items |
|-----------|------|-------|
| Einkommen | Cash | pp_income_10/25/50/100 |
| Fortschritt | Star | pp_xp_15/30, pp_crafting_speed |
| Kostenreduktion | TrendingDown | pp_cost_15/30, pp_mood_slow/immunity, pp_upgrade_discount |
| Komfort & Start | Rocket | pp_start_money/big, pp_better_start_worker, pp_start_worker_b, pp_rush_boost, pp_delivery_speed, pp_golden_screw_25, pp_offline_hours, pp_quickjob_limit |

### 4.2 UI-Aenderungen

- Tabs oder Expander pro Kategorie
- Empfohlene Items hervorheben (z.B. "Bester Wert" Badge fuer pp_income_10)
- Bereits gekaufte Items nach unten sortieren (statt gemischt)
- PP-Kosten farbcodiert: Gruen (leistbar), Grau (zu teuer)

### 4.3 Empfehlungs-System

Ein simples `GetRecommendedItem()` das basierend auf aktuellen PP und gekauften Items das beste naechste Item vorschlaegt:
1. Wenn kein Income-Boost: pp_income_10 (5 PP, bester ROI)
2. Wenn kein Cost-Reduction: pp_cost_15 (12 PP, spart sofort)
3. Wenn kein Start-Money: pp_start_money (6 PP, beschleunigt naechsten Prestige)
4. Naechstes ungekaufftes Item nach Kosten-Effizienz

---

## 5. Prestige-Fortschritts-Indikator

### 5.1 Dashboard-Integration

Wenn Prestige NICHT verfuegbar ist, zeigt der Banner statt "Prestige verfuegbar!" einen Fortschritts-Indikator:

```
Naechstes Prestige: Bronze
Level 17/30 [=======-----] 57%
```

Wenn Prestige verfuegbar ist, bleibt der aktuelle Banner mit den PP/Gains.

### 5.2 Properties (MainViewModel)

```csharp
[ObservableProperty] private bool _isPrestigeProgressVisible;
[ObservableProperty] private string _prestigeProgressText = "";
[ObservableProperty] private double _prestigeProgressPercent;
[ObservableProperty] private string _nextPrestigeTierName = "";
```

In `RefreshPrestigeBanner()`:
```csharp
if (!IsPrestigeAvailable)
{
    // Naechsten erreichbaren Tier finden
    var nextTier = FindNextReachableTier(state);
    if (nextTier != PrestigeTier.None)
    {
        int required = nextTier.GetRequiredLevel();
        int current = state.PlayerLevel;
        PrestigeProgressPercent = Math.Clamp((double)current / required, 0, 1);
        NextPrestigeTierName = loc.GetString(nextTier.GetLocalizationKey());
        PrestigeProgressText = $"Level {current}/{required}";
        IsPrestigeProgressVisible = true;
    }
}
```

---

## 6. Prestige-Onboarding

### 6.1 Tooltip beim ersten Prestige

Wenn `state.Prestige.TotalPrestigeCount == 0` und Prestige erstmalig verfuegbar wird:

1. Prestige-Banner blinkt/pulsiert
2. Tooltip-Bubble (wie bei Onboarding): "Prestige zuruecksetzen? Dein Imperium startet neu - aber STAERKER! Tippe hier fuer Details."
3. Bei Tap: Story-Dialog mit Meister Hans Kapitel 19 triggern

### 6.2 Post-Prestige Zusammenfassung

Nach erfolgreichem Prestige ein kurzes Overlay:
```
+------------------------------------------+
|  Bronze Prestige abgeschlossen!          |
|                                          |
|  +5 Prestige-Punkte erhalten            |
|  Neuer Multiplikator: 1.10x             |
|                                          |
|  Tipp: Kaufe im Prestige-Shop           |
|  permanente Verstaerkungen!              |
|                                          |
|  [  Zum Prestige-Shop  ]                |
|  [  Weiterspielen      ]                |
+------------------------------------------+
```

---

## 7. Prestige-History

### 7.1 Datenmodell

Neues Feld in `PrestigeData`:
```csharp
[JsonPropertyName("prestigeHistory")]
public List<PrestigeHistoryEntry> PrestigeHistory { get; set; } = [];
```

```csharp
public class PrestigeHistoryEntry
{
    [JsonPropertyName("tier")] public PrestigeTier Tier { get; set; }
    [JsonPropertyName("points")] public int PointsEarned { get; set; }
    [JsonPropertyName("date")] public DateTime Date { get; set; }
    [JsonPropertyName("level")] public int PlayerLevelAtPrestige { get; set; }
    [JsonPropertyName("money")] public decimal TotalMoneyAtPrestige { get; set; }
}
```

**In DoPrestige():** Vor dem Reset:
```csharp
prestige.PrestigeHistory.Add(new PrestigeHistoryEntry
{
    Tier = tier,
    PointsEarned = tierPoints,
    Date = DateTime.UtcNow,
    PlayerLevelAtPrestige = state.PlayerLevel,
    TotalMoneyAtPrestige = state.TotalMoneyEarned
});
// Max 20 Eintraege behalten
while (prestige.PrestigeHistory.Count > 20)
    prestige.PrestigeHistory.RemoveAt(0);
```

### 7.2 UI (Statistik-Tab)

Kleiner Abschnitt "Letzte Prestiges" unterhalb der Tier-Roadmap:
- Liste der letzten 5-10 Prestiges
- Pro Eintrag: Tier-Icon + Tier-Name + PP erhalten + Datum + Level
- Kompakt als horizontale Cards

---

## 8. Prestige-Prognose

### 8.1 "PP bei aktuellem Stand"

Im Prestige-Banner (wenn verfuegbar) zusaetzlich anzeigen:
```
Aktuell: +100 PP | Wenn du Level 250 erreichst: +400 PP (Gold!)
```

**Logik:** Fuer jeden hoeheren Tier berechnen was der Spieler bekommen wuerde und den naechsten erreichbaren zeigen.

### 8.2 Implementation

```csharp
// In RefreshPrestigeBanner(), wenn IsPrestigeAvailable:
var nextHigherTier = GetNextTierAbove(highestTier);
if (nextHigherTier != PrestigeTier.None)
{
    int nextTierPoints = (int)(potentialPoints * nextHigherTier.GetPointMultiplier());
    var nextName = loc.GetString(nextHigherTier.GetLocalizationKey());
    PrestigePrognoseText = $"Bei {nextName}: +{nextTierPoints} PP";
}
```

---

## 9. Prestige-Meilensteine

### 9.1 Belohnungen fuer Gesamt-Prestige-Count

Neue Achievement-Kategorie "Prestige-Meister":

| Meilenstein | Bedingung | Belohnung |
|-------------|-----------|-----------|
| Neuanfang | 1. Prestige | 10 Goldschrauben |
| Erfahrener Handwerker | 5 Prestiges | 25 Goldschrauben |
| Prestige-Veteran | 10 Prestiges | 50 Goldschrauben + Titel |
| Meister des Neubeginns | 25 Prestiges | 100 Goldschrauben + Titel |
| Prestige-Legende | 50 Prestiges | 200 Goldschrauben + exklusiver Rahmen |

### 9.2 Implementation

In `AchievementService` neue Achievements hinzufuegen (IDs: `ach_prestige_1/5/10/25/50`).
Check-Trigger: `PrestigeCompleted` Event.

---

## 10. Tier-Auswahl fuer erfahrene Spieler

### 10.1 Konzept

Aktuell wird automatisch der hoechste verfuegbare Tier gewaehlt. Erfahrene Spieler koennten aber bewusst einen niedrigeren Tier waehlen wollen (z.B. schnelle Bronze-Runs fuer PP-Farming).

### 10.2 UI

Im Prestige-Dialog: Dropdown/Tabs fuer verfuegbare Tiers mit Live-Vergleich.

```
Verfuegbare Tiers:
[Bronze: +5 PP, +10%]  [Silver: +10 PP, +25%]  [Gold: +100 PP, +50%]
                                                  ^^^ empfohlen
```

### 10.3 Implementation

`ShowPrestigeConfirmationAsync()` bekommt optionalen `PrestigeTier?` Parameter.
Default: `GetHighestAvailableTier()`. Aber der Dialog zeigt alle verfuegbaren Tiers als Tabs.

---

## 11. Datei-Uebersicht (Neue/Geaenderte Dateien)

### Neue Dateien
| Datei | Zweck |
|-------|-------|
| `Graphics/PrestigeRoadmapRenderer.cs` | SkiaSharp Tier-Roadmap |
| `Models/PrestigeHistoryEntry.cs` | History-Datenmodell |

### Geaenderte Dateien
| Datei | Aenderung |
|-------|-----------|
| `Services/PrestigeService.cs` | Bug-Fix (Legende-Worker, Kommentare), History-Tracking |
| `Models/PrestigeData.cs` | PrestigeHistory + KeptWorkers Felder |
| `Models/PrestigeShop.cs` | Kategorie-Feld pro Item |
| `ViewModels/MainViewModel.Dialogs.cs` | Dialog-Redesign, Tier-Auswahl |
| `ViewModels/MainViewModel.Economy.cs` | Fortschritts-Indikator, Prognose |
| `ViewModels/StatisticsViewModel.cs` | Shop-Kategorien, History-Anzeige, PotentialBonus-Fix |
| `Views/StatisticsView.axaml` | Roadmap-Control, Shop-Tabs, History-Section |
| `CLAUDE.md` | Korrektur Soft-Cap, neue Features dokumentieren |
| RESX (6 Sprachen) | ~15 neue Keys |

---

## 12. Implementierungs-Reihenfolge

1. **Bug-Fixes** (Legende-Worker, Kommentare, PotentialBonus) - Grundlage
2. **Dialog-Redesign** (Gewinne-First) - Quick Win, groesster UX-Impact
3. **Fortschritts-Indikator** - Motiviert Spieler
4. **Shop-Kategorisierung** - Bessere Uebersicht
5. **Tier-Roadmap** (SkiaSharp) - Visueller Wow-Faktor
6. **Prestige-History** - Tracking + Zufriedenheit
7. **Onboarding + Post-Prestige** - Erklaerung fuer neue Spieler
8. **Prognose** - Advanced Feature
9. **Meilensteine** - Langzeit-Motivation
10. **Tier-Auswahl** - Power-User Feature
