using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading.NativeSlTpManager;

namespace BingXBot.Trading;

// Phase 18 / H7 — Composition-Refactor: Delegation an INativeSlTpManager.
// Vorher inline: CancelNativeSlTpOrdersAsync + OnStopLossAdjustedAsync.
// Logik komplett in BingxNativeSlTpManager extrahiert; LiveTradingService haelt nur die
// Instanz + delegiert. Erleichtert Testing (Mock-Manager statt LiveTradingService-Setup).
public partial class LiveTradingService
{
    /// <summary>Phase 18 / H7 — Lazy-init des Managers (umgeht Constructor-Erweiterung).</summary>
    private INativeSlTpManager? _nativeSlTpManager;
    private INativeSlTpManager NativeSlTpManager => _nativeSlTpManager ??=
        new BingxNativeSlTpManager(_restClient, _eventBus, _botSettings.EnableDesktopNotifications);

    private Task CancelNativeSlTpOrdersAsync(string symbol, Side? originalPositionSide = null)
        => NativeSlTpManager.CancelNativeSlTpOrdersAsync(symbol, originalPositionSide);

    protected override Task OnStopLossAdjustedAsync(string symbol, Side side, decimal newStopLoss)
        => NativeSlTpManager.UpdateNativeStopLossAsync(symbol, side, newStopLoss);

    /// <summary>
    /// F10 Fix — Synchroner ExitState-Persist nach kritischen Mutationen (TP-OrderId set/null,
    /// Phase Initial→Tp1Hit, RunnerActive=true, BreakevenSet=true). Vor diesem Fix wurden
    /// ExitStates nur in <c>LiveTradingManager.StopAsync</c> / <c>EmergencyStopAsync</c> persistiert
    /// — Hot-Crash zwischen TP-Place und Stop verlor die Tp1/Tp2-OrderId-Zuordnung; nach Restart
    /// konnte der Bot beim BingX-Fill-Event keinen Phase-Transition mehr triggern.
    /// SQLite-WAL-Write ist auf der Pi-SSD ~1-2 ms — kein spuerbarer Latenz-Impact pro Tick.
    /// </summary>
    protected override async Task PersistExitStatesAsync()
    {
        if (_dbService == null) return;
        try
        {
            // Snapshot-Report-Fix Befund 3 / A0.5: AUCH leeren Snapshot persistieren.
            // Vorher gab es ein "if (snapshot.Count > 0)" — Folge: wenn der Stale-Cleanup alle
            // ExitStates geleert hat, blieb der alte Snapshot in der DB stehen und nach dem naechsten
            // Server-Restart waren die 18 verwaisten Eintraege wieder im Bot.
            var snapshot = new Dictionary<string, PositionExitState>(_exitStates);
            await _dbService.SaveExitStatesAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                $"PersistExitStates fehlgeschlagen: {ex.Message}"));
        }
    }
}
