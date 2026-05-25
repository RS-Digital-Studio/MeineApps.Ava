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
    }
}
