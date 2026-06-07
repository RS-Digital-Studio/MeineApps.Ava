using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Warehouse
{
    /// <summary>
    /// Auto-Verkaufs-Regel pro Material-Slot im Lager.
    /// 1:1-Port aus dem Avalonia-Original (Models/AutoSellRule.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class AutoSellRule
    {
        /// <summary>Auto-Verkauf aktiv (Default false — Spieler entscheidet pro Slot).</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        /// <summary>Minimaler Markt-Preis bei dem verkauft wird (Default 0 = immer verkaufen).</summary>
        [JsonProperty("minPrice")]
        public decimal MinPrice { get; set; }

        /// <summary>Maximaler Bestand der gehalten werden soll (Default 0 = alles bis Stack-Limit). Darüber Tail-Sell.</summary>
        [JsonProperty("keepUntil")]
        public int KeepUntil { get; set; }
    }
}
