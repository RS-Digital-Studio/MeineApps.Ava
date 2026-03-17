using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Extensions;
using MeineApps.Core.Premium.Ava.Services;
using RebornSaga.Engine;
using RebornSaga.Rendering.Backgrounds;
using RebornSaga.Rendering.Characters;
using RebornSaga.Services;
using RebornSaga.ViewModels;
using RebornSaga.Views;
using System.Threading.Tasks;

namespace RebornSaga;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Factory fuer plattformspezifischen IRewardedAdService (Android setzt RewardedAdHelper).
    /// </summary>
    public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IPurchaseService (Android setzt AndroidPurchaseService).
    /// </summary>
    public static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory { get; set; }

    /// <summary>
    /// Factory fuer plattformspezifischen IAudioService (Android setzt AndroidAudioService).
    /// </summary>
    public static Func<IServiceProvider, IAudioService>? AudioServiceFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // DI-Container aufbauen
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Content = new MainView();
            desktop.MainWindow.DataContext = Services.GetRequiredService<MainViewModel>();

            // Desktop: Beim Herunterfahren alle IDisposable-Singletons freigeben
            desktop.ShutdownRequested += (_, _) => DisposeServices();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView();
            singleViewPlatform.MainView.DataContext = Services.GetRequiredService<MainViewModel>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Initialisiert alle Services die Daten beim Start laden müssen.
    /// Muss aufgerufen werden bevor SaveGameService.LoadGameAsync() verwendet wird.
    /// </summary>
    public static async Task InitializeServicesAsync()
    {
        // Skill- und Item-Definitionen parallel aus Embedded JSON laden
        // (Voraussetzung für SaveGameService.LoadGameAsync)
        var skillService = Services.GetRequiredService<SkillService>();
        var inventoryService = Services.GetRequiredService<InventoryService>();
        await Task.WhenAll(
            Task.Run(() => skillService.LoadSkills()),
            Task.Run(() => inventoryService.LoadItems()));

        // Sprite-Rendering initialisieren (SpriteCache → CharacterRenderer + BackgroundCompositor)
        var spriteCache = Services.GetRequiredService<SpriteCache>();
        CharacterRenderer.Initialize(spriteCache);
        BackgroundCompositor.SetSpriteCache(spriteCache);

        // Käufe über Google Play wiederherstellen (Premium-Status nach Gerätewechsel)
        var purchaseService = Services.GetService<IPurchaseService>();
        if (purchaseService != null)
            _ = purchaseService.InitializeAsync(); // AppChecker:ignore - Fire-and-forget OK, verschluckt Fehler intern
    }

    /// <summary>
    /// Gibt alle IDisposable-Singletons frei.
    /// Desktop: ShutdownRequested, Android: OnDestroy.
    /// </summary>
    public static void DisposeServices()
    {
        if (Services == null) return;

        try
        {
            // AudioService: SoundPool + MediaPlayer auf Android freigeben
            Services.GetService<IAudioService>()?.Dispose();

            // SpriteCache: Alle gecachten Bitmaps freigeben
            Services.GetService<SpriteCache>()?.Dispose();

            // SaveGameService: SQLite-Verbindung schließen
            Services.GetService<SaveGameService>()?.Dispose();
        }
        catch
        {
            // Fehler beim Dispose beim Herunterfahren sind unkritisch
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IPreferencesService>(sp => new PreferencesService("RebornSaga"));

        // Lokalisierung (ResourceManager-basiert, 6 Sprachen)
        services.AddSingleton<ILocalizationService>(sp =>
        {
            var locService = new LocalizationService(
                RebornSaga.Resources.Strings.AppStrings.ResourceManager,
                sp.GetRequiredService<IPreferencesService>());
            locService.Initialize();
            return locService;
        });

        // Premium Services (Ads, Purchases)
        services.AddMeineAppsPremium();

        // Android-Override: Echte Rewarded Ads statt Desktop-Simulator
        if (RewardedAdServiceFactory != null)
            services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));

        // Android-Override: Echte Google Play Billing statt Stub
        if (PurchaseServiceFactory != null)
            services.AddSingleton<IPurchaseService>(sp => PurchaseServiceFactory!(sp));

        // Asset-Delivery (Firebase Storage → lokaler Cache)
        services.AddSingleton<IAssetDeliveryService, AssetDeliveryService>();

        // Sprite-Cache (LRU-Cache fuer AI-generierte Charakter-Atlanten)
        services.AddSingleton<SpriteCache>();

        // Engine + Story + Kampf
        services.AddSingleton<SceneManager>();
        services.AddSingleton<StoryEngine>();
        services.AddSingleton<BattleEngine>();

        // RPG-Systeme
        services.AddSingleton<SkillService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<AffinityService>();
        services.AddSingleton<FateTrackingService>();
        services.AddSingleton<CodexService>();
        services.AddSingleton<ProgressionService>();
        services.AddSingleton<SaveGameService>();
        services.AddSingleton<GoldService>();
        services.AddSingleton<ChapterUnlockService>();

        services.AddSingleton<TutorialService>();
        services.AddSingleton<DailyService>();

        // Audio (Desktop-Stub, Android-Override via Factory)
        if (AudioServiceFactory != null)
            services.AddSingleton<IAudioService>(sp => AudioServiceFactory!(sp));
        else
            services.AddSingleton<IAudioService, AudioService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
    }
}
