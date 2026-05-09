namespace BomberBlast.Core.Audio;

/// <summary>
/// Multi-Sample-Pool für SFX-Variations. Brawl-Stars-Pattern:
/// Statt 1 fester Sound wird zufällig aus 3-5 Variations gewählt + Anti-Repeat-Cooldown.
///
/// <para>Beispiel: <c>place_bomb</c> hat <c>place_bomb_a / _b / _c / _d</c>. Pool returnt
/// einen davon pro Aufruf, nie zweimal hintereinander den gleichen.</para>
///
/// <para>Sample-Naming-Konvention: Der Basis-Key ist der gleiche wie vorher
/// (<c>SoundManager.SFX_PLACE_BOMB = "place_bomb"</c>). Variations werden via
/// Suffix <c>_a, _b, _c, ...</c> gebildet.</para>
/// </summary>
public sealed class SoundVariationPool
{
    private readonly Dictionary<string, string[]> _variants = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lastIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();

    /// <summary>
    /// Registriert einen Pool für einen Basis-Key.
    /// </summary>
    /// <param name="baseKey">Basis-Key (z.B. <c>"place_bomb"</c>).</param>
    /// <param name="variantSuffixes">
    /// Suffix-Liste (z.B. <c>"a","b","c","d"</c>). Resultierende Keys werden zu
    /// <c>place_bomb_a, place_bomb_b, ...</c> gebaut. Leere Liste = Pool ist no-op und
    /// fällt auf den Basis-Key zurück.
    /// </param>
    public void RegisterPool(string baseKey, params string[] variantSuffixes)
    {
        if (string.IsNullOrEmpty(baseKey)) return;

        if (variantSuffixes.Length == 0)
        {
            _variants[baseKey] = new[] { baseKey };
            return;
        }

        var keys = new string[variantSuffixes.Length];
        for (int i = 0; i < variantSuffixes.Length; i++)
            keys[i] = $"{baseKey}_{variantSuffixes[i]}";
        _variants[baseKey] = keys;
        _lastIndex[baseKey] = -1;
    }

    /// <summary>
    /// Liefert einen zufälligen Variant-Key aus dem Pool. Verhindert direkte Wiederholung
    /// (lastIndex-Tracking). Bei Pool mit nur 1 Variant wird dieser zurückgegeben.
    /// Bei unbekanntem Basis-Key wird der Basis-Key selbst zurückgegeben (Fallback).
    /// </summary>
    public string PickVariant(string baseKey)
    {
        if (!_variants.TryGetValue(baseKey, out var pool) || pool.Length == 0)
            return baseKey;
        if (pool.Length == 1)
            return pool[0];

        var lastIdx = _lastIndex.TryGetValue(baseKey, out var li) ? li : -1;
        int idx;
        // Bis zu 3 Versuche um Repeat zu vermeiden — bei 2-Pool reicht 1 Re-Roll.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            idx = _random.Next(pool.Length);
            if (idx != lastIdx)
            {
                _lastIndex[baseKey] = idx;
                return pool[idx];
            }
        }
        // Fallback: nimm "next" relativ zu lastIdx
        idx = (lastIdx + 1) % pool.Length;
        _lastIndex[baseKey] = idx;
        return pool[idx];
    }

    /// <summary>
    /// Gibt alle registrierten Variant-Keys zurück (für Preload-Schritt).
    /// Inkludiert auch Basis-Keys ohne Pool nicht — die werden separat geladen.
    /// </summary>
    public IEnumerable<string> EnumerateAllVariantKeys()
    {
        foreach (var arr in _variants.Values)
            foreach (var k in arr)
                yield return k;
    }

    /// <summary>
    /// Liefert die Pool-Größe für einen Basis-Key (1 wenn nicht registriert).
    /// </summary>
    public int GetPoolSize(string baseKey)
        => _variants.TryGetValue(baseKey, out var arr) ? arr.Length : 1;
}
