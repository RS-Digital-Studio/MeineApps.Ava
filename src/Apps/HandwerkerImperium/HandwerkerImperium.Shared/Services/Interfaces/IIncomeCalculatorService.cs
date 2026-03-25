using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentrale Einkommens- und Kostenberechnung.
/// Eliminiert die duplizierte Logik zwischen GameLoopService und OfflineProgressService.
/// </summary>
public interface IIncomeCalculatorService
{
    /// <summary>
    /// Berechnet das Brutto-Einkommen pro Sekunde inkl. aller Modifikatoren
    /// (Prestige-Shop, Research, Events, MasterTools, Gilden, VIP, Soft-Cap).
    /// </summary>
    /// <param name="state">Aktueller Spielstand.</param>
    /// <param name="prestigeIncomeBonus">Prestige-Shop Income-Bonus (gecacht oder frisch berechnet).</param>
    /// <param name="masterToolBonus">MasterTool-Einkommensbonus (gecacht oder frisch berechnet). -1 = frisch berechnen.</param>
    /// <returns>Berechnetes Brutto-Einkommen pro Sekunde nach allen Modifikatoren.</returns>
    decimal CalculateGrossIncome(GameState state, decimal prestigeIncomeBonus, decimal masterToolBonus = -1m);

    /// <summary>
    /// Berechnet die laufenden Kosten pro Sekunde inkl. aller Reduktionen
    /// (Prestige-Shop, Research, Storage-Gebäude, Gilden, Events).
    /// </summary>
    decimal CalculateCosts(GameState state);

    /// <summary>
    /// Wendet den Soft-Cap auf den Brutto-Einkommensmultiplikator an.
    /// Diminishing Returns ab 8.0x.
    /// </summary>
    decimal ApplySoftCap(GameState state, decimal grossIncome);

    /// <summary>
    /// Berechnet den Gesamt-Multiplikator für Crafting-Verkaufspreise.
    /// Wendet alle Einkommens-Boni an (Prestige, Research, Events, MasterTools,
    /// Gilden, VIP, Rebirth, Premium) OHNE Soft-Cap und OHNE Speed/Rush.
    /// </summary>
    /// <param name="state">Aktueller Spielstand.</param>
    /// <param name="prestigeIncomeBonus">Prestige-Shop Income-Bonus.</param>
    /// <param name="rebirthIncomeBonus">Rebirth-Einkommensbonus des Workshops (0-1.5).</param>
    /// <param name="masterToolBonus">MasterTool-Bonus. -1 = frisch berechnen.</param>
    decimal CalculateCraftingSellMultiplier(GameState state, decimal prestigeIncomeBonus, decimal rebirthIncomeBonus, decimal masterToolBonus = -1m);
}
