using BingXBot.Core.Interfaces;

namespace BingXBot.Engine.Helpers;

/// <summary>
/// 04.05.2026 — Static-Bridge zwischen DI-Provider (<see cref="IMarketCapProvider"/>) und
/// statischen Aufrufern in <c>TradingServiceBase</c>. App-Startup ruft <see cref="Configure"/>
/// einmalig mit der konkreten Provider-Instanz auf; Tests und Backtest können das auslassen
/// (no-op Refresh, der Cache bleibt leer und die Volume-Fallback-Logik in <c>ScanHelper</c> greift).
/// </summary>
public static class MarketCapRefreshHelper
{
    private static IMarketCapProvider? _provider;

    /// <summary>Setzt den aktiven Provider (App-Startup). Subsequente Aufrufe ersetzen den vorherigen.</summary>
    public static void Configure(IMarketCapProvider provider) => _provider = provider;

    /// <summary>True wenn ein Provider konfiguriert ist (für UI-Diagnose).</summary>
    public static bool IsConfigured => _provider != null;

    /// <summary>
    /// Triggert <see cref="IMarketCapProvider.RefreshIfNeededAsync"/> wenn ein Provider konfiguriert ist;
    /// no-op sonst (Tests/Backtest).
    /// </summary>
    public static Task RefreshIfNeededAsync(CancellationToken ct = default)
        => _provider?.RefreshIfNeededAsync(ct) ?? Task.CompletedTask;
}
