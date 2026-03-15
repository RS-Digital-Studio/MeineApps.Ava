using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services;
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

        // Schritt 1: Shader + ViewModel + Icons parallel (unabhängig voneinander)
        AddStep(new LoadingStep
        {
            Name = "Shader+ViewModel+Icons",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik-Engine laden...",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                var vmTask = Task.Run(() => services.GetRequiredService<MainViewModel>());
                // Käufe mit Google Play abgleichen (Geräte-/Datenwechsel → Premium-Status wiederherstellen)
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                // Alle 224 Bitmap-Icons vorladen (WebP → SKBitmap → Avalonia IImage)
                var iconsTask = Icons.GameIcon.PreloadAllAsync();
                // 20 Worker-Portraits vorladen (10 Tiers x 2 Geschlechter → AI statt Pixel-Art)
                var assetService = services.GetRequiredService<IGameAssetService>();
                var portraitsTask = assetService.PreloadAsync(WorkerAvatarRenderer.GetAllPortraitPaths());
                await Task.WhenAll(shaderTask, vmTask, purchaseTask, iconsTask, portraitsTask);
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
