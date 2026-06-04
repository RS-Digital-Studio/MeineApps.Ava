using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SunSeeker.Shared.Services;
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

    private MainViewModel? _mainVm;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Desktop nutzt die Mock-Services (keine Hardware) — Factories bleiben null,
                // daher ist sofortiges Aufloesen korrekt.
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
                // Android (Avalonia 12): MainViewModel ERST in der Factory aufloesen — sie wird von
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
        // Plattform-Factory LAZY auswerten (Avalonia-12-Android: DI-Build laeuft vor
        // MainActivity.OnCreate). Build-Zeit-Pruefung wuerde den Mock-Fallback einbrennen.
        services.AddSingleton<ILocationService>(sp =>
            LocationServiceFactory != null ? LocationServiceFactory(sp) : new MockLocationService());

        services.AddSingleton<IHeadingService>(sp =>
            HeadingServiceFactory != null ? HeadingServiceFactory(sp) : new MockHeadingService());

        // Plattformneutrale Kern-Engine (reine Berechnung, testbar).
        services.AddSingleton<ISolarPositionService, SolarPositionService>();
        services.AddSingleton<IAlignmentService, AlignmentService>();
        services.AddSingleton<IBifacialService, BifacialService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
    }
}
