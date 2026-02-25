using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet den Battle Pass (30 Tiers, Free + Premium Track, 30-Tage-Saison).
/// Automatische XP-Vergabe bei Aufträgen, MiniGames und Workshop-Upgrades.
/// Premium-Track kann per IAP freigeschaltet werden.
/// </summary>
public class BattlePassService : IBattlePassService
{
    private readonly IGameStateService _gameState;
    private readonly IPurchaseService _purchaseService;

    public event Action? BattlePassUpdated;

    public BattlePassService(IGameStateService gameState, IPurchaseService purchaseService)
    {
        _gameState = gameState;
        _purchaseService = purchaseService;

        // Automatische XP-Vergabe bei verschiedenen Spielaktionen
        _gameState.OrderCompleted += OnOrderCompleted;
        _gameState.MiniGameResultRecorded += OnMiniGameResultRecorded;
        _gameState.WorkshopUpgraded += OnWorkshopUpgraded;
    }

    public void AddXp(int amount, string source)
    {
        if (amount <= 0) return;

        var bp = _gameState.State.BattlePass;
        if (bp.IsSeasonExpired) return;

        int tierUps = bp.AddXp(amount);

        _gameState.MarkDirty();

        if (tierUps > 0 || amount > 0)
            BattlePassUpdated?.Invoke();
    }

    public void ClaimReward(int tier, bool isPremium)
    {
        var bp = _gameState.State.BattlePass;

        // Tier muss erreicht sein
        if (tier > bp.CurrentTier) return;

        if (isPremium)
        {
            // Premium-Track benötigt Premium-Status
            if (!bp.IsPremium) return;
            if (bp.ClaimedPremiumTiers.Contains(tier)) return;

            var rewards = BattlePass.GeneratePremiumRewards(_gameState.State.TotalIncomePerSecond, bp.SeasonNumber);
            var reward = rewards.FirstOrDefault(r => r.Tier == tier);
            if (reward == null) return;

            ApplyReward(reward);
            bp.ClaimedPremiumTiers.Add(tier);
        }
        else
        {
            // Free-Track
            if (bp.ClaimedFreeTiers.Contains(tier)) return;

            var rewards = BattlePass.GenerateFreeRewards(_gameState.State.TotalIncomePerSecond);
            var reward = rewards.FirstOrDefault(r => r.Tier == tier);
            if (reward == null) return;

            ApplyReward(reward);
            bp.ClaimedFreeTiers.Add(tier);
        }

        _gameState.MarkDirty();
        BattlePassUpdated?.Invoke();
    }

    public void CheckNewSeason()
    {
        var bp = _gameState.State.BattlePass;

        if (!bp.IsSeasonExpired) return;

        // Neue Saison starten: Tiers zurücksetzen, Premium-Status bleibt für aktuelle Saison
        bp.SeasonNumber++;
        bp.CurrentTier = 0;
        bp.CurrentXp = 0;
        bp.ClaimedFreeTiers.Clear();
        bp.ClaimedPremiumTiers.Clear();
        bp.IsPremium = false; // Premium muss pro Saison erneut gekauft werden
        bp.SeasonStartDate = DateTime.UtcNow;

        // SeasonTheme wird automatisch aus SeasonNumber berechnet (SeasonNumber % 4)
        // → Farbe, Icon und Capstone-Reward passen sich der neuen Saison an
        System.Diagnostics.Debug.WriteLine(
            $"[BattlePass] Neue Saison #{bp.SeasonNumber} gestartet: Theme={bp.SeasonTheme}, Farbe={bp.SeasonThemeColor}, Icon={bp.SeasonThemeIcon}");

        _gameState.MarkDirty();
        BattlePassUpdated?.Invoke();
    }

    public async Task UpgradeToPremiumAsync()
    {
        var bp = _gameState.State.BattlePass;
        if (bp.IsPremium) return;

        // Echten IAP-Kauf durchführen
        var success = await _purchaseService.PurchaseConsumableAsync("battle_pass_season");
        if (!success) return;

        bp.IsPremium = true;

        _gameState.MarkDirty();
        BattlePassUpdated?.Invoke();
    }

    /// <summary>
    /// Wendet eine Battle-Pass-Belohnung an.
    /// </summary>
    private void ApplyReward(BattlePassReward reward)
    {
        if (reward.MoneyReward > 0)
            _gameState.AddMoney(reward.MoneyReward);

        if (reward.XpReward > 0)
            _gameState.AddXp(reward.XpReward);

        if (reward.GoldenScrewReward > 0)
            _gameState.AddGoldenScrews(reward.GoldenScrewReward);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT-HANDLER (automatische XP-Vergabe)
    // ═══════════════════════════════════════════════════════════════════════

    private void OnOrderCompleted(object? sender, EventArgs e)
    {
        // +100 BP-XP pro abgeschlossenem Auftrag
        AddXp(100, "order_completed");
    }

    private void OnMiniGameResultRecorded(object? sender, EventArgs e)
    {
        // +50 BP-XP pro MiniGame-Ergebnis
        AddXp(50, "minigame_result");
    }

    private void OnWorkshopUpgraded(object? sender, EventArgs e)
    {
        // +25 BP-XP pro Workshop-Upgrade
        AddXp(25, "workshop_upgraded");
    }
}
