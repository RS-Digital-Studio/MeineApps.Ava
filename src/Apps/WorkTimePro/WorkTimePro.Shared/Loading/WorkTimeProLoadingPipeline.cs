using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using WorkTimePro.Services;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Loading;

/// <summary>
/// WorkTimePro Lade-Pipeline: DB + Shader parallel, dann Achievement, Reminder, ViewModel.
/// Gewichtung spiegelt tatsächliche Ladezeiten auf Android wider.
/// </summary>
public class WorkTimeProLoadingPipeline : LoadingPipelineBase
{
    public WorkTimeProLoadingPipeline(IServiceProvider services)
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

        // Schritt 2: Achievement-Service initialisieren (Tabelle + Definitionen)
        AddStep(new LoadingStep
        {
            Name = "Achievement",
            DisplayName = loc.GetString("LoadingAchievements") ?? "Erfolge werden geladen...",
            Weight = 8,
            ExecuteAsync = () => services.GetRequiredService<IAchievementService>().InitializeAsync()
        });

        // Schritt 3: Reminder-Service initialisieren
        AddStep(new LoadingStep
        {
            Name = "Reminder",
            DisplayName = loc.GetString("LoadingReminders") ?? "Erinnerungen werden geladen...",
            Weight = 5,
            ExecuteAsync = () => services.GetRequiredService<IReminderService>().InitializeAsync()
        });

        // Schritt 4: MainViewModel erstellen + initiale Daten laden
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("LoadingData") ?? "Daten werden geladen...",
            Weight = 20,
            ExecuteAsync = async () =>
            {
                var mainVm = services.GetRequiredService<MainViewModel>();
                await mainVm.WaitForInitializationAsync();
            }
        });
    }
}
