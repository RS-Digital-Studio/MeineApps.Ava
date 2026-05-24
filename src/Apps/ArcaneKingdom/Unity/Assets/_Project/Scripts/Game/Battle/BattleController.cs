#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.World;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Battle
{
    /// <summary>
    /// Orchestriert einen Welt-Kampf: Setup -> Kampf-Loop -> Settlement.
    ///
    /// SKELETT: Strukturelle Verdrahtung der BattleEngine mit Belohnungs-Vergabe.
    /// AI- und UI-Anteile folgen in der MVP-Phase.
    /// </summary>
    public sealed class BattleController
    {
        public sealed class StartParams
        {
            public WorldDefinition World { get; init; } = default!;
            public NodeDefinition Node { get; init; } = default!;
            public int Stars { get; init; } = 1;
            public List<string> PlayerDeckCardInstanceIds { get; init; } = new();
            public int Seed { get; init; }
        }

        public sealed class Result
        {
            public BattleResult Outcome { get; init; }
            public int StarsAwarded { get; init; }
            public int GoldAwarded { get; init; }
            public int ExpAwarded { get; init; }
        }

        private readonly ISaveService<PlayerSave> _save;
        private readonly IAnalyticsService _analytics;
        private readonly IReadOnlyDictionary<string, CardDefinition> _cardDefinitions;

        public BattleController(ISaveService<PlayerSave> save, IAnalyticsService analytics,
                                IReadOnlyDictionary<string, CardDefinition> cardDefinitions)
        {
            _save = save;
            _analytics = analytics;
            _cardDefinitions = cardDefinitions;
        }

        public async UniTask<Result> RunAsync(StartParams p, CancellationToken ct = default)
        {
            GameLogger.Info("Battle", $"Start: {p.World.Id} / {p.Node.Id} / {p.Stars}* (seed {p.Seed})");
            _analytics.Track("battle_start", new Dictionary<string, object>
            {
                ["world"] = p.World.Id, ["node"] = p.Node.Id, ["stars"] = p.Stars, ["deck_size"] = p.PlayerDeckCardInstanceIds.Count
            });

            // Pre-flight: Energie abziehen
            var energyOk = false;
            await _save.MutateAsync(save => { energyOk = save.Currencies.SpendEnergy(p.Node.EnergyCost); return save; }, ct);
            if (!energyOk)
            {
                GameLogger.Warning("Battle", "Nicht genug Energie.");
                return new Result { Outcome = BattleResult.Undecided };
            }

            // Engine aufsetzen
            var state = new BattleState(p.Seed, playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, _cardDefinitions);
            engine.Setup(p.PlayerDeckCardInstanceIds, p.Node.EnemyDeckCardIds);

            // TODO MVP: Run battle loop (PlayerTurn / EnemyTurn / TurnEnd) bis Settlement.
            // Aktuell sofort Sieg simulieren (Stub).
            await UniTask.Yield(ct);
            state.Result = BattleResult.PlayerWins;

            // Settlement: Belohnungen anwenden
            var gold = p.Node.GoldReward(p.Stars);
            var exp = p.Node.ExpReward(p.Stars);
            await _save.MutateAsync(save =>
            {
                save.Currencies.AddGold(gold);
                save.Profile.ExpTotal += exp;
                if (!save.WorldProgress.TryGetValue(p.World.Id, out var wp))
                {
                    wp = new WorldProgress(p.World.Id);
                    save.WorldProgress[p.World.Id] = wp;
                }
                if (!wp.StarsByNodeId.TryGetValue(p.Node.Id, out var existing) || p.Stars > existing)
                    wp.StarsByNodeId[p.Node.Id] = p.Stars;
                wp.LastPlayedAtUtc = DateTime.UtcNow;
                return save;
            }, ct);

            _analytics.Track("battle_end", new Dictionary<string, object>
            {
                ["result"] = state.Result.ToString(), ["gold"] = gold, ["exp"] = exp
            });

            return new Result { Outcome = state.Result, StarsAwarded = p.Stars, GoldAwarded = gold, ExpAwarded = exp };
        }
    }
}
