using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using HandwerkerRechner.ViewModels;

namespace HandwerkerRechner.Loading;

/// <summary>
/// HandwerkerRechner Lade-Pipeline: Shader + History + Projekte + ViewModel.
/// </summary>
public class HandwerkerRechnerLoadingPipeline : LoadingPipelineBase
{
    public HandwerkerRechnerLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader-Kompilierung (Visualisierungen: Tile, Paint, Wallpaper etc.)
        AddStep(new LoadingStep
        {
            Name = "Shader",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 30,
            ExecuteAsync = () => Task.Run(() => ShaderPreloader.PreloadAll())
        });

        // Schritt 2: ViewModel erstellen (lädt CraftEngine, History, Projects)
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
