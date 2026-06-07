using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Guild
{
    /// <summary>
    /// Statisches Template einer Mega-Projekt-Anforderung (Material-Bedarf + permanenter Gilden-Bonus).
    /// 1:1-Port aus dem Avalonia-Original (Models/Firebase/GuildMegaProject.cs). Reine Gameplay-Kataloge;
    /// die GuildMegaProject-Firebase-DTO selbst (HMAC/Contributions) bleibt für die Netzwerk-Schicht.
    /// GetNameKey (Lokalisierung) wandert in die Präsentationsschicht. GuildMegaProjectType-Enum ist in
    /// GuildNetworkEnums.cs (Schicht 10).
    /// </summary>
    public static class GuildMegaProjectTemplates
    {
        /// <summary>Soft-Cap: ab diesem Tag wird ein verlassenes Projekt mit Refund beendet.</summary>
        public const int AbandonmentSunsetDays = 30;

        /// <summary>Liefert die Material-Anforderung eines Mega-Projekt-Typs.</summary>
        public static Dictionary<string, int> GetRequirements(GuildMegaProjectType type) => type switch
        {
            GuildMegaProjectType.Cathedral => new Dictionary<string, int>
            {
                ["luxury_furniture"] = 50,
                ["roof_structure"] = 40,
                ["artwork"] = 30,
                ["smart_home"] = 20,
                ["villa"] = 1,
            },
            GuildMegaProjectType.Headquarters => new Dictionary<string, int>
            {
                ["skyscraper_frame"] = 80,
                ["smart_home"] = 60,
                ["bathroom_installation"] = 50,
                ["master_blueprint"] = 30,
                ["masterpiece_fittings"] = 30,
                ["villa"] = 2,
                ["skyscraper"] = 1,
            },
            _ => new Dictionary<string, int>()
        };

        /// <summary>Permanenter Gilden-Bonus den das Projekt freischaltet.</summary>
        public static GuildMegaProjectReward GetReward(GuildMegaProjectType type) => type switch
        {
            GuildMegaProjectType.Cathedral => new GuildMegaProjectReward
            {
                CraftingSpeedBonus = 0.05m,
                AutoSellPriceBonus = 0.10m,
                BonusWarehouseSlots = 3
            },
            GuildMegaProjectType.Headquarters => new GuildMegaProjectReward
            {
                CraftingSpeedBonus = 0.10m,
                AutoSellPriceBonus = 0.20m,
                BonusWarehouseSlots = 5
            },
            _ => new GuildMegaProjectReward()
        };
    }

    /// <summary>
    /// Permanenter Gilden-Bonus den ein abgeschlossenes Mega-Projekt freischaltet.
    /// 1:1-Port aus dem Avalonia-Original. Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class GuildMegaProjectReward
    {
        /// <summary>Permanenter +X Crafting-Speed-Bonus.</summary>
        [JsonProperty("craftingSpeedBonus")]
        public decimal CraftingSpeedBonus { get; set; }

        /// <summary>Permanenter +X auf Auto-Verkaufs-Preis.</summary>
        [JsonProperty("autoSellPriceBonus")]
        public decimal AutoSellPriceBonus { get; set; }

        /// <summary>Permanente +X Lager-Slots.</summary>
        [JsonProperty("bonusWarehouseSlots")]
        public int BonusWarehouseSlots { get; set; }
    }

    /// <summary>
    /// Donation-Eintrag eines Spielers im Mega-Projekt (Top-Spender-Leaderboard).
    /// 1:1-Port aus dem Avalonia-Original. Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class GuildMegaProjectDonation
    {
        [JsonProperty("playerName")]
        public string PlayerName { get; set; } = "";

        [JsonProperty("totalValue")]
        public decimal TotalValue { get; set; }

        [JsonProperty("itemCount")]
        public int ItemCount { get; set; }

        [JsonProperty("lastDonatedAt")]
        public DateTime LastDonatedAt { get; set; } = DateTime.UtcNow;
    }
}
