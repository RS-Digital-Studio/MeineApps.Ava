using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// V7 (): Auto-Verkaufs-Regel pro Material-Slot im Lager.
///  nutzt nur <see cref="Enabled"/>. erweitert um MinPrice/MaxPrice
/// fuer Markt-basierte Auto-Verkaufs-Strategien.
/// </summary>
public sealed class AutoSellRule
{
    /// <summary>Auto-Verkauf aktiv (Default false — Spieler entscheidet pro Slot).</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// : Minimaler Markt-Preis bei dem verkauft wird (Default 0 = immer verkaufen).
    /// </summary>
    [JsonPropertyName("minPrice")]
    public decimal MinPrice { get; set; }

    /// <summary>
    /// : Maximaler Bestand der gehalten werden soll (Default 0 = alles bis Stack-Limit).
    /// Ueber dieser Schwelle wird verkauft (Tail-Sell).
    /// </summary>
    [JsonPropertyName("keepUntil")]
    public int KeepUntil { get; set; }
}
