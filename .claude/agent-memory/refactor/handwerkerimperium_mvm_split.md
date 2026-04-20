---
name: HandwerkerImperium MainViewModel Split
description: 14-Schritt-Plan zur Zerlegung des MainViewModel (v2.0.30). Phase 1 fertig, Phase 2-4 offen.
type: project
---

**Stand 17.04.2026 (Phase 1 abgeschlossen, Build gruen):**

Plan: `C:\Users\rober\.claude\plans\velvety-booping-peacock-agent-a8124b8211c3016e3.md`

**Phase 1 (Foundation) — DONE:**
- `Services/Interfaces/INavigationHost.cs` — Host-Glue-Interface
- `Services/Interfaces/INavigationService.cs` + `NavigationService.cs` — Phase-1-Proxy
- `Services/Interfaces/IDialogOrchestrator.cs` + `DialogOrchestrator.cs` — Phase-1-Proxy
- `Services/Interfaces/IMiniGameNavigator.cs` + `MiniGameNavigator.cs` — Phase-1-Proxy
- `MainViewModel` implementiert explizit `INavigationHost` (OnChildNavigation, HandleBackPressed als Delegates)
- 3 neue optionale Ctor-Parameter + `AttachHost(this)`-Aufrufe im Ctor
- DI-Registrierung in `App.axaml.cs` (3 Singletons vor den ViewModels)

**Phase 2 (Navigation-Umbau) — OFFEN:**
- Schritt 4: `OnChildNavigation`-Logik in `NavigationService.NavigateToRoute` ziehen (Route-Parsing, Stack, Direct-Routes, Guild-Routes, Ascension, Worker-Overlay, Workshop-Detail). MainViewModel-Methode wird Forwarder.
- Schritt 5: `SelectXxxTab()`-Methoden in NavigationService verschieben. `[RelayCommand]`-Wrapper bleiben in MainViewModel.
- Schritt 6: `HandleBackPressed` Dialog-/Overlay-Kaskade (Z.244-262 Navigation.cs) in `DialogOrchestrator.TryDismissTopmost` ziehen.

**Phase 3 (Feature-VMs) — OFFEN:**
- Schritt 7: `HeaderViewModel` — Money, Income, Goldscrews, Level, Prestige-Badge, Boost, Rush, Delivery, Reputation, Worker-Warning (~35 Props). AXAML-Bindings umstellen.
- Schritt 8: `PrestigeBannerViewModel` — Prestige-Banner-Properties + RefreshPrestigeBanner.
- Schritt 9: `GoalBannerViewModel` — Goal-Properties.
- Schritt 10: `WelcomeFlowViewModel` — Combined/Starter/Offline/DailyReward.

**Phase 4 (Cleanup) — OFFEN:**
- Schritt 11: Navigation.cs und Dialogs.cs in MainViewModel.cs inlinen.
- Schritt 12: Dispose-Chains erweitern.
- Schritt 13: `x:CompileBindings="True"` + `x:DataType` auf neuen VMs.
- Schritt 14: CLAUDE.md HandwerkerImperium aktualisieren.

**Wichtige Kontrollpunkte (aus Plan):**
- Pro AXAML-View EINZELN migrieren, Build nach jedem Schritt.
- Property-Namen auf Feature-VMs identisch halten (Money → HeaderVM.Money).
- DialogOrchestrator.TryDismissTopmost muss 1:1 gleiche Reihenfolge wie Navigation.cs:244-262.
- `_navigationStack` Ownership wechselt zu NavigationService.
- `ActivePage`-Enum bleibt (AXAML-Kompatibilitaet), NavigationService schreibt beide.
- Tests in `tests/` pruefen nicht vergessen.

**Aktuelle MainViewModel-Stats:**
- MainViewModel.cs: ~2300 Zeilen (Ziel <450)
- Partials: Navigation 561, Dialogs 94, Init 571, Economy 116, Missions 28
- Bereits extrahiert: MissionsFeatureViewModel (728 Zeilen), EconomyFeatureViewModel (1328 Zeilen)
- Host-Ref-Pattern etabliert (EconomyVM hat `_host`-Ref auf MainViewModel)

**Referenz:** BingXBot MainViewModel (215 Zeilen) + ViewLocator + CurrentPageViewModel.
