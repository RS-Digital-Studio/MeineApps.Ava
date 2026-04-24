---
name: BingXBot Backtest Multi-TF Fix Review
description: Review der Multi-TF-Standalone-Reparatur in BacktestEngine (ScannerSettings/W1/D1/NavigatorTimeframe)
type: project
---

# Review Backtest-Fixes (16.04.2026)

**Scope**: BacktestEngine.cs RunAsync nach Multi-TF-Standalone-Refactor. W1/D1/ScannerSettings/NavigatorTimeframe wurden zuvor durch positionelle Record-Argumente auf falsche Slots geschrieben. Jetzt named-args + echte W1/D1-Loads + DailySliceUpTo-Helper.

## Was gut gelöst ist
- Benannte Argumente in allen 3 MarketContext-Aufrufen — robust gegen künftige Record-Erweiterungen.
- `DailySliceUpTo` mit Binary-Search + `CandleSlice` (Zero-Copy) — korrekte Look-Ahead-Prävention.
- `RunAsync`-Signatur blieb backward-compatible (beide neuen Parameter optional).

## Findings

### KRITISCH
Keine.

### WICHTIG

**1. Sub-Iteration nutzt H4-Close für D1/W1-Slice (inkonsistent mit Live-Takt)**
- Datei: `BacktestEngine.cs:296-297` + `:320-321`
- Problem: In der Entry-TF-Sub-Iteration wird `DailySliceUpTo(..., currentCandle.CloseTime)` übergeben, wobei `currentCandle` die H4-Primary-Kerze ist, nicht die gerade durchlaufene M30-Sub-Kerze (`entryCandle`). Die M30-Kerze schließt evtl. 30min–3h30 VOR der H4 — bis zum H4-Close können aber neue D1/W1-Kerzen noch nicht geschlossen sein, deshalb bleibt der Slice meist identisch. Aber: wenn die Sub-Iteration z.B. die erste M30-Kerze einer H4 = `H4Open+30m` prüft, bekommt sie einen D1-Slice, der Kerzen enthält, die erst zum H4-Close (3h30 später) geschlossen haben. Das ist Look-Ahead auf M30-Ebene und weicht vom Live-Flow ab (Live evaluiert auf Kerzen-Close-Takt der Navigator-TF, nicht auf Sub-Kerzen).
- Vorschlag: In der Sub-Iteration `entryCandle.CloseTime` statt `currentCandle.CloseTime` an `DailySliceUpTo` übergeben. `NowUtc: entryCandle.CloseTime` ist konsistenter.

**2. Fallback-Pfad `FilterTimeframeCandles = htfContext` kann null sein bei H1-Backtest**
- Datei: `BacktestEngine.cs:318` + `BacktestViewModel.cs:353`
- Problem: Single-TF-Pfad setzt `HtfTimeFrame = timeFrame != H1 ? H1 : null`. Bei H1-Backtest ist `HtfTimeFrame=null` → `htfCandles` leer → `htfContext=null` → `FilterTimeframeCandles=null`. Live-Setup liefert für H1-Navigator immer M15-Filter (`GetFilterTimeframe(H1)=M15`). Ohne ChoCH-Filter läuft SK-Pipeline zwar, aber Gate-7/CWS-Filter die auf `FilterTimeframeCandles` bauen werden übersprungen. Resultat: H1-Backtest liefert systematisch andere Ergebnisse als Live.
- Vorschlag: `BacktestSettings.HtfTimeFrame = SequenzKonzeptStrategy.GetFilterTimeframe(tf)` im Single-TF-Pfad (analog zum Multi-TF-Pfad in `RunBacktestMultiTfAsync:199`). Außerdem: Fallback-Block (Z.316-326) sollte `FilterTimeframeCandles = htfContext ?? null` zumindest NICHT Entry-TF nutzen, aber bei `null` wird CWS-Gate-7 umgangen — Dokumentation als Gotcha.

**3. Entry-TF als `FilterTimeframeCandles` — Mapping-Abweichung zu Live**
- Datei: `BacktestEngine.cs:294` (Sub-Iteration)
- Problem: Backtest legt die Entry-TF (M30/M15/M5) auf den `FilterTimeframeCandles`-Slot. Im Live-Flow ist das der Navigator+1-Level (H4→H1, H1→M15, M5→M1). Die Backtest-Zuordnung ist zufällig bei H4-Nav nahe richtig (Entry=M30, Live-Filter=H1 — beides "nächst tiefer"), aber nicht gleich. Die Strategy nutzt `FilterTimeframeCandles` in ChoCH-/CWS-Gate-Logik mit TF-spezifischen Schwellen — mit M30-Daten statt H1-Daten können ATR%/MinRange abweichen. Dadurch wird das Entry-Verhalten stiller Weise anders als Live.
- Vorschlag: Neben Entry-TF-Sub-Iteration zusätzlich echte `GetFilterTimeframe(tf)`-Kerzen laden (analog zu W1/D1 in Block 1b-SK) und als `FilterTimeframeCandles` übergeben. Entry-TF bleibt dabei der Trigger, Filter-TF der Gate.

**4. Performance: `DailySliceUpTo` binary-search pro Sub-Iteration vergeudet**
- Datei: `BacktestEngine.cs:296-297, 320-321, 482-483`
- Problem: Bei H4-Backtest über 1 Jahr: 8760/4 = 2190 Primary × bis zu 8 M30-Subs + 1 Fallback + 1 RiskContext = ~22k Calls pro Ziel-Liste (D1/W1). Binary-Search auf ~365 D1-Kerzen: 9 Schritte → ~200k Compare-Ops pro Liste. Nicht dramatisch, aber unnötig, weil innerhalb einer Primary-Kerze der Slice konstant bleibt.
- Vorschlag: Slice EINMAL pro Primary-Iteration berechnen (vor der Sub-Schleife) und in drei Kontexte weiterreichen. Bei Fix für Finding #1 (sub-Kerze-CloseTime) dann inkrementell wie htfIdx/entryTfIdx.

**5. Default-Verhalten bei fehlendem `scannerSettings` — CWS-Gates silent off**
- Datei: `BacktestEngine.cs:48-49`, `BacktestViewModel.cs:108-111`
- Problem: Wenn ein Altaufruf ohne `scannerSettings` läuft, landet `ScannerSettings: null` im Kontext. SK-Strategy hat Null-Fallbacks (siehe `SequenzKonzeptStrategy.cs:138`), aber alle CWS-Gates (Kontext/Struktur/Force/POV/Switch) sind hinter Feature-Flags in `ScannerSettings` → stillschweigend deaktiviert. Das ist gut gegen Crash, aber ein User der via Altcode Backtest startet bekommt buchstäblich eine "dumme" SK-Pipeline ohne Hinweis.
- Vorschlag: Warning-Log in RunAsync wenn `scannerSettings==null`: "ScannerSettings nicht übergeben — CWS-Gates und per-TF-Settings inaktiv, Backtest-Ergebnisse nicht vergleichbar mit Live."

### KOSMETIK

**6. Redundante W1/D1-Loads im Multi-TF-Backtest**
- Datei: `BacktestViewModel.cs:209-214`
- Problem: `RunBacktestMultiTfAsync` iteriert über 1–4 TFs und ruft pro TF `engine.RunAsync()` auf, wobei jeder Run eigene W1+D1 lädt. 4 TFs × 2 API-Calls = 8 überflüssige Kline-Requests (Daten sind identisch).
- Vorschlag: W1/D1 einmal in `RunBacktestMultiTfAsync` laden und via neuem Parameter/Property in die Engine injizieren. Niedrige Prio — 4×2 = 8 Requests sind kein Rate-Limit-Problem.

**7. W1/D1 Lade-Logs nicht bei `EntryTf==W1/D1` unterdrückt**
- Datei: `BacktestEngine.cs:94-115`
- Problem: Bei W1-Backtest wird D1 geladen (ok), aber W1 übersprungen. Bei D1-Backtest umgekehrt. Das Log ist in Block 1c (Entry-TF) stumm, in 1b-SK kommt aber noch eine Info-Zeile. OK, reines Kosmetik-Thema.

**8. Tests**
- Bestehende `BacktestEngineTests.cs` nutzt die alte Signatur (5-6 Args). Da neue Parameter optional sind, kompiliert das. Empfehlung: einen Test mit `scannerSettings: new()` hinzufügen, der prüft dass der Context die Settings korrekt durchreicht.

## Zusammenfassung
- Verifizierte Findings: 8 (Bugs: 3 | Konsistenz: 2 | Performance: 1 | Kosmetik: 2)
- Commit-ready: Ja für den aktuellen Scope, aber Finding #2+#3 sollten zeitnah hinterher, sonst weicht Backtest weiter von Live ab.
- Top-3 Prio:
  1. H1-Backtest `HtfTimeFrame=null` — FilterTimeframeCandles fehlt (Finding #2).
  2. Entry-TF-Sub-Iteration nutzt H4-Close statt Sub-Close für Slice + NowUtc (Finding #1).
  3. Entry-TF wird als Filter-TF interpretiert — echter `GetFilterTimeframe(tf)`-Load fehlt (Finding #3).
