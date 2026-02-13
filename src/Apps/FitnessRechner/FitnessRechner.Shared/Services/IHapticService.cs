namespace FitnessRechner.Services;

/// <summary>
/// Interface für haptisches Feedback bei Benutzeraktionen.
/// </summary>
public interface IHapticService
{
    /// <summary>Haptic Feedback aktiviert/deaktiviert.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Kurzes leichtes Feedback (Quick-Add, Tab-Wechsel).</summary>
    void Tick();

    /// <summary>Mittleres Feedback (Eintrag gespeichert).</summary>
    void Click();

    /// <summary>Stärkeres Feedback (Achievement, Level-Up, Tagesziel).</summary>
    void HeavyClick();
}

/// <summary>
/// Desktop-Implementierung: Kein Haptic Feedback verfügbar.
/// </summary>
public class NoOpHapticService : IHapticService
{
    public bool IsEnabled { get; set; } = true;
    public void Tick() { }
    public void Click() { }
    public void HeavyClick() { }
}
