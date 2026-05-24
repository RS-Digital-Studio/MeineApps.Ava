#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using Cysharp.Threading.Tasks;

namespace ArcaneKingdom.Game.Arena
{
    /// <summary>
    /// Arena-PvP-Steuerung: Matchmaking, asynchroner Kampf, Rang-Update.
    ///
    /// SKELETT: API-Schema, Implementierung folgt mit Photon und Backend.
    /// </summary>
    public sealed class ArenaController
    {
        public enum MatchOutcome { Win, Loss, Disconnect }

        public sealed class MatchSummary
        {
            public string OpponentId { get; init; } = string.Empty;
            public int OpponentRank { get; init; }
            public MatchOutcome Outcome { get; init; }
            public int RankChange { get; init; }
            public int NewRank { get; init; }
        }

        private readonly IAnalyticsService _analytics;

        public ArenaController(IAnalyticsService analytics)
        {
            _analytics = analytics;
        }

        public async UniTask<MatchSummary> StartMatchAsync(CancellationToken ct = default)
        {
            _analytics.Track("arena_start_search");
            // TODO MVP: Matchmaking via Glicko-2, Opponent-Snapshot von Firebase laden, Async-PvP via BattleEngine.
            await UniTask.Delay(500, cancellationToken: ct);
            GameLogger.Warning("Arena", "StartMatchAsync — STUB.");
            return new MatchSummary { OpponentId = "stub-opponent", OpponentRank = 100, Outcome = MatchOutcome.Win, RankChange = 25, NewRank = 75 };
        }

        public async UniTask<bool> SurrenderAsync(CancellationToken ct = default)
        {
            await UniTask.Yield(ct);
            _analytics.Track("arena_surrender");
            return true;
        }

        /// <summary>
        /// Berechnet Rangpunkte-Aenderung nach Glicko-2-aehnlicher Heuristik
        /// (DESIGN.md Kap. 11.3). Pure-C# fuer Testbarkeit.
        /// </summary>
        public static int CalculateRankChange(int ownRank, int opponentRank, MatchOutcome outcome)
        {
            var diff = ownRank - opponentRank;        // positiv = Gegner schwaecher
            return outcome switch
            {
                MatchOutcome.Win when diff >= 50 => 25,
                MatchOutcome.Win when diff > 0   => Math.Max(10, 25 - diff / 5),
                MatchOutcome.Win when diff <= -50 => Math.Min(50, 30 - diff / 10),
                MatchOutcome.Win                 => 25,
                MatchOutcome.Loss when diff >= 50 => -Math.Min(50, 30 + diff / 10),
                MatchOutcome.Loss when diff > 0   => -Math.Min(30, 20 + diff / 5),
                MatchOutcome.Loss when diff <= -50 => -Math.Max(5, 15 + diff / 5),
                MatchOutcome.Loss                 => -20,
                MatchOutcome.Disconnect           => -50,
                _ => 0
            };
        }
    }
}
