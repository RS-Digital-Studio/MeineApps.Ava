namespace GardenControl.Core;

/// <summary>
/// Geteilte Auth-Konstanten zwischen Server und Client (Shared-Secret-Header-Auth,
/// analog zum BingXBot-Server, hier aber bewusst als statisches Shared-Secret statt
/// Pairing/Token-Rotation — der GardenControl-Server ist ein Single-Garden-LAN-Geraet).
///
/// Der Server prueft den Header bei JEDEM /api-Aufruf und am SignalR-Hub; ohne ihn kann
/// kein LAN-Geraet und keine im Browser geoeffnete Webseite (CSRF/DNS-Rebinding) die
/// Hardware (Ventile/Pumpe/Notstopp/Modus) schalten.
/// </summary>
public static class GardenAuth
{
    /// <summary>
    /// Name des HTTP-Headers, der das Shared-Secret traegt. Wird vom Client bei REST-Requests
    /// und beim SignalR-Verbindungsaufbau mitgeschickt; der Server validiert ihn in der Middleware.
    /// </summary>
    public const string SecretHeader = "X-Garden-Secret";

    /// <summary>
    /// Default-Secret fuer Entwicklung, Mock-Betrieb (kein Pi) und den Pi-Kiosk out-of-the-box.
    /// </summary>
    /// <remarks>
    /// Bewusst KEIN Geheimnis fuer den Produktivbetrieb: Auf dem Pi MUSS ein eigenes Secret
    /// gesetzt werden (Server: Env-Var <c>Auth__SharedSecret</c> bzw. appsettings <c>Auth:SharedSecret</c>;
    /// Client/Kiosk: Einstellungen → Server-Secret). Solange Server und Client diesen Default
    /// teilen, funktioniert die lokale Entwicklung und der Mock-Modus ohne Konfiguration.
    /// </remarks>
    public const string DefaultDevSecret = "gardencontrol-dev-secret-change-me";

    /// <summary>
    /// Preferences-Schluessel, unter dem der Client das Server-Secret persistiert
    /// (analog zur Server-URL). Liegt in der plattformneutralen Preferences-Datei.
    /// </summary>
    public const string ClientSecretPreferenceKey = "GardenControl.ServerSecret";
}
