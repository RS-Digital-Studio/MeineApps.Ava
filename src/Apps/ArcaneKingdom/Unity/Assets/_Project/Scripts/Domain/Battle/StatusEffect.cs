#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Status-Effekt-Typen fuer Control-/DoT-Skills (Designplan v4 Kap. 3.4).
    /// </summary>
    public enum StatusEffectType
    {
        /// <summary>Schlaf — Karte ueberspringt die naechste Runde komplett (Skill v4 Traumweberin Aria).</summary>
        Sleep = 0,

        /// <summary>Stille — Karte kann keine Skills nutzen, normal angreifen schon (Prinzessin Seraphine).</summary>
        Silence = 1,

        /// <summary>Einfrierung — Karte ueberspringt naechste Runde + erleidet +30% Schaden (Wasser-Element).</summary>
        Frozen = 2,

        /// <summary>Betaeubung — Karte ueberspringt naechste Runde (Erde-Element + Donnerhirsch).</summary>
        Stunned = 3,

        /// <summary>Vergiftung — pro Runde X Schaden (Dunkel-Element + Pestdoktor).</summary>
        Poisoned = 4,

        /// <summary>Verbrennung — pro Runde X Feuerschaden (Feuer-Element).</summary>
        Burning = 5,

        /// <summary>Verlangsamung — +1 Rundenwarten (Elfen-Kontrolle).</summary>
        Slowed = 6,

        /// <summary>Verwurzelung — Karte kann nicht gewechselt/entfernt werden (Erde + Sylvan).</summary>
        Rooted = 7
    }

    /// <summary>
    /// Eine aktive Status-Wirkung auf einer Karte (Designplan v4 Kap. 3.4 + Skills v4).
    /// Magnitude bedeutet je nach Typ:
    ///   - Poisoned/Burning: Schaden pro Runde
    ///   - Slowed: +Rundenwarten
    ///   - Sleep/Silence/Frozen/Stunned/Rooted: irrelevant (Magnitude = 0)
    /// </summary>
    [Serializable]
    public sealed class StatusEffect
    {
        public StatusEffectType Type { get; }
        public int Magnitude { get; }
        public int RemainingTurns { get; set; }
        public string? SourceCardId { get; }   // Wer hat den Effekt verursacht? (fuer "kann nicht entfernt werden"-Logik)

        public StatusEffect(StatusEffectType type, int remainingTurns, int magnitude = 0, string? sourceCardId = null)
        {
            Type = type;
            Magnitude = magnitude;
            RemainingTurns = remainingTurns;
            SourceCardId = sourceCardId;
        }

        /// <summary>Liefert true wenn dieser Effekt die Karte daran hindert, ihre normale Aktion auszufuehren.</summary>
        public bool BlocksAction => Type switch
        {
            StatusEffectType.Sleep    => true,
            StatusEffectType.Frozen   => true,
            StatusEffectType.Stunned  => true,
            _                          => false
        };

        /// <summary>Liefert true wenn dieser Effekt nur Skills blockiert, normalen Angriff aber erlaubt.</summary>
        public bool BlocksSkills => Type == StatusEffectType.Silence;

        /// <summary>Liefert true wenn dieser Effekt pro Runde Schaden verursacht (DoT).</summary>
        public bool IsDamageOverTime => Type == StatusEffectType.Poisoned || Type == StatusEffectType.Burning;
    }

    /// <summary>
    /// Hilfsklasse fuer Status-Effekt-Verwaltung auf einer Karte.
    /// </summary>
    public static class StatusEffectHelpers
    {
        public static bool HasEffect(IReadOnlyList<StatusEffect> effects, StatusEffectType type)
        {
            for (var i = 0; i < effects.Count; i++) if (effects[i].Type == type) return true;
            return false;
        }

        public static bool IsBlocked(IReadOnlyList<StatusEffect> effects)
        {
            for (var i = 0; i < effects.Count; i++) if (effects[i].BlocksAction) return true;
            return false;
        }

        public static bool IsSilenced(IReadOnlyList<StatusEffect> effects)
        {
            for (var i = 0; i < effects.Count; i++) if (effects[i].BlocksSkills) return true;
            return false;
        }

        /// <summary>Wendet alle DoT-Effekte am Rundenanfang an, liefert Gesamt-Schaden.</summary>
        public static int TickDamageOverTime(IReadOnlyList<StatusEffect> effects)
        {
            var total = 0;
            for (var i = 0; i < effects.Count; i++)
                if (effects[i].IsDamageOverTime) total += effects[i].Magnitude;
            return total;
        }

        /// <summary>Reduziert die Dauer aller Effekte um 1 Runde und entfernt abgelaufene.</summary>
        public static void TickAndExpire(List<StatusEffect> effects)
        {
            for (var i = effects.Count - 1; i >= 0; i--)
            {
                effects[i].RemainingTurns--;
                if (effects[i].RemainingTurns <= 0) effects.RemoveAt(i);
            }
        }

        /// <summary>Fuegt einen Effekt hinzu (oder ersetzt einen bestehenden vom gleichen Typ wenn die neue Dauer laenger ist).</summary>
        public static void ApplyOrRefresh(List<StatusEffect> effects, StatusEffect newEffect)
        {
            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i].Type == newEffect.Type)
                {
                    if (newEffect.RemainingTurns > effects[i].RemainingTurns)
                        effects[i] = newEffect;
                    return;
                }
            }
            effects.Add(newEffect);
        }
    }
}
