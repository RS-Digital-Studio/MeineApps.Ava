using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

/// <summary>
/// Sparziel mit Zielbetrag, aktuellem Stand und optionalem Deadline.
/// </summary>
public class SavingsGoal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>Zielbetrag.</summary>
    public double TargetAmount { get; set; }

    /// <summary>Aktuell angespartes Guthaben.</summary>
    public double CurrentAmount { get; set; }

    /// <summary>Optionales Zieldatum.</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Emoji-Icon für die Darstellung.</summary>
    public string Icon { get; set; } = "\U0001F3AF"; // Zielscheibe

    /// <summary>Farbe als Hex-String.</summary>
    public string ColorHex { get; set; } = "#10B981";

    /// <summary>Ob das Ziel erreicht wurde.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Optionale Notiz.</summary>
    public string? Note { get; set; }

    /// <summary>Optionales zugeordnetes Konto (für automatische Berechnung).</summary>
    public string? LinkedAccountId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Berechnete Eigenschaften

    /// <summary>Fortschritt in Prozent (0-100+).</summary>
    public double ProgressPercent => TargetAmount > 0 ? Math.Min((CurrentAmount / TargetAmount) * 100, 100) : 0;

    /// <summary>Verbleibender Betrag bis zum Ziel.</summary>
    public double RemainingAmount => Math.Max(TargetAmount - CurrentAmount, 0);

    /// <summary>Verbleibende Tage bis zum Deadline (null wenn kein Deadline).</summary>
    public int? DaysRemaining => Deadline.HasValue
        ? Math.Max((int)(Deadline.Value.Date - DateTime.Today).TotalDays, 0)
        : null;

    /// <summary>Benötigter monatlicher Sparbetrag um das Ziel zu erreichen.</summary>
    public double? RequiredMonthlySaving
    {
        get
        {
            if (!Deadline.HasValue || RemainingAmount <= 0) return null;
            var monthsLeft = Math.Max(((Deadline.Value.Year - DateTime.Today.Year) * 12)
                + Deadline.Value.Month - DateTime.Today.Month, 1);
            return RemainingAmount / monthsLeft;
        }
    }

    public string TargetDisplay => CurrencyHelper.Format(TargetAmount);
    public string CurrentDisplay => CurrencyHelper.Format(CurrentAmount);
    public string RemainingDisplay => CurrencyHelper.Format(RemainingAmount);
    public string ProgressDisplay => $"{ProgressPercent:F0}%";
}
