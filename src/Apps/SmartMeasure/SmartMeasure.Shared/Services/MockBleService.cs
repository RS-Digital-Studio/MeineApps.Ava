using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Simuliert einen Vermessungsstab für Desktop-Entwicklung ohne Hardware.
///
/// Edge-Case-Simulationen (für Tests die sonst nur im Feld passieren):
/// - <see cref="CycleFixDegradation"/>: Fix 4 → 5 → 2 → 0 → 4 (RTK-Fix → Float → DGPS → NoFix)
/// - <see cref="SimulatePacketLoss"/>: Position-Updates für N Sekunden pausieren
/// - <see cref="SimulateBatteryDrain"/>: Battery-Drain beschleunigen bis 15%
/// - <see cref="SimulateMagLoss"/>: MagAccuracy auf 0 (Kompass-Warnung triggert)
/// - <see cref="SimulateSpuriousDisconnect"/>: Disconnect ohne User-Aktion
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

    // Edge-Case-Simulation State
    private int _fixCycleIndex; // 0=RTK-Fix, 1=Float, 2=DGPS, 3=NoFix
    private static readonly int[] FixCycle = [4, 5, 2, 0];
    private DateTime _packetLossUntil = DateTime.MinValue;
    private bool _batteryDrainActive;
    private int _magAccuracyOverride = -1; // -1 = default, sonst overridden

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
            _fixCycleIndex = 0;
            _batteryDrainActive = false;
            _magAccuracyOverride = -1;
            _packetLossUntil = DateTime.MinValue;
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
    public Task CalibrateImuAsync()
    {
        // Kalibrierung setzt MagAccuracy-Override zurück
        lock (_stateLock) { _magAccuracyOverride = -1; }
        return Task.CompletedTask;
    }

    /// <summary>Simuliert einen Punkt-Trigger (für UI-Test per Button)</summary>
    public void SimulatePointTrigger()
    {
        if (!_isConnected || _isDisposed) return;

        double lat, lon, alt;
        int fix, sats, mag;
        lock (_stateLock)
        {
            lat = _currentLat;
            lon = _currentLon;
            alt = _currentAlt;
            fix = CurrentState.FixQuality;
            sats = CurrentState.SatelliteCount;
            mag = _magAccuracyOverride >= 0 ? _magAccuracyOverride : CurrentState.MagAccuracy;
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
            FixQuality = fix,
            SatelliteCount = sats,
            MagAccuracy = mag,
            Timestamp = DateTime.UtcNow,
            Label = $"Punkt {_pointCounter}"
        };

        PointReceived?.Invoke(point);
    }

    /// <summary>Fix-Quality zyklisch degradieren: RTK-Fix → Float → DGPS → NoFix → zurück.
    /// Testet UI-Flow bei sich verschlechterndem Signal.</summary>
    public void CycleFixDegradation()
    {
        if (_isDisposed) return;

        int newFix;
        lock (_stateLock)
        {
            _fixCycleIndex = (_fixCycleIndex + 1) % FixCycle.Length;
            newFix = FixCycle[_fixCycleIndex];
            CurrentState.FixQuality = newFix;
            // Accuracy mit Fix-Quality korrelieren
            (CurrentState.HorizontalAccuracy, CurrentState.VerticalAccuracy) = newFix switch
            {
                4 => (1.5f, 2.1f),    // RTK-Fix
                5 => (8f, 15f),       // Float: deutlich schlechter
                2 => (80f, 150f),     // DGPS: meter-Bereich
                0 => (500f, 900f),    // NoFix: quasi unbrauchbar
                _ => (1.5f, 2.1f)
            };
        }
        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(newFix);
        AccuracyUpdated?.Invoke(CurrentState.HorizontalAccuracy, CurrentState.VerticalAccuracy);
    }

    /// <summary>Position-Updates für <paramref name="seconds"/> Sekunden einfrieren.
    /// Testet UI-Verhalten bei BLE-Packet-Loss / WiFi-Jitter.</summary>
    public void SimulatePacketLoss(int seconds)
    {
        if (_isDisposed) return;
        lock (_stateLock)
        {
            _packetLossUntil = DateTime.UtcNow.AddSeconds(seconds);
        }
    }

    /// <summary>Ab Aufruf sinkt die Battery ~3% pro Sekunde bis 15%. Testet Low-Battery-Warnung.</summary>
    public void SimulateBatteryDrain()
    {
        if (_isDisposed) return;
        lock (_stateLock)
        {
            _batteryDrainActive = true;
        }
    }

    /// <summary>Kompass-Genauigkeit künstlich schlecht setzen — triggert MagWarning in SurveyViewModel.
    /// Simuliert Metallumgebung (Zaun/Auto) oder unkalibrierte Sensoren.</summary>
    public void SimulateMagLoss()
    {
        if (_isDisposed) return;
        lock (_stateLock)
        {
            _magAccuracyOverride = 0;
            CurrentState.MagAccuracy = 0;
        }
        StateChanged?.Invoke(CurrentState);
    }

    /// <summary>Unerwarteter Disconnect ohne User-Aktion. Testet ob UI-Werte zurückgesetzt werden
    /// und Foreground-Service gestoppt wird.</summary>
    public void SimulateSpuriousDisconnect()
    {
        _ = DisconnectAsync();
    }

    private void SimulatePositionUpdate(object? state)
    {
        if (_isDisposed || !_isConnected) return;

        DateTime packetLossUntil;
        lock (_stateLock) { packetLossUntil = _packetLossUntil; }
        if (DateTime.UtcNow < packetLossUntil)
            return; // PacketLoss-Phase aktiv — keine Updates schicken

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

            // Battery-Drain
            if (_batteryDrainActive && CurrentState.BatteryLevel > 15)
            {
                // 500ms Timer → ~1.5% pro Tick = 3% pro Sekunde
                CurrentState.BatteryLevel = Math.Max(15, CurrentState.BatteryLevel - 2);
                if (CurrentState.BatteryLevel <= 15) _batteryDrainActive = false;
            }

            CurrentState.TiltAngle = (float)rng.NextDouble() * 3f;
            hAcc = CurrentState.HorizontalAccuracy;
            vAcc = CurrentState.VerticalAccuracy;
            CurrentState.SatelliteCount = CurrentState.FixQuality >= 4
                ? 20 + rng.Next(8)
                : Math.Max(4, CurrentState.SatelliteCount - (rng.Next(4) == 0 ? 1 : 0));

            if (_magAccuracyOverride >= 0)
                CurrentState.MagAccuracy = _magAccuracyOverride;
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
