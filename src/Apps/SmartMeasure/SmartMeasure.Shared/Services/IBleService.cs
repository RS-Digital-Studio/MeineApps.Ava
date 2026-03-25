using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>BLE-Verbindung zum Vermessungsstab (plattform-spezifisch)</summary>
public interface IBleService
{
    /// <summary>Ist der Stab verbunden?</summary>
    bool IsConnected { get; }

    /// <summary>Aktueller Stab-Status</summary>
    StickState CurrentState { get; }

    /// <summary>Status hat sich geaendert</summary>
    event Action<StickState>? StateChanged;

    /// <summary>Neuer Messpunkt empfangen (Stab-Knopf gedrueckt, inkl. gepufferter Punkte)</summary>
    event Action<SurveyPoint>? PointReceived;

    /// <summary>Live-Position Update (2Hz)</summary>
    event Action<double, double, double>? PositionUpdated;

    /// <summary>Fix-Quality hat sich geaendert</summary>
    event Action<int>? FixQualityChanged;

    /// <summary>Genauigkeit hat sich geaendert (H-cm, V-cm)</summary>
    event Action<float, float>? AccuracyUpdated;

    /// <summary>BLE-Geraete scannen</summary>
    Task<List<BleDevice>> ScanAsync(CancellationToken ct);

    /// <summary>Mit Stab verbinden</summary>
    Task ConnectAsync(BleDevice device);

    /// <summary>Verbindung trennen</summary>
    Task DisconnectAsync();

    /// <summary>Stabhoehe setzen (fuer Neigungskorrektur)</summary>
    Task SetStabHeightAsync(float meters);

    /// <summary>NTRIP-Konfiguration an den Stab senden</summary>
    Task ConfigureNtripAsync(NtripConfig config);

    /// <summary>WiFi-Credentials an den Stab senden</summary>
    Task ConfigureWiFiAsync(string ssid, string password);

    /// <summary>BNO085 Magnetometer-Kalibrierung starten</summary>
    Task CalibrateImuAsync();
}

/// <summary>Ein gefundenes BLE-Geraet</summary>
public class BleDevice
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Rssi { get; set; }
}
