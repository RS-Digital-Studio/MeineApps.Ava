using Avalonia.Media;
using BomberBlast.Icons;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BomberBlast.Models;

/// <summary>
/// Anzeige-Model für ein PowerUp oder eine Welt-Mechanik im Shop (Fähigkeiten-Sektion).
/// Zeigt Freischaltungs-Status, Icon, Name und Beschreibung.
/// </summary>
public partial class PowerUpDisplayItem : ObservableObject
{
    /// <summary>Eindeutige ID (z.B. "powerup_kick", "mechanic_ice")</summary>
    public string Id { get; init; } = "";

    /// <summary>Lokalisierter Name</summary>
    [ObservableProperty]
    private string _displayName = "";

    /// <summary>Lokalisierte Beschreibung</summary>
    [ObservableProperty]
    private string _displayDescription = "";

    /// <summary>Game-Icon</summary>
    public GameIconKind IconKind { get; init; }

    /// <summary>Icon-Farbe (voll wenn freigeschaltet, grau wenn gesperrt)</summary>
    public Color IconColor { get; init; } = Colors.White;

    /// <summary>Ab welchem Level freigeschaltet</summary>
    public int UnlockLevel { get; init; }

    /// <summary>Ob das Item freigeschaltet ist</summary>
    [ObservableProperty]
    private bool _isUnlocked;

    /// <summary>Karten-Opacity (1.0 wenn freigeschaltet, 0.5 wenn gesperrt)</summary>
    public double CardOpacity => IsUnlocked ? 1.0 : 0.5;

    /// <summary>Anzeige-Text ("Ab Level X" oder "Freigeschaltet")</summary>
    [ObservableProperty]
    private string _unlockText = "";

    partial void OnIsUnlockedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardOpacity));
    }
}
