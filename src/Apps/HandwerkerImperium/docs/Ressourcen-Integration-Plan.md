# HandwerkerImperium — Ressourcen-Integration-Plan (AAA)

**Version:** 1.0
**Datum:** 2026-05-12
**Geltungsbereich:** HandwerkerImperium (Idle-Game, v2.1.0)
**Ziel-Niveau:** AAA-Idle-Game (Adventure Capitalist + Egg Inc + Idle Bank Tycoon-Vergleichsklasse)
**Verantwortlich:** Robert Schneider
**Status:** Konzept, freigabebereit

---

## 1. Vision

Das Imperium soll sich anfuehlen wie ein echtes Imperium — nicht wie 10 parallele Geld-Hahne. Jede Werkstatt soll eine *erkennbare Rolle in einer lebendigen Lieferkette* haben, jedes Material soll an mehreren Stellen im Spiel wirken (Bauen, Aufträge, Gilde, Prestige), und Spieler-Entscheidungen ueber Lagerhaltung, Spezialisierung und Lieferketten-Optimierung sollen genauso strategisch wirken wie heute Workshop-Upgrades und Worker-Hiring.

Das Crafting-System ist heute bereits architektonisch sauber angelegt (`CraftingRecipe.InputProducts`, `CraftingProduct.Tier`, `CraftingInventory`, `MaterialOrder`), wird aber funktional nur als isolierte Verkaufs-Pipeline genutzt. **Dieser Plan aktiviert das vorhandene Fundament zu einem voll integrierten Ressourcen-Loop, ohne das bestehende Spiel umzuwerfen.**

### Kernerlebnis (Player Fantasy)

> "Ich entscheide morgens, ob ich heute Holz vorrats-produziere, weil der Architekt-Tier-3-Auftrag das braucht — oder ob ich auf den Material-Markt setze, weil der Holz-Preis gerade hoch ist. Mein Lager ist Strategie, mein Worker-Pool ist Strategie, meine Werkstaetten sind Glieder einer Kette, die ich orchestriere."

### Erfolgs-Kriterien (Definition of Done)

| Kriterium | Messbar |
|-----------|---------|
| Spieler verbringt Lager-/Crafting-Zeit signifikant | Crafting-Tab-Sessions > 25% aller Sessions |
| Mid-Game (Lv 50–200) hat aktive Entscheidungsmomente | Material-Order-Annahmequote > 60% |
| Late-Game (Lv 300+) hat Sink fuer Ressourcen | Tier-3-Produktion > 50/Tag pro aktivem Spieler |
| Veteranen kehren wegen Material-Loop zurueck | D7-Retention +5%, D30 +3% gegenueber Baseline |
| Keine UX-Regression bei Casual-Spielern (Lv 1–50) | Crash-rate stabil, Tutorial-Drop unverändert |

---

## 2. Status-Quo-Analyse

### 2.1 Was heute existiert (Fundament)

| System | Implementiert | Genutzt | Problem |
|--------|---------------|---------|---------|
| `CraftingRecipe` mit `InputProducts` | Ja | Nur intra-Workshop (Tier-1 → Tier-2 → Tier-3 derselben Werkstatt) | Cross-Workshop-Inputs fehlen |
| `CraftingProduct` (Tier 1/2/3) | Ja, 19 Produkte | Verkauf + MaterialOrder | Keine weiteren Senken |
| `CraftingInventory` Dictionary | Ja | Stapelt unbegrenzt | Kein Lager-Druck, kein Limit |
| `AutoProductionService` | Ja, 10 Workshops, Tier-1 ab Lv50, Higher-Tier ab Lv150/300 | Hintergrund-Drip | Keine Player-Choice |
| `MaterialOrder` Auftragstyp | Ja, ab Lv50, 1.8x Reward | Eine Senke | Einzige Sink ausser Direktverkauf |
| `CraftingSellMultiplier` im IncomeCalculator | Ja | Verkaufspreis | Kein Markt, kein Spread |
| `MasterSmith` Spezial-Boost | Ja, 60s Tick + passive Materialien | Beschleuniger | Keine Material-Veredelung |
| `InnovationLab` | Ja, +Research-Speed | Forschungs-Boost | Kein Crafting-Hebel |

### 2.2 Lücken (was AAA-Standard erwartet, aber heute fehlt)

1. **Keine Cross-Workshop-Lieferketten** — alle Tier-2/3-Rezepte haben nur Inputs der eigenen Werkstatt.
2. **Aufträge konsumieren keine Materialien** ausser MaterialOrder — Standard/Large/Cooperation laufen rein über MiniGames.
3. **Keine Lager-Mechanik** — Inventar ist eine endlose Liste ohne Stack-Limits, ohne Slots, ohne UI-Visualisierung im Imperium-Tab.
4. **Kein Markt** — Preise sind statisch (BaseValue × log-Skala), keine Saisonalität, keine Spekulation.
5. **Forschung schaltet keine Rezepte/Tiers frei** — 45 Research-Nodes sind reine Multiplikator-Boni.
6. **Worker-Affinität endet bei Workshop** — keine Material-Spezialisierung.
7. **Gilde nutzt keine Materialien** — Co-op-Aufträge sind Geld-Aufträge, keine Material-Pools.
8. **Prestige rettet Geld/Worker/Tools, aber keine Items** — Crafting-Output ist beim Reset wertlos.
9. **Daily/Weekly haben keine Material-Achse** — Missions targeten Aufträge und MiniGames.

---

## 3. Designsystem (Kern-Mechaniken)

### 3.1 Material-Klassifikation (3 Achsen)

Jedes Material bekommt drei Eigenschaften, die seine Rolle im Imperium bestimmen:

| Achse | Werte | Beispiel |
|-------|-------|----------|
| **Tier** | 1, 2, 3, 4 (neu) | T1 Holzbrett, T2 Möbelstueck, T3 Luxusmöbel, T4 Imperiums-Manufaktur |
| **Kategorie** | Rohstoff, Halbzeug, Endprodukt, Prestige-Erbstueck | T1 = Rohstoff, T2 = Halbzeug, T3 = Endprodukt, Erbstueck = T4 |
| **Quelle** | Workshop-Eigenproduktion, Cross-Workshop, Lieferant, Markt, Gilde, Event | bestimmt Verfuegbarkeit & Knappheit |

**Neue Klasse:** Tier 4 als "Imperiums-Manufaktur" — wenige, sehr wertvolle Endprodukte, die aus Tier-3 mehrerer Werkstaetten zusammen entstehen. Beispiel: "Villa-Komplett-Paket" = 5x Luxusmoebel + 3x Smart-Home + 2x Dachstruktur + 1x Kunstwerk.

### 3.2 Lieferketten-Architektur (Cross-Workshop)

**Prinzip:** Tier-2/3-Rezepte konsumieren *Materialien anderer Werkstaetten*. Jede Werkstatt hat dadurch eine erkennbare Rolle.

**Werkstatt-Rollen-Matrix (Ziel-Zustand):**

| Werkstatt | T1 (Rohstoff) | T2-Inputs (Halbzeug) | T3-Inputs (Endprodukt) | Rolle in Kette |
|-----------|---------------|----------------------|-------------------------|----------------|
| Carpenter | Holzbrett | 3x Holzbrett + 1x Klebstoff (Painter T1) | 2x Moebelstueck + 1x Beschlag (MasterSmith T1) | Rohstoff-Lieferant |
| Plumber | Rohrleitung | 3x Rohrleitung + 1x Beschlag (MasterSmith T1) | 2x Sanitaer-System + 1x Smart-Home (Electrician T3) | Halbzeug-Knoten |
| Electrician | Kabel | 3x Kabel + 1x Prototyp (InnovationLab T1) | 2x Schaltkreis + 1x Beton (Contractor T1) | Vernetzer |
| Painter | Farbe | 3x Farbe + 1x Blaupause (Architect T1) | 2x Wand-Design + 1x Holzbrett (Carpenter T1) | Veredler |
| Roofer | Dachziegel | 3x Dachziegel + 1x Beton (Contractor T1) | 2x Dachsystem + 1x Tragwerk-Modul (Architect T2-neu) | Schwerlast-Veredler |
| Contractor | Beton | 3x Beton + 1x Stahltraeger (Plumber-Variant) | 2x Beton-Fundament + 1x Vertrag (GeneralContractor T1) | Bauwerks-Basis |
| Architect | Blaupause | 3x Blaupause + 1x Holzbrett + 1x Beton | 2x Tragwerk + 1x Dachstruktur | Planungs-Knoten |
| GeneralContractor | Vertrag | 3x Vertrag + 1x Blaupause (Architect T1) | T3 = T4-Trigger-Item | Imperiums-Spitze |
| MasterSmith | Beschlag | 3x Beschlag + 1x Kabel (Electrician T1) | 2x Meister-Beschlag + 1x Prototyp | Veredelung & Crit |
| InnovationLab | Prototyp | 3x Prototyp + 1x Kabel (Electrician T1) | 2x Innovation + 1x Beschlag (MasterSmith T1) | Premium-Verwerter |

**Neue Tier-2-Rezepte fuer 5 fehlende Werkstaetten:**
- Contractor T2 "Beton-Fundament" (`r_foundation`)
- Architect T2 "Tragwerk-Modul" (`r_framework`)
- GeneralContractor T2 "Bauvertrag-Komplex" (`r_contract_complex`)
- MasterSmith T2 "Meister-Beschlag" (`r_master_fittings`)
- InnovationLab T2 "Innovation" (`r_innovation`)

**Neue Tier-3-Rezepte fuer dieselben 5:**
- Contractor T3 "Hochhaus-Rohbau" (`r_skyscraper_frame`)
- Architect T3 "Komplett-Bauplan" (`r_master_blueprint`)
- GeneralContractor T3 "Generalauftrag" (`r_general_contract`) — Sammelt T4-Trigger
- MasterSmith T3 "Meisterwerk-Beschlag" (`r_masterpiece_fittings`)
- InnovationLab T3 "Patent" (`r_patent`)

**Neue Tier-4-Rezepte (Imperiums-Manufaktur, ab Lv 500):**
- GeneralContractor T4 "Villa-Komplett" (`r_villa`) — 5x Luxusmöbel + 3x Smart-Home + 2x Dachstruktur
- GeneralContractor T4 "Wolkenkratzer" (`r_skyscraper`) — 5x Hochhaus-Rohbau + 3x Sanitaer + 3x Smart-Home + 2x Kunstwerk
- GeneralContractor T4 "Imperiums-Komplex" (`r_imperium_hq`) — alle T3 mind. 2x

Tier-4-Items sind die einzigen, die **Prestige ueberleben koennen** (siehe 3.7).

### 3.3 Aufträge konsumieren Materialien (universelle Material-Anforderung)

**Heute:** Nur `MaterialOrder` (Lv 50+) konsumiert Items.
**Ziel:** Alle Auftragstypen (Standard/Large/Cooperation/Weekly) bekommen eine *optionale* Material-Anforderung mit Bonus-Reward.

**Mechanik:**
- Bei Auftrags-Spawn: 35% Chance auf Material-Anforderung (skaliert mit Level, ab Lv 30 sichtbar).
- Anforderung wird **vor Annahme** transparent angezeigt: "3x Holzbrett + 1x Kabel — Bonus +35% Reward".
- Spieler entscheidet: Annehmen mit Materialien (Items werden verbraucht, Bonus aktiv) oder Annehmen ohne Materialien (normales Reward).
- Falls Annahme mit Materialien gewaehlt: Items sofort reserviert (`ReservedInventory` Dictionary, kein Doppelverbrauch).
- Bei Risk-Strategie + Material: Miss verbraucht Material *und* gibt 0 Reward (echtes Risiko).

**Spielerseitige Wirkung:**
- Inventar fuehlt sich wie aktiver Lagerbestand an, nicht wie Sammlung.
- Cross-Workshop-Lieferkette wird *belohnt*, weil Anforderungen oft gemischte Items verlangen.
- Mid-Game-Loop: Produzieren → Lagern → Auftrag mit Material annehmen → Bonus-Reward → Wieder produzieren.

**Material-Anforderungs-Pool pro Tier:**

| Auftrags-Tier | Material-Anforderung | Bonus-Reward |
|---------------|----------------------|--------------|
| Quick | 1x T1 | +25% |
| Standard | 2–3x T1 | +30% |
| Large | 1x T2 + 3x T1 | +40% |
| Cooperation | 2x T2 (verschiedene WS) | +50% |
| Weekly | 1x T3 + 2x T2 | +60% |
| MaterialOrder | (wie heute) | 1.8x Basis |

### 3.4 Imperiums-Lager (sichtbarer Hub im Imperium-Tab)

**Neue Komponente:** Ein neuer Sub-Tab "Lager" im Imperium-View (zwischen `Workshops` und `Workers`).

**Mechanik:**
- Lager hat **Slots** und **Stack-Limits**. Beides ist upgradebar (Geld + Forschung).
- Start: 20 Slots × 50 Stack-Limit pro Slot.
- Upgrade-Pfad: bis 200 Slots × 9999 Stack via Forschung + Geld.
- Pro Slot: ein Material-Typ. Ueberlauf-Politik:
  - **Bei aktivem Auto-Verkauf-Toggle:** Ueberlauf wird automatisch zum Marktpreis verkauft (kein Verlust).
  - **Ohne Auto-Verkauf:** Produktion stoppt bei vollen Stacks (Push-Visualisierung: "Carpenter pausiert — Lager voll").

**UI:**
- 5×4 Grid (skaliert mit Slots), jedes Slot zeigt Icon, Count, Stack-Limit als Fortschrittsbalken.
- Touch auf Slot: Detail-Sheet mit Item-Info, Verkaufen, Auto-Verkauf-Toggle, Wert/Stück, Wert/Lager.
- Header: Lager-Wert gesamt (Live-Anzeige).

**Pause-Mechanik:**
- Wenn ein Workshop-Slot voll: Production blockt, Workshop zeigt gelben Warn-Badge auf der Card.
- Spieler kann pro Workshop "Bevorzugt verkaufen" toggeln (Auto-Verkauf nur für diesen Workshop).

**Spielerseitige Wirkung:**
- Lager wird sichtbares strategisches Asset.
- Lager-Upgrade ist *Pflicht*, sobald Cross-Workshop-Produktion läuft (mehr Materialien gleichzeitig).
- Push-Logik (Workshop pausiert) zwingt den Spieler zu Entscheidungen: verkaufen, ausgeben, upgraden.

### 3.5 Material-Markt (mit Preis-Dynamik)

**Neuer Sub-Tab im Shop:** "Markt".

**Mechanik:**
- Alle Materialien handelbar (kaufen + verkaufen).
- Preise sind dynamisch:
  - **Basis-Preis:** `BaseValue × CraftingSellMultiplier`
  - **Markt-Schwankung:** ±50% Sinus-Welle ueber 24h (deterministisch pro Spieler, Seed = PlayerId + Tag).
  - **Event-Modulator:** `MaterialShortage`-Event verdreifacht den Preis des betroffenen Workshop-Materials. `HighDemand` doppelt.
  - **Hysterese:** Verkaufspreis = Markt × 0.95 (5% Spread, simuliert Maklergebuehr).
- Spieler-UI: Heatmap-Chart der letzten 24h pro Material + Trend-Pfeil.

**Lieferant-Erweiterung:**
- Existierendes `Lieferant-System` (alle 2–5 min, 5 Typen) wird erweitert um **Material-Lieferung**: 1–10 Tier-1-Items eines zufaelligen Materials gratis.
- Wahrscheinlichkeit: 25% (verdraengt Geld-Lieferung leicht).

**Spielerseitige Wirkung:**
- Spekulations-Mini-Loop: T1 guenstig kaufen, T3 daraus craften, hoch verkaufen.
- Saisonale Events bekommen *direkte* Material-Wirtschaftsfolgen.
- Markt-Tab wird Daily-Habit ("Wie steht Holz heute?").

### 3.6 Forschung schaltet Rezepte/Tiers/Lagerslots frei

**Neuer Forschungs-Branch:** "Logistik" (`logi_*` IDs), 12 Nodes.

| Node | Effekt | Voraussetzung |
|------|--------|---------------|
| `logi_01` | +5 Lager-Slots | — |
| `logi_02` | Stack-Limit ×2 | `logi_01` |
| `logi_03` | T2-Rezepte fuer Contractor/Architect/GenCon/MS/IL freigeschaltet | Workshop Lv 150 |
| `logi_04` | +10 Lager-Slots | `logi_02` |
| `logi_05` | Material-Markt verfuegbar | `logi_03` |
| `logi_06` | T3-Rezepte fuer die 5 neuen Werkstaetten | Workshop Lv 300 |
| `logi_07` | Auto-Verkauf-Regeln (pro Slot Min/Max) | `logi_05` |
| `logi_08` | Lieferanten-Material-Bonus +50% | — |
| `logi_09` | T4-Rezepte (Imperiums-Manufaktur) | Alle T3 1x produziert |
| `logi_10` | Speed-Bonus auf Crafting +20% | `logi_06` |
| `logi_11` | Stack-Limit ×5 | `logi_04` |
| `logi_12` | Materialien ueberleben Prestige (Top-5-Stacks) | Bronze Prestige |

**Wirkung:** Forschung fuehlt sich nicht mehr nur wie "+X%" an, sondern eroeffnet Inhalte. `logi_03`/`logi_06` sind die zentralen Unlock-Gates der gesamten neuen Lieferkette.

### 3.7 Worker-Material-Affinität

**Heute:** Worker haben Workshop-Affinitaet, Personality, Talent.
**Neu:** Sekundaere Material-Affinitaet (eine von 5: Holz, Metall, Stein, Kunst, Tech).

**Mechanik:**
- Affinitaet wird beim Hiring gerollt (Verteilung gleichmaessig 20% pro Achse).
- Wird in `WorkerProfileVM` angezeigt mit Icon + Tooltip.
- Wirkt als Multiplikator auf Crafting-Speed:
  - Affinitaet matched Material-Kategorie: +20% Crafting-Speed des Workshops fuer dieses Material.
  - Affinitaet matched nicht: ±0%.
- S-Tier+ Worker bekommen +5% pro Stern (Skill-Stufe).

**Material-Kategorien-Zuordnung:**

| Affinitaet | Materialien |
|------------|-------------|
| Holz | Holzbrett, Möbelstueck, Luxusmöbel, Beton-Fundament-Schalung-Inputs |
| Metall | Rohrleitung, Beschlag, Stahltraeger, Sanitaer-System |
| Stein | Beton, Hochhaus-Rohbau, Dachziegel, Dachsystem |
| Kunst | Farbe, Wand-Design, Kunstwerk, Luxusmöbel-Veredelung |
| Tech | Kabel, Schaltkreis, Smart-Home, Prototyp, Innovation, Patent |

**Spielerseitige Wirkung:**
- Hiring wird taktisch: "Brauche ich noch einen Holz-Worker, weil ich diese Woche viele Architect-T3-Auftraege habe?"
- Worker-Markt-Filter erweitert um Affinitaets-Filter.

### 3.8 Prestige & Erbstuecke

**Heute:** Prestige rettet je nach Tier Achievements, Settings, Research, Shop, MasterTools, Buildings (Lv→1), Manager (Lv→1), Top-3-Worker.
**Neu:** Spieler kann beim Prestige bis zu **3 Tier-4-Items als Erbstuecke** in den naechsten Run mitnehmen.

**Mechanik:**
- Nur Tier-4 ist erbstueck-faehig (T1–T3 wird zurueckgesetzt).
- Pro Erbstueck im Lager: +2% Globales Einkommen *im naechsten Run* (stackt bis +6%).
- Bei Ascension: Erbstuecke werden permanent in einen "Erbstueck-Schrein" (neues Sub-Element im Ascension-Tab) ueberfuehrt. Jedes permanente Erbstueck: +0.5% Globales Einkommen forever (kein Cap, aber Tier-4-Produktion ist langsam → de-facto-Cap durch Produktionszeit).

**Spielerseitige Wirkung:**
- Tier-4-Produktion am Run-Ende ist nicht wertlos.
- Verbindet Crafting-Loop mit Prestige-Loop (heute zwei getrennte Mini-Loops).
- Veteranen-Sink: Erbstuecke-Sammlung ist eine endlose Quest.

### 3.9 Gilden-Materialpool

**Heute:** Gilden haben Co-op-Aufträge (Geld), Forschung (Geld), Hall (Geld).
**Neu:** Gilden-Materiallager.

**Mechanik:**
- Mitglieder koennen Materialien spenden (PATCH-atomar via Firebase, analog zu Co-op-Aufträgen).
- Mega-Projekte ("Gilden-Kathedrale", "Gilden-Hauptquartier") verbrauchen kumulativ Tier-3/Tier-4 ueber Wochen.
- Belohnung: Permanenter Gildenbonus (+5% Crafting-Speed, +10% Auto-Verkaufs-Preis, +3 Lager-Slots fuer alle Mitglieder).
- HMAC-signiert wie alle Gilden-Operations, Idempotenz ueber `ClaimedGuildProjectIds`.

**UI:** Neuer Tab im Gilden-View "Bauplatz" — zeigt aktives Mega-Projekt, Fortschritt, Top-Spender-Leaderboard.

**Spielerseitige Wirkung:**
- Materialien werden sozial wertvoll.
- Casual-Spieler kann durch Material-Spenden zur Gilde beitragen, ohne Top-Spender beim Geld zu sein.
- Veteranen haben Langzeit-Sink fuer Tier-4-Ueberproduktion.

### 3.10 Daily/Weekly Missions & Battle Pass

**Heute:** Daily Challenges, Weekly Missions, Battle Pass — alles auftrags- und MiniGame-zentriert.
**Neu:** Material-Achse als 4. Mission-Typ.

**Mission-Typen erweitert:**

| Mission | Beispiel | Reward |
|---------|----------|--------|
| Material-Produktion | "Produziere heute 50x Holzbrett" | 5 SP |
| Material-Verkauf | "Verkaufe heute 200x Tier-1-Material" | 3 SP + 200 GS |
| Lieferketten-Mission | "Crafte heute 1x Tier-3-Item" | 10 SP |
| Markt-Mission (Weekly) | "Erziele 1M Profit am Markt diese Woche" | 50 SP + Special Frame |

Bestehende Services (`IDailyChallengeService`, `IWeeklyMissionService`, `IBattlePassService`) haben den Recorded-Hook bereits — `MiniGameResultRecorded`-Pattern wird gespiegelt auf `MaterialCraftedRecorded`, `MaterialSoldRecorded`.

---

## 4. Phasen-Plan (Roadmap)

Vier Releases mit klaren Schnitten, damit jede Phase unabhaengig shippbar ist.

### Phase 1 — Foundation (v2.2.0)

**Ziel:** Cross-Workshop-Lieferkette aktivieren, ohne UI-Umbau. Lager mit Limits einfuehren.

**Scope:**
- `CraftingRecipe.InputProducts` mit Cross-Workshop-Inputs befuellen (T2/T3 fuer alle 10 Werkstaetten ergänzen).
- 5 fehlende T2/T3-Rezepte fuer Contractor/Architect/GenCon/MS/IL hinzufuegen.
- `CraftingService.StartJob()` validiert Cross-Workshop-Inputs gegen `CraftingInventory`.
- `AutoProductionService` prueft Inputs vor passiver Produktion (Skip bei fehlenden Materialien).
- Lager-Slots: 20 Start, Stack-Limit 50, Upgrade via Geld (5 Tiers).
- Push-Logik: Workshop pausiert bei vollem Slot, gelber Warn-Badge auf Card.
- SaveGame Version 7 (Lager-Slots, Stack-Limits, `ReservedInventory`).

**Files:**
- `Models/CraftingRecipe.cs` — Inputs erweitern (~30 neue Recipe-Eintraege).
- `Models/CraftingProduct.cs` — 5 neue T2 + 5 neue T3 + 3 neue T4-Produkte.
- `Models/GameState.cs` — `WarehouseSlotCount`, `WarehouseStackLimit`, `ReservedInventory`.
- `Services/AutoProductionService.cs` — Input-Validierung, Pause-Logik.
- `Services/CraftingService.cs` — Cross-Workshop-Input-Check.
- `Services/WarehouseService.cs` *(NEU)* — Slot-/Stack-Management, Pause-Events.
- `ViewModels/Imperium/WarehouseSectionViewModel.cs` *(NEU)*.
- `Views/Imperium/WarehouseSection.axaml` *(NEU)* — neuer Sub-Tab.
- `ViewModels/MainViewModel.Properties.cs` — `IsImperiumWarehouseActive`.
- `Models/Enums/ImperiumSubTab.cs` — neuer Wert `Warehouse`.

**Risiken:**
- AutoProduction-Pause kann Mid-Game-Spieler verwirren → klare Push-Texte + In-Game-Tipps.
- Cross-Workshop-Inputs koennen Casual-Spieler ueberfordern → Tier-2 nur mit *eigenen* T1-Inputs bis Lv 100, ab Lv 100 Cross-Workshop.

**KPIs:**
- Crafting-Tab-Sessions: Baseline messen vorher, +50% nach Phase 1.
- AutoProduction-Pause-Rate: < 30% der Workshops in einem 24h-Fenster.

### Phase 2 — Aufträge & Material-Anforderungen (v2.3.0)

**Ziel:** Aufträge konsumieren Materialien fuer Bonus-Reward.

**Scope:**
- `Order.MaterialRequirement` (Dictionary<string,int>) + `Order.MaterialBonusMultiplier`.
- `OrderGeneratorService` rollt 35% Material-Anforderung (skaliert mit Level).
- `Order`-UI: Anforderung mit Item-Icons + Bonus-Anzeige *vor* Annahme.
- Annahme reserviert Items in `ReservedInventory`, schreibt sie bei MiniGame-Complete ab.
- Risk-Strategie + Material: Miss konsumiert Material trotzdem.
- Daily/Weekly Mission-Hooks: `MaterialCraftedRecorded`, `MaterialSoldRecorded`, `MaterialOrderCompletedWithBonus`.
- 4 neue Mission-Definitionen (Produktion, Verkauf, Lieferkette, Markt).

**Files:**
- `Models/Order.cs` — Material-Anforderung & Bonus-Felder.
- `Services/OrderGeneratorService.cs` — Roll-Logik + Skalierung.
- `Services/GameStateService.Orders.cs` — Reservation/Verbrauch.
- `ViewModels/EconomyFeatureViewModel.cs` — Annahme-Dialog erweitert.
- `Views/Dashboard/OrdersQuickJobsSection.axaml` — Order-Card mit Material-Badges.
- `Services/DailyChallengeService.cs`, `WeeklyMissionService.cs` — neue Mission-Typen.
- `Models/Missions/MissionTypes.cs` — 4 neue Typen.

**Risiken:**
- Material-Anforderung kann sich wie Wand anfuehlen ("Ich kann den Auftrag nicht annehmen") → *optional*, nicht *Pflicht*. Annahme ohne Material immer moeglich, nur kein Bonus.
- Risk-Strategie + Miss + Material-Verlust kann frustrieren → klare Warnung im Risk-Dialog.

**KPIs:**
- Annahmequote mit Material: > 60% bei Lv 50–200, > 80% bei Lv 200+.
- Material-Anforderung-Bonus-Reward-Anteil am Gesamteinkommen: 15–25%.

### Phase 3 — Markt & Logistik-Forschung (v2.4.0)

**Ziel:** Material-Markt mit Preis-Dynamik. Logistik-Forschungsbranch.

**Scope:**
- Neuer Shop-Sub-Tab "Markt" mit Kauf/Verkauf aller Materialien.
- `MarketService` *(NEU)* mit deterministischer Tagespreis-Logik (Seed = PlayerId + UtcDay).
- Event-Modulatoren auf Markt-Preise (`MaterialShortage` 3x, `HighDemand` 2x).
- Heatmap-Chart (SkiaSharp, neuer `MarketChartRenderer`) fuer 24h-Verlauf.
- 12 `logi_*`-Forschungsnodes, freigeschaltet ab Workshop Lv 150.
- Lieferant-Material-Lieferung mit 25% Wahrscheinlichkeit.

**Files:**
- `Services/MarketService.cs` *(NEU)*, `Services/Interfaces/IMarketService.cs`.
- `Models/MarketTrend.cs` *(NEU)*.
- `ViewModels/Shop/MarketViewModel.cs` *(NEU)*.
- `Views/Shop/MarketView.axaml` *(NEU)*.
- `Graphics/MarketChartRenderer.cs` *(NEU)*, SkPath-Cache.
- `Services/ResearchService.cs` + `Models/ResearchNode.cs` — `logi_*` Nodes.
- `Services/SupplierService.cs` — Material-Lieferung-Typ.

**Risiken:**
- Markt-Spekulation kann Idle-Charakter verwaessern → Markt nur einmal pro Stunde aktualisierbar (Push), kein Live-Daytrading.
- Determinismus: PlayerId-Seed verhindert Save-Scumming.

**KPIs:**
- Markt-Sessions/Spieler/Tag: 0.7 (durchschn.).
- Markt-Profit-Anteil am Gesamteinkommen: 5–15% (gesund), > 30% waere Imbalance.

### Phase 4 — Tier-4 & Sozial-Sink (v2.5.0)

**Ziel:** Imperiums-Manufaktur (T4) + Erbstuecke + Gilden-Material-Mega-Projekte.

**Scope:**
- 3 Tier-4-Rezepte (Villa, Wolkenkratzer, Imperiums-Komplex) ab Lv 500.
- Erbstuecke beim Prestige: bis zu 3 T4-Items mitnehmen, +2% Global pro Stueck.
- Erbstueck-Schrein im Ascension-Tab: permanente Erbstuecke, +0.5% Global pro Stueck.
- Gilden-Materiallager: Spenden via Firebase-PATCH.
- 2 Mega-Projekte ("Gilden-Kathedrale", "Gilden-Hauptquartier") mit Wochen-Skala.
- Material-Affinitaet bei Workers (5 Achsen, +20% Crafting-Speed bei Match).

**Files:**
- `Models/CraftingProduct.cs` — T4-Produkte.
- `Models/CraftingRecipe.cs` — T4-Rezepte.
- `Services/PrestigeService.cs` — Erbstueck-Auswahl-Dialog.
- `ViewModels/PrestigeConfirmationViewModel.cs` — Erbstueck-Slot-UI.
- `Models/AscensionData.cs` — `PermanentHeirlooms` Liste.
- `Services/GuildMaterialPoolService.cs` *(NEU)*.
- `Services/GuildMegaProjectService.cs` *(NEU)*.
- `ViewModels/Guild/GuildBuildSiteViewModel.cs` *(NEU)*.
- `Views/Guild/GuildBuildSiteView.axaml` *(NEU)*.
- `Models/Worker.cs` — `MaterialAffinity` Enum.
- `Services/WorkerService.cs` — Affinitaets-Roll bei Hiring.
- `Services/CraftingService.cs` — Affinitaets-Bonus auf Crafting-Speed.

**Risiken:**
- Tier-4-Produktion ist langsam (gewollt!) — Spieler koennten frustriert sein → klare Telemetrie/UI ("Du bist beim ersten T4-Item in ca. 4h").
- Mega-Projekte koennen verlassene Gilden blockieren → 30-Tage-Sunset-Regel mit Refund.

**KPIs:**
- T4-Produktion pro aktivem Spieler/Woche: > 5.
- Erbstueck-Auswahl beim Prestige: 70% nutzen es.
- Gilden-Mega-Projekt-Abschluss-Rate (in 30 Tagen): > 40% aller aktiven Gilden.

---

## 5. Balancing-Leitlinien

### 5.1 Tier-Wert-Skalierung

| Tier | BaseValue-Range | Input-Wert | Output-Wert | Marge |
|------|-----------------|------------|-------------|-------|
| 1 | 400–2000 | 0 | 400–2000 | ∞ (Rohstoff) |
| 2 | 2000–8000 | ~1800 | 2000–8000 | 1.1–4.4x |
| 3 | 40000–80000 | ~7500 | 40000–80000 | 5.3–10.7x |
| 4 | 1.5M–5M | ~250000 | 1.5M–5M | 6–20x |

**Designprinzip:** Hoehere Tiers haben *progressiv hoehere Margen*, damit Lieferketten-Ausbau immer lohnt — aber *progressiv laengere Produktionszeiten*, damit Casual-Spieler nicht abgehaengt werden. T4 dauert 30+ min, T3 dauert 5 min, T2 dauert 2 min, T1 dauert 30 sek.

### 5.2 Anti-Imbalance-Guards

1. **Markt-Spread 5%** — verhindert Sofort-Arbitrage.
2. **Markt-Tages-Refresh** — kein Daytrading-Loop.
3. **Stack-Limits** — Spieler kann nicht endlos hortet, muss verbrauchen.
4. **Material-Anforderung ist OPTIONAL** — kein Wand-Effekt.
5. **Erbstuecke nur Tier-4** — verhindert, dass Mid-Game-Item-Hortung Late-Game trivialisiert.
6. **Soft-Cap auf Crafting-Sell-Multiplier** — bleibt bestehen, kein Markt-Bypass.

### 5.3 Onboarding-Schutz (Casual-Spieler bis Lv 50)

- Cross-Workshop-Inputs erst ab Lv 100 (vorher nur eigene Inputs).
- Markt erst nach `logi_05` Research (nicht Pflicht-Pfad).
- Material-Anforderung-Trigger erst ab Lv 30 sichtbar.
- Lager-Slots Start: 20 ist genug fuer Lv 1–80 ohne Upgrade.

---

## 6. Save-Game-Migration (Version 7)

### 6.1 Neue Felder

```csharp
// GameState.cs (Version 7)
public int WarehouseSlotCount { get; set; } = 20;
public int WarehouseStackLimit { get; set; } = 50;
public Dictionary<string, int> ReservedInventory { get; set; } = new();
public Dictionary<string, AutoSellRule> AutoSellRules { get; set; } = new();
public List<string> HeirloomItems { get; set; } = []; // bis zu 3 Item-IDs
public List<string> PermanentHeirlooms { get; set; } = []; // im Ascension-Schrein

// Worker.cs
public MaterialAffinity MaterialAffinity { get; set; } = MaterialAffinity.None;

// Order.cs
public Dictionary<string, int>? MaterialRequirement { get; set; }
public double MaterialBonusMultiplier { get; set; } = 0.0;

// AscensionData.cs
public List<string> PermanentHeirlooms { get; set; } = [];
```

### 6.2 Migrations-Logik (`SaveGameService.MigrateToV7`)

1. `WarehouseSlotCount = 20`, `WarehouseStackLimit = 50` (Defaults).
2. `ReservedInventory` leer initialisieren.
3. Vorhandene `CraftingInventory`-Eintraege: wenn Count > Stack-Limit, auf Limit kuerzen, Differenz als Geld gutschreiben (1:1 zu BaseValue).
4. Worker bekommen Material-Affinitaet rueckwirkend per deterministischem Seed (WorkerId-Hash → eine von 5 Achsen).
5. Existierende Aufträge bleiben ohne Material-Anforderung (Backwards-Compat).
6. `currentStateVersion = 7`.

### 6.3 Cloud-Save-Konflikt

V7-Cloud-Save bei V6-Client → Alert (heute schon implementiert in `SaveGameService`).

---

## 7. UX & UI

### 7.1 Imperium-Tab Sub-Navigation (neu)

```
Imperium-Tab:
  Workshops  |  Lager  |  Workers  |  Forschung  |  Equipment  |  Ascension
                  ^ NEU
```

`ImperiumSubTab` Enum erweitert um `Warehouse`. Lock-Icon-Overlay bis Lv 50 (analog zu Ascension-Lock).

### 7.2 Material-Icons

Alle Materialien als 128×128 WebP in `Assets/visuals/materials/` (bestehende GameIcon-Pipeline). 22 neue Icons (5 T1 ergaenzend + 5 T2 + 5 T3 + 3 T4 + 4 Material-Affinitaets-Badges).

### 7.3 Tooltips & Empty-States

- Crafting-Recipe-Card: zeigt Cross-Workshop-Inputs mit Workshop-Farbe als kleiner Strich am Item-Icon (visueller "Liefer-Pfeil").
- Empty-State im Lager: "Dein Imperium braucht Material! Schicke deine Werkstatt los."
- Push wenn Workshop pausiert: gelbe Toast-Nachricht + Workshop-Card-Badge.

### 7.4 Tutorial-Erweiterung

Ab Lv 50: Story-Dialog "Dein erstes Lager", erklaert Slots, Stack-Limits, Push-Logik. Ab Lv 100: Story-Dialog "Lieferketten", erklaert Cross-Workshop-Inputs. Ab Lv 150: Story-Dialog "Forschung & Logistik". (Story-Engine existiert.)

### 7.5 Lokalisierung

22 neue Material-Namen × 6 Sprachen (DE/EN/ES/FR/IT/PT) = 132 Resource-Keys. Plus 4 Material-Affinitaets-Namen × 6 = 24. Plus ~80 weitere Strings (UI-Labels, Tooltips, Tutorial). Gesamt ~240 neue Keys.

---

## 8. Telemetrie & KPIs

### 8.1 Neue Events

```csharp
TelemetryService.Track("material_crafted", productId, tier, workshop);
TelemetryService.Track("material_sold", productId, count, price, source);
TelemetryService.Track("material_market_trade", productId, side, price);
TelemetryService.Track("order_accepted_with_material", orderType, bonus);
TelemetryService.Track("warehouse_full_pause", workshopType, slotCount);
TelemetryService.Track("heirloom_chosen", itemId, runDuration);
TelemetryService.Track("guild_mega_project_donation", projectId, itemId, count);
```

### 8.2 Erfolgs-KPIs (siehe 1. Erfolgs-Kriterien)

Dashboard fuer Live-Beobachtung (HandwerkerImperium-Server oder Firebase Analytics).

### 8.3 Anti-Exploit-Detection

- Markt-Profit > 1.5× Crafting-Profit/Tag → Markt-Imbalance-Flag, Manual-Review.
- Reservation-Doppelverbrauch → atomarer `ReservedInventory`-Lock pro Order-ID.

---

## 9. Risiken & Mitigation

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|--------|---------------------|--------|------------|
| Casual-Spieler ueberfordert | Mittel | Hoch | Onboarding-Schutz (5.3), optionale Materialien, klares Tutorial |
| AutoProduction-Pause stoert Idle-Charakter | Mittel | Mittel | Auto-Verkauf-Toggle als Standard ab Lv 80, klare Push-UX |
| Markt wird Daytrading-Spiel | Niedrig | Hoch | Tages-Refresh, 5% Spread, kein Live-Update |
| Tier-4-Frust | Mittel | Mittel | Klare ETA-Anzeige, Erbstuecke als Belohnung |
| Save-Migration-Bug | Niedrig | Sehr Hoch | Automatischer Backup vor V7-Migration, Rollback-Pfad |
| Cloud-Save-Konflikt | Niedrig | Hoch | Bestehende Alert-Logik, V7-Cloud blockt V6-Client |
| Lokalisierungs-Backlog | Hoch | Niedrig | Phasenweise einfuehren, EN als MVP, Auto-Translate als Fallback |
| Performance bei vielen Slots | Niedrig | Mittel | LazyLoad-Slot-Renderer, 5fps Idle (FpsProfile-konform) |
| Material-Anforderung erzeugt Wand | Mittel | Mittel | IMMER optional, klare Bonus-Anzeige |

---

## 10. Monetarisierung — Imperium-Pass-Repositioning

### 10.1 Status-Quo

- **Banner-Werbung:** Wird im Spiel faktisch **nicht angezeigt** — die `AdMobHelper.AttachToActivity()`-Anbindung im Android-Code ist Legacy und liefert keine eingeblendeten Banner. Banner-Loesch-Versprechen ist heute kein Verkaufsargument und wird im Pass auch nicht beworben.
- **Rewarded Ads:** 13 Placements (Lucky-Spin, 2x-Boost, Material-Refill, Offline-Verdopplung, etc.). Diese bleiben *vollstaendig* erhalten — sie sind freiwillig, generieren Umsatz und sind faktisch Gameplay-Mechanik.
- **Heutiger Premium:** 4,99 EUR Lifetime, Inhalt heute schwach und kommunikativ unscharf ("Premium").
- **GS-Pakete:** Im Shop, bleiben unveraendert.

### 10.2 Neu: Imperium-Pass (4,99 EUR Lifetime)

Der bestehende 4,99-EUR-Kauf wird vom abstrakten "Premium" zum klar profilierten **Imperium-Pass** mit konkretem Inhaltsbundle. Preis-Punkt bleibt identisch, das Versprechen wird greifbar.

**Bundle-Inhalt:**

| Bestandteil | Spielwirkung |
|-------------|--------------|
| Rewarded-Belohnungen ×2 | Spieler schaut Rewarded-Ads weiter (Umsatz bleibt!), bekommt die doppelte Belohnung. Starkes Versprechen ohne Pay-to-Win-Wand. |
| +50% Offline-Einkommen | Direkt spuerbarer Komfort-Boost fuer Idle-Charakter. |
| Markt-Insider-Heatmap (24h-Vorhersage) | Strategischer Vorteil im neuen Material-Markt. Ohne Pass per `logi_05` Research deutlich spaeter erreichbar. |
| Auto-Verkaufs-Regeln (Min/Max-Preise pro Slot) | QoL fuer Lager-Management. Ohne Pass per `logi_07` Research erreichbar. |
| +1 Erbstueck-Slot beim Prestige (3 → 4) | Late-Game-Hebel, direkter Stack-Vorteil ueber Runs. |
| 2× Lucky-Spin pro Tag (statt 1×) | Daily-Engagement-Anker, gibt taegliches Erfolgserlebnis. |
| Auto-ClaimDaily | Bereits bestehend, bleibt im Pass. |
| +100% Goldschrauben aus Gameplay-Quellen | Bereits bestehend, bleibt im Pass. |

**Designprinzip:**
- Kein Inhalt verriegelt einen kompletten Spielzweig (kein Pay-to-Win).
- Jeder Pass-Bonus hat einen Non-Pay-Pfad (Research oder Geduld).
- Rewarded-×2 ist der starke Pitch — Spieler bezahlt 4,99 EUR und bekommt fuer immer den doppelten Wert pro Ad-Klick.

### 10.3 Weitere One-Time- & Consumable-IAPs (parallel zum Pass)

| Item | Wert | Typ |
|------|------|------|
| +5 Lager-Slots-Pack | 50 GS | One-Time, cosmetic+functional |
| Heirloom-Slot +1 (4 → 5, nur mit Pass) | 200 GS | One-Time, Late-Game-Sink |
| Material-Refill (1× T1-Stack auf zufaelliges Material) | Rewarded-Ad | Neuer Placement |
| Sofort-Crafting-Speedup (skipt aktuelle Crafting-Job-Dauer) | Rewarded-Ad | Neuer Placement |
| Markt-Tagespreis-Reveal (1×) | Rewarded-Ad | Neuer Placement, fuer Nicht-Pass-Spieler |

### 10.4 Optional: Saison-Pass (langfristige Option)

Falls langfristig recurring Revenue gewollt: **Saison-Pass** (90 Tage, 4,99 EUR pro Saison) parallel zum Lifetime-Imperium-Pass. Inhalte: saisonale Cosmetics, +1 zusaetzlicher Erbstueck-Slot fuer die Saison, doppelte SP-Belohnungen, exklusive Workshop-Skins. Bindet an das bereits bestehende `SeasonalEvent`- und `BattlePass`-System an. **Nicht Teil dieses Plans, aber als Option fuer eine zukuenftige Phase notiert.**

### 10.5 Migration des bestehenden Kauf-Status

- Spieler, die heute den 4,99-EUR-Premium-Kauf bereits haben, bekommen **automatisch** den vollen Imperium-Pass (Detection via `IPurchaseService.HasPremium`).
- Keine zusaetzliche Zahlung, kein Re-Entitlement noetig.
- Marketing-Push beim ersten Launch nach Phase 1: "Dein Premium ist jetzt der Imperium-Pass — hier sind deine neuen Vorteile."

---

## 11. Out-of-Scope (bewusst nicht eingebaut)

- **Echte Echtzeit-Boerse mit Player-Auktionen.** Zu komplex, zu viel Anti-Cheat-Aufwand. Markt bleibt deterministisch.
- **Material-Trading zwischen Spielern.** Ausser via Gilden-Pool (Spende). Verhindert RMT-Risiko.
- **Auto-Lieferketten-Optimierer.** Bewusst Spieler-Entscheidung. AI-Optimizer wuerde das Kern-Gameplay killen.
- **Material-Quality-Sub-Levels.** Heute haben Items keine Qualitaetsstufen — das bleibt so. Risk-Reward-Strategie deckt das funktional ab.
- **Crafting-MiniGame.** Bewusst passiv (Idle-Game-Identitaet). MiniGames bleiben bei Auftraegen.

---

## 12. Naechste Schritte

1. **Stakeholder-Review** dieses Plans (Robert).
2. **Spike:** Cross-Workshop-Recipe-Validation in `CraftingService` durchspielen, Performance-Profil.
3. **Phase 1 Detail-Spec:** File-by-File Implementation-Plan + Test-Strategie.
4. **UI-Mockups:** Lager-Sub-Tab (Slot-Grid), Markt-Heatmap.
5. **Asset-Pipeline:** 22 neue Material-Icons in Asset-Backlog.
6. **Tickets in Linear/Asana** (falls genutzt) pro Phase.

---

## 13. Anhang: Beispiel-Datenstruktur

### 13.1 Erweitertes CraftingRecipe-Beispiel (Phase 1)

```csharp
new() {
    Id = "r_furniture",
    NameKey = "CraftFurniture",
    WorkshopType = WorkshopType.Carpenter,
    RequiredWorkshopLevel = 150,
    Tier = 2,
    InputProducts = new() {
        { "planks", 3 },         // eigene T1
        { "paint_mix", 1 }       // Cross-Workshop: Painter T1
    },
    OutputProductId = "furniture",
    DurationSeconds = 120
}
```

### 13.2 Beispiel Tier-4-Rezept

```csharp
new() {
    Id = "r_villa",
    NameKey = "CraftVilla",
    WorkshopType = WorkshopType.GeneralContractor,
    RequiredWorkshopLevel = 500,
    Tier = 4,
    InputProducts = new() {
        { "luxury_furniture", 5 },        // Carpenter T3
        { "smart_home", 3 },              // Electrician T3
        { "roof_structure", 2 },          // Roofer T3
        { "artwork", 1 }                  // Painter T3
    },
    OutputProductId = "villa",
    OutputCount = 1,
    DurationSeconds = 1800                // 30 Minuten
}

new() {
    Id = "villa",
    NameKey = "ProductVilla",
    Tier = 4,
    BaseValue = 2_500_000m,
    IsHeirloomEligible = true             // NEU in Phase 4
}
```

### 13.3 Beispiel Material-Anforderung in Order

```csharp
public class Order {
    // ... bestehende Felder ...

    [JsonPropertyName("materialRequirement")]
    public Dictionary<string, int>? MaterialRequirement { get; set; }

    [JsonPropertyName("materialBonusMultiplier")]
    public double MaterialBonusMultiplier { get; set; } = 0.0;

    [JsonIgnore]
    public bool HasMaterialOffer => MaterialRequirement is { Count: > 0 };
}
```

---

## 14. Glossar

| Begriff | Bedeutung |
|---------|-----------|
| Material | Crafting-Produkt (Tier 1–4) |
| Lager | `CraftingInventory` mit Slot- und Stack-Limits |
| Slot | Ein Lager-Platz fuer einen Material-Typ |
| Stack | Anzahl Items im selben Slot |
| Lieferkette | Cross-Workshop-Produktionsabhaengigkeit |
| Material-Anforderung | Optionale Item-Liste, die ein Auftrag fuer Bonus-Reward verlangt |
| Erbstueck | Tier-4-Item, das Prestige ueberlebt (bis zu 3 pro Run) |
| Permanent-Erbstueck | Erbstueck, das ueber Ascension permanent wird |
| Mega-Projekt | Gilden-Quest mit Material-Sammlung ueber Wochen |
| Material-Affinitaet | Worker-Eigenschaft (Holz/Metall/Stein/Kunst/Tech), +20% Crafting-Speed bei Match |
| Markt | Material-Boerse mit Tagespreis-Schwankungen |

---

**Ende des Plans.**
