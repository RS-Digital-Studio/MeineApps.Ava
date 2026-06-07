using System;
using HandwerkerImperium.Domain;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Reine Prestige-Formeln, extrahiert aus dem Avalonia-Original (Services/PrestigeService.cs):
    /// Diminishing-Returns auf den permanenten Multiplikator und steigende Kosten für
    /// wiederholbare Shop-Items. Die State-Orchestrierung (ApplyPrestige/ResetProgress) lebt in
    /// der Unity-Game-Schicht; die deterministische Mathematik gehört in die Domain.
    /// </summary>
    public static class PrestigeFormulas
    {
        /// <summary>
        /// Maximaler permanenter Prestige-Multiplikator (nur Tier-Boni, nicht Shop-Income-Boni).
        /// Realistisch erreichbar nach ~48 Prestiges. Diminishing Returns: 10. Prestige
        /// desselben Tiers bringt nur noch 50% Bonus.
        /// </summary>
        public const decimal MaxPermanentMultiplier = 20.0m;

        /// <summary>
        /// Berechnet die aktuellen Kosten für ein wiederholbares Item.
        /// Formel: Basiskosten * 2^(Kaufanzahl) → z.B. 15/30/60/120... (Cap bei 2^15 gegen Overflow).
        /// </summary>
        public static int GetRepeatableItemCost(PrestigeShopItem item, int purchaseCount)
        {
            return item.Cost * (1 << Math.Min(purchaseCount, 15));
        }

        /// <summary>
        /// Diminishing-Returns-Bonus für einen weiteren Prestige desselben Tiers.
        /// Formel: baseBonus * 1/(1 + DiminishingReturnsPerTierPrestige * tierCount), wobei
        /// tierCount die Anzahl der VORHERIGEN Prestiges dieses Tiers ist (0 für den ersten).
        /// </summary>
        public static decimal CalculateDiminishedMultiplierBonus(decimal baseBonus, int tierCount)
        {
            if (tierCount < 0) tierCount = 0;
            return baseBonus * (1m / (1m + GameBalanceConstants.DiminishingReturnsPerTierPrestige * tierCount));
        }

        /// <summary>
        /// Wendet den Diminishing-Returns-Bonus auf den aktuellen permanenten Multiplikator an
        /// (addieren, auf 3 Nachkommastellen runden, auf <see cref="MaxPermanentMultiplier"/> kappen).
        /// </summary>
        public static decimal ApplyDiminishedBonus(decimal currentMultiplier, decimal baseBonus, int tierCount)
        {
            decimal diminishedBonus = CalculateDiminishedMultiplierBonus(baseBonus, tierCount);
            return Math.Min(Math.Round(currentMultiplier + diminishedBonus, 3), MaxPermanentMultiplier);
        }
    }
}
