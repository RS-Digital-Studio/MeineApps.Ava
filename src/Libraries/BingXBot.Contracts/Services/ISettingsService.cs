using BingXBot.Contracts.Dto;
using BingXBot.Core.Configuration;

namespace BingXBot.Contracts.Services;

/// <summary>
/// Liest + schreibt Settings (Bot/Risk/Scanner/Backtest).
/// Im Server: Greift direkt auf die Singleton-Instanzen + speichert in DB.
/// Im Client: HTTP GET/PUT. Push von SettingsChanged-Event wenn ein anderer Client aendert.
/// </summary>
public interface ISettingsService
{
    Task<FullSettingsDto> GetAsync(CancellationToken ct = default);
    Task SaveBotAsync(BotSettings settings, CancellationToken ct = default);
    Task SaveRiskAsync(RiskSettings settings, CancellationToken ct = default);
    Task SaveScannerAsync(ScannerSettings settings, CancellationToken ct = default);
    Task SaveBacktestAsync(BacktestSettings settings, CancellationToken ct = default);

    /// <summary>Gesamten Settings-Block uebernehmen (atomar — Revision wird geprueft).</summary>
    Task SaveAllAsync(FullSettingsDto snapshot, CancellationToken ct = default);

    /// <summary>v1.6.3 Phase 14 — Audit-Trail-History.</summary>
    Task<SettingsHistoryDto> GetHistoryAsync(string? field = null, DateTime? since = null,
        int limit = 200, CancellationToken ct = default);

    /// <summary>Feuert wenn Server einen Change pushed (Multi-Client-Szenario).</summary>
    event Action<FullSettingsDto>? SettingsChanged;
}
