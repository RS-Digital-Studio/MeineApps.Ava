# Immersiver Splash Screen mit Preloading - Design

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Alle 8 Apps bekommen einen einheitlichen, immersiven Ladebildschirm mit SkiaSharp-Rendering, der alle vorladbaren Ressourcen (Shader, Fonts, Datenbanken, Services) beim Start preloaded.

**Architecture:** Gemeinsame `SplashScreenRenderer` + `PreloadPipeline` Basis in MeineApps.UI. Jede App definiert ihre spezifischen PreloadSteps. Bestehende Loading-Systeme (ZeitManager, WorkTimePro, FinanzRechner, HandwerkerImperium) werden auf die neue Basis migriert.

**Tech Stack:** SkiaSharp 3.119.2, Avalonia 11.3, .NET 10

---

## Visuelles Design

### Layout (Portrait, alle Apps)
```
+----------------------------------+
|                                  |
|  *    *        *    *     *      |  <- Schwebende Glow-Partikel
|     *    *   *       *          |
|                                  |
|         [APP NAME]               |  <- Zentriert, pulsierender Glow
|          v2.0.x                  |  <- Version, dezent (TextMuted)
|                                  |
|     ████████████░░░░░  65%      |  <- Abgerundeter Gradient-Bar
|     Shader kompilieren...        |  <- Aktueller Step-Name
|                                  |
|  *       *     *    *    *      |
+----------------------------------+
```

### Render-Elemente

1. **Hintergrund**: Vertikaler Gradient (Theme.Background → Theme.Surface dunkel)
2. **Partikel**: 24 schwebende Punkte, Theme.Primary mit Alpha 40-120, Radius 2-6px, MaskFilter.Blur Glow, sanftes Floating (Sinus-basiert, verschiedene Phasen/Geschwindigkeiten)
3. **App-Name**: SKFont Embolden, Theme.Text, pulsierender Glow-Ring (MaskFilter.Blur mit Amplitude 4-12px, 1.5s Cycle)
4. **Version**: SKFont klein, Theme.TextMuted
5. **Fortschrittsbalken**: RoundRect (Höhe 8dp, Radius 4dp), Hintergrund Theme.Surface, Fill mit Linear-Gradient (Theme.Primary → Theme.Secondary), animierte Breite (Lerp zum Zielwert, 200ms Easing)
6. **Prozent-Text**: Rechts vom Bar, Theme.Text
7. **Status-Text**: Unter dem Bar, Theme.TextMuted, aktueller Step-Name
8. **Fade-Out**: 300ms Opacity-Transition nach Abschluss, dann View-Swap zu MainView

---

## Architektur

### Gemeinsame Komponenten (MeineApps.UI)

```
src/UI/MeineApps.UI/
├── SkiaSharp/
│   ├── SplashScreen/
│   │   ├── SplashScreenRenderer.cs     # SkiaSharp Render-Logik
│   │   ├── SplashParticle.cs           # Partikel-Struct (Fixed-Size Pool)
│   │   └── PreloadPipeline.cs          # Step-basiertes async Preloading
```

### SplashScreenRenderer.cs

```csharp
public class SplashScreenRenderer : IDisposable
{
    private const int MaxParticles = 24;
    private readonly SplashParticle[] _particles = new SplashParticle[MaxParticles];

    // Gecachte SKPaint/SKFont (kein per-frame Allokation)
    private readonly SKPaint _bgPaint = new();
    private readonly SKPaint _particlePaint = new();
    private readonly SKPaint _barBgPaint = new();
    private readonly SKPaint _barFillPaint = new();
    private readonly SKPaint _textPaint = new();
    private readonly SKPaint _glowPaint = new();
    private readonly SKFont _titleFont = new() { Embolden = true };
    private readonly SKFont _versionFont = new();
    private readonly SKFont _statusFont = new();
    private readonly SKPath _barPath = new();
    private readonly SKMaskFilter _glowFilter;

    // State
    public float Progress { get; set; }       // 0.0 - 1.0
    public string StatusText { get; set; }
    public string AppName { get; set; }
    public string AppVersion { get; set; }
    public float FadeOutAlpha { get; set; } = 1.0f;

    // Render-Methode (wird von SKCanvasView.PaintSurface aufgerufen)
    public void Render(SKCanvas canvas, SKRect bounds, float elapsed);

    // Animation-Update (wird von DispatcherTimer aufgerufen)
    public void Update(float deltaTime);

    public void Dispose();
}
```

### SplashParticle.cs

```csharp
public struct SplashParticle
{
    public float X, Y;           // Position (0-1 normalisiert)
    public float Radius;         // 2-6 dp
    public float Alpha;          // 40-120
    public float Phase;          // Sinus-Phase (0-2*PI)
    public float SpeedX, SpeedY; // Drift-Geschwindigkeit
    public float FloatAmplitude; // Sinus-Amplitude
    public float FloatFrequency; // Sinus-Frequenz
}
```

### PreloadPipeline.cs

```csharp
public class PreloadPipeline
{
    public record PreloadStep(string Name, Func<Task> Action, int Weight = 1);

    private readonly List<PreloadStep> _steps = new();

    public event Action<float, string>? ProgressChanged; // progress 0-1, stepName
    public event Action? Completed;

    public void AddStep(string name, Func<Task> action, int weight = 1);
    public void AddStep(string name, Action action, int weight = 1); // Sync-Overload

    public async Task RunAsync()
    {
        int totalWeight = _steps.Sum(s => s.Weight);
        int completedWeight = 0;

        foreach (var step in _steps)
        {
            ProgressChanged?.Invoke((float)completedWeight / totalWeight, step.Name);
            await step.Action();
            completedWeight += step.Weight;
        }

        ProgressChanged?.Invoke(1.0f, "");
        Completed?.Invoke();
    }
}
```

### Integration in jeder App

Jede App bekommt in `App.axaml.cs`:

```csharp
// 1. SplashView als erste View anzeigen (mit SKCanvasView + SplashScreenRenderer)
// 2. PreloadPipeline mit app-spezifischen Steps konfigurieren
// 3. Pipeline starten, Progress an Renderer weiterleiten
// 4. Nach Abschluss: 300ms Fade-Out, dann MainView setzen
```

**View-Pattern**: `SplashView.axaml` als gemeinsame View in MeineApps.UI mit SKCanvasView, oder pro App eine minimale SplashView die den Renderer hostet.

---

## Preload-Schritte pro App

### RechnerPlus (3 Steps)
```
1. "Grafik vorbereiten..."     → ShaderPreloader.PreloadAll() + SkiaThemeHelper
2. "Verlauf laden..."          → ICalculationHistoryService.InitializeAsync()
3. "App starten..."            → SettingsViewModel Preferences laden
```

### ZeitManager (6 Steps) - ersetzt bestehende SkiaLoadingSplash-Pipeline
```
1. "Grafik vorbereiten..."     → ShaderPreloader.PreloadAll()
2. "Datenbank laden..."        → DatabaseService.InitializeAsync()
3. "Timer laden..."            → TimerViewModel Daten aus DB
4. "Alarme laden..."           → AlarmSchedulerService.InitializeAsync()
5. "Audio vorbereiten..."      → AudioService Sound-Paths registrieren
6. "App starten..."            → Pomodoro-Daten + Schichtplan laden
```

### FinanzRechner (5 Steps) - ersetzt bestehendes SplashOverlay
```
1. "Grafik vorbereiten..."     → ShaderPreloader.PreloadAll()
2. "Datenbank laden..."        → ExpenseService.InitializeAsync()
3. "Daueraufträge prüfen..."   → RecurringTransactions verarbeiten
4. "Dashboard laden..."        → Monats-Zusammenfassung + Budget-Status
5. "App starten..."            → Sparkline + MiniRing Daten
```

### HandwerkerRechner (4 Steps)
```
1. "Grafik vorbereiten..."     → ShaderPreloader.PreloadAll()
2. "Verlauf laden..."          → ICalculationHistoryService.InitializeAsync()
3. "Projekte laden..."         → ProjectService Daten
4. "App starten..."            → UnitConverter-Tabellen + Kategorien
```

### FitnessRechner (6 Steps) - komplett neu
```
1. "Grafik vorbereiten..."     → ShaderPreloader.PreloadAll()
2. "Daten laden..."            → TrackingService + FoodSearchService init
3. "Fortschritt laden..."      → AchievementService + StreakService + LevelService
4. "Challenges laden..."       → ChallengeService.CheckAndResetIfNewDay()
5. "Dashboard laden..."        → LoadHeatmapDataAsync + WeeklyComparison
6. "App starten..."            → SettingsViewModel Preferences
```

### WorkTimePro (7 Steps) - ersetzt bestehende SkiaLoadingSplash-Pipeline
```
1. "Grafik vorbereiten..."     → ShaderPreloader.PreloadAll()
2. "Datenbank laden..."        → DatabaseService.InitializeAsync()
3. "Erfolge laden..."          → AchievementService.InitializeAsync()
4. "Erinnerungen planen..."    → NotificationService.RescheduleAll()
5. "Wochenansicht laden..."    → WeekOverviewViewModel Daten (7 Tage)
6. "Kalender laden..."         → CalendarViewModel Monatsdaten
7. "App starten..."            → StatisticsViewModel + Export-Service
```

### HandwerkerImperium (7 Steps) - bestehenden LoadingScreenRenderer ersetzen
```
1. "Grafik-Engine laden..."    → ShaderPreloader.PreloadAll() (12 Shader)
2. "Schriftarten laden..."     → SKFont-Pool für Renderer
3. "Spielstand laden..."       → SaveGameService.LoadAsync() + Validierung
4. "Werkstätten einrichten..." → RefreshWorkshops + RefreshBuildings
5. "Aufträge prüfen..."        → Orders/QuickJobs/Challenges regenerieren
6. "Belohnungen prüfen..."     → DailyReward + OfflineEarnings + WelcomeBack
7. "Spiel starten..."          → GameLoopService.Start() + Story-Check
```

### BomberBlast (8 Steps) - komplett neu
```
1. "Grafik-Engine laden..."    → ShaderPreloader.PreloadAll() + ExplosionShaders
2. "Audio laden..."            → AudioService SoundPool (12 SFX + 4 Musik)
3. "Fortschritt laden..."      → ProgressService (100 Level, Sterne)
4. "Erfolge laden..."          → AchievementService (66 Achievements)
5. "Karten laden..."           → CardService + CustomizationService
6. "Shop laden..."             → ShopService (12 Upgrades)
7. "Online-Daten laden..."     → CloudSave + LeagueService (optional)
8. "Spiel starten..."          → MenuBackgroundRenderer + DailyReward-Check
```

---

## Bestehende Systeme - Migration

### ZeitManager: SkiaLoadingSplash → SplashScreenRenderer
- `ZeitManagerLoadingPipeline` wird durch `PreloadPipeline` ersetzt
- Bestehende Steps 1:1 übernommen, Reihenfolge beibehalten
- `SkiaLoadingSplash`-View wird durch `SplashView` mit neuem Renderer ersetzt

### WorkTimePro: SkiaLoadingSplash → SplashScreenRenderer
- `WorkTimeProLoadingPipeline` wird durch `PreloadPipeline` ersetzt
- Gleiche Migration wie ZeitManager

### FinanzRechner: SplashOverlay → SplashView
- Bestehendes Overlay-Pattern in MainView wird entfernt
- Eigene SplashView als Startup-View

### HandwerkerImperium: LoadingScreenRenderer → SplashScreenRenderer
- Bestehender LoadingScreenRenderer wird durch gemeinsamen SplashScreenRenderer ersetzt
- InitializeAsync()-Steps werden in PreloadPipeline überführt
- IsLoading-Property in MainViewModel steuert View-Swap

---

## Lokalisierung (6 Sprachen)

Neue RESX-Keys (pro App unterschiedlich, aber gemeinsames Pattern):

```
SplashStep_Graphics = "Grafik vorbereiten..." / "Preparing graphics..." / ...
SplashStep_Database = "Datenbank laden..." / "Loading database..." / ...
SplashStep_Data = "Daten laden..." / "Loading data..." / ...
SplashStep_Progress = "Fortschritt laden..." / "Loading progress..." / ...
SplashStep_Audio = "Audio laden..." / "Loading audio..." / ...
SplashStep_Starting = "App starten..." / "Starting app..." / ...
SplashStep_GameState = "Spielstand laden..." / "Loading game state..." / ...
SplashStep_Workshops = "Werkstätten einrichten..." / "Setting up workshops..." / ...
SplashStep_Orders = "Aufträge prüfen..." / "Checking orders..." / ...
SplashStep_Rewards = "Belohnungen prüfen..." / "Checking rewards..." / ...
SplashStep_Cards = "Karten laden..." / "Loading cards..." / ...
SplashStep_Online = "Online-Daten laden..." / "Loading online data..." / ...
SplashStep_Achievements = "Erfolge laden..." / "Loading achievements..." / ...
SplashStep_Calendar = "Kalender laden..." / "Loading calendar..." / ...
SplashStep_Timer = "Timer laden..." / "Loading timers..." / ...
SplashStep_Alarms = "Alarme laden..." / "Loading alarms..." / ...
SplashStep_History = "Verlauf laden..." / "Loading history..." / ...
SplashStep_Projects = "Projekte laden..." / "Loading projects..." / ...
SplashStep_Challenges = "Challenges laden..." / "Loading challenges..." / ...
SplashStep_Dashboard = "Dashboard laden..." / "Loading dashboard..." / ...
SplashStep_Notifications = "Erinnerungen planen..." / "Scheduling reminders..." / ...
SplashStep_Shop = "Shop laden..." / "Loading shop..." / ...
SplashStep_Fonts = "Schriftarten laden..." / "Loading fonts..." / ...
SplashStep_Recurring = "Daueraufträge prüfen..." / "Processing recurring..." / ...
SplashStep_Week = "Wochenansicht laden..." / "Loading week view..." / ...
```

---

## Performance-Ziele

| Metrik | Ziel |
|--------|------|
| Minimale Splash-Dauer | 800ms (auch wenn alles gecacht) |
| Maximale Splash-Dauer | 5s (Timeout, dann trotzdem MainView) |
| Partikel-FPS | 60fps (DispatcherTimer 16ms) |
| Fortschrittsbalken | Smooth Lerp (nie rückwärts) |
| Fade-Out | 300ms linear |
| Memory-Overhead | < 2MB (Renderer wird nach Splash disposed) |
