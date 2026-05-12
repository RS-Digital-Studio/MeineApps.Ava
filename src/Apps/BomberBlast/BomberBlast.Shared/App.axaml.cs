using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
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
    /// Factory fuer plattformspezifischen IRemoteConfigService (Android setzt FirebaseRemoteConfigService — Sprint 2.1).
    /// Bis dahin: NullRemoteConfigService liefert Defaults (Sprint 1.4c Stub).
    /// </summary>
    public static Func<IServiceProvider, IRemoteConfigService>? RemoteConfigServiceFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    /// <summary>
    /// Sprint 6.3 AAA-Audit #25: Splash-Crash-Recovery Pref-Key.
    /// Inkrementiert vor jedem Init-Versuch, dekrementiert nach erfolgreichem Splash-Abschluss.
    /// Bei >= 3 Crashes in Folge wird der User zum Reset-Dialog geleitet.
    /// </summary>
    private const string KeyCrashCount = "BomberBlast_AppCrashCount";
    private const int CrashRecoveryThreshold = 3;

    public override void OnFrameworkInitializationCompleted()
    {
        // Sprint 6.3 AAA-Audit #25: Crash-Counter VOR der Init-Phase inkrementieren.
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

        // Statischer Logger für ShaderEffects (nicht DI-verwaltet)
        ShaderEffects.Logger = Services.GetRequiredService<IAppLogger>();
        // PersistenceHealth: Logger setzen fuer Corrupt-Preferences-Meldungen (CoinService, GemService, ProgressService, DailyRewardService)
        PersistenceHealth.Logger = Services.GetRequiredService<IAppLogger>();
        // RewardedAdCooldownTracker: Preferences-Hook fuer persistierten Cooldown (Schutz gegen App-Restart-Bypass)
        RewardedAdCooldownTracker.Preferences = Services.GetRequiredService<IPreferencesService>();
        // GameLoopSettings: Persistierten TargetFps-Wert laden (30/60 FPS, default 30 Battery-Mode)
        GameLoopSettings.Initialize(Services.GetRequiredService<IPreferencesService>());

        // v2.0.44 — AAA-Audit: Telemetrie + Analytics + Push-Notifications initialisieren.
        // Bei NullImpl auf Desktop ist das ein No-Op. Auf Android sucht ein konfigurierter
        // Firebase-Setup nach google-services.json (Console-Setup vom User).
        // Sprint 6.3 AAA-Audit #25: Safe-Mode skippt optionale Services (Firebase + Push)
        // damit die App garantiert startet wenn ein optionaler Service der Crash-Ursache war.
        // Game-State + UI funktionieren weiterhin — User kommt ans Settings-Menue,
        // kann Account-Delete oder Reset durchfuehren.
        if (!safeMode)
        {
            try { Services.GetRequiredService<ITelemetryService>().Initialize(); }
            catch (Exception ex) { Services.GetService<IAppLogger>()?.LogError("Telemetry-Init fehlgeschlagen", ex); }

            try { Services.GetRequiredService<IAnalyticsService>().Initialize(); }
            catch (Exception ex) { Services.GetService<IAppLogger>()?.LogError("Analytics-Init fehlgeschlagen", ex); }

            try { _ = Services.GetRequiredService<IPushNotificationService>().InitializeAsync(); }
            catch (Exception ex) { Services.GetService<IAppLogger>()?.LogError("Push-Init fehlgeschlagen", ex); }

            try { _ = Services.GetRequiredService<IRemoteConfigService>().InitializeAsync(); }
            catch (Exception ex) { Services.GetService<IAppLogger>()?.LogError("RemoteConfig-Init fehlgeschlagen", ex); }
        }
        else
        {
            // Safe-Mode: Schreibe einen Diagnose-Eintrag damit Crashlytics weiss, warum dieser Start
            // ohne optionale Services ist. Wird beim naechsten Online-Start gepushed.
            Services.GetService<IAppLogger>()?.LogWarning(
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
            // Sprint 1.4a AAA-Audit: Splash-Version aus Assembly auslesen statt hardcoded.
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

            // Sprint 6.3 AAA-Audit #25: Pipeline-Erfolg → Crash-Counter zuruecksetzen.
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
            // Sprint 4.1 AAA-Audit: AppLogger forwarded an ITelemetryService.LogNonFatal mit Stack-Trace.
            Services?.GetService<IAppLogger>()?.LogError("Loading-Pipeline fehlgeschlagen", ex);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => splash.FadeOut());
        }
    }

    /// <summary>
    /// Sprint 6.3 AAA-Audit #25: Public API fuer den Settings-Screen
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
                (Services.GetService<GameRenderer>() as IDisposable)?.Dispose();
                (gameVm as IDisposable)?.Dispose();
            }
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
        // Logging — Sprint 4.1 AAA-Audit #9: AppLogger leitet Errors/Warnings an Crashlytics weiter.
        // ITelemetryService kommt unten — Lazy-Resolution noetig damit der Logger waehrend
        // der DI-Aufbauphase nicht in eine Zirkularitaet faellt.
        services.AddSingleton<IAppLogger>(sp => new AppLogger(sp.GetService<ITelemetryService>()));

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
        // Phase 20 — AAA-Audit L2: Wöchentlicher Event-Calendar (deterministisch via ISO-Week)
        services.AddSingleton<IEventCalendarService, EventCalendarService>();
        // Phase 23 — AAA-Audit M5: First-Time-Purchase-Bonus (×2 auf ersten Kauf)
        services.AddSingleton<IFirstPurchaseService, FirstPurchaseService>();
        // Phase 25 — DSGVO Art. 20: Datenexport-Service (Account-Holder können ihre Daten als JSON beziehen)
        services.AddSingleton<IDataExportService, DataExportService>();
        // Phase 27 — AAA-Audit P2/P3/P4: Hardware-Profile-Service (Quality-Tier + Battery + Thermal)
        services.AddSingleton<IHardwareProfileService, HardwareProfileService>();
        // Phase 24 — AAA-Audit O3-O5: Retention-Service (FirstWin / FTUE / Inactive-Detection)
        services.AddSingleton<IRetentionService, RetentionService>();
        // Phase 25b — AAA-Audit Compliance: Privacy-Center (DSGVO/COPPA-Toggles)
        services.AddSingleton<IPrivacyCenter, PrivacyCenter>();
        // Phase 23b — AAA-Audit M1+M2: Premium-Pass-Plus + VIP-Subscription
        services.AddSingleton<IBattlePassPlusService, BattlePassPlusService>();
        services.AddSingleton<IVipSubscriptionService, VipSubscriptionService>();
        // v2.0.44 — AAA-Audit: Accessibility + DSGVO Account-Löschung + Telemetrie
        services.AddSingleton<IAccessibilityService, AccessibilityService>();
        services.AddSingleton<IAccountDeletionService, AccountDeletionService>();
        services.AddSingleton<ITelemetryService>(sp => TelemetryServiceFactory?.Invoke(sp) ?? new NullTelemetryService());
        services.AddSingleton<IAnalyticsService>(sp => AnalyticsServiceFactory?.Invoke(sp) ?? new NullAnalyticsService());
        services.AddSingleton<IPushNotificationService>(sp => PushNotificationServiceFactory?.Invoke(sp) ?? new NullPushNotificationService());
        // Sprint 2.1 AAA-Audit #1 — RemoteConfig: Defaults aus eingebetteter JSON.
        // Android-Override (FirebaseRemoteConfigService) ueberschreibt einzelne Keys spaeter
        // via Cloud-Fetch — Defaults bleiben als Fallback fuer Offline + erste-Start-Szenarien.
        services.AddSingleton<IRemoteConfigService>(sp =>
            RemoteConfigServiceFactory?.Invoke(sp)
            ?? new DefaultsRemoteConfigService(sp.GetRequiredService<IAppLogger>()));

        // Sprint 2.3 AAA-Audit #3 — Re-Engagement-Scheduler (D1/D3/D7-Notifications).
        // Wird von MainActivity beim OnPause/OnResume aufgerufen.
        services.AddSingleton<IReEngagementScheduler, ReEngagementScheduler>();

        // Sprint 4.3 AAA-Audit #17 — What's-New-Modal-Service.
        services.AddSingleton<IWhatsNewService, WhatsNewService>();
        // Sprint 4.4 AAA-Audit #20 — Feature-Unlock-Choreographer (Queue + Pref-Flag).
        services.AddSingleton<IFeatureUnlockChoreographer, FeatureUnlockChoreographer>();
        // Sprint 6.2 AAA-Audit #16 — Mini-Story-Beats pro Welt (Intro + Outro).
        services.AddSingleton<IWorldStoryService, WorldStoryService>();

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
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainMenuViewModel>();
        services.AddSingleton<GameViewModel>();
        services.AddSingleton<LevelSelectViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<HighScoresViewModel>();
        services.AddSingleton<GameOverViewModel>();
        services.AddSingleton<PauseViewModel>();
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
        // Sprint 4.3 AAA-Audit #17: What's-New-Modal — Transient (wird bei Bedarf neu erstellt).
        services.AddTransient<WhatsNewViewModel>();
    }
}
