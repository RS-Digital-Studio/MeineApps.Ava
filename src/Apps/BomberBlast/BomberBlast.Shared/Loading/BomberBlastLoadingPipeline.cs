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

        // Phase 24 (AAA-Audit O3-O5): Retention-Service Session-Touch — vor allem anderen.
        // Setzt FirstSessionUtc beim allerersten Start, aktualisiert LastSessionUtc bei jedem Start.
        // Pflicht: D1/D7-Window + Comeback-Detection brauchen den Touch früh.
        services.GetRequiredService<IRetentionService>().TouchSession();

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
                    ShaderPreloader.PreloadAll();   // 12 generische SkSL-Shader
                    ExplosionShaders.Preload();     // Explosion Noise-LUT + Paint-Cache
                    ShaderEffects.Preload();        // WaterRipple SkSL (Ocean-Welt)
                    BloomEffect.Preload();          // Phase 21b — SkSL Threshold + Box-Blur (Ultra-Tier-Gate)
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
    /// Menü-Hintergründe + Bosse (sofort sichtbar oder im Spiel benötigt) +
    /// 12 PowerUps + 12 Enemies + Welt-1-Hintergrund (erster Level-Eintritt).
    /// Welt 2-10 wird lazy beim LevelSelect/GameViewModel.SetParameters preloaded.
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

        // Bosse (werden im Spiel sofort gebraucht) — Single Source of Truth in GameAssetPaths.
        foreach (var path in GameAssetPaths.BossAssetPaths)
            yield return path;

        // PowerUps (universal, welt-übergreifend — 12 Icons, ~480KB total)
        foreach (var path in GameAssetPaths.GetAllPowerUpAssets())
            yield return path;

        // Gegner-Typen (universal, 12 Typen — vermeidet Jank beim ersten Spawn)
        foreach (var path in GameAssetPaths.GetAllEnemyAssets())
            yield return path;

        // Welt 1 Hintergrund (erste Story-Welt, fast garantierter erster Level-Eintritt)
        yield return GameAssetPaths.GetWorldAssetPath(0);
    }
}
