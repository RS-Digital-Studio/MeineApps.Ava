---
name: HandwerkerImperium Health 2026-04-17
description: Makro-Analyse v2.0.29 - 49 Services, Partial-Klassen-Inflation, MainViewModel 2277 Zeilen, 44 Test-Files vorhanden
type: project
---

# HandwerkerImperium Health-Check 2026-04-17

**Version:** 2.0.29 (Produktion) | **Shared .cs:** ~60k LoC, Tests: 36k LoC

## Kernzahlen
- 49 Service-Implementierungen, 53 Interfaces (alle DI-registriert, 3-stufig mit Guild-Sub-Services)
- 34 ViewModels + 10 MiniGame-VMs (alle Singleton)
- 56 AXAML-Views, alle mit `x:CompileBindings="True"`
- 44 Testdateien vorhanden (gut!)
- 0 `App.Services.GetRequiredService` in Views (MVVM sauber)
- 11 Services mit StateLoaded-Reset-Pattern

## Hotspot-Files
- MainViewModel.cs: 2277 (plus 4 Partials: Init 571, Navigation 561, Missions, Dialogs, Economy) = ~5000 LoC gesamt
- GuildViewModel.cs: 1723 (kein Partial-Split trotz Groesse)
- EconomyFeatureViewModel.cs: 1328
- InventGameRenderer.cs / BlueprintGameRenderer.cs ~1900
- GuildService.cs: 1531
- DashboardView.axaml.cs: 802 (sehr hoch fuer Code-Behind)

## Bemerkenswert
- Service-Zahl 49 ist hoch, aber klar domaenenschneidend organisiert (Guild-Subsystem hat 8 eigene Services)
- Ascension GS-Bonus via Delegate-Injektion in GameStateService (App.axaml.cs Zeile 80) - vermeidet zirkulaere DI, sauber dokumentiert
- DisposeServices() Liste in App.axaml.cs manuell gepflegt - fragil bei neuen Services
