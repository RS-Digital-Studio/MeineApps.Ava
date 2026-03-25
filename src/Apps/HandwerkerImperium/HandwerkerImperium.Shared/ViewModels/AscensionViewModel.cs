using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer die Ascension-Ansicht (Meta-Prestige).
/// Zeigt verfuegbare AP, Perks mit Level/Kosten, und Aufstiegs-Button.
/// </summary>
public sealed partial class AscensionViewModel : ViewModelBase
{
    private readonly IAscensionService _ascensionService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IAudioService _audioService;
    private readonly IDialogService _dialogService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Celebration-Effekt ausloesen (Confetti etc.).
    /// </summary>
    public event Action? CelebrationRequested;

    /// <summary>
    /// FloatingText-Event. Parameter: text, category.
    /// </summary>
    public event Action<string, string>? FloatingTextRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _canAscend;

    [ObservableProperty]
    private int _ascensionLevel;

    [ObservableProperty]
    private int _availablePoints;

    [ObservableProperty]
    private int _totalPoints;

    /// <summary>AP die bei naechster Ascension erhalten werden.</summary>
    [ObservableProperty]
    private int _pendingPoints;

    [ObservableProperty]
    private ObservableCollection<AscensionPerkDisplay> _perks = [];

    /// <summary>Ob mindestens ein Perk angezeigt werden kann (fuer UI-Visibility).</summary>
    public bool HasPerks => Perks.Count > 0;

    partial void OnPerksChanged(ObservableCollection<AscensionPerkDisplay> value)
        => OnPropertyChanged(nameof(HasPerks));

    // ═══════════════════════════════════════════════════════════════════════
    // KONSTRUKTOR
    // ═══════════════════════════════════════════════════════════════════════

    public AscensionViewModel(
        IAscensionService ascensionService,
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IAudioService audioService,
        IDialogService dialogService)
    {
        _ascensionService = ascensionService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _audioService = audioService;
        _dialogService = dialogService;

        // Nach Ascension die Anzeige aktualisieren
        _ascensionService.AscensionCompleted += (_, _) => LoadData();

        // Bei State-Wechsel (Import/Reset) ebenfalls aktualisieren
        _gameStateService.StateLoaded += (_, _) => LoadData();

        LoadData();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATEN LADEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Laedt alle Ascension-Daten aus dem State und baut die Perk-Liste auf.
    /// </summary>
    public void LoadData()
    {
        var state = _gameStateService.State;
        var ascData = state.Ascension;

        AscensionLevel = ascData.AscensionLevel;
        AvailablePoints = ascData.AscensionPoints;
        TotalPoints = ascData.TotalAscensionPoints;
        CanAscend = _ascensionService.CanAscend;
        PendingPoints = CanAscend ? _ascensionService.CalculateAscensionPoints() : 0;

        // Perk-Liste aufbauen
        var allPerks = _ascensionService.GetAllPerks();
        var displayPerks = new ObservableCollection<AscensionPerkDisplay>();

        foreach (var perk in allPerks)
        {
            int currentLevel = ascData.GetPerkLevel(perk.Id);
            bool isMax = currentLevel >= perk.MaxLevel;
            int nextCost = isMax ? 0 : perk.GetCost(currentLevel + 1);
            bool canUpgrade = !isMax && AvailablePoints >= nextCost;

            // Effekt-Anzeige: Aktueller Wert und naechster Wert
            string effectDisplay = BuildEffectDisplay(perk, currentLevel);

            displayPerks.Add(new AscensionPerkDisplay
            {
                Id = perk.Id,
                Name = _localizationService.GetString(perk.NameKey) ?? perk.NameKey,
                Description = _localizationService.GetString(perk.DescriptionKey) ?? perk.DescriptionKey,
                CurrentLevel = currentLevel,
                MaxLevel = perk.MaxLevel,
                UpgradeCost = nextCost,
                CanUpgrade = canUpgrade,
                IsMaxLevel = isMax,
                EffectDisplay = effectDisplay,
                IconKind = perk.Icon,
            });
        }

        Perks = displayPerks;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fuehrt die Ascension durch nach Bestaetigung.
    /// Resettet Prestige-Daten komplett, gibt Ascension-Punkte.
    /// </summary>
    [RelayCommand]
    private async Task AscendAsync()
    {
        if (!CanAscend) return;

        var title = _localizationService.GetString("AscensionTitle") ?? "Aufstieg";
        var points = _ascensionService.CalculateAscensionPoints();
        var message = string.Format(
            _localizationService.GetString("AscensionConfirm")
                ?? "Aufstieg durchfuehren? Du erhaeltst {0} AP. Alle Prestige-Daten werden zurueckgesetzt!",
            points);
        var accept = _localizationService.GetString("Ascend") ?? "Aufsteigen";
        var cancel = _localizationService.GetString("Cancel") ?? "Abbrechen";

        // Bestaetigung abwarten
        var confirmed = await _dialogService.ShowConfirmDialog(title, message, accept, cancel);
        if (!confirmed) return;

        var success = await _ascensionService.DoAscension();
        if (!success)
        {
            _dialogService.ShowAlertDialog(
                title,
                _localizationService.GetString("AscensionFailed") ?? "Aufstieg fehlgeschlagen. Voraussetzungen nicht erfuellt.",
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Celebration-Effekte
        await _audioService.PlaySoundAsync(GameSound.LevelUp);
        CelebrationRequested?.Invoke();

        var resultText = string.Format(
            _localizationService.GetString("AscensionComplete") ?? "Aufstieg abgeschlossen! +{0} AP",
            points);
        FloatingTextRequested?.Invoke(resultText, "ascension");

        // Daten neu laden (wird auch ueber AscensionCompleted-Event getriggert)
        LoadData();
    }

    /// <summary>
    /// Upgraded einen Perk um eine Stufe.
    /// </summary>
    [RelayCommand]
    private async Task UpgradePerkAsync(string? perkId)
    {
        if (string.IsNullOrEmpty(perkId)) return;

        var success = _ascensionService.UpgradePerk(perkId);
        if (!success) return;

        await _audioService.PlaySoundAsync(GameSound.Upgrade);

        // Daten aktualisieren
        LoadData();
    }

    /// <summary>
    /// Zurueck zur vorherigen Ansicht.
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut die Effekt-Anzeige fuer einen Perk auf (aktueller + naechster Wert).
    /// </summary>
    private string BuildEffectDisplay(AscensionPerk perk, int currentLevel)
    {
        if (currentLevel == 0)
        {
            // Noch nicht gekauft: Zeige was Level 1 bringt
            var nextVal = perk.GetValue(1);
            return FormatPerkValue(perk.Id, nextVal, isPreview: true);
        }

        var currentVal = perk.GetValue(currentLevel);
        string display = FormatPerkValue(perk.Id, currentVal, isPreview: false);

        if (currentLevel < perk.MaxLevel)
        {
            var nextVal = perk.GetValue(currentLevel + 1);
            display += $" -> {FormatPerkValue(perk.Id, nextVal, isPreview: false)}";
        }

        return display;
    }

    /// <summary>
    /// Formatiert einen Perk-Wert je nach Perk-Typ.
    /// </summary>
    private string FormatPerkValue(string perkId, decimal value, bool isPreview)
    {
        var prefix = isPreview ? "" : "";

        return perkId switch
        {
            // Prozentuale Boni
            "asc_start_capital" => $"+{value * 100:0}% {_localizationService.GetString("StartMoney") ?? "Startgeld"}",
            "asc_timeless_research" => $"-{value * 100:0}% {_localizationService.GetString("ResearchTime") ?? "Forschungszeit"}",
            "asc_golden_era" => $"+{value * 100:0}% {_localizationService.GetString("GoldenScrews") ?? "Goldschrauben"}",

            // Absolute Werte
            "asc_eternal_tools" => $"Lv.{value:0} {_localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge"}",
            "asc_quick_start" => string.Format(
                _localizationService.GetString("QuickStartFormat") ?? "{0} Workshops",
                (int)value),
            "asc_legendary_reputation" => string.Format(
                _localizationService.GetString("StartReputationFormat") ?? "Start-Rep. {0}",
                (int)value),

            _ => $"{value}"
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DISPLAY-MODELL
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Display-Modell fuer einen Ascension-Perk im UI.
/// </summary>
public class AscensionPerkDisplay
{
    /// <summary>Perk-ID (z.B. "asc_start_capital").</summary>
    public string Id { get; set; } = "";

    /// <summary>Lokalisierter Name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Lokalisierte Beschreibung.</summary>
    public string Description { get; set; } = "";

    /// <summary>Aktuelles Level (0 = nicht gekauft).</summary>
    public int CurrentLevel { get; set; }

    /// <summary>Maximales Level.</summary>
    public int MaxLevel { get; set; }

    /// <summary>Kosten fuer das naechste Level (0 wenn max).</summary>
    public int UpgradeCost { get; set; }

    /// <summary>Ob genug AP fuer Upgrade vorhanden sind.</summary>
    public bool CanUpgrade { get; set; }

    /// <summary>Ob maximales Level erreicht ist.</summary>
    public bool IsMaxLevel { get; set; }

    /// <summary>Formatierte Effekt-Anzeige (z.B. "+200% Startgeld -> +500% Startgeld").</summary>
    public string EffectDisplay { get; set; } = "";

    /// <summary>Icon-Name (GameIcon).</summary>
    public string IconKind { get; set; } = "";

    // ═══════════════════════════════════════════════════════════════════
    // BERECHNETE PROPERTIES FUER UI-BINDING
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Level-Anzeige (z.B. "3/5").</summary>
    public string LevelDisplay => $"{CurrentLevel}/{MaxLevel}";

    /// <summary>Kosten-Anzeige (z.B. "3 AP") oder "MAX".</summary>
    public string CostDisplay => IsMaxLevel ? "MAX" : $"{UpgradeCost} AP";

    /// <summary>Fortschritt 0.0-1.0 fuer Balken-Anzeige.</summary>
    public double Progress => MaxLevel > 0 ? (double)CurrentLevel / MaxLevel : 0;

    /// <summary>Opacity: Max-Level leicht gedimmt.</summary>
    public double DisplayOpacity => IsMaxLevel ? 0.7 : 1.0;

    /// <summary>Kosten-Farbe: Gruen wenn leistbar, Rot wenn nicht, Grau wenn max.</summary>
    public string CostColor => IsMaxLevel ? "#808080" : CanUpgrade ? "#22C55E" : "#EF4444";

    /// <summary>Border-Akzent-Farbe: Gold wenn max, sonst transparent.</summary>
    public string BorderColor => IsMaxLevel ? "#40FFD700" : "#20808080";
}
