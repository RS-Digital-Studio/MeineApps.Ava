using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Simuliert einen Vermessungsstab fuer Desktop-Entwicklung ohne Hardware</summary>
public class MockBleService : IBleService, IDisposable
{
    private readonly Random _random = new();
    private Timer? _positionTimer;
    private bool _isConnected;

    // Simulierter Standort (Berlin Mitte)
    private const double BaseLat = 52.520008;
    private const double BaseLon = 13.404954;
    private const double BaseAlt = 34.5;
    private double _currentLat = BaseLat;
    private double _currentLon = BaseLon;
    private double _currentAlt = BaseAlt;
    private int _pointCounter;

    public bool IsConnected => _isConnected;
    public StickState CurrentState { get; } = new();

    public event Action<StickState>? StateChanged;
    public event Action<SurveyPoint>? PointReceived;
    public event Action<double, double, double>? PositionUpdated;
    public event Action<int>? FixQualityChanged;
    public event Action<float, float>? AccuracyUpdated;

    public Task<List<BleDevice>> ScanAsync(CancellationToken ct)
    {
        // Simuliert einen gefundenen Stab nach 1s
        return Task.FromResult(new List<BleDevice>
        {
            new() { Name = "SmartMeasure-Rover", Address = "AA:BB:CC:DD:EE:FF", Rssi = -45 },
            new() { Name = "SmartMeasure-Rover-2", Address = "11:22:33:44:55:66", Rssi = -72 }
        });
    }

    public Task ConnectAsync(BleDevice device)
    {
        _isConnected = true;
        CurrentState.IsConnected = true;
        CurrentState.BatteryLevel = 87;
        CurrentState.FixQuality = 4; // RTK Fix
        CurrentState.HorizontalAccuracy = 1.5f;
        CurrentState.VerticalAccuracy = 2.1f;
        CurrentState.SatelliteCount = 24;
        CurrentState.NtripStatus = 2; // Receiving
        CurrentState.MagAccuracy = 3;

        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(4);
        AccuracyUpdated?.Invoke(1.5f, 2.1f);

        // Positionsupdates starten (2Hz)
        _positionTimer = new Timer(SimulatePositionUpdate, null, 0, 500);

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
        _isConnected = false;
        CurrentState.IsConnected = false;
        CurrentState.FixQuality = 0;
        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(0);
        return Task.CompletedTask;
    }

    public Task SetStabHeightAsync(float meters)
    {
        // Mock: Einfach ignorieren
        return Task.CompletedTask;
    }

    public Task ConfigureNtripAsync(NtripConfig config)
    {
        // Mock: Simuliert NTRIP-Verbindung
        CurrentState.NtripStatus = 2;
        StateChanged?.Invoke(CurrentState);
        return Task.CompletedTask;
    }

    public Task ConfigureWiFiAsync(string ssid, string password)
    {
        return Task.CompletedTask;
    }

    public Task CalibrateImuAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>Simuliert einen Punkt-Trigger (fuer UI-Test per Button)</summary>
    public void SimulatePointTrigger()
    {
        if (!_isConnected) return;

        _pointCounter++;
        var point = new SurveyPoint
        {
            Latitude = _currentLat,
            Longitude = _currentLon,
            Altitude = _currentAlt + (_random.NextDouble() - 0.5) * 2.0, // ±1m Variation
            HorizontalAccuracy = 1.2f + (float)_random.NextDouble() * 0.8f,
            VerticalAccuracy = 1.8f + (float)_random.NextDouble() * 1.2f,
            TiltAngle = (float)_random.NextDouble() * 5f,
            TiltAzimuth = (float)_random.NextDouble() * 360f,
            FixQuality = 4,
            SatelliteCount = 20 + _random.Next(8),
            MagAccuracy = 3,
            Timestamp = DateTime.UtcNow,
            Label = $"Punkt {_pointCounter}"
        };

        PointReceived?.Invoke(point);
    }

    private void SimulatePositionUpdate(object? state)
    {
        // Random Walk: Kleine Schwankungen simulieren (±2cm)
        _currentLat += (_random.NextDouble() - 0.5) * 0.0000004;
        _currentLon += (_random.NextDouble() - 0.5) * 0.0000004;
        _currentAlt = BaseAlt + (_random.NextDouble() - 0.5) * 0.05;

        // Gelegentlich Neigung und Accuracy variieren
        CurrentState.TiltAngle = (float)_random.NextDouble() * 3f;
        CurrentState.HorizontalAccuracy = 1.2f + (float)_random.NextDouble() * 0.5f;
        CurrentState.VerticalAccuracy = 1.8f + (float)_random.NextDouble() * 0.5f;
        CurrentState.SatelliteCount = 20 + _random.Next(8);

        PositionUpdated?.Invoke(_currentLat, _currentLon, _currentAlt);
    }

    public void Dispose()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }
}
