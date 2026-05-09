namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Sub-Tabs im Imperium-Hauptbereich (v2.0.37).
/// Statt einer langen Scrollseite werden Werkstaetten / Worker / Forschung /
/// Ausruestung / Ascension in 5 Sub-Tabs aufgeteilt — analog zur GuildView.
/// </summary>
public enum ImperiumSubTab
{
    /// <summary>Workshops + Gebaeude.</summary>
    Workshops = 0,

    /// <summary>Worker-Markt + Manager.</summary>
    Workers = 1,

    /// <summary>Research + Crafting.</summary>
    Research = 2,

    /// <summary>Equipment + MasterTools.</summary>
    Equipment = 3,

    /// <summary>Ascension (nur sichtbar nach 3x Legende-Prestige).</summary>
    Ascension = 4
}
