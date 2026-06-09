using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Interfaces;

namespace BingXBot.Trading.Local;

/// <summary>
/// Server-seitige Account-Impl: Liest Balance + Positionen aus dem aktiven Service
/// (Live: BingXRestClient; Paper: SimulatedExchange aus PaperTradingService).
/// </summary>
public sealed class LocalAccountService : IAccountService
{
    private readonly LiveTradingManager _liveManager;
    private readonly PaperTradingService _paperService;
    private readonly ISecureStorageService _secureStorage;
    private readonly BotDatabaseService? _db;
    private readonly CrossSectional.CrossSectionalManager? _xsecManager;

    public LocalAccountService(
        LiveTradingManager liveManager,
        PaperTradingService paperService,
        ISecureStorageService secureStorage,
        BotDatabaseService? db = null,
        CrossSectional.CrossSectionalManager? xsecManager = null)
    {
        _liveManager = liveManager;
        _paperService = paperService;
        _secureStorage = secureStorage;
        _db = db;
        _xsecManager = xsecManager;
    }

    public async Task<AccountSnapshotDto> GetSnapshotAsync(CancellationToken ct = default)
    {
        // Cross-Sectional ZUERST: Nach einem Scalper-Stop bleibt _liveManager.IsConnected true
        // (_restClient lebt bis Dispose) — ohne diesen Zweig zeigte ein laufender Paper-Xsec
        // weiterhin das ECHTE Konto, und die persistierte Equity-Kurve mass nie die Paper-Sim.
        if (_xsecManager?.IsRunning == true)
        {
            var exec = _xsecManager.PaperExchange as IExchangeClient ?? _xsecManager.RestClient;
            if (exec != null)
            {
                var info = await exec.GetAccountInfoAsync().ConfigureAwait(false);
                var positions = await exec.GetPositionsAsync(ct).ConfigureAwait(false);
                var positionDtos = positions.Select(p => p.ToDto()).ToList();
                return new AccountSnapshotDto(
                    Balance: info.Balance,
                    Available: info.AvailableBalance,
                    UnrealizedPnl: info.UnrealizedPnl,
                    // Realisierter PnL ist ueber die Trades-Tabelle (Mode=Paper) auswertbar;
                    // die Equity-Messgroesse des Xsec-Laufs ist Balance + UnrealizedPnl.
                    RealizedPnlToday: 0m,
                    TotalPnlAllTime: 0m,
                    OpenPositionCount: positionDtos.Count,
                    Positions: positionDtos,
                    AsOfUtc: DateTime.UtcNow);
            }
        }

        if (_liveManager.IsConnected && _liveManager.RestClient is { } rest)
        {
            var accountInfo = await rest.GetAccountInfoAsync().ConfigureAwait(false);
            var positions = await rest.GetPositionsAsync(ct).ConfigureAwait(false);
            var positionDtos = positions.Select(p => p.ToDto()).ToList();
            return new AccountSnapshotDto(
                Balance: accountInfo.Balance,
                Available: accountInfo.AvailableBalance,
                UnrealizedPnl: accountInfo.UnrealizedPnl,
                RealizedPnlToday: 0m,
                TotalPnlAllTime: 0m,
                OpenPositionCount: positionDtos.Count,
                Positions: positionDtos,
                AsOfUtc: DateTime.UtcNow);
        }

        if (_paperService.Exchange is { } exchange)
        {
            var info = await exchange.GetAccountInfoAsync().ConfigureAwait(false);
            var positions = await exchange.GetPositionsAsync().ConfigureAwait(false);
            var positionDtos = positions.Select(p => p.ToDto()).ToList();
            return new AccountSnapshotDto(
                Balance: info.Balance,
                Available: info.AvailableBalance,
                UnrealizedPnl: info.UnrealizedPnl,
                RealizedPnlToday: 0m,
                TotalPnlAllTime: 0m,
                OpenPositionCount: positionDtos.Count,
                Positions: positionDtos,
                AsOfUtc: DateTime.UtcNow);
        }

        return new AccountSnapshotDto(0, 0, 0, 0, 0, 0, Array.Empty<PositionDto>(), DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken ct = default)
    {
        var snap = await GetSnapshotAsync(ct).ConfigureAwait(false);
        return snap.Positions;
    }

    public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default)
    {
        if (_liveManager.IsConnected && _liveManager.RestClient is { } rest)
        {
            var orders = await rest.GetOpenOrdersAsync(symbol).ConfigureAwait(false);
            return orders.Select(o => o.ToDto()).ToList();
        }
        return Array.Empty<OpenOrderDto>();
    }

    public async Task<IReadOnlyList<EquityPointDto>> GetEquityCurveAsync(int hoursBack = 24, CancellationToken ct = default)
    {
        if (_db == null) return Array.Empty<EquityPointDto>();
        var from = DateTime.UtcNow.AddHours(-Math.Max(1, hoursBack));
        var points = await _db.GetEquitySnapshotsAsync(from).ConfigureAwait(false);
        return points.Select(p => p.ToDto()).ToList();
    }

    public Task<CredentialsStatusDto> GetCredentialsStatusAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new CredentialsStatusDto(
            HasCredentials: _secureStorage.HasCredentials,
            IsConnected: _liveManager.IsConnected,
            ApiKeyMasked: null,
            LastConnectAttemptUtc: null,
            LastError: null));
    }

    public async Task SetCredentialsAsync(SetCredentialsRequest request, CancellationToken ct = default)
    {
        await _secureStorage.SaveCredentialsAsync(request.ApiKey, request.ApiSecret).ConfigureAwait(false);
    }
}
