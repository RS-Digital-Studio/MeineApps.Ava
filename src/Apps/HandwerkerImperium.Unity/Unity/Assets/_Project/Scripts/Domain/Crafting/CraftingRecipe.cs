#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using HandwerkerImperium.Domain;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Crafting
{
    /// <summary>
    /// Ein Handwerksrezept für die Produktionskette.
    /// Tier 1: Rohstoff · Tier 2: Halbzeug (eigene T1 + Cross-Workshop ab Lv100) ·
    /// Tier 3: Endprodukt · Tier 4: Imperiums-Manufaktur (ab Workshop-Lv500).
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/CraftingRecipe.cs). Statischer Katalog (kein
    /// Persistenz-Bedarf), Cross-Workshop-Onboarding-Regel ab <see cref="GameBalanceConstants.MaterialOrderCrossWorkshopLevel"/>.
    /// Unity-sicher (C# 9, netstandard2.1).
    /// </summary>
    public class CraftingRecipe
    {
        public string Id { get; set; } = "";
        public string NameKey { get; set; } = "";
        public WorkshopType WorkshopType { get; set; }
        public int RequiredWorkshopLevel { get; set; }
        public int Tier { get; set; } // 1-4
        public Dictionary<string, int> InputProducts { get; set; } = new(); // productId -> count

        /// <summary>Eigene Workshop-Inputs (immer gefordert, auch unter Lv100).</summary>
        [JsonIgnore]
        public Dictionary<string, int> OwnWorkshopInputs => GetOwnInputs(this);

        /// <summary>Cross-Workshop-Inputs (erst ab Spielerlevel 100 erforderlich, Onboarding-Schutz).</summary>
        [JsonIgnore]
        public Dictionary<string, int> CrossWorkshopInputs => GetCrossInputs(this);

        public string OutputProductId { get; set; } = "";
        public int OutputCount { get; set; } = 1;
        public int DurationSeconds { get; set; } = 60;

        /// <summary>Alle verfügbaren Rezepte (gecacht, keine Allokation pro Aufruf).</summary>
        private static readonly List<CraftingRecipe> AllRecipes = new List<CraftingRecipe>
        {
            // SCHREINER (Carpenter)
            new() { Id = "r_planks", NameKey = "CraftPlanks", WorkshopType = WorkshopType.Carpenter,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "planks", DurationSeconds = 30 },
            new() { Id = "r_furniture", NameKey = "CraftFurniture", WorkshopType = WorkshopType.Carpenter,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "planks", 3 }, { "paint_mix", 1 } },
                OutputProductId = "furniture", DurationSeconds = 120 },
            new() { Id = "r_luxury_furniture", NameKey = "CraftLuxuryFurniture", WorkshopType = WorkshopType.Carpenter,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "furniture", 2 }, { "fittings", 1 } },
                OutputProductId = "luxury_furniture", DurationSeconds = 300 },

            // KLEMPNER (Plumber)
            new() { Id = "r_pipes", NameKey = "CraftPipes", WorkshopType = WorkshopType.Plumber,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "pipes", DurationSeconds = 30 },
            new() { Id = "r_plumbing", NameKey = "CraftPlumbing", WorkshopType = WorkshopType.Plumber,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "pipes", 3 }, { "fittings", 1 } },
                OutputProductId = "plumbing_system", DurationSeconds = 120 },
            new() { Id = "r_bathroom", NameKey = "CraftBathroom", WorkshopType = WorkshopType.Plumber,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "plumbing_system", 2 }, { "cables", 1 } },
                OutputProductId = "bathroom_installation", DurationSeconds = 300 },

            // ELEKTRIKER (Electrician)
            new() { Id = "r_cables", NameKey = "CraftCables", WorkshopType = WorkshopType.Electrician,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "cables", DurationSeconds = 30 },
            new() { Id = "r_circuit", NameKey = "CraftCircuit", WorkshopType = WorkshopType.Electrician,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "cables", 3 }, { "prototype", 1 } },
                OutputProductId = "circuit", DurationSeconds = 120 },
            new() { Id = "r_smarthome", NameKey = "CraftSmartHome", WorkshopType = WorkshopType.Electrician,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "circuit", 2 }, { "concrete", 1 } },
                OutputProductId = "smart_home", DurationSeconds = 300 },

            // MALER (Painter)
            new() { Id = "r_paint", NameKey = "CraftPaint", WorkshopType = WorkshopType.Painter,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "paint_mix", DurationSeconds = 30 },
            new() { Id = "r_walldesign", NameKey = "CraftWallDesign", WorkshopType = WorkshopType.Painter,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "paint_mix", 3 }, { "blueprint", 1 } },
                OutputProductId = "wall_design", DurationSeconds = 120 },
            new() { Id = "r_artwork", NameKey = "CraftArtwork", WorkshopType = WorkshopType.Painter,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "wall_design", 2 }, { "planks", 1 } },
                OutputProductId = "artwork", DurationSeconds = 300 },

            // DACHDECKER (Roofer)
            new() { Id = "r_tiles", NameKey = "CraftTiles", WorkshopType = WorkshopType.Roofer,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "roof_tiles", DurationSeconds = 30 },
            new() { Id = "r_roofing", NameKey = "CraftRoofing", WorkshopType = WorkshopType.Roofer,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "roof_tiles", 3 }, { "concrete", 1 } },
                OutputProductId = "roofing_system", DurationSeconds = 120 },
            new() { Id = "r_roof_structure", NameKey = "CraftRoofStructure", WorkshopType = WorkshopType.Roofer,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "roofing_system", 2 }, { "blueprint", 1 } },
                OutputProductId = "roof_structure", DurationSeconds = 300 },

            // BAUUNTERNEHMER (Contractor)
            new() { Id = "r_concrete", NameKey = "CraftConcrete", WorkshopType = WorkshopType.Contractor,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "concrete", DurationSeconds = 30 },
            new() { Id = "r_foundation", NameKey = "CraftFoundation", WorkshopType = WorkshopType.Contractor,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "concrete", 3 }, { "pipes", 1 } },
                OutputProductId = "concrete_foundation", DurationSeconds = 150 },
            new() { Id = "r_skyscraper_frame", NameKey = "CraftSkyscraperFrame", WorkshopType = WorkshopType.Contractor,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "concrete_foundation", 2 }, { "contract", 1 } },
                OutputProductId = "skyscraper_frame", DurationSeconds = 360 },

            // ARCHITEKT (Architect)
            new() { Id = "r_blueprint", NameKey = "CraftBlueprint", WorkshopType = WorkshopType.Architect,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "blueprint", DurationSeconds = 30 },
            new() { Id = "r_framework", NameKey = "CraftFramework", WorkshopType = WorkshopType.Architect,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "blueprint", 3 }, { "planks", 1 }, { "concrete", 1 } },
                OutputProductId = "framework", DurationSeconds = 150 },
            new() { Id = "r_master_blueprint", NameKey = "CraftMasterBlueprint", WorkshopType = WorkshopType.Architect,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "framework", 2 }, { "contract", 1 } },
                OutputProductId = "master_blueprint", DurationSeconds = 360 },

            // GENERALUNTERNEHMER (GeneralContractor)
            new() { Id = "r_contract", NameKey = "CraftContract", WorkshopType = WorkshopType.GeneralContractor,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "contract", DurationSeconds = 30 },
            new() { Id = "r_contract_complex", NameKey = "CraftContractComplex", WorkshopType = WorkshopType.GeneralContractor,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "contract", 3 }, { "blueprint", 1 } },
                OutputProductId = "contract_complex", DurationSeconds = 150 },
            new() { Id = "r_general_contract", NameKey = "CraftGeneralContract", WorkshopType = WorkshopType.GeneralContractor,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "contract_complex", 2 }, { "blueprint", 1 } },
                OutputProductId = "general_contract", DurationSeconds = 420 },

            // MEISTERSCHMIEDE (MasterSmith)
            new() { Id = "r_fittings", NameKey = "CraftFittings", WorkshopType = WorkshopType.MasterSmith,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "fittings", DurationSeconds = 30 },
            new() { Id = "r_master_fittings", NameKey = "CraftMasterFittings", WorkshopType = WorkshopType.MasterSmith,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "fittings", 3 }, { "cables", 1 } },
                OutputProductId = "master_fittings", DurationSeconds = 150 },
            new() { Id = "r_masterpiece_fittings", NameKey = "CraftMasterpieceFittings", WorkshopType = WorkshopType.MasterSmith,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "master_fittings", 2 }, { "prototype", 1 } },
                OutputProductId = "masterpiece_fittings", DurationSeconds = 360 },

            // INNOVATIONSLABOR (InnovationLab)
            new() { Id = "r_prototype", NameKey = "CraftPrototype", WorkshopType = WorkshopType.InnovationLab,
                RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "prototype", DurationSeconds = 30 },
            new() { Id = "r_innovation", NameKey = "CraftInnovation", WorkshopType = WorkshopType.InnovationLab,
                RequiredWorkshopLevel = 150, Tier = 2,
                InputProducts = new() { { "prototype", 3 }, { "cables", 1 } },
                OutputProductId = "innovation", DurationSeconds = 150 },
            new() { Id = "r_patent", NameKey = "CraftPatent", WorkshopType = WorkshopType.InnovationLab,
                RequiredWorkshopLevel = 300, Tier = 3,
                InputProducts = new() { { "innovation", 2 }, { "master_fittings", 1 } },
                OutputProductId = "patent", DurationSeconds = 420 },

            // TIER 4 — IMPERIUMS-MANUFAKTUR (ab Workshop-Level 500)
            new() { Id = "r_villa", NameKey = "CraftVilla", WorkshopType = WorkshopType.GeneralContractor,
                RequiredWorkshopLevel = 500, Tier = 4,
                InputProducts = new() { { "luxury_furniture", 5 }, { "smart_home", 3 }, { "roof_structure", 2 }, { "artwork", 1 } },
                OutputProductId = "villa", DurationSeconds = 1800 },
            new() { Id = "r_skyscraper", NameKey = "CraftSkyscraper", WorkshopType = WorkshopType.GeneralContractor,
                RequiredWorkshopLevel = 500, Tier = 4,
                InputProducts = new() { { "skyscraper_frame", 5 }, { "bathroom_installation", 3 }, { "smart_home", 3 }, { "artwork", 2 } },
                OutputProductId = "skyscraper", DurationSeconds = 2400 },
            new() { Id = "r_imperium_hq", NameKey = "CraftImperiumHq", WorkshopType = WorkshopType.GeneralContractor,
                RequiredWorkshopLevel = 500, Tier = 4,
                InputProducts = new()
                {
                    { "luxury_furniture", 2 }, { "bathroom_installation", 2 }, { "smart_home", 2 }, { "artwork", 2 },
                    { "roof_structure", 2 }, { "skyscraper_frame", 2 }, { "master_blueprint", 2 },
                    { "general_contract", 1 }, { "masterpiece_fittings", 2 }, { "patent", 2 }
                },
                OutputProductId = "imperium_hq", DurationSeconds = 3600 },
        };

        public static List<CraftingRecipe> GetAllRecipes() => AllRecipes;

        // Gecachte Lookups (einmalig erstellt, O(1) statt O(n))
        private static readonly Dictionary<string, CraftingRecipe> RecipeById =
            AllRecipes.ToDictionary(r => r.Id, r => r);
        private static readonly Dictionary<string, CraftingRecipe> RecipeByOutputProduct =
            AllRecipes.ToDictionary(r => r.OutputProductId, r => r);

        /// <summary>Findet ein Rezept anhand der ID (O(1)).</summary>
        public static CraftingRecipe? GetById(string recipeId) =>
            RecipeById.GetValueOrDefault(recipeId);

        /// <summary>Findet das Rezept das ein bestimmtes Produkt herstellt (O(1)).</summary>
        public static CraftingRecipe? GetByOutputProduct(string productId) =>
            RecipeByOutputProduct.GetValueOrDefault(productId);

        /// <summary>
        /// Liefert die effektiven Inputs unter Berücksichtigung der Onboarding-Regel:
        /// Cross-Workshop-Inputs erst ab Spielerlevel
        /// <see cref="GameBalanceConstants.MaterialOrderCrossWorkshopLevel"/>.
        /// </summary>
        public static Dictionary<string, int> GetEffectiveInputs(CraftingRecipe recipe, int playerLevel)
        {
            if (playerLevel >= GameBalanceConstants.MaterialOrderCrossWorkshopLevel)
                return recipe.InputProducts;

            return GetOwnInputs(recipe);
        }

        private static Dictionary<string, int> GetOwnInputs(CraftingRecipe recipe)
        {
            var own = new Dictionary<string, int>();
            foreach (var (productId, count) in recipe.InputProducts)
            {
                var inputRecipe = GetByOutputProduct(productId);
                if (inputRecipe != null && inputRecipe.WorkshopType == recipe.WorkshopType)
                    own[productId] = count;
            }
            return own;
        }

        private static Dictionary<string, int> GetCrossInputs(CraftingRecipe recipe)
        {
            var cross = new Dictionary<string, int>();
            foreach (var (productId, count) in recipe.InputProducts)
            {
                var inputRecipe = GetByOutputProduct(productId);
                if (inputRecipe == null || inputRecipe.WorkshopType != recipe.WorkshopType)
                    cross[productId] = count;
            }
            return cross;
        }
    }

    /// <summary>Ein Produkt das hergestellt werden kann (statischer Katalog).</summary>
    public class CraftingProduct
    {
        public string Id { get; set; } = "";
        public string NameKey { get; set; } = "";
        public int Tier { get; set; }
        public decimal BaseValue { get; set; }

        /// <summary>True wenn das Produkt beim Prestige als Erbstück mitgenommen werden kann (nur T4).</summary>
        public bool IsHeirloomEligible { get; set; }

        private static readonly Dictionary<string, CraftingProduct> AllProducts = new Dictionary<string, CraftingProduct>
        {
            // TIER 1 — Rohstoffe
            ["planks"] = new() { Id = "planks", NameKey = "ProductPlanks", Tier = 1, BaseValue = 500m },
            ["pipes"] = new() { Id = "pipes", NameKey = "ProductPipes", Tier = 1, BaseValue = 500m },
            ["cables"] = new() { Id = "cables", NameKey = "ProductCables", Tier = 1, BaseValue = 500m },
            ["paint_mix"] = new() { Id = "paint_mix", NameKey = "ProductPaintMix", Tier = 1, BaseValue = 400m },
            ["roof_tiles"] = new() { Id = "roof_tiles", NameKey = "ProductRoofTiles", Tier = 1, BaseValue = 600m },
            ["concrete"] = new() { Id = "concrete", NameKey = "ProductConcrete", Tier = 1, BaseValue = 800m },
            ["blueprint"] = new() { Id = "blueprint", NameKey = "ProductBlueprint", Tier = 1, BaseValue = 1000m },
            ["contract"] = new() { Id = "contract", NameKey = "ProductContract", Tier = 1, BaseValue = 1500m },
            ["fittings"] = new() { Id = "fittings", NameKey = "ProductFittings", Tier = 1, BaseValue = 1200m },
            ["prototype"] = new() { Id = "prototype", NameKey = "ProductPrototype", Tier = 1, BaseValue = 2000m },

            // TIER 2 — Halbzeuge
            ["furniture"] = new() { Id = "furniture", NameKey = "ProductFurniture", Tier = 2, BaseValue = 2500m },
            ["plumbing_system"] = new() { Id = "plumbing_system", NameKey = "ProductPlumbing", Tier = 2, BaseValue = 2500m },
            ["circuit"] = new() { Id = "circuit", NameKey = "ProductCircuit", Tier = 2, BaseValue = 2500m },
            ["wall_design"] = new() { Id = "wall_design", NameKey = "ProductWallDesign", Tier = 2, BaseValue = 2000m },
            ["roofing_system"] = new() { Id = "roofing_system", NameKey = "ProductRoofing", Tier = 2, BaseValue = 3000m },
            ["concrete_foundation"] = new() { Id = "concrete_foundation", NameKey = "ProductFoundation", Tier = 2, BaseValue = 4000m },
            ["framework"] = new() { Id = "framework", NameKey = "ProductFramework", Tier = 2, BaseValue = 5000m },
            ["contract_complex"] = new() { Id = "contract_complex", NameKey = "ProductContractComplex", Tier = 2, BaseValue = 6000m },
            ["master_fittings"] = new() { Id = "master_fittings", NameKey = "ProductMasterFittings", Tier = 2, BaseValue = 5000m },
            ["innovation"] = new() { Id = "innovation", NameKey = "ProductInnovation", Tier = 2, BaseValue = 7000m },

            // TIER 3 — Endprodukte
            ["luxury_furniture"] = new() { Id = "luxury_furniture", NameKey = "ProductLuxuryFurniture", Tier = 3, BaseValue = 50000m },
            ["bathroom_installation"] = new() { Id = "bathroom_installation", NameKey = "ProductBathroom", Tier = 3, BaseValue = 50000m },
            ["smart_home"] = new() { Id = "smart_home", NameKey = "ProductSmartHome", Tier = 3, BaseValue = 50000m },
            ["artwork"] = new() { Id = "artwork", NameKey = "ProductArtwork", Tier = 3, BaseValue = 40000m },
            ["roof_structure"] = new() { Id = "roof_structure", NameKey = "ProductRoofStructure", Tier = 3, BaseValue = 60000m },
            ["skyscraper_frame"] = new() { Id = "skyscraper_frame", NameKey = "ProductSkyscraperFrame", Tier = 3, BaseValue = 60000m },
            ["master_blueprint"] = new() { Id = "master_blueprint", NameKey = "ProductMasterBlueprint", Tier = 3, BaseValue = 70000m },
            ["general_contract"] = new() { Id = "general_contract", NameKey = "ProductGeneralContract", Tier = 3, BaseValue = 80000m },
            ["masterpiece_fittings"] = new() { Id = "masterpiece_fittings", NameKey = "ProductMasterpieceFittings", Tier = 3, BaseValue = 60000m },
            ["patent"] = new() { Id = "patent", NameKey = "ProductPatent", Tier = 3, BaseValue = 75000m },

            // TIER 4 — Imperiums-Manufaktur (alle Erbstück-fähig)
            ["villa"] = new() { Id = "villa", NameKey = "ProductVilla", Tier = 4, BaseValue = 2_500_000m, IsHeirloomEligible = true },
            ["skyscraper"] = new() { Id = "skyscraper", NameKey = "ProductSkyscraper", Tier = 4, BaseValue = 4_000_000m, IsHeirloomEligible = true },
            ["imperium_hq"] = new() { Id = "imperium_hq", NameKey = "ProductImperiumHq", Tier = 4, BaseValue = 5_000_000m, IsHeirloomEligible = true },
        };

        public static Dictionary<string, CraftingProduct> GetAllProducts() => AllProducts;
    }

    /// <summary>Ein aktiver Crafting-Auftrag (persistiert via Newtonsoft).</summary>
    public class CraftingJob
    {
        /// <summary>Eindeutige Job-ID (GUID). Verhindert Verwechslung bei mehreren Jobs desselben Rezepts.</summary>
        [JsonProperty("jobId")]
        public string JobId { get; set; } = Guid.NewGuid().ToString("N");

        [JsonProperty("recipeId")]
        public string RecipeId { get; set; } = "";

        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonProperty("durationSeconds")]
        public int DurationSeconds { get; set; }

        [JsonIgnore]
        public bool IsComplete => (DateTime.UtcNow - StartedAt).TotalSeconds >= DurationSeconds;

        [JsonIgnore]
        public double Progress => Math.Clamp((DateTime.UtcNow - StartedAt).TotalSeconds / DurationSeconds, 0.0, 1.0);

        [JsonIgnore]
        public TimeSpan TimeRemaining
        {
            get
            {
                var remaining = TimeSpan.FromSeconds(DurationSeconds) - (DateTime.UtcNow - StartedAt);
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }
}
