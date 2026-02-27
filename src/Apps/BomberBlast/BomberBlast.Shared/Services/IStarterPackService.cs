namespace BomberBlast.Services;

/// <summary>
/// Service für das einmalige Starterpaket-Angebot nach Level 5.
/// Enthält 2500 Coins + 10 Gems + 2 Rare-Karten für 1999 Coins.
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
