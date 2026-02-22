using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace BomberBlast.Models;

/// <summary>
/// Anzeige-Model fuer ein Shop-Upgrade-Item
/// </summary>
public partial class ShopDisplayItem : ObservableObject
{
    public UpgradeType Type { get; init; }

    /// <summary>Lokalisierungs-Key fuer den Namen</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Lokalisierungs-Key fuer die Beschreibung</summary>
    public string DescriptionKey { get; init; } = "";

    /// <summary>Lokalisierter Name (vom ViewModel gesetzt)</summary>
    [ObservableProperty]
    private string _displayName = "";

    /// <summary>Lokalisierte Beschreibung (vom ViewModel gesetzt)</summary>
    [ObservableProperty]
    private string _displayDescription = "";

    /// <summary>Material-Icon</summary>
    public MaterialIconKind IconKind { get; init; }

    /// <summary>Icon-Farbe</summary>
    public Color IconColor { get; init; } = Colors.White;

    /// <summary>UpgradeType als int fuer ShopUpgradeIconCanvas-Binding</summary>
    public int UpgradeTypeIndex => (int)Type;

    /// <summary>IconColor als ARGB uint fuer ShopUpgradeIconCanvas-Binding</summary>
    public uint IconColorArgb => IconColor.ToUInt32();

    /// <summary>Maximales Level</summary>
    public int MaxLevel { get; init; }

    [ObservableProperty]
    private int _currentLevel;

    [ObservableProperty]
    private int _nextPrice;

    [ObservableProperty]
    private bool _isMaxed;

    [ObservableProperty]
    private string _effectText = "";

    [ObservableProperty]
    private string _levelText = "";

    [ObservableProperty]
    private bool _canAfford;

    /// <summary>Ob der Kauf-Button aktiv sein soll</summary>
    public bool CanBuy => !IsMaxed && CanAfford;

    /// <summary>Level-Fortschrittsanzeige (z.B. "●●○")</summary>
    public string LevelDots
    {
        get
        {
            string filled = new('\u25CF', CurrentLevel);
            string empty = new('\u25CB', MaxLevel - CurrentLevel);
            return filled + empty;
        }
    }

    /// <summary>Aktualisiert abgeleitete Properties nach Aenderungen</summary>
    public void Refresh(int coinBalance)
    {
        CanAfford = coinBalance >= NextPrice;
        OnPropertyChanged(nameof(CanBuy));
        OnPropertyChanged(nameof(LevelDots));
    }
}
