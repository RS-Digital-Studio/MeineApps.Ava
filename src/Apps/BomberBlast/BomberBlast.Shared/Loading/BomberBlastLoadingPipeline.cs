using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using BomberBlast.Graphics;
using BomberBlast.Services;
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

        // Schritt 1: Shader + ViewModel + Assets + Käufe parallel laden
        AddStep(new LoadingStep
        {
            Name = "Shader+ViewModel+Assets",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik-Engine laden...",
            Weight = 60,
            ExecuteAsync = async () =>
            {
                var shaderTask = Task.Run(() =>
                {
                    ShaderPreloader.PreloadAll();
                    ExplosionShaders.Preload();
                });
                var vmTask = Task.Run(() => services.GetRequiredService<MainViewModel>());
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();

                // AI-Assets vorladen (Splash, Menü-Hintergründe, Bosse)
                var assetService = services.GetRequiredService<IGameAssetService>();
                var assetTask = assetService.PreloadAsync(GetCriticalAssets());

                await Task.WhenAll(shaderTask, vmTask, purchaseTask, assetTask);
            }
        });
    }

    /// <summary>
    /// Kritische Assets die beim Start geladen werden sollen.
    /// Menü-Hintergründe + Bosse (sofort sichtbar oder im Spiel benötigt).
    /// Weitere Assets werden lazy per GetBitmap()/LoadBitmapAsync() nachgeladen.
    /// </summary>
    private static IEnumerable<string> GetCriticalAssets()
    {
        // Splash + Menü-Hintergründe
        yield return "splash/splash.webp";
        yield return "menu_bg/menu_default.webp";
        yield return "menu_bg/menu_dungeon.webp";
        yield return "menu_bg/menu_shop.webp";
        yield return "menu_bg/menu_league.webp";
        yield return "menu_bg/menu_battlepass.webp";
        yield return "menu_bg/menu_victory.webp";
        yield return "menu_bg/menu_lucky_spin.webp";

        // Bosse (werden im Spiel sofort gebraucht)
        yield return "bosses/boss_stone_golem.webp";
        yield return "bosses/boss_ice_dragon.webp";
        yield return "bosses/boss_fire_demon.webp";
        yield return "bosses/boss_shadow_master.webp";
        yield return "bosses/boss_final.webp";
    }
}
