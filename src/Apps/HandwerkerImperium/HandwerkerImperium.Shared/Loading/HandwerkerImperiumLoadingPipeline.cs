using MeineApps.Core.Ava.Localization;
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
public class HandwerkerImperiumLoadingPipeline : LoadingPipelineBase
{
    public HandwerkerImperiumLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: GPU-Shader vorab kompilieren (12 SkSL-Shader)
        AddStep(new LoadingStep
        {
            Name = "Shader",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik-Engine laden...",
            Weight = 30,
            ExecuteAsync = () => Task.Run(() => ShaderPreloader.PreloadAll())
        });

        // Schritt 2: ViewModel erstellen (DI, noch ohne InitializeAsync)
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("SplashStep_GameState") ?? "Spielstand laden...",
            Weight = 10,
            ExecuteAsync = () =>
            {
                services.GetRequiredService<MainViewModel>();
                return Task.CompletedTask;
            }
        });

        // Schritt 3: Spielstand laden + initialisieren (SaveGame, Orders, Rewards etc.)
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
