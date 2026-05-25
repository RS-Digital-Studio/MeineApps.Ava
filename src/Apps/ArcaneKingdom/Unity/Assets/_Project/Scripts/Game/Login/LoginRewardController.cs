#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.Player;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Login
{
    /// <summary>
    /// Tageslogin-Belohnungs-Controller (Designplan v4 Oeko Kap. 5.1).
    /// Liest <c>Resources/Data/login_rewards.json</c> und verteilt die Belohnung des
    /// naechsten Tages (1-30, danach Zyklus von vorne) wenn der Spieler heute noch nicht geclaimed hat.
    /// </summary>
    public sealed class LoginRewardController
    {
        private const string LoginRewardsResourcePath = "Data/login_rewards";

        private readonly ISaveService<PlayerSave> _save;
        private readonly SternkartenService _sternkartenService;
        private readonly IAnalyticsService _analytics;

        private LoginRewardsConfig? _config;

        public LoginRewardController(
            ISaveService<PlayerSave> save,
            SternkartenService sternkartenService,
            IAnalyticsService analytics)
        {
            _save = save;
            _sternkartenService = sternkartenService;
            _analytics = analytics;
        }

        private LoginRewardsConfig EnsureConfig()
        {
            if (_config != null) return _config;
            var textAsset = Resources.Load<TextAsset>(LoginRewardsResourcePath);
            if (textAsset == null)
            {
                GameLogger.Warning("LoginReward",
                    $"Resources/{LoginRewardsResourcePath}.json fehlt — keine Login-Belohnungen.");
                _config = new LoginRewardsConfig { cycleDays = 30, rewards = new List<DayRewardDto>() };
                return _config;
            }
            try
            {
                _config = JsonConvert.DeserializeObject<LoginRewardsConfig>(textAsset.text) ?? new LoginRewardsConfig();
                if (_config.rewards == null) _config.rewards = new List<DayRewardDto>();
                return _config;
            }
            catch (Exception ex)
            {
                GameLogger.Error("LoginReward", $"Config parsen fehlgeschlagen: {ex.Message}");
                _config = new LoginRewardsConfig { cycleDays = 30, rewards = new List<DayRewardDto>() };
                return _config;
            }
        }

        /// <summary>
        /// Liefert den naechsten Belohnungs-Tag (1-30) und seine Items, ohne etwas zu mutieren.
        /// Gibt null zurueck wenn der Spieler heute schon geclaimed hat.
        /// </summary>
        public DayRewardPreview? PreviewToday(PlayerSave save, DateTime nowUtc)
        {
            if (!save.Sternkarten.Tracker.CanClaimToday(nowUtc)) return null;
            var cfg = EnsureConfig();
            var nextDay = save.Sternkarten.Tracker.NextDayInCycle;
            var reward = cfg.rewards.Find(r => r.day == nextDay);
            if (reward == null) return null;
            return new DayRewardPreview(nextDay, reward.items ?? new List<RewardItemDto>());
        }

        /// <summary>
        /// Claimed die heutige Belohnung — bucht Items in das PlayerSave-Inventar.
        /// </summary>
        public async UniTask<Result<DayRewardOutcome>> ClaimTodayAsync(CancellationToken ct = default)
        {
            var saveR = await _save.LoadAsync(ct);
            if (!saveR.IsSuccess || saveR.Value == null)
                return Result<DayRewardOutcome>.Failure(saveR.ErrorMessage ?? "Save nicht geladen");
            var save = saveR.Value;

            var nowUtc = DateTime.UtcNow;
            var preview = PreviewToday(save, nowUtc);
            if (preview == null)
                return Result<DayRewardOutcome>.Failure("Heute bereits geclaimed.");

            var grantedItems = new List<RewardItemDto>(preview.Items);

            var mutation = await _save.MutateAsync(state =>
            {
                foreach (var item in grantedItems)
                {
                    ApplyRewardItem(state, item);
                }
                state.Sternkarten.Tracker.MarkClaimed(nowUtc);
                return state;
            }, ct);

            if (!mutation.IsSuccess)
                return Result<DayRewardOutcome>.Failure(mutation.ErrorMessage ?? "Save-Mutation fehlgeschlagen");

            _analytics.Track("login_reward_claimed", new Dictionary<string, object>
            {
                ["day"] = preview.Day,
                ["item_count"] = grantedItems.Count
            });

            return Result<DayRewardOutcome>.Success(new DayRewardOutcome(preview.Day, grantedItems));
        }

        // ============================================================================
        // Reward-Anwendung
        // ============================================================================

        private void ApplyRewardItem(PlayerSave state, RewardItemDto item)
        {
            switch (item.type)
            {
                case "gold":
                    state.Currencies.AddGold(item.magnitude);
                    break;
                case "diamonds":
                    state.Currencies.AddDiamond(item.magnitude);
                    break;
                case "common_scrap":
                    state.Currencies.AddScraps(Domain.Cards.ScrapType.Common, item.magnitude);
                    break;
                case "rare_scrap":
                    state.Currencies.AddScraps(Domain.Cards.ScrapType.Rare, item.magnitude);
                    break;
                case "epic_scrap":
                    state.Currencies.AddScraps(Domain.Cards.ScrapType.Epic, item.magnitude);
                    break;
                case "legendary_scrap":
                    state.Currencies.AddScraps(Domain.Cards.ScrapType.Legendary, item.magnitude);
                    break;
                case "sternkarte":
                    if (Enum.TryParse<SternkartenStufe>(item.sternkartenStufe ?? "Bronze", out var stufe))
                        _sternkartenService.AddSternkarte(state.Sternkarten.Inventory, stufe, item.magnitude);
                    break;
                case "rune_fragment":
                case "exp_potion":
                    // TODO Phase 2: Rune-Fragment-Inventar + EXP-Tranke-Persistenz hinzufuegen
                    GameLogger.Warning("LoginReward", $"Item-Typ '{item.type}' noch nicht implementiert — uebersprungen.");
                    break;
                case "card_random_1star":
                case "card_random_2star":
                case "card_random_3star":
                case "card_random_4star":
                case "card_chosen_2star":
                case "card_chosen_3star":
                    // TODO Phase 2: Karten-Drop-Logik (zufaellig nach Rarity + Pool) implementieren.
                    // Aktuell wird das als Achievement-Pending-Claim hinterlegt fuer spaeteres Auswaehlen.
                    state.PendingClaims.Add(new Domain.Save.PendingClaim
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Kind = Domain.Save.PendingClaimKind.Card,
                        SubType = item.type,
                        Amount = item.magnitude,
                        SourceKey = "login_reward",
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    break;
                default:
                    GameLogger.Warning("LoginReward", $"Unbekannter Item-Typ: {item.type}");
                    break;
            }
        }

        // ============================================================================
        // DTOs + Outcomes
        // ============================================================================

        [Serializable]
        private sealed class LoginRewardsConfig
        {
            public int cycleDays = 30;
            public List<DayRewardDto>? rewards;
        }

        [Serializable]
        private sealed class DayRewardDto
        {
            public int day;
            public List<RewardItemDto>? items;
        }

        [Serializable]
        public sealed class RewardItemDto
        {
            public string type = string.Empty;
            public int magnitude = 1;
            public string? sternkartenStufe;
        }

        public sealed class DayRewardPreview
        {
            public int Day { get; }
            public IReadOnlyList<RewardItemDto> Items { get; }
            public DayRewardPreview(int day, IReadOnlyList<RewardItemDto> items) { Day = day; Items = items; }
        }

        public sealed class DayRewardOutcome
        {
            public int Day { get; }
            public IReadOnlyList<RewardItemDto> GrantedItems { get; }
            public DayRewardOutcome(int day, IReadOnlyList<RewardItemDto> items) { Day = day; GrantedItems = items; }
        }
    }
}
