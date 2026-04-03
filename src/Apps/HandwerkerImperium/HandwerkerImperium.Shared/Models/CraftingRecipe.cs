using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Ein Handwerksrezept für die Produktionskette.
/// </summary>
public class CraftingRecipe
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public WorkshopType WorkshopType { get; set; }
    public int RequiredWorkshopLevel { get; set; }
    public int Tier { get; set; } // 1-3
    public Dictionary<string, int> InputProducts { get; set; } = new(); // productId → count
    public string OutputProductId { get; set; } = "";
    public int OutputCount { get; set; } = 1;
    public int DurationSeconds { get; set; } = 60;

    /// <summary>
    /// Alle verfügbaren Rezepte (gecacht, keine Allokation pro Aufruf).
    /// </summary>
    private static readonly List<CraftingRecipe> AllRecipes =
    [
        // Schreiner Tier 1 (ab Level 50)
        new() { Id = "r_planks", NameKey = "CraftPlanks", WorkshopType = WorkshopType.Carpenter,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "planks", DurationSeconds = 30 },
        // Schreiner Tier 2 (ab Level 150)
        new() { Id = "r_furniture", NameKey = "CraftFurniture", WorkshopType = WorkshopType.Carpenter,
            RequiredWorkshopLevel = 150, Tier = 2,
            InputProducts = new() { { "planks", 3 } },
            OutputProductId = "furniture", DurationSeconds = 120 },
        // Schreiner Tier 3 (ab Level 300)
        new() { Id = "r_luxury_furniture", NameKey = "CraftLuxuryFurniture", WorkshopType = WorkshopType.Carpenter,
            RequiredWorkshopLevel = 300, Tier = 3,
            InputProducts = new() { { "furniture", 2 } },
            OutputProductId = "luxury_furniture", DurationSeconds = 300 },

        // Klempner
        new() { Id = "r_pipes", NameKey = "CraftPipes", WorkshopType = WorkshopType.Plumber,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "pipes", DurationSeconds = 30 },
        new() { Id = "r_plumbing", NameKey = "CraftPlumbing", WorkshopType = WorkshopType.Plumber,
            RequiredWorkshopLevel = 150, Tier = 2,
            InputProducts = new() { { "pipes", 3 } },
            OutputProductId = "plumbing_system", DurationSeconds = 120 },
        new() { Id = "r_bathroom", NameKey = "CraftBathroom", WorkshopType = WorkshopType.Plumber,
            RequiredWorkshopLevel = 300, Tier = 3,
            InputProducts = new() { { "plumbing_system", 2 } },
            OutputProductId = "bathroom_installation", DurationSeconds = 300 },

        // Elektriker
        new() { Id = "r_cables", NameKey = "CraftCables", WorkshopType = WorkshopType.Electrician,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "cables", DurationSeconds = 30 },
        new() { Id = "r_circuit", NameKey = "CraftCircuit", WorkshopType = WorkshopType.Electrician,
            RequiredWorkshopLevel = 150, Tier = 2,
            InputProducts = new() { { "cables", 3 } },
            OutputProductId = "circuit", DurationSeconds = 120 },
        new() { Id = "r_smarthome", NameKey = "CraftSmartHome", WorkshopType = WorkshopType.Electrician,
            RequiredWorkshopLevel = 300, Tier = 3,
            InputProducts = new() { { "circuit", 2 } },
            OutputProductId = "smart_home", DurationSeconds = 300 },

        // Maler
        new() { Id = "r_paint", NameKey = "CraftPaint", WorkshopType = WorkshopType.Painter,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "paint_mix", DurationSeconds = 30 },
        new() { Id = "r_walldesign", NameKey = "CraftWallDesign", WorkshopType = WorkshopType.Painter,
            RequiredWorkshopLevel = 150, Tier = 2,
            InputProducts = new() { { "paint_mix", 3 } },
            OutputProductId = "wall_design", DurationSeconds = 120 },

        // Dachdecker
        new() { Id = "r_tiles", NameKey = "CraftTiles", WorkshopType = WorkshopType.Roofer,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "roof_tiles", DurationSeconds = 30 },
        new() { Id = "r_roofing", NameKey = "CraftRoofing", WorkshopType = WorkshopType.Roofer,
            RequiredWorkshopLevel = 150, Tier = 2,
            InputProducts = new() { { "roof_tiles", 3 } },
            OutputProductId = "roofing_system", DurationSeconds = 120 },
        // Dachdecker Tier 3 (ab Level 300)
        new() { Id = "r_roof_structure", NameKey = "CraftRoofStructure", WorkshopType = WorkshopType.Roofer,
            RequiredWorkshopLevel = 300, Tier = 3,
            InputProducts = new() { { "roofing_system", 2 } },
            OutputProductId = "roof_structure", DurationSeconds = 300 },

        // Maler Tier 3 (ab Level 300)
        new() { Id = "r_artwork", NameKey = "CraftArtwork", WorkshopType = WorkshopType.Painter,
            RequiredWorkshopLevel = 300, Tier = 3,
            InputProducts = new() { { "wall_design", 2 } },
            OutputProductId = "artwork", DurationSeconds = 300 },

        // Bauunternehmer Tier 1 (ab Level 50)
        new() { Id = "r_concrete", NameKey = "CraftConcrete", WorkshopType = WorkshopType.Contractor,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "concrete", DurationSeconds = 30 },

        // Architekt Tier 1 (ab Level 50)
        new() { Id = "r_blueprint", NameKey = "CraftBlueprint", WorkshopType = WorkshopType.Architect,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "blueprint", DurationSeconds = 30 },

        // Generalunternehmer Tier 1 (ab Level 50)
        new() { Id = "r_contract", NameKey = "CraftContract", WorkshopType = WorkshopType.GeneralContractor,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "contract", DurationSeconds = 30 },

        // Meisterschmiede Tier 1 (ab Level 50)
        new() { Id = "r_fittings", NameKey = "CraftFittings", WorkshopType = WorkshopType.MasterSmith,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "fittings", DurationSeconds = 30 },

        // Innovationslabor Tier 1 (ab Level 50)
        new() { Id = "r_prototype", NameKey = "CraftPrototype", WorkshopType = WorkshopType.InnovationLab,
            RequiredWorkshopLevel = 50, Tier = 1, OutputProductId = "prototype", DurationSeconds = 30 },
    ];

    public static List<CraftingRecipe> GetAllRecipes() => AllRecipes;

    // Gecachte Lookups (einmalig erstellt, O(1) statt O(n) pro Suche)
    private static readonly Dictionary<string, CraftingRecipe> RecipeById =
        AllRecipes.ToDictionary(r => r.Id, r => r);
    private static readonly Dictionary<string, CraftingRecipe> RecipeByOutputProduct =
        AllRecipes.ToDictionary(r => r.OutputProductId, r => r);

    /// <summary>
    /// Findet ein Rezept anhand der ID (O(1) Dictionary-Lookup).
    /// </summary>
    public static CraftingRecipe? GetById(string recipeId) =>
        RecipeById.GetValueOrDefault(recipeId);

    /// <summary>
    /// Findet das Rezept das ein bestimmtes Produkt herstellt (O(1) Dictionary-Lookup).
    /// </summary>
    public static CraftingRecipe? GetByOutputProduct(string productId) =>
        RecipeByOutputProduct.GetValueOrDefault(productId);
}

/// <summary>
/// Ein Produkt das hergestellt werden kann.
/// </summary>
public class CraftingProduct
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public int Tier { get; set; }
    public decimal BaseValue { get; set; }

    private static readonly Dictionary<string, CraftingProduct> AllProducts = new()
    {
            ["planks"] = new() { Id = "planks", NameKey = "ProductPlanks", Tier = 1, BaseValue = 500m },
            ["furniture"] = new() { Id = "furniture", NameKey = "ProductFurniture", Tier = 2, BaseValue = 2500m },
            ["luxury_furniture"] = new() { Id = "luxury_furniture", NameKey = "ProductLuxuryFurniture", Tier = 3, BaseValue = 50000m },
            ["pipes"] = new() { Id = "pipes", NameKey = "ProductPipes", Tier = 1, BaseValue = 500m },
            ["plumbing_system"] = new() { Id = "plumbing_system", NameKey = "ProductPlumbing", Tier = 2, BaseValue = 2500m },
            ["bathroom_installation"] = new() { Id = "bathroom_installation", NameKey = "ProductBathroom", Tier = 3, BaseValue = 50000m },
            ["cables"] = new() { Id = "cables", NameKey = "ProductCables", Tier = 1, BaseValue = 500m },
            ["circuit"] = new() { Id = "circuit", NameKey = "ProductCircuit", Tier = 2, BaseValue = 2500m },
            ["smart_home"] = new() { Id = "smart_home", NameKey = "ProductSmartHome", Tier = 3, BaseValue = 50000m },
            ["paint_mix"] = new() { Id = "paint_mix", NameKey = "ProductPaintMix", Tier = 1, BaseValue = 400m },
            ["wall_design"] = new() { Id = "wall_design", NameKey = "ProductWallDesign", Tier = 2, BaseValue = 2000m },
            ["roof_tiles"] = new() { Id = "roof_tiles", NameKey = "ProductRoofTiles", Tier = 1, BaseValue = 600m },
            ["roofing_system"] = new() { Id = "roofing_system", NameKey = "ProductRoofing", Tier = 2, BaseValue = 3000m },
            // Neue Tier-3 Produkte (Maler + Dachdecker)
            ["artwork"] = new() { Id = "artwork", NameKey = "ProductArtwork", Tier = 3, BaseValue = 40000m },
            ["roof_structure"] = new() { Id = "roof_structure", NameKey = "ProductRoofStructure", Tier = 3, BaseValue = 60000m },
            // Neue Tier-1 Produkte (5 fehlende Workshops)
            ["concrete"] = new() { Id = "concrete", NameKey = "ProductConcrete", Tier = 1, BaseValue = 800m },
            ["blueprint"] = new() { Id = "blueprint", NameKey = "ProductBlueprint", Tier = 1, BaseValue = 1000m },
            ["contract"] = new() { Id = "contract", NameKey = "ProductContract", Tier = 1, BaseValue = 1500m },
            ["fittings"] = new() { Id = "fittings", NameKey = "ProductFittings", Tier = 1, BaseValue = 1200m },
            ["prototype"] = new() { Id = "prototype", NameKey = "ProductPrototype", Tier = 1, BaseValue = 2000m },
    };

    public static Dictionary<string, CraftingProduct> GetAllProducts() => AllProducts;
}

/// <summary>
/// Ein aktiver Crafting-Auftrag.
/// </summary>
public class CraftingJob
{
    /// <summary>Eindeutige Job-ID (GUID). Verhindert Verwechslung bei mehreren Jobs desselben Rezepts.</summary>
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("recipeId")]
    public string RecipeId { get; set; } = "";

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("durationSeconds")]
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
