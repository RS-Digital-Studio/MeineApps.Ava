using BingXBot.Contracts.Services;

namespace BingXBot.Services;

/// <summary>
/// Synchronisiert die Remote-Settings (Risk/Scanner/Bot/Backtest) bei jedem
/// erfolgreichen (Re-)Connect vom Server in die Client-DI-Singletons.
///
/// Problem ohne diesen Service: <see cref="App.RefreshRemoteSettingsAsync"/> läuft nur einmal
/// beim App-Start. Wenn der Pi-Server offline war, zwischendurch neu startet oder das Netz
/// kurz weg ist, bleibt der Client auf dem alten Stand und der nächste Save überschreibt
/// frische Server-Werte mit veralteten Client-Werten (z.B. RiskSettings).
///
/// Lösung: Subscribe auf <see cref="IBotEventStream.ConnectionChanged"/>. Bei jedem Wechsel
/// auf <see cref="ConnectionStatus.Connected"/> wird ein Refresh ausgelöst, der silent im
/// Hintergrund läuft (kein UI-Popup). Ein 2-Sekunden-Debounce verhindert Doppel-Refresh,
/// wenn App-Start-Initial-Sync und erster Connect unmittelbar hintereinander feuern.
///
/// Nur im Remote-Modus aktiv. Im Local-Modus existiert die Client-Server-Trennung nicht.
/// </summary>
public sealed class RemoteSettingsAutoSync : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(2);

    private readonly IBotEventStream _stream;
    private readonly Func<Task> _refreshAsync;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private bool _disposed;

    /// <summary>
    /// Ctor subscribed sofort. Der Aufrufer soll die Instanz als Eager-Singleton
    /// resolven (z.B. in App.InitializeAsync), damit der Subscribe vor dem ersten
    /// <see cref="IBotEventStream.StartAsync"/> aktiv ist.
    /// </summary>
    public RemoteSettingsAutoSync(IBotEventStream stream, Func<Task> refreshAsync)
    {
        _stream = stream;
        _refreshAsync = refreshAsync;
        _stream.ConnectionChanged += OnConnectionChanged;
    }

    private async void OnConnectionChanged(ConnectionStatus status)
    {
        if (_disposed) return;
        if (status != ConnectionStatus.Connected) return;

        // Debounce: Wenn der letzte Refresh frischer als 2s ist, skippe. Das fängt den Fall
        // "App-Start-Refresh + direkt darauf initialer SignalR-Connect" ab, ohne Logik-Special-
        // Casing. Bei echten Reconnects ist der Abstand immer > 2s.
        if (DateTime.UtcNow - _lastRefreshUtc < DebounceInterval) return;

        // Non-Blocking try-acquire: Wenn gerade ein Refresh läuft, überspringe — der laufende
        // Refresh übernimmt den aktuellen Server-Stand sowieso mit.
        if (!await _refreshLock.WaitAsync(0).ConfigureAwait(false)) return;

        try
        {
            await _refreshAsync().ConfigureAwait(false);
            _lastRefreshUtc = DateTime.UtcNow;
            System.Diagnostics.Debug.WriteLine("[RemoteSettingsAutoSync] Settings nach Connect vom Server synchronisiert.");
        }
        catch (Exception ex)
        {
            // Silent: UI bleibt funktional, nächster Connect versucht es erneut.
            System.Diagnostics.Debug.WriteLine($"[RemoteSettingsAutoSync] Sync fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream.ConnectionChanged -= OnConnectionChanged;
        _refreshLock.Dispose();
    }
}
