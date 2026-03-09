namespace RebornSaga.Services;

using RebornSaga.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Verwaltet NPC-Affinitäten (Bond-System, 5 Stufen).
/// Trackt Punkte, Bond-Level und gesehene Bond-Szenen.
/// </summary>
public class AffinityService
{
    // NPC-ID → Affinitäts-Daten
    private readonly Dictionary<string, AffinityData> _affinities = new();

    /// <summary>
    /// Event wenn ein Bond-Level aufsteigt (npcId, neues Level).
    /// </summary>
    public event Action<string, int>? BondLevelUp;

    /// <summary>
    /// Initialisiert die Affinitäten für alle NPCs.
    /// </summary>
    public void Initialize()
    {
        _affinities.Clear();
        foreach (var npcId in new[] { "aria", "aldric", "kael", "luna", "vex" })
        {
            _affinities[npcId] = new AffinityData
            {
                NpcId = npcId,
                Points = 0,
                BondLevel = 1
            };
        }
    }

    /// <summary>
    /// Fügt Affinitäts-Punkte hinzu und prüft auf Level-Up.
    /// </summary>
    public bool AddPoints(string npcId, int points)
    {
        if (!_affinities.TryGetValue(npcId, out var data)) return false;

        data.Points += points;
        var newLevel = AffinityData.CalculateLevel(data.Points);

        if (newLevel > data.BondLevel)
        {
            data.BondLevel = newLevel;
            BondLevelUp?.Invoke(npcId, newLevel);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gibt die Affinitäts-Daten für einen NPC zurück.
    /// </summary>
    public AffinityData? GetAffinity(string npcId)
    {
        _affinities.TryGetValue(npcId, out var data);
        return data;
    }

    /// <summary>
    /// Gibt das Bond-Level eines NPCs zurück (1-5).
    /// </summary>
    public int GetBondLevel(string npcId)
    {
        return _affinities.TryGetValue(npcId, out var data) ? data.BondLevel : 1;
    }

    /// <summary>
    /// Markiert eine Bond-Szene als gesehen.
    /// </summary>
    public void MarkSceneSeen(string npcId, int sceneLevel)
    {
        if (_affinities.TryGetValue(npcId, out var data))
            data.SeenScenes.Add(sceneLevel);
    }

    /// <summary>
    /// Prüft ob eine Bond-Szene verfügbar aber noch nicht gesehen ist.
    /// </summary>
    public bool HasUnseenScene(string npcId)
    {
        if (!_affinities.TryGetValue(npcId, out var data)) return false;
        // Bond-Szenen gibt es ab Level 2
        for (int level = 2; level <= data.BondLevel; level++)
        {
            if (!data.SeenScenes.Contains(level))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gibt alle Affinitäts-Daten zurück (für Save).
    /// </summary>
    public Dictionary<string, AffinityData> GetAllAffinities() => new(_affinities);

    /// <summary>
    /// Stellt Affinitäten aus einem Save wieder her.
    /// </summary>
    public void RestoreAffinities(Dictionary<string, AffinityData> data)
    {
        _affinities.Clear();
        foreach (var (id, affinity) in data)
            _affinities[id] = affinity;
    }
}
