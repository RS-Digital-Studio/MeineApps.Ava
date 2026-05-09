using System.Buffers.Binary;
using System.Collections.Concurrent;
using Android.Content;
using BluetoothAdapter = global::Android.Bluetooth.BluetoothAdapter;
using BluetoothDevice = global::Android.Bluetooth.BluetoothDevice;
using BluetoothGatt = global::Android.Bluetooth.BluetoothGatt;
using BluetoothGattCallback = global::Android.Bluetooth.BluetoothGattCallback;
using BluetoothGattCharacteristic = global::Android.Bluetooth.BluetoothGattCharacteristic;
using BluetoothGattDescriptor = global::Android.Bluetooth.BluetoothGattDescriptor;
using BluetoothManager = global::Android.Bluetooth.BluetoothManager;
using BluetoothTransports = global::Android.Bluetooth.BluetoothTransports;
using GattStatus = global::Android.Bluetooth.GattStatus;
using ProfileState = global::Android.Bluetooth.ProfileState;
using ScanCallbackType = global::Android.Bluetooth.LE.ScanCallbackType;
using ScanFailure = global::Android.Bluetooth.LE.ScanFailure;
using ScanResult = global::Android.Bluetooth.LE.ScanResult;
using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Android.Services;

/// <summary>
/// Android-BLE-Implementierung für die Verbindung zum SmartMeasure Rover-Stab.
/// Kommuniziert via BLE GATT mit dem ESP32-S3 auf dem Stab.
///
/// Produktions-Hardening:
/// - MTU-Request 247 (BLE 5.3 DLE Maximum — Default 23 reicht nicht für 48-Byte Point-Pakete)
/// - Write-Queue mit seriellem OnCharacteristicWrite-Acknowledgment
/// - Exponential-Backoff Reconnect (1s, 2s, 4s, max 10s)
/// - Endianness-explizite BitConverter-Alternative (little-endian ESP32)
/// - Thread-safe State via Lock
/// </summary>
public sealed class AndroidBleService : IBleService, IDisposable
{
    // MTU 247 = BLE 5.3 Maximum im Data-Length-Extension-Mode. Samsung S25 Ultra hat BT 5.3.
    // Mehr pro Notification = weniger Fragmentierung, höherer Durchsatz für Point-Pakete.
    private const int TargetMtu = 247;
    private const int ConnectTimeoutSeconds = 15;
    private const int MaxReconnectAttempts = 5;

    private readonly Activity _activity;
    private readonly IGeoidService _geoidService;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly ConcurrentQueue<string> _pendingWrites = new();

    /// <summary>Stablänge in Metern — wird für Tilt-Korrektur verwendet.</summary>
    public float StabHeightMeters { get; set; } = 1.5f;

    private BluetoothAdapter? _adapter;
    private BluetoothGatt? _gatt;
    private BluetoothDevice? _currentDevice;
    private volatile bool _isConnected;
    private volatile bool _isDisposed;
    private TaskCompletionSource<bool>? _connectTcs;
    private TaskCompletionSource<bool>? _writeAckTcs;
    private CancellationTokenSource? _reconnectCts;

    // BLE UUIDs (müssen mit der ESP32-Firmware übereinstimmen)
    private static readonly Java.Util.UUID ServiceUuid =
        Java.Util.UUID.FromString("4fafc201-1fb5-459e-8fcc-c5c9c331914b")!;
    private static readonly Java.Util.UUID PositionCharUuid =
        Java.Util.UUID.FromString("beb5483e-36e1-4688-b7f5-ea07361b26a8")!;
    private static readonly Java.Util.UUID PointCharUuid =
        Java.Util.UUID.FromString("beb5483e-36e1-4688-b7f5-ea07361b26a9")!;
    private static readonly Java.Util.UUID ConfigCharUuid =
        Java.Util.UUID.FromString("beb5483e-36e1-4688-b7f5-ea07361b26aa")!;
    private static readonly Java.Util.UUID StatusCharUuid =
        Java.Util.UUID.FromString("beb5483e-36e1-4688-b7f5-ea07361b26ab")!;
    private static readonly Java.Util.UUID CccdDescriptorUuid =
        Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb")!;

    public bool IsConnected => _isConnected;
    public StickState CurrentState { get; } = new();

    public event Action<StickState>? StateChanged;
    public event Action<SurveyPoint>? PointReceived;
    public event Action<double, double, double>? PositionUpdated;
    public event Action<int>? FixQualityChanged;
    public event Action<float, float>? AccuracyUpdated;

    public AndroidBleService(Activity activity, IGeoidService geoidService)
    {
        _activity = activity;
        _geoidService = geoidService;
        var manager = activity.GetSystemService(Context.BluetoothService) as BluetoothManager;
        _adapter = manager?.Adapter;
    }

    public Task<List<BleDevice>> ScanAsync(CancellationToken ct)
    {
        var devices = new List<BleDevice>();
        var scanner = _adapter?.BluetoothLeScanner;
        if (scanner == null) return Task.FromResult(devices);

        var tcs = new TaskCompletionSource<List<BleDevice>>();
        var callback = new ScanCallback(devices, tcs);

        try { scanner.StartScan(callback); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BLE Scan-Start fehlgeschlagen: {ex.Message}");
            return Task.FromResult(devices);
        }

        // Scan nach 5 Sekunden stoppen
        _ = Task.Delay(5000, ct).ContinueWith(_ =>
        {
            try { scanner.StopScan(callback); } catch { /* OK */ }
            tcs.TrySetResult(devices);
        }, ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        return tcs.Task;
    }

    public async Task ConnectAsync(BleDevice device)
    {
        if (_adapter == null || _isDisposed) return;

        var bluetoothDevice = _adapter.GetRemoteDevice(device.Address);
        if (bluetoothDevice == null) return;

        _currentDevice = bluetoothDevice;
        await DoConnectInternalAsync(bluetoothDevice);
    }

    private async Task<bool> DoConnectInternalAsync(BluetoothDevice device)
    {
        _connectTcs = new TaskCompletionSource<bool>();
        var callback = new GattCallback(this);

        try
        {
            _gatt = device.ConnectGatt(_activity, false, callback, BluetoothTransports.Le);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BLE ConnectGatt fehlgeschlagen: {ex.Message}");
            return false;
        }

        // Warten bis Verbindung steht (max 15s — ConnectGatt + MTU + Service Discovery)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectTimeoutSeconds));
        await using (cts.Token.Register(() => _connectTcs?.TrySetResult(false)))
        {
            return await _connectTcs.Task.ConfigureAwait(false);
        }
    }

    public Task DisconnectAsync()
    {
        CancelReconnect();

        lock (_stateLock)
        {
            _currentDevice = null;
            try { _gatt?.Disconnect(); } catch { /* OK */ }
            try { _gatt?.Close(); } catch { /* OK */ }
            _gatt = null;
            _isConnected = false;
            CurrentState.IsConnected = false;
            CurrentState.FixQuality = 0;
        }

        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(0);
        return Task.CompletedTask;
    }

    public Task SetStabHeightAsync(float meters)
    {
        // Lokal halten für App-seitige Tilt-Korrektur. Firmware bekommt die Länge auch,
        // damit sie sie z.B. für Settling-Validation nutzen kann.
        StabHeightMeters = meters;
        return EnqueueConfigWriteAsync($"STAB_HEIGHT:{meters:F2}");
    }

    public Task ConfigureNtripAsync(NtripConfig config)
        => EnqueueConfigWriteAsync(
            $"NTRIP:{config.Server}:{config.Port}/{config.Mountpoint}:{config.Username}:{config.Password}");

    public Task ConfigureWiFiAsync(string ssid, string password)
        => EnqueueConfigWriteAsync($"WIFI:{ssid}:{password}");

    public Task CalibrateImuAsync()
        => EnqueueConfigWriteAsync("IMU_CAL:START");

    /// <summary>
    /// Serialisiert BLE-Writes über SemaphoreSlim — BLE erlaubt nur 1 Write gleichzeitig,
    /// paralleles Senden überschreibt Werte auf Stack-Ebene.
    /// Wartet auf OnCharacteristicWrite-Callback bevor nächster Write startet.
    /// </summary>
    private async Task EnqueueConfigWriteAsync(string command)
    {
        if (_gatt == null || !_isConnected || _isDisposed)
        {
            System.Diagnostics.Debug.WriteLine($"BLE Write verworfen (nicht verbunden): {command}");
            return;
        }

        await _writeSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var gatt = _gatt;
            if (gatt == null || !_isConnected) return;

            var service = gatt.GetService(ServiceUuid);
            var characteristic = service?.GetCharacteristic(ConfigCharUuid);
            if (characteristic == null)
            {
                System.Diagnostics.Debug.WriteLine("BLE: Config-Characteristic nicht gefunden");
                return;
            }

            _writeAckTcs = new TaskCompletionSource<bool>();

            characteristic.SetValue(command);
            if (!gatt.WriteCharacteristic(characteristic))
            {
                System.Diagnostics.Debug.WriteLine($"BLE WriteCharacteristic abgelehnt: {command}");
                return;
            }

            // Auf OnCharacteristicWrite-Callback warten, max 3s
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await using (cts.Token.Register(() => _writeAckTcs?.TrySetResult(false)))
            {
                await _writeAckTcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _writeAckTcs = null;
            _writeSemaphore.Release();
        }
    }

    internal void OnConnected()
    {
        lock (_stateLock)
        {
            _isConnected = true;
            CurrentState.IsConnected = true;
        }

        // MTU 247 MUSS angefordert werden — Default 23 Bytes reicht nicht:
        // Position-Paket = 24 Bytes, Point-Paket = 48 Bytes → würde abgeschnitten
        var gatt = _gatt;
        if (gatt != null && !gatt.RequestMtu(TargetMtu))
        {
            // MTU-Request abgelehnt → direkt Service-Discovery starten
            System.Diagnostics.Debug.WriteLine("BLE: MTU-Request abgelehnt, fahre mit Default-MTU fort");
            gatt.DiscoverServices();
        }
        // Sonst: OnMtuChanged ruft DiscoverServices() auf
    }

    internal void OnMtuChanged(int mtu, bool success)
    {
        System.Diagnostics.Debug.WriteLine($"BLE MTU geändert: {mtu} (Erfolg: {success})");
        // Service-Discovery erst nach MTU-Verhandlung starten — sonst werden
        // Notifications mit alter MTU initialisiert
        _gatt?.DiscoverServices();
    }

    internal void OnDisconnected()
    {
        lock (_stateLock)
        {
            _isConnected = false;
            CurrentState.IsConnected = false;
        }

        _connectTcs?.TrySetResult(false);
        _writeAckTcs?.TrySetResult(false);

        StateChanged?.Invoke(CurrentState);

        // Nicht-initiierter Disconnect → Reconnect versuchen
        if (!_isDisposed && _currentDevice != null)
            _ = TryReconnectAsync();
    }

    /// <summary>
    /// Exponential-Backoff-Reconnect: 1s → 2s → 4s → 8s → 10s.
    /// Wird automatisch bei OnDisconnected aufgerufen wenn ein Gerät verbunden war.
    /// </summary>
    private async Task TryReconnectAsync()
    {
        CancelReconnect();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        for (var attempt = 1; attempt <= MaxReconnectAttempts && !token.IsCancellationRequested; attempt++)
        {
            var delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt - 1), 10000);
            System.Diagnostics.Debug.WriteLine($"BLE Reconnect-Versuch {attempt}/{MaxReconnectAttempts} in {delayMs}ms");

            try { await Task.Delay(delayMs, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            var device = _currentDevice;
            if (device == null || _isDisposed) return;

            // Alte GATT-Ressourcen freigeben
            try { _gatt?.Close(); } catch { /* OK */ }
            _gatt = null;

            var connected = await DoConnectInternalAsync(device).ConfigureAwait(false);
            if (connected) return;
        }

        System.Diagnostics.Debug.WriteLine("BLE Reconnect aufgegeben nach max. Versuchen");
    }

    private void CancelReconnect()
    {
        try { _reconnectCts?.Cancel(); } catch { /* OK */ }
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    internal void OnServicesDiscovered()
    {
        // Notifications für Position, Punkte und Status aktivieren
        EnableNotification(PositionCharUuid);
        EnableNotification(PointCharUuid);
        EnableNotification(StatusCharUuid);

        // Verbindung erst JETZT als "fertig" markieren — nach Services + MTU
        _connectTcs?.TrySetResult(true);
    }

    private void EnableNotification(Java.Util.UUID charUuid)
    {
        var gatt = _gatt;
        if (gatt == null) return;

        var service = gatt.GetService(ServiceUuid);
        var characteristic = service?.GetCharacteristic(charUuid);
        if (characteristic == null) return;

        gatt.SetCharacteristicNotification(characteristic, true);

        var descriptor = characteristic.GetDescriptor(CccdDescriptorUuid);
        if (descriptor != null)
        {
            descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue?.ToArray());
            gatt.WriteDescriptor(descriptor);
        }
    }

    internal void OnCharacteristicChanged(BluetoothGattCharacteristic characteristic)
    {
        if (_isDisposed) return;

        var uuid = characteristic.Uuid;
        var data = characteristic.GetValue();
        if (data == null || data.Length == 0) return;

        try
        {
            if (uuid?.Equals(PositionCharUuid) == true)
                ParsePositionData(data);
            else if (uuid?.Equals(PointCharUuid) == true)
                ParsePointData(data);
            else if (uuid?.Equals(StatusCharUuid) == true)
                ParseStatusData(data);
        }
        catch (Exception ex)
        {
            // Defekte Pakete dürfen die Verbindung nicht killen
            System.Diagnostics.Debug.WriteLine($"BLE Parse-Fehler: {ex.Message}");
        }
    }

    internal void OnCharacteristicWriteCompleted(GattStatus status)
    {
        _writeAckTcs?.TrySetResult(status == GattStatus.Success);
    }

    /// <summary>Position-Paket parsen (24 Bytes: lat/lon/alt als double, little-endian).
    /// Altitude wird als WGS84-Ellipsoid-Höhe interpretiert und via IGeoidService zu NN korrigiert.</summary>
    private void ParsePositionData(byte[] data)
    {
        if (data.Length < 24) return;
        // ESP32 ist little-endian. BinaryPrimitives garantiert explizite Endianness,
        // unabhängig von Host-Architektur.
        var span = data.AsSpan();
        var lat = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(0, 8));
        var lon = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(8, 8));
        var altEllipsoid = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(16, 8));

        // Ellipsoid → NN (MSL). Wenn Firmware bereits MSL sendet: _geoidService.IsClientCorrectionEnabled=false
        var altMsl = _geoidService.EllipsoidToGeoid(lat, lon, altEllipsoid);
        PositionUpdated?.Invoke(lat, lon, altMsl);
    }

    /// <summary>
    /// Punkt-Paket parsen.
    /// Byte-Layout (little-endian):
    ///   [ 0.. 7] Latitude   (double)   — Antennen-Position WGS84
    ///   [ 8..15] Longitude  (double)   — Antennen-Position WGS84
    ///   [16..23] Altitude   (double)   — Antennen-Höhe WGS84-Ellipsoid
    ///   [24..27] HorizontalAccuracy (float)
    ///   [28..31] VerticalAccuracy   (float)
    ///   [32..35] TiltAngle          (float) — Grad von Vertikal (0 = Stab senkrecht)
    ///   [36..39] TiltAzimuth        (float) — True-Heading der Neigungsrichtung in Grad
    ///   [40]     FixQuality         (byte)
    ///   [41]     SatelliteCount     (byte)
    ///   [42]     MagAccuracy        (byte) — 0 = unkalibriert, 3 = optimal
    ///   [43..47] Padding / Reserved (5 Bytes, z.B. für zukünftige Flags)
    ///
    /// Transformation Antenne → Bodenpunkt (Stabspitze):
    /// 1. Tilt-Korrektur (wenn MagAccuracy ≥ 2): horizontaler Versatz der Spitze relativ zur Antenne.
    ///    Bei 1.8m Stab + 5° Neigung: ≈16cm Versatz — sabotiert ±2cm RTK ohne Korrektur.
    /// 2. Höhen-Korrektur: Höhe der Spitze = Antennen-Höhe − stabHeight · cos(tilt).
    /// 3. Ellipsoid → Geoid (NN) über <see cref="IGeoidService"/>.
    /// </summary>
    private void ParsePointData(byte[] data)
    {
        if (data.Length < 43) return;
        var span = data.AsSpan();

        var antLat = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(0, 8));
        var antLon = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(8, 8));
        var antAltEllipsoid = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(16, 8));
        var hAccuracy = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(24, 4));
        var vAccuracy = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(28, 4));
        var tiltAngleDeg = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(32, 4));
        var tiltAzimuthDeg = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(36, 4));
        var fixQuality = data[40];
        var satCount = data[41];
        var magAccuracy = data[42];

        // Antenne → Stabspitze (Tilt-Korrektur).
        // Vertikal (cos) ist immer gültig, horizontal (sin · Azimuth) nur bei kalibriertem Kompass.
        var tiltRad = tiltAngleDeg * Math.PI / 180.0;
        var cosT = Math.Cos(tiltRad);
        var sinT = Math.Sin(tiltRad);

        var tipAltEllipsoid = antAltEllipsoid - StabHeightMeters * cosT;
        var tipLat = antLat;
        var tipLon = antLon;

        if (magAccuracy >= 2 && sinT > 1e-6)
        {
            // Konvention: TiltAzimuth = True-Heading der Antenne relativ zur Spitze.
            // Die Spitze steht am Boden fest, die Antenne bewegt sich beim Kippen in
            // diese Richtung (z.B. Azimuth=0° → Antenne kippt nach Norden, Spitze
            // bleibt auf der gleichen Bodenposition).
            //
            // Daraus folgt: die Antennen-Position ist um den Offset nach Azimuth
            // gegenüber der Spitze versetzt. Für die Spitze müssen wir also:
            //     tip = antenne - offset(azimuth)
            // Annahme: TiltAzimuth ist True-Heading (Firmware korrigiert Magnetic-Deklination).
            var azRad = tiltAzimuthDeg * Math.PI / 180.0;
            var horizontalOffset = StabHeightMeters * sinT;
            var deltaNorth = horizontalOffset * Math.Cos(azRad);
            var deltaEast = horizontalOffset * Math.Sin(azRad);

            // Meter → Grad (UTM wäre präziser, aber bei <10cm Offset ist 111320-Approx akzeptabel).
            const double metersPerDegLat = 111320.0;
            var metersPerDegLon = 111320.0 * Math.Cos(antLat * Math.PI / 180.0);

            tipLat = antLat - deltaNorth / metersPerDegLat;
            tipLon = antLon - deltaEast / metersPerDegLon;
        }

        // Ellipsoid → NN
        var tipAltMsl = _geoidService.EllipsoidToGeoid(tipLat, tipLon, tipAltEllipsoid);

        var point = new SurveyPoint
        {
            Latitude = tipLat,
            Longitude = tipLon,
            Altitude = tipAltMsl,
            HorizontalAccuracy = hAccuracy,
            VerticalAccuracy = vAccuracy,
            TiltAngle = tiltAngleDeg,
            TiltAzimuth = tiltAzimuthDeg,
            FixQuality = fixQuality,
            SatelliteCount = satCount,
            MagAccuracy = magAccuracy,
            Timestamp = DateTime.UtcNow,
        };
        PointReceived?.Invoke(point);
    }

    /// <summary>
    /// Status-Paket parsen (13 Bytes):
    /// [0] BatteryLevel, [1] FixQuality, [2] SatelliteCount, [3] NtripStatus,
    /// [4] MagAccuracy, [5..8] HorizontalAccuracy (float), [9..12] VerticalAccuracy (float)
    /// </summary>
    private void ParseStatusData(byte[] data)
    {
        if (data.Length < 13) return;
        var span = data.AsSpan();
        CurrentState.BatteryLevel = data[0];
        CurrentState.FixQuality = data[1];
        CurrentState.SatelliteCount = data[2];
        CurrentState.NtripStatus = data[3];
        CurrentState.MagAccuracy = data[4];
        CurrentState.HorizontalAccuracy = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(5, 4));
        CurrentState.VerticalAccuracy = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(9, 4));

        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(CurrentState.FixQuality);
        AccuracyUpdated?.Invoke(CurrentState.HorizontalAccuracy, CurrentState.VerticalAccuracy);
    }

    public void Dispose()
    {
        _isDisposed = true;
        CancelReconnect();

        try { _gatt?.Disconnect(); } catch { /* OK */ }
        try { _gatt?.Close(); } catch { /* OK */ }
        _gatt = null;
        _currentDevice = null;

        _writeSemaphore.Dispose();
    }

    #region BLE Callbacks

    private sealed class ScanCallback(List<BleDevice> devices, TaskCompletionSource<List<BleDevice>> tcs)
        : global::Android.Bluetooth.LE.ScanCallback
    {
        public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
        {
            if (result?.Device?.Name == null) return;
            if (!result.Device.Name.StartsWith("SmartMeasure")) return;

            // Duplikate vermeiden
            lock (devices)
            {
                if (devices.Any(d => d.Address == result.Device.Address)) return;
                devices.Add(new BleDevice
                {
                    Name = result.Device.Name,
                    Address = result.Device.Address!,
                    Rssi = result.Rssi
                });
            }
        }

        public override void OnScanFailed(ScanFailure errorCode)
        {
            System.Diagnostics.Debug.WriteLine($"BLE ScanFailure: {errorCode}");
            tcs.TrySetResult(devices);
        }
    }

    private sealed class GattCallback(AndroidBleService service) : BluetoothGattCallback
    {
        public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
        {
            if (newState == ProfileState.Connected)
                service.OnConnected();
            else if (newState == ProfileState.Disconnected)
                service.OnDisconnected();
        }

        public override void OnMtuChanged(BluetoothGatt? gatt, int mtu, GattStatus status)
        {
            service.OnMtuChanged(mtu, status == GattStatus.Success);
        }

        public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status)
        {
            if (status == GattStatus.Success)
                service.OnServicesDiscovered();
            else
                System.Diagnostics.Debug.WriteLine($"BLE ServiceDiscovery fehlgeschlagen: {status}");
        }

        public override void OnCharacteristicChanged(BluetoothGatt? gatt,
            BluetoothGattCharacteristic? characteristic)
        {
            if (characteristic != null)
                service.OnCharacteristicChanged(characteristic);
        }

        public override void OnCharacteristicWrite(BluetoothGatt? gatt,
            BluetoothGattCharacteristic? characteristic, GattStatus status)
        {
            service.OnCharacteristicWriteCompleted(status);
        }
    }

    #endregion
}
