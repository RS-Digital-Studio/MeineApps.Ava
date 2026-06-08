using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Settings
{
    /// <summary>
    /// Einstellungen für automatische Aktionen (Level-basierte Freischaltung).
    /// 1:1-Port aus dem Avalonia-Original (Models/AutomationSettings.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class AutomationSettings
    {
        /// <summary>Lieferungen automatisch einsammeln (ab Level 15).</summary>
        [JsonProperty("autoCollectDelivery")]
        public bool AutoCollectDelivery { get; set; }

        /// <summary>Aufträge automatisch annehmen (ab Level 25).</summary>
        [JsonProperty("autoAcceptOrder")]
        public bool AutoAcceptOrder { get; set; }

        /// <summary>Arbeiter automatisch zuweisen (ab Level 20, alle 60s).</summary>
        [JsonProperty("autoAssignWorkers")]
        public bool AutoAssignWorkers { get; set; }

        /// <summary>Tägliche Belohnung automatisch einsammeln (nur Premium).</summary>
        [JsonProperty("autoClaimDaily")]
        public bool AutoClaimDaily { get; set; }

        /// <summary>
        /// Wenn aktiv, werden NUR Standard-Aufträge automatisch akzeptiert (Live-/Premium bleiben liegen).
        /// Default true (verhindert, dass AutoAccept VIP-Aufträge "verbrennt").
        /// </summary>
        [JsonProperty("autoAcceptOnlyStandard")]
        public bool AutoAcceptOnlyStandard { get; set; } = true;

        /// <summary>Wenn aktiv, überspringt MiniGame-Auto-Complete Live-/Premium-Aufträge. Default true.</summary>
        [JsonProperty("autoCompleteSkipLiveOrders")]
        public bool AutoCompleteSkipLiveOrders { get; set; } = true;
    }
}
