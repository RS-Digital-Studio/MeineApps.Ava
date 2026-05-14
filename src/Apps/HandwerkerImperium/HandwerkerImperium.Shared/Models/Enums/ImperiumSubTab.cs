namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Sub-Tabs im Imperium-Hauptbereich (v2.0.37 / V7).
/// Statt einer langen Scrollseite werden Werkstaetten / Lager / Worker / Forschung /
/// Ausruestung / Ascension in 6 Sub-Tabs aufgeteilt — analog zur GuildView.
/// V7 (): Lager-Sub-Tab eingefuegt (Plan Section 7.1).
/// </summary>
public enum ImperiumSubTab
{
    /// <summary>Workshops + Gebaeude.</summary>
    Workshops = 0,

    /// <summary>V7: Lager-Slots, Stack-Limits, Auto-Verkauf-Regeln ().</summary>
    Warehouse = 1,

    /// <summary>Worker-Markt + Manager.</summary>
    Workers = 2,

    /// <summary>Research + Crafting.</summary>
    Research = 3,

    /// <summary>Equipment + MasterTools.</summary>
    Equipment = 4,

    /// <summary>Ascension (nur sichtbar nach 3x Legende-Prestige).</summary>
    Ascension = 5
}
