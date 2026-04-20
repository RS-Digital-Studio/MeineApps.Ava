---
name: HandwerkerImperium Health 2026-04-18
description: Makro-Analyse v2.0.30 nach Phase-2/3/4-Refactor - NavigationService/INavigationHost ziehen, CLAUDE.md-Versions-Drift, Ctor-Splash-Fallback stale
type: project
---

# HandwerkerImperium Health-Check 2026-04-18

**Version csproj:** 2.0.30 | **CLAUDE.md-Header:** "v2.0.29" (stale) | **Build:** grün, 0 Warnungen

## Kernzahlen
- 58 Service-Dateien, 58 Interfaces (inkl. 5 GameLoop/GameState-Partials)
- 45 ViewModel-Klassen + 10 MiniGame-VMs, alle Singleton, alle AXAML mit `x:CompileBindings`
- 0 `App.Services.GetRequiredService` in Views (MVVM sauber)
- 9 Services mit StateLoaded-Reset-Pattern
- MainViewModel.cs 2149 Zeilen (reduziert von 2303 durch Phase-3-Extraktion), Navigation.cs 244 (vorher 561), Host.cs 115 (NEU), Missions.cs 28
- GuildViewModel 1719, EconomyFeatureViewModel 1328, GuildService 1531
- MainViewModel.Host.cs ist Forward-/Explicit-Interface-Layer, keine Logik (gutes Pattern)
- 9 StateLoaded-Handler: Crafting/GameLoop/Event/Goal/Vip/Research/Rebirth/Prestige + GameLoop-Stateloaded

## Bemerkenswert (Positiv)
- Phase-2/3/4-Refactor sauber umgesetzt: NavigationService + DialogOrchestrator + MiniGameNavigator + 4 Feature-VMs + 6 Guild-Thin-Wrapper-VMs
- INavigationHost als expliziter Host-Contract (testbar, keine MainViewModel-Lecks)
- DisposeServices nutzt Provider.Dispose() statt Hardcoded-Liste (Memory-Leak-Risiko eliminiert)
- 12 HandwerkerImperium-Testdateien (gute Abdeckung Services)
- 0 Vulnerable NuGets

## Findings
- CLAUDE.md-Drift: Header "Version: 2.0.29 (VersionCode 37)" vs. csproj 2.0.30/38
- App.axaml.cs:138 hardcoded Fallback "v2.0.22" (Assembly-Read existiert, aber Fallback ist 8 Versionen stale)
- MainViewModel.Missions.cs mit nur 28 Zeilen (2 Methoden) - könnte nach Host.cs oder MainViewModel.cs inline, rein kosmetisch
- Dialog-Controls nutzen noch `DataContext={Binding DialogVM}`-Pattern statt ViewLocator (in CLAUDE.md als bekannt dokumentiert)
- 58 Services ist sehr hoch, aber Gilden-Subsystem allein hat 9 → Domain-Schnitte klar

## Health-Score
- Architektur: 92 (Phase 2-4 Refactor vorbildlich)
- Toter Code: 90 (keine offensichtlichen Waisen)
- Konsistenz: 85 (CLAUDE.md-Drift)
- Technische Schulden: 80 (Hotspot-Files GuildVM 1719, MainVM 2149, GuildService 1531)
- Tests: 82 (12 Tests, decken Services)
- Gesamt: 87/100 (verbessert von 84)
