using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using RechnerPlus.ViewModels;

namespace RechnerPlus.Loading;

/// <summary>
/// RechnerPlus Lade-Pipeline: Shader-Kompilierung + ViewModel-Erstellung.
/// Leichtgewichtig (keine DB), aber Shader-Preload verhindert Jank.
/// </summary>
public class RechnerPlusLoadingPipeline : LoadingPipelineBase
{
    public RechnerPlusLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader-Kompilierung (verhindert Jank beim ersten SkiaSharp-Render)
        AddStep(new LoadingStep
        {
            Name = "Shader",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 30,
            ExecuteAsync = () => Task.Run(() => ShaderPreloader.PreloadAll())
        });

        // Schritt 2: ViewModel erstellen (CalcLib Engine, HistoryService)
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("SplashStep_Starting") ?? "App starten...",
            Weight = 10,
            ExecuteAsync = () =>
            {
                services.GetRequiredService<MainViewModel>();
                return Task.CompletedTask;
            }
        });
    }
}
