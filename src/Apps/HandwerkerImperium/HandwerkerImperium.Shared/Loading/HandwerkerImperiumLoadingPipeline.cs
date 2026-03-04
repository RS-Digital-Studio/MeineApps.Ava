using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Loading;

/// <summary>
/// HandwerkerImperium Lade-Pipeline: Shader + Spielstand + InitializeAsync.
/// Übernimmt die gesamte Initialisierung die bisher in MainViewModel.InitializeAsync() war.
/// Die Pipeline meldet Fortschritt an den Splash-Screen.
/// </summary>
public sealed class HandwerkerImperiumLoadingPipeline : LoadingPipelineBase
{
    public HandwerkerImperiumLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader + ViewModel parallel (unabhängig voneinander)
        AddStep(new LoadingStep
        {
            Name = "Shader+ViewModel",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik-Engine laden...",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                var vmTask = Task.Run(() => services.GetRequiredService<MainViewModel>());
                // Käufe mit Google Play abgleichen (Geräte-/Datenwechsel → Premium-Status wiederherstellen)
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                await Task.WhenAll(shaderTask, vmTask, purchaseTask);
            }
        });

        // Schritt 2: Spielstand laden + initialisieren (SaveGame, Orders, Rewards etc.)
        AddStep(new LoadingStep
        {
            Name = "GameInit",
            DisplayName = loc.GetString("SplashStep_Workshops") ?? "Werkstätten einrichten...",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var mainVm = services.GetRequiredService<MainViewModel>();
                await mainVm.InitializeAsync();
            }
        });
    }
}
