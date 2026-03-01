# FitnessRechner + HandwerkerImperium Optimization Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Performance, Code-Qualität und Architektur beider Apps systematisch verbessern - ~738 per-frame Allokationen eliminieren, Memory-Leaks stopfen, toten Code entfernen, Thread-Safety herstellen.

**Architecture:** Renderer-Caching-Pattern (WorkshopSceneRenderer als Vorbild), IDisposable für native Ressourcen, Partial-Class-Aufspaltung für God Objects, Batch-Queries statt N+1.

**Tech Stack:** .NET 10, Avalonia 11.3, SkiaSharp 3.119.2, CommunityToolkit.Mvvm

---

## Phase 1: FitnessRechner - Kritisch + Hoch

### Task 1: Statische SKPaint in 3 Renderern fixen (Thread-Safety)

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/BmiGaugeRenderer.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/BodyFatRenderer.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/CalorieRingRenderer.cs`

**Aktion:** Alle `static readonly SKPaint`-Felder durch lokale `using var paint = new SKPaint { ... }` im Render-Aufruf ersetzen (wie HealthTrendVisualization es vormacht). Statische SKMaskFilter ebenfalls entfernen.

### Task 2: Batch-Query für Heatmap/Dashboard (N+1 eliminieren)

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Services/FoodSearchService.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/MainViewModel.cs`

**Aktion:** `GetFoodLogsInRangeAsync(DateTime start, DateTime end)` Batch-Methode in FoodSearchService erstellen. LoadHeatmapDataAsync und LoadWeeklyComparisonAsync auf Batch umstellen (90+ Einzel-Aufrufe → 1-2 Aufrufe).

### Task 3: 1.260 Zeilen toten Code entfernen

**Files:**
- Delete: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/TrackingViewModel.cs` (691 Zeilen)
- Delete: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/HistoryViewModel.cs` (326 Zeilen)
- Delete: `src/Apps/FitnessRechner/FitnessRechner.Shared/Services/VersionedDataService.cs` (140 Zeilen)
- Delete: `src/Apps/FitnessRechner/FitnessRechner.Shared/Services/UndoService.cs`
- Delete: `src/Apps/FitnessRechner/FitnessRechner.Shared/Services/IUndoService.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/App.axaml.cs` (DI-Registrierung entfernen)

### Task 4: Service-Locator eliminieren

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/ViewModels/MainViewModel.cs`

**Aktion:** `App.Services.GetRequiredService<>()` in CreateCalculatorVm durch Lazy<T> oder Factory-Pattern ersetzen.

### Task 5: CompileBindings in allen 13 Views aktivieren

**Files:**
- Modify: Alle 13 .axaml-Dateien in `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/`

**Aktion:** `x:CompileBindings="True"` + `x:DataType="vm:XxxViewModel"` in jeder View setzen.

### Task 6: Converter-Duplikation + Interface-Fixes + Kleinigkeiten

**Files:**
- Delete/Modify: App-lokaler BoolToStringConverter (Core-Version verwenden)
- Modify: FitnessEngine.cs + StreakService.cs (Interfaces erstellen)
- Modify: MainViewModel.cs (Debug.WriteLine → MessageRequested)
- Modify: FoodSearchService.cs (Lowercase-Cache)
- Modify: HomeView.axaml (Bottom-Margin 60→80dp)
- Modify: BarcodeLookupService.cs (Fire-and-forget Cache fix)

---

## Phase 2: HandwerkerImperium - Kritisch

### Task 7: SKFont/SKPath-Allokationen cachen (44 Renderer)

**Files:**
- Modify: Alle ~44 Renderer in `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/`

**Aktion:** `using var font = new SKFont { Size = X }` → gecachtes Instanz-Feld `_font` mit `_font.Size = X` vor Verwendung. `using var path = new SKPath()` → gecachtes Feld `_path` mit `_path.Reset()` am Anfang. Eliminiert ~738 Allokationen/Frame.

### Task 8: List-basierte Partikel → struct-Arrays (10 Renderer)

**Files:**
- Modify: SawingGameRenderer, ResearchActiveRenderer, GuildHallHeaderRenderer, ResearchTreeRenderer, ResearchBranchBannerRenderer, ResearchCelebrationRenderer, AnimationManager, ResearchLabRenderer, PipePuzzleRenderer (soweit List-basiert)

**Aktion:** `List<Particle>` → `Particle[] _pool = new Particle[MAX]` + `_count`-Index. Wie InventGameRenderer/BlueprintGameRenderer es vormachen.

### Task 9: Static SKPaint → Instanz-Felder (3 Dateien)

**Files:**
- Modify: WorkshopCardRenderer.cs, CityProgressionHelper.cs, ForgeGameRenderer.cs

### Task 10: IDisposable für alle Renderer mit nativen Ressourcen

**Files:**
- Modify: ~20 Renderer die SKPaint/SKMaskFilter/SKShader als Felder halten
- Modify: Zugehörige Views (Dispose in OnDetachedFromVisualTree)

---

## Phase 3: HandwerkerImperium - Hoch + Mittel

### Task 11: MainViewModel in Partial Classes aufteilen

**Files:**
- Create: MainViewModel.Navigation.cs
- Create: MainViewModel.Dialogs.cs
- Create: MainViewModel.Economy.cs
- Create: MainViewModel.Missions.cs
- Create: MainViewModel.Init.cs
- Modify: MainViewModel.cs (auf ~800-900 Zeilen reduzieren)

### Task 12: Service-Locator + Event-Leak + Code-Qualität

**Files:**
- Modify: MainViewModel.cs (5 Services → Constructor Injection)
- Modify: MainViewModel.cs:1091 (Event-Leak fixen)
- Modify: 7 Firebase-Services (30+ leere catch → Debug.WriteLine)
- Modify: 20 Dateien (Debug.WriteLine entfernen/`#if DEBUG`)
- Modify: SaveGameService.cs (WriteIndented = false)
- Modify: MainViewModel.cs (Workshop-Kauflogik deduplizieren)

---

## Validierung

Nach jeder Phase: `dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln`
Nach Abschluss: `dotnet run --project tools/AppChecker FitnessRechner` + `dotnet run --project tools/AppChecker HandwerkerImperium`
