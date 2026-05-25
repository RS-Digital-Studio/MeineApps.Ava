#nullable enable
using System;
using ArcaneKingdom.Domain.Economy;

namespace ArcaneKingdom.Domain.Player
{
    /// <summary>
    /// Alle Spieler-Währungs-Saldi. Energie hat besondere Logik:
    /// Cap = 60, aber Bonus-Energie kann darüber gehen (z.B. 80/60 in grün).
    /// </summary>
    [Serializable]
    public sealed class PlayerCurrencies
    {
        public const int EnergyDefaultCap = 60;

        public long Gold { get; private set; }
        public long Diamond { get; private set; }
        public int Energy { get; private set; }              // Normal-Energie (max EnergyDefaultCap)
        public int EnergyBonus { get; private set; }         // Bonus-Energie (kann über Cap)
        public long GuildPoints { get; private set; }
        public long UniversalScraps { get; private set; }
        public long MeritPoints { get; private set; }
        public int ArenaTickets { get; private set; }

        // Upgrade-Steine — vier verschiedene Pools
        public long CommonScraps { get; private set; }
        public long RareScraps { get; private set; }
        public long EpicScraps { get; private set; }
        public long LegendaryScraps { get; private set; }

        public int TotalEnergy => Energy + EnergyBonus;

        /// <summary>True wenn Bonus-Energie ueber dem Default-Cap liegt — fuer gruene UI-Anzeige (z.B. 80/60).</summary>
        public bool HasEnergyOverflow => EnergyBonus > 0;

        /// <summary>
        /// Adaptive Energie-Vergabe: fuellt zuerst Normal-Energie bis zum Cap auf,
        /// der Rest geht in EnergyBonus. Bequemer fuer Quest-/Event-Belohnungen.
        /// </summary>
        public void AddEnergyAdaptive(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var roomInNormal = Math.Max(0, EnergyDefaultCap - Energy);
            var toNormal = Math.Min(roomInNormal, amount);
            Energy += toNormal;
            var rest = amount - toNormal;
            if (rest > 0) EnergyBonus += rest;
        }

        // --- Mutationen (intern, vom Service aufgerufen) ---

        public void AddGold(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); Gold += amount; }
        public bool SpendGold(long amount) { if (amount < 0 || Gold < amount) return false; Gold -= amount; return true; }

        public void AddDiamond(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); Diamond += amount; }
        public bool SpendDiamond(long amount) { if (amount < 0 || Diamond < amount) return false; Diamond -= amount; return true; }

        /// <summary>Fügt Normal-Energie hinzu, capped bei EnergyDefaultCap.</summary>
        public void AddEnergy(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Energy = Math.Min(Energy + amount, EnergyDefaultCap);
        }

        /// <summary>Fügt Bonus-Energie hinzu (kein Cap).</summary>
        public void AddEnergyBonus(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            EnergyBonus += amount;
        }

        /// <summary>
        /// Verbraucht Energie. Zuerst Bonus, dann Normal (DESIGN.md 3.2).
        /// Gibt false zurück wenn nicht genug Energie vorhanden.
        /// </summary>
        public bool SpendEnergy(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (TotalEnergy < amount) return false;
            var fromBonus = Math.Min(EnergyBonus, amount);
            EnergyBonus -= fromBonus;
            Energy -= (amount - fromBonus);
            return true;
        }

        public void AddGuildPoints(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); GuildPoints += amount; }
        public void AddUniversalScraps(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); UniversalScraps += amount; }
        public bool SpendUniversalScraps(long amount) { if (amount < 0 || UniversalScraps < amount) return false; UniversalScraps -= amount; return true; }
        public void AddMeritPoints(long amount) { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); MeritPoints = Math.Min(MeritPoints + amount, 199_999); }

        public void AddScraps(ScrapType type, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            switch (type)
            {
                case ScrapType.Common: CommonScraps += amount; break;
                case ScrapType.Rare: RareScraps += amount; break;
                case ScrapType.Epic: EpicScraps += amount; break;
                case ScrapType.Legendary: LegendaryScraps += amount; break;
            }
        }

        public bool SpendScraps(ScrapType type, long amount)
        {
            if (amount < 0) return false;
            switch (type)
            {
                case ScrapType.Common:    if (CommonScraps    < amount) return false; CommonScraps    -= amount; return true;
                case ScrapType.Rare:      if (RareScraps      < amount) return false; RareScraps      -= amount; return true;
                case ScrapType.Epic:      if (EpicScraps      < amount) return false; EpicScraps      -= amount; return true;
                case ScrapType.Legendary: if (LegendaryScraps < amount) return false; LegendaryScraps -= amount; return true;
                default: return false;
            }
        }
    }
}
