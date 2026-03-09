namespace RebornSaga.Services;

using RebornSaga.Models;
using System;

/// <summary>
/// Zentrale Steuerung für Spieler-Progression: EXP, Level-Up, Stat-Verteilung.
/// Koordiniert zwischen Player, SkillService und UI-Feedback.
/// </summary>
public class ProgressionService
{
    private readonly SkillService _skillService;
    private readonly GoldService _goldService;

    /// <summary>
    /// Event wenn der Spieler aufsteigt (Spieler, Anzahl Level-Ups).
    /// </summary>
    public event Action<Player, int>? LevelUp;

    /// <summary>
    /// Event wenn ein Skill evolviert (alter Skill-ID, neuer Skill-ID).
    /// </summary>
    public event Action<string, string>? SkillEvolved;

    public ProgressionService(SkillService skillService, GoldService goldService)
    {
        _skillService = skillService;
        _goldService = goldService;
    }

    /// <summary>
    /// Verarbeitet EXP-Gewinn nach einem Kampf.
    /// Gibt die Anzahl Level-Ups zurück.
    /// </summary>
    public int AwardExp(Player player, int expAmount)
    {
        var levelUps = player.AddExp(expAmount);
        if (levelUps > 0)
            LevelUp?.Invoke(player, levelUps);
        return levelUps;
    }

    /// <summary>
    /// Verarbeitet Gold-Gewinn nach einem Kampf (über GoldService mit Clamp + Event).
    /// </summary>
    public void AwardGold(Player player, int goldAmount)
    {
        _goldService.AddGold(player, goldAmount);
    }

    /// <summary>
    /// Registriert eine Skill-Benutzung und prüft auf Evolution.
    /// </summary>
    public void RecordSkillUse(string skillId)
    {
        var evolvedId = _skillService.UseSkill(skillId);
        if (evolvedId != null)
            SkillEvolved?.Invoke(skillId, evolvedId);
    }

    /// <summary>
    /// Verteilt einen freien Stat-Punkt.
    /// </summary>
    public bool AllocateStatPoint(Player player, StatType stat)
    {
        if (player.FreeStatPoints <= 0) return false;

        switch (stat)
        {
            case StatType.Hp:
                player.MaxHp += 5;
                player.Hp += 5;
                break;
            case StatType.Mp:
                player.MaxMp += 3;
                player.Mp += 3;
                break;
            case StatType.Atk:
                player.Atk += 1;
                break;
            case StatType.Def:
                player.Def += 1;
                break;
            case StatType.Spd:
                player.Spd += 1;
                break;
            case StatType.Int:
                player.Int += 1;
                break;
            case StatType.Luk:
                player.Luk += 1;
                break;
        }

        player.FreeStatPoints--;
        return true;
    }
}

/// <summary>
/// Stat-Typen für manuelle Punkt-Verteilung.
/// </summary>
public enum StatType
{
    Hp,
    Mp,
    Atk,
    Def,
    Spd,
    Int,
    Luk
}
