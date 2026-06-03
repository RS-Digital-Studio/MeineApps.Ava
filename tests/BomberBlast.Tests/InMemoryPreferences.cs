using MeineApps.Core.Ava.Services;

namespace BomberBlast.Tests;

/// <summary>
/// In-Memory IPreferencesService für Service-Tests ohne Disk-IO.
/// Imitiert das public API mit reflection-freier Type-Erhaltung über object-Storage.
/// </summary>
public class InMemoryPreferences : IPreferencesService
{
    private readonly Dictionary<string, object?> _store = new();

    public T Get<T>(string key, T defaultValue)
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        _store[key] = value;
    }

    public bool ContainsKey(string key) => _store.ContainsKey(key);

    public void Remove(string key) => _store.Remove(key);

    public void Clear() => _store.Clear();

    // Persistenz-Gate: no-op fuer In-Memory (kein Disk-IO, das ausgesetzt werden muesste).
    public void SuspendPersistence() { }
    public void ResumePersistence() { }
    public void FlushPending() { }
}
