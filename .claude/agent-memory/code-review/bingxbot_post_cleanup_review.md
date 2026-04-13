---
name: BingXBot Post-Cleanup Review April 2026
description: Review nach massivem ATI+Non-Buch-Cleanup. 8 Findings (2 krit): Backtest vs Live BE-Divergenz (nutzt PartialClosed statt SlHalved), Scalping/DayTrading Tp1Close=1.0 deaktiviert Partial-Close, Zombie-Felder, ScanHelper EntryTF M15 statt M30
type: project
---

# BingXBot Post-Cleanup Review (12.04.2026)

Nach ATI- und Non-Buch-Feature-Entfernung (73 Dateien, ~14000 LOC gelöscht) gefunden:

## Kritisch

### K1 — Backtest BE-Logik nutzt PartialClosed als Guard statt SlHalved
`src/Libraries/BingXBot.Backtest/BacktestEngine.cs:300, 308`
Live (TradingServiceBase.cs:415, 427) prüft `!skState.SlHalved` für Stufe 1 (SL halbieren) und `!skState.BreakevenSet` für Stufe 2 (BE). Backtest prüft in beiden Stufen nur `!exitState.PartialClosed` — der Guard feuert falsch, weil PartialClosed NUR nach TP1-Hit true wird, nicht nach SL-Halbierung. Folge: Stufe 1 feuert 100x hintereinander (jeder Tick setzt SL wieder auf halbiert, Position läuft nicht in Stufe 2). Backtest hat keine BacktestExitState.SlHalved/BreakevenSet-Felder.
Fix: BacktestExitState.SlHalved + BreakevenSet Flags ergänzen, Guards umstellen.

### K2 — Scalping/DayTrading: Tp1CloseRatio=1.0m deaktiviert Partial-Close komplett
`src/Libraries/BingXBot.Core/Configuration/TradingModeDefaults.cs:19, 27`
TP1CloseRatio=1.0, TP2CloseRatio=0. TradingServiceBase.cs:449 `if (Tp1CloseRatio > 0 && Tp1CloseRatio < 1m)` überspringt damit Partial-Close. Die Position wartet nur auf TP1, wird dort aber nicht geschlossen (weil die Bedingung false ist). SL/TP-Standard-Check fängt es → Trade schließt erst bei TP1 als Full-Close. Dagegen: SequenzKonzeptStrategy setzt IMMER TakeProfit2 (161.8%=TP1 und 200%=TP2). In Scalping/DayTrading wird TP2 nie angefahren — der Rest der Position wird nie optimal geschlossen. Inkonsistent mit SK-Buch-Flow. Zudem: Wenn die Modi wirklich nur Single-Close machen sollen, müsste Strategy TakeProfit2=null setzen.
Fix: Tp1CloseRatio=0.5m + Tp2CloseRatio=0.5m in ALLEN Presets (Buch-konform), oder Modi entfernen.

## Wichtig

### W1 — ScanHelper lädt M15 statt M30 als Entry-TF
`src/Apps/BingXBot/BingXBot.Shared/Services/ScanHelper.cs:142`
`entryTfCandles = GetKlinesAsync(..., TimeFrame.M15, ...)`. Strategy-Kommentar sagt "M15 für DayTrading/Swing" aber das Buch und die SK-Strategy erwarten M30. Wird zwar vom TradingServiceBase.cs:682 korrekt mit M30 überschrieben (ScanHelper.EvaluateCandidateAsync wird nicht aufgerufen), aber ScanHelper ist toter/inkonsistenter Code, der beim nächsten Missgriff verwendet wird. 
Fix: ScanHelper.EvaluateCandidateAsync entfernen oder M15 → M30 + DailyCandles + WeeklyCandles ergänzen.

### W2 — Zombie-Felder in RiskPreset Record
`src/Libraries/BingXBot.Core/Configuration/TradingModeDefaults.cs:82-86`
`RiskPreset` hat noch `MaxHoldHoursAfterTp1` und `SmartBreakevenAtrMultiplier` als Parameter. Beide RiskSettings-Felder wurden gemäß SK_VERIFY_REPORT.md entfernt. Die Werte werden in MultiModeOrchestrator.cs:385-411 nicht mehr zugewiesen. Tote Parameter im Record.
Fix: Beide Parameter aus RiskPreset-Record entfernen, alle `new(...)`-Aufrufe anpassen.

### W3 — Zombie-Property `_trailingStop` in PositionDisplayItem
`src/Apps/BingXBot/BingXBot.Shared/ViewModels/DashboardViewModel.cs:1472`
`[ObservableProperty] private decimal? _trailingStop; // In Prozent` — wird nie gesetzt, in keiner View gebunden (grep in DashboardView.axaml → 0 Matches für `TrailingStop`). Auch Kommentar Zeile 1456 und 1469 ("Trailing-Felder" / "SL/TP/Trailing") verwirren.
Fix: _trailingStop + Kommentare entfernen.

### W4 — ConfluenceScore in Signal für UI noch nötig, aber Backtest nutzt TakeProfit2 nicht bei Re-Set
`src/Libraries/BingXBot.Backtest/BacktestEngine.cs:446`
`exitTracking[key] = new BacktestExitState { ... Tp2 = signal.TakeProfit2, ... }`. TP2 wird gespeichert, aber Live (TradingServiceBase.cs:855) bekommt Tp2 via `signal.TakeProfit2` in PositionExitState. Wenn signal.TakeProfit2=null (Scalping-Strategy-Variante), bleibt in Live die Position nach TP1-Hit ohne neues Ziel → `_positionSignals[key] = signal with { TakeProfit = exitState.Tp2 }` setzt TakeProfit auf null → nur noch SL greift.
Fix: In TradingServiceBase.cs:461 und BacktestEngine.cs:337 Guard `if (exitState.Tp2.HasValue)` sonst: Full-Close statt Partial.

## Minor

### M1 — Kommentar in DashboardViewModel.cs:797, 1197, 1347 referenziert noch ATI
Kommentare wie "CompletedTrade erstellen damit ATI + RiskManager Feedback bekommen", "ATI-Lernfortschritt + Rolling-Metriken", "Speichert auch den ATI-Lernzustand (Auto-Save alle 5 Min, Paper + Live)". ATI ist entfernt, nur RollingMetrics bleiben.
Fix: Kommentare bereinigen — "RiskManager Feedback" / "Rolling-Metriken".

### M2 — DB-Migration v7 nicht idempotent bei SchemaVersion-Fallback
`src/Apps/BingXBot/BingXBot.Shared/Services/BotDatabaseService.cs:54`
`currentVersion = 1` Default, aber Alt-User mit v6 haben "SchemaVersion"-Eintrag = 6 → läuft nur v6→v7 (v7 DROP FeatureSnapshots). Brandneuer User: v1→v7, überspringt v1→v6 Migrations korrekt (nur WAL-Modus wichtig, Zeile 71). Edge-Case: User auf v4 (hatte früher FeatureSnapshots+Cross-Market-Cols aber kein WAL) → v4→v7 überspringt v5 (FibProximity), macht v6 (WAL) + v7 (FeatureSnapshots-DROP). FibProximity-Column bleibt ungenutzt zurück, kein Problem.
Fix: Kein Fix nötig — nur als Beobachtung.

### M3 — MultiModeOrchestrator.RecoverOpenPositionsAsync: Kommentar "Auto-Breakeven bei Recovery" aber Code macht es nicht
`src/Apps/BingXBot/BingXBot.Shared/Services/MultiModeOrchestrator.cs:432, 456`
Kommentar "Setzt Auto-Breakeven für Positionen die weit genug im Gewinn sind" → Code macht explizit "Hier kein Forced-BE" (Zeile 457). Und `CalculateRecoveryAtrAsync` (Zeile 540) wird nicht mehr verwendet.
Fix: Kommentar anpassen + `CalculateRecoveryAtrAsync` entfernen.

---

## Zusammenfassung
- Build: 0 Fehler, 0 Warnungen
- Tests: 193/193 grün
- 8 Findings (2 krit, 4 wichtig, 2 minor)
- Top-Priorität: K1 (Backtest BE-Divergenz) — wirft Backtest-Ergebnisse gegen Live komplett über den Haufen
