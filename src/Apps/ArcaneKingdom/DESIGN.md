# ArcaneKingdom — Game Design Document (GDD)

> **Version 6.0** | Stand: 2026-05-25 | Plattform: Android | Engine: Unity 6 (6000.4.x)
> Quelle: Designplan v4 + Story v4 + Kartenliste v4 + Skills v4 + Oekosystem v4 + Art Style Guide v4 + Implementierungsplan v4
> (DOCX-Dokumente unter `F:\AI\ComfyUI_windows_portable\ComfyUI\output\eva\Spiele Ideen Ordner\Ideen\`)

Dieses Dokument ist die **autoritative Single-Source-of-Truth** für das Game Design. Es ersetzt v5.4 vollständig.
v6.0 ist die Konsolidierung der v4-Designdokumente (5 Rassen, 6 Elemente Doppel-Dreieck, 131 Karten, 10 Welten, vollständige Story-Mythologie, Karten-Persönlichkeit, Sternkarten-System, Prestige-System).

---

## Inhaltsverzeichnis

0. Game Director Summary
1. Spielphilosophie & Kernprinzipien
2. Die 5 Rassen (Ritter, Götter, Elfen, Tiergeister, Dämonen)
3. Das 6-Elemente-System (Doppel-Dreieck)
4. Welten von Aethera (10 Welten + Mythologie)
5. Karten-Pyramide & Seltenheitsstufen (131 Karten)
6. Fusions-Crafting-System
7. Karten-Persönlichkeit (Dialog-Lines, Synergien, Rivalen)
8. Karten-Level-System (LV 0–15 + Letzter Wille)
9. Helden-Passiv-Skills (1 pro Rasse)
10. Kampfsystem (Mana, Rundenwarten, Element-Matchup, Boss-Phasen)
11. Auto-Battle-Progression (LV 10/20/30/50)
12. Story-Bogen (Erinnerungs-Fragmente, NPCs, Twist)
13. Karten-Ökosystem (Event, Premium, Sternkarten-Tempel, Prestige, Saison-Pass)
14. Prestige-System für Welten
15. Sternkarten-System (Login + Tempel)
16. Arena (PvP), Gilden, Klan-Matches, Dieb-Event
17. Lokalisierung (DE primär, EN sekundär)
18. Datenmodell & Technologie (Unity 6, Firebase, Photon)
19. Entwicklungs-Zeitplan (24 Monate, 7 Phasen)
20. Risiken & MVP-Definition
21. Glossar
22. Änderungslog v5.4 → v6.0

---

## 0. Game Director Summary

**Pitch:** Aethera ist im Sterben. Sechs elementare Säulen — Feuer, Wasser, Natur, Erde, Licht, Dunkel — werden von einer Verderbnis korrumpiert, ausgelöst von Nythragor, dem siebten verbannten Gott. Der Spieler erwacht als namenloser Rufer ohne Erinnerung im sterbenden Elderwald. Er sammelt Seelen-Siegel (Karten), baut Decks, reinigt die sechs Säulen durch zehn Welten — und entdeckt unterwegs eine schmerzhafte Wahrheit über sich selbst.

**Genre:** Mobiles Sammelkartenspiel + RPG-Progression + soziales Endgame
**Plattform:** Android (Phase 1) — iOS (Phase 2 ab Monat 26+)
**Engine:** Unity 6 (6000.4.8f1) + C# (.NET Standard 2.1)
**Backend:** Firebase Auth/Realtime DB/Cloud Functions + Photon (Chat, Dieb-Sync, Phase-2-Live-PvP)
**Sprache:** Deutsch primär (Markt-DACH), Englisch zum Launch
**Monetarisierung:** Free-to-Play. Kein Pay-to-Win. Premium-Shop (Diamanten), Saison-Pass, Notfall-Kauf für verpasste Event-Karten.

**Drei Designsäulen:**
1. **Qualität vor Quantität** — 131 Karten zum Launch, jede mit eigener Persönlichkeit (Dialog-Lines, Synergien, Rivalen). Keine Füllkarten.
2. **Faire Progression** — 6★ Mythisch ist durch aufwendiges Crafting UND Events erreichbar. Kein Content nur durch Echtgeld.
3. **Lebendige Welt** — 10 Welten mit eigener Story, elementarer Säule, Story-Boss und Erinnerungs-Fragment. Twist in Welt 8.

**Launch-KPIs:**

| KPI | Ziel |
|-----|------|
| D1-Retention | ≥ 45 % |
| D7-Retention | ≥ 22 % |
| D30-Retention | ≥ 10 % |
| Conversion (zahlende Spieler) | ≥ 3 % |
| ARPDAU | ≥ 0,12 USD |
| Crash-Rate | < 0,5 % |

---

## 1. Spielphilosophie & Kernprinzipien

| Prinzip | Bedeutung |
|---------|-----------|
| **Start bei Level 0** | Alle Spieler beginnen mit den gleichen 5 Starter-Karten ihrer gewählten Rasse. Keine Pay-Boost-Starter. |
| **Qualität vor Quantität** | 131 sorgfältig designte Karten statt hunderter Füllkarten. Jede Karte hat ATK/HP/Skills und eine klare Rolle. |
| **Faire Progression** | 6★-Mythisch durch Crafting UND Events erreichbar — kein Echtgeld-Lock. |
| **Crafting mit Bedeutung** | Niedrigere Karten behalten dauerhaft Wert als Fusions-Material. |
| **Taktische Tiefe durch Elemente** | 6 Elemente, zwei Dreiecke. Decks müssen je nach Welt angepasst werden. |
| **2D-Visueller Stil** | AI-generierte Karten-Artworks (Mid-Journey/Stable-Diffusion-Pipeline) + Parallax-Kurzszenen für hochwertige Optik mit kleinem Team. |
| **Karten haben Persönlichkeit** | Dialog-Lines beim Einsetzen, Synergie-Boni, Rivalen-Reaktionen. |
| **Auto-Battle als Belohnung** | LV 1–9 manuell (Lerneffekt), LV 10/20/30/50 Speed-Stufen freischalten. |
| **Prestige für Endgame** | Welten können auf Prestige I–IV aufgewertet werden — höhere Schwierigkeit, bessere Drops, exklusive Karten. |

**Hauptunterschied zu Lies of Astaroth (LoA) als Inspirationsquelle:** Stärkere Rassen-Identität (5 statt 8 austauschbare), faires Pyramiden-Karten-System statt Gacha, progressives Auto-Battle, Karten-Persönlichkeit.

---

## 2. Die 5 Rassen

ArcaneKingdom bietet **5 spielbare Rassen** (Designplan v4 Kap. 2). Götter sind eine Premium-Rasse — sie sind erst ab 4★ verfügbar und ausschließlich durch Fusion erhältlich.

| Rasse | Farbe | Stärken | Schwächen | Karten 1★–6★ |
|-------|-------|---------|-----------|---------------|
| **Ritter / Helden** | Gold/Orange | Tank/Support — Schilde, Heilung, Team-Buffs. Klassische Krieger, Paladine, Bogenschützen. Ideal für Anfänger. | Niedrige ATK-Spitzenwerte, langsame Rundenwarten | 31 (10/8/6/4/2/1) |
| **Götter** | Weiß/Celestial | Mächtig aber teuer — starke Einzel-Skills, Buffs/Debuffs auf gesamtes Feld. Prestige-Karten. | Sehr hohe COST, nur ab 4★, schwer zu craften | 7 (—/—/—/4/2/1) |
| **Elfen** | Grün/Türkis | Speed/Kontrolle — schnelle Angriffe, Schlaf/Verlangsamung, Eigenheilung. | Geringe Rüstung, anfällig für AoE | 31 (10/8/6/4/2/1) |
| **Tiergeister / Waldgeister** | Braun/Smaragd | Rudel-Synergien — je mehr Tiergeister im Deck, desto stärker. Beschwörungen, Naturmagie. | Einzeln schwach, brauchen Synergien | 31 (10/8/6/4/2/1) |
| **Dämonen** | Lila/Schwarz/Rot | Hohe ATK — Lebensraub, Gift/Fluch, riskante Mechaniken. | Geringe HP, schlechte Heilung, selbstzerstörerische Skills | 31 (10/8/6/4/2/1) |

**Gesamt:** 131 Karten (124 Standard + 7 Götter).

### 2.1 Helden-Passiv-Skills

Jeder Spieler wählt zu Beginn eine Rasse. Diese bestimmt den **Held-Passiv-Skill**, der für ALLE Kämpfe gilt:

| Rasse | Passiv | Effekt | `HeroFaehigkeitsTyp` |
|-------|--------|--------|----------------------|
| Ritter/Helden | Königliche Aura | Eigene Karten starten mit +5 % HP | `KoeniglicheAura` |
| Götter | Göttlicher Segen | Einmal pro Kampf: Verhindert den Tod einer eigenen Karte (1 HP übrig) | `GoettlicherSegen` |
| Elfen | Waldläufer | Erste eigene Karte jeder Runde kostet 0 COST | `Waldlaeufer` |
| Tiergeister | Rudelbund | +3 % ATK für jede Tiergeist-Karte im Deck (stapelbar) | `Rudelbund` |
| Dämonen | Lebensraub-Aura | 20 % aller Karten-Schäden heilen Helden-HP | `LebensraubAura` |

**Wichtig:** Der Spieler kann Karten aller Rassen im Deck verwenden, unabhängig von seiner gewählten Rasse. Der Helden-Passiv-Skill wirkt aber immer mit der gewählten Rasse.

Daten: `Unity/Assets/_Project/Resources/Data/heroes.json`. Domain: `Domain/Hero/HeroDefinition.cs` + `Domain/Hero/HeroFaehigkeitsTyp.cs`.

---

## 3. Das 6-Elemente-System (Doppel-Dreieck)

ArcaneKingdom verwendet **6 Elemente**, organisiert in zwei Dreiecke (Designplan v4 Kap. 3). Erhöht Deckbuilding-Tiefe — Spieler brauchen für verschiedene Welten unterschiedliche Element-Zusammenstellungen.

### 3.1 Physisches Dreieck

```
Feuer  →  stark gegen  →  Natur
Natur  →  stark gegen  →  Wasser
Wasser →  stark gegen  →  Feuer
```

### 3.2 Magisches Dreieck

```
Licht  →  stark gegen  →  Dunkel
Dunkel →  stark gegen  →  Erde
Erde   →  stark gegen  →  Licht
```

**Erklärung:** Dunkelheit zersetzt die Erde (Verderbnis, Verfall), Erde blockiert das Licht (Verschüttung, Finsternis), Licht vertreibt die Dunkelheit.

### 3.3 Effektivitäts-Multiplikatoren

| Bezug | Multiplikator | Bemerkung |
|-------|--------------|-----------|
| Stark gegen (innerhalb Dreieck) | **1.10x** | +10 % Schaden + Element-Spezialeffekt |
| Schwach gegen (innerhalb Dreieck) | **0.90x** | −10 % Schaden |
| Karten verschiedener Dreiecke | **1.00x** | Neutral |

Implementierung: `Domain/Battle/ElementMatchup.cs::GetMultiplier(Element attacker, Element defender)`.

### 3.4 Element-Spezialeffekte

| Element | Spezialeffekt |
|---------|---------------|
| Feuer | Verbrennung (DoT). Ignoriert Schilde zu 15 %. |
| Wasser | Einfrierung-Chance bei Angriffen (überspringt 1 Runde). |
| Natur | Selbstheilung über Zeit. |
| Erde | Steinpanzer (Schadensreduktion), Erdbeben (AoE-Betäubung), Verwurzelung (Bewegungs-Lock). |
| Dunkel | Verstärkt Gift/Fluch-Effekte. |
| Licht | Hebt Status-Effekte auf, Heilung. |

### 3.5 Welt-Element-Zuordnung

Jede Welt hat ein dominantes Element (siehe Kap. 4). Gegner nutzen vorwiegend dieses Element. Der Spieler muss sein Deck anpassen.

---

## 4. Welten von Aethera

### 4.1 Welt-Mythologie

**Schöpfungsgeschichte:** Am Anfang sprach der Allschöpfer **Aetherius** ein einziges Wort — und die sechs Elemente wurden geboren. Er schuf fünf Völker um die Elemente zu hüten (Ritter, Elfen, Tiergeister, Dämonen). Götter — seine Kinder — wandelten unter den Völkern und schützten sie. Vor tausend Jahren verstummten die Götter — und die Verderbnis begann zu sickern.

**Antagonist:** **Nythragor, der Kettenbrecher** — der siebte verbannte Gott, einst Gott des Wandels. Er glaubte, wahre Perfektion käme nur durch Zerstörung und Neuerschaffung. Verbannt in die Leere, wurde er das Gefäß der Verderbnis. Er hat die anderen Götter zum Schweigen gebracht — eingesperrt in den sechs Säulen.

**Die sechs Säulen halten Aethera im Gleichgewicht:**

| Säule | Element | Welt | Zustand zu Spielbeginn |
|-------|---------|------|------------------------|
| Lebensbaum | Natur | Elderwald (W1) | Verwelkend |
| Urkern | Erde | Titanengrat (W7) | Zerbrochen |
| Flammenherz | Feuer | Vulkanhort (W3) | Korrodiert |
| Gezeitenkern | Wasser | Abysstiefe (W8) | Eingefroren |
| Sternenfeuer | Licht | Sturmzitadelle (W6) | Verblassend |
| Schattenriss | Dunkel | Schattenlande (W5) | Aufgerissen |

Wenn alle sechs fallen, versinkt Aethera in Chaos.

Daten: `Unity/Assets/_Project/Resources/Data/story_fragments.json`.

### 4.2 Welten-Übersicht (10 Welten)

| # | Welt | Element | Boss | Empfohlenes Spieler-Level | Säule | Mentor-NPC |
|---|------|---------|------|--------------------------|-------|-----------|
| 1 | Elderwald | Natur | Uralter Baumwächter | 1 | Lebensbaum | Lumis (Lichtgeist) |
| 2 | Sandreich | Erde | Erdtitan Gorath | 8 | (Ausläufer Urkern) | Marschall Aldor (Ritter) |
| 3 | Vulkanhort | Feuer | Höllenfürst Malphas | 18 | Flammenherz | Lilith (Dämonenkönigin) |
| 4 | Frostgipfel | Wasser | Wasserdrache Tidal | 30 | (Ausläufer Gezeiten) | Mondpriesterin Lira (Elfe) |
| 5 | Schattenlande | Dunkel | Dämonenkönigin Lilith | 50 | Schattenriss | Grimmfang (Tiergeist) |
| 6 | Sturmzitadelle | Licht | Sturmadler Aethon | 65 | Sternenfeuer | Königin Sera |
| 7 | Titanengrat | Erde | Kristalldrache Diamara | 80 | Urkern | General Dorn |
| 8 | Abysstiefe | Wasser | Jormungand (Weltenschlange) | 95 | Gezeitenkern | Lumis (Twist-Revelation!) |
| 9 | Galaxy Wald | Licht (alle) | Selene (oder Schatten-Doppelgänger) | 110 | (Dimensionaler Raum) | Aetherius-Geist |
| 10 | Drachenfeste | Feuer (alle) | **Aetherius** (oder Nythragor-Erlöst-Ende) | 130 | Finale | Nythragor selbst |

Welt-Daten: `Unity/Assets/_Project/Resources/Data/worlds.json`.

### 4.3 Welt-Aufbau (jede Welt: 10 Nodes)

| Node | Typ | Belohnung |
|------|-----|-----------|
| 1–4 | Normal | 1–2★ Karten + Common/Rare Scraps + Gold |
| 5 | **Mini-Boss** | 2–3★ Karten + Material-Drop (Säulen-Splitter) |
| 6–9 | Normal | 2–3★ Karten + Scraps + Gold |
| 10 | **Welt-Boss** | 3–4★ Karten + Story-Drop + Erinnerungs-Fragment |

### 4.4 Sterne pro Node

| Sterne | Schwierigkeit | Energie-Kosten | Erst-Belohnung |
|--------|---------------|----------------|----------------|
| 1 | Classic | 1 | + Basis-Gold + EXP |
| 2 | Amateur (+50 % HP) | 1 | + mehr Gold/EXP |
| 3 | Profi (Skills aktiv) | 2 | + Karten-Drops + Scraps |
| 4 | Gott (Elite-Deck, Phasen-Boss) | 3 | + garantierte Epic/Legendary |

Voraussetzung für Prestige I einer Welt: ALLE Level auf 3 Sternen.

---

## 5. Karten-Pyramide & Seltenheitsstufen

ArcaneKingdom verwendet eine **Pyramiden-Struktur** mit 6 Seltenheitsstufen (Designplan v4 Kap. 4):

### 5.1 Seltenheits-Übersicht

| Stufe | Sterne | Rahmen | Basis-ATK | Basis-HP | Quelle |
|-------|--------|--------|-----------|----------|--------|
| Gewöhnlich | ★ | Grau/Eisen | 100–250 | 300–700 | Starter, Story-Drops, günstiges Crafting |
| Ungewöhnlich | ★★ | Grün/Bronze | 200–400 | 600–1100 | Story (ab W2), Crafting Tier 1 |
| Selten | ★★★ | Blau/Silber | 300–550 | 900–1600 | Crafting Tier 2, Arena, Bosse |
| Epic | ★★★★ | Lila/Amethyst | 450–750 | 1200–2000 | Crafting Tier 3 (LV 50+), Klan-Matches |
| Legendär | ★★★★★ | Gold/leuchtend | 650–1200 | 1700–2500 | Legendary Crafting (LV 70+), Saison-Events |
| Mythisch | ★★★★★★ | Celestial/animiert | 900–1500 | 2200–3200 | Crafting (5x5★ + Mythischer Kern) ODER seltene Events |

### 5.2 Karten-Verteilung pro Rasse

| Rasse | 1★ | 2★ | 3★ | 4★ | 5★ | 6★ | Total |
|-------|----|----|----|----|----|----|-------|
| Ritter | 10 | 8 | 6 | 4 | 2 | 1 | 31 |
| Elfen | 10 | 8 | 6 | 4 | 2 | 1 | 31 |
| Tiergeister | 10 | 8 | 6 | 4 | 2 | 1 | 31 |
| Dämonen | 10 | 8 | 6 | 4 | 2 | 1 | 31 |
| Götter | — | — | — | 4 | 2 | 1 | 7 |
| **GESAMT** | **40** | **32** | **24** | **20** | **10** | **5** | **131** |

Die **5 Mythischen Karten** sind die einzigartigen Endgame-Ziele:
- Ritter: Erzkönig Aldric
- Elfen: Sternbaum-Geist Elarion
- Tiergeister: Fenrir, Urdrachenwolf
- Dämonen: Urdämon Malphas Rex
- Götter: Aetherius, der Allschöpfer

Kartenliste: `Unity/Assets/_Project/Resources/Data/cards.json` (158 Karten Launch — 131 Standard + 9 Event + 6 Premium + 2 Sternkarten-Tempel + 10 Prestige).

### 5.3 Karten-Aufbau (UI-Layout)

| Position | Feld |
|----------|------|
| Oben links | Karten-Level (0–15, gold ab LV 15) |
| Oben Mitte | Kartenname |
| Oben rechts | COST (Mana-Kosten 4–50) + Sterne (1–6★) |
| Mitte links | Rundenwarten (Sanduhr-Icon mit Zahl, 2–4 typisch) |
| Unten links | ATK |
| Unten rechts | HP |
| Detail-Modal | Rasse + Element + 3 Skills + Letzter Wille (nur 6★) + Dialog-Lines |

Domain: `Domain/Cards/CardDefinition.cs` (ScriptableObject).

---

## 6. Fusions-Crafting-System

Zwei Crafting-Wege (Designplan v4 Kap. 5):

### 6.1 Kategorie-basiertes Crafting (Typ A)

Spieler kombiniert mehrere Karten **derselben Rasse und Seltenheit** → erhält eine zufällige höherwertige Karte derselben Rasse. Basis-Karten werden komplett verbraucht.

| Material | Ergebnis | Zusatzkosten |
|----------|----------|--------------|
| 3× gleiche Rasse 1★ | 1× zufällige 2★ (gleiche Rasse) | 1.000 Gold |
| 3× gleiche Rasse 2★ | 1× zufällige 3★ (gleiche Rasse) | 5.000 Gold |
| 4× gleiche Rasse 3★ | 1× zufällige 4★ (gleiche Rasse) | 25.000 Gold + Rare Scrap |
| 4× gleiche Rasse 4★ | 1× zufällige 5★ (gleiche Rasse) | 100.000 Gold + Epic Scrap |
| 3× verschiedene 5★ + Event-Material | 1× 6★ (Mythisch) | 5.000.000 Gold + Mythischer Kern |

Regeln: `Domain/Cards/CategoryFusionRules.cs`.

### 6.2 Feste Rezepte (Typ B)

Bestimmte Karten-Kombinationen ergeben immer eine spezifische höherwertige Karte. Rezepte sind teilweise versteckt — entdeckbar durch Story-Fortschritt, NPC-Hinweise oder Community.

**Beispiel-Rezepte:**

| Rezept | Input | Material | Gold | Output |
|--------|-------|----------|------|--------|
| Mondbogen-Jägerin | Elfenschütze (2★) + Blumenfee (1★) | — | 2.000 | Waldläuferin Fenris (3★) |
| Schattenfürst Kael (versteckt) | Schattenklaue (1★) + Nachtkreatur (1★) | Dunkel-Rune | 3.000 | Schattenläuferin Nyx (3★) |
| Gott des Schildes | Paladin (3★) + Felsenbrecher (3★) + Tempelwächterin (2★) | Heiliger Stein | 50.000 | Solaris (4★ Götter) |

Daten: `Unity/Assets/_Project/Resources/Data/fusion_recipes.json`. Domain: `Domain/Cards/FusionRecipe.cs`.

### 6.3 Götter-Karten Crafting

Götter-Karten gibt es NICHT als Drops. Sie kommen ausschließlich über feste Rezepte (verschiedene Rassen-Karten + seltenes Säulen-Material) oder seltene Events.

**Design-Philosophie:** Götter symbolisieren die Zusammenarbeit aller Rassen. Man braucht Karten verschiedener Rassen um sie zu craften — kein Spieler kann sich nur auf eine Rasse spezialisieren und trotzdem Götter besitzen.

### 6.4 Sicherheitsmechanismen

| Mechanismus | Effekt |
|-------------|--------|
| **Letzte-Kopie-Warnung** | Bestätigungsdialog wenn letzte Kopie für Fusion verbraucht werden soll |
| **Deck-Sperre** | Karten in aktivem Deck können nicht für Fusion verwendet werden |
| **Favoriten-System** | Karten als „Favorit" markieren — vor versehentlicher Fusion geschützt |
| **Premium-Karten gesperrt** | Premium-Karten können NICHT in Fusion verwendet werden (Designplan v4 Oeko Kap. 3.2) |
| **Rückkauf** | 24h nach Verkauf zum doppelten Preis zurückkaufbar (1–3★) |

### 6.5 Karten-Besitz-Limits

| Seltenheit | Max. Kopien | Hinweis bei Überschreitung |
|-----------|-------------|----------------------------|
| 1★ Gewöhnlich | Unbegrenzt | — |
| 2★ Ungewöhnlich | Unbegrenzt | — |
| 3★ Selten | Max. 5 | Ab 5 Kopien werden Drops in Scraps umgewandelt |
| 4★ Epic | Max. 3 | Ab 3 Kopien werden Drops in Epic Scraps umgewandelt |
| 5★ Legendär | Max. 2 | Zweite Kopie nur durch Crafting/Events |
| 6★ Mythisch | Max. 1 | Kann nicht dupliziert werden (Event-Version zählt separat als Skin) |

---

## 7. Karten-Persönlichkeit

Jede Karte ab **3 Sternen** hat Persönlichkeit (Designplan v4 Kap. 8):

### 7.1 Dialog-Lines

| Trigger | Beschreibung |
|---------|--------------|
| `OnPlay` (Einsetzen) | Kurzer Spruch beim Ausspielen |
| `OnVictory` (Sieg) | Spruch nach gewonnenem Kampf |
| `OnDeath` (Tod) | Letzter Spruch beim Tod |

**Beispiele (lokalisiert):**

- **Jormungand** OnPlay: „Die Welt... wird verschlungen."
- **Selene** OnPlay: „Das Mondlicht erinnert sich."
- **Grimmfang** OnDeath: „Das Rudel... kämpft weiter..."

CardDefinition-Felder: `onPlayLineKey`, `onVictoryLineKey`, `onDeathLineKey`.

### 7.2 Karten-Interaktionen

**Synergy-Bonus:** Bestimmte Karten gemeinsam im Deck → kleiner Bonus.
- Beispiel: `elfenmagierin_lira` + `waechterin_sakura` → +5 % HP für beide.

**Rivalen-Dialog:** Wenn bestimmte Karten aufeinandertreffen → spezieller Kampf-Dialog.
- Beispiel: `daemonenkoenigin_lilith` ↔ `selene_mondschleier`.
- Beispiel: `aetherius_allschoepfer` ↔ `fenrir_urdrachenwolf` / `urdaemon_malphas_rex`.

**Lore-Verknüpfung:** Bestimmte Karten erzählen gemeinsam eine Geschichte.

CardDefinition-Felder: `rivalCardIds`, `synergyCardIds` (Listen).

### 7.3 Letzter Wille (Last Will, 6★ Mythisch)

Nur 6★ Mythische Karten haben ein viertes Skill-Slot: **Letzter Wille**, ausgelöst beim Tod der Karte (LV 15 MAX freigeschaltet).

- Aetherius: „Genesis" — belebt ALLE gefallenen eigenen Karten mit 50 % HP, Feinde verlieren alle Buffs.
- Fenrir: „Ragnarök" — verschlingt 2 Feinde mit niedrigstem HP, alle Tiergeister +40 % ATK bis Kampfende.
- Aldric: „Thronfolge" — wählt stärkste eigene Karte als Nachfolger, erhält Aldrics komplette Stats.

CardDefinition-Feld: `lastWillAbilityId`.

---

## 8. Karten-Level-System (LV 0–15)

| Level | Kopien benötigt | Upgrade-Stein-Typ | Stein-Anzahl | Gold-Kosten | Stat-Bonus |
|-------|-----------------|-------------------|--------------|-------------|-----------|
| LV 0 | Start | — | — | — | Basis |
| LV 1 | 0 | Common Scrap | 2 | 500 | +5 % ATK/HP |
| LV 2 | 0 | Common Scrap | 4 | 1.500 | +10 % |
| LV 3 | 0 | Common Scrap | 8 | 4.000 | +15 % |
| LV 4 | 0 | Common Scrap | 16 | 10.000 | +20 % |
| **LV 5** | **1 Kopie** | Rare Scrap | 4 | 25.000 | +25 % + **2. Fähigkeit** |
| LV 6 | 0 | Rare Scrap | 8 | 50.000 | +30 % |
| LV 7 | 0 | Rare Scrap | 16 | 90.000 | +35 % |
| LV 8 | 0 | Rare Scrap | 32 | 150.000 | +40 % |
| LV 9 | 0 | Rare Scrap | 60 | 250.000 | +50 % |
| **LV 10** | **2 Kopien** | Epic Scrap | 10 | 500.000 | +55 % + **3. Fähigkeit** |
| LV 11 | 0 | Epic Scrap | 25 | 800.000 | +58 % |
| LV 12 | 0 | Epic Scrap | 50 | 1.200.000 | +63 % |
| LV 13 | 0 | Epic Scrap | 100 | 2.000.000 | +68 % |
| LV 14 | 0 | Epic Scrap | 200 | 3.500.000 | +75 % |
| **LV 15** | **3 Kopien** | Legendary Scrap | 50 | 8.000.000 | +80 % + **Goldener Rahmen** + (bei 6★) **Letzter Wille** |

---

## 9. Helden-Passiv-Skills

Siehe Kap. 2.1. Helden sind **PASSIV** in v6 (Designplan v4) — kein Cooldown, kein manuelles Auslösen. Die Passiv ist an die gewählte Rasse gekoppelt.

Anders als in der älteren v5.x-Konzeption gibt es keine 6 austauschbaren Helden mit Aktiv-Skills. Stattdessen: 5 Rassen-Helden, je 1 Passiv.

---

## 10. Kampfsystem

### 10.1 Kampf-Ablauf

1. **Vorbereitung:** Deck (max. 10 Karten) gewählt, Runen geladen.
2. **Initialisierung:** Beide Spieler ziehen 4 Karten. Start-Mana 3. Heldenpassiv aktiv.
3. **Runden-Struktur (Spieler zuerst, dann Gegner):**
   - Mana regeneriert (+1 pro Runde bis max. 10)
   - 1 Karte ziehen
   - Karten ausspielen (Drag aus Hand aufs Feld; Mana-Kosten abgezogen)
   - Karten im Feld greifen automatisch an (Standard-Attacke jede Runde)
   - Rundenwarten-Zähler aller Karten zählt herunter — bei 0 zündet Spezialattacke
   - End Turn → Gegner ist dran
4. **Sieg-Bedingung:** Gegner-Held auf 0 HP.
5. **Maximale Runden:** 50 (Sudden-Death, doppelter Schaden).

### 10.2 Element-Schaden-Berechnung

```
finaleSchaden = baseAttack
              × ElementMatchup.GetMultiplier(attacker.Element, defender.Element)
              × (1 + buffPercent)
              × (1 - defensePercent)
```

Element-Matchup siehe Kap. 3.3. Implementierung in `Domain/Battle/ElementMatchup.cs`.

### 10.3 Boss-Kämpfe (LV 5 & LV 10)

Bosse haben **Phasen** — bei < 50 % HP aktiviert Phase 2 mit Spezialfähigkeit + neuen Karten.

| Boss-Typ | Phase 1 | Phase 2 |
|----------|---------|---------|
| LV 5 Mini-Boss | Normal-Kampf | AoE-Attacke alle 3 Runden |
| LV 10 Welt-Boss | Mehrere starke Karten | Ultimate (1.5x Schaden) + 2 zusätzliche Karten |

**Sonderregel (Designplan v4 Kap. 6):**
Bei Boss-Kämpfen ist Auto-Battle beim **ersten Versuch deaktiviert**. Boss-Phasen-Wechsel sollen manuell erlebt werden. Nach dem ersten Sieg kann der Boss auch mit Auto-Battle wiederholt werden.

Implementierung: `Domain/Battle/AutoBattleProgression.cs::IsAutoBattleAllowedForBoss()`.

### 10.4 Prestige-Bosse

Bosse skalieren mit Prestige-Stufe (siehe Kap. 14):
- Prestige III: 3 Phasen
- Prestige IV: 4 Phasen + einzigartige Mechanik

---

## 11. Auto-Battle-Progression

Auto-Battle wird durch Spieler-Level freigeschaltet (Designplan v4 Kap. 6):

| Spieler-Level | Auto-Battle-Feature |
|--------------|---------------------|
| LV 1–9 | **Kein Auto-Battle** (Tutorial-Phase, manuelles Spielen) |
| LV 10 | Auto-Battle 1x freigeschaltet (Pop-up-Feier) |
| LV 20 | 2x Geschwindigkeit |
| LV 30 | 3x Geschwindigkeit |
| LV 50 | 4x Geschwindigkeit (MAX) |

Implementierung: `Domain/Battle/AutoBattleProgression.cs`. Methode: `GetMaxAutoBattleSpeed(int playerLevel)`.

---

## 12. Story-Bogen

### 12.1 Der Spieler-Charakter — Der Namenlose Rufer

Der Spieler erstellt seinen Charakter:
- Name: Frei wählbar
- Geschlecht: M / W (beeinflusst Avatar + einige Dialoge)
- Rasse: Wählbar (Ritter, Elfe, Tiergeist, Dämon) — **Götter NICHT als Starter wählbar**

NPCs nennen den Spieler „Rufer" oder „Der/Die Namenlose". Der Spieler spricht nie selbst — wählt gelegentlich zwischen 2–3 Reaktionen (Nicken, Kopfschütteln, Nachfragen).

### 12.2 Rufer-Lore-Erklärung für Spielmechaniken

| Mechanik | Lore-Erklärung |
|----------|----------------|
| Karten aller Rassen im Deck | Ein Rufer kann JEDE Kreatur beschwören — seine einzigartige Gabe. |
| Beginn mit schwachen Karten | Gedächtnisverlust hat fast alle Seelen-Siegel zerstört. |
| Crafting / Fusion | Rufer können Seelen-Siegel verschmelzen — fortgeschrittene Technik, im Laufe der Story wieder-erlernt. |
| Götter-Karten erst ab 4★ | Götter lassen sich nur von einem Rufer beschwören, der die Zusammenarbeit aller Rassen bewiesen hat. |

### 12.3 Erinnerungs-Fragmente (10 Stück)

Jede Welt endet mit einem **Erinnerungs-Fragment** (5–15 Sekunden Kurzszene, S/W mit Rauscheffekt). Die Fragmente sind bewusst mehrdeutig — der Spieler soll sie falsch interpretieren können, bis der Twist kommt.

| # | Welt | Fragment-Inhalt | Spieler glaubt | Wahrheit |
|---|------|-----------------|----------------|----------|
| 1 | Elderwald | Ein Name: Nythragor. Angst und Schuld. | Ich kämpfte gegen Nythragor | Ich diente ihm |
| 2 | Sandreich | Hände halten Karten. Macht. Ein Lächeln. | Ich war ein mächtiger Held | Ich war machtsüchtig |
| 3 | Vulkanhort | Dunkler Raum. Nythragor spricht: „Du gehörst mir." Ich nicke. | Ich wurde gezwungen | Ich ging FREIWILLIG |
| 4 | Frostgipfel | Ich stand neben Nythragor. Götter weinen. | Erste Zweifel | Vielleicht war ich nicht der Held |
| 5 | Schattenlande | Ich habe eine Säule MIT Absicht zerstört. | Schock | Ich WAR Teil des Problems |
| 6 | Sturmzitadelle | Götter flehten mich an. Ich hörte nicht. | Horror | Was habe ich getan? |
| 7 | Titanengrat | Nythragor gab mir Macht. Ich NAHM sie. | Ich war gierig | Ich wollte die Macht |
| 8 | Abysstiefe | **DER TWIST** — Jormungand zeigt die VOLLE Erinnerung: Ich war Nythragors Champion. | — | Ich WAR der Feind |
| 9 | Galaxy Wald | Ich brach den Pakt. Es kostete alles. | — | Ich wandte mich ab — es kostete mein Gedächtnis |
| 10 | Drachenfeste | Mein wahrer Name. Warum ich mich abwandte: Ich sah das Leid. | — | Identität: Kein Held, kein Schurke. Ich ENTSCHIED MICH, besser zu sein. |

Daten: `Unity/Assets/_Project/Resources/Data/story_fragments.json`.

### 12.4 Schlüssel-NPCs

| NPC | Rasse | Welten | Rolle |
|-----|-------|--------|-------|
| Lumis | Lichtgeist | Alle | Ständiger Begleiter, Tutorial-Guide, eigenes Geheimnis (Fragment der Licht-Säule) |
| Marschall Aldor | Ritter | 1–2, 9–10 | Mentor (Ritter-Spieler), kannte den letzten Rufer |
| Mondpriesterin Lira | Elfe | 1, 4, 9–10 | Mentor (Elfen-Spieler), fürchtet alte Macht im Spieler |
| Grimmfang | Tiergeist | 1, 3, 7, 10 | Mentor (Tiergeist-Spieler), wusste die ganze Zeit wer Spieler war |
| Dämonenkönigin Lilith | Dämon | 3, 5, 9–10 | Mentor (Dämonen-Spieler), Bündnis aus Pragmatismus |
| Königin Sera | Ritter | 2, 6, 10 | Symbol der Hoffnung |
| General Dorn | Ritter | 2, 6 | Misstrauisch — hatte von Anfang an recht |
| Nythragor | Gott (gefallen) | 8–10 | Antagonist — glaubt aufrichtig an seinen Weg |

### 12.5 Endkampf — Entscheidung

In Welt 10 (Drachenfeste) trifft der Spieler auf Nythragor. **Entscheidung:**
- **Nythragor zerstören** — Endpunkt A
- **Nythragor erlösen** — Endpunkt B

Beide Enden sind narrativ gleichwertig. Verschiedene Erfolge / Avatare / Titel pro Ende.

---

## 13. Karten-Ökosystem

Über die 131 Standard-Karten hinaus existieren **27 exklusive Karten** in verschiedenen Ökosystemen (Designplan v4 Oeko-Kap. 7).

| Kategorie | Anzahl | Quelle | CardDefinition-Flag |
|-----------|--------|--------|----------------------|
| Event-Karten | 9 | Saison-Events (5 pro Jahr) | `IsEventCard` |
| Premium-Karten | 6 | Diamanten-Shop (permanent oder rotierend) | `IsPremiumCard` |
| Sternkarten-Tempel-Exklusive | 2 | Login-Sternkarten-Eintausch | `IsStarTempleCard` |
| Prestige-IV-Karten | 10 | Prestige IV einer Welt (1 pro Welt) | `IsPrestigeCard` |
| Saison-Pass-Karten | 24/Jahr (2/Monat) | Saison-Pass Stufe 15 (Free) / 30 (Premium) | `IsSaisonPassCard` |

### 13.1 Event-Karten (9, Launch-Jahr)

| Event | Monat | Event-Karte 1 | Event-Karte 2 | Notfall-Diamantkosten |
|-------|-------|---------------|---------------|----------------------|
| Yule-Fest der Schatten | Dez–Jan | Yule-Baumgeist Veradis (4★) | Frostwolf Fenris (3★) | 1000 / 500 |
| Blütenfest | Mär–Apr | Blütenfee Hanami (4★) | Frühlingsdrache Verdant (3★) | 1000 / 500 |
| Sonnenwende-Inferno | Jun–Jul | Sonnendämon Solaris (5★) | Flammenvogel Helios (4★) | 1500 / 1000 |
| Erntemondfest | Sep–Okt | Erntegott Miraveth (4★) | Mondkaninchen Luna (3★) | 1000 / 500 |
| Schattenerwachen | Nov | Geisterkönig Samhain (5★) | — | 1500 |

**Notfall-Kauf:** Am LETZTEN TAG des Events können Spieler Event-Karten für Diamanten kaufen, falls die Event-Punkte nicht reichten.

Daten: `Unity/Assets/_Project/Resources/Data/events.json`.

### 13.2 Premium-Karten

| Karte | ★ | Rasse | Preis | Verfügbarkeit |
|-------|---|-------|-------|---------------|
| Goldwolf Aurelius | 3★ | Tiergeist | 300 💎 | Permanent |
| Himmelsritter Orion | 4★ | Ritter | 800 💎 | Rotation 3 Monate |
| Schattenprinzessin Nyx | 4★ | Dämon | 800 💎 | Rotation 3 Monate |
| Kristallhirsch Cervus | 3★ | Tiergeist | 300 💎 | Permanent |
| Elfenprinz Luminaris | 4★ | Elfe | 800 💎 | Rotation 4 Monate |
| Infernalwolf Pyrrhus | 3★ | Dämon | 300 💎 | Permanent |

Daten: `Unity/Assets/_Project/Resources/Data/premium_shop.json`.

**Wichtig:** Premium-Karten können NICHT für Fusion verwendet werden (Schutz vor versehentlichem Verlust).

### 13.3 Prestige-IV-Karten (10, eine pro Welt)

| Welt | Karte |
|------|-------|
| W1 Elderwald | Urwaldgeist Ygg (3★ Tiergeist) |
| W2 Sandreich | Sandkaiser Darius (3★ Ritter) |
| W3 Vulkanhort | Lavaschmied Pyrros (3★ Dämon) |
| W4 Frostgipfel | Eiskönigin Freja (4★ Elfe) |
| W5 Schattenlande | Schattenfürst Mordred (4★ Dämon) |
| W6 Sturmzitadelle | Blitzgeneral Thorak (4★ Ritter) |
| W7 Titanengrat | Bergtitan Gorak (3★ Tiergeist) |
| W8 Abysstiefe | Tiefseekaiser Leviath (4★ Dämon) |
| W9 Galaxy Wald | Kosmischer Druide (4★ Elfe) |
| W10 Drachenfeste | Urdrachenlord Tiamat (4★ Tiergeist) |

### 13.4 Sternkarten-Tempel-Exklusive

| Karte | ★ | Rasse | Sternpunkte | Rotation |
|-------|---|-------|-------------|----------|
| Sternenweber Astria | 3★ | Elfe | 150 | Alle 2 Monate |
| Sternentiger Raj | 4★ | Tiergeist | 350 | Alle 3 Monate |

Daten: `Unity/Assets/_Project/Resources/Data/star_temple.json`.

### 13.5 Saison-Pass-Karten

Pro Monat:
- **Stufe 15 (Free-Track):** 3★ Saison-Karte mit Saison-Rahmen (Frühlingsblüten/Herbstblätter/etc.)
- **Stufe 30 (Premium-Track):** 4★ Premium-Saison-Karte

Daten: `Unity/Assets/_Project/Resources/Data/saison_pass.json`.

---

## 14. Prestige-System für Welten

Wenn ein Spieler ALLE Level einer Welt mit 3 Sternen abgeschlossen hat, kann er die Welt für Spielgold auf die nächste Prestige-Stufe aufwerten (Designplan v4 Oeko-Kap. 6).

| Stufe | Kosten | Gegner-Stats | Gold-Drop | Daily-Income | Boss-Phasen | Exklusive Karte |
|-------|--------|--------------|-----------|--------------|-------------|-----------------|
| Normal | — | 1.00x | 1.00x | 1.00x | 2 | — |
| I | 100k Gold | 1.30x | 1.50x | 2.00x | 2 | — |
| II | 500k Gold | 1.60x | 2.00x | 4.00x | 2 | — |
| III | 2M Gold | 2.00x | 3.00x | 8.00x | 3 | — |
| IV (MAX) | 5M Gold | 2.50x | 4.00x | 16.00x | 4 | **Prestige-IV-Karte freigeschaltet** |

**Regeln:**
- Beim Aufwerten werden die Sterne ZURÜCKGESETZT
- Revenue/Day der Welt bleibt erhalten
- Prestige-Stufe wird neben dem Welt-Namen angezeigt (z.B. „Elderwald ★★★" für Prestige III)
- Prestige-Stufen sind pro Welt unabhängig

**Bei allen 10 Welten auf Prestige IV: ca. 150.000+ Gold/Tag passives Einkommen.**

Daten: `Unity/Assets/_Project/Resources/Data/prestige_balancing.json`. Domain: `Domain/World/PrestigeStufe.cs`.

---

## 15. Sternkarten-System

### 15.1 Login-Belohnungen (30-Tage-Zyklus)

Tägliches Einloggen gibt steigende Belohnungen + **Sternkarten** (Sammelkarten ohne Kampfwert).

| Tag | Belohnung-Highlights | Sternkarte |
|-----|---------------------|------------|
| Tag 1–6 | Gold, Common Scraps, kleine Karten | 1× Bronze pro Tag |
| Tag 7 | Wählbare 2★ + Runen-Fragment | 1× Silber |
| Tag 8–13 | Mix aus Gold, Scraps, EXP-Tränken | 1× Bronze pro Tag |
| Tag 14 | Zufällige 3★ + Rare Scrap | 1× Silber |
| Tag 15–20 | Diamanten 5–10/Tag, Scraps | 1× Bronze pro Tag |
| Tag 21 | Wählbare 3★ + Epic Scrap | 1× Gold |
| Tag 22–29 | Diamanten, Epic Scraps | 1× Bronze pro Tag |
| Tag 30 | Zufällige 4★ + Legendary Scrap + 50 💎 | 1× Gold + 1× Platin |

**Pro Monat bei täglichem Login:** 22× Bronze + 2× Silber + 2× Gold + 1× Platin = 112 Sternpunkte.

Daten: `Unity/Assets/_Project/Resources/Data/login_rewards.json`.

### 15.2 Sternkarten-Werte

| Sternkarte | Sternpunkte |
|------------|-------------|
| Bronze | 1 |
| Silber | 5 |
| Gold | 15 |
| Platin | 50 |

### 15.3 Sternkarten-Tempel (Eintausch)

| Belohnung | Kosten (Sternpunkte) | Beschreibung |
|-----------|---------------------|--------------|
| Zufällige 2★-Karte | 30 | Random aus Pool |
| Wählbare 3★-Karte | 80 | Spieler wählt aus Pool |
| Sternkarten-Exklusive 3★ | 150 | Wechselt alle 2 Monate (Sternenweber Astria) |
| Sternkarten-Exklusive 4★ | 350 | Wechselt alle 3 Monate (Sternentiger Raj) |
| Legendary Scrap | 100 | Für LV 15-Upgrade |
| Mythischer Kern-Fragment | 500 | 1/3 eines Kerns (3 Fragmente = 1 Kern für 6★-Crafting) |

**Design-Philosophie:** Sternkarten belohnen KONSTANZ, nicht Intensität. Ein 5-Min-Spieler bekommt gleich viel wie ein 3h-Spieler — solange er sich einloggt.

Daten: `Unity/Assets/_Project/Resources/Data/star_temple.json`. Domain: `Domain/Economy/Sternkarte.cs`.

---

## 16. Arena, Gilden, Klan-Matches, Dieb-Event

Diese Systeme bleiben unverändert aus v5.4 (Designplan v4 referenziert sie als Übernahme aus v3). Kernpunkte:

### 16.1 Arena (PvP, async)

- Glicko-2 Matchmaking, 30s Cooldown, 5 Energie pro Kampf
- Saison-System (30 Tage), Rang-Belohnungen Bronze→Meister
- Phase-2-Feature: Live-PvP für Top-100 via Photon Fusion

### 16.2 Gilden

- Max. 30 Mitglieder (LV 1) → 50 (LV 10)
- Mindestlevel 25, Gründungskosten 50.000 Gold
- Tech-Tree (Tier 1 zum Launch), Gilden-Chat, Karten-Tausch (Gilde-intern Phase 1)

### 16.3 Klan-Matches (Gebiets-Krieg)

- Gebots-System (3 Tage Bietphase, 1 Tag Match-Vorbereitung)
- Best-of-9 zwischen Top 2 Bietern
- Gebiete geben Daily-Income (1.000–20.000 Gold/Mitglied)
- **Saisonale Gebiets-Boni:** Quartalsweise wechseln die Boni (Designplan v4 Kap. 9.1)
- **Live-Weltkarte:** Echtzeit-Übersicht welche Gilde welches Gebiet kontrolliert (für alle Spieler sichtbar)

### 16.4 Dieb-Event (Server-Coop)

- Mysteriöser/Elite/Legendary Dieb alle 4–6h
- HP-Pool skaliert mit DAU
- Belohnungs-Verteilung nach Schadensanteil

Detail-Specs siehe v5.4 (bleibt gültig, hier nur als Übersicht).

---

## 17. Lokalisierung

**Primäre Sprache:** Deutsch (DACH-Markt).
**Sekundäre Sprache:** Englisch (zum Launch).
**Phase 2:** Französisch, Spanisch.

Alle Karten-Namen, Skill-Beschreibungen, Welt-Namen, NPC-Dialoge, Erinnerungs-Fragmente in DE + EN.

Karten-Persönlichkeit-Lines (Einsetzen, Sieg, Tod) müssen authentisch klingen — keine generischen Phrasen.

Localization-Keys folgen Pattern:
- `card.<id>.name`
- `card.<id>.flavor`
- `card.<id>.play` / `victory` / `death`
- `world.<id>.name` / `story` / `memory`
- `fragment.<n>.title` / `content` / `reveal`
- `npc.<id>.name` / `desc`
- `saeule.<name>` / `.state`

Implementierung: Unity Localization Package (`com.unity.localization`).

---

## 18. Datenmodell & Technologie

### 18.1 Tech-Stack

| Bereich | Tech | Hinweis |
|---------|------|---------|
| Engine | Unity 6 (6000.4.8f1) | URP, IL2CPP ARM64 |
| Sprache | C# (.NET Standard 2.1) | UniTask statt Task<T> |
| UI | UI Toolkit + UGUI | Mobile-optimiert |
| DI | VContainer | AOT-kompatibel |
| Backend | Firebase Auth/Realtime DB/Cloud Functions/Crashlytics/Analytics | DSGVO-konform |
| Multiplayer | Photon (Chat, Dieb-Sync) + Photon Fusion (Phase 2 Live-PvP) | |
| Persistenz | Firebase Realtime DB + PlayerPrefs Cache | Save-Schema v3 (mit Prestige + Sternkarten) |
| Asset-Loading | Addressables | Karten-Art on-demand |

### 18.2 Save-Schema v3 (Erweiterung gegenüber v2)

Neue Slices:
- `PrestigeSaveSlice` — Map<worldId, PrestigeStufe>
- `SternkartenSaveSlice` — Inventar Bronze/Silber/Gold/Platin + verbrauchte Sternpunkte
- `MemoryFragmentSaveSlice` — freigeschaltete Fragments + welche schon angezeigt wurden
- `HeroPassivSaveSlice` — gewählte Rasse (= Helden-Passiv)
- `KartenPersoenlichkeitSlice` — gesehene Dialog-Lines (für Skip-Logik)
- `EventSaveSlice` — aktuelle Event-Punkte + Notfall-Kauf-Slot freigeschaltet

`SaveMigrator.CurrentSchemaVersion` muss auf 3 erhöht werden bei Implementierung.

---

## 19. Entwicklungs-Zeitplan (Designplan v4 Implementierungsplan)

| # | Phase | Zeitraum | Hauptziel | Meilenstein |
|---|-------|----------|-----------|-------------|
| 1 | Foundation | Monat 1–3 | Technische Basis, Architektur, Prototypen | Technischer Prototyp spielbar |
| 2 | Core Gameplay Loop | Monat 3–6 | Kampfsystem komplett, Deck-Builder, Element-System, 1 Welt spielbar | Vertical Slice (1 Welt komplett) |
| 3 | Content Pipeline | Monat 5–9 | Alle 131 Karten implementiert, 5 Welten, Crafting, Runen | Alpha (5 Welten) |
| 4 | Meta-Systeme | Monat 7–11 | Prestige, Saison-Pass, Login-System, Sternkarten, Events, Premium-Shop | Feature-Complete (offline) |
| 5 | Multiplayer & Social | Monat 10–14 | Arena, Gilden, Klan-Matches, Chat, Dieb, Ranglisten | Beta-Build (online) |
| 6 | Polish & Content | Monat 13–17 | Alle 10 Welten, Balancing, Kurzszenen, Sound, Performance, Lokalisierung | Release Candidate |
| 7 | Testing & Launch | Monat 16–20 | Bug-Fixing, Server-Stress-Tests, Marketing, Soft-Launch | **LAUNCH** |

**Post-Launch (Monat 21+):**
- Saison 1 Start, iOS-Launch, Echtzeit-PvP (Photon Fusion), Klan-Matches 5v5, Dungeon-System, Labyrinth, Roguelike-Modus.

---

## 20. Risiken & MVP

### 20.1 Top-Risiken (Designplan v4 Implementierungsplan Kap. 13)

| Risiko | Wahrscheinlichkeit | Impact | Fallback |
|--------|--------------------|--------|----------|
| PvP-Balancing explodiert | Hoch | Spieler-Frust | Wöchentliche Balance-Patches, Karten in Arena deaktivierbar |
| AI-Art Konsistenz-Probleme | Hoch | Karten sehen uneinheitlich aus | Art Director prüft JEDE Karte, Photoshop-Nachbearbeitung, Freelancer für kritische Karten |
| Photon-Kosten | Mittel | Server-Kosten zu hoch | Async-PvP als Fallback (wie LoA) |
| Content-Drought nach Launch | Mittel | Retention sinkt | 3 Monate Content VOR Launch vorbereiten |
| Feature Creep | Hoch | Endlose Entwicklung | Strikte MVP-Definition, alles andere Post-Launch |

### 20.2 MVP-Definition

**IM MVP (muss zum Launch da sein):**
- Kampfsystem vollständig (Mana, Elemente, Skills, Held-Passiv, Auto-Battle 1–4x)
- 131 Standard-Karten + 6 Premium + erste 2 Event-Karten = ca. 140 Karten
- Alle 10 Welten mit Story, Sternen, Bossen
- Crafting (kategorie + feste Rezepte + Götter)
- Runen (4 Slots), Arena (async PvP), Gilden (Tier 1)
- Shop, Saison-Pass, Login-System (Sternkarten), Prestige I–IV alle Welten
- Lokalisierung: Deutsch komplett + Englisch komplett

**NICHT im MVP (Post-Launch):**
- Echtzeit-PvP (Photon Fusion)
- Klan-Matches 5v5
- Dungeon (100 Ebenen), Labyrinth, Roguelike
- iOS-Launch
- Weitere Sprachen
- Dieb-System (kann später nachgereicht werden)

---

## 21. Glossar

| Begriff | Bedeutung |
|---------|-----------|
| **Aetherius** | Allschöpfer (höchste Gottheit), 6★ Mythische Karte der Götter-Rasse |
| **Auto-Battle** | KI-gesteuertes Spielen, freigeschaltet ab LV 10 |
| **COST** | Mana-Kosten zum Ausspielen einer Karte |
| **Doppel-Dreieck** | Physisches (Feuer/Wasser/Natur) + magisches (Licht/Dunkel/Erde) Element-Dreieck |
| **Erinnerungs-Fragment** | Kurzszene am Ende jeder Welt, deckt schrittweise die Spieler-Vergangenheit auf |
| **Heldenpassiv** | Rassen-spezifischer Skill der für ALLE Kämpfe gilt (KoeniglicheAura, GoettlicherSegen, Waldlaeufer, Rudelbund, LebensraubAura) |
| **Letzter Wille** | Vierter Skill nur für 6★ Mythische Karten (LV 15 freigeschaltet) |
| **Mythischer Kern** | Material für 6★-Crafting. 3 Fragmente = 1 Kern. |
| **Nythragor** | Antagonist, gefallener siebter Gott, Verderbnis-Quelle |
| **Prestige** | Welt-Schwierigkeit aufwerten (I–IV), bessere Drops + exklusive Karte |
| **Rufer** | Spieler-Charakter, kann Seelen-Siegel (Karten) beschwören |
| **Säule** | Eine der 6 elementaren Säulen die Aethera im Gleichgewicht halten |
| **Sternkarte** | Login-Sammelkarte (Bronze/Silber/Gold/Platin), eintauschbar im Sternkarten-Tempel |
| **Verderbnis** | Krankheit der Realität, von Nythragor verbreitet |

---

## 22. Änderungslog v5.4 → v6.0

| Änderung | Konsequenz |
|----------|-----------|
| **Rassen-Enum komplett umgestellt** | 8 Rassen (Koenigreich/Demonist/Elfe/Daemon/Untoter/Maschine/Tier/Drache) → 5 Rassen (Ritter/Goetter/Elfen/Tiergeister/Daemonen). Alle alten Karten neu klassifiziert. |
| **Element-Enum erweitert** | 5 Elemente (Natur/Feuer/Wasser/Licht/Dunkel) → 6 Elemente (+ Erde). Doppel-Dreieck statt einfaches Dreieck. ElementMatchup-Matrix komplett neu. |
| **Rarity-Enum erweitert** | 5 Stufen → 6 Stufen (+ Mythisch). Pyramide angepasst. |
| **Helden komplett umgebaut** | 6 austauschbare Helden mit Aktiv-Skill → 5 Rassen-Helden mit Passiv-Skill (an Rasse gekoppelt). HeroCooldown-Klasse weiter vorhanden für andere Once-Per-Battle-Effekte. |
| **CardDefinition erweitert** | + `onPlayLineKey`/`onVictoryLineKey`/`onDeathLineKey`, `rivalCardIds`/`synergyCardIds`, `lastWillAbility`, 5 Ökosystem-Marker (Event/Premium/Prestige/Sternkarte/SaisonPass) |
| **WorldDefinition erweitert** | + `recommendedCounterElement`, `saeuleNameKey`, `bossCardId`, `storySummaryKey`, `memoryFragmentKey`, `mentorNpcKey`, `baseGoldPerDay`, `prestige4CardId` |
| **131 Karten neu** | cards.json komplett ersetzt — 158 Karten total (131 Standard + 27 Ökosystem) |
| **10 Welten** | Statt 9 — Titanengrat als W7 eingefügt (Erd-Säule), Drachenfeste als W10 mit Nythragor-Finale |
| **Story-Mythologie** | Welt-Mythologie + Erinnerungs-Fragmente + Twist + Schlüssel-NPCs jetzt autoritativ dokumentiert (`story_fragments.json`) |
| **Prestige-System** | Neu hinzugefügt (`PrestigeStufe.cs` + `prestige_balancing.json`) |
| **Sternkarten-System** | Neu hinzugefügt (`Sternkarte.cs` + `login_rewards.json` + `star_temple.json`) |
| **Auto-Battle-Progression** | Neu hinzugefügt (`AutoBattleProgression.cs`) — LV 10/20/30/50 Speed-Stufen |
| **Fusions-Crafting** | Neue Domain-Klassen `FusionRecipe.cs` + `CategoryFusionRules.cs` + `fusion_recipes.json` |
| **Premium-Shop-Daten** | Neu: `premium_shop.json` (6 Premium-Karten Rotation) |
| **Event-Kalender** | Neu: `events.json` (5 Saison-Events mit Datums-Fenstern und Notfall-Kauf-Optionen) |
| **DataImporter komplett umgebaut** | Neue Felder, neue Validierung (Goetter nur 4★+, 6★ braucht Letzten Willen, Cost-Range 1–60) |

---

**Dokument-Ende.**

> Nächste Aktualisierung: Nach Phase 2 Vertical Slice (Monat 6) — dann GDD v6.1 mit Lessons-Learned aus dem ersten spielbaren Welt-Build.
