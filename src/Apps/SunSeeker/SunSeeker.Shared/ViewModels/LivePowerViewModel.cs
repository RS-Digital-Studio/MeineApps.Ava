using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using SunSeeker.Shared.Graphics;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;
using SunSeeker.Shared.Services.Anker;

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
    private readonly IPreferencesService _prefs;
    private readonly List<PowerSample> _samples = [];

    private double _peakWatts;
    private double _energyWh;
    private DateTime? _lastSampleUtc;

    public LivePowerViewModel(IAnkerMonitorService anker, ILocalizationService loc, IPreferencesService prefs)
    {
        _anker = anker;
        _loc = loc;
        _prefs = prefs;
        _anker.SampleReceived += OnSampleReceived;
        _anker.StateChanged += OnStateChanged;
        IsSimulated = anker.IsSimulated;
        _stateText = loc.GetString("StateDisconnected");

        var creds = AnkerCredentialStore.Load(prefs);
        _ankerEmail = creds?.Email ?? "";
        _ankerPassword = creds?.Password ?? "";
        _ankerCountry = creds?.CountryId ?? "DE";
        _isConfigured = AnkerCredentialStore.Has(prefs);
        _showAnkerSettings = !_isConfigured;
    }

    public event Action? InvalidateRequested;

    public PowerChartRenderer Renderer { get; } = new();

    public IReadOnlyList<PowerSample> Samples => _samples;

    [ObservableProperty] private string _currentWattsText = "—";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string _todayEnergyText = "0 Wh";
    [ObservableProperty] private string _peakWattsText = "0 W";
    [ObservableProperty] private bool _isSimulated;

    // Anker-Zugangsdaten + Verbindungssteuerung
    [ObservableProperty] private string _ankerEmail;
    [ObservableProperty] private string _ankerPassword;
    [ObservableProperty] private string _ankerCountry;
    [ObservableProperty] private bool _isConfigured;
    [ObservableProperty] private bool _showAnkerSettings;
    [ObservableProperty] private string _ankerErrorText = "";
    [ObservableProperty] private bool _hasAnkerError;

    partial void OnAnkerErrorTextChanged(string value) => HasAnkerError = !string.IsNullOrWhiteSpace(value);

    public void Activate() => _ = _anker.ConnectAsync();

    public void Deactivate() => _anker.Disconnect();

    /// <summary>Blendet das Zugangsdaten-Formular ein/aus.</summary>
    [RelayCommand]
    private void ToggleAnkerSettings() => ShowAnkerSettings = !ShowAnkerSettings;

    /// <summary>Speichert die Zugangsdaten und verbindet neu (echte Anker-Cloud + MQTT).</summary>
    [RelayCommand]
    private void ConnectAnker()
    {
        if (string.IsNullOrWhiteSpace(AnkerEmail) || string.IsNullOrWhiteSpace(AnkerPassword))
        {
            AnkerErrorText = _loc.GetString("AnkerErrorMissing");
            return;
        }

        AnkerCredentialStore.Save(_prefs, new AnkerCredentials(
            AnkerEmail, AnkerPassword, string.IsNullOrWhiteSpace(AnkerCountry) ? "DE" : AnkerCountry));
        IsConfigured = true;
        ShowAnkerSettings = false;
        AnkerErrorText = "";
        Reconnect();
    }

    /// <summary>Verwirft die Zugangsdaten und fällt auf den Demo-Modus zurück.</summary>
    [RelayCommand]
    private void ForgetAnker()
    {
        AnkerCredentialStore.Clear(_prefs);
        AnkerPassword = "";
        IsConfigured = false;
        ShowAnkerSettings = true;
        AnkerErrorText = "";
        Reconnect();
    }

    private void Reconnect()
    {
        _anker.Disconnect();
        _ = _anker.ConnectAsync();
    }

    private void OnStateChanged(object? sender, AnkerConnectionState state)
        => Dispatcher.UIThread.Post(() =>
        {
            IsSimulated = _anker.IsSimulated;
            StateText = StateLabel(state);
            AnkerErrorText = state == AnkerConnectionState.Error && _anker is AnkerMonitorService { LastError: { } err }
                ? err
                : "";
        });

    private void OnSampleReceived(object? sender, PowerSample sample)
        => Dispatcher.UIThread.Post(() => AddSample(sample));

    private void AddSample(PowerSample sample)
    {
        // Energie integrieren (Wh) über das tatsächliche Zeitintervall.
        if (_lastSampleUtc is { } last)
        {
            var hours = (sample.TimestampUtc - last).TotalHours;
            if (hours is > 0 and < 0.1) // Plausibilität (kein Riesensprung nach Reconnect)
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
