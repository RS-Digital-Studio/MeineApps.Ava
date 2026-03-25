using System.Text.Json;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.Services;

/// <summary>
/// Materialpreis-Datenbank mit ~40 regionalen Durchschnittspreisen (Deutschland).
/// Benutzerdefinierte Überschreibungen werden als JSON persistiert.
/// </summary>
public sealed class MaterialPriceService : IMaterialPriceService
{
    private readonly string _pricesFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<MaterialPrice> _prices;
    private bool _customLoaded;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MaterialPriceService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeineApps", "HandwerkerRechner");
        Directory.CreateDirectory(appDataPath);
        _pricesFilePath = Path.Combine(appDataPath, "material_prices.json");
        _prices = CreateDefaultPrices();
    }

    public MaterialPrice? GetPrice(string key)
    {
        EnsureCustomLoaded();
        return _prices.Find(p => p.Key == key);
    }

    public List<MaterialPrice> GetAllPrices()
    {
        EnsureCustomLoaded();
        return [.. _prices];
    }

    public List<MaterialPrice> GetPricesByCategory(string category)
    {
        EnsureCustomLoaded();
        return _prices.Where(p => p.Category == category).ToList();
    }

    public async Task SetCustomPriceAsync(string key, double price)
    {
        var material = _prices.Find(p => p.Key == key);
        if (material == null) return;

        material.CustomPrice = price;
        await SaveCustomPricesAsync();
    }

    public async Task ResetToDefaultAsync(string key)
    {
        var material = _prices.Find(p => p.Key == key);
        if (material == null) return;

        material.CustomPrice = -1;
        await SaveCustomPricesAsync();
    }

    public async Task ResetAllToDefaultAsync()
    {
        foreach (var p in _prices)
            p.CustomPrice = -1;
        await SaveCustomPricesAsync();
    }

    private void EnsureCustomLoaded()
    {
        if (_customLoaded) return;
        _semaphore.Wait();
        try
        {
            if (_customLoaded) return;
            LoadCustomPrices();
            _customLoaded = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void LoadCustomPrices()
    {
        if (!File.Exists(_pricesFilePath)) return;
        try
        {
            var json = File.ReadAllText(_pricesFilePath);
            var overrides = JsonSerializer.Deserialize<Dictionary<string, double>>(json);
            if (overrides == null) return;

            foreach (var (key, price) in overrides)
            {
                var material = _prices.Find(p => p.Key == key);
                if (material != null)
                    material.CustomPrice = price;
            }
        }
        catch
        {
            // Fehler beim Laden - Standardpreise verwenden
        }
    }

    private async Task SaveCustomPricesAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            // Nur überschriebene Preise speichern
            var overrides = new Dictionary<string, double>();
            foreach (var p in _prices.Where(p => p.CustomPrice >= 0))
                overrides[p.Key] = p.CustomPrice;

            var json = JsonSerializer.Serialize(overrides, JsonOptions);
            await File.WriteAllTextAsync(_pricesFilePath, json);
        }
        catch
        {
            // Fehler beim Speichern
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Erstellt die Standard-Preisliste mit ~40 regionalen Durchschnittspreisen (Deutschland, 2026).
    /// </summary>
    private static List<MaterialPrice> CreateDefaultPrices() =>
    [
        // Fliesen & Boden
        new() { Key = "tile_standard", NameKey = "PriceTileStandard", Unit = "€/m²", DefaultPrice = 25.0, Category = "flooring" },
        new() { Key = "tile_premium", NameKey = "PriceTilePremium", Unit = "€/m²", DefaultPrice = 55.0, Category = "flooring" },
        new() { Key = "flooring_laminate", NameKey = "PriceFlooringLaminate", Unit = "€/m²", DefaultPrice = 18.0, Category = "flooring" },
        new() { Key = "flooring_parquet", NameKey = "PriceFlooringParquet", Unit = "€/m²", DefaultPrice = 45.0, Category = "flooring" },
        new() { Key = "grout_standard", NameKey = "PriceGroutStandard", Unit = "€/kg", DefaultPrice = 2.50, Category = "flooring" },

        // Farbe & Tapete
        new() { Key = "paint_standard", NameKey = "PricePaintStandard", Unit = "€/l", DefaultPrice = 12.0, Category = "wall" },
        new() { Key = "paint_premium", NameKey = "PricePaintPremium", Unit = "€/l", DefaultPrice = 22.0, Category = "wall" },
        new() { Key = "wallpaper_standard", NameKey = "PriceWallpaperStandard", Unit = "€/Rolle", DefaultPrice = 8.0, Category = "wall" },
        new() { Key = "wallpaper_premium", NameKey = "PriceWallpaperPremium", Unit = "€/Rolle", DefaultPrice = 18.0, Category = "wall" },
        new() { Key = "plaster_sack", NameKey = "PricePlasterSack", Unit = "€/30kg", DefaultPrice = 9.0, Category = "wall" },

        // Trockenbau
        new() { Key = "drywall_plate", NameKey = "PriceDrywallPlate", Unit = "€/m²", DefaultPrice = 8.0, Category = "drywall" },
        new() { Key = "drywall_cw_profile", NameKey = "PriceDrywallCW", Unit = "€/m", DefaultPrice = 3.50, Category = "drywall" },
        new() { Key = "drywall_uw_profile", NameKey = "PriceDrywallUW", Unit = "€/m", DefaultPrice = 2.80, Category = "drywall" },
        new() { Key = "drywall_screws", NameKey = "PriceDrywallScrews", Unit = "€/100Stk", DefaultPrice = 4.50, Category = "drywall" },

        // Elektrik
        new() { Key = "cable_1_5mm", NameKey = "PriceCable15", Unit = "€/m", DefaultPrice = 1.20, Category = "electrical" },
        new() { Key = "cable_2_5mm", NameKey = "PriceCable25", Unit = "€/m", DefaultPrice = 1.80, Category = "electrical" },
        new() { Key = "cable_4mm", NameKey = "PriceCable4", Unit = "€/m", DefaultPrice = 2.80, Category = "electrical" },
        new() { Key = "cable_6mm", NameKey = "PriceCable6", Unit = "€/m", DefaultPrice = 4.20, Category = "electrical" },

        // Beton & Estrich
        new() { Key = "concrete_sack_25kg", NameKey = "PriceConcreteSack25", Unit = "€/25kg", DefaultPrice = 4.50, Category = "concrete" },
        new() { Key = "concrete_sack_40kg", NameKey = "PriceConcreteSack40", Unit = "€/40kg", DefaultPrice = 6.50, Category = "concrete" },
        new() { Key = "screed_sack", NameKey = "PriceScreedSack", Unit = "€/40kg", DefaultPrice = 8.0, Category = "concrete" },

        // Dämmung
        new() { Key = "insulation_eps", NameKey = "PriceInsulationEPS", Unit = "€/m²", DefaultPrice = 8.0, Category = "insulation" },
        new() { Key = "insulation_xps", NameKey = "PriceInsulationXPS", Unit = "€/m²", DefaultPrice = 15.0, Category = "insulation" },
        new() { Key = "insulation_mineral", NameKey = "PriceInsulationMineral", Unit = "€/m²", DefaultPrice = 12.0, Category = "insulation" },
        new() { Key = "insulation_woodfiber", NameKey = "PriceInsulationWoodfiber", Unit = "€/m²", DefaultPrice = 20.0, Category = "insulation" },

        // Dach
        new() { Key = "roof_tile_standard", NameKey = "PriceRoofTile", Unit = "€/Stk", DefaultPrice = 1.20, Category = "roof" },
        new() { Key = "solar_panel_400w", NameKey = "PriceSolarPanel", Unit = "€/Stk", DefaultPrice = 250.0, Category = "roof" },

        // Garten
        new() { Key = "paving_standard", NameKey = "PricePavingStandard", Unit = "€/m²", DefaultPrice = 20.0, Category = "garden" },
        new() { Key = "soil_bag", NameKey = "PriceSoilBag", Unit = "€/40l", DefaultPrice = 5.0, Category = "garden" },
        new() { Key = "mulch_bag", NameKey = "PriceMulchBag", Unit = "€/50l", DefaultPrice = 6.50, Category = "garden" },
        new() { Key = "pond_liner", NameKey = "PricePondLiner", Unit = "€/m²", DefaultPrice = 8.0, Category = "garden" },

        // Metall
        new() { Key = "steel_flat", NameKey = "PriceSteelFlat", Unit = "€/kg", DefaultPrice = 2.50, Category = "metal" },
        new() { Key = "steel_round", NameKey = "PriceSteelRound", Unit = "€/kg", DefaultPrice = 3.00, Category = "metal" },
        new() { Key = "aluminum_flat", NameKey = "PriceAluminumFlat", Unit = "€/kg", DefaultPrice = 8.00, Category = "metal" },
        new() { Key = "stainless_flat", NameKey = "PriceStainlessFlat", Unit = "€/kg", DefaultPrice = 6.50, Category = "metal" },

        // Treppen
        new() { Key = "stair_step_wood", NameKey = "PriceStairStepWood", Unit = "€/Stufe", DefaultPrice = 45.0, Category = "stairs" },
        new() { Key = "stair_railing_m", NameKey = "PriceStairRailing", Unit = "€/m", DefaultPrice = 85.0, Category = "stairs" },

        // Arbeit (für Stundenrechner)
        new() { Key = "labor_helper", NameKey = "PriceLaborHelper", Unit = "€/h", DefaultPrice = 25.0, Category = "labor" },
        new() { Key = "labor_skilled", NameKey = "PriceLaborSkilled", Unit = "€/h", DefaultPrice = 45.0, Category = "labor" },
        new() { Key = "labor_master", NameKey = "PriceLaborMaster", Unit = "€/h", DefaultPrice = 65.0, Category = "labor" },
    ];
}
