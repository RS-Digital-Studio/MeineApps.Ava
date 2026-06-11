using System;

namespace HandwerkerImperium.Domain.Idle
{
    /// <summary>
    /// Deterministischer Tick + Spieler-/Idle-Aktionen des Greybox-Loops (P0). Unity-frei und
    /// damit headless NUnit-testbar — die MonoBehaviours im Game-Layer rendern/bedienen nur diesen
    /// Zustand. Bildet die Idle-/Automatisierungs-Seite ab (Produktion, Worker-Durchsatz, Upgrades,
    /// Hire, Plot-Unlock, Offline); die aktive Spieler-Lauferei ruft <see cref="PlayerDeposit"/>.
    /// </summary>
    public static class GreyboxSimulation
    {
        /// <summary>Ein voller Simulations-Tick: Produktion + Worker-Automatisierung. Liefert das durch Worker verdiente Geld.</summary>
        public static decimal Tick(GreyboxSimState state, IdleBalancing balancing, double dtSeconds)
        {
            if (dtSeconds <= 0) return 0m;
            TickProduction(state, balancing, dtSeconds);
            return TickWorkers(state, balancing, dtSeconds);
        }

        /// <summary>Schreibt die Produktion aller freigeschalteten Stationen fort (Stock bis StackCap).</summary>
        public static void TickProduction(GreyboxSimState state, IdleBalancing balancing, double dtSeconds)
        {
            int n = Math.Min(state.Stations.Count, balancing.Stations.Count);
            for (int i = 0; i < n; i++)
            {
                var st = state.Stations[i];
                if (!st.Unlocked) continue;
                var sb = balancing.Stations[i];

                double interval = IdleEconomyFormulas.EffectiveProduceInterval(sb.ProduceInterval, state.StationSpeedLevel, balancing.UpgradeStep);
                if (interval <= 0) continue;

                // Perfekt-Aktions-Buff (GDD §6.7): beschleunigt die Produktion temporaer; die
                // Restlaufzeit laeuft auch bei vollem Stapel ab (kein "eingefrorener" Buff).
                double speedFactor = 1.0;
                if (st.BoostRemainingSeconds > 0)
                {
                    if (st.BoostMultiplier > 1.0) speedFactor = st.BoostMultiplier;
                    st.BoostRemainingSeconds -= dtSeconds;
                    if (st.BoostRemainingSeconds <= 0)
                    {
                        st.BoostRemainingSeconds = 0;
                        st.BoostMultiplier = 1.0;
                    }
                }

                st.ProduceProgressSeconds += dtSeconds * speedFactor;
                while (st.ProduceProgressSeconds >= interval && st.Stock < sb.StackCap)
                {
                    st.Stock++;
                    st.ProduceProgressSeconds -= interval;
                }
                // Voll: Akkumulator deckeln, damit nach Abholung sofort nachproduziert wird (kein Endlos-Stau).
                if (st.Stock >= sb.StackCap && st.ProduceProgressSeconds > interval)
                    st.ProduceProgressSeconds = interval;
            }
        }

        /// <summary>
        /// Setzt den temporaeren Tempo-Buff der Perfekt-Aktion (GDD §6.7) auf eine Station.
        /// Multiplikator &lt;= 1 oder Dauer &lt;= 0 (Miss) sind no-ops; ein staerkerer/neuer Buff
        /// ersetzt den laufenden (kein Stapeln).
        /// </summary>
        public static void ApplyBoost(GreyboxSimState state, int stationIndex, double multiplier, double durationSeconds)
        {
            if (stationIndex < 0 || stationIndex >= state.Stations.Count) return;
            if (multiplier <= 1.0 || durationSeconds <= 0) return;
            var st = state.Stations[stationIndex];
            if (!st.Unlocked) return;
            st.BoostMultiplier = multiplier;
            st.BoostRemainingSeconds = durationSeconds;
        }

        /// <summary>Automatisierte Stationen: Worker bewegt Stock->Tresen (Waren/s), wandelt in Geld. Liefert Geld-Delta.</summary>
        public static decimal TickWorkers(GreyboxSimState state, IdleBalancing balancing, double dtSeconds)
        {
            decimal earned = 0m;
            int n = Math.Min(state.Stations.Count, balancing.Stations.Count);
            for (int i = 0; i < n; i++)
            {
                var st = state.Stations[i];
                if (!st.Unlocked || !st.HasWorker) continue;
                var sb = balancing.Stations[i];

                st.WorkerProgress += Math.Max(0, balancing.WorkerCarrySpeed) * dtSeconds;
                int whole = (int)st.WorkerProgress;
                int take = Math.Min(whole, st.Stock);
                if (take > 0)
                {
                    st.Stock -= take;
                    st.WorkerProgress -= take;
                    earned += take * sb.SellValue;
                }
                // Kein Stock -> Worker laeuft leer; Akkumulator deckeln (kein „aufgesparter" Burst).
                if (st.Stock == 0 && st.WorkerProgress > 0.9999)
                    st.WorkerProgress = 0.9999;
            }
            state.Money += earned;
            return earned;
        }

        /// <summary>
        /// Aktive Spieler-Abgabe: traegt bis zu <paramref name="requestedCount"/> Waren der Station zum Tresen
        /// und wandelt sie in Geld (Cash-Spawn im Game-Layer). Liefert das verdiente Geld (0 wenn nichts da).
        /// Die Trag-Kapazitaet begrenzt der Aufrufer via <see cref="EffectiveCarryCapacity"/>.
        /// </summary>
        public static decimal PlayerDeposit(GreyboxSimState state, IdleBalancing balancing, int stationIndex, int requestedCount)
        {
            if (stationIndex < 0 || stationIndex >= state.Stations.Count) return 0m;
            var st = state.Stations[stationIndex];
            if (!st.Unlocked || requestedCount <= 0) return 0m;
            var sb = balancing.Stations[stationIndex];

            int count = Math.Min(requestedCount, st.Stock);
            if (count <= 0) return 0m;
            st.Stock -= count;
            decimal earned = count * sb.SellValue;
            state.Money += earned;
            return earned;
        }

        /// <summary>
        /// Physischer Loop, Schritt 1: Avatar nimmt bis zu <paramref name="requestedCount"/> Waren von der
        /// Station auf (Stock sinkt sichtbar). KEIN Geld — das gibt es erst bei <see cref="SellCarried"/> am Tresen.
        /// Liefert die tatsaechlich aufgenommene Menge.
        /// </summary>
        public static int PlayerPickup(GreyboxSimState state, IdleBalancing balancing, int stationIndex, int requestedCount)
        {
            if (stationIndex < 0 || stationIndex >= state.Stations.Count) return 0;
            var st = state.Stations[stationIndex];
            if (!st.Unlocked || requestedCount <= 0) return 0;
            int take = Math.Min(requestedCount, st.Stock);
            if (take <= 0) return 0;
            st.Stock -= take;
            return take;
        }

        /// <summary>
        /// Physischer Loop, Schritt 2: Avatar gibt <paramref name="count"/> getragene Waren der Herkunfts-Station
        /// am Tresen ab -> Geld (count × Verkaufswert). Cash-Wuerfel im Game-Layer sind reine Optik; das Geld ist hier autoritativ.
        /// </summary>
        public static decimal SellCarried(GreyboxSimState state, IdleBalancing balancing, int stationIndex, int count)
        {
            if (stationIndex < 0 || stationIndex >= balancing.Stations.Count || count <= 0) return 0m;
            decimal earned = count * balancing.Stations[stationIndex].SellValue;
            state.Money += earned;
            return earned;
        }

        // ── Upgrades / Hire / Unlock ───────────────────────────────────────

        /// <summary>Aktuelle Kosten der naechsten Stufe einer Upgrade-Achse.</summary>
        public static decimal UpgradeCostFor(GreyboxSimState state, IdleBalancing balancing, UpgradeTrack track) =>
            IdleEconomyFormulas.UpgradeCost(state.GetLevel(track), balancing.UpgradeCostBase, balancing.UpgradeCostGrowth);

        /// <summary>Kauft eine Upgrade-Stufe, wenn genug Geld da ist. Liefert true bei Erfolg.</summary>
        public static bool BuyUpgrade(GreyboxSimState state, IdleBalancing balancing, UpgradeTrack track)
        {
            decimal cost = UpgradeCostFor(state, balancing, track);
            if (state.Money < cost) return false;
            state.Money -= cost;
            state.SetLevel(track, state.GetLevel(track) + 1);
            return true;
        }

        /// <summary>Stellt einen Worker an einer Station an (einmalig je Station). Liefert true bei Erfolg.</summary>
        public static bool HireWorker(GreyboxSimState state, IdleBalancing balancing, int stationIndex)
        {
            if (stationIndex < 0 || stationIndex >= state.Stations.Count) return false;
            var st = state.Stations[stationIndex];
            if (!st.Unlocked || st.HasWorker) return false;
            if (state.Money < balancing.WorkerHireCost) return false;
            state.Money -= balancing.WorkerHireCost;
            st.HasWorker = true;
            return true;
        }

        /// <summary>Plot-Freischalt-Kosten einer Station (per-Station-Progression; 0 im Def = globaler Fallback).</summary>
        public static decimal UnlockCostFor(IdleBalancing balancing, int stationIndex)
        {
            if (stationIndex >= 0 && stationIndex < balancing.Stations.Count && balancing.Stations[stationIndex].UnlockCost > 0m)
                return balancing.Stations[stationIndex].UnlockCost;
            return balancing.PlotUnlockCost;
        }

        /// <summary>Schaltet eine gesperrte Station (Plot) frei. Liefert true bei Erfolg.</summary>
        public static bool UnlockPlot(GreyboxSimState state, IdleBalancing balancing, int stationIndex)
        {
            if (stationIndex < 0 || stationIndex >= state.Stations.Count) return false;
            var st = state.Stations[stationIndex];
            if (st.Unlocked) return false;
            decimal cost = UnlockCostFor(balancing, stationIndex);
            if (state.Money < cost) return false;
            state.Money -= cost;
            st.Unlocked = true;
            return true;
        }

        // ── Abgeleitete Spielwerte ─────────────────────────────────────────

        /// <summary>Effektive Trag-Kapazitaet des Avatars nach Upgrades.</summary>
        public static int EffectiveCarryCapacity(GreyboxSimState state, IdleBalancing balancing) =>
            IdleEconomyFormulas.EffectiveCarryCapacity(balancing.CarryCapacity, state.CarryCapacityLevel, balancing.UpgradeStep);

        /// <summary>Effektiver Auto-Pickup-Sammelradius des Avatars nach Upgrades.</summary>
        public static double EffectiveCollectRadius(GreyboxSimState state, IdleBalancing balancing) =>
            IdleEconomyFormulas.EffectiveCollectRadius(balancing.CollectRadius, state.CollectRadiusLevel, balancing.UpgradeStep);

        /// <summary>Summe des automatisierten Einkommens/Sekunde ueber alle Worker-Stationen.</summary>
        public static decimal TotalAutomatedIncomePerSecond(GreyboxSimState state, IdleBalancing balancing)
        {
            decimal sum = 0m;
            int n = Math.Min(state.Stations.Count, balancing.Stations.Count);
            for (int i = 0; i < n; i++)
            {
                var st = state.Stations[i];
                if (!st.Unlocked || !st.HasWorker) continue;
                var sb = balancing.Stations[i];
                double interval = IdleEconomyFormulas.EffectiveProduceInterval(sb.ProduceInterval, state.StationSpeedLevel, balancing.UpgradeStep);
                double throughput = IdleEconomyFormulas.WorkerThroughputPerSecond(interval, balancing.WorkerCarrySpeed);
                sum += IdleEconomyFormulas.AutomatedIncomePerSecond(sb.SellValue, throughput);
            }
            return sum;
        }

        /// <summary>Anzahl freigeschalteter Stationen mit angestelltem Worker (Automatisierungsgrad).</summary>
        public static int AutomatedWorkerCount(GreyboxSimState state)
        {
            int count = 0;
            for (int i = 0; i < state.Stations.Count; i++)
                if (state.Stations[i].Unlocked && state.Stations[i].HasWorker) count++;
            return count;
        }

        /// <summary>
        /// Berechnet den Offline-Verdienst fuer eine Abwesenheitsdauer (gestaffelt, gedeckelt) — ohne ihn
        /// gutzuschreiben (der „Waehrend-du-weg-warst"-Dialog entscheidet). Rate/Sekunde = abgeleitete
        /// Stations-Oekonomie + optionaler flacher <see cref="IdleBalancing.OfflineRatePerWorker"/> je Worker.
        /// </summary>
        public static decimal ComputeOfflineEarnings(GreyboxSimState state, IdleBalancing balancing, double elapsedSeconds)
        {
            decimal ratePerSecond = TotalAutomatedIncomePerSecond(state, balancing)
                                  + AutomatedWorkerCount(state) * balancing.OfflineRatePerWorker;
            return IdleEconomyFormulas.OfflineEarnings(ratePerSecond, elapsedSeconds, balancing.OfflineCapSeconds);
        }

        /// <summary>Schreibt einen berechneten Offline-Betrag gut und aktualisiert den Zeitstempel.</summary>
        public static void ApplyOfflineEarnings(GreyboxSimState state, decimal amount, long nowUtcTicks)
        {
            if (amount > 0) state.Money += amount;
            state.LastSeenUtcTicks = nowUtcTicks;
        }
    }
}
