---
name: HandwerkerImperium v2.0.29 Review Apr 2026
description: 4 Findings (2 Bugs) - AssignedWorkshop bei Prestige/Ascension nicht gesetzt, ImportSave ohne IO-Lock, 10 async void Timer-Ticks, Cloud-Save PlayerLevel-Race
type: project
---

# HandwerkerImperium v2.0.29 (Apr 2026) - Code Review

## Kritische Findings

1. **Worker.AssignedWorkshop Bug wiederholt** (PrestigeService.cs:752, PrestigeService.cs:730/743, AscensionService.cs:132):
   - `Worker.CreateForTier()` setzt `AssignedWorkshop` nicht
   - PrestigeService.ResetProgress() + RestoreKeptWorkers() + AscensionService.ResetRun() fuegen Worker hinzu OHNE AssignedWorkshop=wsType
   - Gleiches Symptom wie 03.04.2026-Gotcha (damals in GameState.CreateNew() gefixt, aber bei Reset-Pfaden nicht gleichgezogen)
   - SanitizeState repariert nur beim Load, NICHT im Running-State
   - IsWorking haengt von AssignedWorkshop ab -> keine Fatigue nach Prestige/Ascension

2. **SaveGameService.ImportSave ohne IO-Lock** (SaveGameService.cs:204-222):
   - Initialize(state) + SaveAsync() nicht atomar
   - GameLoop kann zwischen Initialize und SaveAsync ticken und Sanitize-Reparaturen ueberschreiben

3. **10 async void Mini-Game Timer-Ticks**:
   - BlueprintGameViewModel, DesignPuzzleGameViewModel, PipePuzzleViewModel, WiringGameViewModel, PaintingGameViewModel, RoofTilingGameViewModel, InventGameViewModel, InspectionGameViewModel, LuckySpinViewModel, MainViewModel
   - Exception -> Prozess-Crash

## Was gut geloest ist

- Alle Services mit Caches abonnieren StateLoaded (CraftingService, EventService, GoalService, PrestigeService, RebirthService, ResearchService, VipService, GameLoopService)
- JsonSerializer.Serialize auf UI-Thread dokumentiert (SaveGameService.cs:74)
- GameLoopService.Dispose mit benannten Handlern sauber
- MVVM-Strenge: Kein App.Services.GetRequiredService in View-Ctoren, sauberes DataContextChanged-Pattern in MainView.axaml.cs
- SKMaskFilter durchgaengig static+cached (CraftTextures, BlueprintGameRenderer, ForgeGameRenderer, GameCardRenderer, GuildResearchTreeRenderer, FireworksRenderer)
- GameTabBarRenderer: IDisposable sauber implementiert (Paths + MaskFilter disposed)
- Lambdas fuer StateLoaded ohne Unsubscribe sind OK: Singleton->Singleton Lifetime
