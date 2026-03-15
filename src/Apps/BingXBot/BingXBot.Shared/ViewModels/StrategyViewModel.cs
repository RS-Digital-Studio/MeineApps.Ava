using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für Strategie-Konfiguration (Indikatoren, Ein-/Ausstiegsregeln).
/// Verbunden mit StrategyManager für echte Strategie-Aktivierung.
/// </summary>
public partial class StrategyViewModel : ObservableObject
{
    private readonly StrategyManager _strategyManager;

    [ObservableProperty] private string _selectedStrategy = "EMA Cross";
    [ObservableProperty] private string _strategyDescription = "Kreuzt Fast-EMA über Slow-EMA = Long, darunter = Short";
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _statusText = "Inaktiv";
    [ObservableProperty] private string _toggleButtonText = "Aktivieren";

    public string[] AvailableStrategies => StrategyFactory.AvailableStrategies;

    /// <summary>
    /// Dynamische Parameter je nach gewählter Strategie.
    /// Werden aus der echten IStrategy.Parameters geladen.
    /// </summary>
    public ObservableCollection<StrategyParameterItem> Parameters { get; } = new();

    public StrategyViewModel(StrategyManager strategyManager)
    {
        _strategyManager = strategyManager;
        LoadParametersFromStrategy();
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        StrategyDescription = value switch
        {
            "EMA Cross" => "Kreuzt Fast-EMA über Slow-EMA = Long, darunter = Short",
            "RSI" => "Long bei RSI < Oversold, Short bei RSI > Overbought",
            "Bollinger Bands" => "Long bei Preis unter unterem Band, Short bei oberem Band",
            "MACD" => "Long bei MACD über Signal-Linie, Short umgekehrt",
            "Grid" => "Gestaffelte Orders in einem Preisbereich",
            _ => ""
        };
        LoadParametersFromStrategy();

        // Wenn aktiv, Strategie im Manager aktualisieren
        if (IsActive)
        {
            var strategy = CreateStrategy();
            _strategyManager.SetStrategy(strategy);
        }
    }

    /// <summary>
    /// Lädt Parameter direkt aus der echten Strategie-Klasse.
    /// </summary>
    private void LoadParametersFromStrategy()
    {
        Parameters.Clear();
        var strategy = CreateStrategy();
        foreach (var param in strategy.Parameters)
        {
            var valueType = param.ValueType == "decimal" ? "decimal" : "int";
            Parameters.Add(new(param.Name, param.DefaultValue?.ToString() ?? "0", valueType));
        }
    }

    /// <summary>
    /// Erstellt die passende IStrategy-Instanz basierend auf SelectedStrategy.
    /// </summary>
    private IStrategy CreateStrategy() => StrategyFactory.Create(SelectedStrategy);

    [RelayCommand]
    private void ToggleActive()
    {
        IsActive = !IsActive;

        if (IsActive)
        {
            // Echte Strategie erstellen und im StrategyManager setzen
            var strategy = CreateStrategy();
            _strategyManager.SetStrategy(strategy);
            StatusText = $"Aktiv ({strategy.Name})";
        }
        else
        {
            // Strategie deaktivieren: Manager zurücksetzen
            _strategyManager.Reset();
            StatusText = "Inaktiv";
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
