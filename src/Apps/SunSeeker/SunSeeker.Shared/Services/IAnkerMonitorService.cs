using SunSeeker.Shared.Models;

namespace SunSeeker.Shared.Services;

/// <summary>Verbindungszustand zur Powerstation.</summary>
public enum AnkerConnectionState { Disconnected, Connecting, Connected, Error }

/// <summary>
/// Liefert die Live-Solar-Eingangsleistung der Anker-Powerstation. Die echte Anbindung der
/// C2000 Gen 2 erfolgt über Ankers Cloud-MQTT (inoffiziell) und benötigt Anker-Zugangsdaten;
/// bis dahin liefert die Mock-Implementierung einen physikalisch plausiblen Verlauf aus dem
/// Sonnenstand.
/// </summary>
public interface IAnkerMonitorService
{
    AnkerConnectionState State { get; }

    double CurrentSolarWatts { get; }

    /// <summary>True, wenn die Werte simuliert sind (kein echtes Gerät) — die UI weist darauf hin.</summary>
    bool IsSimulated { get; }

    event EventHandler<PowerSample>? SampleReceived;

    event EventHandler<AnkerConnectionState>? StateChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    void Disconnect();
}
