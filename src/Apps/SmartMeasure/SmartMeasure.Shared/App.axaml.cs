using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

    private MainViewModel? _mainVm;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
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

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // BLE-Service (plattform-spezifisch oder Mock)
        if (BleServiceFactory != null)
            services.AddSingleton(BleServiceFactory);
        else
            services.AddSingleton<IBleService, MockBleService>();

        // Services
        services.AddSingleton<IMeasurementService, MeasurementService>();
        services.AddSingleton<ICoordinateService, CoordinateService>();
        services.AddSingleton<ITerrainService, TerrainService>();
        services.AddSingleton<IGardenPlanService, GardenPlanService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IExportService, ExportService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ConnectViewModel>();
        services.AddSingleton<SurveyViewModel>();
        services.AddSingleton<TerrainViewModel>();
        services.AddSingleton<GardenPlanViewModel>();
        services.AddSingleton<MapViewModel>();
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }
}
