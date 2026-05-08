using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Http;
using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;

namespace BingXBot.ClientApi.Services;

public sealed class RemoteSettingsService : ISettingsService
{
    private readonly ServerConnection _connection;

    public event Action<FullSettingsDto>? SettingsChanged;

    public RemoteSettingsService(ServerConnection connection)
    {
        _connection = connection;
        // SettingsChanged-Event wird bei SignalR Phase 4.5 angeschlossen
    }

    public Task<FullSettingsDto> GetAsync(CancellationToken ct = default) =>
        _connection.HttpClient.GetJsonAsync<FullSettingsDto>(ApiRoutes.Settings, ct);

    public Task SaveBotAsync(BotSettings settings, CancellationToken ct = default) =>
        _connection.HttpClient.PutJsonAsync(ApiRoutes.SettingsBot, settings, ct);

    public Task SaveRiskAsync(RiskSettings settings, CancellationToken ct = default) =>
        _connection.HttpClient.PutJsonAsync(ApiRoutes.SettingsRisk, settings, ct);

    public Task SaveScannerAsync(ScannerSettings settings, CancellationToken ct = default) =>
        _connection.HttpClient.PutJsonAsync(ApiRoutes.SettingsScanner, settings, ct);

    public Task SaveBacktestAsync(BacktestSettings settings, CancellationToken ct = default) =>
        _connection.HttpClient.PutJsonAsync(ApiRoutes.SettingsBacktest, settings, ct);

    public Task SaveAllAsync(FullSettingsDto snapshot, CancellationToken ct = default) =>
        _connection.HttpClient.PutJsonAsync(ApiRoutes.Settings, snapshot, ct);

    public Task<SettingsHistoryDto> GetHistoryAsync(string? field = null, DateTime? since = null,
        int limit = 200, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(field)) query.Add($"field={Uri.EscapeDataString(field)}");
        if (since.HasValue) query.Add($"since={Uri.EscapeDataString(since.Value.ToUniversalTime().ToString("O"))}");
        if (limit > 0) query.Add($"limit={limit}");
        var url = ApiRoutes.SettingsHistory + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return _connection.HttpClient.GetJsonAsync<SettingsHistoryDto>(url, ct);
    }

    // Wird von App.axaml.cs (Remote-Mode-Wire-up) aufgerufen, wenn der Hub ein SettingsChanged-
    // Event liefert. Dadurch bleiben alle ViewModels die auf ISettingsService.SettingsChanged
    // subscriben automatisch synchron, auch wenn ein anderer Client die Settings aendert.
    public void RaiseChanged(FullSettingsDto dto) => SettingsChanged?.Invoke(dto);
}
