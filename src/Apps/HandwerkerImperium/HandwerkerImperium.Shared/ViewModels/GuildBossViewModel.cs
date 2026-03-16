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
/// Sub-ViewModel für den kooperativen Gilden-Boss.
/// Zeigt Boss-HP, Schadens-Leaderboard, Countdown und eigenen Beitrag.
/// </summary>
public sealed partial class GuildBossViewModel : ViewModelBase
{
    private readonly IGuildBossService _bossService;
    private readonly ILocalizationService _localizationService;
    private bool _isBusy;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
#pragma warning disable CS0067 // Event wird vorbereitet fuer zukuenftige Boss-Celebration
    public event Action? CelebrationRequested;
#pragma warning restore CS0067

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Boss-Info
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _hasActiveBoss;

    [ObservableProperty]
    private string _bossName = "";

    [ObservableProperty]
    private string _bossIcon = "Skull";

    [ObservableProperty]
    private string _bossColor = "#888888";

    [ObservableProperty]
    private long _bossMaxHp;

    [ObservableProperty]
    private long _bossCurrentHp;

    [ObservableProperty]
    private double _bossHpPercent;

    [ObservableProperty]
    private string _bossHpDisplay = "";

    [ObservableProperty]
    private string _bossCountdown = "";

    [ObservableProperty]
    private bool _isCountdownUrgent;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Eigener Beitrag
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private long _ownDamage;

    [ObservableProperty]
    private int _ownHits;

    [ObservableProperty]
    private int _ownRank;

    [ObservableProperty]
    private string _ownDamageDisplay = "";

    [ObservableProperty]
    private string _ownRankDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Leaderboard
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<BossDamageEntry> _leaderboard = [];

    [ObservableProperty]
    private bool _hasLeaderboard;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Status
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GuildBossViewModel(
        IGuildBossService bossService,
        ILocalizationService localizationService)
    {
        _bossService = bossService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadBossDataAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        IsLoading = true;
        try
        {
            // Boss spawnen falls keiner aktiv
            await _bossService.SpawnBossIfNeededAsync();

            var data = await _bossService.GetActiveBossAsync();
            if (data == null || data.Status != BossStatus.Active)
            {
                HasActiveBoss = false;
                StatusMessage = _localizationService.GetString("GuildBossNone") ?? "Kein Boss aktiv";
                return;
            }

            HasActiveBoss = true;
            BossName = _localizationService.GetString(data.BossName) ?? data.BossName;
            BossMaxHp = data.MaxHp;
            BossCurrentHp = data.CurrentHp;
            BossHpPercent = data.HpPercent;
            BossHpDisplay = $"{data.CurrentHp:N0} / {data.MaxHp:N0}";

            // Icon + Farbe vom Boss-Typ
            var definition = GuildBossDefinition.GetAll()
                .FirstOrDefault(d => d.BossType == data.BossType);
            if (definition != null)
            {
                BossIcon = definition.Icon;
                BossColor = definition.Color;
            }

            // Countdown
            var remaining = data.TimeRemaining;
            if (remaining > TimeSpan.Zero)
            {
                BossCountdown = remaining.TotalHours >= 1
                    ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                    : $"{remaining.Minutes}m {remaining.Seconds:D2}s";
                IsCountdownUrgent = remaining.TotalHours < 6;
            }
            else
            {
                BossCountdown = _localizationService.GetString("GuildBossExpiring") ?? "Läuft ab...";
                IsCountdownUrgent = true;
            }

            // Eigener Beitrag
            OwnDamage = data.OwnDamage;
            OwnHits = data.OwnHits;
            OwnRank = data.OwnRank;
            var hitsLabel = _localizationService.GetString("BossHits") ?? "Treffer";
            OwnDamageDisplay = $"{data.OwnDamage:N0} ({data.OwnHits} {hitsLabel})";
            OwnRankDisplay = data.OwnRank > 0
                ? $"#{data.OwnRank}"
                : "-";

            // Leaderboard
            Leaderboard = new ObservableCollection<BossDamageEntry>(data.Leaderboard);
            HasLeaderboard = Leaderboard.Count > 0;
        }
        catch
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Fehler",
                _localizationService.GetString("GuildBossLoadError") ?? "Boss-Daten konnten nicht geladen werden.");
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

    /// <summary>Lädt die Boss-Daten neu (für Hub-Aufruf).</summary>
    public void RefreshBoss() => LoadBossDataAsync().SafeFireAndForget();

    /// <summary>Gibt den Quick-Status für den Guild-Hub zurück.</summary>
    public string GetQuickStatus()
    {
        if (!HasActiveBoss)
            return _localizationService.GetString("GuildBossNone") ?? "Kein Boss";

        return $"{(int)(BossHpPercent * 100)}% {BossName}";
    }

    public void UpdateLocalizedTexts()
    {
        if (HasActiveBoss)
            RefreshBoss();
    }
}
