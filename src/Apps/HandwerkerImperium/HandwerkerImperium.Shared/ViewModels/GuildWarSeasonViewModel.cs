using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using HandwerkerImperium.Helpers;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Sub-ViewModel für die Gilden-Krieg-Saison-Übersicht.
/// Zeigt aktuelle Saison, Liga, Kriegs-Score, Phasen und Bonus-Missionen.
/// </summary>
public sealed partial class GuildWarSeasonViewModel : ViewModelBase
{
    private readonly IGuildWarSeasonService _warSeasonService;
    private readonly ILocalizationService _localizationService;
    private bool _isBusy;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Saison-Übersicht
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _seasonTitle = "";

    [ObservableProperty]
    private string _leagueDisplay = "";

    [ObservableProperty]
    private string _leagueColor = "#CD7F32";

    [ObservableProperty]
    private string _weekDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Aktueller Krieg
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _hasActiveWar;

    [ObservableProperty]
    private bool _isByeWeek;

    [ObservableProperty]
    private string _opponentName = "";

    [ObservableProperty]
    private int _opponentLevel;

    [ObservableProperty]
    private long _ownScore;

    [ObservableProperty]
    private long _opponentScore;

    [ObservableProperty]
    private string _phaseDisplay = "";

    [ObservableProperty]
    private string _phaseTimeRemaining = "";

    [ObservableProperty]
    private bool _isLeading;

    [ObservableProperty]
    private string _mvpDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Bonus-Missionen
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<WarBonusMission> _bonusMissions = [];

    [ObservableProperty]
    private bool _hasBonusMissions;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Kriegs-Log
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<GuildWarLogEntry> _warLog = [];

    [ObservableProperty]
    private bool _hasWarLog;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Status
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isLoading;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GuildWarSeasonViewModel(
        IGuildWarSeasonService warSeasonService,
        ILocalizationService localizationService)
    {
        _warSeasonService = warSeasonService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadWarDataAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        IsLoading = true;
        try
        {
            var data = await _warSeasonService.GetCurrentWarDataAsync();
            if (data == null)
            {
                HasActiveWar = false;
                IsByeWeek = false;
                return;
            }

            // Saison-Info
            var seasonLabel = _localizationService.GetString("GuildWarSeason") ?? "Saison";
            SeasonTitle = $"{seasonLabel} {data.SeasonNumber}";
            WeekDisplay = string.Format(
                _localizationService.GetString("GuildWarWeek") ?? "Woche {0}",
                data.WeekNumber);

            // Liga
            LeagueDisplay = GetLeagueDisplayText(data.OwnLeague);
            LeagueColor = GetLeagueColor(data.OwnLeague);

            // Krieg
            IsByeWeek = data.IsByeWeek;
            HasActiveWar = !data.IsByeWeek;

            if (HasActiveWar)
            {
                OpponentName = data.OpponentName;
                OpponentLevel = data.OpponentLevel;
                OwnScore = data.OwnScore;
                OpponentScore = data.OpponentScore;
                IsLeading = data.IsLeading;
                PhaseDisplay = GetPhaseDisplayText(data.CurrentPhase);

                // Phasen-Countdown
                if (!string.IsNullOrEmpty(data.PhaseEndsAt) &&
                    DateTime.TryParse(data.PhaseEndsAt, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var phaseEnd))
                {
                    var remaining = phaseEnd - DateTime.UtcNow;
                    PhaseTimeRemaining = remaining > TimeSpan.Zero
                        ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                        : _localizationService.GetString("PhaseEnding") ?? "Endet...";
                }

                // MVP
                if (!string.IsNullOrEmpty(data.MvpName))
                {
                    var mvpLabel = _localizationService.GetString("GuildWarMvp") ?? "MVP";
                    MvpDisplay = $"{mvpLabel}: {data.MvpName} ({data.MvpScore:N0})";
                }

                // Bonus-Missionen
                BonusMissions = new ObservableCollection<WarBonusMission>(data.BonusMissions);
                HasBonusMissions = BonusMissions.Count > 0;
            }

            // Kriegs-Log
            var log = await _warSeasonService.GetWarLogAsync(20);
            WarLog = new ObservableCollection<GuildWarLogEntry>(log);
            HasWarLog = WarLog.Count > 0;
        }
        catch
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Fehler",
                _localizationService.GetString("GuildWarLoadError") ?? "Kriegsdaten konnten nicht geladen werden.");
        }
        finally
        {
            IsLoading = false;
            _isBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateBack() => NavigationRequested?.Invoke("..");

    // ═══════════════════════════════════════════════════════════════════════
    // METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Lädt die Kriegs-Daten neu (für Hub-Aufruf).</summary>
    public void RefreshWar() => LoadWarDataAsync().SafeFireAndForget();

    /// <summary>Gibt den Quick-Status für den Guild-Hub zurück.</summary>
    public string GetQuickStatus()
    {
        if (!HasActiveWar && !IsByeWeek)
            return _localizationService.GetString("GuildWarNoSeason") ?? "Keine Saison";

        if (IsByeWeek)
            return _localizationService.GetString("GuildWarByeWeek") ?? "Freilos-Woche";

        return $"{OwnScore:N0}:{OpponentScore:N0} {PhaseDisplay}";
    }

    public void UpdateLocalizedTexts()
    {
        // Wird bei Sprachwechsel vom GuildViewModel aufgerufen
        if (HasActiveWar)
            RefreshWar();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    private string GetLeagueDisplayText(GuildLeague league) => league switch
    {
        GuildLeague.Bronze => _localizationService.GetString("LeagueBronze") ?? "Bronze-Liga",
        GuildLeague.Silver => _localizationService.GetString("LeagueSilver") ?? "Silber-Liga",
        GuildLeague.Gold => _localizationService.GetString("LeagueGold") ?? "Gold-Liga",
        GuildLeague.Diamond => _localizationService.GetString("LeagueDiamond") ?? "Diamant-Liga",
        _ => "Bronze-Liga"
    };

    private static string GetLeagueColor(GuildLeague league) => league switch
    {
        GuildLeague.Bronze => "#CD7F32",
        GuildLeague.Silver => "#C0C0C0",
        GuildLeague.Gold => "#FFD700",
        GuildLeague.Diamond => "#00BCD4",
        _ => "#CD7F32"
    };

    private string GetPhaseDisplayText(WarPhase phase) => phase switch
    {
        WarPhase.Attack => _localizationService.GetString("WarPhaseAttack") ?? "Angriff",
        WarPhase.Defense => _localizationService.GetString("WarPhaseDefense") ?? "Verteidigung",
        WarPhase.Evaluation => _localizationService.GetString("WarPhaseEvaluation") ?? "Auswertung",
        WarPhase.Completed => _localizationService.GetString("WarPhaseCompleted") ?? "Abgeschlossen",
        _ => "Angriff"
    };
}
