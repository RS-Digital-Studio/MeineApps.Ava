namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Long-Term-Engagement post-Lv1000 (12.05.2026).
///
/// Eternal Mastery System: dauerhafter Einkommens-Bonus der mit jedem abgeschlossenen
/// Prestige skaliert. Kein Cap, kein Reset bei Ascension — der Bonus akkumuliert ueber
/// die gesamte Account-Lebenszeit. Sichtbare Meilensteine bei jedem 5. + 10. Prestige.
///
/// Berechnung (vgl. GameBalanceConstants.EternalMastery*):
///   - Linear: 0.5% pro Prestige
///   - +5er-Stufen: 2.5% alle 5 Prestiges
///   - +10er-Mega-Stufen: 5% alle 10 Prestiges
/// </summary>
public interface IEternalMasteryService
{
    /// <summary>Aktueller permanenter Einkommens-Bonus (0.0 = +0%, 1.0 = +100%).</summary>
    decimal IncomeBonus { get; }

    /// <summary>Anzahl absolvierter Prestiges (Quelle der Wahrheit fuer Bonus-Berechnung).</summary>
    int CompletedPrestiges { get; }

    /// <summary>Anzahl der Prestiges bis zur naechsten 5er-Stufe (0 = gerade Stufe erreicht).</summary>
    int PrestigesUntilNextTier { get; }

    /// <summary>Anzahl der Prestiges bis zur naechsten 10er-Mega-Stufe.</summary>
    int PrestigesUntilNextMegaTier { get; }

    /// <summary>Anzeige-Text z.B. "+15.5% Eternal Mastery".</summary>
    string DisplayText { get; }

    /// <summary>True wenn mindestens 1 Prestige abgeschlossen (Bonus &gt; 0).</summary>
    bool IsActive { get; }

    /// <summary>Bonus-Berechnung — wird auch vom Income-Calculator genutzt.</summary>
    decimal CalculateBonus(int completedPrestiges);
}
