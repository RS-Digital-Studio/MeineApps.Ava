using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet den Battle Pass (50 Tiers, Free + Premium Track, 42-Tage-Saison).
/// Automatische XP-Vergabe bei Aufträgen, MiniGames und Workshop-Upgrades.
/// Premium-Track kann per IAP freigeschaltet werden.
/// </summary>
public sealed class BattlePassService : IBattlePassService, IDisposable
{
    private readonly IGameStateService _gameState;
    private readonly IPurchaseService _purchaseService;
    private readonly IWorkerService _workerService;
    private readonly ICraftingService _craftingService;
    private bool _disposed;

    public event Action? BattlePassUpdated;

    public BattlePassService(IGameStateService gameState, IPurchaseService purchaseService, IWorkerService workerService, ICraftingService craftingService)
    {
        _gameState = gameState;
        _purchaseService = purchaseService;
        _workerService = workerService;
        _craftingService = craftingService;

        // Automatische XP-Vergabe bei verschiedenen Spielaktionen
        _gameState.OrderCompleted += OnOrderCompleted;
        _gameState.MiniGameResultRecorded += OnMiniGameResultRecorded;
        _gameState.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameState.WorkerHired += OnWorkerHired;
        _workerService.WorkerLevelUp += OnWorkerLevelUp;
        _craftingService.CraftingProductCollected += OnCraftingProductCollected;
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

            // BaseIncomeAtSeasonStart verwenden, damit Rewards über die Saison fixiert bleiben
            var baseIncome = bp.BaseIncomeAtSeasonStart > 0
                ? bp.BaseIncomeAtSeasonStart
                : _gameState.State.TotalIncomePerSecond;
            var rewards = BattlePass.GeneratePremiumRewards(baseIncome, bp.SeasonNumber);
            var reward = rewards.FirstOrDefault(r => r.Tier == tier);
            if (reward == null) return;

            ApplyReward(reward);
            bp.ClaimedPremiumTiers.Add(tier);
        }
        else
        {
            // Free-Track
            if (bp.ClaimedFreeTiers.Contains(tier)) return;

            var baseIncome = bp.BaseIncomeAtSeasonStart > 0
                ? bp.BaseIncomeAtSeasonStart
                : _gameState.State.TotalIncomePerSecond;
            var rewards = BattlePass.GenerateFreeRewards(baseIncome);
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
        bp.BaseIncomeAtSeasonStart = _gameState.State.TotalIncomePerSecond;

        // SeasonTheme wird automatisch aus SeasonNumber berechnet (SeasonNumber % 4)
        // → Farbe, Icon und Capstone-Reward passen sich der neuen Saison an
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
    /// SpeedBoost-Rewards setzen den SpeedBoostEndTime im GameState (stackt mit laufenden Boosts).
    /// </summary>
    private void ApplyReward(BattlePassReward reward)
    {
        if (reward.MoneyReward > 0)
            _gameState.AddMoney(reward.MoneyReward);

        if (reward.XpReward > 0)
            _gameState.AddXp(reward.XpReward);

        if (reward.GoldenScrewReward > 0)
            _gameState.AddGoldenScrews(reward.GoldenScrewReward);

        // SpeedBoost: Dauer zum bestehenden Boost addieren (stackt mit laufenden Boosts)
        if (reward.RewardType == BattlePassRewardType.SpeedBoost && reward.SpeedBoostMinutes > 0)
        {
            var state = _gameState.State;
            var now = DateTime.UtcNow;
            var currentEnd = state.SpeedBoostEndTime > now ? state.SpeedBoostEndTime : now;
            state.SpeedBoostEndTime = currentEnd.AddMinutes(reward.SpeedBoostMinutes);
        }
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

    private void OnWorkerHired(object? sender, EventArgs e)
    {
        // +30 BP-XP pro eingestelltem Arbeiter
        AddXp(30, "worker_hired");
    }

    private void OnWorkerLevelUp(object? sender, Worker worker)
    {
        // +20 BP-XP pro Worker-Level-Up (Training)
        AddXp(20, "worker_level_up");
    }

    private void OnCraftingProductCollected()
    {
        // +40 BP-XP pro eingesammeltem Crafting-Produkt
        AddXp(40, "crafting_collected");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _gameState.OrderCompleted -= OnOrderCompleted;
        _gameState.MiniGameResultRecorded -= OnMiniGameResultRecorded;
        _gameState.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameState.WorkerHired -= OnWorkerHired;
        _workerService.WorkerLevelUp -= OnWorkerLevelUp;
        _craftingService.CraftingProductCollected -= OnCraftingProductCollected;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
