#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Runes
{
    /// <summary>
    /// Aggregierte, kampffertige Deck-Runen-Boni (Spielplan v5 Kap. 7.2). Vorberechnet im
    /// <see cref="RuneLoadoutBuilder"/>, abgelegt im BattleState. Rein additiv -> reihenfolge-
    /// unabhaengig -> bit-identisch reproduzierbar (Anti-Cheat/Replay, kein RNG).
    ///
    /// Bewusst NICHT [Serializable]: der BattleStateSerializer ist DTO-/whitelist-basiert und
    /// nimmt das Loadout nicht in den Replay-Snapshot auf — die Boni werden in HeroHp/Field-Werte
    /// eingebrannt und sind dadurch rekonstruierbar.
    /// </summary>
    public sealed class RuneLoadout
    {
        public float AttackPercent { get; private set; }       // Angriffs-Runen: +X% ATK aller Deck-Karten
        public float HealthPercent { get; private set; }       // Verteidigungs-Runen: +X% HP aller Deck-Karten
        public int SpecialTurnReduction { get; private set; }  // Geschwindigkeits-Runen: -X Rundenwarten
        public int HeroHpFlat { get; private set; }            // Hero-Runen: +X Helden-HP
        public int BonusStartMana { get; private set; }        // Mana-Runen: +X Start-Mana (einmalig Runde 1)

        private readonly Dictionary<Element, float> _elementDamage = new();
        public IReadOnlyDictionary<Element, float> ElementDamagePercent => _elementDamage;

        // Kombo-Bedingungen: Magnitude wird beim Add gesammelt, die *Active-Flags setzt der
        // RuneLoadoutBuilder deterministisch aus der Deck-Zusammensetzung beim Setup.
        public float ComboDaemonAtkPercent { get; set; }   // gilt fuer ALLE Allies, wenn Daemonen im Deck >= 3
        public bool ComboDaemonActive { get; set; }
        public float ComboDracheAtkPercent { get; set; }   // gilt nur fuer Drachen-Karten, wenn Drachen im Deck >= 2
        public bool ComboDracheActive { get; set; }

        public bool IsEmpty =>
            AttackPercent == 0 && HealthPercent == 0 && SpecialTurnReduction == 0 &&
            HeroHpFlat == 0 && BonusStartMana == 0 && _elementDamage.Count == 0 &&
            !ComboDaemonActive && !ComboDracheActive &&
            ComboDaemonAtkPercent == 0 && ComboDracheAtkPercent == 0;

        public void Add(RuneDefinition def, int runeLevel)
        {
            var mag = def.CalculateMagnitudeAtLevel(runeLevel);
            switch (def.Type)
            {
                case RuneType.Angriff:         AttackPercent += mag; break;
                case RuneType.Verteidigung:    HealthPercent += mag; break;
                case RuneType.Geschwindigkeit: SpecialTurnReduction += (int)System.Math.Round(mag); break;
                case RuneType.Hero:            HeroHpFlat += (int)System.Math.Round(mag); break;
                case RuneType.Mana:            BonusStartMana += (int)System.Math.Round(mag); break;
                case RuneType.Element:
                    _elementDamage[def.ElementTarget] =
                        (_elementDamage.TryGetValue(def.ElementTarget, out var e) ? e : 0f) + mag;
                    break;
                case RuneType.Kombo:
                    if (def.Id == "kombo_daemon") ComboDaemonAtkPercent += mag;
                    else if (def.Id == "kombo_drache") ComboDracheAtkPercent += mag;
                    break;
            }
        }

        public float ElementBonusFor(Element element) =>
            _elementDamage.TryGetValue(element, out var v) ? v : 0f;
    }
}
