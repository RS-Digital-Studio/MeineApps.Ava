# ArcaneKingdom — Referenz-Abgleich (Soll v4/v5 vs. Ist-Code)

> **Stand:** 2026-05-31 · **Methodik:** 11 parallele Abgleich-Agenten haben die autoritative
> Referenz (Arcane_Legends_*_v4-Detaildocs + Spielplan_v5_FINAL + Implementierungsplan_KOMPLETT,
> Originale unter `F:\AI\…\Spiele Ideen Ordner\Ideen\`) gegen Code/JSON unter
> `Unity/Assets/_Project/` abgeglichen. Jede Zeile ist mit Quelle:Zeile + Datei:Zeile belegt.
>
> **Autoritative Linie (von Robert bestätigt):** v4-Detaildocs (Mechanik) + v5-UI (Screens).
> Alles wird an die Referenz angeglichen, auch frühere bewusste Abweichungen.

---

## 0. Gesamtbild

**Korrekt (kein Handlungsbedarf):**
- Die **131 Standard-Karten** stimmen 1:1 (Rasse/Element/Rarity/COST/ATK/HP/RW je Karte exakt, Verteilung 10/8/6/4/2/1 pro Rasse + Götter 4/2/1).
- **5 Helden-Passivs** (Name/Effekt/Magnitude), **6-Elemente-Doppel-Dreieck** + Stark-Multiplikatoren, **Runen-Slot-Level** (1/20/30/40), **Karten-Level-Tabelle** (Bonus-%), **Deck-Regeln** (10 Karten, 1/deck, Begrenzt:2, max 2 Leg/3 Epic/1 Myth, COST 200), **alle Ökosystem-Karten-Stats** (9 Event/6 Premium/2 Sternkarten/10 Prestige), Premium-Preise, Sternkarten-Werte, Tempel-Kosten, Prestige-Multiplikatoren, baseGoldPerDay-Anker, 10 Welten + Reihenfolge + Primär-Elemente W1–7, Boss-Node-Positionen 5/10, Säulen-Zuordnung (6 Säulen).

**Die großen Lücken:** Das Spiel folgt der Referenz **konzeptionell**, aber viele **Mechaniken sind nur Skill-1-tief umgesetzt** und mehrere **Soll-Systeme fehlen** ganz.

---

## 1. KRITISCH — große fehlende/falsche Systeme (Feature-Arbeit)

| # | Befund | Quelle | Aufwand |
|---|--------|--------|---------|
| K1 | **Nur Skill 1 wirkt im Kampf.** Skill 2 (LV5), Skill 3 (LV10), Letzter Wille (LV15) werden in `BattleEngine.TriggerSpecial` nie ausgewertet (nur `def.BaseAbility`). Daten (abilities.json `_2/_3/_lw`, cards.json) sind vorhanden, die Level-Gates `HasSecond/ThirdAbilityUnlocked` sind tote Flags. | Skills_v4; BattleEngine.cs:196-199,439 | Groß |
| K2 | **Skill-Wirkung generisch statt skill-spezifisch.** `AbilityDefinition` hat nur category/magnitude/duration/targets — kann Krit-Chance, Revive%, HP-Swap, Summon, Mehrfach-Effekte, absolute-vs-%-Werte nicht abbilden. Viele Skills falsch kategorisiert (Voss S1, Gorm S2/S3, Kira S2, Libra S1/S3, Aetherius S1) oder durch `magnitude:0` wirkungslos. Buffs/Debuffs ignorieren `durationTurns` (permanent statt temporär). | Skills_v4; AbilityDefinition.cs:34-40, BattleEngine.cs:443-509 | Groß |
| K3 | **Schild-System fehlt komplett** — blockiert Erd-Mechaniken (Steinpanzer, Erdwall, Felssturz), Feuer-„Schild-Pierce 15%" und zahlreiche Skills. Kein Shield/Armor-Feld auf `CardFieldSlot`. | Designplan_v4 Kap.3.4; StatusEffect.cs, BattleState.cs:129-151 | Groß |
| K4 | **Element-Passiveffekte fehlen** — Stark-Treffer löst nur +10% aus, keinen Element-Status (Verbrennung/Einfrierung/etc.). Natur-Selbstheilung, Wasser-Einfrier-Chance, Erde-„Erhöhte Verteidigung", Dunkel-Gift-Verstärkung, Licht-Cleanse alle nicht implementiert. | Designplan_v4 Kap.3.3; BattleEngine.cs:178-193 | Groß |
| K5 | **Auto-Battle-UI fehlt.** `AutoBattleProgression` (10/20/30/50, Speed 1-4, Boss-Erstversuch-Sperre) ist korrekt, aber toter Code — kein Auto-Button, kein Überspringen-Button, kein Speed-System, keine LV10-Freischalt-Feier. | v5 9.1, Designplan_v4 Kap.6; BattleScreen.cs | Groß |
| K6 | **Event-Runtime-System fehlt.** `events.json` ohne Konsument: keine Punkte-Vergabe, kein Aktiv-Fenster, kein Notfall-Kauf (letzter Tag, +50% Archiv). | Oekosystem_v4; events.json | Mittel-Groß |
| K7 | **Saison-Pass-EXP-Kurve linear statt non-linear.** Soll: 2500/7000/12000/18000/25000/35000 (kumulativ je Meilenstein). Ist: `xpPerTier:1167` (linear). | Oekosystem_v4 Kap.4.1; saison_pass.json:8 | Mittel |
| K8 | **Karten-Drops pro Stern / Boss-Belohnungen fehlen.** Node-Belohnungen nur Gold/EXP. Soll: 4★-Boss-Node-5 = Epic-Karte, Boss-Node-10 = Legendär-Karte, gestufte Karten-Drops je Stern. | v5 8.2/9.4; NodeDefinition.cs, worlds.json | Mittel |
| K9 | **Story: Twist-Reveals werden zu früh gezeigt** (`MemoryFragmentModal` ohne `TwistRevealed`-Gate) → narrative Mehrdeutigkeit + W8-Twist vorweggenommen. **Beide W10-Enden nicht spielbar** (`EndingChoice` nie gesetzt, kein Choice-/Ending-UI). | Story_v4 Kap.9/10; MemoryFragmentModal.cs:74, StorySaveSlice.cs:45 | Mittel |
| K10 | **5★→6★-Kategorie-Fusion tot** — `AllowsDifferentCards`-Flag nie ausgewertet, `PreviewCategoryFusion` blockt Mythisch hart. 6★ nur über feste Rezepte. | Designplan_v4 Kap.5.1; FusionService.cs:84-101 | Mittel |
| K11 | **Premium-Shop ohne Rotations-/Kauf-Logik**, **Sternkarten-Tempel/Login/Saison lösen Karten-/Runen-/EXP-Belohnungen nur teilweise ein** (Token-Resolver inzwischen für Karten da, aber rune_fragment/exp_potion fehlen; kein Premium-Kauf-Pfad). | Oekosystem_v4; ShopController.cs, LoginRewardController.cs | Mittel |
| K12 | **Runen-Einsetzen ins Deck fehlt** — weder RuneScreen noch DeckBuilder schreiben je `Deck.RuneInstanceIds`. Runen sind nicht ans Deck verdrahtet. | v5 4.2/7; RuneScreen.cs, DeckBuilderScreen.cs | Mittel |
| K13 | **Karten-Besitz-Limits + Scrap-Umwandlung + Rückkauf fehlen** (3★ max5, 4★ max3, 5★ max2, 6★ max1; über Limit → Scraps; 24h-Rückkauf). | Oekosystem_v4 Kap.; BalancingConfig (nur Config, ungenutzt) | Mittel |

---

## 2. MITTEL — gezielte Daten-/Code-Fixes

| # | Befund | Soll | Ist | Quelle |
|---|--------|------|-----|--------|
| M1 | Starter-Karten | **5 rassenspezifische** (3× eigene Rasse + 1 Elfe/Ritter + 1 Tiergeist) | 3 feste (alle Ritter) | Designplan_v4 Kap.7 |
| M2 | W10-Endboss | **Nythragor** | aetherius_allschoepfer | Story_v4 Z.194 |
| M3 | W5-Boss | Schatten-Doppelgänger | daemonenkoenigin_lilith (ist Mentorin!) | Story_v4 Z.371 |
| M4 | W9-Boss | Verderbnis-Avatar | selene_mondschleier | Story_v4 Z.188 |
| M5 | Boss-Namen | „Erdtitan" sollte „Sandkaiser" sein (W2), Story-Bossnamen | erdtitan_gorath etc. | Story_v4 Kap.5 |
| M6 | Boss-Phase 2 | bei **jedem** Boss-Kampf ab 50% HP | nur auf Gott-Stufe (`ActivatesBossPhases`) | v5 9.4 |
| M7 | Element→Status-Mapping | Dunkel→Gift, Natur→Heilung, Licht→Cleanse | Natur→Poison, Dunkel→Silence, Licht→Slowed (thematisch falsch) | Designplan_v4 Kap.3.3 |
| M8 | `Deck.MaxRuneSlots` | **4** | 5 (durch RuneType.Mana-Extra) | v5 7.1 |
| M9 | Mentor W1/W5/W7/W9 | rassenabhängig (W1) bzw. Story-Welt-Sets | statisch (Lumis/Grimmfang/Dorn/Aetherius) | Story_v4 Kap.10 |
| M10 | Mythic-Kern | eine Quelle | doppelt (`Inventory` + `SternkartenSaveSlice`) | — |
| M11 | story_fragments.json | verdrahten oder streichen | tote Daten (kein Loader) | — |
| M12 | Fragment-ID-Schema | konsistent (`fragment_N` ODER worldId überall) | Code speichert worldId, JSON erwartet fragment_N | — |
| M13 | Synergie/Rivalen-Daten | flächig + reziprok | nur 2 synergyCardIds (einseitig) + 5 rivalCardIds | Designplan_v4 Kap.8 |
| M14 | Mixed-Element-Welten | W8 Wasser/Dunkel, W9/W10 „Alle" | single themeElement | Designplan_v4 Kap.3.5 |
| M15 | „Gott des Schildes" | eigene Götter-Karte | erzeugt solaris_gott_feuer (Kollision mit recipe_solaris_4star) | Designplan_v4 Kap.5 |
| M16 | Stat-Spannen-Verstöße | 5 Epic-Karten HP>2000, marschall_eisen ATK<650, jormungand HP>2500, baumhueter_sylvan HP>1600, heilige_wachtmeisterin ATK<300 | siehe Befund | Designplan_v4 Kap.4.1 |
| M17 | Saison-Pass-Karten | ≥2 `isSaisonPassCard`-Karten (3★/4★) in cards.json | 0 vorhanden | Oekosystem_v4 |
| M18 | DeckBuilder UI | Runen-Zuweisung + COST-„X/200" + Validierungs-Texte (Cost/Leg/Epic/Myth) | fehlt / generisch | v5 4.2 |
| M19 | Event 15.000-Punkte | + Goldener Event-Titel | nur 500 Diamanten | Oekosystem_v4 |
| M20 | Geheimnisvolle Gestalt (W1/W3-Spion) + Lumis-Geheimnis | als Story-Figuren | fehlen ganz | Story_v4 |

---

## 3. KLEIN — eindeutige, isolierte Fixes (sofort)

| # | Befund | Fix |
|---|--------|-----|
| S1 | Waldläufer-Text „kostet 0 COST" | → „kostet 0 Mana" (strings.csv, 6 Sprachen) |
| S2 | `race.*.name`-Keys fehlen | RaceSelectionScreen auf `hero.*.name` umstellen (existieren) |
| S3 | SaisonPassDefinition-Defaults | TotalTiers 50→30, HardCapTier 100→30, Doc-Kommentar |
| S4 | Lira→Selene-Bezug | „Selene" in Lira-Desc/W4-Story ergänzen |
| S5 | Lumis-Rasse „Goetter" | als Lichtgeist markieren (story_fragments.json) |
| S6 | CardUpgradeService Quell-Kommentare | auf aktuelle Spec-Stelle korrigieren (bereits +80% gefixt) |
| S7 | material_drops.json | titanengrat_n5/n10 ergänzen (W7 ohne Drops) |

---

## 4. QUELLENKONFLIKTE — brauchen Robert-Entscheidung

| # | Konflikt | Option A | Option B |
|---|----------|----------|----------|
| Q1 | **Karten-Leveln mit Scraps** | v4-„infos_danach": Upgrade-Steine streichen, Leveln nur EXP+Gold | DESIGN.md v6: Scraps fürs Leveln (aktueller Code) |
| Q2 | **Dämonen-Mentor** | Designplan_v4 Kap.7: Schattenflüsterer Morah | Story_v4 Kap.10: Lilith (aktueller Code) |
| Q3 | **Schwach-gegen-Malus** | v4: nur +10% Stark, kein Eigen-Malus (WeakMultiplier entfernen) | Code: −10% WeakMultiplier (Balancing-Extra) |
| Q4 | **Besitz-Limit-Quelle** | v5: max 3 sammelbar | DESIGN.md: 3★ max5/4★ max3/5★ max2/6★ max1 |

---

## 5. CODE-EXTRAS (nicht in Referenz — dokumentieren oder entfernen)

`RuneType.Mana` + mana_start-Rune · `WeakMultiplier=0.90f` · Stat-Multiplikatoren Classic/Amateur/Profi/Gott (1.0/1.25/1.6/2.2) · `recommendedPlayerLevel` (1…130) · alle Node-Gold/EXP-Werte + baseGoldPerDay W3-9 · EXP-Schwellen-Tabelle · Runen-Level-1-10-System · DailyShop (aus v5-Shop) · MaxTurns=50/Draw-Tiebreak · Enemy-Hero-HP fix 1000 · 4 Sammelset-Karten (aus v5) · `isFinale`-Flag · Pseudo-Säulen W2/4/9/10.

→ Diese sind überwiegend sinnvolle Erweiterungen. Empfehlung: in `DESIGN.md`/`balancing.md` als bewusste Code-Erweiterungen festhalten, statt sie zu entfernen (außer RuneType.Mana → entfernen, da es den 5. Deck-Slot-Konflikt verursacht).

---

## 6. Abarbeitungs-Reihenfolge (empfohlen)

1. **Klein-Fixes (Abschnitt 3)** + sichere Mittel-Fixes (M6/M7/M8/M16/M19) — sofort, Unity-verifiziert.
2. **Quellenkonflikte (Abschnitt 4)** mit Robert klären.
3. **Story (K9)** — hoher narrativer Wert, mittlerer Aufwand (Twist-Gate + Ending-Choice).
4. **Karten-Drops/Boss-Belohnungen (K8)** + Premium/Tempel-Belohnungen (K11) — Economy-Loop.
5. **Auto-Battle-UI (K5)** — Domain-Logik existiert, nur Verdrahtung.
6. **Skill-System (K1/K2)** + Schild/Element-Effekte (K3/K4) — größter Brocken, eigenes Datenmodell.
7. **Event-Runtime (K6)**, Runen-Einsetzen (K12), Besitz-Limits (K13), Mixed-Element (M14).

> Detaillierte Einzelbefunde mit Datei:Zeile-Belegen liegen in den 11 Agenten-Berichten
> (temporär unter `…/ak_abgleich/*.md`); die wichtigsten sind hier konsolidiert.
