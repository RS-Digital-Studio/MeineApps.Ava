using CommunityToolkit.Mvvm.ComponentModel;

namespace HandwerkerRechner.Services;

/// <summary>
/// Kapselt alle 19 Calculator-VM Factories in einem Service.
/// Eliminiert 19 einzelne Func-Parameter im MainViewModel-Konstruktor.
/// Route → Factory-Mapping für die Calculator-Erstellung.
/// </summary>
public interface ICalculatorFactoryService
{
    /// <summary>Erstellt ein Calculator-ViewModel anhand der Route</summary>
    ObservableObject? Create(string route);

    /// <summary>Prüft ob eine Route bekannt ist</summary>
    bool HasRoute(string route);
}
