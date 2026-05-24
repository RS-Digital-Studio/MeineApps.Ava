#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Guild
{
    /// <summary>
    /// Gilden-Snapshot (DESIGN.md Kap. 12). Live-State (Mitglieder, Beitraege) wird im
    /// Backend (Firestore) verwaltet — der Client erhaelt Snapshots.
    ///
    /// <para>Hinweis: Klasse heisst absichtlich <c>GuildSnapshot</c> und NICHT
    /// <c>Guild</c>, damit es keinen Namens-Konflikt mit dem
    /// <c>ArcaneKingdom.Domain.Guild</c>-Namespace gibt (Compiler-CS0118 in
    /// referenzierenden Assemblies sonst).</para>
    /// </summary>
    [Serializable]
    public sealed class GuildSnapshot
    {
        public string Id { get; }
        public string Name { get; set; }
        public string Tag { get; }               // 5 Zeichen, unveraenderlich
        public string Slogan { get; set; }
        public int Level { get; set; }
        public long TotalContributionPoints { get; set; }
        public GuildJoinPolicy JoinPolicy { get; set; }
        public string LeaderId { get; set; }
        public List<GuildMember> Members { get; }
        public List<string> TerritoryIds { get; }
        public Dictionary<string, int> TechTreeLevels { get; }
        public DateTime CreatedAtUtc { get; }

        public GuildSnapshot(string id, string name, string tag, string leaderId, DateTime createdAtUtc)
        {
            if (tag.Length != 5) throw new ArgumentException("Gilden-Tag muss exakt 5 Zeichen lang sein.", nameof(tag));
            if (name.Length is < 3 or > 20) throw new ArgumentException("Gilden-Name muss 3-20 Zeichen lang sein.", nameof(name));

            Id = id;
            Name = name;
            Tag = tag;
            Slogan = string.Empty;
            Level = 1;
            TotalContributionPoints = 0;
            JoinPolicy = GuildJoinPolicy.OnRequest;
            LeaderId = leaderId;
            Members = new List<GuildMember>();
            TerritoryIds = new List<string>();
            TechTreeLevels = new Dictionary<string, int>();
            CreatedAtUtc = createdAtUtc;
        }

        /// <summary>
        /// Max-Mitglieder auf Basis des Gilden-Levels (DESIGN.md Kap. 12.1).
        /// LV 1: 30, LV 5: 40, LV 10: 50.
        /// </summary>
        public int MaxMembers => Level switch
        {
            < 5 => 30,
            < 10 => 40,
            _ => 50
        };

        /// <summary>
        /// Naechste Level-Up-Schwelle (kumulierte Punkte).
        /// </summary>
        public static long ContributionRequiredForLevel(int targetLevel) => targetLevel switch
        {
            <= 1 => 0,
            2 => 100_000,
            3 => 500_000,
            4 => 1_500_000,
            5 => 5_000_000,
            6 => 10_000_000,
            7 => 18_000_000,
            8 => 28_000_000,
            9 => 40_000_000,
            10 => 50_000_000,
            _ => long.MaxValue
        };

        public int LevelForContribution(long total)
        {
            for (var lv = 10; lv >= 1; lv--)
                if (total >= ContributionRequiredForLevel(lv)) return lv;
            return 1;
        }
    }

    [Serializable]
    public sealed class GuildMember
    {
        public string PlayerId { get; }
        public string DisplayName { get; set; }
        public int PlayerLevel { get; set; }
        public GuildRole Role { get; set; }
        public long SeasonContribution { get; set; }
        public long TotalContribution { get; set; }
        public DateTime JoinedAtUtc { get; }
        public DateTime LastSeenAtUtc { get; set; }

        public GuildMember(string playerId, string displayName, int playerLevel, GuildRole role, DateTime joinedAtUtc)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            PlayerLevel = playerLevel;
            Role = role;
            JoinedAtUtc = joinedAtUtc;
            LastSeenAtUtc = joinedAtUtc;
        }
    }
}
