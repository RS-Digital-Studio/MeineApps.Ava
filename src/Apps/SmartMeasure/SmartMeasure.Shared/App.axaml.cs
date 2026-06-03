using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

    /// <summary>Plan-Kap. 5.12: Plattform-Voice-Service-Factory. Wenn nicht gesetzt
    /// (Desktop/Mock), faellt DI auf <see cref="NullVoiceAnnotationService"/> zurueck.</summary>
    public static Func<IServiceProvider, IVoiceAnnotationService>? VoiceAnnotationServiceFactory { get; set; }

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

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Desktop nutzt bewusst die Mock-Services (keine Hardware) — die Platform-
                // Factories bleiben null, daher ist sofortiges Aufloesen korrekt.
                _mainVm = Services.GetRequiredService<MainViewModel>();
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
                // Android (Avalonia 12): OnFrameworkInitializationCompleted laeuft bereits in
                // AvaloniaAndroidApplication.OnCreate — also VOR MainActivity.OnCreate, das die
                // Platform-Factories (AR/BLE/AppPaths/Voice) erst setzt. Wuerde das MainViewModel
                // hier sofort aufgeloest, bekaeme der gesamte Objektgraph die Mock-Fallbacks
                // injiziert (MockArCaptureService → 10 Punkte ohne Kamera, MockBleService, ...).
                // Deshalb das Aufloesen via Dispatcher verzoegern: der Post laeuft nach
                // MainActivity.OnCreate, sodass die echten Android-Services greifen. Zusammen mit
                // der Lazy-Registrierung in ConfigureServices ist die Service-Wahl damit korrekt.
                var mainView = new MainView();
                singleView.MainView = mainView;
                Dispatcher.UIThread.Post(() =>
                {
                    _mainVm = Services.GetRequiredService<MainViewModel>();
                    mainView.DataContext = _mainVm;
                    _ = _mainVm.InitializeAsync();
                });
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
        // etc. hängen davon ab. Auf Android liefert die Factory sandbox-sichere Pfade,
        // auf Desktop greift der AppPaths-Fallback (ApplicationData).
        //
        // WICHTIG (Avalonia 12 Android): Die Factory-Pruefung MUSS lazy (zur Resolve-Zeit) im
        // Lambda passieren, NICHT beim BuildServiceProvider. OnFrameworkInitializationCompleted
        // laeuft VOR MainActivity.OnCreate (siehe dortigen Kommentar) — beim DI-Build sind die
        // Platform-Factories also noch null. Eine Build-Zeit-Pruefung (if (Factory != null))
        // wuerde dauerhaft den Mock-Fallback einbrennen. Das Lambda liest die Factory erst beim
        // ersten Resolve, der durch das verzoegerte MainViewModel-Aufloesen nach der
        // Factory-Setzung liegt → echte Android-Services statt Mock.
        services.AddSingleton<IAppPaths>(_ =>
            AppPathsFactory != null ? AppPathsFactory() : new AppPaths());

        // Preferences (JSON-Persistenz für User-Settings)
        services.AddSingleton<IPreferencesService>(_ => new PreferencesService("SmartMeasure"));

        // BLE-Service (plattform-spezifisch oder Mock) — lazy, siehe IAppPaths-Hinweis oben.
        services.AddSingleton<IBleService>(sp =>
            BleServiceFactory != null ? BleServiceFactory(sp) : new MockBleService());

        // AR-Capture-Service (plattform-spezifisch oder Mock) — lazy, siehe IAppPaths-Hinweis oben.
        services.AddSingleton<IArCaptureService>(sp =>
            ArCaptureServiceFactory != null ? ArCaptureServiceFactory(sp) : new MockArCaptureService());

        // Adaptiver Betriebsmodus (AR-First vs RTK-Stab) — haengt von IBleService + Preferences ab.
        services.AddSingleton<IHardwareModeService, HardwareModeService>();

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
        services.AddSingleton<IGnssConditionService, GnssConditionService>();
        services.AddSingleton<IVolumeService, VolumeService>();
        services.AddSingleton<ITotalStationService, TotalStationService>();
        services.AddSingleton<ILeastSquaresAdjustmentService, LeastSquaresAdjustmentService>();
        // Voice/Multi-User/Mesh: Interface-Stubs ohne Default-Impl — werden vom
        // jeweiligen Plattform-Modul oder einer Folge-Iteration verkabelt.
        // Voice-Annotation (Android: SpeechRecognizer, sonst Null) — lazy, siehe IAppPaths-Hinweis oben.
        services.AddSingleton<IVoiceAnnotationService>(sp =>
            VoiceAnnotationServiceFactory != null ? VoiceAnnotationServiceFactory(sp) : new NullVoiceAnnotationService());
        services.AddSingleton<ISurveyReportService, SurveyReportService>();
        services.AddSingleton<ISceneReconstructionService, SceneReconstructionService>();
        services.AddSingleton<IMultiUserSessionService, LocalTcpMultiUserService>();

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
