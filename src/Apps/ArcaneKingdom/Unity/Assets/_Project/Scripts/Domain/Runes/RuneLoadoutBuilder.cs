#nullable enable
using System;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;

namespace ArcaneKingdom.Domain.Runes
{
    /// <summary>
    /// Statische, geteilte Aggregations-Logik fuer Deck-Runen (Spielplan v5 Kap. 7.2).
    /// Genutzt von BattleBootstrap (Kampf) und RuneScreen (Anzeige). Reine Domain-Logik —
    /// nimmt Lookup-Funktionen statt Game-Layer-Services (keine Aufwaerts-Abhaengigkeit).
    /// </summary>
    public static class RuneLoadoutBuilder
    {
        /// <summary>
        /// Baut das kampffertige RuneLoadout eines Decks. Nur Slots &lt;= freigeschalteter
        /// Spieler-Level zaehlen (Anti-Exploit). Kombo-Bedingungen werden aus der
        /// Deck-Zusammensetzung deterministisch vorberechnet.
        /// </summary>
        /// <returns>null, wenn keine wirksame Rune gesetzt ist.</returns>
        public static RuneLoadout? Build(
            Deck deck,
            PlayerSave save,
            Func<string, RuneDefinition?> findRune,
            Func<string, CardDefinition?> findCardDef)
        {
            if (deck == null || save == null) return null;

            var unlocked = RuneSlotUnlock.UnlockedSlotCount(save.Profile.Level); // 0..4
            var loadout = new RuneLoadout();
            for (var slot = 0; slot < deck.RuneInstanceIds.Count && slot < unlocked; slot++)
            {
                var runeInstId = deck.RuneInstanceIds[slot];
                if (string.IsNullOrEmpty(runeInstId)) continue;
                if (!save.RuneInventory.TryGetValue(runeInstId!, out var inst)) continue;
                var def = findRune(inst.RuneDefinitionId);
                if (def == null) continue;
                loadout.Add(def, inst.Level);
            }

            // Kombo-Bedingungen: Deck-Zusammensetzung zaehlen (3+ Daemonen / 2+ Drachen).
            if (loadout.ComboDaemonAtkPercent > 0 || loadout.ComboDracheAtkPercent > 0)
            {
                var daemonCount = 0;
                var dracheCount = 0;
                foreach (var cardInstId in deck.CardInstanceIds)
                {
                    if (!save.CardInventory.TryGetValue(cardInstId, out var cInst)) continue;
                    var cDef = findCardDef(cInst.CardDefinitionId);
                    if (cDef == null) continue;
                    if (cDef.Race == Race.Daemonen) daemonCount++;
                    if (IsDrache(cDef)) dracheCount++;
                }
                loadout.ComboDaemonActive = loadout.ComboDaemonAtkPercent > 0 && daemonCount >= 3;
                loadout.ComboDracheActive = loadout.ComboDracheAtkPercent > 0 && dracheCount >= 2;
            }

            return loadout.IsEmpty ? null : loadout;
        }

        /// <summary>
        /// "Drache"-Erkennung. CardDefinition hat KEIN Drachen-Tag (nur Race/Id), daher
        /// ueber ID-Konvention: Card-IDs enthalten drache/drago/dragon/wyrm/urdrachen.
        /// MVP-Heuristik (deterministisch, string-basiert) — bei spaeterem Tag-System ersetzen.
        /// TODO: gegen kuratierte Drachen-Allowlist (cards.json) absichern, bevor kombo_drache
        /// als balanciert gilt.
        /// </summary>
        public static bool IsDrache(CardDefinition def)
        {
            var id = def.Id.ToLowerInvariant();
            return id.Contains("drache") || id.Contains("drago")
                || id.Contains("dragon") || id.Contains("wyrm")
                || id.Contains("urdrachen");
        }
    }
}
