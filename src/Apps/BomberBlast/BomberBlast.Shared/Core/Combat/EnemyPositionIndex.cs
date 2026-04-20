using BomberBlast.Models.Entities;

namespace BomberBlast.Core.Combat;

/// <summary>
/// Positions-Index fuer Gegner (v2.0.30+ Extract aus GameEngine.Collision.cs).
/// Vermeidet O(n)-Iteration pro Explosionszelle - Enemy-Hit-Test wird O(1).
///
/// Pure Data-Struktur ohne Game-State-Abhaengigkeit. Bosse belegen mehrere Zellen
/// und werden unter jeder Koordinate registriert.
///
/// Die Listen werden wiederverwendet (keine Per-Frame-Allokation), periodisch werden
/// verwaiste Keys entfernt (alle 120 Aufrufe).
/// </summary>
public sealed class EnemyPositionIndex
{
    private readonly Dictionary<(int, int), List<Enemy>> _cache = new();
    private int _cleanupCounter;

    /// <summary>
    /// Baut den Positions-Cache neu auf. Alte Listen werden geleert (nicht deallokiert).
    /// Bosse werden fuer jede ihrer Zellen registriert.
    /// </summary>
    public void Rebuild(IReadOnlyList<Enemy> enemies)
    {
        foreach (var list in _cache.Values)
            list.Clear();

        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (!enemy.IsActive || enemy.IsDying)
                continue;

            if (enemy is BossEnemy boss)
            {
                for (int bx = 0; bx < boss.BossSize; bx++)
                    for (int by = 0; by < boss.BossSize; by++)
                        Add((boss.GridX + bx, boss.GridY + by), boss);
            }
            else
            {
                Add((enemy.GridX, enemy.GridY), enemy);
            }
        }

        // Periodisches Cleanup verwaister Keys (Dict wuerde sonst unbegrenzt wachsen)
        if (++_cleanupCounter >= 120)
        {
            _cleanupCounter = 0;
            List<(int, int)>? toRemove = null;
            foreach (var kvp in _cache)
            {
                if (kvp.Value.Count == 0)
                {
                    toRemove ??= new List<(int, int)>();
                    toRemove.Add(kvp.Key);
                }
            }
            if (toRemove != null)
                foreach (var key in toRemove)
                    _cache.Remove(key);
        }
    }

    /// <summary>Lookup: Welche Enemies stehen auf (x, y)?</summary>
    public bool TryGetAt(int x, int y, out List<Enemy> enemies)
    {
        if (_cache.TryGetValue((x, y), out var list))
        {
            enemies = list;
            return true;
        }
        enemies = null!;
        return false;
    }

    /// <summary>Kompletter Reset bei Level-Wechsel (leert alle Buckets und Keys).</summary>
    public void Clear()
    {
        _cache.Clear();
        _cleanupCounter = 0;
    }

    private void Add((int, int) key, Enemy enemy)
    {
        if (!_cache.TryGetValue(key, out var list))
        {
            list = new List<Enemy>(4);
            _cache[key] = list;
        }
        list.Add(enemy);
    }
}
