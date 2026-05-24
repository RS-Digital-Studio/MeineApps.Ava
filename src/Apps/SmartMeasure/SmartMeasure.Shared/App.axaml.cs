using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;
using SmartMeasure.Shared.Services;
using SmartMeasure.Shared.ViewModels;
using SmartMeasure.Shared.Views;

namespace SmartMeasure.Shared;

public class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Plattform-spezifischer BleService (Android: native, Desktop: InTheHand)</summary>
    public static Func<IServiceProvider, IBleService>? BleServiceFactory { get; set; }

    /// <summary>Plattform-spezifischer AR-Capture-Service (Android: ARCore, Desktop: Mock)</summary>
    public static Func<IServiceProvider, IArCaptureService>? ArCaptureServiceFactory { get; set; }

    /// <summary>Plattform-spezifischer IAppPaths (Android: Context.FilesDir, Desktop: ApplicationData)</summary>
    public static Func<IAppPaths>? AppPathsFactory { get; set; }

    private MainViewModel? _mainVm;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
            _mainVm = Services.GetRequiredService<MainViewModel>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Window
                {
                    Title = "SmartMeasure - 3D-Vermessung",
                    Width = 450,
                    Height = 900,
                    Content = new MainView { DataContext = _mainVm }
                };

                _ = _mainVm.InitializeAsync();
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainView { DataContext = _mainVm };
                _ = _mainVm.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SmartMeasure Start-Fehler: {ex}");
            throw; // Weiterwerfen damit der Crash-Log im Logcat sichtbar ist
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // IAppPaths MUSS als erstes registriert werden — ProjectService, ExportService
        // etc. hängen davon ab. Auf Android liefert Factory sandbox-sicheren Pfad,
        // auf Desktop Fallback auf AppPaths (ApplicationData).
        if (AppPathsFactory != null)
            services.AddSingleton(_ => AppPathsFactory());
        else
            services.AddSingleton<IAppPaths, AppPaths>();

        // Preferences (JSON-Persistenz für User-Settings)
        services.AddSingleton<IPreferencesService>(_ => new PreferencesService("SmartMeasure"));

        // BLE-Service (plattform-spezifisch oder Mock)
        if (BleServiceFactory != null)
            services.AddSingleton(BleServiceFactory);
        else
            services.AddSingleton<IBleService, MockBleService>();

        // AR-Capture-Service (plattform-spezifisch oder Mock)
        if (ArCaptureServiceFactory != null)
            services.AddSingleton(ArCaptureServiceFactory);
        else
            services.AddSingleton<IArCaptureService, MockArCaptureService>();

        // Services
        services.AddSingleton<IMeasurementService, MeasurementService>();
        services.AddSingleton<ICoordinateService, CoordinateService>();
        services.AddSingleton<IGeoidService, Egm96GeoidService>();
        services.AddSingleton<ITerrainService, TerrainService>();
        services.AddSingleton<IGardenPlanService, GardenPlanService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IBlenderExportService, BlenderExportService>();
        services.AddSingleton<IArTransferService, ArTransferService>();
        services.AddSingleton<IDifferentialSnapshotService, DifferentialSnapshotService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ConnectViewModel>();
        services.AddSingleton<SurveyViewModel>();
        services.AddSingleton<TerrainViewModel>();
        services.AddSingleton<GardenPlanViewModel>();
        services.AddSingleton<MapViewModel>();
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<StakeoutViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }
}
