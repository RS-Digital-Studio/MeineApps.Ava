# SK Buch-Konform Refactoring Plan

Ziel: Das BingXBot SK-System **exakt 1:1 zum offiziellen Tradebook** (Sascha Wenzel / Stefan Kassing) umbauen. Nicht mehr und nicht weniger. Für alle Assets gleich.

**Ausnahmen (nach Robert):** Kein Tages-Risiko-Limit, kein Gleicher-Tag-Exit.

## Richtungs-Prinzipien
- **KEINE Bot-spezifischen Erweiterungen** (Multi-Tier, BTC-Health, externe Confluence, Bottom-Up Feedback, Position-Scale-Boni, gestaffeltes RRR, ATR-SL).
- **ALLE fehlenden Buch-Features** implementieren (M30-Primär, Weekly, Pip-SL, SL-Halbierung, TP-Prioritäten 1-5, BCKL-Re-Entry, Multi-Entry-Staffelung, BE-Exit → Re-Entry, 1% Risiko).
- **Einheitlich für alle Assets** (Crypto + Forex + Commodity + Index + Stock) nach den kategorie-spezifischen Buch-Regeln.

---

## Phase A: Entfernungen (Simplifizierung)

### A1 — Multi-Tier Holy Trinity → Single-Tier M30
**Datei:** `src/Libraries/BingXBot.Engine/Strategies/SequenzKonzeptStrategy.cs`
- Klasse komplett neu schreiben: nur 1 Tier (Weekly/Daily/H4/H1/M30)
- `EvaluateTier()` entfernen, `Evaluate()` arbeitet direkt
- Tier1/2/3 Direction-Tracking raus (`_lastTier1Direction`, `_lastTier2Direction`)
- `EvaluateTier`-Parameter `vetoDirection`, `categoryFactor` raus
- `TradingTier` Enum entfernen oder auf Dummy reduzieren
- `AmpelStatus` vereinfachen: W1/D1/H4/H1/M30 statt H4/H1/M15

**Datei:** `src/Libraries/BingXBot.Core/Models/MarketContext.cs`
- `M5Candles`, `M1Candles` entfernen
- `ExternalData` entfernen
- `EntryTimeframeCandles` = M30 (statt M15)
- Neue Property: `WeeklyCandles`

**Datei:** `src/Apps/BingXBot/BingXBot.Shared/Services/ScanHelper.cs`
- M5/M1-Candles-Loading entfernen
- M30 statt M15 als Entry-Chart
- Weekly-Candles ergänzen

### A2 — Externe Marktdaten weg
**Löschen:**
- `src/Libraries/BingXBot.Engine/External/` (ganzer Ordner)
- `src/Libraries/BingXBot.Core/Models/ExternalMarketData.cs`

**Referenzen entfernen in:**
- `SequenzKonzeptStrategy.cs` (Zeile ~1007-1133, externe Confluence-Quellen)
- `MarketContext.cs` (ExternalData-Parameter)
- `App.axaml.cs` (DI-Registrierung)
- `DashboardViewModel.cs` (Verwendung)
- `TradingServiceBase.cs` (Verwendung)

### A3 — BTC-Health-Score + MarketFilter raus
**Datei:** `src/Libraries/BingXBot.Engine/...` → MarketFilter finden und BTC-Health-Teil entfernen (der Rest kann bleiben wenn er unabhängig ist)

### A4 — Confluence-Score auf Buch-Level reduzieren
**Datei:** `SequenzKonzeptStrategy.cs`
- 20+ Confluence-Quellen → 3-4 Buch-Bestätigungen:
  1. Sequenz aktiviert (Close über Punkt A)
  2. Richtung valide (kein Punkt-0-Bruch)
  3. B im 50-66.7% Golden Pocket
  4. 100er Extension überschritten (optional)
- `_consecutiveFailsInDirection` raus (Zeile 58-60)
- B-Punkt PositionScale raus (Zeile ~1186-1193)
- SK Score-basierter Position-Boost raus (100%/125%)
- Gestaffeltes Min-RRR raus → nur min 1:1 (Buch)

### A5 — Asset-Kategorie-spezifische Parameter einheitlich
**Datei:** `SequenzKonzeptStrategy.cs`
- `categoryFactor` (0.25/0.5/0.4/0.6/1.0) weg — alle Assets gleich
- `crashThreshold` Kategorie-skaliert weg
- Alle Kategorie-Multiplikatoren einheitlich

---

## Phase B: Buch-Features hinzufügen

### B1 — M30 als Primär-Entry-Chart
**Datei:** `ScannerSettings.cs`
- `ScanTimeFrame = TimeFrame.H4` (bleibt, ist Navigator)
- **Entry-TF fest auf M30** in ScanHelper verdrahten (nicht mehr via UseM15EntryTiming)
- `EnableTier2Intraday`, `EnableTier3Scalp`, `UseM15EntryTiming` entfernen

**Datei:** `ScanHelper.cs`
- M30-Candles laden (1000 Candles für Fib-Berechnung)
- M15/M5/M1 komplett raus

### B2 — Weekly-Analyse als Fahrplan-Top-Level
**Datei:** `SequenzKonzeptStrategy.cs`
- Weekly-Candles prüfen: In Weekly-GKL? Dann Weekly-Richtung bevorzugen
- Fallback auf Daily → H4 EMA-200

**Datei:** `ScanHelper.cs`
- Weekly-Candles (50+) laden

### B3 — Feste Pip-SL pro Asset-Klasse
**Neue Datei:** `src/Libraries/BingXBot.Engine/Risk/PipStopLossCalculator.cs`
```csharp
public static decimal CalculatePipStopLoss(string symbol, MarketCategory category, decimal entryPrice, bool isLong)
{
    var pipValue = GetPipValue(symbol, category);
    var pipsSL = category switch
    {
        MarketCategory.Forex or MarketCategory.Commodity when IsMetal(symbol) => 20m,
        MarketCategory.Forex when IsGbpPair(symbol) => 30m,   // GBP höher
        MarketCategory.Forex when IsExoticPair(symbol) => 30m, // AUD/CAD/NZD
        MarketCategory.Forex => 20m,
        MarketCategory.Commodity => 40m, // Öl
        MarketCategory.Index => 40m,
        MarketCategory.Stock => 40m,
        MarketCategory.Crypto => 100m,
        _ => 20m
    };
    var slDistance = pipsSL * pipValue;
    return isLong ? entryPrice - slDistance : entryPrice + slDistance;
}

private static decimal GetPipValue(string symbol, MarketCategory cat) => cat switch
{
    MarketCategory.Forex when symbol.Contains("JPY") => 0.01m,
    MarketCategory.Forex => 0.0001m,
    MarketCategory.Commodity when IsMetal(symbol) => 0.1m,
    MarketCategory.Commodity => 0.01m, // Öl
    MarketCategory.Index => 1m,
    MarketCategory.Crypto => _ => 0.0001m * entryPrice, // Krypto: 1/10000 des Preises als Pip
    _ => 0.0001m
};
```

### B4 — SL-Halbierung
**Datei:** `src/Apps/BingXBot/BingXBot.Shared/Services/TradingServiceBase.cs`
- Neue Stufe: Wenn Trade ≥ 1× SL-Distanz im Gewinn → SL auf halbe SL-Distanz von Entry (nicht BE)
- Bestehende 2×-BE-Regel bleibt als nachfolgende Stufe
- Max 5 Pips nach Reduzierung prüfen (Buch)

### B5 — TP-Prioritäten 1-5
**Datei:** `SequenzKonzeptStrategy.cs`
- Aktuell: TP1=161.8%, TP2=161.8% Navigator
- Neu: 
  - **TP1 = 100% Extension** (konservativ, Buch Prio 3)
  - **TP2 = 161.8% Extension** (Hauptziel, Buch Prio 4)
  - Optional: Bullen-/Bärenkorrektur-Level als erster TP-Check (Buch Prio 2) — bei niedrigem RRR
  - Dailyrange als Fallback-TP (Buch Prio 5)

### B6 — BCKL als eigenständiger Re-Entry
**Datei:** `SequenzKonzeptStrategy.cs`
- Wenn 100er-Extension erreicht + Preis korrigiert in BCKL (50-66.7% C→100%-Strecke) → neuer Entry
- SL unter Mikro-Point-C (neues Tief nach 100er-Abpraller)
- TP: 161.8% Extension der ursprünglichen Sequenz

**Datei:** `SequenceDetector.cs` (BCKL-Berechnung existiert bereits)

### B7 — Multi-Entry Staffelung
**Datei:** `SequenzKonzeptStrategy.cs` + `SignalResult.cs`
- Neue SignalResult-Property: `AdditionalEntry` (bool)
- Im 50er-Bereich voller Entry, im 66.7er-Bereich halber zusätzlicher Entry
- SequenceStateMachine merkt sich `FirstEntryTriggered`

**Datei:** `TradingServiceBase.cs`
- Bei `AdditionalEntry=true` zweite Position mit halber Size eröffnen (gleicher SL)

### B8 — BE-Exit → sofortiger Re-Entry
**Datei:** `TradingServiceBase.cs`
- Exit-Reason erweitern: `BeExit` vs. `SlExit`
- Bei BE-Exit: Symbol-Cooldown SKIPPEN (4h Pause nur bei echtem Verlust)

### B9 — Max 1% Risiko pro Trade
**Datei:** `RiskSettings.cs`
- `MaxMarginPerTradePercent` von 10 → **1** 
- `CategorySettings` alle auf 1% Risk
- `MaxPositionSizePercent` auf 3% (Ziel-Size, nicht Risk)

---

## Phase C: Settings-Cleanup

### C1 — TradingModeDefaults vereinfachen
**Datei:** `TradingModeDefaults.cs`
- Nur noch 1 Modus: SK-System (Swing)
- Scalping + DayTrading Presets entfernen
- `TradingModePreset` Enum auf 1 Wert reduzieren (oder komplett entfernen)
- Vol-adaptive SL-Multiplikatoren weg (feste Pips)

### C2 — RiskSettings bereinigen
**Datei:** `RiskSettings.cs`
- `MaxOpenPositionsTier1/2/3` raus
- Nur `MaxOpenPositions` (= Total)
- `EnableEquityCurveTrading`, `EnableMomentumDecay`, `ConsiderFundingRate` raus
- `MaxDailyDrawdownPercent` bleibt (Robert will kein Tages-Limit, aber Schalter bleibt für andere Strategien)

### C3 — ScannerSettings bereinigen
**Datei:** `ScannerSettings.cs`
- `EnableTier2Intraday`, `EnableTier3Scalp` raus
- `UseM15EntryTiming` → `UseM30EntryTiming` (immer true)

---

## Phase D: Build + Verify + Doku

### D1 — Build testen
```bash
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln
```

### D2 — AppChecker
```bash
dotnet run --project tools/AppChecker BingXBot
```

### D3 — Gotchas prüfen
Alle SK-Gotchas in Haupt-CLAUDE.md durchgehen, irrelevante entfernen, neue ergänzen.

### D4 — CLAUDE.md aktualisieren
- `src/Apps/BingXBot/CLAUDE.md`: Multi-Tier raus, M30-Primär, Pip-SL-Tabelle, TP-Prioritäten, neue Architektur
- Haupt-`CLAUDE.md`: Gotchas-Tabelle aktualisieren

---

## Betroffene Dateien (geschätzt)

**Neu:**
- `src/Libraries/BingXBot.Engine/Risk/PipStopLossCalculator.cs`

**Massiv geändert:**
- `src/Libraries/BingXBot.Engine/Strategies/SequenzKonzeptStrategy.cs` (~1358 Zeilen → ~600)
- `src/Libraries/BingXBot.Core/Models/MarketContext.cs`
- `src/Apps/BingXBot/BingXBot.Shared/Services/ScanHelper.cs`
- `src/Apps/BingXBot/BingXBot.Shared/Services/TradingServiceBase.cs`
- `src/Libraries/BingXBot.Core/Configuration/RiskSettings.cs`
- `src/Libraries/BingXBot.Core/Configuration/ScannerSettings.cs`
- `src/Libraries/BingXBot.Core/Configuration/TradingModeDefaults.cs`
- `src/Libraries/BingXBot.Core/Models/SignalResult.cs`

**Gelöscht:**
- `src/Libraries/BingXBot.Engine/External/` (ganzer Ordner)
- `src/Libraries/BingXBot.Core/Models/ExternalMarketData.cs`

**Kleinere Anpassungen:**
- `src/Apps/BingXBot/BingXBot.Shared/App.axaml.cs` (DI)
- `src/Apps/BingXBot/BingXBot.Shared/ViewModels/DashboardViewModel.cs` (ExternalData-Referenzen)
- `src/Libraries/BingXBot.Engine/Indicators/` (ggf. MarketFilter BTC-Health)

---

## Reihenfolge der Umsetzung (dependency-aware)

1. Plan-Datei ✓
2. A1 + A2 parallel (Multi-Tier + ExternalData entfernen — minimiert Complexity)
3. A3 + A5 (BTC-Health, Category-Factor)
4. A4 (Confluence-Reduktion)
5. C1 + C2 + C3 (Settings zuerst, bevor B-Phase Settings-Referenzen verändert)
6. B1 (M30 primär) — erfordert A1 (Multi-Tier weg)
7. B2 (Weekly)
8. B3 (Pip-SL)
9. B4 + B5 (SL-Halbierung + TP-Prioritäten)
10. B6 + B7 (BCKL + Multi-Entry)
11. B8 (BE-Exit Re-Entry)
12. B9 (1% Risiko)
13. D (Build + Doku)
