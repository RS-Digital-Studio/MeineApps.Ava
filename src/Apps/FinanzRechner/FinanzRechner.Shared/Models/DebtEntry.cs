using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Schulden-Eintrag für den Schulden-Tracker.
/// Verfolgt Kredite, Darlehen und andere Schulden mit Zinssatz und Tilgungsplan.
/// </summary>
public class DebtEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>Ursprünglicher Schuldenbetrag.</summary>
    public double OriginalAmount { get; set; }

    /// <summary>Aktuell verbleibender Betrag.</summary>
    public double RemainingAmount { get; set; }

    /// <summary>Jährlicher Zinssatz in Prozent.</summary>
    public double InterestRate { get; set; }

    /// <summary>Monatliche Rate.</summary>
    public double MonthlyPayment { get; set; }

    /// <summary>Startdatum der Schuld.</summary>
    public DateTime StartDate { get; set; } = DateTime.Today;

    /// <summary>Geplantes Tilgungsdatum (berechnet oder manuell).</summary>
    public DateTime? TargetPayoffDate { get; set; }

    /// <summary>Optionale Notiz.</summary>
    public string? Note { get; set; }

    /// <summary>Emoji-Icon für die Darstellung.</summary>
    public string Icon { get; set; } = "\U0001F4B3"; // Kreditkarte

    /// <summary>Farbe als Hex-String.</summary>
    public string ColorHex { get; set; } = "#EF4444";

    /// <summary>Ob die Schuld noch aktiv ist.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Berechnete Eigenschaften

    /// <summary>Bereits getilgter Betrag.</summary>
    public double PaidAmount => OriginalAmount - RemainingAmount;

    /// <summary>Fortschritt der Tilgung in Prozent.</summary>
    public double PayoffPercent => OriginalAmount > 0
        ? Math.Min((PaidAmount / OriginalAmount) * 100, 100) : 0;

    /// <summary>
    /// Geschätzte Restlaufzeit in Monaten.
    /// Analytische Formel: n = -ln(1 - r*P/M) / ln(1+r)
    /// wobei r=Monatszins, P=Restbetrag, M=Rate.
    /// </summary>
    public int? EstimatedMonthsRemaining
    {
        get
        {
            if (MonthlyPayment <= 0 || RemainingAmount <= 0) return null;
            if (InterestRate <= 0)
                return (int)Math.Ceiling(RemainingAmount / MonthlyPayment);

            var monthlyRate = InterestRate / 100.0 / 12.0;

            // Rate muss die monatlichen Zinsen übersteigen
            if (MonthlyPayment <= RemainingAmount * monthlyRate)
                return null;

            // Analytische Formel statt iterativer Schleife (O(1) statt O(n))
            var months = -Math.Log(1 - monthlyRate * RemainingAmount / MonthlyPayment)
                         / Math.Log(1 + monthlyRate);
            return (int)Math.Ceiling(months);
        }
    }

    /// <summary>Geschätztes Tilgungsdatum basierend auf aktueller Rate.</summary>
    public DateTime? EstimatedPayoffDate =>
        EstimatedMonthsRemaining.HasValue
            ? DateTime.Today.AddMonths(EstimatedMonthsRemaining.Value)
            : null;

    /// <summary>Gesamte geschätzte Zinskosten über die Restlaufzeit.</summary>
    public double? TotalInterestRemaining
    {
        get
        {
            if (!EstimatedMonthsRemaining.HasValue) return null;
            return (MonthlyPayment * EstimatedMonthsRemaining.Value) - RemainingAmount;
        }
    }

    public string OriginalDisplay => CurrencyHelper.Format(OriginalAmount);
    public string RemainingDisplay => CurrencyHelper.Format(RemainingAmount);
    public string PaidDisplay => CurrencyHelper.Format(PaidAmount);
    public string MonthlyPaymentDisplay => CurrencyHelper.Format(MonthlyPayment);
    public string PayoffPercentDisplay => $"{PayoffPercent:F0}%";
}
