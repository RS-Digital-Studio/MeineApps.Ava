using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IHeroService"/> (Sprint 7.1 AAA-Audit #21).
/// Persistiert ActiveHeroId + Unlocked-Set in Preferences.
/// </summary>
public sealed class HeroService : IHeroService
{
    private const string KeyActiveHeroId = "Hero_ActiveId";
    private const string KeyUnlockedPrefix = "Hero_Unlocked_";

    private readonly IPreferencesService _prefs;
    private HeroDefinition _activeHero = HeroDefinitions.Default;

    public IReadOnlyList<HeroDefinition> AllHeroes => HeroDefinitions.All;

    public HeroDefinition ActiveHero => _activeHero;

    public event Action<HeroDefinition>? ActiveHeroChanged;

    public HeroService(IPreferencesService prefs)
    {
        _prefs = prefs;

        // Default-Hero ist immer unlocked
        if (!IsUnlocked(HeroDefinitions.Default.Id))
        {
            Unlock(HeroDefinitions.Default.Id);
        }

        // Aktiven Hero aus Preferences wiederherstellen — Fallback auf Default.
        var activeId = _prefs.Get(KeyActiveHeroId, HeroDefinitions.Default.Id);
        var found = HeroDefinitions.All.FirstOrDefault(h => h.Id == activeId);
        if (found != null && IsUnlocked(found.Id))
        {
            _activeHero = found;
        }
    }

    public bool IsUnlocked(string heroId)
    {
        // Default ist IMMER unlocked, auch wenn die Pref-Flag fehlt (z.B. nach Datenwechsel).
        if (heroId == HeroDefinitions.Default.Id) return true;
        return _prefs.Get(KeyUnlockedPrefix + heroId, false);
    }

    public void SetActiveHero(string heroId)
    {
        if (!IsUnlocked(heroId))
            throw new InvalidOperationException($"Hero '{heroId}' is not unlocked.");

        var hero = HeroDefinitions.All.FirstOrDefault(h => h.Id == heroId)
            ?? throw new InvalidOperationException($"Hero '{heroId}' not found.");

        if (_activeHero.Id == heroId) return;  // No-Op

        _activeHero = hero;
        _prefs.Set(KeyActiveHeroId, heroId);
        ActiveHeroChanged?.Invoke(hero);
    }

    public void Unlock(string heroId)
    {
        _prefs.Set(KeyUnlockedPrefix + heroId, true);
    }
}
