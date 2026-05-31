#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Core.Utility;

namespace ArcaneKingdom.Domain.Economy
{
    /// <summary>
    /// Inventar der Sternkarten eines Spielers (Designplan v4 Oeko Kap. 5.2).
    /// Plus aktueller Sternkarten-Tempel-Stand (eingetauschte vs. verbraucht).
    /// </summary>
    [Serializable]
    public sealed class SternkartenInventory
    {
        public int Bronze { get; set; }
        public int Silber { get; set; }
        public int Gold { get; set; }
        public int Platin { get; set; }

        /// <summary>Bereits zu Sternpunkten konvertiert + im Tempel ausgegeben.</summary>
        public int SternpunkteSpent { get; set; }

        /// <summary>Anzahl Mythischer Kern-Fragmente (3 Fragmente = 1 Mythischer Kern fuer 6*-Crafting).</summary>
        public int MythicCoreFragments { get; set; }

        // Hinweis: Fertig gecraftete Mythische Kerne werden NICHT hier, sondern auf der Slice-Ebene
        // (SternkartenSaveSlice.MythicCoresAvailable) gehalten — das ist die Single-Source-of-Truth,
        // die der Fusions-Konsument (FusionAppService.CountMaterial) liest. Siehe CraftMythicCore.

        /// <summary>Gesamt-Sternpunkte verfuegbar = sum(Karten × Werte) - SternpunkteSpent.</summary>
        public int AvailableSternpunkte =>
              Bronze * SternkartenWerte.GetSternpunkte(SternkartenStufe.Bronze)
            + Silber * SternkartenWerte.GetSternpunkte(SternkartenStufe.Silber)
            + Gold   * SternkartenWerte.GetSternpunkte(SternkartenStufe.Gold)
            + Platin * SternkartenWerte.GetSternpunkte(SternkartenStufe.Platin)
            - SternpunkteSpent;
    }

    /// <summary>
    /// Pure Domain-Logik fuer Sternkarten-Sammlung + Tempel-Eintausch.
    /// Wird vom Application-Service orchestriert (Persistenz, Karten-Drop, Notifications).
    /// </summary>
    public sealed class SternkartenService
    {
        /// <summary>
        /// Verbucht eine erhaltene Sternkarte (z.B. aus dem Login-Belohnungssystem).
        /// </summary>
        public void AddSternkarte(SternkartenInventory inv, SternkartenStufe stufe, int count = 1)
        {
            if (count <= 0) return;
            switch (stufe)
            {
                case SternkartenStufe.Bronze: inv.Bronze += count; break;
                case SternkartenStufe.Silber: inv.Silber += count; break;
                case SternkartenStufe.Gold:   inv.Gold   += count; break;
                case SternkartenStufe.Platin: inv.Platin += count; break;
            }
        }

        /// <summary>
        /// Prueft, ob ein Tempel-Eintausch moeglich ist.
        /// </summary>
        public Result CanExchange(SternkartenInventory inv, int costSternpunkte)
        {
            if (costSternpunkte <= 0) return Result.Failure("Ungueltige Eintausch-Kosten.");
            if (inv.AvailableSternpunkte < costSternpunkte)
                return Result.Failure($"Nicht genug Sternpunkte (benoetigt: {costSternpunkte}, vorhanden: {inv.AvailableSternpunkte}).");
            return Result.Success();
        }

        /// <summary>
        /// Bucht einen Tempel-Eintausch ab — Sternpunkte verbraucht.
        /// </summary>
        public Result Exchange(SternkartenInventory inv, int costSternpunkte)
        {
            var canDo = CanExchange(inv, costSternpunkte);
            if (!canDo.IsSuccess) return canDo;
            inv.SternpunkteSpent += costSternpunkte;
            return Result.Success();
        }

        /// <summary>
        /// Spezialfall: Mythic-Fragment-Sammlung. 3 Fragmente = 1 Mythischer Kern.
        /// </summary>
        public Result<int> ExchangeForMythicFragment(SternkartenInventory inv)
        {
            var canDo = CanExchange(inv, SternkartenWerte.CostMythicFragment);
            if (!canDo.IsSuccess) return Result<int>.Failure(canDo.ErrorMessage ?? "Eintausch fehlgeschlagen");
            inv.SternpunkteSpent += SternkartenWerte.CostMythicFragment;
            inv.MythicCoreFragments++;
            return Result<int>.Success(inv.MythicCoreFragments);
        }

        /// <summary>
        /// Prueft ob aus den gesammelten Fragmenten ein Mythischer Kern konstruiert werden kann.
        /// </summary>
        public bool CanCraftMythicCore(SternkartenInventory inv)
            => inv.MythicCoreFragments >= SternkartenWerte.MythicFragmentsPerCore;

        /// <summary>
        /// Verbraucht 3 Fragmente (aus dem Inventar) und schreibt 1 Mythischen Kern auf die
        /// Slice-Ebene (<see cref="ArcaneKingdom.Domain.Save.SternkartenSaveSlice.MythicCoresAvailable"/>)
        /// — exakt das Feld, das die 6*-Fusion liest. Liefert die neue Gesamtzahl verfuegbarer Kerne.
        ///
        /// K10-Fix: Frueher schrieb diese Methode in ein zweites, redundantes Inventory-Feld, das die
        /// Fusion nie las — gecraftete Kerne waren damit unsichtbar (6*-Crafting end-to-end tot).
        /// </summary>
        public Result<int> CraftMythicCore(ArcaneKingdom.Domain.Save.SternkartenSaveSlice slice)
        {
            var inv = slice.Inventory;
            if (!CanCraftMythicCore(inv))
                return Result<int>.Failure($"Brauche {SternkartenWerte.MythicFragmentsPerCore} Fragmente, habe {inv.MythicCoreFragments}.");
            inv.MythicCoreFragments -= SternkartenWerte.MythicFragmentsPerCore;
            slice.MythicCoresAvailable++;
            return Result<int>.Success(slice.MythicCoresAvailable);
        }
    }

    /// <summary>
    /// Login-Tracker (Designplan v4 Oeko Kap. 5.1).
    /// 30-Tage-Zyklus, ein Eintrag pro Tag.
    /// </summary>
    [Serializable]
    public sealed class LoginTracker
    {
        /// <summary>Bisher ueber den Zyklus eingesammelte Tage (1-30).</summary>
        public int DaysClaimedThisCycle { get; set; }

        /// <summary>Letztes Login-Belohnungs-Claim-Datum (UTC).</summary>
        public DateTime LastClaimedUtc { get; set; }

        /// <summary>
        /// Liefert true, wenn heute (UTC) noch nicht geclaimed wurde.
        /// </summary>
        public bool CanClaimToday(DateTime nowUtc) => LastClaimedUtc.Date < nowUtc.Date;

        /// <summary>
        /// Liefert den naechsten Tag im 30-Tage-Zyklus (1-30, dann zurueck zu 1).
        /// </summary>
        public int NextDayInCycle => (DaysClaimedThisCycle % 30) + 1;

        /// <summary>
        /// Markiert einen Tag als geclaimed.
        /// </summary>
        public void MarkClaimed(DateTime nowUtc)
        {
            DaysClaimedThisCycle++;
            if (DaysClaimedThisCycle > 30) DaysClaimedThisCycle = 1;   // Zyklus neu
            LastClaimedUtc = nowUtc;
        }
    }
}
