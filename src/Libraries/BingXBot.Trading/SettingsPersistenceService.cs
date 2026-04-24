using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;

namespace BingXBot.Trading;

/// <summary>
/// Standard-Implementierung von <see cref="ISettingsPersistenceService"/>.
/// Baut einen <see cref="FullSettingsDto"/>-Snapshot aus den DI-Singletons und
/// delegiert an <see cref="ISettingsService.SaveAllAsync"/> — der entscheidet dann
/// je nach Remote/Local-Modus über DB oder HTTP-Call.
/// </summary>
public sealed class SettingsPersistenceService : ISettingsPersistenceService
{
    private readonly ISettingsService _settings;
    private readonly BotSettings _bot;
    private readonly RiskSettings _risk;
    private readonly ScannerSettings _scanner;
    private readonly BacktestSettings _backtest;
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);

    public SettingsPersistenceService(
        ISettingsService settings,
        BotSettings bot,
        RiskSettings risk,
        ScannerSettings scanner,
        BacktestSettings backtest)
    {
        _settings = settings;
        _bot = bot;
        _risk = risk;
        _scanner = scanner;
        _backtest = backtest;
    }

    public async Task SaveAllAsync()
    {
        // SemaphoreSlim schützt gegen parallele fire-and-forget Aufrufe aus mehreren ViewModels.
        if (!await _saveSemaphore.WaitAsync(500)) return;
        try
        {
            _bot.Risk = _risk;
            _bot.Scanner = _scanner;
            _bot.Backtest = _backtest;

            var snapshot = new FullSettingsDto(_bot, _risk, _scanner, _backtest, 0);
            await _settings.SaveAllAsync(snapshot);
        }
        finally { _saveSemaphore.Release(); }
    }
}
