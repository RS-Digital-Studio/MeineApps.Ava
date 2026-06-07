using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Verfolgt alle Prestige-bezogenen Daten über Resets hinweg.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/PrestigeData.cs). Datenklasse + reine
    /// Verfügbarkeits-/PP-Logik (CalculatePrestigePoints, CanPrestige, GetHighestAvailableTier).
    /// Persistenz: Newtonsoft.Json.
    /// </summary>
    public class PrestigeData
    {
        /// <summary>Aktuell höchste erreichte Stufe.</summary>
        [JsonProperty("currentTier")]
        public PrestigeTier CurrentTier { get; set; } = PrestigeTier.None;

        [JsonProperty("bronzeCount")]
        public int BronzeCount { get; set; }

        [JsonProperty("silverCount")]
        public int SilverCount { get; set; }

        [JsonProperty("goldCount")]
        public int GoldCount { get; set; }

        [JsonProperty("platinCount")]
        public int PlatinCount { get; set; }

        [JsonProperty("diamantCount")]
        public int DiamantCount { get; set; }

        [JsonProperty("meisterCount")]
        public int MeisterCount { get; set; }

        [JsonProperty("legendeCount")]
        public int LegendeCount { get; set; }

        /// <summary>Ausgebbare Prestige-Punkte.</summary>
        [JsonProperty("prestigePoints")]
        public int PrestigePoints { get; set; }

        /// <summary>Insgesamt verdiente Prestige-Punkte (Lifetime).</summary>
        [JsonProperty("totalPrestigePoints")]
        public int TotalPrestigePoints { get; set; }

        /// <summary>IDs gekaufter Prestige-Shop-Items.</summary>
        [JsonProperty("purchasedShopItems")]
        public List<string> PurchasedShopItems { get; set; } = new List<string>();

        /// <summary>
        /// Gespeicherte beste Worker pro Workshop-Typ (Legende-Prestige).
        /// Key = WorkshopType-Name, Value = Worker-Instanz.
        /// Wird beim Workshop-Unlock angewendet und dann entfernt.
        /// </summary>
        [JsonProperty("keptWorkers")]
        public Dictionary<string, Worker> KeptWorkers { get; set; } = new Dictionary<string, Worker>();

        /// <summary>Kaufanzahl für wiederholbare Prestige-Shop-Items. Key = Item-ID, Value = Anzahl.</summary>
        [JsonProperty("repeatableItemCounts")]
        public Dictionary<string, int> RepeatableItemCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>Chronologische Aufzeichnung aller Prestige-Durchläufe (max. 20, neueste zuerst).</summary>
        [JsonProperty("history")]
        public List<PrestigeHistoryEntry> History { get; set; } = new List<PrestigeHistoryEntry>();

        // ── Prestige-Herausforderungen ──

        /// <summary>
        /// Aktive Run-Modifikatoren für den nächsten/aktuellen Durchlauf.
        /// Max 3 gleichzeitig (PrestigeChallengeExtensions.MaxActiveChallenges).
        /// </summary>
        [JsonProperty("activeChallenges")]
        public List<PrestigeChallengeType> ActiveChallenges { get; set; } = new List<PrestigeChallengeType>();

        // ── Speedrun-Tracking ──

        /// <summary>
        /// Startzeit des aktuellen Durchlaufs (UTC). DateTime.MinValue = erster Run.
        /// </summary>
        [JsonProperty("runStartTime")]
        public DateTime RunStartTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Bestzeiten pro Prestige-Tier (Key = PrestigeTier.ToString(), Value = Ticks).
        /// Nur der schnellste Run pro Tier (max 7 Einträge).
        /// </summary>
        [JsonProperty("bestRunTimes")]
        public Dictionary<string, long> BestRunTimes { get; set; } = new Dictionary<string, long>();

        /// <summary>Gibt die Bestzeit für einen Tier zurück (null = noch keine).</summary>
        public TimeSpan? GetBestRunTime(PrestigeTier tier) =>
            BestRunTimes.TryGetValue(tier.ToString(), out var ticks) ? TimeSpan.FromTicks(ticks) : (TimeSpan?)null;

        // ── Prestige-Meilensteine ──

        /// <summary>
        /// IDs bereits beanspruchter Prestige-Meilensteine. Permanent (kein Reset bei Ascension).
        /// </summary>
        [JsonProperty("claimedMilestones")]
        public HashSet<string> ClaimedMilestones { get; set; } = new HashSet<string>();

        /// <summary>
        /// Wiederholbarer „Wochen-Meilenstein" — alle 7 Prestiges +5 GS.
        /// Counter zählt pro Prestige hoch; bei 7 wird Bonus vergeben und Counter resettet.
        /// </summary>
        [JsonProperty("prestigesSinceLastWeeklyReward")]
        public int PrestigesSinceLastWeeklyReward { get; set; }

        /// <summary>Kumulativer permanenter Einkommens-Multiplikator aus allen Prestiges. Start 1.0.</summary>
        [JsonProperty("permanentMultiplier")]
        public decimal PermanentMultiplier { get; set; } = 1.0m;

        /// <summary>Gesamtzahl aller Prestiges über alle Stufen.</summary>
        [JsonIgnore]
        public int TotalPrestigeCount => BronzeCount + SilverCount + GoldCount
            + PlatinCount + DiamantCount + MeisterCount + LegendeCount;

        /// <summary>
        /// Berechnet die Prestige-Punkte für den AKTUELLEN Durchlauf.
        /// Formel: floor(sqrt(currentRunMoney / 100_000)).
        /// </summary>
        /// <param name="currentRunMoney">Im aktuellen Durchlauf verdientes Geld (nicht kumulativ).</param>
        public static int CalculatePrestigePoints(decimal currentRunMoney)
        {
            if (currentRunMoney <= 0) return 0;
            return (int)Math.Floor(Math.Sqrt((double)(currentRunMoney / 100_000m)));
        }

        /// <summary>Prüft, ob eine bestimmte Prestige-Stufe verfügbar ist.</summary>
        public bool CanPrestige(PrestigeTier tier, int playerLevel)
        {
            if (playerLevel < tier.GetRequiredLevel()) return false;

            return tier switch
            {
                PrestigeTier.Bronze => true,
                PrestigeTier.Silver => BronzeCount >= tier.GetRequiredPreviousTierCount(),
                PrestigeTier.Gold => SilverCount >= tier.GetRequiredPreviousTierCount(),
                PrestigeTier.Platin => GoldCount >= tier.GetRequiredPreviousTierCount(),
                PrestigeTier.Diamant => PlatinCount >= tier.GetRequiredPreviousTierCount(),
                PrestigeTier.Meister => DiamantCount >= tier.GetRequiredPreviousTierCount(),
                PrestigeTier.Legende => MeisterCount >= tier.GetRequiredPreviousTierCount(),
                _ => false
            };
        }

        /// <summary>Gibt die höchste verfügbare Stufe für Prestige zurück.</summary>
        public PrestigeTier GetHighestAvailableTier(int playerLevel)
        {
            if (CanPrestige(PrestigeTier.Legende, playerLevel)) return PrestigeTier.Legende;
            if (CanPrestige(PrestigeTier.Meister, playerLevel)) return PrestigeTier.Meister;
            if (CanPrestige(PrestigeTier.Diamant, playerLevel)) return PrestigeTier.Diamant;
            if (CanPrestige(PrestigeTier.Platin, playerLevel)) return PrestigeTier.Platin;
            if (CanPrestige(PrestigeTier.Gold, playerLevel)) return PrestigeTier.Gold;
            if (CanPrestige(PrestigeTier.Silver, playerLevel)) return PrestigeTier.Silver;
            if (CanPrestige(PrestigeTier.Bronze, playerLevel)) return PrestigeTier.Bronze;
            return PrestigeTier.None;
        }

        /// <summary>Gibt alle verfügbaren Tiers zurück (aufsteigend sortiert).</summary>
        public List<PrestigeTier> GetAllAvailableTiers(int playerLevel)
        {
            var tiers = new List<PrestigeTier>();
            PrestigeTier[] allTiers = new PrestigeTier[]
            {
                PrestigeTier.Bronze, PrestigeTier.Silver, PrestigeTier.Gold,
                PrestigeTier.Platin, PrestigeTier.Diamant, PrestigeTier.Meister,
                PrestigeTier.Legende
            };

            foreach (var tier in allTiers)
            {
                if (CanPrestige(tier, playerLevel))
                    tiers.Add(tier);
            }

            return tiers;
        }
    }
}
