using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;
using SunSeeker.Shared.Resources.Strings;
using SunSeeker.Shared.Services;
using SunSeeker.Shared.Services.Anker;
using SunSeeker.Shared.ViewModels;
using SunSeeker.Shared.Views;

namespace SunSeeker.Shared;

public class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Plattform-spezifischer Positions-Provider (Android: LocationManager, Desktop: Mock).</summary>
    public static Func<IServiceProvider, ILocationService>? LocationServiceFactory { get; set; }

    /// <summary>Plattform-spezifischer Heading-Provider (Android: SensorManager, Desktop: Mock).</summary>
    public static Func<IServiceProvider, IHeadingService>? HeadingServiceFactory { get; set; }

    /// <summary>Anker-Powerstation-Monitor (echte Cloud-MQTT-Anbindung oder Mock).</summary>
    public static Func<IServiceProvider, IAnkerMonitorService>? AnkerMonitorServiceFactory { get; set; }

    /// <summary>Öffnet das AR-Sonnenbahn-Overlay (Android: native Kamera-Activity). Auf Desktop null → Button ausgeblendet.</summary>
    public static Action? LaunchSunAr { get; set; }

    private MainViewModel? _mainVm;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Lokalisierung initialisieren (Gerätesprache erkennen/persistieren) BEVOR die erste
            // View geladen wird — die {loc:Translate}-Markup-Extension löst beim View-Load auf.
            var localization = Services.GetRequiredService<ILocalizationService>();
            localization.Initialize();
            LocalizationManager.Initialize(localization);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Desktop nutzt die Mock-Services (keine Hardware) — Factories bleiben null,
                // daher ist sofortiges Auflösen korrekt.
                _mainVm = Services.GetRequiredService<MainViewModel>();
                desktop.MainWindow = new Window
                {
                    Title = "SunSeeker",
                    Width = 450,
                    Height = 900,
                    Content = new MainView { DataContext = _mainVm }
                };
                _ = _mainVm.InitializeAsync();
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime activity)
            {
                // Android (Avalonia 12): MainViewModel ERST in der Factory auflösen — sie wird von
                // AvaloniaActivity in MainActivity.OnCreate.base aufgerufen, also NACH der
                // Platform-Factory-Setzung dort. Zusammen mit der Lazy-Registrierung in
                // ConfigureServices greifen so die echten Android-Services statt der Mocks.
                activity.MainViewFactory = () =>
                {
                    _mainVm = Services.GetRequiredService<MainViewModel>();
                    _ = _mainVm.InitializeAsync();
                    return new MainView { DataContext = _mainVm };
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                _mainVm = Services.GetRequiredService<MainViewModel>();
                singleView.MainView = new MainView { DataContext = _mainVm };
                _ = _mainVm.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SunSeeker Start-Fehler: {ex}");
            throw; // Weiterwerfen, damit der Crash im Logcat sichtbar ist
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Lokalisierung (Preferences für die persistierte Sprachwahl).
        services.AddSingleton<IPreferencesService>(_ => new PreferencesService("SunSeeker"));
        services.AddSingleton<ILocalizationService>(sp =>
            new LocalizationService(AppStrings.ResourceManager, sp.GetRequiredService<IPreferencesService>()));

        // Plattform-Factory LAZY auswerten (Avalonia-12-Android: DI-Build läuft vor
        // MainActivity.OnCreate). Build-Zeit-Prüfung würde den Mock-Fallback einbrennen.
        services.AddSingleton<ILocationService>(sp =>
            LocationServiceFactory != null ? LocationServiceFactory(sp) : new MockLocationService());

        services.AddSingleton<IHeadingService>(sp =>
            HeadingServiceFactory != null ? HeadingServiceFactory(sp) : new MockHeadingService());

        // Demo-Quelle (Watt aus Sonnenstand) — Fallback des echten Monitors, wenn keine Anker-Zugangsdaten gesetzt sind.
        services.AddSingleton<MockAnkerMonitorService>(sp => new MockAnkerMonitorService(
            sp.GetRequiredService<ISolarPositionService>(),
            sp.GetRequiredService<ILocationService>()));

        // Echte Anker-Cloud-/MQTT-Anbindung (plattformneutral via MQTTnet). Factory-Override bleibt möglich.
        services.AddSingleton<IAnkerMonitorService>(sp =>
            AnkerMonitorServiceFactory != null
                ? AnkerMonitorServiceFactory(sp)
                : new AnkerMonitorService(
                    sp.GetRequiredService<IPreferencesService>(),
                    sp.GetRequiredService<MockAnkerMonitorService>()));

        // Plattformneutrale Kern-Engine (reine Berechnung, testbar).
        services.AddSingleton<ISolarPositionService, SolarPositionService>();
        services.AddSingleton<IAlignmentService, AlignmentService>();
        services.AddSingleton<IBifacialService, BifacialService>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<AlignViewModel>();
        services.AddSingleton<LivePowerViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
