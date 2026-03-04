namespace MeineApps.Core.Ava.Services;

/// <summary>Haptisches Feedback (Vibration) - Plattform-Abstraktion.</summary>
public interface IHapticService
{
    /// <summary>Haptic Feedback aktiviert/deaktiviert.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Kurzes leichtes Feedback (Ziffern, Tab-Wechsel).</summary>
    void Tick();

    /// <summary>Mittleres Feedback (Speichern, CheckIn/CheckOut).</summary>
    void Click();

    /// <summary>Stärkeres Feedback (Berechnung, Achievement, Alarm-Dismiss).</summary>
    void HeavyClick();
}

/// <summary>No-Op für Desktop-Plattformen ohne Vibrations-Hardware.</summary>
public sealed class NoOpHapticService : IHapticService
{
    public bool IsEnabled { get; set; } = true;
    public void Tick() { }
    public void Click() { }
    public void HeavyClick() { }
}
