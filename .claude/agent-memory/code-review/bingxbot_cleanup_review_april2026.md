---
name: BingXBot Cleanup-Review April 2026
description: Review nach massivem Cleanup (ATI + Non-Buch-Features raus) - 11 Findings (4 kritisch), davon viele Zombie-Felder die in JSON-Saves landen
type: feedback
---

Review am 12.04.2026 nach Refactoring auf strikte Buch-Konformität (SK-System).

## Verifizierte Findings

### Kritisch
- `App.axaml.cs:104-105` schreibt nicht mehr existierendes `AtiMinTradesBeforeLearning` / `AtiAutoSaveIntervalMinutes` — `BotSettings.cs:20-24` hält die Felder noch
- `RiskSettingsView.axaml:311-326` hat leere Trailing-Stop-Border-Sektion mit Titel ohne Inhalt
- `DashboardView.axaml.cs:72-73, 151-152` sucht per FindControl nach `StrategyWeightsCanvas`/`AtiLearningCanvas` — nicht mehr im XAML. `Graphics/StrategyWeightsRenderer.cs` ist toter Code
- `PositionExitState.cs:45-55, 69-70`: `ExtremePriceSinceEntry`, `TrailingAtrMultiplier`, `CurrentAtr`, `ConfluenceScore`, `Tp2Closed` + `ExitPhase.Trailing` — geschrieben aber NIE gelesen. Writes in `TradingServiceBase.cs:163, 861-862`

### Wichtig
- `BacktestEngine.cs:691-697` BacktestExitState hat ebenfalls Trailing-Zombie-Felder
- `BacktestViewModel.cs:76` `RegimeBreakdownText` wird nie befüllt, `BacktestView.axaml:565-582` Binding läuft ins Leere
- `BotDatabaseService.cs:67-85` Migrations v2-v5 schreiben FeatureSnapshots-Tabelle die nicht mehr erstellt wird → wirft "no such table" auf neuen Installs
- Alte DBs haben `SettingEntity(Key='AtiState')` mit MB-großem JSON-Blob — nie bereinigt
- `BingXBot.Engine.csproj` referenziert `Microsoft.ML`, `Microsoft.ML.LightGbm`, `Microsoft.ML.OnnxRuntime`, `GeneticSharp` — 0 Usings, ~200 MB im publish

### Minor
- Massenhaft Geister-Kommentare in TradingServiceBase, LiveTradingManager, DashboardViewModel, BacktestEngine ("ATI-Lernen", "Holy Trinity", "Trailing")
- `src/Apps/BingXBot/CLAUDE.md:210-253` dokumentiert noch komplett ATI-Architektur obwohl entfernt

## Lessons

**Why:** Bei Refactorings die ganze Module entfernen wird oft der direkte Code gelöscht, aber Hilfsfelder in Data-Classes (`PositionExitState`, Backtest-Tracking-Structs) bleiben. Das Problem: Die werden in JSON persistiert und "wachsen" bei jedem Save. Plus: MSBuild schmeißt entfernte Package-References nicht auto raus.

**How to apply:** Bei Cleanup-Reviews ALLE Data-Classes grep-basiert auf entfernte Feld-Namen prüfen (`TrailingAtr`, `Regime`, etc.), die .csproj-Files auf verwaiste Package-References prüfen, Migrationen (`RunMigrationsAsync`) auf Tabellen-Namen die nicht mehr existieren prüfen, DB-Setting-Keys (z.B. `AtiState`) auf orphaned JSON-Blobs prüfen.
