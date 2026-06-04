using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.Localization;
using SunSeeker.Shared.Graphics;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;

namespace SunSeeker.Shared.ViewModels;

/// <summary>
/// Leistungs-Tab: zeigt die Live-Solar-Eingangsleistung der Powerstation, einen Trend-Chart,
/// den kumulierten Tagesertrag (Wh) und die Spitzenleistung. Der Monitor wird nur bei aktivem
/// Tab betrieben (<see cref="Activate"/>/<see cref="Deactivate"/>).
/// </summary>
public partial class LivePowerViewModel : ObservableObject, IDisposable
{
    private const int WindowSize = 300; // ~5 Minuten bei 1 Hz

    private readonly IAnkerMonitorService _anker;
    private readonly ILocalizationService _loc;
    private readonly List<PowerSample> _samples = [];

    private double _peakWatts;
    private double _energyWh;
    private DateTime? _lastSampleUtc;

    public LivePowerViewModel(IAnkerMonitorService anker, ILocalizationService loc)
    {
        _anker = anker;
        _loc = loc;
        _anker.SampleReceived += OnSampleReceived;
        _anker.StateChanged += OnStateChanged;
        IsSimulated = anker.IsSimulated;
        _stateText = loc.GetString("StateDisconnected");
    }

    public event Action? InvalidateRequested;

    public PowerChartRenderer Renderer { get; } = new();

    public IReadOnlyList<PowerSample> Samples => _samples;

    [ObservableProperty] private string _currentWattsText = "—";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string _todayEnergyText = "0 Wh";
    [ObservableProperty] private string _peakWattsText = "0 W";
    [ObservableProperty] private bool _isSimulated;

    public void Activate() => _ = _anker.ConnectAsync();

    public void Deactivate() => _anker.Disconnect();

    private void OnStateChanged(object? sender, AnkerConnectionState state)
        => Dispatcher.UIThread.Post(() => StateText = StateLabel(state));

    private void OnSampleReceived(object? sender, PowerSample sample)
        => Dispatcher.UIThread.Post(() => AddSample(sample));

    private void AddSample(PowerSample sample)
    {
        // Energie integrieren (Wh) ueber das tatsaechliche Zeitintervall.
        if (_lastSampleUtc is { } last)
        {
            var hours = (sample.TimestampUtc - last).TotalHours;
            if (hours is > 0 and < 0.1) // Plausibilitaet (kein Riesensprung nach Reconnect)
                _energyWh += sample.SolarWatts * hours;
        }
        _lastSampleUtc = sample.TimestampUtc;

        _samples.Add(sample);
        if (_samples.Count > WindowSize)
            _samples.RemoveRange(0, _samples.Count - WindowSize);

        if (sample.SolarWatts > _peakWatts)
            _peakWatts = sample.SolarWatts;

        CurrentWattsText = $"{sample.SolarWatts:0} W";
        PeakWattsText = $"{_peakWatts:0} W";
        TodayEnergyText = _energyWh >= 1000 ? $"{_energyWh / 1000:0.00} kWh" : $"{_energyWh:0} Wh";

        InvalidateRequested?.Invoke();
    }

    private string StateLabel(AnkerConnectionState state) => _loc.GetString(state switch
    {
        AnkerConnectionState.Connected => IsSimulated ? "StateConnectedDemo" : "StateConnected",
        AnkerConnectionState.Connecting => "StateConnecting",
        AnkerConnectionState.Error => "StateError",
        _ => "StateDisconnected",
    });

    public void Dispose()
    {
        Deactivate();
        _anker.SampleReceived -= OnSampleReceived;
        _anker.StateChanged -= OnStateChanged;
        Renderer.Dispose();
    }
}
