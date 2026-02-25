using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Verwaltet rotierende Tages- und Wochenangebote im Shop.
/// 3 Tagesdeals (wechseln täglich) + 1 Wochendeal (wechselt wöchentlich).
/// </summary>
public interface IRotatingDealsService
{
    /// <summary>Gibt die 3 Tagesangebote zurück</summary>
    List<RotatingDeal> GetTodaysDeals();

    /// <summary>Gibt das aktuelle Wochenangebot zurück</summary>
    RotatingDeal? GetWeeklyDeal();

    /// <summary>Deal einlösen (kaufen). Gibt true bei Erfolg zurück</summary>
    bool ClaimDeal(string dealId);
}
