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
public sealed class RechnerPlusLoadingPipeline : LoadingPipelineBase
{
    public RechnerPlusLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader + ViewModel parallel (unabhängig voneinander)
        AddStep(new LoadingStep
        {
            Name = "Shader+ViewModel",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                var vmTask = Task.Run(() => services.GetRequiredService<MainViewModel>());
                await Task.WhenAll(shaderTask, vmTask);
            }
        });
    }
}
