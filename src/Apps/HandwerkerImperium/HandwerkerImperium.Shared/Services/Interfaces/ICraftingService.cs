using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für das Crafting-System (Produktionsketten mit Rezepten).
/// </summary>
public interface ICraftingService
{
    /// <summary>Feuert wenn sich der Crafting-Zustand ändert.</summary>
    event Action? CraftingUpdated;

    /// <summary>Feuert wenn ein fertiges Crafting-Produkt eingesammelt wird.</summary>
    event Action? CraftingProductCollected;

    /// <summary>Gibt verfügbare Rezepte für einen Workshop-Typ und Level zurück.</summary>
    List<CraftingRecipe> GetAvailableRecipes(WorkshopType workshopType, int workshopLevel);

    /// <summary>Startet einen Crafting-Auftrag. Gibt true bei Erfolg zurück.</summary>
    bool StartCrafting(string recipeId);

    /// <summary>Aktualisiert Timer und prüft fertige Aufträge.</summary>
    void UpdateTimers();

    /// <summary>Sammelt ein fertiges Produkt ein. Gibt true bei Erfolg zurück.</summary>
    bool CollectProduct(string jobId);

    /// <summary>Verkauft ein Produkt aus dem Inventar. Gibt true bei Erfolg zurück.</summary>
    bool SellProduct(string productId);

    /// <summary>Verkauft mehrere Einheiten eines Produkts. Gibt den Gesamterlös zurück.</summary>
    decimal SellProducts(string productId, int count);

    /// <summary>Berechnet den aktuellen Verkaufspreis eines Produkts (1 Stück) inkl. aller Multiplikatoren.</summary>
    decimal GetSellPrice(string productId);
}
