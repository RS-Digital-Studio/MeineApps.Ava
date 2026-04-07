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
/// Android-BLE-Implementierung fuer die Verbindung zum SmartMeasure Rover-Stab.
/// Kommuniziert via BLE GATT mit dem ESP32-S3 auf dem Stab.
/// </summary>
public sealed class AndroidBleService : IBleService, IDisposable
{
    private readonly Activity _activity;
    private BluetoothAdapter? _adapter;
    private BluetoothGatt? _gatt;
    private bool _isConnected;
    private TaskCompletionSource<bool>? _connectTcs;

    // BLE UUIDs (muessen mit der ESP32-Firmware uebereinstimmen)
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

    public bool IsConnected => _isConnected;
    public StickState CurrentState { get; } = new();

    public event Action<StickState>? StateChanged;
    public event Action<SurveyPoint>? PointReceived;
    public event Action<double, double, double>? PositionUpdated;
    public event Action<int>? FixQualityChanged;
    public event Action<float, float>? AccuracyUpdated;

    public AndroidBleService(Activity activity)
    {
        _activity = activity;
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

        scanner.StartScan(callback);

        // Scan nach 5 Sekunden stoppen
        _ = Task.Delay(5000, ct).ContinueWith(_ =>
        {
            try { scanner.StopScan(callback); } catch { /* OK */ }
            tcs.TrySetResult(devices);
        }, ct);

        return tcs.Task;
    }

    public async Task ConnectAsync(BleDevice device)
    {
        if (_adapter == null) return;

        var bluetoothDevice = _adapter.GetRemoteDevice(device.Address);
        if (bluetoothDevice == null) return;

        _connectTcs = new TaskCompletionSource<bool>();
        var callback = new GattCallback(this);
        _gatt = bluetoothDevice.ConnectGatt(_activity, false, callback, BluetoothTransports.Le);

        // Warten bis Verbindung steht (max 10s)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (cts.Token.Register(() => _connectTcs.TrySetResult(false)))
        {
            await _connectTcs.Task;
        }
    }

    public Task DisconnectAsync()
    {
        _gatt?.Disconnect();
        _gatt?.Close();
        _gatt = null;
        _isConnected = false;
        CurrentState.IsConnected = false;
        CurrentState.FixQuality = 0;
        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(0);
        return Task.CompletedTask;
    }

    public Task SetStabHeightAsync(float meters)
    {
        return WriteConfigAsync($"STAB_HEIGHT:{meters:F2}");
    }

    public Task ConfigureNtripAsync(NtripConfig config)
    {
        return WriteConfigAsync(
            $"NTRIP:{config.Server}:{config.Port}/{config.Mountpoint}:{config.Username}:{config.Password}");
    }

    public Task ConfigureWiFiAsync(string ssid, string password)
    {
        return WriteConfigAsync($"WIFI:{ssid}:{password}");
    }

    public Task CalibrateImuAsync()
    {
        return WriteConfigAsync("IMU_CAL:START");
    }

    private Task WriteConfigAsync(string command)
    {
        if (_gatt == null || !_isConnected) return Task.CompletedTask;

        var service = _gatt.GetService(ServiceUuid);
        var characteristic = service?.GetCharacteristic(ConfigCharUuid);
        if (characteristic == null) return Task.CompletedTask;

        characteristic.SetValue(command);
        _gatt.WriteCharacteristic(characteristic);
        return Task.CompletedTask;
    }

    internal void OnConnected()
    {
        _isConnected = true;
        CurrentState.IsConnected = true;
        _connectTcs?.TrySetResult(true);
        _gatt?.DiscoverServices();
    }

    internal void OnDisconnected()
    {
        _isConnected = false;
        CurrentState.IsConnected = false;
        _connectTcs?.TrySetResult(false);
        StateChanged?.Invoke(CurrentState);
    }

    internal void OnServicesDiscovered()
    {
        // Notifications fuer Position, Punkte und Status aktivieren
        EnableNotification(PositionCharUuid);
        EnableNotification(PointCharUuid);
        EnableNotification(StatusCharUuid);
    }

    private void EnableNotification(Java.Util.UUID charUuid)
    {
        var service = _gatt?.GetService(ServiceUuid);
        var characteristic = service?.GetCharacteristic(charUuid);
        if (characteristic == null || _gatt == null) return;

        _gatt.SetCharacteristicNotification(characteristic, true);
        var descriptor = characteristic.GetDescriptor(
            Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb"));
        if (descriptor != null)
        {
            descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue?.ToArray());
            _gatt.WriteDescriptor(descriptor);
        }
    }

    internal void OnCharacteristicChanged(BluetoothGattCharacteristic characteristic)
    {
        var uuid = characteristic.Uuid;
        var data = characteristic.GetValue();
        if (data == null || data.Length == 0) return;

        if (uuid?.Equals(PositionCharUuid) == true)
            ParsePositionData(data);
        else if (uuid?.Equals(PointCharUuid) == true)
            ParsePointData(data);
        else if (uuid?.Equals(StatusCharUuid) == true)
            ParseStatusData(data);
    }

    /// <summary>Position-Paket parsen (24 Bytes: lat/lon/alt als double)</summary>
    private void ParsePositionData(byte[] data)
    {
        if (data.Length < 24) return;
        var lat = BitConverter.ToDouble(data, 0);
        var lon = BitConverter.ToDouble(data, 8);
        var alt = BitConverter.ToDouble(data, 16);
        PositionUpdated?.Invoke(lat, lon, alt);
    }

    /// <summary>Punkt-Paket parsen (vollstaendiger SurveyPoint vom Stab)</summary>
    private void ParsePointData(byte[] data)
    {
        if (data.Length < 48) return;
        var point = new SurveyPoint
        {
            Latitude = BitConverter.ToDouble(data, 0),
            Longitude = BitConverter.ToDouble(data, 8),
            Altitude = BitConverter.ToDouble(data, 16),
            HorizontalAccuracy = BitConverter.ToSingle(data, 24),
            VerticalAccuracy = BitConverter.ToSingle(data, 28),
            TiltAngle = BitConverter.ToSingle(data, 32),
            TiltAzimuth = BitConverter.ToSingle(data, 36),
            FixQuality = data[40],
            SatelliteCount = data[41],
            MagAccuracy = data[42],
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
        CurrentState.BatteryLevel = data[0];
        CurrentState.FixQuality = data[1];
        CurrentState.SatelliteCount = data[2];
        CurrentState.NtripStatus = data[3];
        CurrentState.MagAccuracy = data[4];
        CurrentState.HorizontalAccuracy = BitConverter.ToSingle(data, 5);
        CurrentState.VerticalAccuracy = BitConverter.ToSingle(data, 9);

        StateChanged?.Invoke(CurrentState);
        FixQualityChanged?.Invoke(CurrentState.FixQuality);
        AccuracyUpdated?.Invoke(CurrentState.HorizontalAccuracy, CurrentState.VerticalAccuracy);
    }

    public void Dispose()
    {
        _gatt?.Disconnect();
        _gatt?.Close();
        _gatt = null;
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
            if (devices.Any(d => d.Address == result.Device.Address)) return;

            devices.Add(new BleDevice
            {
                Name = result.Device.Name,
                Address = result.Device.Address!,
                Rssi = result.Rssi
            });
        }

        public override void OnScanFailed(ScanFailure errorCode)
        {
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

        public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status)
        {
            if (status == GattStatus.Success)
                service.OnServicesDiscovered();
        }

        public override void OnCharacteristicChanged(BluetoothGatt? gatt,
            BluetoothGattCharacteristic? characteristic)
        {
            if (characteristic != null)
                service.OnCharacteristicChanged(characteristic);
        }
    }

    #endregion
}
