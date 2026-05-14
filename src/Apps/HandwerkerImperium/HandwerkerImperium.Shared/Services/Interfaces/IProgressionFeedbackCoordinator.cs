namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bündelt die Progression-Feedback-Handler (Level-Up, Prestige, Workshop-Upgrade,
/// Worker-Events, Master-Tools, Achievements) aus MainViewModel.EventHandlers.cs.
/// Subscribed selbst auf die Service-Events und feuert UI-Effekte über den IUiEffectBus.
/// Singleton im DI.
/// </summary>
public interface IProgressionFeedbackCoordinator
{
    /// <summary>Verbindet die schmale Host-Bruecke (MainViewModel) — einmalig im MainViewModel-Ctor.</summary>
    void AttachHost(IProgressionFeedbackHost host);

    /// <summary>Aktiviert die Event-Subscriptions. Idempotent — mehrfacher Aufruf ist sicher.</summary>
    void StartListening();

    /// <summary>
    /// Prüft ob der Play-Store-Review-Prompt gezeigt werden soll.
    /// Public, weil MainViewModel.OnOrderCompleted ihn ebenfalls aufruft.
    /// </summary>
    void CheckReviewPrompt();
}
