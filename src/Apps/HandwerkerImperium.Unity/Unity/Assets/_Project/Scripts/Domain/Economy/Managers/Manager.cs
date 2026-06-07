#nullable enable
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Statische Definition eines Managers (Template). 1:1-Port aus dem Avalonia-Original
    /// (Models/Manager.cs). Original ist ein positional record mit GetDisplayName(ILocalizationService)
    /// — in Unity/netstandard2.1 kein IsExternalInit → einfache Klasse mit Ctor; GetDisplayName +
    /// DefaultNames-Tabelle (Lokalisierung/Display) wandern in die Präsentationsschicht.
    /// ManagerAbility-Enum ist in ManagerAbility.cs (Schicht 10).
    /// </summary>
    public sealed class ManagerDefinition
    {
        public string Id { get; }
        public string NameKey { get; }
        public WorkshopType? Workshop { get; }  // null = gilt für alle
        public ManagerAbility Ability { get; }
        public int RequiredLevel { get; }
        public int RequiredPrestige { get; }
        public int RequiredPerfectRatings { get; }

        public ManagerDefinition(string id, string nameKey, WorkshopType? workshop, ManagerAbility ability,
            int requiredLevel, int requiredPrestige, int requiredPerfectRatings)
        {
            Id = id;
            NameKey = nameKey;
            Workshop = workshop;
            Ability = ability;
            RequiredLevel = requiredLevel;
            RequiredPrestige = requiredPrestige;
            RequiredPerfectRatings = requiredPerfectRatings;
        }
    }

    /// <summary>
    /// Ein freigeschalteter Manager mit Level. 1:1-Port aus dem Avalonia-Original. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class Manager
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("level")]
        public int Level { get; set; } = 1;

        [JsonProperty("isUnlocked")]
        public bool IsUnlocked { get; set; }

        /// <summary>Max-Level ist 5.</summary>
        [JsonIgnore]
        public bool IsMaxLevel => Level >= 5;

        /// <summary>Upgrade-Kosten in Goldschrauben.</summary>
        [JsonIgnore]
        public int UpgradeCost => Level * 10;

        /// <summary>Alle 14 Manager-Definitionen (gecacht, keine Allokation pro Aufruf).</summary>
        private static readonly List<ManagerDefinition> _allDefinitions = new List<ManagerDefinition>
        {
            new ManagerDefinition("mgr_hans", "ManagerHans", WorkshopType.Carpenter, ManagerAbility.EfficiencyBoost, 10, 0, 0),
            new ManagerDefinition("mgr_fritz", "ManagerFritz", WorkshopType.Plumber, ManagerAbility.FatigueReduction, 20, 0, 0),
            new ManagerDefinition("mgr_kurt", "ManagerKurt", WorkshopType.Electrician, ManagerAbility.IncomeBoost, 30, 0, 0),
            new ManagerDefinition("mgr_lisa", "ManagerLisa", WorkshopType.Painter, ManagerAbility.MoodBoost, 40, 0, 0),
            new ManagerDefinition("mgr_karl", "ManagerKarl", WorkshopType.Roofer, ManagerAbility.EfficiencyBoost, 60, 0, 0),
            new ManagerDefinition("mgr_otto", "ManagerOtto", WorkshopType.Contractor, ManagerAbility.IncomeBoost, 80, 0, 0),
            new ManagerDefinition("mgr_anna", "ManagerAnna", WorkshopType.Architect, ManagerAbility.FatigueReduction, 0, 0, 25),
            new ManagerDefinition("mgr_max", "ManagerMax", WorkshopType.GeneralContractor, ManagerAbility.IncomeBoost, 100, 0, 0),
            new ManagerDefinition("mgr_schmied", "ManagerSchmied", WorkshopType.MasterSmith, ManagerAbility.EfficiencyBoost, 120, 0, 0),
            new ManagerDefinition("mgr_erfinder", "ManagerErfinder", WorkshopType.InnovationLab, ManagerAbility.IncomeBoost, 140, 0, 0),
            new ManagerDefinition("mgr_schmidt", "ManagerSchmidt", null, ManagerAbility.TrainingSpeedUp, 0, 1, 0),
            new ManagerDefinition("mgr_weber", "ManagerWeber", null, ManagerAbility.AutoCollectOrders, 0, 2, 0),
            new ManagerDefinition("mgr_mueller", "ManagerMueller", null, ManagerAbility.EfficiencyBoost, 0, 3, 0),
            new ManagerDefinition("mgr_kaiser", "ManagerKaiser", null, ManagerAbility.IncomeBoost, 0, 4, 0),
        };

        private static readonly Dictionary<string, ManagerDefinition> _definitionsById =
            _allDefinitions.ToDictionary(d => d.Id, d => d);

        public static List<ManagerDefinition> GetAllDefinitions() => _allDefinitions;

        /// <summary>Findet eine Manager-Definition per ID (O(1) Dictionary-Lookup).</summary>
        public static ManagerDefinition? GetDefinitionById(string id) =>
            _definitionsById.TryGetValue(id, out var def) ? def : null;

        /// <summary>Berechnet den Bonus basierend auf Manager-Level und Fähigkeit.</summary>
        public decimal GetBonus(ManagerAbility ability)
        {
            if (!IsUnlocked) return 0m;
            var def = GetDefinitionById(Id);
            if (def == null || def.Ability != ability) return 0m;

            return ability switch
            {
                ManagerAbility.EfficiencyBoost => 0.05m * Level,   // +5% pro Level
                ManagerAbility.FatigueReduction => 0.03m * Level,  // -3% pro Level
                ManagerAbility.MoodBoost => 0.04m * Level,         // +4% pro Level
                ManagerAbility.IncomeBoost => 0.05m * Level,       // +5% pro Level
                ManagerAbility.TrainingSpeedUp => 0.10m * Level,   // +10% pro Level
                ManagerAbility.AutoCollectOrders => Level,          // Anzahl pro Check
                _ => 0m
            };
        }
    }
}
