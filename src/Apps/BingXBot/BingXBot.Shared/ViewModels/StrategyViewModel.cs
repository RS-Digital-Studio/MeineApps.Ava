using Avalonia.Threading;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using BingXBot.Trading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Strategie-Konfiguration (Indikatoren, Ein-/Ausstiegsregeln).
/// Verbunden mit StrategyManager für echte Strategie-Aktivierung.
/// Publiziert Aktivierung/Deaktivierung über den BotEventBus.
/// </summary>
public partial class StrategyViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Watchdog-Schwelle (24.04.2026): Wenn länger als 5 min kein SK-Ampel-Update kommt
    /// ODER der Bot nicht im Running-State ist → Ampel als veraltet markieren.
    /// Hintergrund: Live-Bug am 24.04. — UI zeigte "sucheB" obwohl die Engine seit 3 Tagen tot war.
    /// </summary>
    private static readonly TimeSpan AmpelStaleThreshold = TimeSpan.FromMinutes(5);

    private readonly StrategyManager _strategyManager;
    private readonly BotEventBus _eventBus;
    private readonly Core.Configuration.ScannerSettings _scannerSettings;
    private DispatcherTimer? _watchdogTimer;

    private DateTime _lastAmpelUpdateUtc = DateTime.MinValue;
    private BotState _lastBotState = BotState.Stopped;

    [ObservableProperty] private string _selectedStrategy = "SK-System";
    [ObservableProperty] private string _strategyDescription = "Buch-konformes Stefan-Kassing System (Multi-TF Standalone: D1/H4/H1/M15, Pip-SL, 3-5 Bestätigungen)";
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _statusText = "Inaktiv";
    [ObservableProperty] private string _toggleButtonText = "Aktivieren";

    /// <summary>Multi-TF Standalone: Aktuell visualisierte Navigator-TF (für Chart-Overlay).</summary>
    [ObservableProperty] private string _selectedVisualTimeframe = "4H";

    /// <summary>Letzter SK-Status pro TF (vom TradingServiceBase aktualisiert).</summary>
    [ObservableProperty] private string _visualizedAmpelStatus = "—";

    /// <summary>True wenn der Bot nicht läuft ODER seit ≥ 5 min kein Engine-Update kam.</summary>
    [ObservableProperty] private bool _isAmpelStale = true;

    /// <summary>Erklärt dem User warum die Ampel veraltet ist (lokalisiert in Deutsch).</summary>
    [ObservableProperty] private string _idleHintText = "Bot läuft nicht — Status oben in Dashboard prüfen und ggf. Start drücken.";

    public string[] AvailableStrategies => StrategyFactory.AvailableStrategies;
    public string[] AvailableVisualTimeframes => new[] { "1D", "4H", "1H", "15m" };

    /// <summary>
    /// Dynamische Parameter je nach gewählter Strategie.
    /// Werden aus der echten IStrategy.Parameters geladen.
    /// </summary>
    public ObservableCollection<StrategyParameterItem> Parameters { get; } = new();

    // Cache des letzten SK-Ampel-Snapshots pro TF (vom BotEventBus)
    private readonly Dictionary<Core.Enums.TimeFrame, string> _ampelCache = new();

    public StrategyViewModel(StrategyManager strategyManager, BotEventBus eventBus,
        Core.Configuration.ScannerSettings scannerSettings)
    {
        _strategyManager = strategyManager;
        _eventBus = eventBus;
        _scannerSettings = scannerSettings;
        _eventBus.SkAmpelUpdated += OnSkAmpelUpdated;
        _eventBus.BotStateChanged += OnBotStateChanged;
        LoadParametersFromStrategy();

        // Watchdog: Re-Evaluiere Stale-Status alle 30 s. Reicht weil Threshold = 5 min.
        _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _watchdogTimer.Tick += (_, _) => EvaluateStaleness();
        _watchdogTimer.Start();
    }

    partial void OnSelectedVisualTimeframeChanged(string value) => UpdateVisualizedStatus();

    private void OnSkAmpelUpdated(object? sender, Dictionary<Core.Enums.TimeFrame, string> ampel)
    {
        foreach (var kv in ampel) _ampelCache[kv.Key] = kv.Value;
        _lastAmpelUpdateUtc = DateTime.UtcNow;
        Dispatcher.UIThread.Post(() =>
        {
            UpdateVisualizedStatus();
            EvaluateStaleness();
        });
    }

    private void OnBotStateChanged(object? sender, BotState state)
    {
        _lastBotState = state;
        Dispatcher.UIThread.Post(EvaluateStaleness);
    }

    /// <summary>
    /// Setzt <see cref="IsAmpelStale"/> und <see cref="IdleHintText"/> basierend auf:
    /// 1. BotState (nicht Running → stale)
    /// 2. Alter des letzten SkAmpelUpdated-Events (≥ 5 min → stale)
    /// </summary>
    private void EvaluateStaleness()
    {
        var ageOk = _lastAmpelUpdateUtc != DateTime.MinValue
                    && (DateTime.UtcNow - _lastAmpelUpdateUtc) <= AmpelStaleThreshold;
        var botRunning = _lastBotState == BotState.Running;
        var stale = !botRunning || !ageOk;

        if (stale)
        {
            if (!botRunning)
            {
                IdleHintText = "Bot läuft nicht — angezeigter Status ist veraltet. Im Dashboard auf Start drücken.";
            }
            else
            {
                var ageMin = _lastAmpelUpdateUtc == DateTime.MinValue
                    ? 0
                    : (int)(DateTime.UtcNow - _lastAmpelUpdateUtc).TotalMinutes;
                IdleHintText = ageMin <= 0
                    ? "Bot läuft, aber noch keine Engine-Updates empfangen. Bitte einen Moment warten."
                    : $"Letztes Engine-Update vor {ageMin} min — Engine prüft aktuell nichts. Logs prüfen.";
            }
        }

        IsAmpelStale = stale;
    }

    private void UpdateVisualizedStatus()
    {
        var tf = SelectedVisualTimeframe switch
        {
            "1D" => Core.Enums.TimeFrame.D1,
            "4H" => Core.Enums.TimeFrame.H4,
            "1H" => Core.Enums.TimeFrame.H1,
            "15m" => Core.Enums.TimeFrame.M15,
            _ => Core.Enums.TimeFrame.H4,
        };
        VisualizedAmpelStatus = _ampelCache.TryGetValue(tf, out var s) ? s : "—";
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        // Beschreibung direkt aus der Strategie-Instanz laden
        var tempStrategy = StrategyFactory.Create(value);
        StrategyDescription = tempStrategy.Description;
        LoadParametersFromStrategy();

        // Wenn aktiv, Strategie im Manager aktualisieren
        if (IsActive)
        {
            var strategy = CreateStrategyWithParameters();
            _strategyManager.SetStrategy(strategy);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                $"Strategie gewechselt zu: {value}"));
        }
    }

    /// <summary>
    /// Lädt Parameter direkt aus der echten Strategie-Klasse.
    /// </summary>
    private void LoadParametersFromStrategy()
    {
        Parameters.Clear();
        var strategy = StrategyFactory.Create(SelectedStrategy);
        foreach (var param in strategy.Parameters)
        {
            var valueType = param.ValueType == "decimal" ? "decimal" : "int";
            Parameters.Add(new(param.Name, param.DefaultValue?.ToString() ?? "0", valueType));
        }
    }

    /// <summary>
    /// Erstellt eine IStrategy-Instanz und schreibt die UI-Parameter per Reflection zurück.
    /// Strategien haben private Felder die per Naming-Convention gemappt werden:
    /// z.B. Parameter "FastPeriod" → Feld "_fastPeriod".
    /// </summary>
    private IStrategy CreateStrategyWithParameters()
    {
        var strategy = StrategyFactory.Create(SelectedStrategy);

        // Original-Parameter für Min/Max-Validierung
        var strategyParams = strategy.Parameters;

        // Parameter aus der UI auf die Strategie-Instanz anwenden
        var type = strategy.GetType();
        foreach (var param in Parameters)
        {
            // Convention: UI-Name "FastPeriod" → Feld "_fastPeriod"
            var fieldName = "_" + char.ToLower(param.Name[0]) + param.Name[1..];
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null) continue;

            // Original-Parameter für Min/Max-Grenzen holen
            var stratParam = strategyParams.FirstOrDefault(sp => sp.Name == param.Name);

            try
            {
                if (param.ValueType == "decimal" && decimal.TryParse(param.Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var decVal))
                {
                    // Min/Max-Validierung für decimal-Parameter
                    decVal = ClampDecimal(decVal, stratParam, param.Name);
                    field.SetValue(strategy, decVal);
                }
                else if (param.ValueType == "int" && int.TryParse(param.Value, out var intVal))
                {
                    // Min/Max-Validierung für int-Parameter
                    intVal = ClampInt(intVal, stratParam, param.Name);
                    field.SetValue(strategy, intVal);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Parameter '{param.Name}' setzen fehlgeschlagen: {ex.Message}");
            }
        }

        return strategy;
    }

    /// <summary>Clampt einen decimal-Wert auf Min/Max-Grenzen des StrategyParameter.</summary>
    private decimal ClampDecimal(decimal value, StrategyParameter? stratParam, string paramName)
    {
        if (stratParam == null) return value;

        var min = stratParam.MinValue is decimal minDec ? minDec
            : stratParam.MinValue is int minInt ? (decimal)minInt
            : stratParam.MinValue is double minDbl ? (decimal)minDbl
            : (decimal?)null;

        var max = stratParam.MaxValue is decimal maxDec ? maxDec
            : stratParam.MaxValue is int maxInt ? (decimal)maxInt
            : stratParam.MaxValue is double maxDbl ? (decimal)maxDbl
            : (decimal?)null;

        if (min.HasValue && max.HasValue)
        {
            var clamped = Math.Clamp(value, min.Value, max.Value);
            if (clamped != value)
                System.Diagnostics.Debug.WriteLine(
                    $"Parameter '{paramName}': Wert {value} auf [{min.Value}, {max.Value}] geclampt → {clamped}");
            return clamped;
        }

        if (min.HasValue && value < min.Value)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Parameter '{paramName}': Wert {value} unter Minimum {min.Value} geclampt");
            return min.Value;
        }

        if (max.HasValue && value > max.Value)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Parameter '{paramName}': Wert {value} über Maximum {max.Value} geclampt");
            return max.Value;
        }

        return value;
    }

    /// <summary>Clampt einen int-Wert auf Min/Max-Grenzen des StrategyParameter.</summary>
    private int ClampInt(int value, StrategyParameter? stratParam, string paramName)
    {
        if (stratParam == null) return value;

        var min = stratParam.MinValue is int minInt ? minInt
            : stratParam.MinValue is decimal minDec ? (int)minDec
            : stratParam.MinValue is double minDbl ? (int)minDbl
            : (int?)null;

        var max = stratParam.MaxValue is int maxInt ? maxInt
            : stratParam.MaxValue is decimal maxDec ? (int)maxDec
            : stratParam.MaxValue is double maxDbl ? (int)maxDbl
            : (int?)null;

        if (min.HasValue && max.HasValue)
        {
            var clamped = Math.Clamp(value, min.Value, max.Value);
            if (clamped != value)
                System.Diagnostics.Debug.WriteLine(
                    $"Parameter '{paramName}': Wert {value} auf [{min.Value}, {max.Value}] geclampt → {clamped}");
            return clamped;
        }

        if (min.HasValue && value < min.Value)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Parameter '{paramName}': Wert {value} unter Minimum {min.Value} geclampt");
            return min.Value;
        }

        if (max.HasValue && value > max.Value)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Parameter '{paramName}': Wert {value} über Maximum {max.Value} geclampt");
            return max.Value;
        }

        return value;
    }

    /// <summary>
    /// Räumt Watchdog-Timer + EventBus-Subscriptions auf.
    /// Singleton-Lifetime: wird beim App-Shutdown vom DI-Container aufgerufen.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_watchdogTimer != null)
        {
            _watchdogTimer.Stop();
            _watchdogTimer = null;
        }
        _eventBus.SkAmpelUpdated -= OnSkAmpelUpdated;
        _eventBus.BotStateChanged -= OnBotStateChanged;
    }

    [RelayCommand]
    private void ToggleActive()
    {
        IsActive = !IsActive;

        if (IsActive)
        {
            // Strategie mit benutzerdefinierten Parametern erstellen und im Manager setzen
            var strategy = CreateStrategyWithParameters();
            _strategyManager.SetStrategy(strategy);
            StatusText = $"Aktiv ({strategy.Name})";

            // Parameter-Übersicht für Log erstellen
            var paramText = string.Join(", ", Parameters.Select(p => $"{p.Name}={p.Value}"));
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                $"Strategie aktiviert: {strategy.Name} [{paramText}]"));
        }
        else
        {
            // Strategie deaktivieren: Manager zurücksetzen
            _strategyManager.Reset();
            StatusText = "Inaktiv";

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                "Strategie deaktiviert"));
        }

        ToggleButtonText = IsActive ? "Deaktivieren" : "Aktivieren";
    }
}

/// <summary>
/// Ein einzelner Strategie-Parameter mit Name, Wert und Typ.
/// </summary>
public class StrategyParameterItem : ObservableObject
{
    public string Name { get; set; }
    private string _value;
    public string Value { get => _value; set => SetProperty(ref _value, value); }
    public string ValueType { get; set; }

    public StrategyParameterItem(string name, string value, string type)
    {
        Name = name;
        _value = value;
        ValueType = type;
    }
}
