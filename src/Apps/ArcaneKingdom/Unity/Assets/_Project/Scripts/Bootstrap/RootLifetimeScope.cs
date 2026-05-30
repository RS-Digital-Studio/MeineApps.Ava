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
            // Pflicht-Abhaengigkeiten NICHT bedingt registrieren: ein nicht verdrahteter Scene-Slot
            // soll hier laut und lokalisierbar fehlschlagen, statt spaeter als diffuser Resolve-/NullRef-
            // Crash an entfernter Stelle (ScreenManager, HubController, SettingsScreen).
            if (balancingConfig == null)
                throw new System.InvalidOperationException(
                    "RootLifetimeScope: BalancingConfig-Slot nicht verdrahtet — Asset auf das [Bootstrapper]-GameObject ziehen (siehe Scenes/README.md).");
            if (audioService == null)
                throw new System.InvalidOperationException(
                    "RootLifetimeScope: AudioService-Slot nicht verdrahtet — UnityAudioService auf das [Audio]-GameObject ziehen (siehe Scenes/README.md).");
            if (uiRoot == null)
                throw new System.InvalidOperationException(
                    "RootLifetimeScope: UIRoot-Slot nicht verdrahtet — UIRoot-Component referenzieren (Pflicht fuer ScreenManager, siehe Scenes/README.md).");

            builder.RegisterInstance(balancingConfig);
            builder.RegisterComponent(audioService).AsImplementedInterfaces();
            // UIRoot-MonoBehaviour als Singleton-Component (haelt UIDocument + Screen/Overlay-Container).
            builder.RegisterComponent(uiRoot);

            // Game-Assembly-Registrierungen (Services, Controller, EntryPoints)
            GameInstaller.RegisterServices(builder);

            // UI-Foundation (ScreenManager, Factory, Toasts) + alle konkreten Screens
            UIBootstrap.RegisterAllScreens(builder);

            builder.RegisterEntryPoint<BootEntryPoint>();
        }
    }
}
