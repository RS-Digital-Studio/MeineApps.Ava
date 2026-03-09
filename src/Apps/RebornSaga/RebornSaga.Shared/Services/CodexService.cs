namespace RebornSaga.Services;

using RebornSaga.Models;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Verwaltet den Kodex (Bestiary, Lore, Charakter-Profile).
/// Einträge werden durch Story-Fortschritt und Entdeckungen freigeschaltet.
/// </summary>
public class CodexService
{
    private readonly Dictionary<string, CodexEntry> _entries = new();

    /// <summary>
    /// Registriert einen Kodex-Eintrag.
    /// </summary>
    public void RegisterEntry(string id, string categoryKey, string titleKey, string contentKey)
    {
        _entries[id] = new CodexEntry
        {
            Id = id,
            CategoryKey = categoryKey,
            TitleKey = titleKey,
            ContentKey = contentKey,
            IsUnlocked = false
        };
    }

    /// <summary>
    /// Schaltet einen Kodex-Eintrag frei. Gibt true zurück wenn neu freigeschaltet.
    /// </summary>
    public bool UnlockEntry(string id)
    {
        if (!_entries.TryGetValue(id, out var entry)) return false;
        if (entry.IsUnlocked) return false;
        entry.IsUnlocked = true;
        return true;
    }

    /// <summary>
    /// Gibt alle freigeschalteten Einträge einer Kategorie zurück.
    /// </summary>
    public List<CodexEntry> GetEntriesByCategory(string categoryKey)
    {
        return _entries.Values
            .Where(e => e.IsUnlocked && e.CategoryKey == categoryKey)
            .ToList();
    }

    /// <summary>
    /// Gibt alle freigeschalteten Einträge zurück.
    /// </summary>
    public List<CodexEntry> GetUnlockedEntries()
    {
        return _entries.Values.Where(e => e.IsUnlocked).ToList();
    }

    /// <summary>
    /// Gibt die Gesamtanzahl und freigeschaltete Anzahl zurück.
    /// </summary>
    public (int total, int unlocked) GetProgress()
    {
        return (_entries.Count, _entries.Values.Count(e => e.IsUnlocked));
    }

    /// <summary>
    /// Gibt alle verfügbaren Kategorien zurück.
    /// </summary>
    public List<string> GetCategories()
    {
        return _entries.Values
            .Select(e => e.CategoryKey)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Stellt den Zustand aus einem Save wieder her.
    /// </summary>
    public void RestoreUnlocked(HashSet<string> unlockedIds)
    {
        foreach (var entry in _entries.Values)
            entry.IsUnlocked = unlockedIds.Contains(entry.Id);
    }

    /// <summary>
    /// Gibt alle freigeschalteten IDs zurück (für Save).
    /// </summary>
    public HashSet<string> GetUnlockedIds()
    {
        return new HashSet<string>(_entries.Values.Where(e => e.IsUnlocked).Select(e => e.Id));
    }
}
