using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Service für Schulden-Tracking und Tilgungsplanung.
/// </summary>
public interface IDebtService
{
    Task InitializeAsync();

    Task<DebtEntry> CreateDebtAsync(DebtEntry debt);
    Task<bool> UpdateDebtAsync(DebtEntry debt);
    Task<bool> DeleteDebtAsync(string id);
    Task<DebtEntry?> GetDebtAsync(string id);
    Task<IReadOnlyList<DebtEntry>> GetAllDebtsAsync();

    /// <summary>Zahlung auf eine Schuld buchen (reduziert RemainingAmount).</summary>
    Task<bool> MakePaymentAsync(string debtId, double amount);

    /// <summary>Gesamte Schuldensumme berechnen.</summary>
    Task<double> GetTotalDebtAsync();

    /// <summary>Schuld als abbezahlt markieren.</summary>
    Task<bool> PayOffDebtAsync(string debtId);

    // Daten-Export/Import
    Task<string> ExportToJsonAsync();
    Task<int> ImportFromJsonAsync(string json, bool merge = false);
}
