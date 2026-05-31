#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.UI.Arena;
using ArcaneKingdom.UI.Battle;
using ArcaneKingdom.UI.BattleReport;
using ArcaneKingdom.UI.Chat;
using ArcaneKingdom.UI.Codex;
using ArcaneKingdom.UI.CollectionTrade;
using ArcaneKingdom.UI.DeckBuilder;
using ArcaneKingdom.UI.Foundation;
using ArcaneKingdom.UI.Friends;
using ArcaneKingdom.UI.Guild;
using ArcaneKingdom.UI.GuildWorld;
using ArcaneKingdom.UI.Hub;
using ArcaneKingdom.UI.Login;
using ArcaneKingdom.UI.Merit;
using ArcaneKingdom.UI.Modals;
using ArcaneKingdom.UI.PlayerProfile;
using ArcaneKingdom.UI.Pvp;
using ArcaneKingdom.UI.Quest;
using ArcaneKingdom.UI.RaceSelection;
using ArcaneKingdom.UI.Registration;
using ArcaneKingdom.UI.Runes;
using ArcaneKingdom.UI.SaisonPass;
using ArcaneKingdom.UI.Schmiede;
using ArcaneKingdom.UI.Settings;
using ArcaneKingdom.UI.Shop;
using ArcaneKingdom.UI.Splash;
using ArcaneKingdom.UI.Tempel;
using ArcaneKingdom.UI.Thief;
using ArcaneKingdom.UI.Tutorial;
using ArcaneKingdom.UI.WorldMap;
using VContainer;

namespace ArcaneKingdom.UI.Bootstrap
{
    /// <summary>
    /// Zentrale Registrierung aller Screens. Wird vom <c>RootLifetimeScope</c>
    /// aufgerufen.
    ///
    /// Wenn ein neuer Screen hinzukommt:
    ///   1. Konkrete Screen-Klasse erstellen (erbt von <see cref="ScreenBase"/>)
    ///   2. Hier in der Map registrieren: [ScreenId.NewName] = typeof(NewScreen)
    ///   3. UXML unter Assets/_Project/Resources/UI/NewScreen.uxml ablegen
    /// </summary>
    public static class UIBootstrap
    {
        public static void RegisterAllScreens(IContainerBuilder builder)
        {
            var screens = new Dictionary<string, Type>
            {
                [ScreenId.Splash]             = typeof(SplashScreen),
                [ScreenId.Registration]       = typeof(RegistrationScreen),
                [ScreenId.Login]              = typeof(LoginScreen),
                [ScreenId.Hub]                = typeof(HubScreen),
                [ScreenId.CardDetailOverlay]  = typeof(CardDetailModal),
                [ScreenId.PackOpeningOverlay] = typeof(PackOpeningModal),
                [ScreenId.DeckBuilder]        = typeof(DeckBuilderScreen),
                [ScreenId.WorldMap]           = typeof(WorldMapScreen),
                [ScreenId.Battle]             = typeof(BattleScreen),
                [ScreenId.Arena]              = typeof(ArenaScreen),
                [ScreenId.Guild]              = typeof(GuildScreen),
                [ScreenId.SaisonPass]         = typeof(SaisonPassScreen),
                [ScreenId.Friends]            = typeof(FriendsScreen),
                [ScreenId.Settings]           = typeof(SettingsScreen),
                [ScreenId.Codex]              = typeof(CodexScreen),
                [ScreenId.TutorialOverlay]    = typeof(TutorialOverlay),

                // v6 (Designplan v4) — neue Screens
                [ScreenId.Schmiede]                 = typeof(SchmiedeScreen),
                [ScreenId.Tempel]                   = typeof(TempelScreen),
                [ScreenId.RaceSelection]            = typeof(RaceSelectionScreen),
                [ScreenId.PrestigeUpgradeOverlay]   = typeof(PrestigeUpgradeModal),
                [ScreenId.MemoryFragmentOverlay]    = typeof(MemoryFragmentModal),
                [ScreenId.EndingChoiceOverlay]      = typeof(EndingChoiceModal),
                [ScreenId.DifficultyPickerOverlay]  = typeof(DifficultyPickerModal),

                // Spielplan v5 — neue Screens
                [ScreenId.Runes]                    = typeof(RuneScreen),
                [ScreenId.PlayerProfile]            = typeof(PlayerProfileScreen),
                [ScreenId.Shop]                     = typeof(ShopScreen),
                [ScreenId.QuestCenter]              = typeof(QuestCenterScreen),
                [ScreenId.MeritRanking]             = typeof(MeritRankingScreen),
                [ScreenId.BattleReport]             = typeof(BattleReportScreen),
                [ScreenId.ThiefScreen]              = typeof(ThiefScreen),
                [ScreenId.GuildWorldMap]            = typeof(GuildWorldMapScreen),
                [ScreenId.PvpMatchmaking]           = typeof(PvpMatchmakingScreen),
                [ScreenId.ChatOverlay]              = typeof(ChatOverlay),
                ["collection-trade"]                = typeof(CollectionTradeScreen),
            };

            UIInstaller.RegisterUI(builder, screens);
        }
    }
}
