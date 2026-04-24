using BingXBot.Contracts.Dto;

namespace BingXBot.Contracts.Services;

/// <summary>
/// Account-Infos: Balance, Positionen, offene Orders, Equity-Kurve.
/// Im Server: Wrappt BingXRestClient + LiveTradingManager.
/// Im Client: HTTP + Push via SignalR (PositionUpdated, EquityUpdate).
///
/// 24.04.2026 Phase-4-Audit m4: Push-Events (AccountUpdated/PositionUpdated/EquityUpdated/MarginWarning)
/// entfernt — sie waren Toter-Code: kein Producer im Server, kein Consumer im Client. Live-Updates
/// laufen ueber <see cref="IBotEventStream"/> der dieselben DTOs an SignalR-Clients pushed.
/// Konsumenten subscribieren <see cref="IBotEventStream"/> direkt — sauberer + ein Wiring-Pfad weniger.
/// </summary>
public interface IAccountService
{
    Task<AccountSnapshotDto> GetSnapshotAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default);
    Task<IReadOnlyList<EquityPointDto>> GetEquityCurveAsync(int hoursBack = 24, CancellationToken ct = default);

    /// <summary>Credentials-Status (fuer UI: "API-Keys konfiguriert?").</summary>
    Task<CredentialsStatusDto> GetCredentialsStatusAsync(CancellationToken ct = default);

    /// <summary>API-Key/Secret auf den Pi uebertragen (Server verschluesselt lokal).</summary>
    Task SetCredentialsAsync(SetCredentialsRequest request, CancellationToken ct = default);
}
