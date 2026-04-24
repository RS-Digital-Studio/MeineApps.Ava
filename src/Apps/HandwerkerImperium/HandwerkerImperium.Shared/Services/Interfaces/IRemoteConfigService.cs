namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Firebase-Remote-Config-Wrapper. Liest Balancing-Werte und Feature-Flags
/// aus <c>remote_config/*</c> in der Firebase-Realtime-DB.
///
/// Verwendung: Beim App-Start einmal <see cref="InitializeAsync"/>, danach
/// synchrone Getter. Werte werden nach dem Download in den <see cref="IPreferencesService"/>
/// gecached — wenn der naechste Start offline ist, bleiben die zuletzt bekannten Werte aktiv.
///
/// Alle Get-Methoden liefern den <c>defaultValue</c> zurueck wenn der Key nicht gefunden wird.
/// So bleiben alle Features lauffaehig wenn Firebase offline ist oder noch kein Cache existiert.
/// </summary>
public interface IRemoteConfigService
{
    /// <summary>
    /// Zeitpunkt des letzten erfolgreichen Downloads (UTC). Null wenn noch nie abgerufen.
    /// </summary>
    DateTime? LastFetchedAt { get; }

    /// <summary>
    /// Laedt die aktuelle Remote-Config aus Firebase.
    /// Bei Fehler (Netz/Parse) werden bereits gecachte Werte weiterverwendet.
    /// </summary>
    Task InitializeAsync();

    /// <summary>Liefert einen Int-Wert aus der Remote-Config.</summary>
    int GetInt(string key, int defaultValue);

    /// <summary>Liefert einen Decimal-Wert aus der Remote-Config.</summary>
    decimal GetDecimal(string key, decimal defaultValue);

    /// <summary>Liefert einen Bool-Wert aus der Remote-Config.</summary>
    bool GetBool(string key, bool defaultValue);

    /// <summary>Liefert einen String-Wert aus der Remote-Config.</summary>
    string GetString(string key, string defaultValue);
}
