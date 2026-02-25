namespace BomberBlast.Services;

/// <summary>
/// Service für das einmalige Starterpaket-Angebot nach Level 5.
/// Enthält 5000 Coins + 20 Gems + 3 Rare-Karten für 0,99 EUR.
/// </summary>
public interface IStarterPackService
{
    /// <summary>Ob das Starterpaket verfügbar ist (Level >= 5 und noch nicht gekauft)</summary>
    bool IsAvailable { get; }

    /// <summary>Ob das Starterpaket bereits gekauft wurde</summary>
    bool IsAlreadyPurchased { get; }

    /// <summary>Markiert das Starterpaket als gekauft und vergibt die Belohnungen</summary>
    void MarkAsPurchased();

    /// <summary>Prüft die Berechtigung basierend auf dem aktuellen Level</summary>
    void CheckEligibility(int currentLevel);
}
