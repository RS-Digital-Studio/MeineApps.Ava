#nullable enable
using ArcaneKingdom.Domain.Config;
using ArcaneKingdom.Game.Bootstrap;
using ArcaneKingdom.Game.Services;
using ArcaneKingdom.UI.Bootstrap;
using ArcaneKingdom.UI.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ArcaneKingdom.Bootstrap
{
    /// <summary>
    /// Root-LifetimeScope (VContainer). Registriert alle Singleton-Services.
    /// Liegt auf der Boot-Scene als persistentes GameObject (DontDestroyOnLoad).
    /// </summary>
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField] private BalancingConfig? balancingConfig;
        [SerializeField] private UnityAudioService? audioService;
        [SerializeField] private UIRoot? uiRoot;

        protected override void Configure(IContainerBuilder builder)
        {
            if (balancingConfig != null)
                builder.RegisterInstance(balancingConfig);

            if (audioService != null)
                builder.RegisterComponent(audioService).AsImplementedInterfaces();

            // UIRoot-MonoBehaviour als Singleton-Component registrieren (haelt
            // das UIDocument + Screen/Overlay-Container). Pflicht fuer ScreenManager.
            if (uiRoot != null)
                builder.RegisterComponent(uiRoot);

            // Game-Assembly-Registrierungen (Services, Controller, EntryPoints)
            GameInstaller.RegisterServices(builder);

            // UI-Foundation (ScreenManager, Factory, Toasts) + alle konkreten Screens
            UIBootstrap.RegisterAllScreens(builder);

            builder.RegisterEntryPoint<BootEntryPoint>();
        }
    }
}
