namespace BomberBlast.Services;

/// <summary>
/// Verwaltet die Coin-Waehrung des Spielers
/// </summary>
public interface ICoinService
{
    /// <summary>Aktueller Coin-Stand</summary>
    int Balance { get; }

    /// <summary>Insgesamt verdiente Coins (Lifetime)</summary>
    int TotalEarned { get; }

    /// <summary>Coins hinzufuegen</summary>
    void AddCoins(int amount);

    /// <summary>Coins ausgeben (gibt false zurueck wenn nicht genug)</summary>
    bool TrySpendCoins(int amount);

    /// <summary>Pruefen ob genug Coins vorhanden</summary>
    bool CanAfford(int amount);

    /// <summary>Coin-Stand hat sich geaendert</summary>
    event EventHandler? BalanceChanged;

    /// <summary>Ob der tägliche Bonus verfügbar ist</summary>
    bool IsDailyBonusAvailable { get; }

    /// <summary>Täglichen Bonus beanspruchen (500 Coins). Gibt true zurück wenn erfolgreich.</summary>
    bool TryClaimDailyBonus();

    /// <summary>Betrag des täglichen Bonus</summary>
    int DailyBonusAmount { get; }
}
