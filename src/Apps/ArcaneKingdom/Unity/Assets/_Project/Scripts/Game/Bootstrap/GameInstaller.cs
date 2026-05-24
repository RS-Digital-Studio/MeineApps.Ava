#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Login;
using ArcaneKingdom.Game.Services;
using ArcaneKingdom.Game.Arena;
using ArcaneKingdom.Game.Battle;
using ArcaneKingdom.Game.Hub;
using VContainer;
using VContainer.Unity;

namespace ArcaneKingdom.Game.Bootstrap
{
    /// <summary>
    /// Zentrale Registrierungs-Helper. Wird vom <c>RootLifetimeScope</c> aufgerufen,
    /// damit die Game-Assembly nicht auf VContainer-Internals des Bootstrap-Layers angewiesen ist.
    /// </summary>
    public static class GameInstaller
    {
        public static void RegisterServices(IContainerBuilder builder)
        {
            builder.Register<ISceneLoaderService, AdditiveSceneLoaderService>(Lifetime.Singleton);
            builder.Register<IAuthService, FirebaseAuthService>(Lifetime.Singleton);
            builder.Register<ISaveService<PlayerSave>, FirebaseSaveService>(Lifetime.Singleton);
            builder.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton);
            // UnityAudioService ist MonoBehaviour — wird in der Boot-Scene als GameObject platziert
            // und kann via RegisterComponentInHierarchy<UnityAudioService>() eingebunden werden.

            builder.Register<HubController>(Lifetime.Singleton);
            builder.Register<BattleController>(Lifetime.Singleton);
            builder.Register<ArenaController>(Lifetime.Singleton);

            builder.RegisterEntryPoint<LoginController>();
        }
    }
}
