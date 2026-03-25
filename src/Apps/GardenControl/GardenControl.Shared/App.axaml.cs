using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GardenControl.Shared.Services;
using GardenControl.Shared.ViewModels;
using GardenControl.Shared.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GardenControl.Shared;

public class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Läuft die App auf einem Raspberry Pi?</summary>
    public static bool IsRunningOnPi { get; private set; }

    private MainViewModel? _mainVm;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        IsRunningOnPi = DetectRaspberryPi();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<IConnectionService, ConnectionService>();
        services.AddSingleton<IApiService, ApiService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ZoneControlViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<CalibrationViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();

        Services = services.BuildServiceProvider();
        _mainVm = Services.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (IsRunningOnPi)
            {
                _mainVm.ServerUrl = "http://localhost:5000";
                desktop.MainWindow = new Window
                {
                    Title = "GardenControl",
                    WindowState = WindowState.FullScreen,
                    SystemDecorations = SystemDecorations.None,
                    Content = new MainView { DataContext = _mainVm }
                };
            }
            else
            {
                desktop.MainWindow = new Window
                {
                    Title = "GardenControl - Bewässerungssteuerung",
                    Width = 1200,
                    Height = 800,
                    Content = new MainView { DataContext = _mainVm }
                };
            }

            // Sauberes Cleanup beim Beenden
            desktop.ShutdownRequested += async (_, _) =>
            {
                if (_mainVm != null)
                    await _mainVm.DisposeAsync();
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

    private static bool DetectRaspberryPi()
    {
        if (!OperatingSystem.IsLinux()) return false;
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64 &&
            RuntimeInformation.ProcessArchitecture != Architecture.Arm)
            return false;

        try
        {
            var model = File.ReadAllText("/proc/device-tree/model");
            return model.Contains("Raspberry", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
