using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using BingXBot.Services;
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
public partial class StrategyViewModel : ViewModelBase
{
    private readonly StrategyManager _strategyManager;
    private readonly BotEventBus _eventBus;

    [ObservableProperty] private string _selectedStrategy = "Trend-Following";
    [ObservableProperty] private string _strategyDescription = "Multi-Indikator Trend-Following: EMA+RSI+MACD+Volume (5 Bedingungen, Krypto-optimiert)";
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _statusText = "Inaktiv";
    [ObservableProperty] private string _toggleButtonText = "Aktivieren";

    public string[] AvailableStrategies => StrategyFactory.AvailableStrategies;

    /// <summary>
    /// Dynamische Parameter je nach gewählter Strategie.
    /// Werden aus der echten IStrategy.Parameters geladen.
    /// </summary>
    public ObservableCollection<StrategyParameterItem> Parameters { get; } = new();

    public StrategyViewModel(StrategyManager strategyManager, BotEventBus eventBus)
    {
        _strategyManager = strategyManager;
        _eventBus = eventBus;
        LoadParametersFromStrategy();
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

        // Parameter aus der UI auf die Strategie-Instanz anwenden
        var type = strategy.GetType();
        foreach (var param in Parameters)
        {
            // Convention: UI-Name "FastPeriod" → Feld "_fastPeriod"
            var fieldName = "_" + char.ToLower(param.Name[0]) + param.Name[1..];
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null) continue;

            try
            {
                if (param.ValueType == "decimal" && decimal.TryParse(param.Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var decVal))
                {
                    field.SetValue(strategy, decVal);
                }
                else if (param.ValueType == "int" && int.TryParse(param.Value, out var intVal))
                {
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
