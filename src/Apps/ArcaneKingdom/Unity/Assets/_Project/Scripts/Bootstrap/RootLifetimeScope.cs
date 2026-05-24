#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Config;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ArcaneKingdom.Bootstrap
{
    /// <summary>
    /// Root-LifetimeScope (VContainer). Registriert alle Singleton-Services.
    /// Liegt auf der Boot-Scene als persistentes GameObject (DontDestroyOnLoad).
    ///
    /// HINWEIS: Service-Implementierungen sind in <c>ArcaneKingdom.Game</c>
    /// und werden erst ergaenzt, wenn das Unity-Projekt zum ersten Mal geoeffnet wird.
    /// </summary>
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField] private BalancingConfig? balancingConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            // Konfiguration als Singleton
            if (balancingConfig != null)
                builder.RegisterInstance(balancingConfig);

            // Service-Stubs (Implementierungen folgen in Game-Assembly):
            // builder.Register<IAuthService, FirebaseAuthService>(Lifetime.Singleton);
            // builder.Register<ISaveService<PlayerSave>, FirebaseSaveService>(Lifetime.Singleton);
            // builder.Register<INetworkService, PhotonNetworkService>(Lifetime.Singleton);
            // builder.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton);
            // builder.Register<IAudioService, UnityAudioService>(Lifetime.Singleton);
            // builder.Register<ISceneLoaderService, AdditiveSceneLoaderService>(Lifetime.Singleton);

            // Entry Point — wird beim Container-Build aufgerufen
            builder.RegisterEntryPoint<BootEntryPoint>();
        }
    }
}
