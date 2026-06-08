# Progression & Balancing — Langzeit-Modell (max. 3 Prestige, Monate Spielzeit)

> Vertieft und **ersetzt** die Progressions-/Prestige-Beschreibung im GDD ([3D_IDLE_GAME_PLAN.md §7](3D_IDLE_GAME_PLAN.md)).
> **Vorgabe:** Prestige ist auf **maximal 3** gedeckelt. Das Spiel muss trotzdem **Monate** tragen.
> **Konsequenz (Kern-Idee):** Prestige ist **nicht länger der Haupt-Treadmill**, sondern ein seltenes,
> zeremonielles **Akt-Finale**. Die Langzeit-Motivation kommt aus **parallelen Progressions-Vektoren**,
> einem **permanenten Meisterschafts-Rückgrat** und einem **Soft-Infinite-Endgame** — getragen von Live-Ops.

---

## 1. Warum gedeckeltes Prestige (3×) hier besser ist

Unendliches Prestige („reset & re-climb forever") ist der billige Longevity-Trick vieler Idle-Games — aber
es macht jede Stadt austauschbar und stumpft ab. Mit **3 Prestiges** gewinnen wir:

1. **Authored Arc:** 4 handgebaute, distinkte Städte (Dorf → Metropole) statt repetitiver Klon-Resets.
2. **Zeremonielles Prestige:** seltener = bedeutsamer. Jedes Prestige ist ein Cinematic + Hans-Story-Beat + sichtbarer Welt-Sprung.
3. **Gesündere Retention:** Langzeit liegt auf **dauerhaften** Systemen (Meisterschaft, Sammlung, Live-Ops) statt auf Reset-Grind — das hält Spieler über Monate, ohne sie zu „resetten".
4. **Klarere Monetarisierung:** kein „Pay-to-skip-the-infinite-loop". IAP beschleunigt *innerhalb* eines Akts, Cosmetics & Endgame-Ressource — fairer und lesbarer.

**Das „Unendliche", das Idle-Spieler erwarten, liefert der Endgame-Meistergrad-Loop (§5) — bewusst KEIN 4. Prestige.**

---

## 2. Die 4 Akte / Städte (3 Prestige-Übergänge)

| Akt | Stadt | Charakter | Ziel-Spielzeit bis Prestige | kumulativ |
|-----|-------|-----------|------------------------------|-----------|
| **1** | **Hansstadt** (Dorf) | Erbe der Garage, erstes Imperium | ~5–7 Tage | ~1 Woche |
| **2** | **Kreisstadt** | Kleinstadt, mehr Distrikte | ~2 Wochen | ~3 Wochen |
| **3** | **Großstadt** | größter Aufbau-Akt | ~5–7 Wochen | ~8–10 Wochen |
| **4** | **Metropole** *(Endstadt)* | Endgame-Heimat, **kein** weiteres Prestige | — (Soft-Infinite) | **Monate 3 → ∞** |

- **3 Prestige-Übergänge:** Hansstadt→Kreisstadt (P1), Kreisstadt→Großstadt (P2), Großstadt→Metropole (P3).
- **Akte werden länger, nicht kürzer:** jede Stadt hat mehr Distrikte/Wahrzeichen, tiefere Stations-Ziele und höhere Stern-Schwellen. Der permanente Prestige-Multiplikator macht den *Wieder*aufstieg schneller, aber die Stadt ist auch **größer** → netto neue Spielzeit.
- **Nach P3** bleibt der Spieler dauerhaft in der Metropole; Progression läuft über §4–§6 weiter.

> Reihenfolge gesetzt, Namen provisorisch (vorher waren 5 Städte/2 Prestige-Slots mehr geplant — jetzt 4 Städte / 3 Prestige).

---

## 3. Progressions-Vektoren — der Überblick

Longevity entsteht durch **gestaffelte, parallele** Vektoren mit unterschiedlichen Zeit-Skalen. Drei Klassen:

| Klasse | Vektor | Reset bei Prestige? | Zeit-Skala | Funktion |
|--------|--------|---------------------|-----------|----------|
| **Akt-intern** | Stationslevel + Meilensteine | ✅ ja | Stunden→Tage | der aktive Sekunden-Loop |
| | Distrikt-/Wahrzeichen-Sanierung | ✅ ja | Tage | Akt-Hauptquest, Welt-Feedback |
| | Stern-Rating 1–5★ | ✅ ja | Akt-Dauer | Gate + Prestige-Freigabe |
| | Arbeiter (Hire + Stufen) | ✅ ja | Tage | Automatisierung |
| **Permanent** (überlebt alle Prestiges) | **Meisterschafts-Track** | ❌ nie | Wochen→Monate | **das Langzeit-Rückgrat** (§4) |
| | Master-Tools (12) | ❌ nie | Wochen→Monate | Sammlung, permanente Boni |
| | Imperium-Marken-Perkboard | ❌ nie | ganzes Spiel | Meta-Boni, langsam gefüllt |
| | Achievements + Cosmetics-Sammlung | ❌ nie | Monate | Vervollständigung |
| **Endgame** (nach P3) | **Meistergrade** (Soft-Infinite) | ❌ nie | Monate→∞ | der „Spiel-für-immer"-Schwanz (§5) |
| **Laufend** | Daily / Weekly / Saison / Leaderboards | — | täglich/wöchentlich | Engagement-Takt über Monate |

Die Kunst: zu **jedem** Zeitpunkt liegt mindestens ein lohnendes Sub-Ziel **< 1 Session** entfernt (Station-Meilenstein,
Master-Tool-Bedingung, Meisterschafts-Level, Daily) — auch wenn das nächste Prestige noch Wochen weg ist.

---

## 4. Das permanente Rückgrat: Meisterschafts-Track

**Das wichtigste Longevity-System** — eine **kontoweite, nie zurückgesetzte** Meisterschaftsstufe, die den Spieler
zwischen den seltenen Prestiges durchgehend „im Plus" hält.

- **Quelle:** ein Bruchteil **aller** Aktivität fließt als Meisterschafts-XP (Aufträge bedient, Distrikte saniert,
  Stations-Meilensteine, jedes Prestige als großer Schub).
- **Kurve:** `XP(Level N) = base × 1.15^N` → ~**100+ Level über Monate** erreichbar, jedes Level langsamer.
- **Belohnung je Level:** kleiner **permanenter globaler Income-Bonus** (z. B. +1–2 %) + alle paar Level ein Perk-Slot/Marke.
- **Soft-Cap:** der globale Meisterschafts-Bonus läuft durch den **Log2-Soft-Cap** (`IncomeFormulas.ApplySoftCap`),
  damit späte Level nicht explodieren, aber **nie** ganz wertlos werden.
- **Rolle:** überbrückt die Lücken zwischen Prestiges — Fortschritt fühlt sich **nie** gestallt an, selbst tief im längsten Akt.

> Das ist die idle-arcade-taugliche, **leichte** Wiederkehr der Original-„Eternal Mastery" — **als permanenter Track, NICHT als Prestige** (§15.5).

---

## 5. Endgame: Meistergrade (Soft-Infinite, nach dem 3. Prestige)

Nach P3 (in der Metropole) gibt es **kein** weiteres Stadt-Prestige. Stattdessen der **Meistergrad-Loop** — der
unendliche, aber langsam-diminishende Schwanz, den Idle-Spieler erwarten:

- Hat die Metropole ihr 5★ erreicht, schaltet der Spieler **Meisterzyklen** frei: ein Metropol-Distrikt wird
  „auf Meisterniveau optimiert" → **+1 Meistergrad**.
- **Ressource:** ein dedizierter Endgame-Stoff **„Imperium-Renommee"**, der langsam aus Spitzen-Einkommen/Aufträgen akkumuliert.
- **Kosten:** `Renommee(Grad R) = base × 1.5^R` → Grade werden **geometrisch langsamer** → Monate an inkrementellen Zielen.
- **Belohnung je Grad:** kleiner permanenter Global-Bonus (Income/Offline-Cap), **Soft-Cap**-gedämpft → nie trivialisierend, nie nutzlos.
- **Kein Welt-Reset:** Meistergrade verändern die Stadt **nicht** zurück — es ist ein **persönlicher** Mastery-Grind, klar abgegrenzt vom (gedeckelten) Prestige.

**Wichtig fürs Pacing:** der **erste** Meistergrad muss **kurz nach P3** erreichbar sein (Endgame-Hook), danach gestaffelte Verlangsamung.

---

## 6. Weitere permanente Vektoren

- **Master-Tools (12):** Freischalt-Bedingungen bewusst **über alle Akte gestreut**; die letzten 3–4 sind wochentief
  (z. B. „Station X Level 100", „alle Distrikte Stadt 3 saniert", „Meistergrad 10"). Pacet die Sammlung über Monate.
- **Imperium-Marken-Perkboard:** Marken aus den 3 Prestiges **+** aus Meilensteinen (Sanierungen, Achievements, Meistergrade).
  ~6–10 Perks × mehrere Stufen (Start-Geld nach Prestige, Offline-Cap, Global-Tempo, Auto-Collect-Radius, Worker-Basistempo …).
  Füllt sich über das ganze Spiel langsam.
- **Achievements (95+) & Cosmetics-Sammlung:** Vervollständigungs-Ziele für Komplettisten, monate-tragend, monetarisierungs-freundlich.

---

## 7. Balancing-Mathematik (Kurven & Formeln)

Alles über `BalancingConfig` (ScriptableObject) + **Remote-Config** (Live-Tuning P3/P4) — **kein Hardcoding**.

| Größe | Formel / Ansatz | Tuning-Ziel |
|-------|------------------|-------------|
| Einkommen/s | `Basis × Π(Multiplikatoren) → ApplySoftCap (Log2)` | lesbare Zahlen, kein Explodieren |
| Stations-Upgradekosten | `cost(L) = base × growth^L`, growth ≈ 1.07–1.12 | „nur-noch-eins" früh, Tiefe spät |
| Stations-Meilensteine | bei L = 10/25/50/100/200: Output ×2 (Sprung) | Chase-Ziele über Wochen |
| Stern-Schwelle je Akt | steigt pro Stadt (mehr Distrikte/Volumen nötig) | Akte werden länger |
| Prestige-Multiplikator | permanent, **3 Stufen**: ~×3 / ×4 / ×5 (kumulativ ~×60) | nächster Akt: Re-Climb-Start ~30–50 % der Vorakt-Zeit |
| Prestige-Marken | fester Schub je Prestige (3×) + Meilenstein-Marken | Perkboard füllt über das Spiel |
| Meisterschafts-XP | `XP(N) = base × 1.15^N`, Bonus/Level +1–2 % (Soft-Cap) | 100+ Level über Monate |
| Meistergrad-Kosten | `Renommee(R) = base × 1.5^R` | Soft-Infinite, geometrisch langsamer |
| Offline | Staffel 0.80/0.35/0.15/0.05 (`OfflineProgressFormulas`), Cap via Perkboard/Premium | Rückkehr lohnt, kein AFK-Win |

**Schlüssel-Insight:** Prestige liefert einen **starken, aber endlichen** Multiplikator-Stack (×~60 gesamt). Der **unendliche**
Anteil kommt aus **additiv-soft-capped** Quellen (Meisterschaft, Meistergrade, Master-Tools) — die wachsen ewig weiter,
aber gedämpft, sodass das Late-Game **Monate** sinnvoll bleibt, ohne dass Zahlen unkontrolliert eskalieren.

---

## 8. Zeit-Budget — was den Spieler wann antreibt

| Zeitpunkt | Primär-Treiber | Sekundär |
|-----------|----------------|----------|
| **Tag 1** | Onboarding, erste Werkstätten, **erster Arbeiter (Automatisierungs-Aha)** | Daily, erste Master-Tool-Bedingung |
| **Woche 1** | Hansstadt 5★ → **Prestige 1** → Kreisstadt | Meisterschaft Lvl ~5–10, erste Marken |
| **Woche 2–3** | Kreisstadt-Aufbau → **Prestige 2** → Großstadt | mehr Distrikte, Perkboard wächst |
| **Woche 4–8** | Großstadt (größter Akt), tiefe Stationslevel + Meilensteine | Master-Tools 6–9, Saison-Event |
| **~Woche 8–10** | **Prestige 3** → Metropole (Endstadt) | Meisterschaft ~Lvl 40–60 |
| **Monat 3–4** | **Meistergrade starten**, letzte Master-Tools, Metropol-Sanierung | Saisons, Daily/Weekly |
| **Monat 5–6+** | **Soft-Infinite Meistergrade** + Meisterschafts-Langschwanz | Leaderboards, Cosmetics/Achievements-Komplettierung, Live-Ops |

Kein Hard-Ende: ab Monat 3 tragen Meistergrade + Meisterschaft + saisonale Live-Ops + Sammlung beliebig lange.

---

## 9. Monetarisierungs-Konsequenz

- **Längere engagierte Lifetime** → mehr Ad-/IAP-Touchpoints über Zeit (besseres LTV als ein „in 2 Wochen durch"-Idle).
- **Premium-Auto-Collect** wird in einem Monate-langen Spiel **noch** wertvoller (Kernkaufgrund).
- **Kein Prestige-Skip-Verkauf:** IAP beschleunigt **innerhalb** eines Akts, kauft Cosmetics oder **Imperium-Renommee** (Endgame) — lesbar & fair.
- **BattlePass/Saison** (P2/P3) passt perfekt auf die lange Laufzeit (wiederkehrender 30-Tage-Track).
- **Whale-Pfad:** Endgame-Renommee + Cosmetics-Komplettierung geben Vielausgebern dauerhaft Ziele, ohne Balance zu brechen (Soft-Cap).

---

## 10. Risiken & Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|--------|----------------|
| **Mid-Game-Wall** (zwischen Prestiges fühlt sich Grind zäh an) | Meisterschafts-Track + Master-Tool-Chases + Daily/Weekly halten Mikro-Fortschritt; immer ein Sub-Ziel < 1 Session entfernt |
| **Endgame-Leere** nach P3 | erster Meistergrad **kurz** nach P3 erreichbar (Hook); Saisons + Leaderboards + Sammlung als Dauer-Loop |
| **Prestige fühlt sich zu fern an** | klarer Fortschrittsbalken zum nächsten Prestige + Zwischen-Stern-Meilensteine; Hans-Foreshadowing |
| **Prestige zu stark/schwach** | Multiplikator so tunen, dass Re-Climb-Start ~30–50 % der Vorakt-Zeit kostet, dann durch größere Stadt verlängert |
| **Zahlen explodieren** | Log2-Soft-Cap auf aggregierte Multiplikatoren (`IncomeFormulas`); unendliche Quellen additiv + gedämpft |
| **„Nur 3 Prestige = wenig Inhalt"-Eindruck** | Kommunikation: 4 distinkte Städte + Endgame-Mastery; Tiefe statt Wiederholung |

---

## 11. Tunable-Parameter (BalancingConfig + Remote-Config)

Akt-Stern-Schwellen · Stations-`growth` & Meilenstein-Sprünge · die **3** Prestige-Multiplikatoren & Marken-Schübe ·
Meisterschafts-XP-`base`/Exponent/Bonus-pro-Level · Meistergrad-`base`/`1.5^R`/Bonus-pro-Grad · Renommee-Akkumulationsrate ·
Offline-Cap/-Rate · Daily/Weekly/Saison-Rewards. **Remote-Config** erlaubt Live-Balancing in Beta/Launch ohne App-Update.

---

## 12. Umsetzungs-Konsequenzen für die Phasen

| Phase | Was ändert sich ggü. den Phasen-Specs |
|-------|----------------------------------------|
| **P1** | genau **1** Prestige (Hansstadt→Kreisstadt) — als erstes von dreien; Meisterschafts-Track **als Stub** schon mitdenken (Save-Slice) |
| **P2** | **4** Städte (nicht 5), volle Sanierung; **Meisterschafts-Track**, **Master-Tools**, **Perkboard** ausbauen |
| **P3** | **Meistergrad-Endgame** + Renommee-Ressource; Remote-Config für Kurven-Live-Tuning; Leaderboards (Meistergrad/Income) |
| **P4** | KPI-Balancing der Kurven (Prestige-Timing, Meisterschaft-Steigung, Meistergrad-Verlangsamung) gegen echte Retention-Daten |

---

## Verweise
- GDD (Progression §7, Stadt §5, Live-Ops §10, Weggelassenes §15.5): [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)
- Phasen: [P1](P1_VERTICAL_SLICE.md) · [P2](P2_CONTENT.md) · [P3](P3_SOCIAL_BETA.md) · [P4](P4_POLISH_CUTOVER.md)
- Wiederverwendbare Formeln (Income-Soft-Cap, Offline): [DOMAIN_3D_PLAN.md](DOMAIN_3D_PLAN.md) · Werte-Referenz: [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md)
- Tech-Conventions: [CLAUDE.md](CLAUDE.md)
