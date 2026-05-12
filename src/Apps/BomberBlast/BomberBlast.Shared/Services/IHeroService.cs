using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Hero/Character-Service (Sprint 7.1 AAA-Audit #21).
///
/// <para>
/// Verwaltet die 5 spielbaren Charaktere: Unlock-Status, aktiver Hero,
/// Stat-Anwendung beim Player-Spawn.
/// </para>
///
/// <para>
/// Unlock-Quellen:
/// <list type="bullet">
/// <item>Achievements (z.B. "ach_speed_demon" → SpeedySam)</item>
/// <item>Gem-Kauf (z.B. "gems_500" → TwinTina)</item>
/// <item>"default" → von Anfang an unlocked (Default-Bomber)</item>
/// </list>
/// </para>
/// </summary>
public interface IHeroService
{
    /// <summary>Aktuell aktiver Hero (default = HeroDefinitions.Default).</summary>
    HeroDefinition ActiveHero { get; }

    /// <summary>Alle 5 Heroes (statische Liste).</summary>
    IReadOnlyList<HeroDefinition> AllHeroes { get; }

    /// <summary>True wenn der Hero unlocked ist (Default IMMER true).</summary>
    bool IsUnlocked(string heroId);

    /// <summary>
    /// Aktiviert einen Hero. Wirft InvalidOperationException wenn nicht unlocked.
    /// Persistiert die Auswahl via Preferences. Feuert ActiveHeroChanged-Event.
    /// </summary>
    void SetActiveHero(string heroId);

    /// <summary>
    /// Markiert einen Hero als unlocked (z.B. nach Achievement-Trigger oder Gem-Kauf).
    /// Idempotent — bestehende Unlocks werden nicht zurueckgesetzt.
    /// </summary>
    void Unlock(string heroId);

    /// <summary>Wird gefeuert wenn der aktive Hero gewechselt wurde.</summary>
    event Action<HeroDefinition>? ActiveHeroChanged;
}
