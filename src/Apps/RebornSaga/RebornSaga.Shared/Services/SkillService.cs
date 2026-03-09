namespace RebornSaga.Services;

using RebornSaga.Models;
using RebornSaga.Models.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Verwaltet Skill-Definitionen, Mastery-Fortschritt und Evolution.
/// Lädt Skills aus Embedded JSON pro Klasse.
/// </summary>
public class SkillService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    // Alle Skill-Definitionen (ID → Skill)
    private readonly Dictionary<string, Skill> _allSkills = new();

    // Spieler-Skills (ID → PlayerSkill)
    private readonly Dictionary<string, PlayerSkill> _playerSkills = new();

    /// <summary>
    /// Lädt alle Skill-Definitionen für alle Klassen aus Embedded JSON.
    /// </summary>
    public void LoadSkills()
    {
        _allSkills.Clear();
        LoadClassSkills("swordmaster");
        LoadClassSkills("arcanist");
        LoadClassSkills("shadowblade");
    }

    /// <summary>
    /// Initialisiert die Spieler-Skills für eine bestimmte Klasse.
    /// Schaltet die Basis-Skills (Tier 1) frei.
    /// </summary>
    public void InitializeForClass(ClassName className)
    {
        _playerSkills.Clear();

        // Alle Tier-1-Skills der Klasse freischalten (per Class-Property, nicht ID-Prefix)
        foreach (var skill in _allSkills.Values.Where(s => s.Class == className && s.Tier == 1))
        {
            _playerSkills[skill.Id] = new PlayerSkill
            {
                Definition = skill,
                Mastery = 0,
                IsUnlocked = true
            };
        }
    }

    /// <summary>
    /// Registriert eine Skill-Benutzung und prüft auf Evolution.
    /// Gibt die neue Skill-ID zurück wenn Evolution stattfand, sonst null.
    /// </summary>
    public string? UseSkill(string skillId)
    {
        if (!_playerSkills.TryGetValue(skillId, out var playerSkill)) return null;
        if (!playerSkill.IsUnlocked) return null;

        playerSkill.Mastery++;

        // Evolution prüfen
        if (playerSkill.CanEvolve)
            return EvolveSkill(skillId);

        return null;
    }

    /// <summary>
    /// Evolviert einen Skill zur nächsten Stufe.
    /// </summary>
    private string? EvolveSkill(string skillId)
    {
        if (!_playerSkills.TryGetValue(skillId, out var current)) return null;
        if (string.IsNullOrEmpty(current.Definition.NextTierId)) return null;
        if (!_allSkills.TryGetValue(current.Definition.NextTierId, out var nextSkill)) return null;

        // Alten Skill sperren
        current.IsUnlocked = false;

        // Neuen Skill freischalten
        _playerSkills[nextSkill.Id] = new PlayerSkill
        {
            Definition = nextSkill,
            Mastery = 0,
            IsUnlocked = true
        };

        return nextSkill.Id;
    }

    /// <summary>
    /// Gibt alle freigeschalteten Spieler-Skills zurück.
    /// </summary>
    public List<PlayerSkill> GetUnlockedSkills()
    {
        return _playerSkills.Values.Where(s => s.IsUnlocked).ToList();
    }

    /// <summary>
    /// Gibt einen Skill anhand seiner ID zurück.
    /// </summary>
    public Skill? GetSkillDefinition(string skillId)
    {
        _allSkills.TryGetValue(skillId, out var skill);
        return skill;
    }

    /// <summary>
    /// Gibt einen PlayerSkill anhand seiner ID zurück.
    /// </summary>
    public PlayerSkill? GetPlayerSkill(string skillId)
    {
        _playerSkills.TryGetValue(skillId, out var ps);
        return ps;
    }

    /// <summary>
    /// Gibt alle Spieler-Skills zurück (für Save).
    /// </summary>
    public Dictionary<string, PlayerSkill> GetAllPlayerSkills() => new(_playerSkills);

    /// <summary>
    /// Setzt den Mastery-Stand (für Save-Load).
    /// </summary>
    public void RestorePlayerSkills(Dictionary<string, int> masteryData, HashSet<string> unlockedIds)
    {
        _playerSkills.Clear();
        foreach (var (id, mastery) in masteryData)
        {
            if (!_allSkills.TryGetValue(id, out var skill)) continue;
            _playerSkills[id] = new PlayerSkill
            {
                Definition = skill,
                Mastery = mastery,
                IsUnlocked = unlockedIds.Contains(id)
            };
        }
    }

    private void LoadClassSkills(string className)
    {
        try
        {
            var resourceName = $"RebornSaga.Data.Skills.skills_{className}.json";
            using var stream = typeof(SkillService).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var skills = JsonSerializer.Deserialize<List<Skill>>(json, JsonOptions);
            if (skills == null) return;

            foreach (var skill in skills)
                _allSkills[skill.Id] = skill;
        }
        catch (Exception)
        {
            // Skills konnten nicht geladen werden - leere Skill-Liste
        }
    }
}
