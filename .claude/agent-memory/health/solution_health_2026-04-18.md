---
name: Solution Health-Check 2026-04-18
description: Makro-Gesundheit aller 12 Apps + 10 Libraries, Build gruen, 22 Warnings. Hauptbefund ML-Packages 4+Genetic unbenutzt, HandwerkerImperium NotificationReceiver 5 Null-Derefs, RebornSaga DataContext im Code-Behind, MainViewModel 2149 Zeilen reduziert von 2277.
type: project
---

Datum: 2026-04-18
Scope: Solution-Weit (12 Apps, 10 Libraries: 7 BingXBot-Libs + MeineApps.Core.Ava/Premium.Ava + CalcLib + UI)

Ergebnis: Build gruen (0 Fehler, 22 Warnings), keine Vulnerabilities, Score B+.

Positiv verifiziert (sauber):
- Kein einziger `App.Services.GetRequiredService` in View-Code-Behind (alle 11 Apps). Alte Regressionen (ZeitManager/RechnerPlus/FinanzRechner) wurden behoben seit 2026-04-17.
- BingXBot SK-Service-Library-Trennung vollzogen (BingXBot.Core, BingXBot.Exchange, BingXBot.Engine, BingXBot.Contracts, BingXBot.Backtest, BingXBot.ClientApi, BingXBot.Trading). Shared-Projekt enthaelt nur noch VMs/Views.
- Alle App.axaml.cs setzen DataContext nur auf Einstiegspunkt (MainWindow/SingleView) — zwei Ausnahmen: RebornSaga (MainView), SmartMeasure/GardenControl (separate MainView-Instanz mit DataContext). Pattern inkonsistent aber nicht falsch.

Kritische/Hohe Findings:

1) Microsoft.ML + GeneticSharp + OnnxRuntime + LightGbm in Directory.Packages.props deklariert, aber in 0 csproj referenziert. Kein einziges `using Microsoft.ML` im gesamten src/. Ballast fuer NuGet-Restore. Fix: Packages entfernen oder ATI-Konzept endlich implementieren.
2) RebornSaga App.axaml.cs:55,63 setzt DataContext per Code-Behind mit `Services.GetRequiredService<MainViewModel>()`. Gegen MVVM-Strict-Regel. Fix: ViewLocator + DataTemplates wie BingXBot.
3) HandwerkerImperium NotificationReceiver.cs:33-42 hat 5 CS8602 Null-Derefs + doppelter `using HandwerkerImperium.Services` in MainActivity.cs:9,18. Kosmetisch aber ein negatives Signal in Produktions-App.
4) HandwerkerImperium MainView.Android SetDecorFitsSystemWindows aufruf ohne API-Version-Check Z.169 (CA1422) — deprecated ab 35.0, muss guarded werden.

Mittlere Findings:
- BingXBot Desktop-Projekt referenziert nur 3 Packages, Shared 13 — Konsistenz OK.
- HandwerkerImperium MainViewModel.cs ist bei 2149 Zeilen (war 2277 laut Memo 2026-04-17). Reduktion durch Feature-VM-Extraktion (HeaderVM/PrestigeBannerVM/GoalBannerVM/WelcomeFlowVM). Weitere Zerlegung moeglich, nicht dringend.
- GardenControl.Core nur Models/DTOs/Enums — keine Duplikation oder Layer-Verletzung.
- AndroidFontResolver nur in SmartMeasure implementiert, obwohl PdfSharpCore auch in anderen Apps (FinanzRechner/WorkTimePro) via ClosedXML/PdfSharp importiert wird — kein Problem da sie den Font-Resolver nicht brauchen (reines Excel).

Niedrig:
- 14 CA1416 Warnings in FitnessRechner.Android (Android-Versions-Guards fehlen lokal, aber MinSdk 26 setzt sie implizit). Kosmetisch.
- CS8620 Null-Annotation Fehler in HandwerkerImperium.Tests/SettingsViewModelTests.cs:61 (NSubstitute Task<string?> vs Task<string>).

Ausstehende Beobachtung: BingXBot-CLAUDE.md ist mit 15.04.2026 ViewLocator-Refactor konsistent. Alter Widerspruch aus Memo 2026-04-17 wohl geloest.

Why: 12 Apps + 10 Libs + 3 Tools in Mono-Repo — Regressions-Risiko bei Refactors hoch. Laufender Zustand ist stabil, aber ML-Tech-Debt (5 Packages = ~300 MB NuGet-Footprint) ist eine klare Bereinigungs-Chance.

How to apply: Bei naechster Health-Inspektion pruefen ob (1) ML-Packages entfernt oder endlich benutzt, (2) RebornSaga ViewLocator-Refactor erfolgt, (3) HandwerkerImperium NotificationReceiver null-sauber ist.
