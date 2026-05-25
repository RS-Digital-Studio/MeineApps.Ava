#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Core.Utility;

namespace ArcaneKingdom.Domain.World
{
    /// <summary>
    /// Pure Domain-Logik fuer das Prestige-System (Designplan v4 Oeko Kap. 6).
    /// Steuert Welt-Aufwertung I-IV, Sterne-Reset, Stat-Multiplier, Drop-Bonus, Daily-Income-Multiplier.
    /// </summary>
    public sealed class PrestigeService
    {
        /// <summary>
        /// Prueft, ob eine Welt zur naechsten Prestige-Stufe aufgewertet werden kann.
        /// Voraussetzung: Alle Level der Welt auf 3 Sternen (Designplan v4 Oeko Kap. 6.2).
        /// </summary>
        public Result CanUpgradePrestige(
            PrestigeStufe currentStufe,
            IReadOnlyDictionary<string, int> nodeStars,
            long playerGold)
        {
            if (currentStufe == PrestigeStufe.IV)
                return Result.Failure("Prestige-Stufe IV ist die maximale Stufe.");

            // Alle Nodes muessen mindestens 3 Sterne haben
            if (nodeStars == null || nodeStars.Count == 0)
                return Result.Failure("Keine Welt-Nodes geladen.");
            foreach (var kv in nodeStars)
            {
                if (kv.Value < 3)
                    return Result.Failure($"Node '{kv.Key}' hat nur {kv.Value} Sterne (3 noetig).");
            }

            var cost = PrestigeStufeBalancing.GetUpgradeGoldCost(currentStufe);
            if (cost < 0)
                return Result.Failure("Kein gueltiger Upgrade-Pfad.");
            if (playerGold < cost)
                return Result.Failure($"Nicht genug Gold (benoetigt: {cost:N0}, vorhanden: {playerGold:N0}).");
            return Result.Success();
        }

        /// <summary>
        /// Liefert die naechste Prestige-Stufe nach einem erfolgreichen Upgrade.
        /// </summary>
        public PrestigeStufe NextStufe(PrestigeStufe current) => current switch
        {
            PrestigeStufe.Normal => PrestigeStufe.I,
            PrestigeStufe.I      => PrestigeStufe.II,
            PrestigeStufe.II     => PrestigeStufe.III,
            PrestigeStufe.III    => PrestigeStufe.IV,
            PrestigeStufe.IV     => PrestigeStufe.IV,
            _                    => PrestigeStufe.Normal
        };

        /// <summary>
        /// Berechnet Gegner-ATK/HP nach aktueller Prestige-Stufe.
        /// </summary>
        public (int atk, int hp) ScaleEnemyStats(int baseAtk, int baseHp, PrestigeStufe stufe)
        {
            var m = PrestigeStufeBalancing.GetEnemyStatMultiplier(stufe);
            return ((int)(baseAtk * m), (int)(baseHp * m));
        }

        /// <summary>
        /// Berechnet Gold-Drop nach aktueller Prestige-Stufe.
        /// </summary>
        public int ScaleGoldDrop(int baseGold, PrestigeStufe stufe)
            => (int)(baseGold * PrestigeStufeBalancing.GetGoldDropMultiplier(stufe));

        /// <summary>
        /// Berechnet das taegliche Idle-Income einer Welt nach Prestige-Stufe.
        /// </summary>
        public int CalculateDailyRevenue(int baseGoldPerDay, PrestigeStufe stufe)
            => (int)(baseGoldPerDay * PrestigeStufeBalancing.GetDailyRevenueMultiplier(stufe));

        /// <summary>
        /// Liefert, ob nach diesem Prestige-Upgrade die exklusive Prestige-IV-Karte freigeschaltet wird.
        /// </summary>
        public bool UnlocksExclusiveCard(PrestigeStufe newStufe)
            => newStufe == PrestigeStufe.IV;

        /// <summary>
        /// Anzahl Boss-Phasen abhaengig von Prestige-Stufe (Normal/I/II = 2 Phasen, III = 3, IV = 4).
        /// </summary>
        public int GetBossPhaseCount(PrestigeStufe stufe) => stufe switch
        {
            PrestigeStufe.III => 3,
            PrestigeStufe.IV  => 4,
            _                 => 2
        };
    }
}
