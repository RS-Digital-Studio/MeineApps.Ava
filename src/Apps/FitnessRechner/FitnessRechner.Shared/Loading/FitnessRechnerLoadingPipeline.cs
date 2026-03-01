using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using FitnessRechner.ViewModels;

namespace FitnessRechner.Loading;

/// <summary>
/// FitnessRechner Lade-Pipeline: Shader + Tracking-Services + ViewModel.
/// </summary>
public class FitnessRechnerLoadingPipeline : LoadingPipelineBase
{
    public FitnessRechnerLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader-Kompilierung (BMI-Gauge, BodyFat-Ring, Kalorie-Ring)
        AddStep(new LoadingStep
        {
            Name = "Shader",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 30,
            ExecuteAsync = () => Task.Run(() => ShaderPreloader.PreloadAll())
        });

        // Schritt 2: ViewModel erstellen (TrackingService, AchievementService, LevelService etc.)
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
