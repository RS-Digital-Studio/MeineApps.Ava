using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.ViewModels;


/// <summary>
/// MainViewModel Automation-State (AAA-Audit Review-Pass 12.05.2026):
/// Forwarding zu GameState.Automation — AutoCollect, AutoAccept, AutoAssign.
/// Aus MainViewModel.Properties.cs extrahiert um Themen-Cluster zu trennen.
/// </summary>
public sealed partial class MainViewModel
{
    // AUTOMATION (Forwarding zu GameState.Automation)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Lieferungen automatisch einsammeln (ab Level 15).</summary>
    public bool AutoCollectDelivery
    {
        get => _gameStateService.Automation.AutoCollectDelivery;
        set
        {
            if (_gameStateService.Automation.AutoCollectDelivery == value) return;
            _gameStateService.Automation.AutoCollectDelivery = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>Besten Auftrag automatisch annehmen (ab Level 25).</summary>
    public bool AutoAcceptOrder
    {
        get => _gameStateService.Automation.AutoAcceptOrder;
        set
        {
            if (_gameStateService.Automation.AutoAcceptOrder == value) return;
            _gameStateService.Automation.AutoAcceptOrder = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>Daily Reward automatisch einlösen (nur Premium).</summary>
    public bool AutoClaimDaily
    {
        get => _gameStateService.Automation.AutoClaimDaily;
        set
        {
            if (_gameStateService.Automation.AutoClaimDaily == value) return;
            _gameStateService.Automation.AutoClaimDaily = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    public bool AutoAssignWorkers
    {
        get => _gameStateService.Automation.AutoAssignWorkers;
        set
        {
            if (_gameStateService.Automation.AutoAssignWorkers == value) return;
            _gameStateService.Automation.AutoAssignWorkers = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>v2.0.36: Nur Standard-Auftraege automatisch annehmen (Live/VIP bleiben liegen).</summary>
    public bool AutoAcceptOnlyStandard
    {
        get => _gameStateService.Automation.AutoAcceptOnlyStandard;
        set
        {
            if (_gameStateService.Automation.AutoAcceptOnlyStandard == value) return;
            _gameStateService.Automation.AutoAcceptOnlyStandard = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>v2.0.36: MiniGame-Auto-Complete ueberspringt Live-/Premium-Auftraege.</summary>
    public bool AutoCompleteSkipLiveOrders
    {
        get => _gameStateService.Automation.AutoCompleteSkipLiveOrders;
        set
        {
            if (_gameStateService.Automation.AutoCompleteSkipLiveOrders == value) return;
            _gameStateService.Automation.AutoCompleteSkipLiveOrders = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    // Level-Gates für Automatisierung (delegiert an GameStateService)
    public bool IsAutoCollectUnlocked => _gameStateService.IsAutoCollectUnlocked;
    public bool IsAutoAcceptUnlocked => _gameStateService.IsAutoAcceptUnlocked;
    public bool IsAutoAssignUnlocked => _gameStateService.IsAutoAssignUnlocked;
    public bool IsAutoClaimUnlocked => _purchaseService.IsPremium;

    /// <summary>
    /// v2.0.36: Wenn die Grafik-Qualitaet auf Low steht, schalten wir die Loop-Animationen
    /// (GoldenBadgeShimmer, TutorialHintPulse, BoostPulse) aus. Die wichtigen Event-getriebenen
    /// One-Shot-Animationen (LevelUpFlash, IncomePulse) bleiben — die geben Spieler-Feedback.
    /// </summary>
    public bool ReduceMotion => _gameStateService.Settings.GraphicsQuality == Models.Enums.GraphicsQuality.Low;

    // ═══════════════════════════════════════════════════════════════════════
    // REPUTATION-TIER (v2.0.37 — Header-Badge + Spawn-Boni)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Aktuelles Reputations-Tier (computed aus dem ReputationScore).</summary>
    public Models.Enums.CustomerReputationTier CurrentReputationTier
        => _gameStateService.State.Reputation.CurrentTier;

    /// <summary>True ab Tier CityKnown — Anfaenger-Tier wird nicht angezeigt (Spam-Schutz).</summary>
    public bool ShowReputationTierBadge
        => CurrentReputationTier > Models.Enums.CustomerReputationTier.Beginner;

    /// <summary>Lokalisierter Tier-Name fuer den Header-Badge.</summary>
    public string ReputationTierName
        => _localizationService.GetString(CurrentReputationTier.GetLocalizationKey())
           ?? CurrentReputationTier.ToString();

    /// <summary>Hex-Farbe fuer das Tier-Badge (Bronze/Silber/Gold).</summary>
    public string ReputationTierColor => CurrentReputationTier.GetBadgeColor();

    // ═══════════════════════════════════════════════════════════════════════
}
