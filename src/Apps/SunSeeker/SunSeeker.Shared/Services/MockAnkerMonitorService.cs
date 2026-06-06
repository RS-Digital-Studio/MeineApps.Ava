using System.Timers;
using SunSeeker.Shared.Models;
using Timer = System.Timers.Timer;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Simuliert die Solar-Eingangsleistung physikalisch plausibel aus dem aktuellen Sonnenstand:
/// Leistung skaliert mit dem Sinus der Sonnen-Elevation (Luftmassen-/Einstrahlungs-Näherung),
/// gedeckelt auf die Panel-Nennleistung, plus leichte "Wolken"-Schwankung. Nachts 0 W.
/// Ersetzt die (noch fehlende) echte Anker-Cloud-MQTT-Anbindung für Entwicklung + Demo.
/// </summary>
public sealed class MockAnkerMonitorService : IAnkerMonitorService, IDisposable
{
    private const double NominalWatts = 400.0;   // Anker PS400
    private const double SystemEfficiency = 0.82; // MPPT + Kabel + Temperatur

    private readonly ISolarPositionService _solar;
    private readonly ILocationService _location;
    private readonly Timer _timer = new(1000) { AutoReset = true };

    private double _cloudFactor = 1.0;

    public MockAnkerMonitorService(ISolarPositionService solar, ILocationService location)
    {
        _solar = solar;
        _location = location;
        _timer.Elapsed += OnTick;
    }

    public AnkerConnectionState State { get; private set; } = AnkerConnectionState.Disconnected;

    public double CurrentSolarWatts { get; private set; }

    public bool IsSimulated => true;

    public event EventHandler<PowerSample>? SampleReceived;

    public event EventHandler<AnkerConnectionState>? StateChanged;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        SetState(AnkerConnectionState.Connecting);
        SetState(AnkerConnectionState.Connected);
        _timer.Start();
        Emit(); // sofort einen ersten Wert liefern
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        _timer.Stop();
        SetState(AnkerConnectionState.Disconnected);
    }

    private void OnTick(object? sender, ElapsedEventArgs e) => Emit();

    private void Emit()
    {
        var location = _location.Current ?? new GeoLocation(52.52, 13.405, 38);
        var sun = _solar.GetPosition(location, DateTime.UtcNow);

        double watts = 0;
        if (sun.IsDaylight)
        {
            // Wolken-Faktor traege variieren (Random-Walk, geklemmt).
            _cloudFactor += (Random.Shared.NextDouble() - 0.5) * 0.08;
            _cloudFactor = Math.Clamp(_cloudFactor, 0.55, 1.0);

            var elevRad = sun.Elevation * Math.PI / 180.0;
            watts = NominalWatts * Math.Max(0, Math.Sin(elevRad)) * SystemEfficiency * _cloudFactor;
            watts = Math.Clamp(watts, 0, NominalWatts);
        }

        CurrentSolarWatts = watts;
        SampleReceived?.Invoke(this, new PowerSample(DateTime.UtcNow, watts));
    }

    private void SetState(AnkerConnectionState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Elapsed -= OnTick;
        _timer.Dispose();
    }
}
