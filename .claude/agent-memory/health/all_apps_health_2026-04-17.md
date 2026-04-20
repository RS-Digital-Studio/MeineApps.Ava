---
name: Alle 12 Apps Health-Check 2026-04-17
description: Makro-Gesundheit Solution (12 Apps + 5 Libraries), Build gruen, 0 Vulns, Score B. Hauptprobleme Service-Locator in 3 Apps-Views, RebornSaga setzt DataContext im Code-Behind, BingXBot-CLAUDE.md in Teilen veraltet.
type: project
---

Datum: 2026-04-17
Scope: Solution-weiter Health-Check (12 Apps, 5 Libraries)

**Ergebnis Score B**. Build: gruen (364 CA1416-Warnings in HandwerkerImperium.Android, rein Android-API-Versions-Guards). Keine Vulnerabilities.

**Why**: BingXBot hat durch den 15.04.2026 ViewLocator-Refactor neue Architektur-Baseline gesetzt (Services in `BingXBot.Trading`-Library, ViewLocator + `Application.DataTemplates`, kein DataContext-Setzen im Code-Behind). Andere Apps hinken dem Standard nach.

**How to apply**: Bei neuen Health-Checks diese Apps priorisiert pruefen:
- ZeitManager MainView.axaml.cs (4x `App.Services.GetRequiredService` Zeile 131/138/164/179) -- Onboarding-Service-Locator
- RechnerPlus MainView.axaml.cs (5x, Zeile 62/77/192/198/232) -- Splash-Preload-Service-Locator
- FinanzRechner SettingsView.axaml.cs:53 -- FileShareService-Lookup
- RebornSaga App.axaml.cs:55,63 -- setzt DataContext im Code-Behind, braucht ViewLocator-Refactor nach BingXBot-Vorbild
- GardenControl MainViewModel: kein BackPressHelper/HandleBackPressed (Convention-Abweichung)

**Positiv verifiziert (sauber)**:
- BingXBot Services korrekt in `BingXBot.Trading`-Library, nur noch VMs/Views in `BingXBot.Shared`
- 18 von 19 BingXBot-Views nutzen `x:CompileBindings` + `x:DataType` (nur DashboardView fehlt Compiled Bindings)
- Alle ViewModels nutzen ObservableObject/ObservableRecipient basis
- Keine vulnerablen NuGet-Packages in der Solution
- DI-Pattern in 11 Apps identisch (Singleton MainVM, `Services.GetRequiredService<MainViewModel>()` in App.axaml.cs OnFrameworkInitializationCompleted)

Ausstehende CLAUDE.md-Diskrepanz: `src/Apps/BingXBot/CLAUDE.md` hat widerspruechliche Abschnitte -- Multi-TF-Standalone (15.04.2026) sagt MultiModeOrchestrator ist entfernt, spaetere Abschnitte beschreiben ihn noch als Bestandteil. Vor naechster BingXBot-Analyse konsolidieren.
