using System.Collections.Concurrent;
using System.Text.Json;
using BingXBot.Contracts.Dto;

namespace BingXBot.Server.Services;

/// <summary>
/// Persistiert die von Clients registrierten FCM-Tokens (Firebase Cloud Messaging).
/// Datei: DataDirectory/fcm-devices.json. Tokens werden pro DeviceId gehalten.
/// </summary>
public sealed class FcmDeviceStore
{
    private readonly string _path;
    private readonly ILogger<FcmDeviceStore> _logger;
    private ConcurrentDictionary<string, FcmDeviceRegistrationDto> _devices = new();
    /// <summary>Phase 18 / F2 — In-Memory LastSeen pro DeviceId fuer Stale-Cleanup. Persistiert NICHT (DTO bleibt unveraendert) — bei Server-Restart wird auf "jetzt" zurueckgesetzt.</summary>
    private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();
    private readonly Lock _saveLock = new();

    public FcmDeviceStore(IConfiguration config, ILogger<FcmDeviceStore> logger)
    {
        _logger = logger;
        var dataDir = config.GetValue<string>("Server:DataDirectory");
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingXBot", "Server")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "bingxbot");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "fcm-devices.json");
        Load();
    }

    public IReadOnlyCollection<FcmDeviceRegistrationDto> AllDevices => _devices.Values.ToList();

    public void Register(FcmDeviceRegistrationDto device)
    {
        _devices[device.DeviceId] = device;
        _lastSeen[device.DeviceId] = DateTime.UtcNow; // Phase 18 / F2 — Tracking aktualisieren
        Save();
    }

    public void Unregister(string deviceId)
    {
        _devices.TryRemove(deviceId, out _);
        _lastSeen.TryRemove(deviceId, out _);
        Save();
    }

    /// <summary>
    /// Phase 18 / F2 — Markiert ein Device als "lebt" (z.B. nach erfolgreichem FCM-Send).
    /// Ohne aktiven Touch droht Pruning nach <see cref="PruneStaleDevices"/>-Aufruf.
    /// </summary>
    public void MarkSeen(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return;
        if (_devices.ContainsKey(deviceId))
            _lastSeen[deviceId] = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase 18 / F2 — Entfernt alle Devices, deren letzter "Kontakt" laenger als <paramref name="maxAge"/>
    /// her ist. Wird vom <c>FcmTokenCleanupService</c> periodisch aufgerufen — Default 30 Tage.
    /// Liefert die Anzahl entfernter Devices.
    /// </summary>
    public int PruneStaleDevices(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var removed = 0;
        foreach (var kvp in _devices)
        {
            // Wenn kein LastSeen → Default = "jetzt" beim Boot/Register, also nicht stale.
            // Pruning trifft nur Devices, die seit Boot keinen Touch (Send/Register) hatten.
            if (_lastSeen.TryGetValue(kvp.Key, out var lastSeen) && lastSeen < cutoff)
            {
                if (_devices.TryRemove(kvp.Key, out _))
                {
                    _lastSeen.TryRemove(kvp.Key, out _);
                    removed++;
                }
            }
        }
        if (removed > 0)
        {
            _logger.LogInformation("FCM-Token-Cleanup: {Removed} stale Devices entfernt (>{MaxAge} Tage inaktiv)",
                removed, maxAge.TotalDays);
            Save();
        }
        return removed;
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<FcmDeviceRegistrationDto>>(json) ?? new();
            _devices = new ConcurrentDictionary<string, FcmDeviceRegistrationDto>(
                list.ToDictionary(d => d.DeviceId));
            // Phase 18 / F2 — Beim Boot LastSeen mit "jetzt" initialisieren — so werden persistierte
            // Devices nicht direkt am ersten Pruning-Tick entfernt.
            var now = DateTime.UtcNow;
            foreach (var d in list) _lastSeen[d.DeviceId] = now;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "FCM-Devices laden fehlgeschlagen"); }
    }

    private void Save()
    {
        lock (_saveLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_devices.Values.ToList(),
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
                if (!OperatingSystem.IsWindows())
                {
                    try { File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "FCM-Devices speichern fehlgeschlagen"); }
        }
    }
}
