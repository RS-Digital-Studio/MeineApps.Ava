namespace WorkTimePro.Services;

/// <summary>
/// Interface für haptisches Feedback bei CheckIn/CheckOut/Pause-Aktionen.
/// </summary>
public interface IHapticService
{
    /// <summary>Mittleres Feedback (CheckIn/CheckOut, Pause Start/Ende).</summary>
    void Click();

    /// <summary>Stärkeres Feedback (Feierabend-Celebration).</summary>
    void HeavyClick();
}

/// <summary>
/// Desktop-Implementierung: Kein Haptic Feedback verfügbar.
/// </summary>
public class NoOpHapticService : IHapticService
{
    public void Click() { }
    public void HeavyClick() { }
}
