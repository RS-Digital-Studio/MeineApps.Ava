using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Zentraler adaptiver Betriebsmodus der App: AR-First (ohne Hardware) versus RTK-Stab.
///
/// SmartMeasure wird vorwiegend im AR-Kamera-Modus OHNE RTK-Stab genutzt. Die App startet
/// daher AR-First: die gesamte Hardware-UI (Live-Kompass, Position, Satelliten, Stab-Akku,
/// BLE-Tab, Stab-Einstellungen) ist ausgeblendet, bis tatsaechlich ein RTK-Stab verbunden
/// wird. Sobald einmal ein Stab verbunden war, merkt sich die App das (Preference) und zeigt
/// die Hardware-UI dauerhaft an — so muss der Nutzer nie manuell umschalten.
///
/// Konsumenten (MainViewModel, SurveyViewModel, SettingsViewModel) binden gegen
/// <see cref="ShowRtkUi"/> und reagieren auf <see cref="Changed"/>. Das Event kommt vom
/// BLE-Background-Thread — UI-Konsumenten muessen via <c>Dispatcher.UIThread.Post</c> marshallen.
/// </summary>
public interface IHardwareModeService
{
    /// <summary>True, wenn die RTK-Hardware-UI angezeigt werden soll: aktuell ein Stab
    /// verbunden ODER frueher schon einmal einer verbunden war. False = reiner AR-Modus.</summary>
    bool ShowRtkUi { get; }

    /// <summary>Aktuell ein RTK-Stab via BLE verbunden?</summary>
    bool IsConnected { get; }

    /// <summary>Feuert, wenn sich <see cref="ShowRtkUi"/> oder <see cref="IsConnected"/> aendert.
    /// Kommt vom BLE-Background-Thread — Konsumenten muessen via Dispatcher marshallen.</summary>
    event Action? Changed;

    /// <summary>Setzt die App auf reinen AR-Modus zurueck (vergisst frueher verbundene Staebe).
    /// Blendet die Hardware-UI wieder aus, sofern aktuell kein Stab verbunden ist.</summary>
    void ResetToArMode();
}

/// <summary>Standard-Implementierung: leitet den Modus aus dem BLE-Verbindungsstatus ab und
/// persistiert die Erst-Verbindung via <see cref="MeineApps.Core.Ava.Services.IPreferencesService"/>.</summary>
public sealed class HardwareModeService : IHardwareModeService, IDisposable
{
    private const string KeyHasEverConnected = "sm.has_ever_connected_ble";

    private readonly IBleService _ble;
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _prefs;

    private bool _isConnected;
    private bool _hasEverConnected;

    public HardwareModeService(IBleService ble, MeineApps.Core.Ava.Services.IPreferencesService prefs)
    {
        _ble = ble;
        _prefs = prefs;
        _hasEverConnected = _prefs.Get(KeyHasEverConnected, false);
        _isConnected = _ble.CurrentState.IsConnected;
        _ble.StateChanged += OnStateChanged;
    }

    public bool IsConnected => _isConnected;

    public bool ShowRtkUi => _isConnected || _hasEverConnected;

    public event Action? Changed;

    private void OnStateChanged(StickState state)
    {
        var changed = false;

        if (state.IsConnected != _isConnected)
        {
            _isConnected = state.IsConnected;
            changed = true;
        }

        // Erst-Verbindung merken — ab jetzt zeigt die App dauerhaft die Hardware-UI.
        if (state.IsConnected && !_hasEverConnected)
        {
            _hasEverConnected = true;
            _prefs.Set(KeyHasEverConnected, true);
            changed = true;
        }

        if (changed) Changed?.Invoke();
    }

    public void ResetToArMode()
    {
        _hasEverConnected = false;
        _prefs.Set(KeyHasEverConnected, false);
        Changed?.Invoke();
    }

    public void Dispose() => _ble.StateChanged -= OnStateChanged;
}
