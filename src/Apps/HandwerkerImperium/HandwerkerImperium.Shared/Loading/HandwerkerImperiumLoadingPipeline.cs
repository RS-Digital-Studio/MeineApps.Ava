using Avalonia.Threading;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
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
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Loading graphics",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                // Der MainViewModel-Objektgraph MUSS auf dem UI-Thread konstruiert werden: VM-Ctors
                // erzeugen UI-thread-affine Objekte (SolidColorBrush etc.), DispatcherTimer und
                // Event-Abos. Frueher per Task.Run auf einem ThreadPool-Thread — latente Crash-Falle
                // und nichtdeterministisch (MainActivity loest denselben Singleton parallel auf).
                var vmTask = Dispatcher.UIThread.InvokeAsync(
                    () => services.GetRequiredService<MainViewModel>()).GetTask();
                // Alle 224 Bitmap-Icons vorladen (WebP → SKBitmap → Avalonia IImage)
                var iconsTask = Icons.GameIcon.PreloadAllAsync();
                // 20 Worker-Portraits vorladen (10 Tiers x 2 Geschlechter → AI statt Pixel-Art)
                var assetService = services.GetRequiredService<IGameAssetService>();
                var portraitsTask = assetService.PreloadAsync(WorkerAvatarRenderer.GetAllPortraitPaths());
                await Task.WhenAll(shaderTask, vmTask, iconsTask, portraitsTask);
            }
        });

        // Schritt 2: Spielstand laden + initialisieren (SaveGame, Orders, Rewards etc.)
        AddStep(new LoadingStep
        {
            Name = "GameInit",
            DisplayName = loc.GetString("SplashStep_Workshops") ?? "Loading workshops...",
            Weight = 35,
            ExecuteAsync = async () =>
            {
                var mainVm = services.GetRequiredService<MainViewModel>();
                await mainVm.InitializeAsync();
                // Käufe NACH InitializeAsync abgleichen (SanitizeState setzt Premium=false,
                // danach stellt RestorePurchases den echten Status via Google Play wieder her)
                await services.GetRequiredService<IPurchaseService>().InitializeAsync();
            }
        });

        // Schritt 3: Remote-Config laden (nicht-blockierend). Default-Werte bleiben nutzbar wenn Download fehlschlaegt.
        AddStep(new LoadingStep
        {
            Name = "RemoteConfig",
            DisplayName = loc.GetString("SplashStep_Config") ?? "Loading configuration...",
            Weight = 5,
            ExecuteAsync = async () =>
            {
                var remoteConfig = services.GetService<IRemoteConfigService>();
                bool remoteConfigReady = false;
                if (remoteConfig != null)
                {
                    // Max. 5 Sekunden warten — wenn Firebase offline ist, darf der App-Start nicht blockieren.
                    var fetchTask = remoteConfig.InitializeAsync();
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                    var winner = await Task.WhenAny(fetchTask, timeoutTask);
                    remoteConfigReady = winner == fetchTask;

                    // Code-Review-Fix [KRITISCH]: Falls RemoteConfig im Timeout
                    // hängt, fetchTask im Hintergrund weiterlaufen lassen und DailyBundle nach
                    // erfolgreichem Fetch nachträglich initialisieren. Verhindert dass der
                    // Spieler das Bundle erst beim nächsten App-Start sieht.
                    if (!remoteConfigReady)
                    {
                        _ = fetchTask.ContinueWith(async _ =>
                        {
                            var dailyBundleLate = services.GetService<IDailyBundleService>();
                            if (dailyBundleLate != null)
                            {
                                try { await dailyBundleLate.InitializeAsync(); }
                                catch { /* Bundle-Late-Init-Fehler ignorieren */ }
                            }
                        }, TaskScheduler.Default);
                    }
                }

                // DailyBundle nur initialisieren wenn RemoteConfig synchron bereit war.
                // Sonst übernimmt der ContinueWith-Hook oben das (deferred Init).
                if (remoteConfigReady)
                {
                    var dailyBundle = services.GetService<IDailyBundleService>();
                    if (dailyBundle != null)
                    {
                        try { await dailyBundle.InitializeAsync(); }
                        catch { /* Bundle-Init-Fehler darf App-Start nicht blockieren */ }
                    }
                }
            }
        });
    }
}
