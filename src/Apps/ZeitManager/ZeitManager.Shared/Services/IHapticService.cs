namespace ZeitManager.Services;

/// <summary>
/// Interface fuer haptisches Feedback bei Timer-Ende, Alarm-Dismiss und Alarm-Snooze.
/// </summary>
public interface IHapticService
{
    /// <summary>Mittleres Feedback (Alarm-Snooze, Timer-Pause).</summary>
    void Click();

    /// <summary>Staerkeres Feedback (Alarm-Dismiss, Timer-Ende).</summary>
    void HeavyClick();
}

/// <summary>
/// Desktop-Implementierung: Kein Haptic Feedback verfuegbar.
/// </summary>
public class NoOpHapticService : IHapticService
{
    public void Click() { }
    public void HeavyClick() { }
}
