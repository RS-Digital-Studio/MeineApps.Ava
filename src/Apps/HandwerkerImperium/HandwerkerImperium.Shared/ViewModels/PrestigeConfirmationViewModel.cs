using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// AAA-Audit P0 (12.05.2026): Echte VM-Trennung für die Prestige-Tier-Auswahl-Dialog-Logik.
/// Aus <see cref="DialogViewModel"/>.PrestigeTier.cs (Partial) zu eigenständiger ViewModel-Klasse
/// umgezogen — klarer Single-Responsibility-Schnitt.
///
/// Pattern: PrestigeConfirmation wird als Composition-Property von <see cref="DialogViewModel"/>
/// gehalten. Die Confirm-Modal-Mechanik (IsConfirmDialogVisible, ConfirmDialogTitle/Message/Texts)
/// bleibt auf DialogVM — diese Klasse setzt sie via Reference. XAML-Bindings nutzen den Pfad
/// <c>DialogVM.PrestigeConfirmation.X</c>.
/// </summary>
public sealed partial class PrestigeConfirmationViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;
    private readonly IGameStateService _gameStateService;
    private readonly IPrestigeService _prestigeService;
    private readonly IAdService _adService;
    private readonly IChallengeConstraintService? _challengeConstraints;
    private readonly DialogViewModel _dialogVm;

    /// <summary>Ob die Tier-Auswahl-Chips im Bestätigungsdialog sichtbar sind.</summary>
    [ObservableProperty] private bool _isTierSelectionVisible;

    /// <summary>Ob mehrere Prestige-Tiers verfügbar sind (für Tier-Auswahl im Dialog).</summary>
    [ObservableProperty] private bool _hasMultipleTiers;

    /// <summary>Aktuell ausgewählter Tier im Bestätigungsdialog (Index in AvailableTiers).</summary>
    [ObservableProperty] private int _selectedTierIndex;

    /// <summary>Tier-Auswahl-Chips für den Dialog.</summary>
    [ObservableProperty] private List<PrestigeTierOption> _availableTierOptions = [];

    // ═══════════════════════════════════════════════════════════════════
    // V7 (Phase 4 Ressourcen-Plan, Section 3.8): Heirloom-Wahl
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>True wenn der Spieler T4-Items im Inventar hat — UI zeigt Erbstueck-Sektion.</summary>
    [ObservableProperty] private bool _hasHeirloomCandidates;

    /// <summary>Wahlbare Tier-4-Items aus dem aktuellen Inventar.</summary>
    [ObservableProperty] private ObservableCollection<HeirloomCandidateOption> _heirloomCandidates = new();

    /// <summary>Max-Cap der Heirloom-Slots fuer den aktuellen Run (3 Free, 4 Premium).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeirloomSelectionStatus))]
    private int _maxHeirloomSlots;

    /// <summary>Aktuell gewaehlte Anzahl Erbstuecke (live updated bei Toggle).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeirloomSelectionStatus))]
    private int _selectedHeirloomCount;

    /// <summary>UI-Anzeige: "2 / 3 Erbstuecke gewaehlt".</summary>
    public string HeirloomSelectionStatus => string.Format(
        _localizationService.GetString("HeirloomSlotsFormat") ?? "{0} / {1} heirlooms selected",
        SelectedHeirloomCount, MaxHeirloomSlots);

    /// <summary>Bonus-PP-Vorschau aus Spielleistung (flat).</summary>
    [ObservableProperty] private string _bonusPpPreview = string.Empty;

    /// <summary>Challenge-PP-Vorschau (additiver Bonus).</summary>
    [ObservableProperty] private string _challengePpPreview = string.Empty;

    /// <summary>Aktuelle Run-Dauer (für Speedrun-Anzeige im Dialog).</summary>
    [ObservableProperty] private string _currentRunDurationText = string.Empty;

    /// <summary>
    /// v2.1.1 (Audit U-C06): Verlust-Liste fuer den Prestige-Confirm-Dialog (newline-getrennt).
    /// Wird im ConfirmDialog als eigenstaendiger roter Block neben den Gewinnen angezeigt —
    /// vorher waren Verluste nur Teil der Confirm-Message in grauem Text und wurden uebersehen.
    /// </summary>
    [ObservableProperty] private string _lossesPreview = string.Empty;

    /// <summary>Merkt sich den aktuell ausgewählten Tier im Prestige-Dialog.</summary>
    private PrestigeTier _selectedTier = PrestigeTier.None;

    /// <summary>Zugriff auf den gewählten Tier für MainViewModel nach Bestätigung.</summary>
    public PrestigeTier SelectedTier => _selectedTier;

    private TaskCompletionSource<bool>? _confirmTcs;

    public PrestigeConfirmationViewModel(
        ILocalizationService localizationService,
        IGameStateService gameStateService,
        IPrestigeService prestigeService,
        IAdService adService,
        DialogViewModel dialogVm,
        IChallengeConstraintService? challengeConstraints = null)
    {
        _localizationService = localizationService;
        _gameStateService = gameStateService;
        _prestigeService = prestigeService;
        _adService = adService;
        _dialogVm = dialogVm;
        _challengeConstraints = challengeConstraints;
    }

    /// <summary>
    /// Tier-Auswahl im Prestige-Dialog: Aktualisiert die Vorschau beim Wechsel.
    /// Parameter ist der Tier-Name als String (z.B. "Bronze", "Silver").
    /// </summary>
    [RelayCommand]
    private void SelectTier(string tierName)
    {
        if (!Enum.TryParse<PrestigeTier>(tierName, out var tier)) return;

        var idx = AvailableTierOptions.FindIndex(o => o.Tier == tier);
        if (idx < 0) return;

        SelectedTierIndex = idx;

        for (int i = 0; i < AvailableTierOptions.Count; i++)
            AvailableTierOptions[i].IsSelected = i == idx;
        OnPropertyChanged(nameof(AvailableTierOptions));

        _selectedTier = tier;
        UpdateContent(tier);
    }

    /// <summary>
    /// Baut die Dialog-Texte für einen bestimmten Tier auf.
    /// Zeigt Gewinne, Bewahrung, detaillierte Verluste und Timing-Warnung.
    /// </summary>
    public void UpdateContent(PrestigeTier tier)
    {
        var state = _gameStateService.State;
        var tierName = _localizationService.GetString(tier.GetLocalizationKey()) ?? tier.ToString();
        int tierPoints = CalculateEffectivePoints(state, tier);

        // Startgeld für den gewählten Tier (Tier-Basis + Shop-Boni, identisch mit ResetProgress)
        var startMoney = tier.GetTierStartMoney();
        foreach (var shopItem in PrestigeShop.GetAllItems())
        {
            if (state.Prestige.PurchasedShopItems.Contains(shopItem.Id) && shopItem.Effect.ExtraStartMoney > 0)
                startMoney += shopItem.Effect.ExtraStartMoney;
        }

        // 1. GEWINNE (prominent, oben)
        var gains = new List<string>();
        gains.Add($"⬆ +{tierPoints} PP ({tierName} x{tier.GetPointMultiplier()})");
        gains.Add($"⬆ +{tier.GetPermanentMultiplierBonus():P0} {_localizationService.GetString("PermanentIncomeBonus") ?? "permanent income bonus"}");
        gains.Add($"⬆ {_localizationService.GetString("StartMoney") ?? "Start money"}: {MoneyFormatter.FormatCompact(startMoney)}");

        // Speed-Up Prognose
        decimal currentMult = state.Prestige.PermanentMultiplier;
        decimal newMult = currentMult + tier.GetPermanentMultiplierBonus();
        if (currentMult > 0)
        {
            int speedUp = (int)((newMult / currentMult - 1m) * 100);
            gains.Add($"⚡ ~{speedUp}% {_localizationService.GetString("Faster") ?? "faster"}");
        }

        // 2. BEWAHRUNG (positiv formuliert)
        if (tier.KeepsResearch())
            gains.Add($"✓ {_localizationService.GetString("PrestigeKeepsResearch") ?? "Research preserved!"}");
        if (tier.KeepsMasterTools())
            gains.Add($"✓ {_localizationService.GetString("PrestigeKeepsTools") ?? "Master tools preserved!"}");
        if (tier.KeepsBuildings())
            gains.Add($"✓ {_localizationService.GetString("PrestigeKeepsBuildings") ?? "Buildings preserved (Lv.1)!"}");
        if (tier.KeepsManagers())
            gains.Add($"✓ {_localizationService.GetString("PrestigeKeepsManagers") ?? "Managers preserved (Lv.1)!"}");
        if (tier.KeepsBestWorkers())
            gains.Add($"✓ {_localizationService.GetString("PrestigeKeepsWorkers") ?? "Best workers preserved!"}");

        // 3. VERLUSTE (detailliert, explizit benannt)
        var losses = new List<string>();
        losses.Add($"✖ {_localizationService.GetString("PrestigeLossLevel") ?? "Level and XP"}");
        losses.Add($"✖ {_localizationService.GetString("PrestigeLossMoney") ?? "Money and orders"}");
        losses.Add($"✖ {_localizationService.GetString("PrestigeLossWorkshops") ?? "Workshops (only Carpenter remains)"}");

        if (!tier.KeepsResearch())
            losses.Add($"✖ {_localizationService.GetString("PrestigeLossResearch") ?? "Research"}");
        if (!tier.KeepsMasterTools())
            losses.Add($"✖ {_localizationService.GetString("PrestigeLossTools") ?? "Master tools"}");
        if (!tier.KeepsBuildings())
            losses.Add($"✖ {_localizationService.GetString("PrestigeLossBuildings") ?? "Buildings"}");
        if (!tier.KeepsEquipment())
            losses.Add($"✖ {_localizationService.GetString("PrestigeLossEquipment") ?? "Equipment"}");
        if (!tier.KeepsManagers())
            losses.Add($"✖ {_localizationService.GetString("PrestigeLossManagers") ?? "Foremen"}");

        // Immer verloren (unabhängig vom Tier)
        losses.Add($"✖ {_localizationService.GetString("PrestigeLossCrafting") ?? "Crafting inventory and orders"}");

        // 4. TIMING-WARNUNG (suboptimales Prestige erkennen)
        string timingWarning = "";
        var nextTier = tier.GetNextTier();
        if (nextTier != PrestigeTier.None)
        {
            int currentLevel = state.PlayerLevel;
            int nextTierLevel = nextTier.GetRequiredLevel();

            bool wouldNextTierBeAvailable = currentLevel >= nextTierLevel;
            if (wouldNextTierBeAvailable)
            {
                int currentTierCountAfterPrestige = tier switch
                {
                    PrestigeTier.Bronze => state.Prestige.BronzeCount + 1,
                    PrestigeTier.Silver => state.Prestige.SilverCount + 1,
                    PrestigeTier.Gold => state.Prestige.GoldCount + 1,
                    PrestigeTier.Platin => state.Prestige.PlatinCount + 1,
                    PrestigeTier.Diamant => state.Prestige.DiamantCount + 1,
                    PrestigeTier.Meister => state.Prestige.MeisterCount + 1,
                    _ => 0
                };
                wouldNextTierBeAvailable = currentTierCountAfterPrestige >= nextTier.GetRequiredPreviousTierCount();
            }

            if (wouldNextTierBeAvailable)
            {
                int nextTierPoints = CalculateEffectivePoints(state, nextTier);
                timingWarning = $"\n⚠ {string.Format(
                    _localizationService.GetString("PrestigeTimingWarning") ?? "You are already level {0} - at level {1} you could do {2} (more PP)!",
                    currentLevel, nextTierLevel,
                    _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString())}";
            }
            else if (currentLevel >= (int)(nextTierLevel * 0.7))
            {
                var nextTierName = _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString();
                timingWarning = $"\nℹ {string.Format(
                    _localizationService.GetString("PrestigeNearNextTier") ?? "Only {0} more levels until {1} (level {2}) - keep playing!",
                    nextTierLevel - currentLevel, nextTierName, nextTierLevel)}";
            }
        }

        // 5. BONUS-PP + CHALLENGES
        int bonusPp = _prestigeService.CalculateBonusPrestigePoints(tier);
        BonusPpPreview = bonusPp > 0
            ? $"+{bonusPp} PP ({_localizationService.GetString("BonusPP") ?? "Bonus PP"})"
            : string.Empty;

        decimal challengeMult = _challengeConstraints?.GetChallengePpMultiplier() ?? 1.0m;
        ChallengePpPreview = challengeMult > 1.0m
            ? $"x{challengeMult:F2} ({_localizationService.GetString("Challenges") ?? "Challenges"})"
            : string.Empty;

        // 6. SPEEDRUN-TIMER
        var runDuration = _prestigeService.GetCurrentRunDuration();
        CurrentRunDurationText = runDuration.HasValue
            ? $"{runDuration.Value.Hours}h {runDuration.Value.Minutes:D2}m"
            : string.Empty;

        _dialogVm.ConfirmDialogTitle = $"{_localizationService.GetString("Prestige") ?? "Prestige"} → {tierName}";
        // v2.1.1 (Audit U-C06): Gewinne weiterhin in ConfirmDialogMessage, Verluste in separate
        // LossesPreview-Property — das XAML rendert sie als roten Block, damit Spieler vor dem
        // ersten Prestige nicht uebersehen was sie wirklich verlieren.
        _dialogVm.ConfirmDialogMessage = string.Join("\n", gains) + timingWarning;
        LossesPreview = string.Join("\n", losses);
    }

    /// <summary>
    /// Bereitet den Prestige-Bestätigungsdialog vor und zeigt ihn an.
    /// Liefert true bei Bestätigung, false bei Abbruch.
    /// </summary>
    public async Task<(bool confirmed, PrestigeTier selectedTier)> ShowAsync()
    {
        // Reentrancy-Guard: doppelter Aufruf während aktiven Dialogs würde TCS überschreiben
        // und den ersten Caller für immer im await hängen lassen.
        if (_confirmTcs != null && !_confirmTcs.Task.IsCompleted)
            return (false, PrestigeTier.None);

        var state = _gameStateService.State;
        var availableTiers = state.Prestige.GetAllAvailableTiers(state.PlayerLevel);

        if (availableTiers.Count == 0)
        {
            var minLevel = PrestigeTier.Bronze.GetRequiredLevel();
            _dialogVm.ShowAlertDialog(
                _localizationService.GetString("PrestigeNotAvailable") ?? "Prestige Not Available",
                string.Format(
                    _localizationService.GetString("PrestigeNotAvailableDesc") ?? "You need at least Level {0} to prestige. You are currently Level {1}.",
                    minLevel, state.PlayerLevel),
                _localizationService.GetString("OK") ?? "OK");
            return (false, PrestigeTier.None);
        }

        var highestTier = availableTiers[^1];
        _selectedTier = highestTier;

        BuildTierOptions(state, availableTiers, highestTier);
        UpdateContent(highestTier);
        // V7 (Phase 4 Ressourcen-Plan, Section 3.8): Heirloom-Wahl vorbereiten.
        BuildHeirloomCandidates();

        _dialogVm.ConfirmDialogAcceptText = _localizationService.GetString("PrestigeConfirm") ?? "Prestige now";
        _dialogVm.ConfirmDialogCancelText = _localizationService.GetString("Cancel") ?? "Cancel";
        IsTierSelectionVisible = HasMultipleTiers;

        _confirmTcs = new TaskCompletionSource<bool>();
        _dialogVm.AttachExternalConfirmTcs(_confirmTcs);
        _dialogVm.IsConfirmDialogVisible = true;
        _adService.HideBanner();

        var confirmed = await _confirmTcs.Task;

        HasMultipleTiers = false;
        IsTierSelectionVisible = false;
        AvailableTierOptions = [];

        return (confirmed, _selectedTier);
    }

    /// <summary>
    /// Bereitet die Prestige-Page-Daten vor (Page-Modus statt Modal).
    /// </summary>
    public Task<(bool confirmed, PrestigeTier selectedTier)> PreparePageAsync()
    {
        // Reentrancy-Guard (analog zu ShowAsync)
        if (_confirmTcs != null && !_confirmTcs.Task.IsCompleted)
            return Task.FromResult((false, PrestigeTier.None));

        var state = _gameStateService.State;
        var availableTiers = state.Prestige.GetAllAvailableTiers(state.PlayerLevel);

        if (availableTiers.Count == 0)
        {
            var minLevel = PrestigeTier.Bronze.GetRequiredLevel();
            _dialogVm.ShowAlertDialog(
                _localizationService.GetString("PrestigeNotAvailable") ?? "Prestige Not Available",
                string.Format(
                    _localizationService.GetString("PrestigeNotAvailableDesc") ?? "You need at least Level {0} to prestige. You are currently Level {1}.",
                    minLevel, state.PlayerLevel),
                _localizationService.GetString("OK") ?? "OK");
            return Task.FromResult((false, PrestigeTier.None));
        }

        var highestTier = availableTiers[^1];
        _selectedTier = highestTier;

        BuildTierOptions(state, availableTiers, highestTier);
        UpdateContent(highestTier);
        // V7 (Phase 4 Ressourcen-Plan, Section 3.8): Heirloom-Wahl vorbereiten.
        BuildHeirloomCandidates();

        _dialogVm.ConfirmDialogAcceptText = _localizationService.GetString("PrestigeConfirm") ?? "Prestige now";
        _dialogVm.ConfirmDialogCancelText = _localizationService.GetString("Cancel") ?? "Cancel";
        IsTierSelectionVisible = HasMultipleTiers;

        // KEIN IsConfirmDialogVisible=true — Page-Modus statt Modal.
        _confirmTcs = new TaskCompletionSource<bool>();
        _dialogVm.AttachExternalConfirmTcs(_confirmTcs);
        _adService.HideBanner();

        return WrapTcsAsync();
    }

    private async Task<(bool, PrestigeTier)> WrapTcsAsync()
    {
        if (_confirmTcs == null) return (false, PrestigeTier.None);
        var confirmed = await _confirmTcs.Task;

        HasMultipleTiers = false;
        IsTierSelectionVisible = false;
        AvailableTierOptions = [];

        return (confirmed, _selectedTier);
    }

    /// <summary>
    /// Tier-Optionen fuer Auswahl-Chips aufbauen.
    /// </summary>
    private void BuildTierOptions(GameState state, IReadOnlyList<PrestigeTier> availableTiers, PrestigeTier highestTier)
    {
        var options = new List<PrestigeTierOption>();
        for (int i = 0; i < availableTiers.Count; i++)
        {
            var t = availableTiers[i];
            var tName = _localizationService.GetString(t.GetLocalizationKey()) ?? t.ToString();
            int tPoints = CalculateEffectivePoints(state, t);
            var preservations = new List<string>();
            if (t.KeepsResearch()) preservations.Add(_localizationService.GetString("Research") ?? "Research");
            if (t.KeepsMasterTools()) preservations.Add(_localizationService.GetString("MasterTools") ?? "Master Tools");
            if (t.KeepsBuildings()) preservations.Add(_localizationService.GetString("Buildings") ?? "Buildings");
            if (t.KeepsManagers()) preservations.Add(_localizationService.GetString("Managers") ?? "Foremen");
            if (t.KeepsBestWorkers()) preservations.Add(_localizationService.GetString("Workers") ?? "Team");

            options.Add(new PrestigeTierOption
            {
                Tier = t,
                Name = tName,
                Icon = t.GetIcon(),
                Color = t.GetColorKey(),
                Points = tPoints,
                PointsText = $"+{tPoints} PP",
                BonusText = $"+{t.GetPermanentMultiplierBonus():P0}",
                PreservationText = preservations.Count > 0 ? string.Join(", ", preservations) : "",
                IsSelected = t == highestTier,
                IsRecommended = t == highestTier,
            });
        }
        AvailableTierOptions = options;
        SelectedTierIndex = availableTiers.Count - 1;
        HasMultipleTiers = availableTiers.Count > 1;
    }

    /// <summary>
    /// Berechnet die effektiven Prestige-Punkte inklusive aller Boni (identisch mit DoPrestige-Logik).
    /// Bronze-Minimum, Prestige-Pass (+50%), Gilden-Forschung (+10%) werden berücksichtigt.
    /// </summary>
    public int CalculateEffectivePoints(GameState state, PrestigeTier tier)
    {
        int basePoints = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
        int tierPoints = (int)(basePoints * tier.GetPointMultiplier());

        if (tier == PrestigeTier.Bronze && tierPoints < 10)
            tierPoints = 10;

        if (state.IsPrestigePassActive)
            tierPoints = (int)(tierPoints * 1.5m);

        if (state.GuildMembership?.ResearchPrestigePointBonus > 0)
            tierPoints = (int)(tierPoints * (1m + state.GuildMembership.ResearchPrestigePointBonus));

        return tierPoints;
    }

    // ═══════════════════════════════════════════════════════════════════
    // V7 (Phase 4 Ressourcen-Plan, Section 3.8): Heirloom-Selection
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sammelt alle Tier-4-Items im aktuellen Inventar als Heirloom-Kandidaten und befuellt
    /// die UI-Liste. Top-N wird automatisch vorselektiert (Pass-Cap berueksichtigt).
    /// Muss VOR ShowAsync/PreparePageAsync aufgerufen werden.
    /// </summary>
    public void BuildHeirloomCandidates()
    {
        var state = _gameStateService.State;
        MaxHeirloomSlots = GameBalanceConstants.GetEffectiveHeirloomSlots(state.IsPremium);

        var allProducts = CraftingProduct.GetAllProducts();
        var candidates = new List<HeirloomCandidateOption>();
        foreach (var (productId, count) in state.CraftingInventory)
        {
            if (count <= 0) continue;
            if (!allProducts.TryGetValue(productId, out var product)) continue;
            if (!product.IsHeirloomEligible) continue;

            // Jedes Stueck im Inventar = ein eigener Kandidat (Spieler kann das gleiche T4
            // mehrfach als Erbstueck mitnehmen, wenn er es mehrfach besitzt).
            for (int i = 0; i < count; i++)
            {
                candidates.Add(new HeirloomCandidateOption
                {
                    InstanceId = $"{productId}#{i}",
                    ProductId = productId,
                    Name = _localizationService.GetString(product.NameKey) ?? product.NameKey,
                    BaseValue = product.BaseValue,
                    ValueDisplay = MoneyFormatter.Format(product.BaseValue, 0),
                    Icon = GetHeirloomIcon(productId),
                    IsSelected = false
                });
            }
        }

        // v2.1.1 (Audit U-H08): Top-N nach BaseValue automatisch vorselektieren — Spieler
        // verliert keine Erbstuecke mehr wenn er ohne Auswahl auf Confirm tippt (frueher
        // ging der +8%/Run-Bonus permanent verloren).
        var sorted = candidates.OrderByDescending(c => c.BaseValue).ToList();
        for (int i = 0; i < sorted.Count && i < MaxHeirloomSlots; i++)
            sorted[i].IsSelected = true;

        HeirloomCandidates = new ObservableCollection<HeirloomCandidateOption>(sorted);
        SelectedHeirloomCount = HeirloomCandidates.Count(h => h.IsSelected);
        HasHeirloomCandidates = HeirloomCandidates.Count > 0;
    }

    /// <summary>
    /// Toggle-Command fuer einen Heirloom-Kandidaten. Respektiert MaxHeirloomSlots —
    /// wenn das Maximum schon erreicht ist und der Spieler einen weiteren waehlen will,
    /// blockt der Toggle (UI-Hint zeigt SelectionStatus).
    /// </summary>
    [RelayCommand]
    private void ToggleHeirloom(HeirloomCandidateOption? candidate)
    {
        if (candidate == null) return;

        if (candidate.IsSelected)
        {
            candidate.IsSelected = false;
            SelectedHeirloomCount = Math.Max(0, SelectedHeirloomCount - 1);
        }
        else
        {
            if (SelectedHeirloomCount >= MaxHeirloomSlots) return; // Cap erreicht
            candidate.IsSelected = true;
            SelectedHeirloomCount++;
        }

        // ObservableCollection feuert kein PropertyChanged auf Sub-Properties — Refresh erzwingen.
        var idx = HeirloomCandidates.IndexOf(candidate);
        if (idx >= 0)
        {
            HeirloomCandidates.RemoveAt(idx);
            HeirloomCandidates.Insert(idx, candidate);
        }
    }

    /// <summary>
    /// Wird vom PrestigeService aufgerufen VOR ResetProgress — schreibt die gewaehlten
    /// Heirloom-Items in <see cref="GameState.HeirloomItems"/>, sodass der Reset sie bewahrt.
    /// </summary>
    public void ApplyHeirloomSelection()
    {
        var state = _gameStateService.State;
        state.HeirloomItems.Clear();
        foreach (var candidate in HeirloomCandidates.Where(c => c.IsSelected))
            state.HeirloomItems.Add(candidate.ProductId);
    }

    private static GameIconKind GetHeirloomIcon(string productId) => productId switch
    {
        "villa" => GameIconKind.HomeCity,
        "skyscraper" => GameIconKind.OfficeBuilding,
        "imperium_hq" => GameIconKind.Bank,
        _ => GameIconKind.Crown
    };
}

/// <summary>
/// V7 (Phase 4): Auswahl-Option fuer ein Heirloom-Kandidat-Item im Prestige-Confirm-Dialog.
/// </summary>
public sealed partial class HeirloomCandidateOption : ObservableObject
{
    public string InstanceId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal BaseValue { get; set; }
    public string ValueDisplay { get; set; } = "";
    public GameIconKind Icon { get; set; } = GameIconKind.Crown;
    [ObservableProperty] private bool _isSelected;
}
