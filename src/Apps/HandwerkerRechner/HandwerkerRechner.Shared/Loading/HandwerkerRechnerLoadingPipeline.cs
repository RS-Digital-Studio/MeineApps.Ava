using Avalonia.Threading;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
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

        // Schritt 1: Käufe + ViewModel parallel (unabhängig voneinander)
        AddStep(new LoadingStep
        {
            Name = "Purchase+ViewModel",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 45,
            ExecuteAsync = async () =>
            {
                // Kein Shader-Preload: HandwerkerRechner rendert KEINEN der 12 SkSL-Effekte —
                // weder direkt noch über ein MeineApps.UI-Control. Alle Blueprint-Visualisierungen
                // (SkiaBlueprintCanvas etc.) nutzen klassische SkiaSharp-Gradienten/Pfade ohne SkSL.
                // PreloadAll() hätte hier 12 nie genutzte Shader (bis 2,4s auf Android) kompiliert.
                // Käufe mit Google Play abgleichen (Geräte-/Datenwechsel → Premium-Status wiederherstellen)
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                // VM-Graph auf dem UI-Thread instanziieren — ViewModels haben UI-Thread-Affinität
                // (Brushes etc.), NIE auf einem Task.Run-Thread erzeugen (Workspace-Regel).
                await Dispatcher.UIThread.InvokeAsync(() => services.GetRequiredService<MainViewModel>());
                await purchaseTask;
            }
        });
    }
}
