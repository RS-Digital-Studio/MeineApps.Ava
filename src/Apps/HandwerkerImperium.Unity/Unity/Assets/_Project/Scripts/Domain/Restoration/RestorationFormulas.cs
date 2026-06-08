#nullable enable
using System;
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Restoration
{
    /// <summary>
    /// Stadt-Wiederaufbau (P1 §6.4 / GDD §6.4): ein Wahrzeichen je Distrikt baut sich über mehrere Bauphasen
    /// sichtbar auf, gespeist aus über Zeit investierten Ressourcen/Geld. Jede abgeschlossene Phase liefert
    /// „Vorher/Nachher"-Feedback + speist das Stern-Rating (Sanierungs-Anteil). Reine, Unity-freie Mathematik.
    /// </summary>
    public sealed class LandmarkState
    {
        public string Id = "";
        public int PhasesComplete;
        public int TotalPhases;
        /// <summary>Bereits investierte, noch nicht in eine Phase umgesetzte Ressourcen.</summary>
        public decimal InvestedResources;

        public LandmarkState() { }

        public LandmarkState(string id, int totalPhases)
        {
            Id = id;
            TotalPhases = totalPhases;
        }
    }

    public static class RestorationFormulas
    {
        /// <summary>
        /// Kosten der Bauphase mit Index <paramref name="phaseIndex"/> (0-basiert), geometrisch steigend:
        /// <c>base × growth^phaseIndex</c>.
        /// </summary>
        public static decimal PhaseCost(int phaseIndex, decimal baseCost, double growth)
        {
            if (phaseIndex < 0) phaseIndex = 0;
            double raw = (double)baseCost * Math.Pow(growth, phaseIndex);
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw > (double)decimal.MaxValue) return decimal.MaxValue;
            decimal cost = Math.Round((decimal)raw); // ganze Zahlen wie bei den Upgrade-Kosten
            return cost < 1m ? 1m : cost;
        }

        /// <summary>
        /// Investiert <paramref name="amount"/> in ein Wahrzeichen und schließt so viele Bauphasen ab,
        /// wie das angesparte Budget trägt. Liefert die Anzahl in diesem Aufruf abgeschlossener Phasen.
        /// </summary>
        public static int Invest(LandmarkState landmark, decimal amount, decimal baseCost, double growth)
        {
            if (landmark == null || amount <= 0m || landmark.PhasesComplete >= landmark.TotalPhases) return 0;
            landmark.InvestedResources += amount;

            int completed = 0;
            while (landmark.PhasesComplete < landmark.TotalPhases)
            {
                decimal cost = PhaseCost(landmark.PhasesComplete, baseCost, growth);
                if (landmark.InvestedResources < cost) break;
                landmark.InvestedResources -= cost;
                landmark.PhasesComplete++;
                completed++;
            }
            return completed;
        }

        /// <summary>Verbleibende Ressourcen bis zum Abschluss der nächsten Bauphase.</summary>
        public static decimal RemainingForNextPhase(LandmarkState landmark, decimal baseCost, double growth)
        {
            if (landmark == null || landmark.PhasesComplete >= landmark.TotalPhases) return 0m;
            decimal cost = PhaseCost(landmark.PhasesComplete, baseCost, growth);
            decimal remaining = cost - landmark.InvestedResources;
            return remaining > 0m ? remaining : 0m;
        }

        /// <summary>True, wenn alle Bauphasen abgeschlossen sind (Wahrzeichen saniert).</summary>
        public static bool IsComplete(LandmarkState landmark) =>
            landmark != null && landmark.TotalPhases > 0 && landmark.PhasesComplete >= landmark.TotalPhases;

        /// <summary>Summe aller abgeschlossenen Bauphasen über mehrere Wahrzeichen (speist das Stern-Rating).</summary>
        public static int TotalPhasesComplete(IReadOnlyList<LandmarkState>? landmarks)
        {
            if (landmarks == null) return 0;
            int sum = 0;
            for (int i = 0; i < landmarks.Count; i++)
                if (landmarks[i] != null) sum += landmarks[i].PhasesComplete;
            return sum;
        }

        /// <summary>Anzahl vollständig sanierter Wahrzeichen (Distrikt-Gates).</summary>
        public static int CompletedLandmarks(IReadOnlyList<LandmarkState>? landmarks)
        {
            if (landmarks == null) return 0;
            int count = 0;
            for (int i = 0; i < landmarks.Count; i++)
                if (IsComplete(landmarks[i])) count++;
            return count;
        }
    }
}
