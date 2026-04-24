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
        Save();
    }

    public void Unregister(string deviceId)
    {
        _devices.TryRemove(deviceId, out _);
        Save();
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
