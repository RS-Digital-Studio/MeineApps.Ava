using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Einstellungen für automatische Aktionen (Level-basierte Freischaltung).
/// </summary>
public class AutomationSettings
{
    /// <summary>
    /// Lieferungen automatisch einsammeln (ab Level 15).
    /// </summary>
    [JsonPropertyName("autoCollectDelivery")]
    public bool AutoCollectDelivery { get; set; }

    /// <summary>
    /// Aufträge automatisch annehmen (ab Level 25).
    /// </summary>
    [JsonPropertyName("autoAcceptOrder")]
    public bool AutoAcceptOrder { get; set; }

    /// <summary>
    /// Arbeiter automatisch zuweisen (ab Level 20, alle 60s).
    /// </summary>
    [JsonPropertyName("autoAssignWorkers")]
    public bool AutoAssignWorkers { get; set; }

    /// <summary>
    /// Tägliche Belohnung automatisch einsammeln (nur Premium).
    /// </summary>
    [JsonPropertyName("autoClaimDaily")]
    public bool AutoClaimDaily { get; set; }

    /// <summary>
    /// v2.0.36: Wenn aktiv, werden NUR Standard-Auftraege automatisch akzeptiert.
    /// Live-/Premium-Auftraege bleiben fuer manuelle Annahme liegen.
    /// Default: true (sicherer Default fuer bestehende Spieler — verhindert dass
    /// AutoAccept versehentlich VIP-Auftraege „verbrennt").
    /// </summary>
    [JsonPropertyName("autoAcceptOnlyStandard")]
    public bool AutoAcceptOnlyStandard { get; set; } = true;

    /// <summary>
    /// v2.0.36: Wenn aktiv, ueberspringt MiniGame-Auto-Complete Live-/Premium-Auftraege.
    /// Sodass diese manuell gespielt werden — Strategie + Risk/Reward bleiben relevant.
    /// Default: true.
    /// </summary>
    [JsonPropertyName("autoCompleteSkipLiveOrders")]
    public bool AutoCompleteSkipLiveOrders { get; set; } = true;
}
