namespace FinanzRechner.Models;

/// <summary>
/// Finanzielle Gesundheitsbewertung (0-100 Punkte).
/// Bewertet Budget-Einhaltung, Sparquote, Schuldenstand und Regelmäßigkeit.
/// </summary>
public class FinancialScore
{
    /// <summary>Gesamtpunktzahl (0-100).</summary>
    public int Score { get; set; }

    /// <summary>Note (A+ bis F).</summary>
    public string Grade { get; set; } = "F";

    /// <summary>Farbe als Hex-String basierend auf der Note.</summary>
    public string GradeColorHex { get; set; } = "#EF4444";

    /// <summary>Einzelne Bewertungsfaktoren.</summary>
    public List<ScoreFactor> Factors { get; set; } = [];

    /// <summary>Personalisierte Tipps zur Verbesserung.</summary>
    public List<string> Tips { get; set; } = [];

    /// <summary>Trend: Veränderung zum Vormonat.</summary>
    public int? TrendFromLastMonth { get; set; }

    /// <summary>Ob sich der Score verbessert hat.</summary>
    public bool IsImproving => TrendFromLastMonth.HasValue && TrendFromLastMonth.Value > 0;

    /// <summary>Berechnet Note und Farbe aus dem Score.</summary>
    public static (string Grade, string ColorHex) GetGradeFromScore(int score) => score switch
    {
        >= 90 => ("A+", "#10B981"), // Smaragd
        >= 80 => ("A", "#22C55E"),  // Grün
        >= 70 => ("B+", "#84CC16"), // Lime
        >= 60 => ("B", "#EAB308"),  // Gelb
        >= 50 => ("C", "#F97316"),  // Orange
        >= 40 => ("D", "#EF4444"), // Rot
        _ => ("F", "#DC2626")       // Dunkelrot
    };
}

/// <summary>
/// Einzelner Bewertungsfaktor des Finanz-Scores.
/// </summary>
public record ScoreFactor(
    string Name,
    string Description,
    int Points,
    int MaxPoints)
{
    /// <summary>Prozent der erreichten Punkte in diesem Faktor.</summary>
    public double Percent => MaxPoints > 0 ? (double)Points / MaxPoints * 100 : 0;

    /// <summary>Farbe basierend auf dem Prozentsatz.</summary>
    public string ColorHex => Percent switch
    {
        >= 80 => "#10B981",
        >= 60 => "#EAB308",
        >= 40 => "#F97316",
        _ => "#EF4444"
    };
}

/// <summary>
/// Finanzielle Prognose: Hochrechnung basierend auf aktuellem Verhalten.
/// </summary>
public class FinancialForecast
{
    /// <summary>Prognostiziertes Monatsende-Saldo.</summary>
    public double ProjectedEndOfMonthBalance { get; set; }

    /// <summary>Prognostizierte Monatsausgaben (basierend auf aktuellem Tempo).</summary>
    public double ProjectedMonthlyExpenses { get; set; }

    /// <summary>Prognostizierte Monatseinnahmen.</summary>
    public double ProjectedMonthlyIncome { get; set; }

    /// <summary>Durchschnittliche tägliche Ausgaben.</summary>
    public double AverageDailyExpense { get; set; }

    /// <summary>Verbleibende Tage im Monat.</summary>
    public int RemainingDaysInMonth { get; set; }

    /// <summary>Tägliches Budget um Ausgaben-Ziel einzuhalten.</summary>
    public double? DailyBudgetRemaining { get; set; }

    /// <summary>Ob die Prognose positiv ist (Einnahmen > Ausgaben).</summary>
    public bool IsPositive => ProjectedEndOfMonthBalance >= 0;

    /// <summary>Datenpunkte für Trend-Chart (Tag → kumulierte Ausgaben).</summary>
    public List<(int Day, double CumulativeExpenses)> ExpenseTrend { get; set; } = [];

    /// <summary>Datenpunkte für Prognose-Linie (ab heute bis Monatsende).</summary>
    public List<(int Day, double ProjectedCumulative)> ForecastLine { get; set; } = [];
}
