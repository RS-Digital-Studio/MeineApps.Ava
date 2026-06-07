using System;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Prestige-Stufen mit steigenden Anforderungen und Belohnungen.
    /// Jede Stufe erfordert mehrere Abschlüsse der vorherigen Stufe.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/PrestigeTier.cs). Die reinen
    /// Gameplay-Werte (Level-/Tier-Anforderungen, Multiplikatoren, Bewahrungs-Gates, Startgeld)
    /// leben hier; die UI-Methoden (Farb-Key, Icon, Lokalisierungs-Key) wandern in die
    /// Unity-Präsentationsschicht.
    /// </summary>
    public enum PrestigeTier
    {
        /// <summary>Kein Prestige</summary>
        None = 0,

        /// <summary>Erste Prestige-Stufe, erfordert Level 30</summary>
        Bronze = 1,

        /// <summary>Zweite Stufe, erfordert Level 100 + 1x Bronze</summary>
        Silver = 2,

        /// <summary>Dritte Stufe, erfordert Level 250 + 1x Silver</summary>
        Gold = 3,

        /// <summary>Vierte Stufe, erfordert Level 500 + 2x Gold</summary>
        Platin = 4,

        /// <summary>Fünfte Stufe, erfordert Level 750 + 2x Platin</summary>
        Diamant = 5,

        /// <summary>Sechste Stufe, erfordert Level 1000 + 2x Diamant</summary>
        Meister = 6,

        /// <summary>Höchste Stufe, erfordert Level 1200 + 3x Meister</summary>
        Legende = 7
    }

    public static class PrestigeTierExtensions
    {
        /// <summary>Minimales Spieler-Level für Prestige auf dieser Stufe.</summary>
        public static int GetRequiredLevel(this PrestigeTier tier) => tier switch
        {
            PrestigeTier.Bronze => 30,
            PrestigeTier.Silver => 100,
            PrestigeTier.Gold => 250,
            PrestigeTier.Platin => 500,
            PrestigeTier.Diamant => 750,
            PrestigeTier.Meister => 1000,
            PrestigeTier.Legende => 1200,
            _ => int.MaxValue
        };

        /// <summary>Anzahl benötigter Abschlüsse der vorherigen Stufe.</summary>
        public static int GetRequiredPreviousTierCount(this PrestigeTier tier) => tier switch
        {
            PrestigeTier.Bronze => 0,
            PrestigeTier.Silver => 1,   // 1x Bronze
            PrestigeTier.Gold => 1,     // 1x Silver
            PrestigeTier.Platin => 2,   // 2x Gold
            PrestigeTier.Diamant => 2,  // 2x Platin
            PrestigeTier.Meister => 2,  // 2x Diamant
            PrestigeTier.Legende => 3,  // 3x Meister
            _ => 0
        };

        /// <summary>Basis-Prestige-Punkte-Multiplikator für diese Stufe.</summary>
        public static decimal GetPointMultiplier(this PrestigeTier tier) => tier switch
        {
            PrestigeTier.Bronze => 1.0m,
            PrestigeTier.Silver => 2.0m,
            PrestigeTier.Gold => 4.0m,
            PrestigeTier.Platin => 8.0m,
            PrestigeTier.Diamant => 16.0m,
            PrestigeTier.Meister => 32.0m,
            PrestigeTier.Legende => 64.0m,
            _ => 0m
        };

        /// <summary>Permanenter Einkommens-Multiplikator-Bonus pro Prestige auf dieser Stufe.</summary>
        public static decimal GetPermanentMultiplierBonus(this PrestigeTier tier) => tier switch
        {
            PrestigeTier.Bronze => 0.20m,   // +20% pro Bronze
            PrestigeTier.Silver => 0.35m,   // +35% pro Silver
            PrestigeTier.Gold => 0.50m,     // +50% pro Gold
            PrestigeTier.Platin => 1.00m,   // +100% pro Platin
            PrestigeTier.Diamant => 2.00m,  // +200% pro Diamant
            PrestigeTier.Meister => 4.00m,  // +400% pro Meister
            PrestigeTier.Legende => 8.00m,  // +800% pro Legende
            _ => 0m
        };

        /// <summary>
        /// Was bei Prestige erhalten bleibt (steigt mit der Stufe):
        /// Gold+: Research · Platin+: Shop-Items · Diamant+: MasterTools ·
        /// Meister+: Gebäude (Level→1) + Equipment · Legende: Manager (Level→1) + beste Worker.
        /// </summary>
        public static bool KeepsResearch(this PrestigeTier tier) => tier >= PrestigeTier.Gold;
        public static bool KeepsShopItems(this PrestigeTier tier) => tier >= PrestigeTier.Platin;
        public static bool KeepsMasterTools(this PrestigeTier tier) => tier >= PrestigeTier.Diamant;
        public static bool KeepsBuildings(this PrestigeTier tier) => tier >= PrestigeTier.Meister;
        public static bool KeepsEquipment(this PrestigeTier tier) => tier >= PrestigeTier.Meister;
        public static bool KeepsManagers(this PrestigeTier tier) => tier >= PrestigeTier.Legende;
        public static bool KeepsBestWorkers(this PrestigeTier tier) => tier >= PrestigeTier.Legende;

        /// <summary>
        /// Basis-Startgeld nach Prestige, skaliert mit Stufe. Kommt zusätzlich zu
        /// Prestige-Shop-Boni (pp_start_money).
        /// </summary>
        public static decimal GetTierStartMoney(this PrestigeTier tier) => tier switch
        {
            PrestigeTier.Bronze => 10_000m,
            PrestigeTier.Silver => 100_000m,
            PrestigeTier.Gold => 1_000_000m,
            PrestigeTier.Platin => 25_000_000m,
            PrestigeTier.Diamant => 250_000_000m,
            PrestigeTier.Meister => 2_500_000_000m,
            PrestigeTier.Legende => 25_000_000_000m,
            _ => 100m
        };

        /// <summary>Gibt die nächsthöhere Prestige-Stufe zurück (None wenn bereits Legende).</summary>
        public static PrestigeTier GetNextTier(this PrestigeTier tier) => tier switch
        {
            PrestigeTier.None => PrestigeTier.Bronze,
            PrestigeTier.Bronze => PrestigeTier.Silver,
            PrestigeTier.Silver => PrestigeTier.Gold,
            PrestigeTier.Gold => PrestigeTier.Platin,
            PrestigeTier.Platin => PrestigeTier.Diamant,
            PrestigeTier.Diamant => PrestigeTier.Meister,
            PrestigeTier.Meister => PrestigeTier.Legende,
            _ => PrestigeTier.None
        };
    }
}
