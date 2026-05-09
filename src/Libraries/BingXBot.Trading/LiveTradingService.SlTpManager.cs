using BingXBot.Core.Enums;
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
}
