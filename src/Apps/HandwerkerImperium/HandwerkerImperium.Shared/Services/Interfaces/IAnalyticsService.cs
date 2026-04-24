namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Telemetrie-/Analytics-Service fuer Retention-, Funnel- und Monetisierungs-Analysen.
/// Schreibt Events via Firebase REST in <c>analytics_events/{YYYY-MM-DD}</c> und aggregierte
/// Tageszaehler nach <c>analytics_daily/{YYYY-MM-DD}/counters/{eventName}</c>.
///
/// DSGVO: Events werden nur geschrieben wenn <see cref="IsEnabled"/> true ist.
/// Consent wird ueber <see cref="SettingsData.AnalyticsEnabled"/> persistiert.
///
/// Performance: Events landen in einer internen Queue und werden alle 30s oder bei
/// <see cref="FlushAsync"/> gebatcht an Firebase geschickt. So bleiben HTTP-Kosten gering.
/// </summary>
public interface IAnalyticsService : IDisposable
{
    /// <summary>
    /// Liefert/setzt ob Analytics aktiv ist. Aenderung wird in SettingsData persistiert.
    /// Beim Deaktivieren wird die aktuelle Queue verworfen.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Initialisiert den Service. Liest Consent-Status aus Settings,
    /// startet den Flush-Timer (wenn aktiviert) und setzt Session-Start-Event.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Trackt ein einzelnes Event. No-Op wenn <see cref="IsEnabled"/> false.
    /// Parameter werden mit dem Event-Namen in die Firebase-DB geschrieben.
    /// </summary>
    void TrackEvent(string eventName, Dictionary<string, object?>? parameters = null);

    /// <summary>
    /// Trackt einen Schritt in einem Funnel (z.B. Tutorial, IAP-Flow).
    /// Aequivalent zu <see cref="TrackEvent"/> mit Parametern <c>funnel</c> und <c>step</c>.
    /// </summary>
    void TrackFunnelStep(string funnelName, int step, string stepName);

    /// <summary>
    /// Setzt eine User-Property (z.B. <c>player_tier</c>, <c>premium</c>, <c>language</c>).
    /// Wird bei jedem Event mitgeschickt (als Teil des <c>user</c>-Segments).
    /// </summary>
    void SetUserProperty(string name, string? value);

    /// <summary>
    /// Schickt die aktuelle Event-Queue sofort an Firebase.
    /// Wird beim App-Pause, App-Stop und alle 30s automatisch aufgerufen.
    /// </summary>
    Task FlushAsync();
}
