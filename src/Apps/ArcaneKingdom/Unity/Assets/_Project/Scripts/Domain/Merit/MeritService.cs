#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Domain.Player;

namespace ArcaneKingdom.Domain.Merit
{
    /// <summary>
    /// Eintrag in der Merit-Rangliste (Spielplan v5 Kap. 15.1).
    /// Wird vom Server geliefert (Photon/Firebase) — lokal mit Bot-Mocks fuer Tests.
    /// </summary>
    public sealed class MeritRankEntry
    {
        public int Rank { get; init; }
        public string PlayerId { get; init; } = string.Empty;
        public string PlayerName { get; init; } = string.Empty;
        public string? GuildTag { get; init; }
        public int Level { get; init; }
        public long MeritPoints { get; init; }
        public string? AvatarKey { get; init; }
    }

    /// <summary>
    /// Pure Logik fuer Merit-Vergabe (kein State, kein Server-Call).
    /// Mutation des PlayerCurrencies.MeritPoints erfolgt durch den Caller.
    /// </summary>
    public sealed class MeritService
    {
        /// <summary>
        /// Liefert die Merit-Vergabe-Summe fuer eine Quelle mit Magnitude.
        /// Magnitude ist quell-spezifisch (Schaden fuer ThiefAttack, Stufe fuer SaisonPass, etc.).
        /// </summary>
        public long ComputeReward(MeritSource source, long magnitude, bool isWin = false)
        {
            // M11: Magnitude defensiv begrenzen — kein Negativwert (sonst negative Merit-Vergabe) und
            // die quell-spezifischen Maxima cappen (sonst beliebig hohe Punkte aus manipuliertem Input).
            if (magnitude < 0) magnitude = 0;
            return source switch
            {
                MeritSource.DailyQuest        => MeritRewardTable.DailyQuestMin
                                                  + (Math.Min(magnitude, 100) * (MeritRewardTable.DailyQuestMax - MeritRewardTable.DailyQuestMin) / 100),
                MeritSource.ArenaBattle       => isWin ? MeritRewardTable.ArenaWin : MeritRewardTable.ArenaLoss,
                MeritSource.ThiefAttack       => Math.Min(magnitude / 1000, MeritRewardTable.ThiefPerEncounterCap),
                MeritSource.GuildContribution => isWin ? MeritRewardTable.GuildClanMatchWin : MeritRewardTable.GuildTechDonation,
                MeritSource.EventCompleted    => Math.Max(50, Math.Min(500, magnitude)),
                MeritSource.WorldBossDefeated => magnitude == 10 ? MeritRewardTable.WorldBoss10 : MeritRewardTable.WorldBoss5,
                MeritSource.SaisonPassTier    => Math.Min(magnitude, 30) * MeritRewardTable.SaisonPassTierStep,
                MeritSource.Achievement       => Math.Min(magnitude, 5_000),
                _ => 0
            };
        }

        /// <summary>
        /// Wendet die Merit-Vergabe direkt auf den Spieler an (respektiert Account-Cap).
        /// </summary>
        public long Award(PlayerSave save, MeritSource source, long magnitude, bool isWin = false)
        {
            if (save is null) throw new ArgumentNullException(nameof(save));
            var amount = ComputeReward(source, magnitude, isWin);
            if (amount <= 0) return 0;
            var before = save.Currencies.MeritPoints;
            save.Currencies.AddMeritPoints(amount);
            return save.Currencies.MeritPoints - before;
        }

        /// <summary>
        /// Sortiert eine Rangliste neu nach Merit-Punkten (absteigend) und vergibt Rang-Nummern.
        /// </summary>
        public IReadOnlyList<MeritRankEntry> RankByMerit(IEnumerable<MeritRankEntry> entries)
        {
            return entries
                .OrderByDescending(e => e.MeritPoints)
                .ThenByDescending(e => e.Level)
                .Select((e, i) => new MeritRankEntry
                {
                    Rank = i + 1,
                    PlayerId = e.PlayerId,
                    PlayerName = e.PlayerName,
                    GuildTag = e.GuildTag,
                    Level = e.Level,
                    MeritPoints = e.MeritPoints,
                    AvatarKey = e.AvatarKey
                })
                .ToList();
        }

        /// <summary>
        /// Liefert die Belohnung fuer eine Merit-Rang-Position (Spielplan v5 Kap. 15.1 Rewards List).
        /// </summary>
        public MeritRankReward GetRewardForRank(int rank)
        {
            // Plan-Werte aus V5 sind nur grob skizziert — wir nehmen sinnvolle Stufen
            if (rank == 1) return new MeritRankReward { Gold = 100_000, Diamonds = 100, Title = "merit.title.rank1" };
            if (rank <= 3) return new MeritRankReward { Gold = 50_000, Diamonds = 50, Title = "merit.title.top3" };
            if (rank <= 10) return new MeritRankReward { Gold = 20_000, Diamonds = 25, Title = "merit.title.top10" };
            if (rank <= 50) return new MeritRankReward { Gold = 10_000, Diamonds = 10 };
            if (rank <= 100) return new MeritRankReward { Gold = 5_000, Diamonds = 5 };
            return new MeritRankReward { Gold = 1_000 };
        }
    }

    /// <summary>Belohnungs-Datensatz fuer einen Merit-Rang.</summary>
    public sealed class MeritRankReward
    {
        public long Gold { get; init; }
        public int Diamonds { get; init; }
        public string? Title { get; init; }
        public List<string> CardIds { get; init; } = new();
    }
}
