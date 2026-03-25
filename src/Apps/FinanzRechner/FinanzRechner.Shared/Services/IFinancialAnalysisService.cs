using FinanzRechner.Models;

namespace FinanzRechner.Services;

/// <summary>
/// Service für Finanzanalysen: Score, Monatsvergleich, Prognose.
/// Reine Berechnungs-Logik ohne eigene Persistenz.
/// </summary>
public interface IFinancialAnalysisService
{
    /// <summary>Berechnet den Finanz-Gesundheits-Score (0-100).</summary>
    Task<FinancialScore> CalculateScoreAsync();

    /// <summary>Vergleicht den aktuellen Monat mit dem Vormonat.</summary>
    Task<MonthComparison> GetMonthComparisonAsync(int year, int month);

    /// <summary>Erstellt eine Ausgaben-Prognose für den aktuellen Monat.</summary>
    Task<FinancialForecast> GetForecastAsync();

    /// <summary>Berechnet das Nettovermögen (Assets - Schulden).</summary>
    Task<double> CalculateNetWorthAsync();

    /// <summary>
    /// Gebündeltes Laden aller Dashboard-Insights in einem Durchgang.
    /// Vermeidet redundante Service-Aufrufe (Score, Forecast, NetWorth).
    /// </summary>
    Task<FinancialInsightsBundle> GetAllInsightsAsync();
}

/// <summary>
/// Gebündeltes Ergebnis aller Dashboard-Insights.
/// Wird in einem einzigen Durchgang berechnet um redundante Daten-Abfragen zu vermeiden.
/// </summary>
public record FinancialInsightsBundle(
    FinancialScore Score,
    FinancialForecast Forecast,
    double NetWorth);
