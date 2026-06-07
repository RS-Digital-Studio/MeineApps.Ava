using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>Ausrüstungstyp für Arbeiter.</summary>
    public enum EquipmentType
    {
        Helmet,
        Gloves,
        Boots,
        Belt
    }

    /// <summary>Seltenheitsstufe der Ausrüstung.</summary>
    public enum EquipmentRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic
    }

    /// <summary>
    /// Ein Ausrüstungsgegenstand, der einem Arbeiter zugewiesen werden kann.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Equipment.cs). Persistenz über Newtonsoft.Json
    /// (Unity-Konvention) statt System.Text.Json. UI-Methoden (Farbe/Icon) leben in der Präsentationsschicht.
    /// Unity-sicher (C# 9, netstandard2.1): kein Random.Shared, kein Range-Operator.
    /// </summary>
    public class Equipment
    {
        // Eine geteilte RNG-Instanz (Unity-netstandard hat kein Random.Shared).
        private static readonly Random Rng = new Random();

        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("type")]
        public EquipmentType Type { get; set; }

        [JsonProperty("rarity")]
        public EquipmentRarity Rarity { get; set; }

        [JsonProperty("nameKey")]
        public string NameKey { get; set; } = "";

        [JsonProperty("efficiencyBonus")]
        public decimal EfficiencyBonus { get; set; }

        [JsonProperty("fatigueReduction")]
        public decimal FatigueReduction { get; set; }

        [JsonProperty("moodBonus")]
        public decimal MoodBonus { get; set; }

        /// <summary>Erzeugt ein zufälliges Equipment basierend auf Schwierigkeit.</summary>
        public static Equipment GenerateRandom(int difficultyLevel)
        {
            var rng = Rng;
            var type = (EquipmentType)rng.Next(4);

            // Seltenheit gewichtet nach Schwierigkeit
            int roll = rng.Next(100);
            EquipmentRarity rarity;
            if (difficultyLevel >= 3 && roll < 5) rarity = EquipmentRarity.Epic;
            else if (difficultyLevel >= 2 && roll < 20) rarity = EquipmentRarity.Rare;
            else if (roll < 45) rarity = EquipmentRarity.Uncommon;
            else rarity = EquipmentRarity.Common;

            // Bonuswerte nach Seltenheit
            decimal effBonus = rarity switch
            {
                EquipmentRarity.Common => rng.Next(5, 8) / 100m,
                EquipmentRarity.Uncommon => rng.Next(8, 11) / 100m,
                EquipmentRarity.Rare => rng.Next(11, 14) / 100m,
                EquipmentRarity.Epic => rng.Next(13, 16) / 100m,
                _ => 0.05m
            };

            decimal fatReduction = rarity switch
            {
                EquipmentRarity.Common => rng.Next(3, 6) / 100m,
                EquipmentRarity.Uncommon => rng.Next(6, 9) / 100m,
                EquipmentRarity.Rare => rng.Next(9, 12) / 100m,
                EquipmentRarity.Epic => rng.Next(11, 15) / 100m,
                _ => 0.03m
            };

            decimal moodBonus = rarity switch
            {
                EquipmentRarity.Common => rng.Next(3, 6) / 100m,
                EquipmentRarity.Uncommon => rng.Next(5, 8) / 100m,
                EquipmentRarity.Rare => rng.Next(7, 10) / 100m,
                EquipmentRarity.Epic => rng.Next(8, 11) / 100m,
                _ => 0.03m
            };

            string nameKey = $"Equipment_{type}_{rarity}";

            return new Equipment
            {
                Type = type,
                Rarity = rarity,
                NameKey = nameKey,
                EfficiencyBonus = effBonus,
                FatigueReduction = fatReduction,
                MoodBonus = moodBonus
            };
        }

        /// <summary>
        /// Goldschrauben-Preis im Shop (Spiellogik-Wert, balancing-relevant).
        /// </summary>
        [JsonIgnore]
        public int ShopPrice => Rarity switch
        {
            EquipmentRarity.Common => 3,
            EquipmentRarity.Uncommon => 8,
            EquipmentRarity.Rare => 18,
            EquipmentRarity.Epic => 40,
            _ => 3
        };
    }
}
