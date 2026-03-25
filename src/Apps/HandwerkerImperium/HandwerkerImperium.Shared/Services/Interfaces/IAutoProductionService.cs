using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für die automatische Produktion von Crafting-Items in Workshops.
/// Workshops produzieren passiv Tier-1 Items basierend auf arbeitenden Workern.
/// </summary>
public interface IAutoProductionService
{
    /// <summary>
    /// Produziert Items für alle qualifizierten Workshops.
    /// Wird periodisch vom GameLoopService aufgerufen.
    /// </summary>
    void ProduceForAllWorkshops(GameState state);

    /// <summary>
    /// Gibt das Produktionsintervall in Sekunden für einen Workshop-Typ zurück.
    /// Standard: 180s, InnovationLab: 120s, MasterSmith: 60s.
    /// </summary>
    int GetProductionInterval(WorkshopType type);

    /// <summary>
    /// Gibt die Tier-1 Produkt-ID für einen Workshop-Typ zurück.
    /// Null wenn der Typ kein Auto-Produkt hat.
    /// </summary>
    string? GetTier1ProductId(WorkshopType type);

    /// <summary>
    /// Prüft ob Auto-Produktion für einen Workshop freigeschaltet ist (Level >= 50).
    /// </summary>
    bool IsAutoProductionUnlocked(Workshop workshop);

    /// <summary>
    /// Berechnet die Items die während Offline-Zeit produziert wurden.
    /// Gibt ein Dictionary (productId → count) zurück.
    /// </summary>
    Dictionary<string, int> CalculateOfflineProduction(GameState state, double offlineSeconds);
}
