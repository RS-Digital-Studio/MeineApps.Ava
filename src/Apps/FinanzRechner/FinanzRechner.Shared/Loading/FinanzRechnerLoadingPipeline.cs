using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using FinanzRechner.Services;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Loading;

/// <summary>
/// FinanzRechner Lade-Pipeline: Shader + DB-Init + ViewModel.
/// ExpenseService.InitializeAsync() wird vorab ausgeführt (statt lazy in MainView).
/// </summary>
public class FinanzRechnerLoadingPipeline : LoadingPipelineBase
{
    public FinanzRechnerLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader + DB parallel (größter Zeitblock)
        AddStep(new LoadingStep
        {
            Name = "DB+Shader",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var dbTask = services.GetRequiredService<IExpenseService>().InitializeAsync();
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                await Task.WhenAll(dbTask, shaderTask);
            }
        });

        // Schritt 2: ViewModel erstellen (bindet an ExpenseService-Daten)
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("SplashStep_Starting") ?? "App starten...",
            Weight = 15,
            ExecuteAsync = () =>
            {
                services.GetRequiredService<MainViewModel>();
                return Task.CompletedTask;
            }
        });
    }
}
