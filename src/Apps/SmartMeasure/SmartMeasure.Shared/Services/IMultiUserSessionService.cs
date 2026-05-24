using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.11: Mehrere Geraete erfassen gleichzeitig dasselbe Grundstueck.
/// Variante B (empfohlen): WiFi-Direct + lokaler SignalR-Hub; Variante A (kostenpflichtig):
/// ARCore Cloud Anchors. Punkt-Events werden zwischen Geraeten gestreamt, Empfaenger
/// transformiert in eigene Earth-Anchor-Welt.</summary>
public interface IMultiUserSessionService
{
    /// <summary>Eigener Geraete-Name in der Session.</summary>
    string DeviceName { get; }

    /// <summary>True wenn ein Hub aktiv ist (egal ob Host oder Client).</summary>
    bool IsConnected { get; }

    /// <summary>Startet einen lokalen SignalR-Hub (WiFi-Direct-Group-Owner).
    /// Andere Geraete koennen sich mit der zurueckgegebenen URL verbinden.</summary>
    Task<string> StartHostAsync(int port = 5119, CancellationToken ct = default);

    /// <summary>Verbindet zu einem bestehenden Host. URL-Format: <c>http://&lt;ip&gt;:5119/sm</c>.</summary>
    Task ConnectAsync(string url, CancellationToken ct = default);

    /// <summary>Trennt + raeumt auf.</summary>
    Task DisconnectAsync();

    /// <summary>Aktive Teilnehmer in der Session (inkl. lokal).</summary>
    IReadOnlyList<string> ConnectedDevices { get; }

    /// <summary>Event: ein Remote-Geraet hat einen neuen Punkt erfasst.</summary>
    event Action<MultiUserPointEvent>? RemotePointAdded;

    /// <summary>Sendet einen lokal erfassten Punkt an alle anderen Geraete.</summary>
    Task BroadcastPointAsync(SurveyPoint point, CancellationToken ct = default);
}

/// <summary>Punkt-Event aus einer Multi-User-Session.</summary>
public sealed record MultiUserPointEvent(string SenderDeviceName, SurveyPoint Point, DateTime ReceivedAt);
