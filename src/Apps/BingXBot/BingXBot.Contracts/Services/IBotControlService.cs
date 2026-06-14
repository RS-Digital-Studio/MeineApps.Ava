using BingXBot.Contracts.Dto;

namespace BingXBot.Contracts.Services;

/// <summary>
/// Steuerung des Bots: Start/Stop/EmergencyStop + Einzelpositions-Close.
/// Im Server: Wrappt LiveTradingManager/PaperTradingService (Multi-TF Standalone).
/// Im Client: HTTP-Calls zu /bot/start, /bot/stop, etc.
/// </summary>
public interface IBotControlService
{
    /// <summary>Aktueller Status (synchron, schneller Snapshot ohne Netz-Call wenn moeglich).</summary>
    BotStatusDto GetStatus();

    /// <summary>Status asynchron vom Server holen (erzwingt Refresh im Remote-Modus).</summary>
    Task<BotStatusDto> GetStatusAsync(CancellationToken ct = default);

    Task<BotStatusDto> StartAsync(BotStartRequest request, CancellationToken ct = default);
    Task<BotStatusDto> StopAsync(CancellationToken ct = default);
    Task<BotStatusDto> EmergencyStopAsync(CancellationToken ct = default);
    Task ClosePositionAsync(string symbol, Core.Enums.Side side, CancellationToken ct = default);

    /// <summary>Wird gefeuert wenn sich der Status aendert (Remote: Push via SignalR).</summary>
    event Action<BotStatusDto>? StatusChanged;
}
