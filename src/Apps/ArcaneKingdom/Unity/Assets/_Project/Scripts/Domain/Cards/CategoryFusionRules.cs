#nullable enable
namespace ArcaneKingdom.Domain.Cards
{
    /// <summary>
    /// Kategorie-basiertes Crafting (Designplan v4 Kap. 5.1 Typ A).
    /// Spieler kombiniert mehrere Karten derselben Rasse und Seltenheit, erhaelt eine zufaellige
    /// hoeherwertige Karte derselben Rasse. Die Basis-Karten werden verbraucht.
    /// </summary>
    public static class CategoryFusionRules
    {
        /// <summary>
        /// Liefert Konfiguration fuer den Upgrade-Pfad einer Seltenheit zur naechsten.
        /// </summary>
        public static CategoryFusionConfig? GetConfig(Rarity fromRarity)
        {
            return fromRarity switch
            {
                Rarity.Gewoehnlich   => new CategoryFusionConfig(
                    requiredSameRaceCards: 3,
                    goldCost: 1_000,
                    requiresScrapId: null,
                    resultRarity: Rarity.Ungewoehnlich),

                Rarity.Ungewoehnlich => new CategoryFusionConfig(
                    requiredSameRaceCards: 3,
                    goldCost: 5_000,
                    requiresScrapId: null,
                    resultRarity: Rarity.Selten),

                Rarity.Selten        => new CategoryFusionConfig(
                    requiredSameRaceCards: 4,
                    goldCost: 25_000,
                    requiresScrapId: "rare_scrap",
                    resultRarity: Rarity.Epic),

                Rarity.Epic          => new CategoryFusionConfig(
                    requiredSameRaceCards: 4,
                    goldCost: 100_000,
                    requiresScrapId: "epic_scrap",
                    resultRarity: Rarity.Legendaer),

                // 5★ → 6★: 3 paarweise VERSCHIEDENE 5★ (Rasse egal) + Mythischer Kern + 5 Mio Gold
                // → zufaellige Nicht-Goetter-6★ (Designplan v4 Kap. 5.1 Tabelle Z6 + Kap. 5.3;
                // Goetter nur per festem Rezept, Kap. 5.2). Material-ID muss "mythischer_kern" sein
                // (FusionAppService.MythicCoreMaterialId), sonst wird der Kern nie geprueft/abgezogen.
                Rarity.Legendaer     => new CategoryFusionConfig(
                    requiredSameRaceCards: 3,
                    goldCost: 5_000_000,
                    requiresScrapId: "mythischer_kern",
                    resultRarity: Rarity.Mythisch,
                    allowsDifferentCards: true),

                _ => null
            };
        }

        public sealed class CategoryFusionConfig
        {
            public int RequiredSameRaceCards { get; }
            public long GoldCost { get; }
            public string? RequiresScrapId { get; }
            public Rarity ResultRarity { get; }
            public bool AllowsDifferentCards { get; }

            public CategoryFusionConfig(
                int requiredSameRaceCards,
                long goldCost,
                string? requiresScrapId,
                Rarity resultRarity,
                bool allowsDifferentCards = false)
            {
                RequiredSameRaceCards = requiredSameRaceCards;
                GoldCost = goldCost;
                RequiresScrapId = requiresScrapId;
                ResultRarity = resultRarity;
                AllowsDifferentCards = allowsDifferentCards;
            }
        }
    }
}
