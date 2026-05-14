using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Controls;
using BomberBlast.Core;
using BomberBlast.Core.LevelGeneration;
using BomberBlast.Extensions;
using BomberBlast.Graphics;
using BomberBlast.Input;
using BomberBlast.Loading;
using BomberBlast.Resources.Strings;
using BomberBlast.Services;
using BomberBlast.ViewModels;
using BomberBlast.Views;
using MeineApps.UI.SkiaSharp;

namespace BomberBlast;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Aktuelles Mobile-Root-Panel — wird vom IActivityApplicationLifetime.MainViewFactory gesetzt.
    /// RunLoadingAsync greift darauf zu um den DataContext nach Pipeline-Abschluss zu setzen.
    /// Avalonia 12 hat kein direktes "View"-Getter auf IActivityApplicationLifetime.
    /// </summary>
    private static Control? _activityRoot;

    /// <summary>
    /// Factory fuer plattformspezifischen IRewardedAdService (Android setzt RewardedAdHelper).
    /// Nimmt IServiceProvider entgegen fuer Lazy-Resolution von Abhaengigkeiten.
    /// </summary>
    public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IPurchaseService (Android setzt AndroidPurchaseService).
    /// </summary>
    public static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen ISoundService (Android setzt AndroidSoundService).
    /// </summary>
    public static Func<IServiceProvider, ISoundService>? SoundServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IPlayGamesService (Android setzt AndroidPlayGamesService).
    /// </summary>
    public static Func<IServiceProvider, IPlayGamesService>? PlayGamesServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen ICloudSaveService (Android setzt CloudSaveService).
    /// </summary>
    public static Func<IServiceProvider, ICloudSaveService>? CloudSaveServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IVibrationService (Android setzt AndroidVibrationService).
    /// </summary>
    public static Func<IServiceProvider, IVibrationService>? VibrationServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen ITelemetryService (Android setzt AndroidCrashlyticsService).
    /// Wird im Console-Setup von Robert nachgereicht — bis dahin NullTelemetryService.
    /// </summary>
    public static Func<IServiceProvider, ITelemetryService>? TelemetryServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IAnalyticsService (Android setzt AndroidAnalyticsService).
    /// </summary>
    public static Func<IServiceProvider, IAnalyticsService>? AnalyticsServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IPushNotificationService (Android setzt AndroidPushNotificationService).
    /// </summary>
    public static Func<IServiceProvider, IPushNotificationService>? PushNotificationServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IRemoteConfigService (Android setzt FirebaseRemoteConfigService —.1).
    /// Bis dahin: NullRemoteConfigService liefert Defaults (.4c Stub).
    /// </summary>
    public static Func<IServiceProvider, IRemoteConfigService>? RemoteConfigServiceFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    /// <summary>
    ///.3 : Splash-Crash-Recovery Pref-Key.
    /// Inkrementiert vor jedem Init-Versuch, dekrementiert nach erfolgreichem Splash-Abschluss.
    /// Bei >= 3 Crashes in Folge wird der User zum Reset-Dialog geleitet.
    /// </summary>
    private const string KeyCrashCount = "BomberBlast_AppCrashCount";
    private const int CrashRecoveryThreshold = 3;

    public override void OnFrameworkInitializationCompleted()
    {
        //.3 : Crash-Counter VOR der Init-Phase inkrementieren.
        // Wenn die App in den naechsten Schritten crasht, ueberlebt der Counter persistent —
        // beim 3. Try greift Safe-Mode-Recovery.
        // Wir nutzen einen direkten PreferencesService weil DI noch nicht aufgebaut ist.
        var crashRecoveryPrefs = new PreferencesService("BomberBlast");
        int crashCount = crashRecoveryPrefs.Get(KeyCrashCount, 0) + 1;
        crashRecoveryPrefs.Set(KeyCrashCount, crashCount);
        bool safeModeRequested = crashCount >= CrashRecoveryThreshold;

        try
        {
            InitializeServicesAndUi(safeModeRequested);
        }
        catch (Exception ex)
        {
            // Init-Crash: Counter bleibt erhoeht. Beim naechsten Start kommt der Safe-Mode.
            Avalonia.Logging.Logger.TryGet(Avalonia.Logging.LogEventLevel.Fatal, "BomberBlast")
                ?.Log(this, $"Init-Crash (count={crashCount}): {ex}");
            throw;
        }

        // Erfolgreich initialisiert — Counter zuruecksetzen damit der naechste Crash sauber
        // beim 1. Try wieder starten kann (kein false-positive Safe-Mode bei einmaligen Glitches).
        // Wird in RunLoadingAsync nach Pipeline-Abschluss erst final auf 0 gesetzt — hier nur
        // als Schutz gegen partial-init-Crash.
    }

    private void InitializeServicesAndUi(bool safeMode)
    {
        // Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Statischer Logger für ShaderEffects (nicht DI-verwaltet) — generic ILogger<T>.
        ShaderEffects.Logger = Services.GetRequiredService<ILogger<ShaderEffects>>();
        // PersistenceHealth ist eine statische Klasse — kein ILogger<T> moeglich.
        // Logger via Factory mit Kategorie-Name erzeugen (Standard-Pattern fuer statische Sinks).
        PersistenceHealth.Logger = Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(PersistenceHealth));
        // RewardedAdCooldownTracker: Preferences-Hook fuer persistierten Cooldown (Schutz gegen App-Restart-Bypass)
        RewardedAdCooldownTracker.Preferences = Services.GetRequiredService<IPreferencesService>();
        // GameLoopSettings: Persistierten TargetFps-Wert laden (30/60 FPS, default 30 Battery-Mode)
        GameLoopSettings.Initialize(Services.GetRequiredService<IPreferencesService>());

        // v2.0.44 — : Telemetrie + Analytics + Push-Notifications initialisieren.
        // Bei NullImpl auf Desktop ist das ein No-Op. Auf Android sucht ein konfigurierter
        // Firebase-Setup nach google-services.json (Console-Setup vom User).
        //.3 : Safe-Mode skippt optionale Services (Firebase + Push)
        // damit die App garantiert startet wenn ein optionaler Service der Crash-Ursache war.
        // Game-State + UI funktionieren weiterhin — User kommt ans Settings-Menue,
        // kann Account-Delete oder Reset durchfuehren.
        if (!safeMode)
        {
            try { Services.GetRequiredService<ITelemetryService>().Initialize(); }
            catch (Exception ex) { Services.GetService<ILogger<App>>()?.LogError(ex, "Telemetry-Init fehlgeschlagen"); }

            try { Services.GetRequiredService<IAnalyticsService>().Initialize(); }
            catch (Exception ex) { Services.GetService<ILogger<App>>()?.LogError(ex, "Analytics-Init fehlgeschlagen"); }

            try { _ = Services.GetRequiredService<IPushNotificationService>().InitializeAsync(); }
            catch (Exception ex) { Services.GetService<ILogger<App>>()?.LogError(ex, "Push-Init fehlgeschlagen"); }

            try { _ = Services.GetRequiredService<IRemoteConfigService>().InitializeAsync(); }
            catch (Exception ex) { Services.GetService<ILogger<App>>()?.LogError(ex, "RemoteConfig-Init fehlgeschlagen"); }
        }
        else
        {
            // Safe-Mode: Schreibe einen Diagnose-Eintrag damit Crashlytics weiss, warum dieser Start
            // ohne optionale Services ist. Wird beim naechsten Online-Start gepushed.
            Services.GetService<ILogger<App>>()?.LogWarning(
                "Safe-Mode aktiv — optionale Services (Telemetry/Analytics/Push/RemoteConfig) uebersprungen wegen wiederholter Crashes.");
        }

        // Statischer Accessor für AI-Asset-Renderer (statische Klassen ohne DI)
        GameAssetService.Current = Services.GetRequiredService<IGameAssetService>();

        // Zirkuläre Dependencies werden per Lazy<T> aufgelöst (siehe LazyServiceExtensions)
        // Keine manuelle SetXxxService()-Verdrahtung mehr nötig

        // Initialize localization
        var locService = Services.GetRequiredService<ILocalizationService>();
        locService.Initialize();
        LocalizationManager.Initialize(locService);

        // Farb-Cache für SkiaSharp initialisieren
        SkiaThemeHelper.RefreshColors();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            var splash = CreateSplash();
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            desktop.MainWindow.Content = panel;
            _ = RunLoadingAsync(splash);

            // Desktop: Beim Herunterfahren alle IDisposable-Singletons disposen
            desktop.ShutdownRequested += (_, _) => DisposeServices();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            // Avalonia 12: MainViewFactory wird pro Activity neu aufgerufen.
            // Pipeline laeuft beim allerersten Factory-Call (Singleton via _activityRoot-Guard).
            activity.MainViewFactory = () =>
            {
                var splash = CreateSplash();
                var panel = new Panel();
                panel.Children.Add(new MainView());
                panel.Children.Add(splash);
                _activityRoot = panel;
                _ = RunLoadingAsync(splash);
                return panel;
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var splash = CreateSplash();
            var panel = new Panel();
            panel.Children.Add(new MainView());
            panel.Children.Add(splash);
            singleViewPlatform.MainView = panel;
            _ = RunLoadingAsync(splash);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static SkiaLoadingSplash CreateSplash()
    {
        return new SkiaLoadingSplash
        {
            AppName = "BomberBlast",
            //.4a : Splash-Version aus Assembly auslesen statt hardcoded.
            // BomberBlast.Shared.csproj <Version> wird zur Assembly-Version → ToString(3) = "X.Y.Z".
            AppVersion = "v" + GetAppVersionString(),
            Renderer = new BomberBlastSplashRenderer()
        };
    }

    /// <summary>
    /// Liefert die App-Version aus dem Shared-Assembly als "X.Y.Z" (3 Komponenten).
    /// Fallback "0.0.0" wenn Assembly-Version aus irgendeinem Grund nicht lesbar.
    /// </summary>
    private static string GetAppVersionString()
    {
        var version = typeof(App).Assembly.GetName().Version;
        return version is null ? "0.0.0" : version.ToString(3);
    }

    private async Task RunLoadingAsync(SkiaLoadingSplash splash)
    {
        try
        {
            var pipeline = new BomberBlastLoadingPipeline(Services);
            pipeline.ProgressChanged += (progress, text) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    splash.Progress = progress;
                    splash.StatusText = text;
                });

            var sw = Stopwatch.StartNew();
            await pipeline.ExecuteAsync();

            // Mindestens 800ms anzeigen damit die Splash-Animation sichtbar ist
            var remaining = 800 - (int)sw.ElapsedMilliseconds;
            if (remaining > 0) await Task.Delay(remaining);

            var mainVm = Services.GetRequiredService<MainViewModel>();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    && desktop.MainWindow != null)
                    desktop.MainWindow.DataContext = mainVm;
                else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform
                         && singleViewPlatform.MainView != null)
                    singleViewPlatform.MainView.DataContext = mainVm;

                splash.FadeOut();
            });

            //.3 : Pipeline-Erfolg → Crash-Counter zuruecksetzen.
            // Naechster Start beginnt sauber bei 1.
            try
            {
                var prefs = Services.GetRequiredService<IPreferencesService>();
                prefs.Set(KeyCrashCount, 0);
            }
            catch { /* Best-Effort */ }
        }
        catch (Exception ex)
        {
            // Logger statt Debug.WriteLine - wird auch im Release-Build sichtbar (LogCat auf Android).
            // CrashlyticsLoggerProvider forwarded an ITelemetryService.LogNonFatal mit Stack-Trace.
            Services?.GetService<ILogger<App>>()?.LogError(ex, "Loading-Pipeline fehlgeschlagen");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
    }

    /// <summary>
    ///.3 : Public API fuer den Settings-Screen
    /// — der User kann manuell den Crash-Counter zuruecksetzen wenn er das Spiel
    /// neu starten will ohne dass der Safe-Mode getriggert wird.
    /// </summary>
    public static void ResetCrashRecoveryCounter()
    {
        try
        {
            var prefs = Services?.GetService<IPreferencesService>();
            prefs?.Set(KeyCrashCount, 0);
        }
        catch { /* Best-Effort */ }
    }

    /// <summary>
    /// Disposed alle IDisposable-Singletons (GameEngine, GameRenderer, etc.).
    /// Wird bei Desktop-Shutdown aufgerufen, auf Android via MainActivity.OnDestroy().
    /// </summary>
    public static void DisposeServices()
    {
        if (Services == null) return;

        try
        {
            // GameEngine + GameRenderer + GameViewModel werden seit v2.0.36 LAZY resolved
            // (siehe MainViewModel.EnsureGameVm). Nur disposen wenn der User mind. einmal
            // im Spiel war — sonst wuerden wir sie hier beim Shutdown unnoetig instanziieren.
            var mainVm = Services.GetService<MainViewModel>();
            if (mainVm?.GameVm is { } gameVm)
            {
                (Services.GetService<GameEngine>() as IDisposable)?.Dispose();
                // GameRenderer NICHT hier disposen (Audit C08):
                // Android-OnDestroy ist haeufig kein echter Process-Kill — der Renderer-Singleton
                // wuerde beim naechsten OnCreate disposed weiter-resolved, mit invaliden SKPaint/SKMaskFilter,
                // und der erste DrawText/DrawPaint-Call crasht. OS raeumt den nativen Heap beim echten
                // Process-Kill ohnehin auf; auf Desktop ueberlebt das Prozess-Ende den ShutdownRequested.
                (gameVm as IDisposable)?.Dispose();
            }
            // Audit M24: Weitere IDisposable-Singleton-VMs (MainMenu/Shop/LevelSelect) sind Subscriber
            // auf BalanceChanged/LanguageChanged-Events und nutzen DispatcherTimer. Dispose stoppt Timer
            // und unsubscribes — wichtig auf Desktop. Auf Android laeuft das via ShutdownRequested/OnDestroy.
            if (mainVm != null)
            {
                (mainVm.ShopVm as IDisposable)?.Dispose();
                (mainVm.LevelSelectVm as IDisposable)?.Dispose();
                (mainVm.MenuVm as IDisposable)?.Dispose();
                // Event-Subscription-Cleanup fuer weitere Lazy-VMs (Audit Event-Subscription-Lücken).
                (mainVm.DeckVm as IDisposable)?.Dispose();
                (mainVm.DungeonVm as IDisposable)?.Dispose();
                (mainVm.GemShopVm as IDisposable)?.Dispose();
                (mainVm.LuckySpinVm as IDisposable)?.Dispose();
                (mainVm.ProfileVm as IDisposable)?.Dispose();
            }

            // Audit L07: Pending Dirty-Saves von Achievements/Collection/Tracking ans Disk flushen
            // bevor Process stirbt. Wird sonst nur ueber GameEngine-Lifecycle gefluscht — bei
            // OnDestroy ohne Game-Session blieben Dirty-Achievements im Memory liegen.
            Services.GetService<IGameTrackingService>()?.FlushIfDirty();
            (Services.GetService<IFirebaseService>() as IDisposable)?.Dispose();
            (Services.GetService<IGameAssetService>() as IDisposable)?.Dispose();
            // InputManager haelt NeonJoystick (20 SKPaint + 5 SKPath) - muss auch disposed werden
            (Services.GetService<Input.InputManager>() as IDisposable)?.Dispose();
            // v2.0.35: Services mit CloudStateLoaded-Subscription + pending Debounced-Writes
            // DeckTelemetryService-Dispose flusht letzten pending Save (vermeidet Datenverlust bei Shutdown)
            Services.GetService<IDeckTelemetryService>()?.Dispose();
            Services.GetService<IMasterModeService>()?.Dispose();
            Services.GetService<ICloudSaveService>()?.Dispose();
            // Statisch gecachten WaterRipple-SkSL-Shader freigeben (wurde im Splash preloaded)
            ShaderEffects.DisposeSharedResources();
        }
        catch
        {
            // Fehler beim Dispose beim Herunterfahren sind unkritisch
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging —.1 (Welle 6: AppLogger-Fassade abgeloest).
        // Microsoft.Extensions.Logging mit drei eigenen Providern (Code-only, keine NuGet-Sinks):
        //   - TraceLoggerProvider → LogCat auf Android / Debug-Output auf Desktop
        //   - FileLoggerProvider → rollende Log-Datei (App-intern, ueberlebt App-Crashes)
        //   - CrashlyticsLoggerProvider → Bridge zu ITelemetryService.LogNonFatal/Log
        // Build-Filter: Trace im Debug, Info im Release.
        services.AddSingleton<ILoggerFactory>(sp => LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new Services.Logging.TraceLoggerProvider());
            builder.AddProvider(new Services.Logging.FileLoggerProvider());
            builder.AddProvider(new Services.Logging.CrashlyticsLoggerProvider(sp));
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Trace);
#else
            builder.SetMinimumLevel(LogLevel.Information);
#endif
        }));
        // ILogger<T> Open-Generic-Resolver: Services injizieren ILogger<MyService> per Ctor.
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        // Lazy<T>-Auflösung für zirkuläre Dependencies (statt manueller SetXxxService()-Verdrahtung)
        services.AddLazyResolution();

        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("BomberBlast"));

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        // Android-Override: Echte Google Play Billing statt Stub
        if (PurchaseServiceFactory != null)
            services.AddSingleton<IPurchaseService>(sp => PurchaseServiceFactory!(sp));

        // Localization
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // Google Play Games Services
        if (PlayGamesServiceFactory != null)
            services.AddSingleton<IPlayGamesService>(sp => PlayGamesServiceFactory!(sp));
        else
            services.AddSingleton<IPlayGamesService, NullPlayGamesService>();

        // Game Services
        services.AddSingleton<IProgressService, ProgressService>();
        services.AddSingleton<IHighScoreService, HighScoreService>();
        // Android-Override: Echte Sounds statt NullSoundService
        if (SoundServiceFactory != null)
            services.AddSingleton<ISoundService>(sp => SoundServiceFactory!(sp));
        else
            services.AddSingleton<ISoundService, NullSoundService>();
        services.AddSingleton<IGameStyleService, GameStyleService>();
        services.AddSingleton<ICoinService, CoinService>();
        services.AddSingleton<IGemService, GemService>();
        services.AddSingleton<IShopService, ShopService>();
        services.AddSingleton<ILevelGenerator, LevelGenerator>();
        services.AddSingleton<ITutorialService, TutorialService>();
        services.AddSingleton<IDailyRewardService, DailyRewardService>();
        services.AddSingleton<IDailyChallengeService, DailyChallengeService>();
        services.AddSingleton<ICustomizationService, CustomizationService>();
        services.AddSingleton<IReviewService, ReviewService>();
        services.AddSingleton<IAchievementService, AchievementService>();
        services.AddSingleton<IDiscoveryService, DiscoveryService>();
        services.AddSingleton<ILuckySpinService, LuckySpinService>();
        services.AddSingleton<IWeeklyChallengeService, WeeklyChallengeService>();
        services.AddSingleton<IDailyMissionService, DailyMissionService>();
        services.AddSingleton<ICardService, CardService>();
        services.AddSingleton<IDungeonService, DungeonService>();
        services.AddSingleton<IDungeonUpgradeService, DungeonUpgradeService>();
        services.AddSingleton<IBattlePassService, BattlePassService>();
        services.AddSingleton<ICollectionService, CollectionService>();
        services.AddSingleton<IFirebaseService, FirebaseService>();
        services.AddSingleton<ILeagueService, LeagueService>();

        // Cloud Save (Android-Override: Echte Google Drive Cloud Saves statt NullCloudSaveService)
        if (CloudSaveServiceFactory != null)
            services.AddSingleton<ICloudSaveService>(sp => CloudSaveServiceFactory!(sp));
        else
            services.AddSingleton<ICloudSaveService, NullCloudSaveService>();

        services.AddSingleton<IStarterPackService, StarterPackService>();
        services.AddSingleton<IRotatingDealsService, RotatingDealsService>();
        services.AddSingleton<IDeckTelemetryService, DeckTelemetryService>();
        services.AddSingleton<IMasterModeService, MasterModeService>();
        services.AddSingleton<ILoadoutService, LoadoutService>();
        services.AddSingleton<IBossRushService, BossRushService>();
        services.AddSingleton<IEventService, EventService>();
        // Phase 20 — L2: Wöchentlicher Event-Calendar (deterministisch via ISO-Week)
        services.AddSingleton<IEventCalendarService, EventCalendarService>();
        // Phase 23 — M5: First-Time-Purchase-Bonus (×2 auf ersten Kauf)
        services.AddSingleton<IFirstPurchaseService, FirstPurchaseService>();
        // Phase 25 — DSGVO Art. 20: Datenexport-Service (Account-Holder können ihre Daten als JSON beziehen)
        services.AddSingleton<IDataExportService, DataExportService>();
        // Phase 27 — P2/P3/P4: Hardware-Profile-Service (Quality-Tier + Battery + Thermal)
        services.AddSingleton<IHardwareProfileService, HardwareProfileService>();
        // Phase 24 — O3-O5: Retention-Service (FirstWin / FTUE / Inactive-Detection)
        services.AddSingleton<IRetentionService, RetentionService>();
        // Phase 25b — Compliance: Privacy-Center (DSGVO/COPPA-Toggles)
        services.AddSingleton<IPrivacyCenter, PrivacyCenter>();
        // Phase 23b — M1+M2: Premium-Pass-Plus + VIP-Subscription
        services.AddSingleton<IBattlePassPlusService, BattlePassPlusService>();
        services.AddSingleton<IVipSubscriptionService, VipSubscriptionService>();
        // v2.0.44 — : Accessibility + DSGVO Account-Löschung + Telemetrie
        services.AddSingleton<IAccessibilityService, AccessibilityService>();
        services.AddSingleton<IAccountDeletionService, AccountDeletionService>();
        services.AddSingleton<ITelemetryService>(sp => TelemetryServiceFactory?.Invoke(sp) ?? new NullTelemetryService());
        services.AddSingleton<IAnalyticsService>(sp => AnalyticsServiceFactory?.Invoke(sp) ?? new NullAnalyticsService());
        services.AddSingleton<IPushNotificationService>(sp => PushNotificationServiceFactory?.Invoke(sp) ?? new NullPushNotificationService());
        //.1  — RemoteConfig: Defaults aus eingebetteter JSON.
        // Android-Override (FirebaseRemoteConfigService) ueberschreibt einzelne Keys spaeter
        // via Cloud-Fetch — Defaults bleiben als Fallback fuer Offline + erste-Start-Szenarien.
        services.AddSingleton<IRemoteConfigService>(sp =>
            RemoteConfigServiceFactory?.Invoke(sp)
            ?? new DefaultsRemoteConfigService(sp.GetRequiredService<ILogger<DefaultsRemoteConfigService>>()));

        //.3  — Re-Engagement-Scheduler (D1/D3/D7-Notifications).
        // Wird von MainActivity beim OnPause/OnResume aufgerufen.
        services.AddSingleton<IReEngagementScheduler, ReEngagementScheduler>();

        //.3  — What's-New-Modal-Service.
        services.AddSingleton<IWhatsNewService, WhatsNewService>();
        //.4  — Feature-Unlock-Choreographer (Queue + Pref-Flag).
        services.AddSingleton<IFeatureUnlockChoreographer, FeatureUnlockChoreographer>();
        //.2  — Mini-Story-Beats pro Welt (Intro + Outro).
        services.AddSingleton<IWorldStoryService, WorldStoryService>();
        //.1  — Hero/Character-System (5 spielbare Charaktere).
        services.AddSingleton<IHeroService, HeroService>();
        //.4  — Wochen-Content-Pipeline (deterministisch via ISO-Woche).
        services.AddSingleton<IWeeklyContentService, WeeklyContentService>();
        //.3  — Clan-System echte Firebase-Implementation.
        // FirebaseClanService nutzt IFirebaseService (existierend) fuer Realtime-DB-Calls.
        // Asynchron via Pull (alle 30s Chat) — kein Live-Sync, kein dedizierter Server.
        services.AddSingleton<IClanService, FirebaseClanService>();
        //.2  — Multiplayer-Session-Service (Foundation, Engine-Integration deferred).
        services.AddSingleton<IMultiplayerSessionService, MultiplayerSessionService>();
        //.2  — IRngProvider (Core-Interface): DeterministicRngProvider als Default
        // fuer Replay-Foundation. SystemRngProvider bleibt fuer Visual-Random (Partikel, Wackel).
        services.AddSingleton<BomberBlast.Core.IRngProvider>(_ =>
            new BomberBlast.Core.DeterministicRngProvider((ulong)DateTime.UtcNow.Ticks));
        //.2  — GameEventBus (Foundation, MainViewModel-Reduktion).
        services.AddSingleton<IGameEventBus, GameEventBus>();
        //.1  — BottomTabHub (Foundation, UI-Refactor deferred).
        services.AddSingleton<IBottomTabHub, BottomTabHub>();

        // Vibration (Android-Override: Echte Vibration statt NullVibrationService)
        if (VibrationServiceFactory != null)
            services.AddSingleton<IVibrationService>(sp => VibrationServiceFactory!(sp));
        else
            services.AddSingleton<IVibrationService, NullVibrationService>();
        services.AddSingleton<IGameTrackingService, GameTrackingService>();
        services.AddSingleton<IGameAssetService, GameAssetService>();
        services.AddSingleton<SoundManager>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<GameRenderer>();
        services.AddSingleton<GameEngine>();

        // ViewModels (alle Singleton: werden von MainViewModel gehalten, dürfen nicht doppelt existieren)
        // Audit M25: Dependency-Aggregat fuer MainViewModel (32-Parameter-Ctor → 1).
        services.AddSingleton<MainViewModelDependencies>();

        // Welle 6 MainViewModel-Refactor: 5 Feature-Module fuer Navigation/Tabs/Dialogs/VM-Registry/Lifecycle.
        //  — Foundation: Interfaces + leere Impls registriert. Logik-Migration in -6.
        services.AddSingleton<BomberBlast.Services.IDialogPresenter, BomberBlast.Services.DialogPresenter>();
        services.AddSingleton<BomberBlast.ViewModels.IChildViewModelRegistry, BomberBlast.ViewModels.ChildViewModelRegistry>();
        services.AddSingleton<BomberBlast.ViewModels.ILifecycleHub, BomberBlast.ViewModels.LifecycleHub>();
        services.AddSingleton<BomberBlast.Navigation.IBottomTabController, BomberBlast.Navigation.BottomTabController>();
        services.AddSingleton<BomberBlast.Navigation.INavigationCoordinator, BomberBlast.Navigation.NavigationCoordinator>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainMenuViewModel>();
        services.AddSingleton<GameViewModel>();
        services.AddSingleton<LevelSelectViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<HighScoresViewModel>();
        services.AddSingleton<GameOverViewModel>();
        // PauseViewModel entfernt — Pause-UI ist im SkiaSharp-Canvas (GameEngine.RenderPausedOverlay),
        // VM war ungenutzt (keine View, keine Bindings, kein Render-Pfad).
        services.AddSingleton<HelpViewModel>();
        services.AddSingleton<ShopViewModel>();
        services.AddSingleton<AchievementsViewModel>();
        services.AddSingleton<DailyChallengeViewModel>();
        services.AddSingleton<VictoryViewModel>();
        services.AddSingleton<LuckySpinViewModel>();
        services.AddSingleton<WeeklyChallengeViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<QuickPlayViewModel>();
        services.AddSingleton<DeckViewModel>();
        services.AddSingleton<DungeonViewModel>();
        services.AddSingleton<BattlePassViewModel>();
        services.AddSingleton<CollectionViewModel>();
        services.AddSingleton<LeagueViewModel>();
        services.AddSingleton<ProfileViewModel>();
        services.AddSingleton<GemShopViewModel>();
        services.AddSingleton<BossRushViewModel>();
        //.1 : Play-Hub — Eager-VM (Kern-Navigation, Bottom-Tab "Spielen").
        services.AddSingleton<PlayHubViewModel>();
        //.3 : What's-New-Modal — Transient (wird bei Bedarf neu erstellt).
        services.AddTransient<WhatsNewViewModel>();
        //.1 : BottomTabBar-VM — Transient (View hat eigene Instanz).
        services.AddTransient<BottomTabBarViewModel>();
    }
}
