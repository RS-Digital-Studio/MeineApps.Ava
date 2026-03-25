using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für das Workshop-Rebirth-System.
/// Workshop kann bei Level 1000 wiedergeboren werden: Level wird auf 1 zurückgesetzt, +1 permanenter Stern.
/// Sterne überleben Prestige + Ascension (permanentester Fortschritt im Spiel).
/// </summary>
public interface IRebirthService
{
    /// <summary>Ob der Workshop wiedergeboren werden kann (Level 1000, Sterne kleiner 5).</summary>
    bool CanRebirth(WorkshopType type);

    /// <summary>Kosten für den nächsten Rebirth (Goldschrauben + Geld in Prozent des aktuellen Geldes).</summary>
    (int goldenScrews, decimal moneyPercent) GetRebirthCost(WorkshopType type);

    /// <summary>Führt Rebirth durch. Gibt false zurück wenn Voraussetzungen nicht erfüllt oder nicht genug Ressourcen.</summary>
    bool DoRebirth(WorkshopType type);

    /// <summary>Aktuelle Stern-Anzahl eines Workshops (0-5).</summary>
    int GetStars(WorkshopType type);

    /// <summary>
    /// Wendet die gespeicherten Sterne auf alle Workshop-Instanzen an.
    /// Muss nach State-Load und nach Prestige aufgerufen werden.
    /// </summary>
    void ApplyStarsToWorkshops();

    /// <summary>Wird nach erfolgreichem Rebirth gefeuert.</summary>
    event EventHandler<WorkshopType>? RebirthCompleted;
}
