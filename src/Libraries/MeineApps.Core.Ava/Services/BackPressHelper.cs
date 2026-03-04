namespace MeineApps.Core.Ava.Services;

/// <summary>Double-Back-to-Exit Logik für Android</summary>
public class BackPressHelper
{
    private DateTime _lastPress = DateTime.MinValue;
    private const int IntervalMs = 2000;

    public event Action<string>? ExitHintRequested;

    /// <summary>Prüft Double-Back-to-Exit. Gibt false zurück wenn App beendet werden soll.</summary>
    public bool HandleDoubleBack(string exitMessage)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastPress).TotalMilliseconds < IntervalMs) return false;
        _lastPress = now;
        ExitHintRequested?.Invoke(exitMessage);
        return true;
    }
}
