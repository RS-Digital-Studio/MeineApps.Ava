using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using BomberBlast.Graphics;
using BomberBlast.ViewModels;

namespace BomberBlast.Loading;

/// <summary>
/// BomberBlast Lade-Pipeline: Shader + ExplosionShader + Audio + ViewModel.
/// Schwere GPU-Shader-Kompilierung (12 Standard + ExplosionShaders) während Splash.
/// </summary>
public class BomberBlastLoadingPipeline : LoadingPipelineBase
{
    public BomberBlastLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader-Kompilierung (12 Standard + ExplosionShaders)
        AddStep(new LoadingStep
        {
            Name = "Shader",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik-Engine laden...",
            Weight = 40,
            ExecuteAsync = () => Task.Run(() =>
            {
                ShaderPreloader.PreloadAll();
                // ExplosionShaders vorab kompilieren (verhindert Jank beim ersten Explosions-Frame)
                ExplosionShaders.Preload();
            })
        });

        // Schritt 2: ViewModel erstellen (löst alle Services auf: Progress, Shop, Cards, Dungeon etc.)
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("SplashStep_Starting") ?? "Spiel starten...",
            Weight = 20,
            ExecuteAsync = () =>
            {
                services.GetRequiredService<MainViewModel>();
                return Task.CompletedTask;
            }
        });
    }
}
