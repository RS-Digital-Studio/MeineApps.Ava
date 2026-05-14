using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Schmale Host-Facade für <see cref="ProgressionFeedbackCoordinator"/>. Kapselt die
/// MainViewModel-Zugriffe, die die Progression-Feedback-Handler noch brauchen —
/// EconomyVM-Refreshes, Property-Notifies und Dialog-State-Abfragen.
/// </summary>
public interface IProgressionFeedbackHost
{
    /// <summary>True während Hold-to-Upgrade — unterdrückt Hints/Dialoge/Zeremonien.</summary>
    bool IsHoldingUpgrade { get; }

    /// <summary>True wenn irgendein Overlay-Dialog sichtbar ist.</summary>
    bool IsAnyDialogVisible { get; }

    /// <summary>Aktualisiert alle Workshop-Karten.</summary>
    void RefreshWorkshops();

    /// <summary>Aktualisiert eine einzelne Workshop-Karte.</summary>
    void RefreshSingleWorkshop(WorkshopType type);

    /// <summary>Aktualisiert das Eternal-Mastery-Badge im Header.</summary>
    void RefreshEternalMastery();

    /// <summary>Feuert PropertyChanged für die Automation-Unlock-Properties (Level-Gate kann sich ändern).</summary>
    void NotifyAutomationUnlockChanged();

    /// <summary>Setzt die Sichtbarkeit des Tutorial-Hint-Pulses um die erste Workshop-Karte.</summary>
    void SetTutorialHintVisible(bool visible);
}
