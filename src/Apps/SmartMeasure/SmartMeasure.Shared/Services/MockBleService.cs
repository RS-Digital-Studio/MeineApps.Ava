using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Simuliert einen Vermessungsstab für Desktop-Entwicklung ohne Hardware.
///
/// Thread-Safety:
/// - Random.Shared (.NET 6+) ist thread-safe — kein eigenes Locking nötig
/// - _isConnected als volatile für lock-freies Read im Timer-Callback
/// - _stateLock schützt alle Positions-Schreibzugriffe
/// - Dispose ist idempotent (mehrfach aufrufbar)
/// - Events werden auf Threadpool gefeuert — Consumer muss Dispatcher.UIThread.Post nutzen
/// </summary>
public sealed class MockBleService : IBleService, IDisposable
{
    private readonly object _stateLock = new();
    private Timer? _positionTimer;
    private volatile bool _isConnected;
    private volatile bool _isDisposed;

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
        if (_isDisposed) return Task.FromResult(new List<BleDevice>());

        return Task.FromResult(new List<BleDevice>
        {
            new() { Name = "SmartMeasure-Rover", Address = "AA:BB:CC:DD:EE:FF", Rssi = -45 },
            new() { Name = "SmartMeasure-Rover-2", Address = "11:22:33:44:55:66", Rssi = -72 }
        });
    }

    public Task ConnectAsync(BleDevice device)
    {
        if (_isDisposed) return Task.CompletedTask;

        lock (_stateLock)
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
        }

        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(4);
        AccuracyUpdated?.Invoke(1.5f, 2.1f);

        // Positions-Updates alle 500ms (2 Hz analog zum echten Rover)
        _positionTimer = new Timer(SimulatePositionUpdate, null, 0, 500);

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;

        lock (_stateLock)
        {
            _isConnected = false;
            CurrentState.IsConnected = false;
            CurrentState.FixQuality = 0;
        }

        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(0);
        return Task.CompletedTask;
    }

    public Task SetStabHeightAsync(float meters) => Task.CompletedTask;

    public Task ConfigureNtripAsync(NtripConfig config)
    {
        lock (_stateLock) { CurrentState.NtripStatus = 2; }
        StateChanged?.Invoke(CurrentState);
        return Task.CompletedTask;
    }

    public Task ConfigureWiFiAsync(string ssid, string password) => Task.CompletedTask;
    public Task CalibrateImuAsync() => Task.CompletedTask;

    /// <summary>Simuliert einen Punkt-Trigger (für UI-Test per Button)</summary>
    public void SimulatePointTrigger()
    {
        if (!_isConnected || _isDisposed) return;

        double lat, lon, alt;
        lock (_stateLock)
        {
            lat = _currentLat;
            lon = _currentLon;
            alt = _currentAlt;
            _pointCounter++;
        }

        // Random.Shared ist thread-safe seit .NET 6 — kein eigenes Lock nötig
        var rng = Random.Shared;
        var point = new SurveyPoint
        {
            Latitude = lat,
            Longitude = lon,
            Altitude = alt + (rng.NextDouble() - 0.5) * 2.0, // ±1m Variation
            HorizontalAccuracy = 1.2f + (float)rng.NextDouble() * 0.8f,
            VerticalAccuracy = 1.8f + (float)rng.NextDouble() * 1.2f,
            TiltAngle = (float)rng.NextDouble() * 5f,
            TiltAzimuth = (float)rng.NextDouble() * 360f,
            FixQuality = 4,
            SatelliteCount = 20 + rng.Next(8),
            MagAccuracy = 3,
            Timestamp = DateTime.UtcNow,
            Label = $"Punkt {_pointCounter}"
        };

        PointReceived?.Invoke(point);
    }

    private void SimulatePositionUpdate(object? state)
    {
        if (_isDisposed || !_isConnected) return;

        double lat, lon, alt;
        float hAcc, vAcc;
        var rng = Random.Shared;

        lock (_stateLock)
        {
            // Random Walk: Kleine Schwankungen simulieren (~2cm)
            _currentLat += (rng.NextDouble() - 0.5) * 0.0000004;
            _currentLon += (rng.NextDouble() - 0.5) * 0.0000004;
            _currentAlt = BaseAlt + (rng.NextDouble() - 0.5) * 0.05;
            lat = _currentLat;
            lon = _currentLon;
            alt = _currentAlt;

            CurrentState.TiltAngle = (float)rng.NextDouble() * 3f;
            hAcc = 1.2f + (float)rng.NextDouble() * 0.5f;
            vAcc = 1.8f + (float)rng.NextDouble() * 0.5f;
            CurrentState.HorizontalAccuracy = hAcc;
            CurrentState.VerticalAccuracy = vAcc;
            CurrentState.SatelliteCount = 20 + rng.Next(8);
        }

        PositionUpdated?.Invoke(lat, lon, alt);
        AccuracyUpdated?.Invoke(hAcc, vAcc);
        StateChanged?.Invoke(CurrentState);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _isConnected = false;
        _positionTimer?.Dispose();
        _positionTimer = null;
    }
}
