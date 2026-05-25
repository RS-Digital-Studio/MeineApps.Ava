#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Login;
using ArcaneKingdom.Game.Services;
using ArcaneKingdom.Game.Arena;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Battle;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.Game.Chat;
using ArcaneKingdom.Game.Codex;
using ArcaneKingdom.Game.Collection;
using ArcaneKingdom.Game.DeckBuilder;
using ArcaneKingdom.Game.Guild;
using ArcaneKingdom.Game.Hero;
using ArcaneKingdom.Game.Hub;
using ArcaneKingdom.Game.Iap;
using ArcaneKingdom.Game.Localization;
using ArcaneKingdom.Game.Notification;
using ArcaneKingdom.Game.Progression;
using ArcaneKingdom.Game.Quest;
using ArcaneKingdom.Game.Replay;
using ArcaneKingdom.Game.Season;
using ArcaneKingdom.Game.Shop;
using ArcaneKingdom.Game.Thief;
using ArcaneKingdom.Game.Tutorial;
using ArcaneKingdom.Game.Achievement;
using ArcaneKingdom.Game.Friends;
using ArcaneKingdom.Game.SaisonPass;
using ArcaneKingdom.Game.DailyShop;
using ArcaneKingdom.Game.Treasury;
using ArcaneKingdom.Game.World;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.World;
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
            builder.Register<ILocalizationService, CsvLocalizationService>(Lifetime.Singleton);
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

            // Card-Catalog: Runtime-Lookup von CardDefinitions per ID
            builder.Register<CardCatalogService>(Lifetime.Singleton);
            builder.Register<WorldCatalogService>(Lifetime.Singleton);
            builder.Register<BattleBootstrap>(Lifetime.Singleton);
            builder.Register<CardArtworkService>(Lifetime.Singleton);

            // Iter 6: Schema-v2 + Server-Anbindungs-Vorbereitung
            builder.Register<AchievementService>(Lifetime.Singleton);
            builder.Register<ChatModerationService>(Lifetime.Singleton);
            builder.Register<MaterialDropService>(Lifetime.Singleton);
            builder.Register<SaisonPassService>(Lifetime.Singleton);
            builder.Register<DailyShopService>(Lifetime.Singleton);
            builder.Register<FriendsService>(Lifetime.Singleton);
            builder.Register<GuildTreasuryService>(Lifetime.Singleton);

            // LoginController NICHT mehr als EntryPoint — wird vom LoginScreen
            // (UI-Layer) aufgerufen sobald der Screen sichtbar ist. So sieht der
            // User Status-Updates statt eines schwarzen Bildschirms.
            builder.Register<LoginController>(Lifetime.Singleton);

            // v6 (Designplan v4) — Domain-Services
            // FusionService benötigt Karten-Definitionen + Rezept-Liste — diese werden
            // vom CardCatalogService bereitgestellt, daher wird FusionService via Factory
            // erzeugt: builder.Register(c => new FusionService(catalogService.AllDefs, recipes), Lifetime.Singleton)
            // Aktuell registrieren wir den Service ohne Factory — der Application-Layer
            // muss die Argumente beim Aufruf bereitstellen. Alternative: einen Wrapper-Service
            // FusionApplicationService in der Game-Assembly anlegen, der das Wiring uebernimmt.
            builder.Register<PrestigeService>(Lifetime.Singleton);
            builder.Register<SternkartenService>(Lifetime.Singleton);
            // FusionService bleibt newable-by-app — die Game-Assembly hat eine FusionAppService-Faecade
            // die CardCatalog + Recipes injiziert (geplant fuer Phase 2 zusammen mit der Schmiede-UI).
        }
    }
}
