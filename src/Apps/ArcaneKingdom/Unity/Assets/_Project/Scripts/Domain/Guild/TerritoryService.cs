#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Utility;

namespace ArcaneKingdom.Domain.Guild
{
    /// <summary>
    /// Auswertung der Gebote auf ein Gebiet (Spielplan v5 Kap. 13.2).
    /// </summary>
    public enum TerritoryBidOutcome
    {
        /// <summary>Keine Gebote — Gebiet bleibt neutral oder beim aktuellen Besitzer.</summary>
        NoBids = 0,
        /// <summary>Ein eindeutiger Gewinner — Gilde uebernimmt das Gebiet direkt.</summary>
        UniqueWinner = 1,
        /// <summary>Zwei oder mehr gleiche Hoechstgebote — Klan-Match wird angesetzt.</summary>
        ClanMatchScheduled = 2
    }

    /// <summary>
    /// Ergebnis der Gebots-Auswertung.
    /// </summary>
    public sealed class TerritoryBidResolution
    {
        public TerritoryBidOutcome Outcome { get; init; }
        public string? WinnerGuildId { get; init; }
        public List<string> TiedGuildIds { get; init; } = new();
        public long WinningBid { get; init; }
        public DateTime? ClanMatchScheduledAtUtc { get; init; }
    }

    /// <summary>
    /// Klan-Match-Spec — zwei Gilden, ein Gebiet, ein fester Match-Zeitpunkt.
    /// </summary>
    public sealed class ClanMatch
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string TerritoryId { get; init; } = string.Empty;
        public string Guild1Id { get; init; } = string.Empty;
        public string Guild2Id { get; init; } = string.Empty;
        public long BidAmount { get; init; }
        public DateTime ScheduledAtUtc { get; init; }
        public ClanMatchState State { get; set; } = ClanMatchState.Scheduled;
        public Dictionary<string, int> ScoresByGuildId { get; init; } = new();
        public string? WinnerGuildId { get; set; }
    }

    public enum ClanMatchState
    {
        Scheduled = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3
    }

    /// <summary>
    /// Service fuer Gebots-Auswertung + Klan-Match-Auslosung.
    /// Pure Logik, kein Photon/Firebase-Coupling.
    /// </summary>
    public sealed class TerritoryService
    {
        /// <summary>
        /// Wertet alle aktiven Gebote eines Gebiets aus und entscheidet, ob Direct-Win oder Klan-Match.
        /// </summary>
        public TerritoryBidResolution ResolveBids(Territory territory, DateTime matchSchedulingTimeUtc)
        {
            if (territory is null) throw new ArgumentNullException(nameof(territory));
            if (territory.ActiveBids.Count == 0)
                return new TerritoryBidResolution { Outcome = TerritoryBidOutcome.NoBids };

            var maxBid = territory.ActiveBids.Values.Max();
            var topBidders = territory.ActiveBids
                .Where(kv => kv.Value == maxBid)
                .Select(kv => kv.Key)
                .ToList();

            if (topBidders.Count == 1)
            {
                return new TerritoryBidResolution
                {
                    Outcome = TerritoryBidOutcome.UniqueWinner,
                    WinnerGuildId = topBidders[0],
                    WinningBid = maxBid
                };
            }

            return new TerritoryBidResolution
            {
                Outcome = TerritoryBidOutcome.ClanMatchScheduled,
                TiedGuildIds = topBidders,
                WinningBid = maxBid,
                ClanMatchScheduledAtUtc = matchSchedulingTimeUtc
            };
        }

        /// <summary>
        /// Bewirbt ein neues Gebot. Gibt false zurueck wenn unter MinBidAmount oder Gilde schon gebot hat.
        /// </summary>
        public Result<bool> PlaceBid(Territory territory, string guildId, long bidAmount)
        {
            if (territory is null) return Result<bool>.Failure("Territory ist null");
            if (string.IsNullOrWhiteSpace(guildId)) return Result<bool>.Failure("GuildId leer");
            if (bidAmount < territory.MinBidAmount)
                return Result<bool>.Failure($"Gebot unter Minimum ({territory.MinBidAmount:N0} Gold).");
            if (territory.ActiveBids.ContainsKey(guildId))
                return Result<bool>.Failure("Gilde hat bereits ein Gebot abgegeben.");
            territory.ActiveBids[guildId] = bidAmount;
            return Result<bool>.Success(true);
        }

        /// <summary>
        /// Klan-Match auslosen aus 2 gleichberechtigten Bietern.
        /// </summary>
        public ClanMatch ScheduleClanMatch(string territoryId, string guild1Id, string guild2Id,
                                            long bidAmount, DateTime scheduledAtUtc)
        {
            return new ClanMatch
            {
                TerritoryId = territoryId,
                Guild1Id = guild1Id,
                Guild2Id = guild2Id,
                BidAmount = bidAmount,
                ScheduledAtUtc = scheduledAtUtc,
                State = ClanMatchState.Scheduled
            };
        }

        /// <summary>
        /// Klan-Match abschliessen: Sieger ermitteln aus Punkte-Tally.
        /// </summary>
        public void CompleteClanMatch(ClanMatch match, IReadOnlyDictionary<string, int> finalScores)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));
            if (match.State == ClanMatchState.Completed) return;

            foreach (var kv in finalScores) match.ScoresByGuildId[kv.Key] = kv.Value;

            var score1 = finalScores.GetValueOrDefault(match.Guild1Id);
            var score2 = finalScores.GetValueOrDefault(match.Guild2Id);

            match.WinnerGuildId = score1 > score2 ? match.Guild1Id : (score2 > score1 ? match.Guild2Id : null);
            match.State = ClanMatchState.Completed;
        }

        /// <summary>
        /// Gebiet einer Gilde zuweisen (nach Direct-Win oder Klan-Match-Sieg).
        /// </summary>
        public void AssignTerritory(Territory territory, string winnerGuildId, DateTime nowUtc)
        {
            if (territory is null) throw new ArgumentNullException(nameof(territory));
            territory.OwnerGuildId = winnerGuildId;
            territory.CapturedAtUtc = nowUtc;
            territory.ActiveBids.Clear();
            territory.BidPhaseEndsAtUtc = null;
            // Naechste Bid-Phase ist eine Woche spaeter (Spielplan v5 Kap. 13.3)
            territory.NextMatchAtUtc = nowUtc.AddDays(7);
        }
    }
}
