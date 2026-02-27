# HandwerkerImperium Balancing-Redesign - Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** AAA-Niveau Idle-Game-Balancing mit Prestige-Rebalancing, Soft-Cap, Multiplikator-Meilensteinen, gestaffelten Offline-Earnings, GoalService und aufgewerteten Zeremonien.

**Architecture:** Reine Balancing/Logik-Aenderungen in Models + Services, ein neuer GoalService mit DI-Integration, UI-Erweiterungen in DashboardView (Ziel-Banner) und ImperiumView (Prestige-Preview). Alle Aenderungen sind savegame-kompatibel.

**Tech Stack:** Avalonia 11.3.11, .NET 10, CommunityToolkit.Mvvm 8.4.0, SkiaSharp 3.119.2

**Projekt-Root:** `F:\Meine_Apps_Ava\`
**App-Root:** `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/`

---

## Task 1: Prestige-Anforderungen senken

**Files:**
- Modify: `Models/Enums/PrestigeTier.cs:54-63`

**Step 1: GetRequiredPreviousTierCount anpassen**

```csharp
// Zeile 54-63 ersetzen
public static int GetRequiredPreviousTierCount(this PrestigeTier tier) => tier switch
{
    PrestigeTier.Bronze => 0,
    PrestigeTier.Silver => 1,   // war 3 → 1x Bronze reicht
    PrestigeTier.Gold => 1,     // war 3 → 1x Silver reicht
    PrestigeTier.Platin => 2,   // war 3 → 2x Gold
    PrestigeTier.Diamant => 2,  // war 3 → 2x Platin
    PrestigeTier.Meister => 2,  // war 3 → 2x Diamant
    PrestigeTier.Legende => 3,  // bleibt 3 → Endgame-Ziel
    _ => 0
};
```

**Step 2: XML-Kommentare in enum aktualisieren**

```csharp
// Zeile 15-30: Kommentare anpassen
/// <summary>Zweite Stufe, erfordert Level 100 + 1x Bronze</summary>
Silver = 2,

/// <summary>Dritte Stufe, erfordert Level 250 + 1x Silver</summary>
Gold = 3,

/// <summary>Vierte Stufe, erfordert Level 500 + 2x Gold</summary>
Platin = 4,

/// <summary>Fuenfte Stufe, erfordert Level 750 + 2x Platin</summary>
Diamant = 5,

/// <summary>Sechste Stufe, erfordert Level 1000 + 2x Diamant</summary>
Meister = 6,

/// <summary>Hoechste Stufe, erfordert Level 1200 + 3x Meister</summary>
Legende = 7
```

**Step 3: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/Enums/PrestigeTier.cs
git commit -m "HandwerkerImperium: Prestige-Anforderungen gesenkt (3x→1/1/2/2/2/3)"
```

---

## Task 2: Workshop-Level Multiplikator-Meilensteine

**Files:**
- Modify: `Models/Workshop.cs`

**Step 1: GetMilestoneMultiplier() hinzufuegen**

Nach `GetWorkerLevelFitFactor()` (nach Zeile 148) einfuegen:

```csharp
/// <summary>
/// Multiplikator-Meilensteine bei bestimmten Workshop-Leveln.
/// Erzeugt "Bumpy Progression" (AdVenture-Capitalist-Pattern):
/// Vor einem Meilenstein verlangsamt es sich, danach explodiert das Einkommen.
/// Kumulativ: Lv1000 = 1.5 * 2 * 2 * 3 * 5 * 10 = 900x
/// </summary>
public decimal GetMilestoneMultiplier()
{
    decimal mult = 1.0m;
    if (Level >= 25) mult *= 1.5m;
    if (Level >= 50) mult *= 2.0m;
    if (Level >= 100) mult *= 2.0m;
    if (Level >= 250) mult *= 3.0m;
    if (Level >= 500) mult *= 5.0m;
    if (Level >= 1000) mult *= 10.0m;
    return mult;
}

/// <summary>
/// Prueft ob das aktuelle Level ein Multiplikator-Meilenstein ist.
/// </summary>
public static bool IsMilestoneLevel(int level) =>
    level is 25 or 50 or 100 or 250 or 500 or 1000;

/// <summary>
/// Gibt den Multiplikator fuer ein bestimmtes Meilenstein-Level zurueck.
/// </summary>
public static decimal GetMilestoneMultiplierForLevel(int level) => level switch
{
    25 => 1.5m,
    50 => 2.0m,
    100 => 2.0m,
    250 => 3.0m,
    500 => 5.0m,
    1000 => 10.0m,
    _ => 1.0m
};
```

**Step 2: BaseIncomePerWorker anpassen (Zeile 99-106)**

```csharp
/// <summary>
/// Base income per worker per second at current level.
/// Formel: 1 * 1.025^(Level-1) * TypeMultiplier * MilestoneMultiplier
/// Meilensteine erzeugen "Bumpy Progression" bei Lv 25/50/100/250/500/1000.
/// </summary>
[JsonIgnore]
public decimal BaseIncomePerWorker
{
    get
    {
        decimal baseIncome = (decimal)Math.Pow(1.025, Level - 1);
        return baseIncome * Type.GetBaseIncomeMultiplier() * GetMilestoneMultiplier();
    }
}
```

**Step 3: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/Workshop.cs
git commit -m "HandwerkerImperium: Workshop Multiplikator-Meilensteine (x1.5-x10 bei Lv25-1000)"
```

---

## Task 3: Hard-Cap → Soft-Cap (GameLoopService)

**Files:**
- Modify: `Services/GameLoopService.cs:307-315`

**Step 1: Hard-Cap durch Soft-Cap ersetzen**

Zeile 307-315 ersetzen:

```csharp
        // 3f. Soft-Cap: Diminishing Returns ab 2.0x Einkommens-Multiplikator
        // Logarithmisch: Jeder Bonus bringt etwas, aber immer weniger.
        // Beispiele: 3.0x→2.58x, 5.0x→3.17x, 10.0x→4.17x, 50.0x→6.64x
        if (state.TotalIncomePerSecond > 0)
        {
            decimal effectiveMultiplier = grossIncome / state.TotalIncomePerSecond;
            if (effectiveMultiplier > 2.0m)
            {
                decimal excess = effectiveMultiplier - 2.0m;
                decimal softened = 2.0m + (decimal)Math.Log(1.0 + (double)excess, 2.0);
                grossIncome = state.TotalIncomePerSecond * softened;
            }
        }
```

**Step 2: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/GameLoopService.cs
git commit -m "HandwerkerImperium: Hard-Cap 3.0x → logarithmischer Soft-Cap ab 2.0x"
```

---

## Task 4: Hard-Cap → Soft-Cap (OfflineProgressService)

**Files:**
- Modify: `Services/OfflineProgressService.cs:75-82, 101-103`

**Step 1: Soft-Cap in OfflineProgress einbauen (Zeile 75-82)**

```csharp
        // Soft-Cap: Diminishing Returns ab 2.0x (identisch mit GameLoopService)
        if (state.TotalIncomePerSecond > 0)
        {
            decimal effectiveMultiplier = grossIncome / state.TotalIncomePerSecond;
            if (effectiveMultiplier > 2.0m)
            {
                decimal excess = effectiveMultiplier - 2.0m;
                decimal softened = 2.0m + (decimal)Math.Log(1.0 + (double)excess, 2.0);
                grossIncome = state.TotalIncomePerSecond * softened;
            }
        }
```

**Step 2: Gestaffelte Offline-Earnings einbauen (Zeile 101-103)**

Ersetze:
```csharp
        decimal earnings = netPerSecond * (decimal)effectiveDuration.TotalSeconds;
```

Durch:
```csharp
        // Gestaffelte Offline-Earnings: 100% erste 2h, 50% bis 6h, 25% danach
        // Anreiz regelmaessig reinzuschauen (Egg Inc / Idle Miner Pattern)
        decimal totalSeconds = (decimal)effectiveDuration.TotalSeconds;
        decimal first2h = Math.Min(totalSeconds, 7200m);
        decimal next4h = Math.Min(Math.Max(totalSeconds - 7200m, 0m), 14400m);
        decimal remaining = Math.Max(totalSeconds - 21600m, 0m);
        decimal earnings = netPerSecond * (first2h + next4h * 0.5m + remaining * 0.25m);
```

**Step 3: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/OfflineProgressService.cs
git commit -m "HandwerkerImperium: Offline-Earnings Soft-Cap + Staffelung (100%/50%/25%)"
```

---

## Task 5: Meilenstein-Celebrations in MainViewModel

**Files:**
- Modify: `ViewModels/MainViewModel.cs` (OnWorkshopUpgraded, ca. Zeile 3521-3545)

**Step 1: Meilenstein-Multiplikator-Celebrations erweitern**

In der `OnWorkshopUpgraded`-Methode, VOR dem bestehenden `workshopMilestones`-Block (Zeile 3523), neuen Block einfuegen:

```csharp
        // Multiplikator-Meilensteine (Bumpy Progression)
        if (!IsHoldingUpgrade && Workshop.IsMilestoneLevel(e.NewLevel))
        {
            decimal milestoneMultiplier = Workshop.GetMilestoneMultiplierForLevel(e.NewLevel);
            var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
            string boostText = $"x{milestoneMultiplier:0.#} {_localizationService.GetString("IncomeBoost") ?? "EINKOMMENS-BOOST"}!";

            FloatingTextRequested?.Invoke(boostText, "golden_screws");
            _audioService.PlaySoundAsync(GameSound.Achievement).FireAndForget();

            // Groessere Zeremonien bei hoeheren Meilensteinen
            if (e.NewLevel >= 50)
            {
                CeremonyRequested?.Invoke(CeremonyType.WorkshopMilestone,
                    $"{workshopName} Lv.{e.NewLevel}",
                    boostText);
            }
        }
```

**Step 2: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/ViewModels/MainViewModel.cs
git commit -m "HandwerkerImperium: Multiplikator-Meilenstein Celebrations (Lv25-1000)"
```

---

## Task 6: GoalService (Naechstes-Ziel-System)

**Files:**
- Create: `Services/Interfaces/IGoalService.cs`
- Create: `Services/GoalService.cs`
- Create: `Models/GameGoal.cs`

**Step 1: GameGoal Model erstellen**

```csharp
// Models/GameGoal.cs
namespace HandwerkerImperium.Models;

/// <summary>
/// Repraesentiert das naechste empfohlene Ziel fuer den Spieler.
/// Dynamisch berechnet basierend auf Spielfortschritt.
/// </summary>
public class GameGoal
{
    /// <summary>Lokalisierter Beschreibungstext des Ziels.</summary>
    public string Description { get; init; } = "";

    /// <summary>Lokalisierter Belohnungstext (z.B. "x2 Einkommen!").</summary>
    public string RewardHint { get; init; } = "";

    /// <summary>Fortschritt zum Ziel (0.0-1.0).</summary>
    public double Progress { get; init; }

    /// <summary>Navigations-Route bei Tap (z.B. "imperium", "workshop_Carpenter").</summary>
    public string? NavigationRoute { get; init; }

    /// <summary>Material Icon Kind (z.B. "TrendingUp", "Star").</summary>
    public string IconKind { get; init; } = "TrendingUp";

    /// <summary>Prioritaet (niedrigere Zahl = hoehere Prio).</summary>
    public int Priority { get; init; }
}
```

**Step 2: IGoalService Interface erstellen**

```csharp
// Services/Interfaces/IGoalService.cs
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

public interface IGoalService
{
    /// <summary>
    /// Berechnet das aktuell empfohlene Ziel.
    /// Gecacht, wird nur alle 60 GameLoop-Ticks neu berechnet.
    /// </summary>
    GameGoal? GetCurrentGoal();

    /// <summary>
    /// Erzwingt Neuberechnung (z.B. nach Workshop-Upgrade).
    /// </summary>
    void Invalidate();
}
```

**Step 3: GoalService implementieren**

```csharp
// Services/GoalService.cs
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Berechnet dynamisch das naechste empfohlene Ziel fuer den Spieler.
/// Priorisiert nach: Meilenstein nahe → Prestige verfuegbar → Neuer Workshop → Gebaeude → Challenges.
/// </summary>
public class GoalService : IGoalService
{
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IPrestigeService _prestigeService;
    private GameGoal? _cachedGoal;
    private bool _isDirty = true;

    public GoalService(
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IPrestigeService prestigeService)
    {
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _prestigeService = prestigeService;
    }

    public GameGoal? GetCurrentGoal()
    {
        if (!_isDirty && _cachedGoal != null) return _cachedGoal;
        _cachedGoal = CalculateBestGoal();
        _isDirty = false;
        return _cachedGoal;
    }

    public void Invalidate() => _isDirty = true;

    private GameGoal? CalculateBestGoal()
    {
        var state = _gameStateService.State;
        var goals = new List<GameGoal>();

        // 1. Workshop-Meilenstein nahe (hoechste Prio wenn <5 Level entfernt)
        foreach (var ws in state.Workshops.Where(w => w.IsUnlocked))
        {
            int[] milestones = [25, 50, 100, 250, 500, 1000];
            foreach (int milestone in milestones)
            {
                int diff = milestone - ws.Level;
                if (diff > 0 && diff <= 5)
                {
                    decimal mult = Workshop.GetMilestoneMultiplierForLevel(milestone);
                    var wsName = _localizationService.GetString(ws.Type.GetLocalizationKey()) ?? ws.Type.ToString();
                    goals.Add(new GameGoal
                    {
                        Description = $"{wsName} → Lv.{milestone}",
                        RewardHint = $"x{mult:0.#} {_localizationService.GetString("IncomeBoost") ?? "Einkommens-Boost"}!",
                        Progress = (double)ws.Level / milestone,
                        NavigationRoute = $"workshop_{ws.Type}",
                        IconKind = "TrendingUp",
                        Priority = 1
                    });
                    break; // Nur naechsten Meilenstein pro Workshop
                }
            }
        }

        // 2. Prestige verfuegbar
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
        if (highestTier != PrestigeTier.None)
        {
            var points = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
            int tierPoints = (int)(points * highestTier.GetPointMultiplier());
            goals.Add(new GameGoal
            {
                Description = _localizationService.GetString("PrestigeAvailable") ?? "Prestige verfügbar!",
                RewardHint = $"+{tierPoints} {_localizationService.GetString("PrestigePointsShort") ?? "PP"}",
                Progress = 1.0,
                NavigationRoute = "imperium",
                IconKind = "StarFourPoints",
                Priority = 2
            });
        }

        // 3. Naechster Workshop freischaltbar
        var nextWorkshop = state.Workshops
            .Where(w => !w.IsUnlocked && state.IsWorkshopAvailable(w.Type))
            .OrderBy(w => w.UnlockCost)
            .FirstOrDefault();
        if (nextWorkshop != null)
        {
            var wsName = _localizationService.GetString(nextWorkshop.Type.GetLocalizationKey()) ?? nextWorkshop.Type.ToString();
            decimal remaining = nextWorkshop.UnlockCost - state.Money;
            if (remaining > 0 && remaining < state.Money * 5) // Nur wenn halbwegs erreichbar
            {
                goals.Add(new GameGoal
                {
                    Description = $"{wsName} {_localizationService.GetString("Unlock") ?? "freischalten"}",
                    RewardHint = $"x{nextWorkshop.Type.GetBaseIncomeMultiplier():0.#} {_localizationService.GetString("Income") ?? "Einkommen"}",
                    Progress = Math.Min(1.0, (double)(state.Money / nextWorkshop.UnlockCost)),
                    NavigationRoute = "dashboard",
                    IconKind = "LockOpenVariant",
                    Priority = 3
                });
            }
        }

        // 4. Gebaeude-Upgrade (wenn erschwinglich)
        foreach (var building in state.Buildings.Where(b => b.Level < 5))
        {
            var upgradeCost = building.GetUpgradeCost();
            if (state.Money >= upgradeCost * 0.5m) // Wenn mindestens halben Preis hat
            {
                var bName = _localizationService.GetString(building.Type.GetLocalizationKey()) ?? building.Type.ToString();
                goals.Add(new GameGoal
                {
                    Description = $"{bName} → Lv.{building.Level + 1}",
                    RewardHint = _localizationService.GetString("BuildingUpgradeHint") ?? "Bessere Boni!",
                    Progress = Math.Min(1.0, (double)(state.Money / upgradeCost)),
                    NavigationRoute = "imperium",
                    IconKind = "HomeCity",
                    Priority = 4
                });
                break; // Nur ein Gebaeude vorschlagen
            }
        }

        // Bestes Ziel nach Prioritaet zurueckgeben
        return goals.OrderBy(g => g.Priority).FirstOrDefault();
    }
}
```

**Step 4: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded (GoalService wird noch nicht referenziert)

**Step 5: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/GameGoal.cs
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/Interfaces/IGoalService.cs
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/GoalService.cs
git commit -m "HandwerkerImperium: GoalService - dynamisches Naechstes-Ziel-System"
```

---

## Task 7: GoalService DI-Registrierung + MainViewModel-Integration

**Files:**
- Modify: `App.axaml.cs` (DI-Zeile ~172)
- Modify: `ViewModels/MainViewModel.cs` (Constructor + Properties + GameLoop-Hook)

**Step 1: DI registrieren in App.axaml.cs**

Nach Zeile 172 (`ICraftingService`) einfuegen:

```csharp
        services.AddSingleton<IGoalService, GoalService>();
```

**Step 2: MainViewModel - Constructor + Property**

Im Constructor (bereits sehr lang, ~42 Parameter) neuen Parameter `IGoalService goalService` hinzufuegen.
Feld: `private readonly IGoalService _goalService;`

Neue Properties:

```csharp
[ObservableProperty]
private string _currentGoalDescription = "";

[ObservableProperty]
private string _currentGoalReward = "";

[ObservableProperty]
private double _currentGoalProgress;

[ObservableProperty]
private string _currentGoalIcon = "TrendingUp";

[ObservableProperty]
private bool _hasCurrentGoal;

private string? _currentGoalRoute;
```

**Step 3: Ziel-Aktualisierung im GameLoop-Callback**

In der Methode die pro Tick aufgerufen wird (wo auch RefreshPrestigeBanner laeuft), alle 60 Ticks:

```csharp
// Naechstes Ziel alle 60 Ticks aktualisieren
if (_tickForGoal++ >= 60)
{
    _tickForGoal = 0;
    RefreshCurrentGoal();
}
```

```csharp
private int _tickForGoal;

private void RefreshCurrentGoal()
{
    var goal = _goalService.GetCurrentGoal();
    HasCurrentGoal = goal != null;
    if (goal != null)
    {
        CurrentGoalDescription = goal.Description;
        CurrentGoalReward = goal.RewardHint;
        CurrentGoalProgress = goal.Progress;
        CurrentGoalIcon = goal.IconKind;
        _currentGoalRoute = goal.NavigationRoute;
    }
}
```

**Step 4: NavigateToGoal Command**

```csharp
[RelayCommand]
private void NavigateToGoal()
{
    if (_currentGoalRoute != null)
        NavigationRequested?.Invoke(_currentGoalRoute);
}
```

**Step 5: GoalService invalidieren bei relevanten Events**

In `OnWorkshopUpgraded`, `OnWorkerHired`, `OnOrderCompleted` und nach Prestige:

```csharp
_goalService.Invalidate();
```

**Step 6: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 7: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/App.axaml.cs
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/ViewModels/MainViewModel.cs
git commit -m "HandwerkerImperium: GoalService in DI + MainViewModel integriert"
```

---

## Task 8: Dashboard Ziel-Banner UI

**Files:**
- Modify: `Views/DashboardView.axaml` (nach BannerStrip, Zeile ~285)

**Step 1: Ziel-Banner AXAML einfuegen**

Nach `<dashboard:BannerStrip Margin="0,8,0,0" />` (Zeile 285):

```xml
        <!-- Naechstes Ziel Banner -->
        <Border Margin="0,8,0,0" Padding="12,10"
                CornerRadius="12"
                Background="#20D97706"
                BorderBrush="{StaticResource CraftGoldBrush}"
                BorderThickness="1.5"
                IsVisible="{Binding HasCurrentGoal}"
                AutomationProperties.AutomationId="Dashboard_GoalBanner">
          <Border.Styles>
            <Style Selector="Border#GoalBorder">
              <Style.Animations>
                <Animation Duration="0:0:3" IterationCount="Infinite">
                  <KeyFrame Cue="0%"><Setter Property="BorderBrush" Value="#60D97706"/></KeyFrame>
                  <KeyFrame Cue="50%"><Setter Property="BorderBrush" Value="#FFD97706"/></KeyFrame>
                  <KeyFrame Cue="100%"><Setter Property="BorderBrush" Value="#60D97706"/></KeyFrame>
                </Animation>
              </Style.Animations>
            </Style>
          </Border.Styles>
          <Button Classes="Text" Command="{Binding NavigateToGoalCommand}"
                  HorizontalAlignment="Stretch" Padding="0">
            <Grid ColumnDefinitions="Auto,*,Auto" RowDefinitions="Auto,Auto">
              <!-- Icon -->
              <mi:MaterialIcon Grid.Column="0" Grid.RowSpan="2"
                               Kind="TrendingUp"
                               Width="28" Height="28"
                               Foreground="{StaticResource CraftGoldBrush}"
                               Margin="0,0,10,0"
                               VerticalAlignment="Center" />
              <!-- Beschreibung -->
              <TextBlock Grid.Column="1" Grid.Row="0"
                         Text="{Binding CurrentGoalDescription}"
                         FontSize="14" FontWeight="SemiBold"
                         Foreground="{DynamicResource TextPrimaryBrush}" />
              <!-- Belohnung -->
              <TextBlock Grid.Column="1" Grid.Row="1"
                         Text="{Binding CurrentGoalReward}"
                         FontSize="12"
                         Foreground="{StaticResource CraftGoldBrush}" />
              <!-- Pfeil -->
              <mi:MaterialIcon Grid.Column="2" Grid.RowSpan="2"
                               Kind="ChevronRight"
                               Width="20" Height="20"
                               Foreground="{DynamicResource TextSecondaryBrush}"
                               VerticalAlignment="Center" />
            </Grid>
          </Button>
        </Border>
```

**Step 2: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/DashboardView.axaml
git commit -m "HandwerkerImperium: Naechstes-Ziel-Banner im Dashboard"
```

---

## Task 9: Erweiterte Prestige-Preview

**Files:**
- Modify: `ViewModels/MainViewModel.cs` (RefreshPrestigeBanner)
- Modify: `Views/ImperiumView.axaml` (Prestige-Banner erweitern)

**Step 1: Neue Properties in MainViewModel**

```csharp
[ObservableProperty]
private string _prestigePreviewGains = "";

[ObservableProperty]
private string _prestigePreviewLosses = "";

[ObservableProperty]
private string _prestigePreviewSpeedUp = "";

[ObservableProperty]
private string _prestigePreviewTierName = "";
```

**Step 2: RefreshPrestigeBanner erweitern (Zeile 2183-2198)**

```csharp
private void RefreshPrestigeBanner(GameState state)
{
    var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
    IsPrestigeAvailable = highestTier != PrestigeTier.None;

    if (IsPrestigeAvailable)
    {
        var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
        int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());
        var pointsLabel = _localizationService.GetString("PrestigePoints") ?? "Prestige-Punkte";
        PrestigePointsPreview = $"+{tierPoints} {pointsLabel}";
        PrestigePreviewTierName = _localizationService.GetString(highestTier.GetLocalizationKey()) ?? highestTier.ToString();

        // Gewinne
        decimal permanentBonus = highestTier.GetPermanentMultiplierBonus() * 100;
        var gains = new List<string>();
        gains.Add($"+{tierPoints} {pointsLabel} (x{highestTier.GetPointMultiplier()})");
        gains.Add($"+{permanentBonus:0}% {_localizationService.GetString("PermanentIncomeBonus") ?? "permanenter Einkommens-Bonus"}");
        if (highestTier.KeepsResearch())
            gains.Add(_localizationService.GetString("PrestigeKeepsResearch") ?? "Forschung bleibt erhalten!");
        if (highestTier.KeepsShopItems())
            gains.Add(_localizationService.GetString("PrestigeKeepsShop") ?? "Prestige-Shop bleibt!");
        if (highestTier.KeepsMasterTools())
            gains.Add(_localizationService.GetString("PrestigeKeepsTools") ?? "Meisterwerkzeuge bleiben!");
        if (highestTier.KeepsBuildings())
            gains.Add(_localizationService.GetString("PrestigeKeepsBuildings") ?? "Gebäude bleiben (Lv.1)!");
        if (highestTier.KeepsManagers())
            gains.Add(_localizationService.GetString("PrestigeKeepsManagers") ?? "Manager bleiben (Lv.1)!");
        if (highestTier.KeepsBestWorkers())
            gains.Add(_localizationService.GetString("PrestigeKeepsWorkers") ?? "Beste Worker bleiben!");
        PrestigePreviewGains = string.Join("\n", gains);

        // Verluste
        var losses = new List<string>();
        losses.Add(_localizationService.GetString("PrestigeLosesLevel") ?? "Spieler-Level → 1");
        losses.Add(_localizationService.GetString("PrestigeLosesMoney") ?? "Geld → 0");
        losses.Add(_localizationService.GetString("PrestigeLosesWorkers") ?? "Worker → entlassen");
        if (!highestTier.KeepsResearch())
            losses.Add(_localizationService.GetString("PrestigeLosesResearch") ?? "Forschung → Reset");
        PrestigePreviewLosses = string.Join("\n", losses);

        // Geschaetzter Speed-Up (basierend auf permanentem Multiplikator-Zuwachs)
        decimal currentMult = state.Prestige.PermanentMultiplier;
        decimal newMult = currentMult + highestTier.GetPermanentMultiplierBonus();
        int speedUpPercent = currentMult > 0 ? (int)((newMult / currentMult - 1m) * 100) : 100;
        PrestigePreviewSpeedUp = $"~{speedUpPercent}% {_localizationService.GetString("Faster") ?? "schneller"}";
    }
    else
    {
        PrestigePointsPreview = "";
        PrestigePreviewGains = "";
        PrestigePreviewLosses = "";
        PrestigePreviewSpeedUp = "";
        PrestigePreviewTierName = "";
    }
}
```

**Step 3: ImperiumView.axaml Prestige-Banner erweitern**

Den bestehenden Prestige-Banner in ImperiumView.axaml (ca. Zeile 80-100) um die neuen Felder erweitern. Unter dem bestehenden `PrestigePointsPreview` TextBlock:

```xml
              <!-- Gewinne -->
              <TextBlock Text="{Binding PrestigePreviewGains}"
                         FontSize="12" Foreground="#4CAF50"
                         TextWrapping="Wrap" Margin="0,6,0,0"
                         IsVisible="{Binding IsPrestigeAvailable}" />
              <!-- Verluste -->
              <TextBlock Text="{Binding PrestigePreviewLosses}"
                         FontSize="12" Foreground="#EF5350"
                         TextWrapping="Wrap" Margin="0,4,0,0"
                         IsVisible="{Binding IsPrestigeAvailable}" />
              <!-- Speed-Up -->
              <TextBlock Text="{Binding PrestigePreviewSpeedUp}"
                         FontSize="13" FontWeight="Bold"
                         Foreground="{StaticResource CraftGoldBrush}"
                         Margin="0,6,0,0"
                         IsVisible="{Binding IsPrestigeAvailable}" />
```

**Step 4: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 5: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/ViewModels/MainViewModel.cs
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/ImperiumView.axaml
git commit -m "HandwerkerImperium: Erweiterte Prestige-Preview (Gewinne/Verluste/Speed-Up)"
```

---

## Task 10: RESX-Keys fuer alle 6 Sprachen

**Files:**
- Modify: `Resources/Strings/AppStrings.resx` (EN)
- Modify: `Resources/Strings/AppStrings.de.resx` (DE)
- Modify: `Resources/Strings/AppStrings.es.resx` (ES)
- Modify: `Resources/Strings/AppStrings.fr.resx` (FR)
- Modify: `Resources/Strings/AppStrings.it.resx` (IT)
- Modify: `Resources/Strings/AppStrings.pt.resx` (PT)

**Neue Keys:**

| Key | EN | DE |
|-----|----|----|
| IncomeBoost | Income Boost | Einkommens-Boost |
| PrestigeAvailable | Prestige available! | Prestige verfügbar! |
| PermanentIncomeBonus | permanent income bonus | permanenter Einkommens-Bonus |
| PrestigeKeepsResearch | Research preserved! | Forschung bleibt erhalten! |
| PrestigeKeepsShop | Prestige shop preserved! | Prestige-Shop bleibt! |
| PrestigeKeepsTools | Master tools preserved! | Meisterwerkzeuge bleiben! |
| PrestigeKeepsBuildings | Buildings preserved (Lv.1)! | Gebäude bleiben (Lv.1)! |
| PrestigeKeepsManagers | Managers preserved (Lv.1)! | Manager bleiben (Lv.1)! |
| PrestigeKeepsWorkers | Best workers preserved! | Beste Worker bleiben! |
| PrestigeLosesLevel | Player level → 1 | Spieler-Level → 1 |
| PrestigeLosesMoney | Money → 0 | Geld → 0 |
| PrestigeLosesWorkers | Workers → dismissed | Worker → entlassen |
| PrestigeLosesResearch | Research → reset | Forschung → Reset |
| Faster | faster | schneller |
| Unlock | unlock | freischalten |
| BuildingUpgradeHint | Better bonuses! | Bessere Boni! |

ES/FR/IT/PT analog uebersetzen.

**Step 1: Keys in alle 6 RESX-Dateien einfuegen**

**Step 2: Build pruefen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Resources/Strings/
git commit -m "HandwerkerImperium: RESX-Keys fuer Balancing-Redesign (6 Sprachen)"
```

---

## Task 11: CLAUDE.md + balancing.md aktualisieren

**Files:**
- Modify: `src/Apps/HandwerkerImperium/CLAUDE.md`
- Modify: Memory `balancing.md`

**Step 1: CLAUDE.md Prestige-Abschnitt aktualisieren**

- Prestige-Anforderungen: 3→1/1/2/2/2/3
- Hard-Cap: "Soft-Cap ab 2.0x (logarithmisch)"
- Neues Feature: GoalService
- Neue Multiplikator-Meilensteine dokumentieren

**Step 2: balancing.md HandwerkerImperium-Abschnitt aktualisieren**

- Prestige-Tabelle: Neue Anforderungen
- Workshop-Upgrade-Kosten: + Meilenstein-Multiplikatoren
- Income-Cap: "Soft-Cap ab 2.0x, log2 Diminishing Returns"
- Offline-Earnings: "100%/50%/25% Staffelung"

**Step 3: Commit**

```bash
git add src/Apps/HandwerkerImperium/CLAUDE.md
git commit -m "HandwerkerImperium: CLAUDE.md + Balancing-Doku aktualisiert"
```

---

## Task 12: Finaler Build + AppChecker

**Step 1: Vollstaendiger Build**

Run: `dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln`
Expected: Build succeeded

**Step 2: AppChecker**

Run: `dotnet run --project tools/AppChecker HandwerkerImperium`
Expected: Keine kritischen Fehler

**Step 3: Finaler Commit (falls noetig)**

```bash
git commit -m "HandwerkerImperium: Balancing-Redesign abgeschlossen"
```
