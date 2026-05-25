#nullable enable

namespace ArcaneKingdom.UI.Foundation
{
    /// <summary>
    /// Zentrale Liste aller Screen-IDs. Wird vom <see cref="ScreenManager"/> als Key
    /// für Push/Pop/Replace-Operationen verwendet.
    ///
    /// Jeder Screen registriert sich in <see cref="UIInstaller"/> unter einer dieser IDs.
    /// </summary>
    public static class ScreenId
    {
        // --- Boot-Phase ---
        public const string Splash = "splash";
        public const string Login = "login";

        // --- Hub ---
        public const string Hub = "hub";

        // --- Hub-Sub-Screens ---
        public const string DeckBuilder = "deck-builder";
        public const string WorldMap = "world-map";
        public const string Codex = "codex";
        public const string Settings = "settings";

        // --- Multiplayer/Soziales ---
        public const string Arena = "arena";
        public const string Guild = "guild";
        public const string Friends = "friends";

        // --- Shop / Saison ---
        public const string Shop = "shop";
        public const string SaisonPass = "saison-pass";

        // --- Battle ---
        public const string Battle = "battle";

        // --- Overlays (immer über dem aktuellen Screen) ---
        public const string CardDetailOverlay = "overlay-card-detail";
        public const string PackOpeningOverlay = "overlay-pack-opening";
        public const string TutorialOverlay = "overlay-tutorial";

        // --- v6 (Designplan v4) — Schmiede / Tempel / Prestige / Story / Onboarding ---
        public const string Schmiede = "schmiede";
        public const string Tempel = "tempel";
        public const string RaceSelection = "race-selection";
        public const string PrestigeUpgradeOverlay = "overlay-prestige-upgrade";
        public const string MemoryFragmentOverlay = "overlay-memory-fragment";

        // --- Spielplan v5 — Auth / Boot ---
        public const string Registration = "registration";

        // --- Spielplan v5 — Sub-Screens ---
        public const string Runes = "runes";
        public const string PlayerProfile = "player-profile";
        public const string QuestCenter = "quest-center";
        public const string MeritRanking = "merit-ranking";
        public const string LeistungsRanking = "leistungs-ranking";
        public const string BattleReport = "battle-report";
        public const string ThiefScreen = "thief";
        public const string GuildWorldMap = "guild-world-map";
        public const string ClanMatch = "clan-match";
        public const string PvpMatchmaking = "pvp-matchmaking";

        // --- Spielplan v5 — Overlays ---
        public const string ChatOverlay = "overlay-chat";
        public const string DifficultyPickerOverlay = "overlay-difficulty-picker";
    }
}
