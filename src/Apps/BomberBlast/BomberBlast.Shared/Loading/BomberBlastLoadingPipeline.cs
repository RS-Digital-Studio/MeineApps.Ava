using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
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
public sealed class BomberBlastLoadingPipeline : LoadingPipelineBase
{
    public BomberBlastLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader-Kompilierung + ViewModel parallel (unabhängig voneinander)
        AddStep(new LoadingStep
        {
            Name = "Shader+ViewModel",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik-Engine laden...",
            Weight = 60,
            ExecuteAsync = async () =>
            {
                var shaderTask = Task.Run(() =>
                {
                    ShaderPreloader.PreloadAll();
                    // ExplosionShaders vorab kompilieren (verhindert Jank beim ersten Explosions-Frame)
                    ExplosionShaders.Preload();
                });
                var vmTask = Task.Run(() => services.GetRequiredService<MainViewModel>());
                // Käufe mit Google Play abgleichen (Geräte-/Datenwechsel → Premium-Status wiederherstellen)
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                await Task.WhenAll(shaderTask, vmTask, purchaseTask);
            }
        });
    }
}
