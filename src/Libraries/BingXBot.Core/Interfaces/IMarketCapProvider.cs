namespace BingXBot.Core.Interfaces;

/// <summary>
/// Domain-Abstraktion für Market-Cap-Daten. Konkrete Implementationen (z.B.
/// <c>CoinGeckoMarketCapProvider</c> in <c>BingXBot.Engine</c>) holen die Rankings
/// von externen APIs und befüllen <see cref="Helpers.MarketCapCache"/>.
/// 04.05.2026: Eingeführt um die HTTP-Layer-Verletzung in Core zu beseitigen.
/// </summary>
public interface IMarketCapProvider
{
    /// <summary>
    /// Aktualisiert den Cache wenn er älter als das Intervall ist (no-op wenn frisch).
    /// Wird vom Trading-Service vor jedem Scan-Zyklus aufgerufen.
    /// </summary>
    Task RefreshIfNeededAsync(CancellationToken ct = default);
}
