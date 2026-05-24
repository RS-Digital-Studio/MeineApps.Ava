using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using WorkTimePro.Services;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Loading;

/// <summary>
/// WorkTimePro Lade-Pipeline: DB + Shader parallel, dann Reminder, ViewModel.
/// Gewichtung spiegelt tatsächliche Ladezeiten auf Android wider.
/// </summary>
public sealed class WorkTimeProLoadingPipeline : LoadingPipelineBase
{
    public WorkTimeProLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: DB-Init + Shader-Kompilierung + Purchase parallel; danach Reminder
        // (Reminder hängt von DB ab → muss sequenziell nach DB laufen, aber Pipeline-Stage
        // entfällt — spart einen ProgressChanged-Event-Roundtrip).
        AddStep(new LoadingStep
        {
            Name = "Init",
            DisplayName = loc.GetString("LoadingInit") ?? "Initialisierung...",
            Weight = 45,
            ExecuteAsync = async () =>
            {
                var dbTask = services.GetRequiredService<IDatabaseService>().InitializeAsync();
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                // Käufe mit Google Play abgleichen (Geräte-/Datenwechsel → Premium-Status wiederherstellen)
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                await Task.WhenAll(dbTask, shaderTask, purchaseTask);

                // Reminder hängt von DB ab — sequenziell, aber innerhalb derselben Stage
                await services.GetRequiredService<IReminderService>().InitializeAsync();
            }
        });

        // Schritt 2: MainViewModel erstellen + initiale Daten laden
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
