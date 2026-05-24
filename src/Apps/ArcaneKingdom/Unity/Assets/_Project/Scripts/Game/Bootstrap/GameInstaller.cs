#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Login;
using ArcaneKingdom.Game.Services;
using ArcaneKingdom.Game.Arena;
using ArcaneKingdom.Game.Battle;
using ArcaneKingdom.Game.Chat;
using ArcaneKingdom.Game.Codex;
using ArcaneKingdom.Game.Collection;
using ArcaneKingdom.Game.DeckBuilder;
using ArcaneKingdom.Game.Guild;
using ArcaneKingdom.Game.Hero;
using ArcaneKingdom.Game.Hub;
using ArcaneKingdom.Game.Iap;
using ArcaneKingdom.Game.Notification;
using ArcaneKingdom.Game.Progression;
using ArcaneKingdom.Game.Quest;
using ArcaneKingdom.Game.Replay;
using ArcaneKingdom.Game.Season;
using ArcaneKingdom.Game.Shop;
using ArcaneKingdom.Game.Thief;
using ArcaneKingdom.Game.Tutorial;
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
            builder.Register<GuildController>(Lifetime.Singleton);
            builder.Register<ThiefController>(Lifetime.Singleton);
            builder.Register<ChatController>(Lifetime.Singleton);
            builder.Register<ShopController>(Lifetime.Singleton);
            builder.Register<QuestService>(Lifetime.Singleton);
            builder.Register<DailyRewardService>(Lifetime.Singleton);
            builder.Register<ProgressionService>(Lifetime.Singleton);
            builder.Register<HeroService>(Lifetime.Singleton);
            builder.Register<ReplayService>(Lifetime.Singleton);
            builder.Register<IIapService, UnityIapService>(Lifetime.Singleton);
            builder.Register<DeckBuilderService>(Lifetime.Singleton);
            builder.Register<CollectionService>(Lifetime.Singleton);
            builder.Register<TutorialService>(Lifetime.Singleton);
            builder.Register<INotificationService, NotificationService>(Lifetime.Singleton);
            builder.Register<SeasonResetService>(Lifetime.Singleton);
            builder.Register<CodexService>(Lifetime.Singleton);

            builder.RegisterEntryPoint<LoginController>();
        }
    }
}
