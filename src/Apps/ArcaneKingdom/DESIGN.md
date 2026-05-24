# ArcaneKingdom — Game Design Document (GDD)

> **Version 5.4** | Stand: 2026-05-24 | Plattform: Android | Engine: Unity 6 (6000.4.x)
> Quelle: `Kartenspiel Start.docx` v5.0 (Maerz 2026), Referenz: *Arcane Magic*

Dieses Dokument ist die **autoritative Single-Source-of-Truth** fuer das Game Design.
v5.3 ergaenzt die Material-Karten-IDs der Sammelsets, dokumentiert die 8 Tutorial-Schritte
und praezisiert die Reset-Zeiten (Daily 00:00 UTC, Weekly Montag 00:00 UTC).
Alle Aenderungen gegenueber v5.0 / v5.1 / v5.2 sind im Abschnitt **23. Aenderungslog** dokumentiert.

---

## Inhaltsverzeichnis

0.  Game Director Summary
1.  Vision & Kernkonzept
2.  Login- & Ladebildschirm
3.  Hauptbildschirm (Hub-Welt)
4.  Spieler-Profil & Charakter-System
5.  Das Karten-System
6.  Craft- & Tauschsystem (Zauberschmiede)
7.  Das Runen-System
8.  Welten & Level-Karte
9.  Das Kampfsystem
10. Dieb-System (World Event)
11. Arena (PvP)
12. Gilden-System
13. Gilden-Weltkarte & Klan-Matches
14. Chat-System
15. Merit-System & Ranglisten
16. Langzeit-Content (Events, Quests)
17. Waehrungs- & Wirtschaftssystem
18. Technologie & Entwicklung
19. Entwicklungs-Zeitplan
20. Datenmodell-Skizze (Implementierungs-Sicht)
21. Game-Economy-Stellschrauben & Telemetrie
22. Glossar
23. Aenderungslog v5.0 → v5.1 und offene Punkte (TBD)

---

## 0. Game Director Summary

**Pitch (Elevator):** Ein Mobile-Sammelkartenspiel mit RPG-Tiefe. Spieler sammeln und leveln Karten,
bauen Decks, ziehen durch 9 thematische Welten, treten in der Arena gegeneinander an, jagen
server-weite Diebe und kaempfen in Gilden um Gebiete einer gemeinsamen Weltkarte.

**Genre:** TCG (Trading Card Game) + RPG-Progression + soziales Sandbox-Endgame
**Plattform:** Android (Phase 1, Monat 23 Launch) — iOS (Phase 2, Monat 26+)
**Spielsitzung:** 3-5 Min (PvE-Kampf) bzw. 10-15 Min (Arena/Event-Burst)
**F2P-Modell:** Optionale Diamant-Kaeufe (Karten-Packs, Energie, Premium-Saison-Pass ab Saison 2)

**Drei Designsaeulen:**
1. **Sammeln & Bauen** — jede der ca. 150 Karten kann auf LV 15 gebracht werden; max. 3 Kopien farmbar, Rest via Craft/Trade. Decks (max. 10 Karten) sind die kreative Spieleridentitaet.
2. **Strategischer Kampf** — Mana-System, Rundenwarten fuer Spezialattacken, Elementarvorteile, Phasen-Bosse. Reine "Auto-Battle"-Decks werden von strategischen Decks geschlagen.
3. **Soziales Endgame** — Gilden, Klan-Matches um Gebiete, World-Dieb-Events, Chat. Ohne Gilde bleibt das Mid/Endgame eindimensional.

**Wichtigste Erfolgs-KPIs (Launch-Ziele):**

| KPI | Ziel |
|-----|------|
| D1-Retention | >= 45 % |
| D7-Retention | >= 22 % |
| D30-Retention | >= 10 % |
| Avg. Session/Tag | >= 4 |
| Conversion (zahlende Spieler) | >= 3 % |
| ARPDAU | >= 0,12 USD |

---

## 1. Vision & Kernkonzept

| Merkmal | Beschreibung |
|---------|--------------|
| Genre | Sammelkartenspiel + RPG |
| Plattform | Android (Phase 1), iOS (Phase 2) |
| Spielmodi | PvE (Welten-Kampagnen, Boss-Raids), PvP (Arena), Coop (Dieb-Event), Guild-vs-Guild (Klan-Match) |
| Monetarisierung | F2P + IAP (Diamanten, Packs, Energie, Premium-Pass ab Saison 2) |
| Zielgruppe | 16-35 Jahre, Kartenspiel- & RPG-Fans |
| Inspiration | *Arcane Magic*, *Clash Royale* (Deck-Bau), *Marvel Snap* (kurze Kaempfe), *Lords Mobile* (Gilden-Karte) |

### Kern-Spielerfantasie

- "Ich baue mein perfektes Deck und sehe es im Kampf glaenzen."
- "Meine Gilde dominiert das Schwarzbinge-Gebiet — dafuer bekomme ich taeglich Gold."
- "Ich war Top-3 im Server beim letzten Legendary-Dieb."

### Out-of-Scope (Phase 1)

- Story-Modus mit gesprochenen Dialogen / Cutscenes (nur Text-Briefings)
- Cross-Server-PvP
- Custom-Skins fuer Karten / Karten-Kosmetik
- Single-Card-Loadouts (Hero/Karten-Skins)
- Live-Spectator-Modus fuer Klan-Matches

---

## 2. Login- & Ladebildschirm

Der erste Eindruck. Atmosphaerische Fantasy-Illustration (mystischer Wald mit leuchtenden Pflanzen).
Login soll < 2 s zum interaktiven Zustand brauchen, < 8 s bis Hub-Welt geladen.

### 2.1 Elemente des Login-Bildschirms

| Element | Beschreibung |
|---------|--------------|
| Hintergrund | Stimmungsvolle Fantasy-Illustration mit dezentem Parallax (3 Layer) |
| Logo | Mittig oben, leichte Pulsations-Animation (2 s Periode) |
| Server-Auswahl | Dropdown oben rechts: zeigt letzten Server (sticky), tippen oeffnet Server-Liste |
| Auto-Login | Grosser roter Button mittig: verbindet automatisch mit letztem Server (Default) |
| Manual-Login | Schmaler Sekundaer-Button darunter: Login mit anderem Account (Gast → Google → E-Mail) |
| Fehler-Pop-up | Bei Netzwerkfehler: Modal mit Retry/Settings, App nicht crashen |
| Ladebalken | Erscheint nach Auto-Login. 4 Phasen: Auth → Server-Connect → Asset-Sync → Player-Daten |
| Patch-Notes | Kollabierbares Banner unten: "Was ist neu?" → oeffnet WebView mit Changelog |
| Maintenance-Modus | Wenn Server in Wartung: Vollbild-Pop-up mit ETA, kein Login moeglich |

### 2.2 Account-System

| Login-Typ | Persistenz | Wechsel-Strategie |
|-----------|-----------|-------------------|
| Gast (Default) | Geraete-spezifisch, nicht ueber Geraete uebertragbar | Spieler wird beim Verlassen aufgefordert, Account zu verknuepfen |
| Google Play Games | Cloud-Save, geraetuebergreifend | Empfohlen (Default-CTA nach 3 Sessions) |
| E-Mail/Passwort | Cloud-Save, plattformuebergreifend | Optional, fuer iOS-Spaeter wichtig |

### 2.3 Server-Konzept

- **Server-Kapazitaet:** ca. 50.000 aktive Spieler pro Server (kalibriert nach Last-Test in Closed-Beta)
- **Server-Namen:** Mythische Wesen (z.B. *Poseidon*, *Athena*, *Phoenix*) — neue Server bei Bedarf
- **Server-Wechsel:** 1.000 Diamanten, max. 1x / 90 Tage (Cooldown mit Server-seitigem Timestamp)
- **Region:** EU/NA/SEA — Photon-Realtime-Regionen automatisch zugeordnet

---

## 3. Hauptbildschirm (Hub-Welt)

Animierte Fantasy-Stadtansicht mit klickbaren Gebaeuden. Parallax-Hintergrund (3-5 Schichten),
dezent animierte Elemente (Rauch, Voegel, Lichteffekte). **Layout:** Portrait (9:16).

### 3.1 Spieler-Info (oben links)

| Element | Beschreibung |
|---------|--------------|
| Avatar | Rund, 64 dp, antippbar → oeffnet Profil-Bildschirm |
| Avatar-Rahmen | Saison-/Achievement-abhaengig (Bronze/Silber/Gold/Platin/Diamant/Meister) |
| Spieler-Level | Klein im Avatar-Rahmen (z.B. "88") |
| Spielername | Mit Gilden-Tag in eckigen Klammern (z.B. "[KINGZ] Sperber") |
| Gilden-Tag | Maximal 5 Zeichen, vom Gildenleiter festgelegt |

### 3.2 Ressourcen-Leiste (oben, fix angeheftet)

| Symbol | Ressource | Format | Tap-Aktion |
|--------|-----------|--------|------------|
| Muenze | Gold | Mit Tausender-Trennung (18.487.900) | Tooltip: Quellen anzeigen |
| Diamant | Diamanten | Mit Tausender-Trennung (3.548) | Oeffnet Diamant-Shop |
| Blitz | Energie | "current/max" (z.B. "80/60", Bonus in Gruen) | Tooltip + Energie-Kauf-Modal |
| + (Energie) | Diamanten → Energie | Direkter Kauf-Button | Modal mit Mengen-Optionen |

**Energie-Regeneration:** 1 Energie pro 6 Min (10/h), max. 60 ohne Bonus.
**Energie-Cap-Strategie:** Spieler werden bei 60/60 nicht weiter aufgefuellt — also "stehlen" sie sich aktiv durch Login zurueck. Push-Notification bei "60/60 erreicht" wenn der Spieler offline ist (opt-in).

**Energie-Bonus-Quellen (kann ueber Cap):**
- Login-Boni (taeglich, Tempel)
- Quest-Belohnungen
- Energie-Trank (Item) aus Events
- Bezahlte Energie-Kaeufe (Diamanten)

Bonus-Energie wird zuerst verbraucht; danach faellt der Wert wieder auf max. 60.

### 3.3 Navigationsbuttons (rechts, vertikal gestapelt)

| Button | Farbe | Funktion |
|--------|-------|----------|
| Landkarte | Gruen/Gold | Welten-Karte fuer Story-Kaempfe |
| Zauberschmiede | Gruen | Karten craften, verbessern, tauschen |
| Arena | Rot | PvP-Rangliste & Kaempfe |
| Tempel | Braun/Gold | Taegliche Belohnungen, Login-Boni, Sieben-Tage-Bonus |
| Ankuendigung | Banner | News & Events, mit roter Badge bei neuen Inhalten |
| Wand der Ehre | Banner | Top-100-Spieler-Galerie pro Saison |

### 3.4 Untere Navigationsleiste (4 Tabs)

| Tab | Funktion |
|-----|----------|
| Menue | Settings, Hilfe, Support, Logout, Saison-Pass (ab Saison 2), Codex |
| Laden | Shop: Karten-Pakete, Diamanten, Sonderangebote, Saison-Pass |
| Deck | Deck-Verwaltung, Karten-Sammlung, Runen-Inventar |
| Freunde | Freundesliste, Anfragen, Nachrichten (Inbox) |

### 3.5 Hub-Welt Animationen

- Parallax-Bewegung bei Geraete-Tilt (Gyroscope, opt-in)
- Tageszeit-Variante: Morgen/Mittag/Abend/Nacht (lokale Geraetezeit, max. 4 Skyboxen)
- Saison-Skin: Halloween/Weihnachten/etc. ueberlagert Default-Hub fuer Eventzeitraum
- Klickbare Gebaeude (Schmiede, Tempel, Arena) zeigen Hover-Glow + Tap-Sound

---

## 4. Spieler-Profil & Charakter-System

### 4.1 Profil-Werte (Deck-Bildschirm, linke Spalte)

| Wert | Beschreibung | Sichtbarkeit |
|------|--------------|--------------|
| Spieler-Level | 1-150 (Soft-Cap), steigt durch EXP | Oeffentlich |
| EXP-Fortschritt | Balken + Zahl (z.B. 908.640 / 1.200.000) | Privat |
| Helden-HP | Basis 1.000 bei LV 1, +50/Spieler-Level | Im Kampf sichtbar |
| Deck-COST | Summe Mana-Kosten aller 10 Karten (z.B. 192) | Privat |
| Aktuelles Deck | Index 1-3 (3 Deck-Slots, weitere via Premium) | Privat |
| ATK gesamt | Summe ATK aller Deck-Karten | Privat |
| HP gesamt | Summe HP aller Deck-Karten | Privat |
| Server | z.B. "Poseidon" | Oeffentlich |
| Titel | Aus Gilde / Arena (z.B. "[KING]", "[KINGZ]", "Meister-S2") | Oeffentlich |

### 4.2 Spieler-Level-Kurve (EXP-Tabelle)

| Stufe | EXP zur naechsten | Kumuliert |
|-------|-------------------|-----------|
| 1 → 10 | 1.000 → 10.000 (linear) | ca. 50.000 |
| 10 → 30 | 15.000 → 80.000 (quadratisch sanft) | ca. 800.000 |
| 30 → 60 | 100.000 → 400.000 | ca. 6.000.000 |
| 60 → 100 | 500.000 → 1.500.000 | ca. 35.000.000 |
| 100 → 150 (Soft-Cap) | 1.500.000 → 5.000.000 | ca. 150.000.000 |

> Konkrete Formel (vorlaeufig): `EXP(n) = round(1000 * 1.08^n + 50 * n^2)`. Iteration nach Beta-Test.

### 4.3 Level-Up-Belohnungen

| Level | Freischaltung | Belohnung |
|-------|--------------|-----------|
| 5 | Hub-Tutorial abgeschlossen | 1 Karte-Pack (Common), 100 Diamanten |
| 10 | Erstes Deck speichern | 1 seltene Karte garantiert |
| 15 | Arena freigeschaltet | Arena-Eintrittspass + 5 Tickets |
| 20 | Rune-Slot 2 freigeschaltet | 1 Rare-Rune garantiert |
| 25 | Gilden-Beitritt moeglich | Gilden-Suchhilfe-Tutorial |
| 30 | Rune-Slot 3 freigeschaltet | 1 Epic-Rune garantiert |
| 40 | Rune-Slot 4 freigeschaltet | 1 Epic-Rune + 200 Diamanten |
| 50 | Klan-Match-Teilnahme erlaubt | Gilden-Banner-Item |
| 60 | Welt 5 (Abysstiefe) freigeschaltet | Karten-Pack-Bundle |
| 80 | Welt 7 (Sturmzitadelle) freigeschaltet | Legendary-Rune-Token |
| 100 | "Legendaer"-Titel | Exklusiver Avatar-Rahmen |

### 4.4 Deck-Verwaltung

- **Deck-Anzahl:** 3 Slots (frei), weitere 3 Slots via Premium-Pass oder Diamanten (500 pro Slot)
- **Max. Karten pro Deck:** 10
- **Deck-Wechsel:** Jederzeit ausserhalb des Kampfes, Wechsel-Cooldown 5 s in Arena
- **Auto-Build:** "Suggest"-Button — System schlaegt optimales Deck aus Sammlung vor (KPI: max ATK+HP innerhalb COST-Budget)
- **Deck-Sharing:** Decks koennen per Code (8 Zeichen) geteilt werden, Empfaenger braucht Karten selbst

### 4.5 Avatare & Profil-Bilder

| Quelle | Anzahl | Verfuegbarkeit |
|--------|--------|----------------|
| Premade-Avatare | 50 (5 pro Element/Rasse + Default) | Sofort ab LV 1 |
| Saison-Avatare | 1-2 pro Saison | Saison-exklusiv, danach Sammler-Item |
| Achievement-Avatare | ca. 10 | Permanent unlock (z.B. "Erster Welt-9-Sieg") |
| Premium-Avatare | 6 | Nur via Diamant-Kauf (300 Diamanten pro Avatar) |
| Custom-Avatare | nein (Phase 1) | Wegen Moderations-Aufwand gestrichen |

**Avatar-Rahmen:** Separates Belohnungssystem (Saison-Endrang, Achievements), kombinierbar mit jedem Avatar.

---

## 5. Das Karten-System

### 5.1 Aufbau einer Karte (UI-Layout)

| Position | Feld | Beschreibung |
|----------|------|--------------|
| Oben links | Karten-Level | LV 0-15, gold ab LV 15 |
| Oben links | Element-Symbol | Klein neben Level |
| Oben Mitte | Kartenname | Bis 18 Zeichen, mittiger TMP-Text |
| Oben rechts | COST | Mana-Kosten 1-10 (Standard 1-7) |
| Oben rechts | Sterne | 1-5 Sterne, je nach Seltenheit |
| Mitte links | Rundenwarten | Sanduhr-Icon mit Zahl (3-6 Runden Standard) |
| Unten links | ATK | Angriffskraft (z.B. 1.200) |
| Unten rechts | HP | Lebenspunkte (z.B. 1.800) |
| Detail-Modal | Rasse | Koenigreich, Demonist, Elfe, Daemon, Untoter, Maschine, Tier, Drache |
| Detail-Modal | Basis-Faehigkeit | Hauptfaehigkeit, ab LV 0 aktiv |
| Detail-Modal | 2. Faehigkeit | Ab LV 5 aktiv |
| Detail-Modal | 3. Faehigkeit | Ab LV 10 aktiv |
| Detail-Modal | Deck-Limit | "1/deck" oder "Begrenzt: 2" oder unbegrenzt |

### 5.2 Seltenheitsstufen

| Stufe | Sterne | Rahmenfarbe | Vorkommen | Karten-Pool ca. |
|-------|--------|-------------|-----------|-----------------|
| Gewoehnlich | 1 | Grau | Normal-Level (frueh) | 40 Karten |
| Ungewoehnlich | 2 | Gruen | Normal-Level (mittel/spaet) | 35 Karten |
| Selten | 3 | Blau | Boss LV 5-10 Classic-Profi | 30 Karten |
| Epic | 4 | Lila | Boss LV 5 (Gott), Events, Arena | 25 Karten |
| Legendaer | 5 | Gold | Boss LV 10 (Gott), Events, Gilden | 20 Karten |

**Ziel-Gesamt:** ca. 150 Karten zum Launch (Konzept), 30 zusaetzliche Karten pro Major-Saison.

### 5.3 Karten-Level System (LV 0 bis LV 15)

| Level | Kopien benoetigt | Upgrade-Stein-Typ | Stein-Anzahl | Gold-Kosten | Stat-Bonus |
|-------|-----------------|-------------------|--------------|-------------|-----------|
| LV 0 | Startzustand | — | — | — | Basiswerte |
| LV 1 | 0 | Common Scrap | 2 | 500 | +5 % ATK/HP |
| LV 2 | 0 | Common Scrap | 4 | 1.500 | +10 % |
| LV 3 | 0 | Common Scrap | 8 | 4.000 | +15 % |
| LV 4 | 0 | Common Scrap | 16 | 10.000 | +20 % |
| **LV 5** | **1 Kopie** | Rare Scrap | 4 | 25.000 | +25 % + 2. Faehigkeit |
| LV 6 | 0 | Rare Scrap | 8 | 50.000 | +30 % |
| LV 7 | 0 | Rare Scrap | 16 | 90.000 | +35 % |
| LV 8 | 0 | Rare Scrap | 32 | 150.000 | +40 % |
| LV 9 | 0 | Rare Scrap | 60 | 250.000 | +50 % |
| **LV 10** | **2 Kopien** | Epic Scrap | 10 | 500.000 | +55 % + 3. Faehigkeit |
| LV 11 | 0 | Epic Scrap | 25 | 800.000 | +58 % |
| LV 12 | 0 | Epic Scrap | 50 | 1.200.000 | +63 % |
| LV 13 | 0 | Epic Scrap | 100 | 2.000.000 | +68 % |
| LV 14 | 0 | Epic Scrap | 200 | 3.500.000 | +75 % |
| **LV 15** | **3 Kopien** | Legendary Scrap | 50 | 8.000.000 | +80 % + Goldener Rahmen |

> Werte sind Pilot-Vorschlag. Iteration nach Beta. Wichtig: Pfad LV 0 → 15 soll auf einer Common-Karte ca. 30 Tage Casual-Play dauern.

### 5.4 Faehigkeiten-System

Jede Karte hat 3 Faehigkeits-Slots. Faehigkeiten sind entweder **passiv** (immer aktiv) oder
**aktiv** (durch Rundenwarten ausgeloest).

| Faehigkeit | Freischaltung | Typ-Mix |
|-----------|---------------|---------|
| Basis-Faehigkeit | LV 0 | Meist passiv oder Auto-Spezialattacke |
| 2. Faehigkeit | LV 5 | Erweitert Basis-Faehigkeit oder neuer Trigger |
| 3. Faehigkeit | LV 10 | Endgame-Effekt (z.B. Eliminierung, Schadensimmunitaet) |

**Faehigkeits-Kategorien:**

| Kategorie | Beispiele |
|-----------|-----------|
| Schaden | Doppelschlag, Eliminierung, AoE, Pierce |
| Verteidigung | Schild, Heilung, Schadensreflexion, Unbegrenzte Schild |
| Kontrolle | Betaeubung, Schweigen, Frieren, Mana-Brand |
| Buff/Debuff | ATK-Up Allies, HP-Down Enemies, Element-Schwaeche |
| Synergien | Rassen-Bonus (alle Daemonen +20 % ATK), Element-Resonanz |

### 5.5 Karten-Beschraenkung (Deck-Konstruktion)

- **1/deck:** Karte darf nur 1x pro Deck (typisch fuer Epic/Legendaer mit starken Effekten)
- **Begrenzt: 2:** Max. 2x pro Deck (typisch Selten/Epic ohne Unique-Effekt)
- **Unbegrenzt:** Max 3x (faktisch limitiert durch Sammelbarkeit — max. 3 Kopien farmbar)

Designziel: Decks bestehen aus ca. 8 verschiedenen Karten + 2 Duplikate, statt 5 Karten × 2 Duplikate.

### 5.6 Karten-Sammlung & Materialien-Karten

**Spezielle Karten** koennen nur durch das Sammeln von **Materialien-Karten** freigeschaltet werden.
Die Material-IDs sind autoritativ in `Unity/Assets/_Project/Resources/Data/collections.json` gepflegt
und werden vom `CollectionService` (Game-Layer) gegen das Spieler-Inventar ausgewertet.

| Sammlung | Set-ID | Material-IDs (4-6) | Resultat-Karte | Resultat-Rarity |
|----------|--------|-------------------|----------------|-----------------|
| Weisses Herz | `white_heart` | `helle_sphaere`, `sterne_splitter`, `engelsfeder`, `heiliges_wasser` | `engelsritter` | Epic, Licht |
| Schwarzes Herz | `dark_heart` | `schattenscherbe`, `knochenstaub`, `daemonenbrand`, `pestodem`, `schreckensauge`, `schwarzes_herz_material` | `schattenfuerst` | Legendaer, Dunkel |
| Drachen-Set | `dragon_set` | `drachen_schuppe_rot`, `_blau`, `_gruen`, `_gold`, `_schwarz` | `elder_drache` | Legendaer, Alle |
| Maschinen-Kern | `machine_core` | `zahnrad`, `eisenkern`, `plasma_zelle`, `steuermodul` | `kriegsmaschine` | Epic, Licht |

**Material-Karten im Inventar** werden mit Praefix `material.` gespeichert (z.B.
`material.helle_sphaere`). ATK 1 / HP 1, nicht spielbar. Beim Exchange werden je eine Kopie
pro Material verbraucht, die Resultat-Karte landet als neue `CardInstance` im Inventar.

- **Materialien-Karten:** ATK 1 / HP 1, nicht spielbar, nur Tauschware
- **Exchange-Button:** Erscheint sobald alle benoetigt vorhanden — Tap fuer Belohnung
- **Quellen:** Boss-Drops (LV 5/LV 10 Welt-Bosse), Saison-Events, Gilden-Shop
- **UI:** Eigene Sub-Sektion in Karten-Sammlung, Progress "3/4", "5/6"

### 5.7 Karten-Sammelbarkeit

| Quelle | Verfuegbarkeit |
|--------|----------------|
| Welt-Kaempfe | Max. 3 Kopien pro Karte (Garantie + Drops) |
| Karten-Packs (Shop) | Zufaellige Auswahl gem. Pack-Tabelle |
| Events | Saison-exklusive Karten |
| Quest-Belohnungen | Selten/Epic-Karten gezielt |
| Gilden-Shop | 1-3 Epic-Karten pro Saison |
| Arena-Belohnungen | 1 Legendary garantiert in Top-100 |
| Craft (Zauberschmiede) | Universal Scraps → spezifische Karten |
| Karten-Tausch | Spieler-zu-Spieler (Gilde-intern Phase 1, Marktplatz ab Phase 2 — siehe 6.3) |
| Materialien-Tausch | Spezialkarten via Sets |

---

## 6. Craft- & Tauschsystem (Zauberschmiede)

Zentrum fuer Karten-Crafting, Upgrades und Spezial-Tauschaktionen.

### 6.1 Craft-System (Karten herstellen)

| Element | Beschreibung |
|---------|--------------|
| Karten-Liste | Alle craftbaren Karten (vertikal scrollbar, mit Filter nach Element/Rarity/Verfuegbar) |
| NO. X/Y | Globaler Craft-Zaehler (z.B. 20/90 — 20 von max. 90 weltweit gecraftet) |
| Universal Scraps | Craft-Waehrung (eine Waehrung fuer Craften, nicht zu verwechseln mit Upgrade-Steinen) |
| Gold-Kosten | Zusaetzliche Gold-Kosten pro Craft |
| Available | Gruener Stempel "AVAILABLE" wenn beide Bedingungen erfuellt |
| Craft-Button | Rechts oben, mit Bestaetigungs-Modal vor Verbrauch |
| Uses Universal Scraps first | Toggle: Wenn aktiv, werden Scraps vor Gold gewertet (Default: an) |

**Craft-Kosten (Pilot):**

| Karten-Rarity | Universal Scraps | Gold |
|---------------|-----------------|------|
| Gewoehnlich | 50 | 5.000 |
| Ungewoehnlich | 200 | 25.000 |
| Selten | 800 | 100.000 |
| Epic | 3.000 | 500.000 |
| Legendaer | 15.000 | 3.000.000 |

**Globales Craft-Limit (NO. X/Y):**
- Verhindert Power-Creep: nur eine bestimmte Anzahl pro Karte weltweit
- Y skaliert mit Saison (Saison 1: 90 Stueck pro Legendaer)
- Anzeige der "Restplaetze" erhoeht Dringlichkeit

### 6.2 Upgrade-Materialien (Upgrade-Steine)

**Wichtig:** Upgrade-Steine sind separate Ressource von Universal Scraps. Vier Stein-Typen:

| Stein-Typ | Symbol | Verwendung | Quellen |
|-----------|--------|------------|---------|
| Common Scrap | Grau | Karten LV 0 → 4 | Taegliche Quests, Welt-Kaempfe (1-3 / Sieg) |
| Rare Scrap | Lila | Karten LV 5 → 9 | Events, Arena (Bronze+), Boss-Drops |
| Epic Scrap | Orange/Braun | Karten LV 10 → 14 | Gilden-Shop, Saison-Belohnungen, Gott-Bosse |
| Legendary Scrap | Gold | Karten LV 15 | Top-Arena (Platin+), exklusive Events |

**Konvertierung (Zauberschmiede):**
- 10 Common Scraps → 1 Rare Scrap (eine Richtung, Verlust)
- 10 Rare Scraps → 1 Epic Scrap
- 10 Epic Scraps → 1 Legendary Scrap
- Diamanten-Direktkauf moeglich (Epic Scrap = 50 Diamanten, Legendary Scrap = 500)

### 6.3 Tauschsystem (Karten-Boerse)

Spieler-zu-Spieler-Tausch von Karten. Restriktionen verhindern Pay-to-Win-Botting.

| Restriktion | Wert |
|-------------|------|
| Nur Gilde-intern | Ja (Phase 1) |
| Karten-Level | Beide Karten muessen LV 0 sein |
| Cooldown pro Spieler | 1 Tausch / 7 Tage |
| Seltenheit-Gleichheit | Selten ↔ Selten, Epic ↔ Epic. Cross-Rarity nicht erlaubt |
| Gebuehr | 10 % der Gold-Kosten der Karten-Craft-Kosten |
| Legendaer-Tausch | Erst ab Spieler-Level 60 |

**Tausch-Marktplatz UI:**
- Eigene Sub-Page in Gilden-Bereich (nicht Schmiede)
- Spieler postet "Anfrage" (will: Karte X, biete: Karte Y)
- Andere Gilden-Mitglieder sehen Anfragen, koennen annehmen
- Anfrage gilt 24h, danach automatisch zurueckgezogen

**Phase-2-Erweiterung (Marktplatz, Monat 18+):**
- Server-weiter Karten-Marktplatz mit Gold-Auktionen (Bid + Buyout)
- Listing-Gebuehr 5 % Gold, Verkaufs-Gebuehr 10 % Gold
- Cooldown gleich (1 Tausch + 1 Listing pro 7 Tage)
- Legendary nur via Direkt-Trade (nicht Auktion) zur Manipulation-Vermeidung

---

## 7. Das Runen-System

Runen werden in Slots auf dem Deck-Bildschirm eingesetzt und verstaerken den Helden sowie alle
Deck-Karten. Slots schalten sich mit Spieler-Level frei.

### 7.1 Runen-Slot Freischaltung

| Slot | Freischaltung |
|------|---------------|
| 1 | Spieler-Level 1 (Start) |
| 2 | Spieler-Level 20 |
| 3 | Spieler-Level 30 |
| 4 | Spieler-Level 40 |
| 5 | Spieler-Level 60 + aktiver Premium-Pass (Phase-2-Feature ab Saison 2) |

### 7.2 Runen-Typen

| Rune | Effekt | Seltenheit | Bonus-Range |
|------|--------|-----------|-------------|
| Angriffs-Rune | + X % ATK aller Deck-Karten | Gewoehnlich-Selten | 3-15 % |
| Verteidigungs-Rune | + X % HP aller Deck-Karten | Gewoehnlich-Selten | 3-15 % |
| Geschwindigkeits-Rune | -1 Rundenwarten bei Spezialattacken | Selten-Epic | -1 (selten) / -1 + 5 % Wiederaufladung (epic) |
| Element-Rune (5 Varianten) | + X % Schaden bei passendem Element | Selten-Epic | 10-25 % |
| Hero-Rune | + X Helden-HP | Epic-Legendaer | +500 / +1.500 |
| Kombo-Rune | Kombinations-Effekte (z.B. "+10 % ATK wenn 3+ Daemonen im Deck") | Epic-Legendaer | Set-spezifisch |
| Mana-Rune | +1 Start-Mana im Kampf | Legendaer | Saison-2-exklusiv |

### 7.3 Runen-Leveling

Runen koennen aufgeleveld werden (max. LV 10):
- 2 gleiche Runen + Gold → 1 LV+1 Rune
- Bonus skaliert linear (z.B. LV 1 = +5 % ATK → LV 10 = +12 % ATK)
- Bei Misserfolg (10 % bis LV 5, 30 % danach) bleibt Rune auf altem Level, Material verloren
- Schutz-Item ("Runen-Stein") verhindert Misserfolg, kostet Diamanten/Event-Items

### 7.4 Runen erhalten

| Quelle | Typ |
|--------|-----|
| Welt-Kaempfe | Zufaellig Gewoehnlich-Ungewoehnlich |
| Arena-Saison | Selten-Legendaer je nach Rang |
| Events | Exklusive Event-Runen (oft mit Set-Bonus) |
| Gilden-Shop | Selten-Epic gegen Gildenpunkte |
| Mythische Beschwoerung (Diamanten) | Garantiert mind. Epic, 5 % Legendaer |

---

## 8. Welten & Level-Karte

Die Welten-Karte ist das Herzstueck des Einzelspieler-Inhalts. Jede Welt hat eine malerische
Karte mit benannten Orten (Nodes), die durch Wege verbunden sind.

### 8.1 Aufbau einer Welt

| Element | Beschreibung |
|---------|--------------|
| Kapitel-Tabs | Oben scrollbar (1-9 Welten, mit Endgame-Erweiterung > 9 fuer Saison-Content) |
| Welt-Name | Thematischer Name (z.B. *Galaxy Wald*) |
| Gesamt-Sterne | z.B. "34/36" — alle in dieser Welt erreichbaren Sterne |
| Welt-Einnahmen | Gold-Gesamteinnahmen der Welt (z.B. 35.700 — fuer Vollstaendigkeit) |
| Energie-Anzeige | Unten links, Bonus-Energie gruen markiert (z.B. 80/60) |
| Level-Nodes | Benannte Orte mit Nummer (z.B. *Dimension Tuer 17-1*) |
| Schloss-Symbol | Naechste Welt gesperrt bis aktuelle abgeschlossen (mindestens 1 Stern pro Node) |
| Laden-Button | Shop direkt aus der Weltkarte aufrufen (Bequemlichkeit) |
| Deck-Button | Deck direkt aus der Weltkarte wechseln |
| Boss-Marker | Lila-pulsierender Marker bei LV 5 und LV 10 (Boss-Levels) |

### 8.2 Welten-Aufbau

Jede Welt hat genau **10 Nodes**:

| Node | Typ | Belohnung |
|------|-----|-----------|
| 1-4 | Normal-Kampf | Gewoehnliche/Ungewoehnliche Karten + Common Scrap + Gold |
| 5 | **MINI-BOSS** | Ungewoehnliche/Seltene Karte + Rare Scrap + Material-Karte (Set) |
| 6-9 | Normal-Kampf | Gewoehnliche/Ungewoehnliche Karten + Common Scrap + Gold |
| 10 | **WELT-BOSS** | Seltene Karte + Rare Scrap + Saison-Token + (bei 4 Sterne) Legendaer-Karte garantiert |

### 8.3 Level-Typen & Belohnungs-Garantien

| Level-Typ | Sterne | Garantierte Belohnung | Bonus bei 4 Sterne (Gott-Modus) |
|-----------|--------|----------------------|-------------------------------|
| Normal 1-4 | 1-4 | Gewoehnliche Karte | + Ungewoehnliche Karte |
| Normal 5 (Mini-Boss) | 1-4 | Ungewoehnliche/Seltene Karte | + EPIC-Karte (Drop-Chance 100 % bei 4 Sternen) |
| Normal 6-9 | 1-4 | Gewoehnliche/Ungewoehnliche Karte | + Seltene Karte |
| Welt-Boss 10 | 1-4 | Seltene Karte + Material-Karte | + LEGENDAERE Karte (Drop-Chance 100 % bei 4 Sternen) |

### 8.4 Sterne-System (Schwierigkeit pro Level)

| Sterne | Schwierigkeit | Gegner | Energie | Erst-Belohnung |
|--------|---------------|--------|---------|---------------|
| 1 Stern | Classic | Basis-Deck, kein Element-Bonus | 1 | + 50 Gold + 10 EXP |
| 2 Sterne | Amateur | Mehr HP (+50 %), staerkere Karten | 1 | + 100 Gold + 25 EXP |
| 3 Sterne | Profi | Spezialfaehigkeiten aktiv | 2 | + 200 Gold + 50 EXP |
| 4 Sterne | Gott | Elite-Deck, Phasen-Boss bei Level 5/10 | 3 | + 500 Gold + 100 EXP |

Spieler muss Stern-Stufe nacheinander freischalten — kein direkter Gott-Modus bei LV 1 moeglich.

### 8.5 Welten-Uebersicht (9 Welten zum Launch)

| # | Name | Element | Thema | Empfohlenes Spieler-Level |
|---|------|---------|-------|--------------------------|
| 1 | Elderwald | Natur | Waldwesen, Tiere, Pilzriesen | 1 |
| 2 | Sandreich | Feuer | Wuestenkrieger, Sandgolems | 8 |
| 3 | Vulkanhort | Feuer | Daemonen, Lavawesen | 18 |
| 4 | Frostgipfel | Wasser | Eisriesen, Schneegoettinnen | 30 |
| 5 | Abysstiefe | Wasser | Meeresmonster, Krakenwesen | 50 |
| 6 | Schattenlande | Dunkel | Untote, Vampirfuersten | 65 |
| 7 | Sturmzitadelle | Licht | Maschinenkrieger, Blitzgoetter | 80 |
| 8 | Galaxy Wald | Alle | Dimensionswesen, kosmische Monster | 95 |
| 9 | Drachenfeste | Alle | Drachen aller Art (Endregion) | 110 |

### 8.6 Welten-Wiederholung & Energie-Investment

- Pro Sterne-Stufe einmal gewonnen: Belohnung garantiert
- Wiederholung mit 1 Stern: 50 % Belohnung (nur Gold + EXP, keine Karten)
- "Sweep"-Funktion (ab Welt-Boss 3 Sterne): Verbrauche Energie, ueberspringe Animation, erhalte Belohnung sofort. Limit: 5 Sweeps/Tag pro Level

---

## 9. Das Kampfsystem

### 9.1 Kampf-Bildschirm Layout (Landscape oder Portrait?)

> **Designentscheidung:** Portrait — konsistent mit Hub, One-Handed-Use auf Mobile.
> Lesbarkeit der Gegner-Karten wird durch Long-Press (Karte gross einblenden) sichergestellt.

```
+-----------------------------------------------+
| [Runde 21]                          [Auto] [⏩] |  <- Top-HUD: Rundenzaehler + Auto + Skip
|                                                |
|  +-Gegner Avatar+    [Gegner-Karten Reihe]    |  <- 1 grosse Gegner-HP-Kreis + bis 5 Karten
|  | LV 88 HP 12k|     [G1] [G2] [G3] [G4] [G5] |
|  +--------------+    (ATK/HP unter jeder)      |
|                                                |
|  =====[ Kampf-Arena, animiert ]=========       |  <- Effekte, Spell-Animationen
|                                                |
|  +-Spieler Avatar    [Spieler-Karten Reihe]   |
|  | LV 89 HP 15k     [S1] [S2] [S3] [S4] [S5] |
|  +-------------+     (ATK/HP unter jeder)      |
|                                                |
|  [Mana: ●●●○○○○○○○]              [End Turn]   |  <- Mana-Orbs + End-Turn-Button
|                                                |
|  [Karte] [Karte] [Karte] [Karte] [Karte]      |  <- Handkarten (max 5)
+-----------------------------------------------+
```

| Bereich | Beschreibung |
|---------|--------------|
| Top-HUD | Rundenzaehler, Auto-Kampf-Toggle, Skip-Turn |
| Gegner-Avatar | Bild, Level, HP-Kreis, Effekt-Icons (Buffs/Debuffs) |
| Gegner-Karten | Bis 5 Karten im Feld, ATK/HP sichtbar, Spezialattacken-Cooldown |
| Kampf-Arena | Animierter Hintergrund, FX, Damage-Numbers fliegen |
| Spieler-Karten | Eigene 5 Karten im Feld, ATK/HP, Spezialattacken-Cooldown |
| Spieler-Avatar | Bild, Level, HP-Kreis, Helden-Faehigkeit (Tap-Button, siehe 9.6) |
| Mana-Orbs | Bis 10 Slots, gefuellte Orbs = verfuegbares Mana |
| End-Turn-Button | Beendet eigenen Zug (oder Auto-End nach 60 s) |
| Handkarten | Bis 5 Karten in der Hand, drag-and-drop aufs Feld |

### 9.2 Kampf-Ablauf

1. **Vorbereitung:** Deck (10 Karten) gewaehlt, Runen geladen.
2. **Initialisierung:** Beide Spieler ziehen 4 Karten, Start-Mana 3.
3. **Runden-Struktur (Spieler zuerst, dann Gegner):**
   - Mana regeneriert (+1 pro Runde bis max. 10)
   - 1 Karte ziehen
   - Karten ausspielen (Drag aus Hand aufs Feld; Mana-Kosten abgezogen)
   - Karten im Feld greifen automatisch an (Standard-Attacke jede Runde)
   - Rundenwarten-Zaehler aller Karten zaehlt herunter — bei 0 zuendet Spezialattacke
   - End Turn → Gegner ist dran
4. **Sieg-Bedingung:** Spieler-Held auf 0 HP → Niederlage. Beide Decks erschoepft → Held mit hoeherem HP gewinnt.
5. **Maximale Runden:** 50 (danach Sudden-Death: doppelte Schaden) — Anti-Stalemate.

### 9.3 Element-System (vollstaendige Matrix)

| Angreifer ↓ \ Verteidiger → | Natur | Feuer | Wasser | Licht | Dunkel |
|---------------------------|-------|-------|--------|-------|--------|
| Natur | — | Schwach (×0,75) | Stark (×1,5) | Neutral | Neutral |
| Feuer | Stark (×1,5) | — | Schwach (×0,75) | Neutral | Neutral |
| Wasser | Schwach (×0,75) | Stark (×1,5) | — | Neutral | Neutral |
| Licht | Neutral | Neutral | Neutral | — | Stark (×1,5) |
| Dunkel | Neutral | Neutral | Neutral | Schwach (×0,75) | — |

> **v5.1-Korrektur:** v5.0 hatte nur 6 Eintraege und liess Licht/Dunkel-Interaktion mit Natur/Feuer/Wasser undefiniert. v5.1 setzt diese auf Neutral.

**Designentscheidung:** Licht/Dunkel sind ein eigener "Achsen-Konflikt" abseits des Natur/Feuer/Wasser-Dreiecks.

### 9.4 Boss-Kaempfe (LV 5 & LV 10)

Bosse haben **Phasen** — bei < 50 % HP aktiviert Phase 2 mit Spezialfaehigkeit + neuen Karten im Feld.

| Boss-Typ | Phase-1-Verhalten | Phase-2-Verhalten |
|----------|-------------------|-------------------|
| LV 5 Mini-Boss | Normal-Kampf, etwas mehr ATK/HP | Aktiviert AoE-Attacke alle 3 Runden |
| LV 10 Welt-Boss | Mehrere starke Karten + Boss-Karte | Boss-Karte zueckt Ultimate (1.5x Schaden) + ruft 2 zusaetzliche Karten |

**Boss-Drops (Garantien):**
- LV 5 (Gott-Modus): 1 Epic-Karte garantiert
- LV 10 (Gott-Modus): 1 Legendaer-Karte garantiert
- Boss-Sieg bei 1-3 Sternen: Material-Karte mit erhoehter Drop-Chance

### 9.5 Auto-Kampf & Skip

- **Auto-Kampf:** Spielt Karten nach KI-Strategie automatisch (Greedy: hoechste ATK zuerst, Mana effizient nutzen). Spieler kann jederzeit eingreifen.
- **Skip-Turn:** Beendet sofort eigene Runde (z.B. wenn keine Karten spielbar)
- **Schnell-Kampf (Sweep):** Animation ueberspringen, sofort Ergebnis. Nur fuer wiederholte Levels mit 3+ Sternen verfuegbar.
- **Auto-Kampf-Belohnung:** Identisch zum manuellen Kampf, kein Belohnungs-Penalty

### 9.6 Spieler-Held (Hero) Faehigkeiten

Der Spieler-Held hat eine eigene Faehigkeit, die per Tap aktiviert wird.

- **Held-Auswahl:** 1 aus 6 Helden bei Account-Erstellung (Wechsel via Diamanten nach 30 Tagen)
- **Helden-Liste (Launch):**

| Held | Element | Faehigkeit | Cooldown |
|------|---------|------------|----------|
| Lichtkoenigin Aurelia | Licht | Allies +30 % HP (Heilung) | 5 Runden |
| Frostmagier Velgar | Wasser | -2 Mana fuer Gegner sofort | 6 Runden |
| Flammenmeister Zarok | Feuer | AoE 500 Schaden auf Gegner-Reihe | 7 Runden |
| Drachenfuerst Mortis | Dunkel | Spielt eine Random-Karte aus dem Deck kostenlos | 8 Runden |
| Druiden-Schamane Eolyn | Natur | Allies +1 Mana sofort | 5 Runden |
| Maschinenfuerst Cog | Licht | Karte ziehen + +1 max Mana fuer 3 Runden | 6 Runden |

Faehigkeit kostet kein Mana, nur den Cooldown. Anzeige im Spieler-Avatar-Bereich. Pilot ueber Beta evaluiert — bei < 5 % Nutzung wird das Feature gestrichen.

---

## 10. Dieb-System (World Event)

Server-weites Coop-Event: Zufaellig erscheinen Daemon-Diebe. Alle Spieler des Servers koennen
gemeinsam angreifen, um Belohnungen zu erhalten.

### 10.1 Dieb-Spawn-Mechanik

| Aspekt | Wert |
|--------|------|
| Spawn-Frequenz | Alle 4-6 Stunden (zufaellig) |
| Notification | Push fuer alle Server-Spieler (opt-in), In-App-Banner |
| Sichtbarkeit | Pop-up "Dieb erschienen!" mit Tap → direkt zum Dieb-Bildschirm |
| Aktive Zeit | 2 Stunden, danach flueht der Dieb |
| Max. gleichzeitig | 3 Diebe pro Server |

### 10.2 Dieb-Typen

| Typ | Farbe | Schwierigkeit | HP-Pool | Belohnung |
|-----|-------|--------------|---------|-----------|
| Mysterioeser Dieb | Blau/Gruen | Normal | 15.000 × Server-Aktivitaet | Gold (+50-200), Common Scraps, Karten-Fragmente |
| Elitedieb | Lila | Schwer | 80.000 × Server-Aktivitaet | Rare Scraps, Selten-Karten, Runen-Fragmente |
| Legendary Dieb | Gold | Extrem schwer | 500.000 × Server-Aktivitaet | Epic Scraps, Epic/Legendaer-Karten, exklusive Runen |

> **Server-Aktivitaets-Multiplikator** (DAU = Daily Active Users der letzten 24h):
> - DAU < 1.000: Faktor 0,4
> - DAU 1.000 - 5.000: Faktor 0,6 + (DAU - 1000) / 10.000
> - DAU 5.000 - 20.000: Faktor 1,0 + (DAU - 5000) / 30.000
> - DAU > 20.000: Faktor 1,5 (Cap)
> Wird durch Cloud Function alle 4 Stunden aktualisiert. Kalibrierung nach Closed-Beta.

### 10.3 Dieb-Bildschirm

| Element | Beschreibung |
|---------|--------------|
| Dieb-Bild | Grosses Artwork mit Name (z.B. "Mysterioeser Dieb") |
| Level | Schwierigkeitsstufe (z.B. Level 58) |
| Daemon-Lebenspunkte | Gesamte HP (z.B. 16.724 HP) |
| HP-Balken | Live-Update fuer alle via Photon Realtime Room (1 Room pro Dieb, Master = Cloud Function) |
| Timer | Verbleibende Aktive-Zeit (z.B. 1:59:34) |
| Kampf-Button | Eigenen Angriff starten |
| Entdeckt von | Spieler-Name + Avatar |
| Letzte Attacke | Spieler-Name + Schaden + Zeitstempel |
| Kampfrunden | Anzahl bereits gefuehrte Angriffe (global) |
| Top-Attacker | Top 10 Schaden in Echtzeit |
| Aktualisieren | Refresh-Button (Auto-Refresh alle 30 s) |
| Anfreunden | Mitspieler direkt als Freund hinzufuegen |

### 10.4 Belohnungs-Verteilung

| Spieler-Beitrag | Belohnung |
|----------------|-----------|
| 0-1 % vom Gesamt-Schaden | "Trostpreis": 50 Gold, 1 Common Scrap |
| 1-5 % | Basis-Drop (Karten-Fragment, Rare Scrap) |
| 5-10 % | Standard-Drop (1 Karte garantiert) |
| 10-25 % | Erhoeht (1 Karte + Rune) |
| 25-50 % | Premium (Epic-Karte + Epic-Rune) |
| Top-1-Schaden | Bonus (Legendary-Drop bei Legendary-Dieb) |

**Anti-Bot:** Spieler kann max. 10 Angriffe pro Dieb starten, jeder Angriff verbraucht 5 Energie.

---

## 11. Arena (PvP)

Asynchrones PvP: Spieler kaempfen gegen das **Deck eines anderen Spielers**, der von der KI
gesteuert wird (kein Live-PvP wegen Mobile-Connection-Stabilitaet).

> **Designentscheidung:** Async-PvP (Marvel Snap-Style) statt Live (Hearthstone-Style). Phase 2 (Monat 18+): zusaetzlicher Live-Modus fuer Top-100-Spieler via Photon Fusion (deterministische Simulation).

### 11.1 Arena-Rangliste

| Element | Beschreibung |
|---------|--------------|
| Rang | Position (z.B. Rang 28) — Top 100 mit Hervorhebung |
| Avatar + Name | Spielerbild, Gilden-Tag, Name |
| Level | Spieler-Level |
| Siegesrate | Gewinnquote (z.B. 26 %) |
| Kampfanzahl | Anzahl gespielte Arena-Kaempfe |
| Buttons | "Mein Rang" (springt zur eigenen Position), "Zur Spitze" (Rang 1), "Rangpunkte" (Saison-Fortschritt) |

### 11.2 Matchmaking

| Aspekt | Wert |
|--------|------|
| Algorithmus | Glicko-2 (Rangpunkte als Rating) |
| Match-Pool | ±150 Rangpunkte initial, erweitert sich nach 30 s Such-Zeit |
| Cooldown | 30 s zwischen Kaempfen |
| Eintritts-Energie | 5 Energie pro Arena-Kampf |
| Eintritts-Tickets | Alternative zu Energie: 1 Arena-Ticket / Kampf, taeglich 5 gratis + Quest-Bonus + Pack-Bonus. Tickets verfallen nach 30 Tagen. |

### 11.3 Rang-Punkte-System

| Ergebnis | Aenderung |
|----------|-----------|
| Sieg (gegen gleichen Rang) | +25 |
| Sieg (gegen schlechteren Rang) | +10 bis +20 |
| Sieg (gegen besseren Rang) | +30 bis +50 |
| Niederlage (gegen gleichen Rang) | -20 |
| Niederlage (gegen schlechteren Rang) | -30 bis -50 |
| Niederlage (gegen besseren Rang) | -5 bis -15 |
| Verbindungsabbruch / Aufgabe | -50 (Anti-Rage-Quit) |

### 11.4 Saison-System

| Aspekt | Wert |
|--------|------|
| Saison-Dauer | 30 Tage |
| Saison-Ende | Letzter Tag, 23:59 UTC |
| Saison-Reset | Rangpunkte werden um 25 % reduziert (statt komplett zurueck), Belohnungen nach Endrang |
| Belohnungs-Verteilung | 24h nach Saison-Ende |

### 11.5 Saison-Belohnungen

| Rang | Saison-Belohnung |
|------|------------------|
| Bronze (0-499 Punkte) | 5.000 Gold + 3 Gewoehnliche Karten + 10 Common Scraps |
| Silber (500-999) | 15.000 Gold + 3 Ungewoehnliche Karten + 5 Rare Scraps + 1 Gewoehnliche Rune |
| Gold (1.000-1.499) | 50.000 Gold + 2 Seltene Karten + 10 Rare Scraps + 1 Selten Rune |
| Platin (1.500-2.499) | 200.000 Gold + 1 Epic-Karte + 5 Epic Scraps + 1 Epic-Rune |
| Diamant (2.500-3.499) | 500.000 Gold + 1 Epic-Karte + 1 Legendaer-Karte + 1 Legendary Scrap + 1 Epic-Rune |
| Meister (Top 100) | 1.500.000 Gold + 2 Legendaer-Karten + 2 Legendary Scraps + 1 Legendaer-Rune + Saison-Avatar-Rahmen + Titel "Meister-S{N}" |

### 11.6 Schlachtbericht

| Element | Beschreibung |
|---------|--------------|
| Ergebnis | WIN/LOST mit Punkte-Aenderung |
| Rang-Aenderung | Position vorher → nachher (z.B. 31 → 28) |
| Gegner-Daten | Avatar, Name, Level, Deck-Zusammenfassung |
| Kampf-Replay | Optional ansehbar (Snapshot-basiert, kein Live-Replay) |
| Stats | Schaden, ueberlebte Runden, Spezial-Attacken |

---

## 12. Gilden-System

Soziales Endgame. Gilden ermoeglichen Klan-Matches, Coop-Bonuses, geteilte Technologie und
Karten-Tausch.

### 12.1 Gilden-Grundparameter

| Aspekt | Wert |
|--------|------|
| Max. Mitglieder | 30 (LV 1) → 50 (LV 10) |
| Mindestlevel-Spieler | 25 |
| Gruendungskosten | 50.000 Gold |
| Gilden-Tag | 5 Zeichen, einmalig vergeben (nicht aenderbar nach Gruendung) |
| Gilden-Name | Bis 20 Zeichen, einmalig vergeben |
| Inaktivitaets-Kick | 7 Tage ohne Login → automatisch kickbar |

### 12.2 Gilden-Uebersicht (5 Tabs)

| Tab | Inhalt |
|-----|--------|
| Clan List | Alle Gilden: Name, Level, Anfuehrer, Mitglieder-Anzahl, Slogan, Beitritts-Modus (offen/auf Anfrage/geschlossen) |
| My Clan | Eigene Gilde: Details, Level, Beschreibung, Beitragsuebersicht |
| Clan Member | Mitgliederliste: Name, Position, Beitrag, Stufe, Eintrittsdatum, Letzte Anmeldung |
| Technology | Gilden-Technologie-Baum (gemeinsame Upgrades, kostet Gilden-Beitragspunkte) |
| Application | Beitrittsantraege: Agree (Annehmen) / Refuse (Ablehnen) |

### 12.3 Mitglieder-Verwaltung

| Position | Rechte |
|----------|--------|
| Leader (1) | Alles: Kick, Promote, Demote, Gilde aufloesen, Tag/Name aendern (nur Slogan), Tech-Upgrade |
| Officer (max. 3) | Kick (Member), Beitrittsantraege bearbeiten, Tech-Upgrade vorschlagen |
| Veteran (max. 5) | Kann Tausch-Anfragen bestaetigen |
| Member | Beitragen, Chat, Tausch-Anfragen stellen, Klan-Match teilnehmen |

| Feld | Beschreibung |
|------|--------------|
| Beitrag | Individuelle Gildenpunkte (z.B. 22.900). **Saisonal-Score** (resetbar fuer Mitglieder-Ranking, alle 30 Tage) + **Total-Score** (kumuliert, nicht resetbar, fuer Gilden-Treasury und Tech-Tree). |
| Stufe | Spieler-Level |
| Eintrittsdatum | Wann beigetreten |
| Letzte Anmeldung | Wichtig fuer Inaktivitaets-Pruefung |

### 12.4 Gilden-Aktionen

| Aktion | Beschreibung |
|--------|--------------|
| Anfreunden | Mitglied als Freund hinzufuegen |
| Kick | Inaktives/problematisches Mitglied entfernen |
| Leiter transferieren | Fuehrerschaft uebergeben (mit Bestaetigungsdialog 24h) |
| Aus dem Klan austreten | Cooldown 24 h vor naechstem Gilden-Beitritt |
| Beitrittsantrag | Auf Anfrage: Antrag senden + Nachricht |

### 12.5 Gilden-Levels & Tech-Tree

| Gilden-Level | Aufstieg-Bedingung | Bonus |
|--------------|-------------------|-------|
| 1 | Start | 30 Mitglieder, Chat, Klan-Match Teilnahme |
| 2 | 100.000 Gildenpunkte | +5 % Gold aus Welt-Kaempfen (alle Mitglieder) |
| 3 | 500.000 Gildenpunkte | +5 % EXP, Tech-Tree freigeschalten |
| 5 | 5.000.000 | 40 Mitglieder, +10 % Klan-Match-Belohnungen |
| 10 | 50.000.000 | 50 Mitglieder, exklusives Gilden-Banner, +20 % Boni |

### 12.6 Gildenpunkte

Spieler verdienen Gildenpunkte durch Aktivitaet, sie fliessen in die Gilden-Kasse:

| Aktivitaet | Gildenpunkte |
|-----------|--------------|
| Taeglicher Login | +10 |
| Welt-Kampf gewonnen | +5 |
| Arena-Sieg | +20 |
| Dieb-Beitrag | +50-500 (je nach Anteil) |
| Klan-Match-Sieg | +500 |

Gildenpunkte werden NICHT zurueckgesetzt (kumuliert lebenslang). Persoenlicher Beitrag wird saisonweise resetbar.

---

## 13. Gilden-Weltkarte & Klan-Matches

Separate Overworld-Ansicht: Gilden bieten auf Gebiete, gewinnen sie in Klan-Matches und erhalten
taegliche Boni.

### 13.1 Gilden-Weltkarte

| Element | Beschreibung |
|---------|--------------|
| Weltkarte | Grosse Overworld mit benannten Inseln/Gebieten |
| Gebiete | z.B. *Abendtundra*, *Schwarzbinge*, *Meeresreich*, *Himmelsreich*, *Uebungswald*, *Westwindinsel*, *Verbrannte Ebene*, *Schildkroeteninsel*, *Endzeitkamm*, *Jadewald* |
| Spieler-Icons | Andere Gilden-Spieler sichtbar (online/offline-Indikator) |
| Bevorstehende Matches | Liste oben links (z.B. *Abendtundra 19:50*, *Schwarzbinge 19:50*) |
| Reinzoomen | Hineinzoomen in Gebiete fuer Details |
| Aufnahme | Snapshot-Replays: BattleState wird pro Runde als JSON gespeichert (deterministische Engine erlaubt deterministisches Replay). Speicher pro Match ca. 50 KB, 30 Tage aufbewahrt. |

### 13.2 Klan-Match-Ablauf (Gebots-System)

**Schritt-fuer-Schritt:**

1. **Bietphase (3 Tage):**
   - Jede Gilde kann ein Gebot in Gold abgeben (Min. 50.000, Max. nach Gebiet)
   - Mehrere Gilden koennen auf dasselbe Gebiet bieten
   - Hoechstes Gebot gewinnt **automatisch ohne Match**, wenn keine andere Gilde mitbietet (Auktion-Style)

2. **Match-Vorbereitung (1 Tag):**
   - Bei mind. 2 Gilden mit gleichem Hoechstgebot: Klan-Match-Pop-up
   - Top 2 Bieter spielen das Match. Bei > 2 Bietern: Hoechste 2 spielen, andere bekommen Gebot zurueck.
   - Match-Zeit wird festgelegt (z.B. 2017-11-15 12:00)

3. **Match-Tag:**
   - Match beginnt zur festgelegten Zeit (z.B. 19:50)
   - **Format:** Best-of-9 (Single-Elimination, 9 Mitglieder pro Gilde)
   - Jeder Spieler kaempft 1x, mit eigenem Deck (kein Live-PvP, sondern Async wie Arena)
   - Gilde mit 5+ Siegen gewinnt

4. **Gewinn-Konsequenzen:**
   - Gewinner-Gilde kontrolliert das Gebiet (24 h Cooldown bevor neu gebietbar)
   - Gewinner-Gebot wird einbehalten (50 % an Server-Pool, 50 % an Verlierer als Trost)
   - Verlierer-Gebot wird zurueckerstattet (minus 10 % Bearbeitungsgebuehr)
   - Gewinner erhaelt taegliche Gold-Belohnung aus dem Gebiet

### 13.3 Gebiets-Boni (taegliche Einnahmen)

| Gebiet-Rarity | Tagesbonus pro Gilden-Mitglied | Gesamtertrag/Tag (50 Mitglieder) |
|---------------|-------------------------------|----------------------------------|
| Common (Uebungswald, Jadewald) | 1.000 Gold | 50.000 Gold |
| Rare (Westwindinsel, Schildkroeteninsel) | 3.000 Gold + 2 Common Scraps | 150.000 Gold + 100 Scraps |
| Epic (Schwarzbinge, Himmelsreich) | 8.000 Gold + 2 Rare Scraps + 50 Diamanten | 400.000 Gold + 100 Scraps + 2.500 Diamanten |
| Legendaer (Endzeitkamm, Verbrannte Ebene) | 20.000 Gold + 1 Epic Scrap + 100 Diamanten | 1.000.000 Gold + 50 Scraps + 5.000 Diamanten |

**Auszahlung:** Taeglich um 00:00 UTC, Gold landet in der Gilden-Kasse. Leader entscheidet ueber Verteilung (Auto-Split / Manuell).

### 13.4 Klan-Match Regelwerk

| Regel | Beschreibung |
|-------|--------------|
| Teilnahme | Nur Mitglieder mit Spieler-Level >= 50 |
| Deck-Sperre | Decks ab Match-Tag 00:00 gesperrt, kein Karten-Wechsel waehrend Match |
| Zeitfenster | Spieler muss innerhalb von 2 h nach Match-Start eingeloggt sein, sonst Auto-Loss |
| Mehrfach-Gebote | Eine Gilde kann gleichzeitig auf max. 3 Gebiete bieten |
| Gebiete-Limit | Eine Gilde haelt max. 5 Gebiete gleichzeitig |

---

## 14. Chat-System

| Kanal | Sichtbar fuer | Moderation |
|-------|--------------|------------|
| Alle (All-Feed) | Eigener Spieler | Aggregator-View aller Kanaele |
| Welt (World) | Alle Server-Spieler | Cooldown 30 s zwischen Messages, max. 200 Zeichen |
| Privat | Nur die 2 Spieler | Unbegrenzt, mit Block/Report-Funktion |
| Gilde (Clan) | Nur Gilden-Mitglieder | Officer kann Mute setzen |

### 14.1 System-Nachrichten

| Trigger | Format |
|---------|--------|
| Neuer Gilden-Beitritt | "Willkommen, {Spieler}!" (Gilden-Chat) |
| Gilden-Level-Up | "Clan {Name} ist nun Level {N}!" |
| Klan-Match-Ankuendigung | "Klan-Match {Gegner} um {Zeit}" |
| Server-Event | "Dieb erschienen!" / "Wartung in 30 Min" |

### 14.2 Chat-Features

| Feature | Beschreibung |
|---------|--------------|
| Emoji-Picker | Standard-Set (≈ 30 Emojis) |
| Karten-Link | "[Karte: Drachenherrscher]" → tippen oeffnet Karte-Detail im Modal |
| Spieler-Link | "@Name" → tippen oeffnet Profil im Modal |
| Voice-Notes | NICHT geplant — wegen Moderations-Aufwand auch in Phase 2 gestrichen. Frueheste Evaluierung Monat 30+. |
| Translation | Phase 2 (Monat 18+): Google Translate-API als Opt-in pro Welt/Privat-Chat. Gilden-Chat keine Translation (Gilden waehlen Sprache). |

### 14.3 Moderation

- **Wortfilter:** Server-seitig (mehrsprachiger Bad-Word-List)
- **Report-Button:** Pro Message verfuegbar, mit Grund (Spam, Belaestigung, Cheating)
- **Auto-Mute:** Bei 3 Reports innerhalb 24 h → 24 h Mute, manueller Moderator-Review

---

## 15. Merit-System & Ranglisten

Zusaetzlich zur Arena-Rangliste gibt es eine **Merit-Rangliste** fuer kumulierte Gesamtleistung und
eine **Leistungs-Rangliste** fuer Trophaeen.

### 15.1 Merit-Punkte

Merit-Punkte werden durch verschiedene Aktivitaeten gesammelt (kumuliert, nicht resetbar bis Cap 199.999):

| Aktivitaet | Merit-Punkte |
|-----------|--------------|
| Taegliche Quest abgeschlossen | +50 |
| Arena-Kampf | +5 (Sieg) / +1 (Niederlage) |
| Dieb-Beitrag (1+ %) | +20 (Mysterious) / +100 (Elite) / +500 (Legendary) |
| Gilden-Beitragsleistung | 0,01 % der Gilden-Beitragspunkte als Merit |
| Event-Abschluss | +500 - +5.000 je nach Event |
| Welt-Boss 4 Sterne | +200 pro neuem Boss |

**Cap:** 199.999 Punkte (Max-Rang-Status). Danach bleibt der Wert stehen, gibt aber Cosmetic-Badges.

### 15.2 Merit-Rangliste

| Element | Beschreibung |
|---------|--------------|
| Rang | Position (Rang 1, 2, 3...) |
| Spieler | Avatar, Name mit Gilden-Tag, Level |
| Merit-Punkte | Gesammelte Punkte |
| Top-Button | Springt zu Rang 1 |
| My Rank | Eigene Position |
| Rewards List | Belohnungen fuer Rang-Bereiche (siehe 15.3) |

### 15.3 Merit-Belohnungen (woechentlich, nach Top-Cutoffs)

| Rang | Belohnung |
|------|-----------|
| Top 1 | 1.000 Diamanten + Exklusiver Avatar |
| Top 2-10 | 500 Diamanten + Top-10-Avatar |
| Top 11-100 | 100 Diamanten + 1 Legendaere Karte |
| Top 101-1.000 | 50 Diamanten + 1 Epic-Karte |
| Top 1.001-10.000 | 10 Diamanten + 1 Selten-Karte |

### 15.4 Leistungs-Rangliste

Gesamtleistung im Spiel (kumulierte Trophaeen aus Quests, Achievements):
- Buttons: *Zur Spitze*, *Mein Rang*, *Trophaeen-Liste*
- Eigener Rang immer hervorgehoben in der Liste
- Belohnungen wie Merit-Liste, aber weniger Diamant-fokussiert (mehr Karten + Titel)

---

## 16. Langzeit-Content (Events & Quests)

### 16.1 Event-Typen

| Event-Typ | Frequenz | Beispiel | Belohnung |
|-----------|---------|----------|-----------|
| Saison-Event | 1x pro Jahreszeit | Halloween, Weihnachten | Exklusive Karten + Kosmetik |
| Story-Event | Quartalsweise | "Die Drachen-Verschwoerung" | Story-Karten, Gold, Lore |
| Turnier-Event | Monatlich | Wochenend-Turnier | Epic/Legendaere Karten |
| Klan-Match-Event | Woechentlich | Gebiets-Krieg | Gebiets-Kontrolle, Boni |
| Dieb-Event | Spontan | Wochenend-Legendary-Dieb | Seltene Karten, Runen |
| Doppel-Belohnung | Quartalsweise | "2x EXP-Wochenende" | 2x Gold/EXP temporaer |
| Login-Event | Permanent | Taeglich-Login-Belohnung | Karten, Gold, Diamanten (Sieben-Tage-Zyklus) |

### 16.2 Quests

| Quest-Typ | Beispiel | Reset | Belohnung |
|-----------|---------|-------|-----------|
| Taeglich | Gewinne 3 Kaempfe | 00:00 UTC | 200 Gold + 2 Common Scraps |
| Taeglich | Spiele 5 Feuerkarten | 00:00 UTC | 50 EXP + 1 Runen-Fragment |
| Taeglich | Logge dich ein | Sofort | 10 Diamanten |
| Woechentlich | Erreiche Profi (3 Sterne) auf 5 Leveln | Montag 00:00 UTC | 1 Seltene Karte |
| Woechentlich | Gewinne 10 Arena-Kaempfe | Montag 00:00 UTC | 1 Epic-Karte |
| Woechentlich | Spiele 50 Karten | Montag 00:00 UTC | 1.000 Diamanten + 1 Epic-Rune |
| Errungenschaft | Besiege 100 Bosse | Permanent | 1 Legendaere Karte + Titel |
| Errungenschaft | Erreiche Spieler-Level 100 | Permanent | Avatar-Rahmen + 1 Legendary Scrap |

### 16.3 Saison-Pass (ab Saison 2)

Implementierung in **Phase 2** (Monat 18+). Spezifikation final:

- Saison-Pass laeuft **30 Tage** parallel zur Arena-Saison
- **50 Stufen** mit Belohnungen, freischaltbar durch Saison-XP
- Saison-XP aus allen Aktivitaeten (Quests, Kaempfe, Events). Cap: 100 Stufen, dann nur noch Diamanten als Stufen-Belohnung
- **Free-Track:** Standard-Belohnungen (siehe Stufen-Tabelle unten)
- **Premium-Track:** + 1.500 Diamanten ueber 50 Stufen, 1 exklusive Saison-Karte, 1 Saison-Rune, doppelte Belohnungen, Saison-Avatar-Rahmen
- **Preis:** 9,99 EUR / Saison (oder 999 Diamanten)
- **Premium-Pass-Stufen-Skip:** 100 Diamanten pro Stufe (Whale-Conversion)

**Stufen-Beispiele (Free / Premium):**

| Stufe | Free-Belohnung | Premium-Belohnung (zusaetzlich) |
|-------|----------------|--------------------------------|
| 1 | 100 Gold | 50 Diamanten |
| 10 | 1 Selten-Karte | 1 Epic-Rune |
| 25 | 2 Epic Scraps | 1 Epic-Karte |
| 50 | 1 Epic-Karte | 1 Legendaer-Karte (saison-exklusiv) + Saison-Avatar-Rahmen |

### 16.4 Tutorial-Schritte (First-Time-User-Experience)

8 Schritte, Event-getriggert, einzeln skippbar (ausser Schritt 1). Komplette
Implementierung in `Unity/Assets/_Project/Resources/Data/tutorial.json`. Progress wird
im PlayerSave-Schema v2 persistiert.

| # | Step-ID | Trigger-Event | Highlight | Skippable | Inhalt (Kurz) |
|---|---------|---------------|-----------|-----------|---------------|
| 1 | `welcome` | `first_session_start` | — | nein | Splash + Tap-to-Begin |
| 2 | `hub` | `hub_entered` | Welt-Map-Button | ja | Erklaerung Hub-Navigation |
| 3 | `first_battle` | `battle_started` | Erste Karte | ja | Drag-and-Drop einer Karte |
| 4 | `deck_edit` | `first_battle_won` | Deck-Tab | ja | Deck mit 10 Karten anpassen |
| 5 | `first_pack` | `shop_entered` | Gratis Common Pack | ja | Pack-Oeffnen-Mechanik |
| 6 | `collection` | `material_card_obtained` | Sammlungen-Tab | ja | Erstmaliges Material-Drop |
| 7 | `arena` | `level_15_reached` | Arena-Button | ja | Arena ist verfuegbar |
| 8 | `guild` | `level_25_reached` | Gilden-Menue | ja | Gilden-Beitritt erklaeren |

**Reset-Zeiten (Quest-Reset, DESIGN-Praezisierung v5.3):**

| Reset-Typ | Zeitpunkt | Trigger im Code |
|-----------|-----------|------------------|
| Daily | 00:00 UTC | `SeasonResetService.CheckResetsAsync` bei Hub-Tick, `ResetWindow.HasCrossedDailyReset` |
| Weekly | Montag 00:00 UTC | analog mit `HasCrossedWeeklyReset` |
| Saison | Account-Erstellung + 30 Tage (Server-getrieben in Production) | `ResetWindow.NextSeasonResetUtc` |

---

## 17. Waehrungs- & Wirtschaftssystem

| Waehrung | Symbol | Quellen | Verwendung |
|----------|--------|---------|-----------|
| Gold | Muenze | Kaempfe, Quests, taegliche Belohnungen, Gebiets-Kontrolle | Craften, Upgrades, Klan-Gebote, Tausch-Gebuehr |
| Diamanten | Diamant | Hauptsaechlich Kauf, kleine Mengen durch Events/Quests/Achievements | Seltene Packs, Energie, Premium-Pass, Scrap-Direktkauf |
| Energie | Blitz | Regeneriert (10/h, Cap 60), kaufbar mit Diamanten | Welt-Kaempfe (1-3 / Kampf), Dieb-Angriffe (5 / Angriff), Arena (5 / Kampf) |
| Gildenpunkte | Gilden-Symbol | Gilden-Aktivitaeten & Beitraege | Gilden-Tech-Tree, Gilden-Shop |
| Universal Scraps | Scrap-Symbol | Quests, Events, Arena, Tausch (Karten zerlegen) | Karten craften |
| Common/Rare/Epic/Legendary Scrap | Stein-Symbole | Welt-Kaempfe, Boss-Drops, Events | Karten LV-Up (siehe 5.3) |
| Merit-Punkte | Merit-Symbol | Alle Aktivitaeten | Merit-Rangliste & Belohnungen |
| Arena-Tickets | Ticket | Taeglich 5 gratis, Pack-Bonus, Quests | Arena-Eintritt (Alternative zu Energie). Verfall nach 30 Tagen. |
| Material-Karten | Karten | Boss-Drops, Saison-Events | Spezialkarten-Tausch (Sets) |

### 17.1 Diamanten-Preise (Shop)

| Pack | Diamanten | Preis EUR | Wert/EUR |
|------|-----------|-----------|----------|
| Starter | 60 | 0,99 | 60 |
| Klein | 300 + 30 Bonus | 4,99 | 66 |
| Mittel | 980 + 150 Bonus | 14,99 | 75 |
| Gross | 1.980 + 400 Bonus | 29,99 | 79 |
| Riesig | 3.280 + 800 Bonus | 49,99 | 81 |
| Mega | 6.480 + 2.000 Bonus | 99,99 | 84 |

### 17.2 Diamant-Verwendungen (Pilot)

| Item | Diamanten |
|------|-----------|
| Energie 30 | 50 |
| Energie 60 | 90 |
| Common Pack | 50 (10 Karten, garantiert 1 Selten) |
| Rare Pack | 250 (10 Karten, garantiert 1 Epic) |
| Epic Pack | 1.000 (10 Karten, garantiert 1 Legendaer) |
| Saison-Pass | 999 |
| Deck-Slot 4 | 500 |
| Server-Wechsel | 1.000 |
| Rename | 200 |
| Mythische Beschwoerung (10x) | 2.000 (alle Epic+, 10 % Legendaer-Chance pro Karte) |

### 17.3 Anti-P2W-Massnahmen

- **Karten-Cap pro Pack:** Max. 1 Legendaere Karte pro Pack-Oeffnung (verhindert Whale-Stacking)
- **Soft-Cap auf Karten-Level:** Bis LV 10 kostenfrei farmbar (Common Scraps drop ueppig), LV 11-15 ist Endgame-Grind
- **Gebiete als Equalizer:** Aktive Gilden ohne Whales koennen Gebiete halten, kompensieren P2W
- **Arena-Matchmaking:** Glicko-2 stellt sicher, dass P2W-Decks gegen andere P2W-Decks spielen, nicht F2P

---

## 18. Technologie & Entwicklung

### 18.1 Engine: Unity 6

| Aspekt | Wert |
|--------|------|
| Version | Unity 6 (6000.4.8f1, kontinuierliche LTS-Linie) |
| Sprache | C# (.NET Standard 2.1) |
| Render-Pipeline | URP (Universal Render Pipeline) |
| UI-Stack | UI Toolkit (UIElements, USS, UXML) + UGUI fuer Animations-haeufige UI |
| Input | New Input System |
| Localization | com.unity.localization |
| DI-Container | VContainer (leichtgewichtig, AOT-kompatibel) |
| Async | UniTask (Allokations-arm, statt Coroutines/Tasks) |
| Build-Pipeline | Unity Cloud Build oder lokal mit BuildPipeline-Script |

### 18.2 Backend: Firebase + Photon

| Service | Verwendung |
|---------|-----------|
| Firebase Auth | Anonyme Auth, Google Play Games, E-Mail |
| Firebase Realtime Database | Spieler-Daten, Welt-Fortschritt, Gilden, Karten-Inventar |
| Firebase Cloud Messaging | Push: Energie voll, Dieb erschienen, Klan-Match-Ankuendigung |
| Firebase Remote Config | Live-Balancing-Aenderungen ohne App-Update |
| Firebase Analytics | KPI-Tracking (Retention, ARPDAU, Funnel) |
| Firebase Crashlytics | Crash-Reports |
| Photon Realtime / PUN 2 | Dieb-HP-Sync, Klan-Match-Match-Hub, Live-Chat |
| Photon Chat | Welt-Chat, Privat, Gilden-Chat |

> **Hinweis:** Live-PvP-Arena ist async (Marvel Snap-Style). Photon hier nur fuer Chat & Dieb-HP-Sync.
> Wenn spaeter Live-PvP gewuenscht: Photon Fusion fuer deterministische Kampfsimulation.

### 18.3 Asset-Pipeline

| Komponente | Tool |
|-----------|------|
| Karten-Art | Extern (Mid-Journey / Stable Diffusion / Illustratoren), Import als TGA |
| Karten-Frames | Unity Sprite-Atlanten pro Rarity-Stufe |
| Animationen | DOTween (Tween-Lib), Timeline fuer komplexe Sequences |
| Partikel | Built-in Particle System + Visual Effect Graph fuer Heavy-FX |
| Sound | Wwise (optional, sonst Unity Audio + Resonance Audio) |
| Loadout | Addressables (Karten-Art on-demand, Welten-Hintergruende per Welt-Switch) |

### 18.4 Build-Targets

| Target | Build-Settings |
|--------|---------------|
| Android Phase 1 | API 24+, ARM64 only (Play Store-Vorgabe), AAB, IL2CPP |
| Android Phase 2 | + ARM32 (Asia-Markt), Texture-Streaming aktiv |
| iOS Phase 2 | iOS 14+, ARM64 |

### 18.5 Tooling

- **Versionierung:** Git (vorhandenes Repo)
- **CI:** GitHub Actions + game-ci/unity-builder Action (siehe `.github/workflows/unity-android.yml`). Unity-License via GitHub Secret. Builds bei Push auf `main` und Tags.
- **Crash-Monitoring:** Crashlytics + Sentry (optional)
- **Telemetrie:** Firebase Analytics + Custom-Events fuer Game-Economy

---

## 19. Entwicklungs-Zeitplan (24 Monate)

> Aggressive Timeline. Realistisch fuer ein 2-5-Personen-Team. Solo-Dev wuerde 36+ Monate brauchen.

| Phase | Zeitraum | Meilenstein |
|-------|---------|-------------|
| Konzeption & GDD | Monat 1-2 | Alle Systeme & 50 Karten designed, GDD v6.0 |
| MVP: Kampfsystem | Monat 4-6 | Kartenkampf offline spielbar (Hot-Seat oder vs. KI) |
| MVP: Welten-Karte | Monat 6-8 | Welt 1 (Elderwald) + Sterne-System komplett |
| MVP: Karten & Runen | Monat 8-10 | Craft, Aufleveln, Runen funktional |
| Login & Profil | Monat 10-11 | Firebase Auth + Profil + 3 Decks |
| Online & Firebase | Monat 11-14 | Cloud-Save, Welt-Chat, Sync ueber Geraete |
| Arena & PvP | Monat 14-16 | Async-PvP, Rangliste, Schlachtberichte |
| Gilden-System | Monat 16-17 | Gilden online, Chat, Mitglieder-Verwaltung |
| Gilden-Weltkarte | Monat 17-18 | Klan-Matches, Gebots-System, Gebiete-Boni |
| Dieb-System | Monat 18-19 | World Events live, Belohnungs-Verteilung |
| Merit & Ranglisten | Monat 19-20 | Merit-System online, Saison-Belohnungen |
| Alle Welten | Monat 20-22 | Vollstaendige Karte (9 Welten, 150 Karten) |
| Closed Beta | Monat 22-23 | Geschlossener Beta-Test (500 Spieler) |
| Launch | Monat 23-24 | Google Play Soft-Launch (EU/SEA), dann Global |

---

## 20. Datenmodell-Skizze (Implementierungs-Sicht)

### 20.1 Core-Entitaeten (vereinfacht)

```
Player
├── id (Firebase UID)
├── displayName
├── server
├── level
├── exp
├── guildId
├── currencies: { gold, diamonds, energy, energyBonus, gildenPoints, meritPoints, universalScraps, commonScrap, rareScrap, epicScrap, legendaryScrap }
├── unlockedDecks: int (1-6)
├── decks: Deck[]  (1-6)
├── cardInventory: Map<cardId, CardInstance>
├── runeInventory: Map<runeId, RuneInstance>
├── worldProgress: Map<worldId, WorldProgress>
├── arenaRank: ArenaRank
├── achievements: Achievement[]
└── settings: PlayerSettings

Deck
├── slotIndex (0-5)
├── name
├── cards: cardInstanceId[]  (10)
├── runes: runeInstanceId[]  (1-4)
└── lastModified

CardInstance
├── cardDefinitionId  -> CardDefinition (ScriptableObject)
├── level (0-15)
├── exp (intra-level)
└── obtainedAt

CardDefinition (ScriptableObject)
├── id, displayName, description
├── element, rarity, race
├── cost
├── baseAtk, baseHp
├── turnsToSpecial
├── abilities: AbilityDefinition[]  (3 Slots)
├── deckLimit: { OneOnly | Limited2 | Unlimited }
├── globalCraftLimit (Y in NO. X/Y)
├── artworkAddressableKey
└── voiceLineAddressableKey

RuneInstance
├── runeDefinitionId
├── level (0-10)
└── obtainedAt

WorldProgress
├── worldId
├── stars: Map<nodeId, 0-4>
└── lastPlayed

Guild
├── id, name, tag, slogan
├── leaderId, officers[], members[]
├── level, contributionPoints
├── territoryIds[]
├── techTree: Map<techId, level>
├── joinPolicy: { Open | OnRequest | Closed }
└── createdAt
```

### 20.2 Firebase-Datenbank-Strategie

- **Realtime Database** fuer Spieler-Daten (low-latency-Reads, write-light)
- **Firestore** fuer Gilden, Klan-Matches, Marketplace (komplexe Queries)
- **Cloud Functions** fuer Anti-Cheat (Karten-Drops validieren, Saison-Belohnungen verteilen, Klan-Match-Resultate)
- **Storage** wird **nicht** fuer User-Generated Avatare verwendet (Custom-Avatare gestrichen, siehe 4.5). Nur fuer Asset-Bundles (Phase 2 Remote-Catalog) und Saison-Replays (siehe 13.1)

### 20.3 Save-System

- **Save-Strategie:** Inkrementelle Cloud-Saves nach jeder relevanten Aktion (Kampf-Sieg, Karten-Drop, Deck-Aenderung)
- **Offline-Modus:** Welt-Kaempfe spielbar offline, Sync bei naechstem Connect (mit Konflikt-Resolution: Server gewinnt)
- **Backup:** Server-seitiges Backup alle 24 h, Spieler kann auf 7-Tage-Snapshot zurueckspulen (via Support)

---

## 21. Game-Economy-Stellschrauben & Telemetrie

### 21.1 Stellschrauben fuer Live-Balancing (via Firebase Remote Config)

| Parameter | Typische Range |
|-----------|---------------|
| Card-Drop-Rate pro Welt-Node | 5-15 % Selten, 1-3 % Epic |
| Pack-Probabilities | Pro Rarity konfigurierbar, mit Pity-Counter |
| Energie-Regen-Rate | 8-12 / h |
| Arena-Belohnungs-Cooldown | 0,5 - 2 Stunden |
| Saison-XP-Multiplikator | 0,8 - 1,5 (fuer Event-Boosts) |
| Klan-Match-Min-Gebot | 10.000 - 100.000 |
| Diamant-Item-Preise | ±20 % |

### 21.2 Telemetrie-Events (Pilot-Liste)

| Event | Daten |
|-------|-------|
| `session_start` | timestamp, server, app_version |
| `world_battle_start` | world, node, stars, deck_cost |
| `world_battle_end` | result, duration, cards_played, mana_used |
| `card_drop` | card_id, source (world/pack/event), rarity |
| `card_level_up` | card_id, old_level, new_level, scraps_used |
| `arena_match` | result, rank_change, opponent_rank |
| `thief_attack` | thief_type, damage_dealt, position |
| `guild_join` | guild_id, contribution_at_join |
| `purchase` | item_id, price_eur, currency_in, currency_out |
| `tutorial_step` | step_id, completed, skipped |

---

## 22. Glossar

| Begriff | Bedeutung |
|---------|-----------|
| **Auto-Kampf** | KI-gesteuertes Spielen waehrend des Kampfes |
| **COST** | Mana-Kosten zum Ausspielen einer Karte |
| **Deck** | Set aus max. 10 Karten + bis 4 Runen |
| **Dieb** | Server-weites Coop-Event-Monster |
| **Energie** | Ressource fuer PvE-Kaempfe (regeneriert) |
| **Gildenpunkte** | Gilden-Beitragspunkte fuer Tech-Tree, Shop |
| **Hub-Welt** | Hauptbildschirm mit Navigations-Gebaeuden |
| **Klan-Match** | Gebiets-Kampf zwischen zwei Gilden |
| **Materialien-Karte** | Tausch-Karte (ATK 1 / HP 1), nicht spielbar |
| **Merit-Punkte** | Lebenslang kumulierte Aktivitaets-Punkte |
| **Mythische Beschwoerung** | Diamant-Pack mit Epic-Garantie |
| **Pity-Counter** | Garantie nach X Pack-Oeffnungen ohne Legendaer |
| **Rundenwarten** | Cooldown bis zur naechsten Spezialattacke einer Karte |
| **Saison** | 30-Tage-Zyklus fuer Arena + Pass |
| **Sweep** | Automatisches Wiederholen eines abgeschlossenen Levels |
| **Universal Scraps** | Craft-Waehrung (eine, fuer alle Karten) |
| **Upgrade-Stein** | Karten-Level-Material (Common/Rare/Epic/Legendary) |

---

## 23. Aenderungslog v5.0 → v5.1 → v5.2

### 23.1 Geschlossene Inkonsistenzen (v5.0 → v5.1)

| v5.0 Problem | v5.1 Loesung |
|--------------|--------------|
| Element-Tabelle hatte 6 Eintraege fuer 5 Elemente, Licht/Dunkel-Interaktion mit Natur/Feuer/Wasser undefiniert | Vollstaendige 5x5-Matrix (siehe 9.3), Licht/Dunkel auf "Neutral" gegenueber Element-Dreieck gesetzt |
| "Universal Scraps" vs. "Common/Rare/Epic/Legendary Scrap" doppelt definiert | Klar getrennt: Universal Scraps = Craft-Waehrung (eine), Upgrade-Steine = Karten-Level-Material (vier Stufen, siehe 6.2) |
| Karten-Level Tabelle fehlten Kopien fuer LV 1-4 + Stein-Mengen unklar | Vollstaendige Tabelle in 5.3 mit Kopien, Stein-Typ, Anzahl, Gold, Stat-Bonus |
| Klan-Match-Regel bei >2 Bietern undefiniert | 13.2: Auktion ohne Gegenbieter, sonst Top 2 spielen, andere bekommen Gebot zurueck |
| Energie-Ueberlauf 80/60 — Quelle unklar | 3.2: Bonus-Energie aus Login-Boni, Quests, Items, Diamant-Kaeufen; wird zuerst verbraucht |
| Saison-Belohnungen ohne konkrete Werte | 11.5: Vollstaendige Tabelle mit Gold/Karten/Scraps/Runen pro Rang |
| Dieb-Belohnungs-Verteilung unklar | 10.4: Verteilung nach Schadensanteil-Prozent |
| Welten-Empfohlenes-Level fehlte | 8.5: Empfohlenes Spieler-Level pro Welt |
| EXP-Tabelle fuer Spieler-Level fehlte | 4.2: Pilot-Formel + Eckdaten |
| Diamanten-Preise nicht definiert | 17.1: Pack-Preise + 17.2: Item-Preise |
| Deck-Anzahl nicht definiert | 4.4: 3 Slots frei + bis 3 zusaetzlich gekauft |
| Spieler-Level-Cap fehlte | 4.1: Soft-Cap 150 |
| Gilden-Groesse fehlte | 12.1: 30-50 Mitglieder, skaliert mit Gilden-Level |
| Server-Konzept fehlte | 2.3: Kapazitaet, Wechsel-Regeln, Regionen |
| Arena-Cooldown nicht definiert | 11.2: 30 s zwischen Kaempfen, 5 Energie pro Kampf |

### 23.2 Geschlossene TBDs (v5.1 → v5.2)

Alle 15 in v5.1 markierten TBDs sind in v5.2 entschieden und im Haupttext eingearbeitet.

| ID | Frage | Entscheidung | Im Haupttext |
|----|-------|--------------|--------------|
| TBD-01 | Server-Wechsel-Kosten | **1.000 Diamanten, max. 1x / 90 Tage** | Kap. 2.3 |
| TBD-02 | Voice-Notes in Chat | **Gestrichen** (Moderation zu aufwendig, auch nicht in Phase 2) | Kap. 14.2 |
| TBD-03 | Hero-Faehigkeiten | **Implementiert mit 6 Helden, je 1 aktive Faehigkeit** | Kap. 9.6 |
| TBD-04 | Live-PvP-Arena Top-100 | **Phase 2 (Monat 18+) via Photon Fusion** | Kap. 11 (Designnote) |
| TBD-05 | Karten-Tausch ausserhalb Gilde | **Phase 2 Marktplatz (Gold-Auktionen)** | Kap. 6.3 |
| TBD-06 | Rune-Slot 5 | **Phase 2, Spieler-Level 60 + Premium-Pass** | Kap. 7.1 |
| TBD-07 | Mana-Rune (Legendaer) | **Saison-2-exklusiv** | Kap. 7.2 |
| TBD-08 | Saison-Pass Implementierung | **Phase 2, vollstaendige Spec** | Kap. 16.3 |
| TBD-09 | Klan-Match-Replays | **Snapshot-basiert (JSON pro Runde), 30 Tage Aufbewahrung** | Kap. 13.1 |
| TBD-10 | Gilden-Beitragspunkte Reset | **Saisonal-Score + kumulativer Total-Score** | Kap. 12.3 |
| TBD-11 | Chat-Translation | **Phase 2, Google Translate-API, opt-in Welt/Privat** | Kap. 14.2 |
| TBD-12 | Arena-Tickets als Alt zu Energie | **JA, taeglich 5 gratis, 30 Tage Verfall** | Kap. 11.2 + 17 |
| TBD-13 | Avatar-Quellen | **50 Premade + Saison/Achievement-Avatare, keine Custom-Uploads** | Kap. 4.5 |
| TBD-14 | Dieb-Aktivitaets-Multiplikator | **DAU-basierte Formel (0,4 - 1,5)** | Kap. 10.2 |
| TBD-15 | iOS-Launch Zeitpunkt | **Phase 2, Monat 26+** | Kap. 1 + 18.4 |

### 23.3.B Aenderungen v5.3 → v5.4

| Aenderung | Quelle |
|-----------|--------|
| Material-Drop-Tabelle pro Welt-Boss-/Mini-Boss-Node | Neue `material_drops.json` + `MaterialDropResolver` |
| Saison-Pass-Engine + Pilot-Saison "season_2_classic" | `saison_pass.json` + `SaisonPassService` |
| Daily-Shop-Rotation (deterministisch per UTC-Tag) | `DailyShopRotation` |
| Friends-System (Anfrage/Accept/Block) | `FriendsService` |
| Chat-Moderation (Mute/Report/AutoMute-Aggregation) | `ChatModerationService` |
| Achievement-Trigger-Hooks aktiv + PendingClaims | `AchievementService` |
| Gilden-Treasury (Territory-Boni-Engine) | `TerritoryBonusEngine` + `GuildTreasuryService` |
| PlayerSave-Schema v2 mit 8 neuen Slices | `SaveMigrator.CurrentSchemaVersion = 2` |
| Server-Operations-Doku + 8 Cloud-Functions-Stubs | `Server/SERVEROPS.md` + `Server/CloudFunctions/` |
| BattleEngine PlayCard/EndTurn voll implementiert + Boss-Phase-2 | `Domain/Battle/BattleEngine.cs` |
| BattleStateSerializer (deterministische JSON-Roundtrip) | `Domain/Battle/BattleStateSerializer.cs` |

### 23.3.A Aenderungen v5.2 → v5.3

| Aenderung | Quelle |
|-----------|--------|
| Material-IDs aller 4 Sammelsets autoritativ in DESIGN + `collections.json` | Kap. 5.6 |
| Material-Karten-Naming-Konvention (`material.<id>`-Praefix) | Kap. 5.6 |
| 8 Tutorial-Schritte konkret spezifiziert | Neuer Kap. 16.4 |
| Daily/Weekly-Reset-Zeiten (00:00 UTC bzw. Montag 00:00 UTC) explizit | Kap. 11.4 (Quest-Reset) |
| Notification-Templates final (Energie/Dieb/Klan-Match/Daily/Saison-Ende) | Kap. 14.x via `notifications.json` |
| 6 Helden konkret spezifiziert (vorher TBD-03 noch im Pilot-Schwebezustand) | Kap. 9.6 |

### 23.3 Neue Inhalte in v5.2

- **Kap. 4.5 (Avatare & Profil-Bilder)** — neue Sektion
- **Kap. 9.6 (Hero-Faehigkeiten)** — komplett ausspezifiziert mit 6 Helden + Faehigkeiten + Cooldowns
- **Kap. 16.3 (Saison-Pass)** — Stufen-Tabelle und Preise final
- **Kap. 10.2 (Dieb-Multiplikator)** — DAU-Formel statt grober Schaetzung
- **Kap. 6.3 (Tausch-Marktplatz Phase 2)** — Phase-2-Erweiterung dokumentiert

### 23.4 Neue offene Punkte fuer v5.3 (Konzept-Phase Monat 2)

| ID | Frage | Wo zu klaeren |
|----|-------|---------------|
| TBD-N01 | Genaue Verteil-Mechanik fuer Saison-Pass-XP (welche Aktivitaet wieviel?) | Beta-Test, mit Telemetrie |
| TBD-N02 | Hero-Wechsel-Kosten und Cooldown nach 1. Wechsel | Nach Hero-Pick-Rate-Daten aus Beta |
| TBD-N03 | Saison-2 startet wann nach Launch (28 / 30 / 42 Tage)? | Marketing-Abstimmung |
| TBD-N04 | Marktplatz-Gold-Limits (Min/Max-Listings) | Anti-RMT-Analyse vor Phase-2-Start |
| TBD-N05 | Dieb-Anti-Bot: Captcha bei 10/10 Angriffen oder Rate-Limit Server-seitig? | Sicherheits-Review |

---

**Dokument-Ende.**

> Naechste Aktualisierung: Nach Closed-Beta (Monat 23) — dann GDD v6.0 mit Beta-Erkenntnissen.
