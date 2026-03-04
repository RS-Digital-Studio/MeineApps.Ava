using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using HandwerkerRechner.ViewModels;

namespace HandwerkerRechner.Loading;

/// <summary>
/// HandwerkerRechner Lade-Pipeline: Shader + History + Projekte + ViewModel.
/// </summary>
public sealed class HandwerkerRechnerLoadingPipeline : LoadingPipelineBase
{
    public HandwerkerRechnerLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader + ViewModel parallel (unabhängig voneinander)
        AddStep(new LoadingStep
        {
            Name = "Shader+ViewModel",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 45,
            ExecuteAsync = async () =>
            {
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                var vmTask = Task.Run(() => services.GetRequiredService<MainViewModel>());
                // Käufe mit Google Play abgleichen (Geräte-/Datenwechsel → Premium-Status wiederherstellen)
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                await Task.WhenAll(shaderTask, vmTask, purchaseTask);
            }
        });
    }
}
