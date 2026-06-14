using MeineApps.Core.Ava.Localization;
using MeineApps.UI.Loading;
using Microsoft.Extensions.DependencyInjection;
using RechnerPlus.ViewModels;

namespace RechnerPlus.Loading;

/// <summary>
/// RechnerPlus Lade-Pipeline: ViewModel-Erstellung (leichtgewichtig, keine DB).
/// Kein Shader-Preload — RechnerPlus rendert KEINEN der 12 SkSL-Effekte. Alle Grafiken
/// (VFD-Display, Result-Burst, Funktionsgraph, animierter Hintergrund) nutzen klassische
/// SkiaSharp-Gradienten + MaskFilter, kein SkSL und kein MeineApps.UI-Control, das einen
/// Shader zöge. PreloadAll() hätte 12 ungenutzte Shader (bis 2,4s auf Android) kompiliert.
/// </summary>
public sealed class RechnerPlusLoadingPipeline : LoadingPipelineBase
{
    public RechnerPlusLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: ViewModel-Graph auflösen
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 40,
            ExecuteAsync = () => Task.Run(() => services.GetRequiredService<MainViewModel>())
        });
    }
}
