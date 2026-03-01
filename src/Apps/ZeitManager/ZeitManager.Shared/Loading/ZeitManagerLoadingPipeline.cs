using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using ZeitManager.Services;
using ZeitManager.ViewModels;

namespace ZeitManager.Loading;

/// <summary>
/// ZeitManager Lade-Pipeline: DB + Shader parallel, dann AlarmScheduler, ViewModel.
/// Gewichtung spiegelt tatsächliche Ladezeiten auf Android wider.
/// </summary>
public class ZeitManagerLoadingPipeline : LoadingPipelineBase
{
    public ZeitManagerLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: DB-Init + Shader-Kompilierung parallel (größter Zeitblock)
        AddStep(new LoadingStep
        {
            Name = "DB+Shader",
            DisplayName = loc.GetString("LoadingInit") ?? "Initialisierung...",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var dbTask = services.GetRequiredService<IDatabaseService>().InitializeAsync();
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                await Task.WhenAll(dbTask, shaderTask);
            }
        });

        // Schritt 2: AlarmScheduler initialisieren (braucht DB)
        AddStep(new LoadingStep
        {
            Name = "AlarmScheduler",
            DisplayName = loc.GetString("LoadingAlarms") ?? "Alarme werden geladen...",
            Weight = 8,
            ExecuteAsync = () => services.GetRequiredService<IAlarmSchedulerService>().InitializeAsync()
        });

        // Schritt 3: MainViewModel erstellen + auf Kind-VM-Initialisierung warten
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("LoadingData") ?? "Daten werden geladen...",
            Weight = 20,
            ExecuteAsync = async () =>
            {
                // ViewModel via DI auflösen (löst alle Child-VMs aus)
                var mainVm = services.GetRequiredService<MainViewModel>();
                // Warten bis Timer, Alarme etc. aus DB geladen sind
                await mainVm.WaitForInitializationAsync();
            }
        });
    }
}
