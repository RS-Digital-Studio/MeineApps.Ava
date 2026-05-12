namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bounded-Context "Content": Bündelt alle Subsysteme die spielbaren Content liefern —
/// Forschung, Crafting, Equipment, Buildings, Manager. AAA-Audit P1 Service-Sprawl-Reduction.
///
/// Additiv eingeführt — bestehende Konsumenten der Einzel-Services bleiben unverändert.
/// </summary>
public interface IContentFacade
{
    /// <summary>Forschungsbaum (45 Nodes, 6 Branches, Timer, Effekt-Cache).</summary>
    IResearchService Research { get; }

    /// <summary>Crafting-Pipeline (Rezepte, Material-Konsum, Verkaufspreise, Tier-Items).</summary>
    ICraftingService Crafting { get; }

    /// <summary>Worker-Ausrüstung (Drop-Pool, Slot-Belegung, Income/XP-Boni).</summary>
    IEquipmentService Equipment { get; }

    /// <summary>Gebäude (10 Typen, Upgrade-Kosten, Effekt-Cache, Max-Level).</summary>
    IBuildingService Building { get; }

    /// <summary>Manager (Vorarbeiter, Workshop-spezifische + globale Boni).</summary>
    IManagerService Manager { get; }
}
