using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// V7 (Phase 1): Auto-Verkaufs-Regel pro Material-Slot im Lager.
/// Phase 1 nutzt nur <see cref="Enabled"/>. Phase 3 erweitert um MinPrice/MaxPrice
/// fuer Markt-basierte Auto-Verkaufs-Strategien.
/// </summary>
public sealed class AutoSellRule
{
    /// <summary>Auto-Verkauf aktiv (Default false — Spieler entscheidet pro Slot).</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Phase 3: Minimaler Markt-Preis bei dem verkauft wird (Default 0 = immer verkaufen).
    /// </summary>
    [JsonPropertyName("minPrice")]
    public decimal MinPrice { get; set; }

    /// <summary>
    /// Phase 3: Maximaler Bestand der gehalten werden soll (Default 0 = alles bis Stack-Limit).
    /// Ueber dieser Schwelle wird verkauft (Tail-Sell).
    /// </summary>
    [JsonPropertyName("keepUntil")]
    public int KeepUntil { get; set; }
}
