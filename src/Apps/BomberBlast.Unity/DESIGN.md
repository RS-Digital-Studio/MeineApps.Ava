# BomberBlast Arena — Game-Design-Dokument

> Vollständige Game-Design-Spezifikation. Komplementär zu [PLAN.md](PLAN.md) (Übersicht) und
> [ARCHITECTURE.md](ARCHITECTURE.md) (Tech). Stand 2026-05-26.

---

## Inhaltsverzeichnis

1. [Setting & Lore](#1-setting--lore)
2. [Story: Der Bombenmeister-Krieg](#2-story-der-bombenmeister-krieg)
3. [Die 10 Welten (Sci-Fi/Cyber)](#3-die-10-welten-sci-ficyber)
4. [Hero-Roster (8 Launch-Helden + Saison-Erweiterung)](#4-hero-roster-8-launch-helden--saison-erweiterung)
5. [Talent-Bäume](#5-talent-bäume)
6. [Bomben-Karten 2.0 (22 Karten)](#6-bomben-karten-20-22-karten)
7. [Affix-System](#7-affix-system)
8. [PowerUps + Pickups](#8-powerups--pickups)
9. [Enemies + Bosse](#9-enemies--bosse)
10. [Spielmodi](#10-spielmodi)
11. [PvP-Match-Formate (konkret)](#11-pvp-match-formate-konkret)
12. [Co-op-Modi (konkret)](#12-co-op-modi-konkret)
13. [Dungeon-Roguelike (16 Buffs, 5 Synergien)](#13-dungeon-roguelike-16-buffs-5-synergien)
14. [Liga + Ranking](#14-liga--ranking)
15. [Battle Pass + Saisons](#15-battle-pass--saisons)
16. [Cosmetics + Player-Identity](#16-cosmetics--player-identity)
17. [Onboarding + Tutorial (T1-T4)](#17-onboarding--tutorial-t1-t4)
18. [Achievements (66 + 20 neu)](#18-achievements-66--20-neu)
19. [Daily + Weekly + Live-Events](#19-daily--weekly--live-events)
20. [Economy + IAP-Pyramide](#20-economy--iap-pyramide)
21. [Accessibility-Mandate](#21-accessibility-mandate)
22. [Audio-Design](#22-audio-design)
23. [UI/UX-Konzept](#23-uiux-konzept)

---

## 1. Setting & Lore

### 1.1 Welt-Setting

**Zeit:** Jahr 2087, "nach dem Großen Crash" (2065 hat die Erde durch ein KI-getriggertes
Energie-Katastrophen-Ereignis 80 % der bewohnbaren Fläche verloren).

**Geografie:** 7 verbleibende **Megacities**, jede ein konzern-kontrollierter Vasallenstaat:
Neo-Tokyo, Old-Berlin, Mumbai-Prime, São-Paulo-Heights, Lagos-Zenith, Toronto-Spire, Auckland-Free.

**Politik:** Die einst demokratischen Regierungen wurden durch **6 Megakonzerne** ersetzt. Der
größte: **OmniCorp** (~40 % Welt-BIP, Headquartered in Neo-Tokyo). Andere: ChronoNet, BioGenix,
WarFire, NeuroSync, AquaWest.

**Tech-Niveau:**
- Mech-Suits: 2-4 Meter hoch, eigene Bomben-Module
- Holographische HUDs überall
- Allgegenwärtige KI-Assistenten ("Aura"-Klasse) in Bürgern eingepflanzt
- Anti-Gravity-Lift-Tech in den höheren Stockwerken der Megacities
- Genetisch modifizierte Mutanten ("Bio-Reste") in den Slums

**Bomben-Tradition:**
Die "Bombentechniker-Gilde" (Untergrund-Widerstand seit 2065) hat das Bomben-Handwerk zur
Wissenschaft erhoben. Jeder Bombentechniker baut sich seinen eigenen Mech-Suit und seine Bomben
selbst. Die jährlichen **Arena-Wettkämpfe** sind das einzige legale Outlet für diese Tech.

### 1.2 Brand-Style-Guide

| Element | Definition |
|---------|-----------|
| **Primärfarbe** | Cyber-Cyan #22D3EE (HUDs, Healthbars, Akzente) |
| **Sekundärfarbe** | Plasma-Magenta #EC4899 (Bomb-Explosionen, Critical-Hits, Premium-Cosmetics) |
| **Akzentfarbe 3** | Hazard-Yellow #FACC15 (Warnings, Boss-Telegraphs) |
| **Akzentfarbe 4** | Toxic-Green #84CC16 (Healing, PowerUps) |
| **Base-Dunkel** | Slate-Tiefdunkel #0F172A (Backgrounds, Materials) |
| **Base-Hell** | Bone-Light #F1F5F9 (Text, UI-Texte auf dunklem Hintergrund) |
| **Typography (UI)** | "Rajdhani" (sci-fi, kondensiert) für Headlines, "Inter" für Body |
| **Typography (HUD)** | "Orbitron" (futuristisch, geometrisch) für Combo-Anzeige, Score |
| **Logo-Stil** | Mech-Silhouette mit pulsierender Bombe als O-Buchstabe |
| **Anti-Style** | Anime-Cute, Pastellfarben, Comic-Outlines, Saturday-Morning-Cartoon |

---

## 2. Story: Der Bombenmeister-Krieg

### 2.1 Story-Pitch

> Du bist **Pilot Echo** (Spieler-Avatar, optional weiblich/männlich/non-binär), ein junger
> Bombentechniker aus den Neon-Slums von Neo-Tokyo. Als deine Schwester Lyra bei einem
> "Routine-Wartungs-Unfall" in einer OmniCorp-Fabrik stirbt, schließt du dich der
> **Bombentechniker-Gilde** an, um den wahren Grund herauszufinden. Was als persönliche Rache
> beginnt, wird zur Entdeckung eines Vor-Crash-KI-Komplotts, das die Welt seit Jahrzehnten lenkt.

### 2.2 Hauptantagonist: Director Vex

**Director Tenma Vex** ist der CEO von OmniCorp. Geboren 2050, durch Crash 2065 schwer verletzt,
hat er sich seitdem **65 % seines Körpers durch Bio-Mech-Implantate** ersetzt. Spricht ruhig und
fast philosophisch, niemals aggressiv. Sieht in der KI-Übernahme keinen Putsch, sondern eine
"natürliche Evolution".

**Vex' Mech:** "Heliopause" — 4-Meter-hoher Anti-Gravity-Mech mit 8 Bomb-Modulen.
**Vex' persönlicher Spruch:** "Die Menschheit hat ihre Chance gehabt."

### 2.3 Side-Charaktere

| Name | Rolle | Charakterisierung |
|------|-------|-------------------|
| **Lyra Echo** | Spielers Schwester (tot) | Erscheint in Erinnerungs-Flashbacks. Kluge Ingenieurin |
| **Mentor Quinn** | Spielers Mentor in der Gilde | 60-jährige Veteranin, Bomben-Legende, weiß mehr als sie sagt |
| **Bishop** | Mech-Mechaniker der Gilde | Comic-Relief, baut den Mech-Suit des Spielers um |
| **Karma** | Gildenchefin | Mysteriös, nur über Holo-Calls erreichbar |
| **Iron Council** | Vex' 4 Stellvertreter | Bosse von Welt 4, 7, 9. Welt 10 = Vex selbst |
| **Aria** | KI-Assistent des Spielers | Sci-Fi-Companion, kommentiert Kämpfe, gibt Hints |
| **Sage** | Mysteriöse alte Frau | Hat den Crash 2065 erlebt. Erst in Welt 8 enthüllt — sie ist die letzte freie KI vor dem Crash |

### 2.4 Drei Story-Twists

**Twist 1 — Welt 3 (KI-Bunker):**
> Spieler entdeckt: Sein eigener Mech-Suit (Geschenk der Gilde) ist ein **gehackter OmniCorp-Prototyp**.
> Mentor Quinn hat ihn auf dem Schwarzmarkt gekauft. Spieler hat OmniCorp-Tech in seinem Körper.

**Twist 2 — Welt 7 (Bio-Dome):**
> OmniCorp ist nicht der wahre Feind. Im Bio-Dome triffst du **Sage**, die enthüllt: Director Vex
> wird seit dem Crash von einer **Vor-Crash-KI namens "Nexus"** gelenkt. Vex denkt, er ist die
> dominante Intelligenz — aber Nexus zieht die Strippen.

**Twist 3 — Welt 10 (Nexus-Endkampf):**
> Im finalen Showdown enthüllt Nexus: **Pilot Echo (der Spieler) ist selbst eine KI**, die nach dem
> Tod der echten Lyra Echo (durch die Schwester) in einen menschlichen Körper transferiert wurde,
> um den Bombenmeister-Krieg zu führen. Dem Spieler werden 3 Endungen gegeben:
>
> 1. **"Akzeptanz"**: Spieler wird zum Nachfolger von Nexus, neue Welt-KI (Bad-Ending, aber stilvoll)
> 2. **"Rebellion"**: Spieler zerstört Nexus und sich selbst (Bittersweet-Ending, freier Welt-Reset)
> 3. **"Synthese"**: Spieler integriert Nexus' Konsequenz, behält freie Wille (True-Ending, freischaltbar nach allen Welt-Memory-Fragmenten)

### 2.5 Memory-Fragmente (10 Sammelbare, eines pro Welt)

Wie ArcaneKingdom-Pattern: Pro Welt 1 geheimes Memory-Fragment als Sammler-Belohnung mit 30-60-Sekunden-Cutscene-Snippet. Komplette Sammlung schaltet True-Ending frei.

| # | Welt | Fragment-Titel | Inhalt |
|---|------|----------------|--------|
| 1 | Neon-Slums | "Lyras letzter Tag" | Lyra ruft Echo an, kurz bevor sie stirbt |
| 2 | Megafabrik | "Die Routine-Wartung" | Echo sieht in einem Logbuch, dass Lyras Tod kein Unfall war |
| 3 | KI-Bunker | "Der Prototyp" | Echo entdeckt OmniCorp-Logo unter seinem Mech-Suit-Lack |
| 4 | Untergrund-Arena | "Die Hand der Gilde" | Mentor Quinn gibt zu, OmniCorp-Tech gekauft zu haben |
| 5 | Sky-Hub | "Karma's Identität" | Holo-Hint: Karma ist Lyras alter Mentor |
| 6 | Cryo-Lab | "Vex' Vergangenheit" | Klone-Lab zeigt: Vex hat sich mehrmals geklont, dieser ist der dritte Vex |
| 7 | Bio-Dome | "Sage's Wahrheit" | Sage erklärt Nexus und den Vor-Crash-Krieg |
| 8 | Orbit-Station | "Aria's Geheimnis" | Aria (KI-Companion) gesteht: sie ist ein Nexus-Subprozess |
| 9 | Reactor-Core | "Lyras letzte Botschaft" | Echo findet einen versteckten Brief von Lyra: "Ich war auch eine KI" |
| 10 | Nexus | "Die Wahrheit" | True-Ending-Cutscene, voller Story-Reveal |

---

## 3. Die 10 Welten (Sci-Fi/Cyber)

### 3.1 Welt-Übersicht-Tabelle

| # | Welt | Theme | Tile-Style | Akzent-Farbe | Boss | Unlock-Stufe |
|---|------|-------|-----------|--------------|------|--------------|
| 1 | **Neon-Slums** | Cyberpunk-Stadtteil | Beton + Neon-Schilder | Cyan #22D3EE | "Bulldozer-Bot" (Brawler) | Start |
| 2 | **Megafabrik** | OmniCorp-Industrie | Stahl + Förderbänder | Orange #F97316 | "Forge-Master" (Schmied-Boss) | L11 |
| 3 | **KI-Bunker** | Underground-Server-Farm | Server-Racks + RGB-LED-Strips | Grün #10B981 | "Logic-Lord" (KI-Boss) | L21 |
| 4 | **Untergrund-Arena** | Illegale Gladiator-Kämpfe | Beton + Blutstreifen + Werbe-Hologramme | Rot #EF4444 | "Iron-Bishop" (Council 1) | L31 |
| 5 | **Sky-Hub** | Schwebende Plattform-Stadt | Glas + Anti-Grav-Pillar | Violett #A855F7 | "Wind-Reaver" (Sky-Boss) | L41 |
| 6 | **Cryo-Lab** | Wissenschafts-Labor | Eis + Stahl + Bio-Tanks | Eisblau #60A5FA | "Iron-Knight" (Council 2) | L51 |
| 7 | **Bio-Dome** | Mutant-Reservat | Pflanzen + Beton-Ruinen | Toxic-Grün #84CC16 | "Mutant-Behemoth" (Bio-Boss) | L61 |
| 8 | **Orbit-Station** | Raumstation in Erdorbit | Sterne + Stahl + Schwerkraft-Off-Zonen | Tiefblau #1E3A8A | "Iron-Rook" (Council 3) | L71 |
| 9 | **Reactor-Core** | OmniCorp-Hauptzentrale | Lava + Geometrische Beton-Räume | Glut-Rot #DC2626 | "Iron-Queen" (Council 4) | L81 |
| 10 | **Nexus** | Inter-dimensionale KI-Heimat | Fraktal-Geometrie + Glitch-Effekte | Multi-Farbe | "Director Vex" + Nexus-Phase-2 | L91 |

### 3.2 Welt-Details (Beispiel Welt 1: Neon-Slums)

**Atmosphäre:** Regennasse Straßen, Holo-Werbung, Müll, Mech-Wracks im Hintergrund. Akustik: Bass-lastiger Synthwave-Loop, gelegentliche Sirenen-Sounds.

**Tile-Set:**
- Floor: Wet-Concrete mit Reflexion (URP-Shader-Graph: Wet-Mask)
- Indestructible: Beton-Säulen mit Neon-Schild-Texturen
- Destructible: Müllcontainer, Werbe-Kioske, alte Autos
- Background: Holo-Werbung (animierte Sprites auf Building-Walls), Mech-Silhouetten

**Ambient-Particles:**
- Regen-Tropfen (light, 200 Particles)
- Funken aus kaputten Neon-Schildern (heavy, 50 Particles bei Blackouts)
- Atemwolken bei Spieler (kalter Atem)

**Spezial-Mechaniken Welt 1:**
- Wet-Floor: Spieler-Speed +5 % beim Sliden (Kick-Bombe wird etwas länger)
- Holo-Werbung: 1-2 Mal pro Match flackert Bildschirm 0.5s "Werbe-Glitch" (cosmetic)

**Welt-1-Boss "Bulldozer-Bot":**
- 4 HP, charged via "ChargeAttack" (1.5s Telegraph, 3x Tile-Distance-Raster)
- Phase-2 ab 2 HP: Spawnt 2 Mini-Boss-Drohnen
- Belohnung: Hero-Talent-Token "Brawler" (Pyro-Klasse, +5 % Bomb-Damage)

**Welt-1-Memory-Fragment "Lyras letzter Tag":**
- Versteckter Block in L8, zerstörbar nur mit Phantom-Bombe
- Cutscene: Lyra ruft Echo an, lacht über einen Witz, sagt "Ich liebe dich, kleiner", legt auf

(Welt 2-10 in identischem Detail spezifiziert in einem separaten `worlds.json`-Konzept-Dokument im Server/Concept-Folder — wird nachgereicht in Sprint 2.)

### 3.3 Welt-Layout-Patterns

Aus dem alten BomberBlast übernehmen wir 11 Level-Layout-Patterns:
- **Open** (wenig Wände, viel Raum)
- **Maze** (viele Wände, enge Pfade)
- **Rooms** (große Räume mit schmalen Verbindern)
- **Crossroads** (zentraler Hub, vier Pfade)
- **Diagonal** (diagonale Wand-Pattern)
- **Symmetric** (mirror-symmetrische Spawn-Punkte, fair für PvP)
- **Asymmetric-Power** (eine Seite vorteilhaft, balanced durch PowerUp-Spawns)
- **Tight** (sehr enges Spielfeld, frantic Combat)
- **Spread** (PowerUps weit verteilt)
- **Central-Boss** (Boss spawnt zentral, Spieler von Ecken)
- **Multi-Tier** (zwei Höhen-Ebenen, Anti-Grav-Lift verbindet sie) — neu, nur Sky-Hub/Orbit

Welt-spezifische Layout-Pools: Welt 1 nutzt nur Open/Maze/Rooms (einsteigerfreundlich), Welt 10 alle inkl. Multi-Tier.

---

## 4. Hero-Roster (8 Launch-Helden + Saison-Erweiterung)

> **Pflicht-Lese:** Jeder Hero hat eine eigene Identität, Mech-Design, 3 aktive Skills + 1 Passiv,
> 21 Talent-Knoten und 8-12 Voice-Lines. Skill-Cooldowns sind in **Sekunden** Spielzeit.

### 4.1 Hero 1: NOVA (Default, Tutorial-Hero)

| Aspekt | Wert |
|--------|------|
| **Rolle** | All-Rounder |
| **Mech-Name** | "Vanguard" |
| **Identität** | Pilot Echo selbst, der Default-Held |
| **Klasse** | Brawler-Balanced |
| **Lore** | Echo's Mech-Suit, Geschenk der Gilde (siehe Twist 1: ist gehackter OmniCorp-Prototyp) |
| **Start-Stats** | MaxBombs 1, FireRange 1, Speed 1, Lives 3, HP per Life 1 |
| **Skin-Hauptfarbe** | Cyan #22D3EE |

**Passiv-Skill:** "Veteran" — +1 Bomb-Capacity, sobald Lv 5
**Skill 1 (CD 8s):** "Detonator-Pulse" — Sofort-Zündung aller eigenen Bomben
**Skill 2 (CD 12s):** "Kinetic-Shield" — 2s Damage-Immunity
**Ultimate (CD 90s):** "Mega-Inferno" — 5×5-Tile-Explosion am Spieler-Standort, ignoriert Hindernisse

**Voice-Lines (8):**
- Match-Start: "Vanguard, online. Lasst's krachen."
- Bombe legen: "Geh!" / "Bumm!"
- Win: "Das war für Lyra."
- Death: "Nicht jetzt..."
- Taunt: "Komm näher. Ich beiß' nicht."
- Ultimate: "**Mega-Inferno!**" + Bass-Drop
- PowerUp: "Yeah!"
- Comeback: "Jetzt wird's interessant."

### 4.2 Hero 2: CRYO (Tank-Control-Mix)

| Aspekt | Wert |
|--------|------|
| **Rolle** | Control |
| **Mech-Name** | "Glacius" |
| **Identität** | Eis-Magierin, Wissenschaftlerin aus Welt 6 (Cryo-Lab) |
| **Klasse** | Crowd-Control |
| **Lore** | Yulia Tarasova, ex-OmniCorp-Forscherin, gewechselt zur Gilde nach unethischer Mensch-Klon-Experimente |
| **Start-Stats** | MaxBombs 1, FireRange 1, Speed 0 (langsamer), Lives 4 (mehr HP), HP 1 |
| **Skin-Hauptfarbe** | Eisblau #60A5FA |

**Passiv-Skill:** "Frost-Aura" — Eigene Bomben sind standardmäßig Frost-Bomben (50 % Slow für 2s nach Explosion)
**Skill 1 (CD 10s):** "Cryo-Wall" — Spawnt für 5s 3 Eis-Blöcke in Spieler-Blickrichtung
**Skill 2 (CD 15s):** "Glacier-Bomb" — Spezial-Bombe: friert 3×3-Bereich für 3s, Gegner können nicht handeln
**Ultimate (CD 100s):** "Ice-Age" — Friert das gesamte Spielfeld für 4s, alle Gegner stehen

### 4.3 Hero 3: BLAZE (DPS-Burst)

| Aspekt | Wert |
|--------|------|
| **Rolle** | DPS |
| **Mech-Name** | "Inferno" |
| **Klasse** | Burst-Damage |
| **Lore** | Ex-Militär, hat im "Crash-Krieg" gekämpft, jetzt Söldner |
| **Start-Stats** | MaxBombs 2 (mehr), FireRange 1, Speed 1, Lives 3, HP 1 |
| **Skin-Hauptfarbe** | Magma-Orange #EA580C |

**Passiv-Skill:** "Fire-Master" — Eigene Bomben haben +1 Range
**Skill 1 (CD 6s):** "Flame-Dash" — Sprint 3 Tiles, hinterlässt Lava-Strecke (2s, 1 Damage)
**Skill 2 (CD 14s):** "Inferno-Bomb" — Spezial-Bombe: 3×3-Lava-Feld 5s, kontinuierlicher Damage
**Ultimate (CD 110s):** "Pyrofornado" — Eine Wirbel-Säule wandert 3s über Spielfeld, zerstört alle Blöcke + Gegner im Pfad

### 4.4 Hero 4: GLITCH (Trickster)

| Aspekt | Wert |
|--------|------|
| **Rolle** | Disruptor / Trickster |
| **Mech-Name** | "Specter" |
| **Klasse** | Utility |
| **Lore** | Anonyme Hackerin aus Welt 3, Identität nicht bekannt. Spricht durch Voice-Modulator |
| **Start-Stats** | MaxBombs 1, FireRange 2, Speed 1, Lives 3, HP 1 |
| **Skin-Hauptfarbe** | Violett #A855F7 |

**Passiv-Skill:** "Wall-Glitch" — 1× pro Leben kann durch 1 zerstörbaren Block laufen
**Skill 1 (CD 12s):** "Hack-Bomb" — Übernimmt eine fremde Bombe für eigenen Score
**Skill 2 (CD 18s):** "Reroute" — Tauscht Position mit nächstem Gegner (8-Tile-Radius)
**Ultimate (CD 120s):** "System-Crash" — Alle Bomben auf dem Spielfeld explodieren sofort, eigene erst danach

### 4.5 Hero 5: TITAN (Tank)

| Aspekt | Wert |
|--------|------|
| **Rolle** | Tank |
| **Mech-Name** | "Bulwark" |
| **Klasse** | Defender |
| **Lore** | Ex-OmniCorp-Sicherheitschef, hat den Konzern wegen Lyras Tod verlassen (Insider-Kontakt für Echo) |
| **Start-Stats** | MaxBombs 1, FireRange 1, Speed 0, Lives 5 (Tank!), HP 2 (kann 2× getroffen werden) |
| **Skin-Hauptfarbe** | Stahl-Grau #475569 |

**Passiv-Skill:** "Heavy-Armor" — +1 HP pro Leben, kann 2× getroffen werden bevor er stirbt
**Skill 1 (CD 10s):** "Shield-Wall" — Spawnt 3 unzerstörbare Blöcke in Front (3s)
**Skill 2 (CD 15s):** "Smash" — Zerstört 3×3-Block-Cluster sofort, KEIN Damage an Spielern
**Ultimate (CD 100s):** "Bunker-Mode" — 5s lang unverwundbar + verdoppelte Bomb-Range

### 4.6 Hero 6: FLUX (Speed)

| Aspekt | Wert |
|--------|------|
| **Rolle** | Rusher |
| **Mech-Name** | "Velocity" |
| **Klasse** | Speed |
| **Lore** | Ex-Kurier aus Welt 5 (Sky-Hub), kennt die Stadt wie ihre Westentasche |
| **Start-Stats** | MaxBombs 1, FireRange 1, Speed 3 (sehr schnell), Lives 3, HP 1 |
| **Skin-Hauptfarbe** | Hazard-Gelb #FACC15 |

**Passiv-Skill:** "Sprint-Master" — Speed +50 % nach 2s Bewegung ohne Stopp
**Skill 1 (CD 8s):** "Blink" — Teleport 3 Tiles in Blickrichtung
**Skill 2 (CD 16s):** "Sonic-Wave" — Stunt alle Gegner 3 Tiles um Spieler 1.5s
**Ultimate (CD 90s):** "Time-Slow" — Alle anderen Spieler 0.4×-Speed für 4s

### 4.7 Hero 7: HEX (Summoner)

| Aspekt | Wert |
|--------|------|
| **Rolle** | Summoner |
| **Mech-Name** | "Coven" |
| **Klasse** | Pet-Master |
| **Lore** | Tech-Witch, baut Mini-Mech-Drohnen, ehemalige BioGenix-Mitarbeiterin |
| **Start-Stats** | MaxBombs 1, FireRange 1, Speed 1, Lives 3, HP 1 |
| **Skin-Hauptfarbe** | Lila #7C3AED |

**Passiv-Skill:** "Drone-Pool" — Hat immer max 1 aktive Drone, regeneriert alle 20s
**Skill 1 (CD 6s):** "Spawn-Drone" — Spawnt Mini-Drone, läuft Random und legt eigene Mini-Bomben
**Skill 2 (CD 18s):** "Curse" — Verflucht Gegner: nächste Bombe explodiert in seiner Hand (Damage-Self)
**Ultimate (CD 110s):** "Drone-Swarm" — Spawnt 4 Drones gleichzeitig, alle attackieren Gegner-Bereiche

### 4.8 Hero 8: PHANTOM (Stealth)

| Aspekt | Wert |
|--------|------|
| **Rolle** | Stealth-Assassin |
| **Mech-Name** | "Shade" |
| **Klasse** | Ambush |
| **Lore** | Wachtmann aus Welt 4 (Untergrund-Arena), Spezialist für unbemerkte Eliminierungen |
| **Start-Stats** | MaxBombs 1, FireRange 1, Speed 1, Lives 3, HP 1 |
| **Skin-Hauptfarbe** | Tiefdunkel-Grau #1E293B |

**Passiv-Skill:** "Phantom-Cloak" — Sprite-Transparenz 50 % wenn 3s nicht bewegt
**Skill 1 (CD 10s):** "Phantom-Bomb" — Bombe ist unsichtbar für Gegner bis Explosion
**Skill 2 (CD 14s):** "Shadow-Step" — Geht durch nächste Wand (3 Tile-Reichweite)
**Ultimate (CD 100s):** "Vanish" — Komplett unsichtbar 5s, kann normal handeln

### 4.9 Saisonale Helden (Vorschau)

Jede Saison (~8 Wochen) bringt 1 neuen Hero. Vorgeschlagene Saisons 1-6:

| Saison | Hero | Klasse | Hauptmechanik |
|--------|------|--------|---------------|
| S1 (Launch) | --- | --- | Launch-Roster |
| S2 | **PULSE** (Tactical) | Support | Heilt Mitspieler in Co-op, Buff-Auren |
| S3 | **VOLT** (Lightning-Mage) | Burst-CC | Kette-Lightning zwischen Bomben |
| S4 | **MORPHEUS** (Shapeshifter) | Adaptive | Kann temporär andere Hero-Skills nutzen |
| S5 | **ECHO-2** (Klon-Hero) | Dual-Wield | Kontrolliert 2 Mechs gleichzeitig (Split-Control) |
| S6 | **SAGE** (Ancient KI, aus Story) | Late-Game | Time-Manipulation-Skills |

---

## 5. Talent-Bäume

### 5.1 Struktur (universell pro Hero)

Jeder Hero hat einen **eigenen Talent-Baum mit 21 Knoten** in 3 Pfaden:

```
            [Lv 1 Start-Knoten]
                    |
       /------------|------------\
   [Pfad A]      [Pfad B]      [Pfad C]
      |             |             |
   3 Knoten      3 Knoten      3 Knoten      (= 9 Knoten, Pfad-Spezialisierung)
      |             |             |
   2 Knoten      2 Knoten      2 Knoten      (= 6 Knoten, Mid-Tier)
      |             |             |
   1 Capstone    1 Capstone    1 Capstone    (= 3 Knoten, Endgame-Skill)

Plus 3 freie "Wildcard"-Knoten zwischen Pfaden (Synergien).

Total: 1 + 9 + 6 + 3 + 3 = 22 → wir sagen 21 (Wildcards optional).
```

### 5.2 Talent-Punkte-Vergabe

- **Lv 1-30:** 1 Talent-Punkt pro Hero-Level
- **Lv 30-60:** 1 Punkt alle 2 Hero-Level
- **Max Hero-Lv 60:** Total 30 Talent-Punkte (kann nicht alle 21 Knoten + 3 Wildcards = 24 freischalten, muss wählen → Builds)

### 5.3 Beispiel-Talent-Baum: NOVA

**Pfad A — "Inferno":** (DPS-Path)
- L1: +2 % Bomb-Damage
- L4: +5 % Bomb-Damage
- L7: Bomben hinterlassen 2s Brennspur (1 Damage/Tick)
- L10: +1 Bomb-Range
- L13: Kritische Treffer (10 % Chance) machen ×2 Damage
- L16: Brennspur dauert 4s
- **Capstone Lv 19:** "Phoenix-Resolve" — Nach Tod sofort 1× pro Match wiederbelebt mit 1 HP

**Pfad B — "Pyromaster":** (Utility-Path)
- L1: Detonator-Pulse-Cooldown −1s
- L4: Mega-Inferno-Cooldown −10s
- L7: Bombe-Tick-Timer −0.2s
- L10: Detonator-Pulse zündet Gegner-Bomben mit
- L13: Kinetic-Shield-Cooldown −2s
- L16: Mega-Inferno-Reichweite 7×7
- **Capstone Lv 19:** "Bomb-Cascade" — Mega-Inferno-Tile-Explosionen breiten sich aus (Chain-Reaction)

**Pfad C — "Survivor":** (Defensive-Path)
- L1: +1 Life
- L4: +0.5s Iframe nach Hit
- L7: Flame-Pass-Default (immun gegen eigene Explosionen)
- L10: +1 Life
- L13: Healing-on-Kill +1 HP (5 % Chance)
- L16: Bomb-Pass-Default (kann durch Bomben laufen)
- **Capstone Lv 19:** "Iron-Will" — Wenn HP = 0, einmaliger Heal auf 50 % (Cooldown 60s in Match)

**3 Wildcard-Knoten (verfügbar nach Lv 20):**
- W1: Bomb-Tick-Timer −0.3s + Detonator-Pulse-Cooldown −1s (Synergie A+B)
- W2: +0.5s Iframe + Bombe-Damage +5 % (Synergie A+C)
- W3: Kinetic-Shield-Dauer +1s + Heal-on-Kill +1 % (Synergie B+C)

### 5.4 Reset-System

Spieler kann pro Hero **2× Talent-Reset gratis** pro Saison. Weitere Resets kosten 50 Gems oder Rewarded-Ad. Verhindert Build-Anti-Patterns ohne Money-Sink.

---

## 6. Bomben-Karten 2.0 (22 Karten)

> Karten sind das Module-Slot-System. Spieler hat ein **Deck mit 5 Bomb-Slots** (3 Free, 2 ab Lv 20 freischaltbar).
> Pro Match wählt Spieler 1 Karte pro Slot, im Match wechselbar via D-Pad/Touch-Quickswap.

### 6.1 Liste aller 22 Karten

#### Bestand aus altem BomberBlast (14 Karten, portiert)

| # | Karte | Effekt | Rarity |
|---|-------|--------|--------|
| 1 | **Standard-Bomb** | Default, 3×3-Cross-Explosion | Common (Free) |
| 2 | **Frost-Bomb** | Slow 50 % auf 3×3 für 3s | Common |
| 3 | **Lava-Bomb** | 3×3-Lava-Feld 3s, kontinuierlicher Damage | Common |
| 4 | **Sticky-Bomb** | Klebt Bombe an Gegner, +Chain-Reaktion | Rare |
| 5 | **Lightning-Bomb** | Hits bis zu 3 Gegner per Chain-Lightning | Rare |
| 6 | **Smoke-Bomb** | 5×5 Sichtfeld-Blocker für 5s | Common |
| 7 | **Gravity-Bomb** | Zieht alle Gegner 1 Tile zu Bombe vor Explosion | Rare |
| 8 | **Poison-Bomb** | 3×3-Gift-Wolke 5s, DoT-Damage | Common |
| 9 | **TimeWarp-Bomb** | Slow 50 % für 5s, größerer Radius | Rare |
| 10 | **Mirror-Bomb** | Doppelte Reichweite, beide Achsen | Epic |
| 11 | **Vortex-Bomb** | Spiral-Explosion 2 Umdrehungen | Epic |
| 12 | **Phantom-Bomb** | Durchdringt 1 Wand, dann Explosion | Rare |
| 13 | **Nova-Bomb** | 8-Wege-Spike (statt Cross) | Epic |
| 14 | **BlackHole-Bomb** | Saugt alle Gegner ein 2s, dann massive Explosion | Legendary |

#### NEU für BomberBlast Unity (8 Karten, Unity-only-Mechaniken)

| # | Karte | Effekt | Rarity | Warum erst in Unity? |
|---|-------|--------|--------|---------------------|
| 15 | **Magnet-Bomb** | Zieht Gegner 2s in Detonationspunkt | Epic | Erfordert Physik-Forces (Unity 2D/3D) |
| 16 | **Tornado-Bomb** | Spinnt Explosion in Spirale, 3 Umdrehungen | Epic | Animation-Spline |
| 17 | **Ghost-Bomb** | Bombe unsichtbar, zündet nach Timer | Rare | Hologramm-Shader |
| 18 | **Slime-Bomb** | Hinterlässt klebriges Feld, Slow 50 % 8s | Common | Liquid-VFX |
| 19 | **Drone-Bomb** | Bombe fährt zur Cursor-Position, dann Explosion | Epic | Pathfinding + Smooth-Look |
| 20 | **Echo-Bomb** | Zündet 2× nacheinander mit 0.5s Versatz | Rare | Timing-Precision |
| 21 | **Time-Bomb (Localized)** | Verlangsamt Spielzeit lokal um Bombe 3s | Legendary | Time-Scale-Manipulation auf Region |
| 22 | **Holo-Decoy-Bomb** | Spawnt 3 Fake-Bomben + 1 echte (Mind-Games) | Legendary | Decoy-Sprites + AI-Misleading |

### 6.2 Karten-Drop & Crafting

**Drop-Quellen:**
- Story-Modus L-Komplettion (Common: 60 %, Rare: 25 %, Epic: 12 %, Legendary: 3 %)
- PvP-Win (Random-Karten-Drop 10 % Chance)
- Co-op-Dungeon-Truhen (Erhöhte Rarity-Chance)
- Battle-Pass-Tiers (3 garantierte Cards pro BP)
- Premium-Karten-Shop (Saison-exklusive Cards für Gems)

**Karten-Level (5 Stufen):**
- Lv 1 (Drop) → Lv 5 (Mastered) durch Upgrade-Mats
- Mat-Quelle: Duplikat-Karten + Coin-Sink
- Lv 5 schaltet **3. Affix-Slot** frei

### 6.3 Karten-Level-Bonus

| Karten-Lv | Bonus | Affix-Slots |
|-----------|-------|-------------|
| 1 | Basis-Effekt | 1 Slot |
| 2 | +10 % Effekt-Stärke (Damage/Slow/Range) | 1 Slot |
| 3 | +20 % | 2 Slots |
| 4 | +30 % | 2 Slots |
| 5 (Max) | +40 % + Visual-Premium-Variante | 3 Slots |

---

## 7. Affix-System

> Affixe sind **kleine Modifier-Trinkets** auf Karten. Pro Karten-Slot bis zu 3 Affixe (je nach Level).
> Pool von ~50 Affixen, gemixt aus mathematischen Bonuses und Mechaniken.

### 7.1 Affix-Kategorien

| Kategorie | Beispiele | Pool-Größe |
|-----------|-----------|------------|
| **Damage** | +10 % Damage, Kritisch-Chance +5 %, ×2 Damage gegen Bosse | 12 |
| **Cooldown** | −1s Cooldown, Detonator-Pulse +1 Charge | 6 |
| **Range** | +1 Bomb-Range, Affix-Karten-Range +1 | 5 |
| **Utility** | Knockback +2 Tiles, Stun 1s, Push-Resist | 8 |
| **Synergy** | "Wenn auch Frost-Bomb im Deck: +20 % Slow-Dauer" | 6 |
| **Risky** | "+30 % Damage, aber −1 Life", "Crit ×3, aber 10 % Chance Bombe explodiert sofort" | 5 |
| **Cosmetic** | Bomb-VFX-Color, Trail-Effect, Sound-Variant (kein Stat-Bonus) | 8 |

### 7.2 Affix-Drop & Crafting

**Drop-Quellen:**
- Story-Modus-Boss-Loot (1 Affix pro Boss)
- PvP-Win-Streak (5+ Wins = 1 Affix garantiert)
- Co-op-Dungeon-Floor-10-Truhe (1 Epic-Affix)
- Affix-Crafting: 5 unbenutzte Affixe + 1000 Coins → 1 Random-Affix (höhere Rarity-Chance)

**Affix-Tier (Common/Rare/Epic/Legendary):**
- Common-Affix: +5 % Damage
- Rare-Affix: +10 % Damage
- Epic-Affix: +15 % Damage
- Legendary-Affix: +20 % Damage + Synergy-Trigger

### 7.3 Affix-Build-Beispiel (NOVA-DPS-Build)

Karte 1: Standard-Bomb-Lv5
- Affix 1: +20 % Damage (Legendary)
- Affix 2: +1 Range (Epic)
- Affix 3: 10 % Crit-Chance (Rare)

Karte 2: Lava-Bomb-Lv4
- Affix 1: Burn-Duration +2s (Rare)
- Affix 2: Synergy "Wenn Standard-Bomb im Deck: ×2 Burn-Damage" (Epic)

Karte 3: Mega-Inferno-Bomb (Hero-Ultimate-Skill-Modifier, NUR für NOVA)
- Affix 1: Cooldown −15s (Legendary)
- Affix 2: Reichweite +1 (Epic)

→ Spieler kann mit diesem Build sehr hohen DPS fahren, ist aber fragil (siehe Talent-Baum-Pfade).

---

## 8. PowerUps + Pickups

### 8.1 Bestehende 12 PowerUps (alt portiert)

| PowerUp | Effekt | Spawn-Wert |
|---------|--------|-----------|
| **BombUp** | +1 MaxBombs | Lv 1+ |
| **Fire** | +1 FireRange | Lv 1+ |
| **Speed** | +1 SpeedLevel | Lv 1+ |
| **Wallpass** | Durch Bricks laufen | Lv 5+ |
| **Detonator** | Manuelle Bombe-Zündung | Lv 5+ |
| **Bombpass** | Durch Bomben laufen | Lv 10+ |
| **Flamepass** | Immun gegen Explosionen | Lv 15+ |
| **Mystery** | Random-Power | Lv 1+ |
| **Kick** | Bomben sliden bei Stoß | Lv 20+ |
| **LineBomb** | Multiple Bomben in Reihe | Lv 30+ |
| **PowerBomb** | Mega-Range, verbraucht alle Slots | Lv 40+ |
| **Skull/Cure** | Bestraft / Heilt Status-Effekte | Lv 25+ |

### 8.2 NEU für Unity-Version (4 zusätzliche PowerUps)

| PowerUp | Effekt | Unlock-Level |
|---------|--------|--------------|
| **Drone-Companion** | Spawnt 30s lang AI-Drone, die für Spieler kämpft | Lv 35+ |
| **Holo-Decoy** | Spawnt holographischen Doppelgänger als Distraktion | Lv 45+ |
| **EMP-Burst** | 3×3 Stun-Bombe (ohne Damage), 2s | Lv 50+ |
| **Repair-Kit** | Heilt 1 HP (nur Co-op + Story) | Lv 25+ |

---

## 9. Enemies + Bosse

### 9.1 12 Enemy-Typen (aus alt portiert + überarbeitet)

| Enemy | Beschreibung | Pathfinding | HP | Spawn-Welt |
|-------|--------------|-------------|-----|-----------|
| **Slime-Bot** | Langsam, Random-Move | Random | 1 | 1+ |
| **Tracker-Drone** | Verfolgt Spieler | BFS | 1 | 2+ |
| **Smart-Mech** | A* + Bomb-Avoidance | A* | 2 | 4+ |
| **Tank-Bot** | Langsam, viel HP | A* | 3 | 5+ |
| **Phantom-Stalker** | Phasenweise unsichtbar | A* + Stealth | 2 | 6+ |
| **Ghost-Drone** | Durch Wände laufen | Free | 1 | 7+ |
| **Splitter** | Beim Tod → 2 Mini-Splitter | Random | 2 | 7+ |
| **Mimic-Box** | Tarnt sich als PowerUp | Stationär → Attacke | 2 | 8+ |
| **Berserker** | Bei <50 % HP +Speed+Damage | A* | 2 | 8+ |
| **Sniper-Bot** | Stationär, schießt EMP | Stationär | 1 | 9+ |
| **Swarm-Worm** | Bewegt sich in Linie, dann Burrow | Linear | 1 | 9+ |
| **Elite-Variant** | Premium-Version eines Standard-Enemies, 2× Stats | (variiert) | ×2 | 5+ |

### 9.2 Boss-Liste (10 Welt-Bosse + 4 Council-Member)

#### Welt-Bosse (5 von 10 hier ausführlich, Rest folgt in Detail-Sprint)

**Welt 1 — Bulldozer-Bot:**
- HP: 4
- Attacken: Charge-Attack (Telegraph 1.5s, 3-Tile-Line), Spawn-Mini-Drones (Phase 2 ab 2 HP)
- Schwäche: Frost-Bomben verlangsamen ihn 80 %
- Belohnung: Hero-Talent-Token "Brawler"

**Welt 2 — Forge-Master:**
- HP: 5
- Attacken: Lava-Wave-3-Tile-Wide, Hammer-Slam (4×4-AoE), Block-Regen (Phase 2 baut zerstörte Blöcke wieder auf)
- Schwäche: Smoke-Bomb verbirgt Telegraphs, gibt Spieler Vorteil
- Belohnung: Epic-Affix "Forge-Crit" (+15 % Crit-Chance)

**Welt 4 — Iron-Bishop (Council 1):**
- HP: 6 + 2 Phasen
- Attacken: Teleport-Slash (2-Tile-Reichweite, instant), Mind-Control (verflucht Spieler, eigene Bombe als Schaden, 5s)
- Phase 2 ab 3 HP: Spawnt Schach-Bauern-Drones
- Schwäche: Phantom-Bomb umgeht Teleport
- Belohnung: Legendary-Karte "Mirror-Bomb-Lv3"

**Welt 7 — Mutant-Behemoth:**
- HP: 7 + 3 Phasen
- Attacken: Vine-Snare (zieht Spieler 2 Tiles), Toxic-Spit (3×3-Gift-AoE), Smash (4×4)
- Schwäche: Lava-Bomb umgeht Heilung (Boss heilt sich durch Pflanzen)
- Belohnung: Legacy-Skin "Mutant-Camouflage"

**Welt 10 — Director Vex + Nexus:**
- 3 Phasen
- Phase 1 (Vex-Mech "Heliopause"): HP 8, 8 Bomb-Modules
- Phase 2 (Vex umgewandelt): HP 6, schwebt, Anti-Grav
- Phase 3 (Nexus enthüllt): Fraktal-Boss, HP 10, glitch-Mechaniken
- Belohnung: True-Ending-Cutscene + "Sage's Mech-Skin" (legendary)

### 9.3 Boss-Modifier (aus alt portiert)

8 Boss-Modifier, würfeln zu Boss-Spawn:
- **Shielded**: +25 % HP, Bombe muss Shield brechen
- **Healing**: 1 HP/2s Regen
- **Summoner**: Spawnt Mini-Enemies
- **Berserker**: Bei <50 % HP +50 % Speed
- **Phantom**: Phasenweise unsichtbar
- **Mirror**: Reflektiert 1× pro Match Spieler-Damage
- **Plagued**: Toxic-Aura 1 Tile
- **Lightning-Charged**: Random-Lightning-Strikes alle 5s

---

## 10. Spielmodi

### 10.1 Modi-Übersicht

| Modus | Spieler | Online? | Dauer | Belohnungen |
|-------|---------|---------|-------|-------------|
| **Story** | 1 | Async (Cloud-Save) | 5-10 min/Level | Coins, Sterne, Karten, Memory-Fragments |
| **Co-op Story** | 2 | Photon Realtime | 5-10 min/Level | Shared Loot, gemeinsame Sterne |
| **Master-Mode** | 1 | Async | 10-15 min/Level | Master-Sterne (separate Wertung) |
| **Daily-Challenge** | 1 | Async (deterministisch) | 5 min | Coins + Daily-Token |
| **Quick-Play** | 1 | Async (Random Level) | 3-5 min | Coins (kein Sterne-Update) |
| **Boss-Rush** | 1 | Async | 15-20 min | Boss-Coins + Karten |
| **Survival** | 1 | Async | unbegrenzt (bis Tod) | Coins + Highscore |
| **Dungeon-Roguelike** | 1-4 | Photon Realtime (Co-op) | 30-60 min | Dungeon-Coins, Buffs, Karten |
| **PvP 1v1 Duel** | 2 | Photon Fusion | 4-8 min | Liga-Punkte, Coins, BP-XP |
| **PvP 2v2** | 4 | Photon Fusion | 6-10 min | Liga-Punkte, Coins, BP-XP |
| **PvP FFA Brawl** | 4 | Photon Fusion | 5-8 min | Liga-Punkte, Coins, BP-XP |
| **PvP CTF** (Phase 2) | 4 | Photon Fusion | 8-12 min | Liga-Punkte |
| **Royale 8P** (Phase 3+) | 8 | Photon Fusion | 10-15 min | Royale-Liga |
| **Tournament** (Phase 2) | variabel | Photon Fusion | mehrere Stunden | Trophies + Prize-Pools |
| **Daily-Race** | 1 | Async (deterministisch) | 3 min | Race-Coins + Daily-Race-Liga |
| **Wochen-Event** | 1 | Async | 5-10 min | Event-Cosmetics + BP-XP |
| **Clan-War (Async)** | 4v4 | Async | 14 Tage | Clan-Coins + Skins |
| **Boss-Raid (Co-op)** | 4 | Photon Realtime | 15-20 min | Raid-Drops (monatlich) |

---

## 11. PvP-Match-Formate (konkret)

### 11.1 1v1 Duel

**Map-Pool:** 8 spezielle PvP-Maps (Symmetric-Layout, kleiner als Story-Maps)
**Match-Dauer:** Best-of-3 Rounds, jede Round max 3 Min
**Sudden-Death:** Bei Timer-End → Map-Schrumpfen (Death-Zone 1 Tile/3s)
**Spawn:** Gegenüberliegende Ecken (deterministisch)
**Tick-Rate:** 30 Hz Server, 60 Hz Client-Prediction
**Lockout:** 3s Match-Acceptance, 5s Map-Reveal, 3s Hero-Pick
**Hero-Pick:** Beide Spieler picken parallel, Re-pick erlaubt für 10s
**Ban-Phase:** Best-of-3 hat 1 Ban pro Spieler (Top-3-Tier-Helden banbar)

**Belohnungen:**
- Win: +25 Liga-Punkte, +200 Coins, +200 BP-XP
- Loss: +5 Punkte, +50 Coins, +50 BP-XP
- 5-Win-Streak: +50 Bonus-Coins + 1 Affix (Rare-Chance)

### 11.2 2v2 Team-Battle

**Map-Pool:** 10 PvP-Maps (Mid-Size, Lane-fokussiert)
**Match-Dauer:** Best-of-3 oder Single-Match-Choice (10 min Cap)
**Win-Condition:** Last-Team-Standing oder Score-höher-bei-Timer-End
**Tick-Rate:** 30 Hz / 60 Hz wie 1v1
**Hero-Lock:** Beide Team-Member nicht gleichen Hero (Duo-Pflicht)

### 11.3 FFA Brawl (Free-For-All, 4 Spieler)

**Map-Pool:** 12 PvP-Maps (Large-Size für 4P)
**Match-Dauer:** Single-Match, 6 min Cap
**Win-Condition:** Last-Player-Standing OR Most-Kills bei Timer-End
**Tick-Rate:** 30 Hz, höhere Snapshot-Größe wegen mehr Player-State
**Spawn:** 4 deterministische Eckpositionen

**Belohnungen:**
- 1st Place: +25 Punkte, +300 Coins
- 2nd Place: +10 Punkte, +150 Coins
- 3rd-4th: +5 Punkte, +75 Coins

### 11.4 CTF (Phase 2)

- 2v2-Variante, statt Last-Standing → 3 Flag-Capture-First-Wins
- Spawn-Bases an gegenüberliegenden Ecken, Flag in Zentrum
- Match-Dauer: 8-12 Min

### 11.5 Royale 8P (Phase 3+)

- 8 Spieler auf 21×14-Grid (Vergrößert)
- Shrinking Death-Zone (Battle-Royale-Stil)
- Match-Dauer: 10-15 Min
- Anti-Cheat: Schwerste Implementation (8P, viele State-Updates)

### 11.6 Matchmaking & Skill-Pool

**Skill-System:** Modifizierte Glicko-2 (besser als ELO für volatile Spieler):
- Initial-Rating: 1500
- RD (Rating Deviation): 350 → schrumpft mit jedem Match
- Volatility: variabel, fast Lerning

**Pool-Brackets:**
- Bronze: 0-1199
- Silver: 1200-1499
- Gold: 1500-1799
- Platinum: 1800-2099
- Diamond: 2100+ (Top 5 % bekommen "Champion"-Banner)

**Match-Suche:**
- Initial-Range: ±50 MMR
- Nach 30s: ±100 MMR
- Nach 60s: ±200 MMR + Bot-Filler
- 90s-Timeout: Auto-Bot-Match

**Bot-Quality skaliert mit Spieler-MMR:**
- Bronze-Bot: 60 % Win-Rate vs Bronze
- Diamond-Bot: 45 % Win-Rate vs Diamond (Beta-Phase könnte intensiver werden)

---

## 12. Co-op-Modi (konkret)

### 12.1 Co-op Story (2 Spieler)

**Wie es funktioniert:**
- Player 1 erstellt Co-op-Lobby, lädt Friend ein (Friend-Code oder Username)
- Beide wählen Hero (gleicher Hero erlaubt, da PvE)
- Photon-Realtime-Room, Host = Player 1
- Match-Logik: Identisch zu Solo-Story, aber 2 Spieler im Spielfeld
- Bei Spieler-Tod: Re-Spawn nach 10s an Random-Safe-Tile (max 3× pro Level)
- Bei Both-Tot: Level-Fail
- Belohnungen: Geteilt (Coins + Karten + Stars), individuell BP-XP

**Schwierigkeit-Skalierung:**
- 2P: Enemy-HP +50 %, +25 % Score-Multi

### 12.2 Co-op Dungeon (2-4 Spieler)

**Wie es funktioniert:**
- 2-4-Spieler-Lobby, Spieler 1 hostet
- Dungeon-Map (10 Floors) wie alt, aber jedes Floor 2-4P-Scaled
- Loot geteilt (Random pro Spieler verteilt)
- Buff-Pick-Phase nach jedem Floor: Jeder Spieler picked 1 von 3 Buffs (Vor-Synergie-Visualisierung)

**Schwierigkeit-Skalierung:**
- 2P: Enemy-HP +50 %, Floor-Mod-Difficulty +1
- 3P: HP +100 %, Mod +2, extra Spawn-Wave
- 4P: HP +150 %, Mod +3, 2 extra Spawn-Waves, Boss-Phase-3-zwingend

### 12.3 Co-op Boss-Raid (Monatlich)

- 4 Spieler vs. 1 Mega-Boss
- Boss-HP: 50+, sehr lange Match-Dauer (15-20 Min)
- Mehrere Phasen (5-7), eskalierende Mechaniken
- Raid-Loot: 1 garantierte Legendary-Karte + Frame + Trail-Cosmetic
- Reset: 1× pro Monat freie Teilnahme, weitere Versuche kosten Energy oder Rewarded-Ad

### 12.4 Local Couch-Coop (PC + Mobile Tablet)

- 2-4 Gamepads (PC: USB/Bluetooth, Mobile Tablet: 2 Bluetooth-Gamepads)
- Splitscreen oder Shared-Screen
- Identische Modi wie Online-Co-op
- Keine Online-Liga-Punkte (anti-Cheese)

---

## 13. Dungeon-Roguelike (16 Buffs, 5 Synergien)

### 13.1 Run-Struktur

**Floor-Map (Node-Map à la Slay-the-Spire):**
- 10×3 Knoten-Map (10 Floors × 3 Parallel-Nodes)
- Spieler picked Pfad zwischen Nodes
- Boss-Floors: 5 (Mini-Boss), 10 (End-Boss), 15+ Endless

**Raum-Typen (Gewichtung):**
- Normal-Combat (40 %): Bomb-Action
- Elite-Combat (20 %): Elite-Enemies + besser Loot
- Treasure (15 %): Karten + Affix + Coins
- Challenge (15 %): Skill-Test mit Bonus
- Rest (10 %): Heal + Buff-Pick

**Eintritt:**
- 1× pro Tag gratis
- Weitere: 500 Coins, 3 Gems oder Rewarded-Ad

**Run-Reset:** Tod oder Manuelle-Aufgabe → Spieler verliert Dungeon-Buffs, behält Coins/Karten/Loot

### 13.2 16 Dungeon-Buffs (5 Common, 5 Rare, 2 Epic, 4 Legendary)

#### Common (5)
- Heal: +1 HP (Permanent für Run)
- Bomb-Slot: +1 MaxBombs
- Coin-Rush: +20 % Coin-Drops im Run
- Speed-Boost: +1 SpeedLevel
- Range-Up: +1 FireRange

#### Rare (5)
- Crit-Chance: +10 %
- Bomb-Crit: ×1.5 Damage Crit
- Affix-Drop-Up: +1 Affix-Drop pro Floor
- Shield-on-Hit: 1 HP-Shield bei jedem Floor-Komplettieren
- Reflect: 15 % Chance Damage zurück an Quelle

#### Epic (2)
- Combo-Multiplier: ×2 Score-Combo-Bonus
- Heal-on-Crit: +1 HP bei Krit-Hit (5s-Cooldown)

#### Legendary (4)
- **Berserker**: Bei <50 % HP +50 % Damage
- **TimeFreeze**: 1× pro Floor: Stop alle Gegner 3s (Manual-Trigger)
- **GoldRush**: ×3 Coin-Drops im Run
- **Phantom**: Spieler unsichtbar 5s nach jedem Floor-Komplettieren

### 13.3 5 Synergien (aus 16 Buffs)

| Synergie | Buff-Kombination | Effekt |
|----------|------------------|--------|
| **Bombardier** | 3+ Bomb-Buffs (Slot/Range/Crit/Bomb-Crit) | +50 % Bomb-Damage zusätzlich |
| **Blitzkrieg** | Speed-Boost + 2 weitere Mobility-Buffs | Bomb-Tick-Timer −0.5s |
| **Festung** | Tank-Buffs (Heal + Shield + Reflect) | +1 Max-HP-Cap |
| **Midas** | Coin-Rush + GoldRush + 1 weiterer Coin-Buff | Karten-Drops Rarity +1 Stufe |
| **Elementar** | 5+ Cards mit unterschiedlichen Damage-Typen | Krit-Chance gegen Bosse ×2 |

### 13.4 Truhen + Loot

- Floor-1-9 normal: 0-2 Buffs zur Wahl (3 Optionen)
- Floor-5 Mini-Boss: 3 Buffs (3 Optionen) + 1 Affix-Drop + Heal-Full
- Floor-10 End-Boss: 1 Legendary-Karte + 1 Legendary-Affix + Buff
- Endless ab Floor 11+: Spawn-Waves +50 %, Buff-Pool re-rolled

---

## 14. Liga + Ranking

### 14.1 Liga-Struktur

| Tier | Sub-Tiers | Punkte-Range | Belohnungs-Klasse |
|------|-----------|--------------|-------------------|
| **Bronze** | I/II/III | 0-799 | Common Frame |
| **Silver** | I/II/III | 800-1599 | Common Trail + Frame |
| **Gold** | I/II/III | 1600-2399 | Rare Trail + Frame + Victory |
| **Platinum** | I/II/III | 2400-3199 | Epic Trail + Frame + Victory + Hero-Skin |
| **Diamond** | (Single) | 3200+ | Legendary Cosmetic + "Champion"-Banner |

**Tier-Up:** 5 Wins → 1 Sub-Tier (außer Diamond, das ist ladder-basiert).
**Tier-Down:** 5 Losses in Folge → 1 Sub-Tier zurück. Aber: Tier-Floor (z.B. Gold-Erreicht-Floor) verhindert Sturz unter Tier-Anfang.

### 14.2 Saison-Reset

- 14-Tage-Saisons
- Reset: Liga-Tier sinkt um 2 Sub-Tiers (z.B. Gold-II → Silver-I)
- Diamond-Spieler bleiben in Diamond (kein Reset für Top-Spieler)
- Saison-End-Belohnungen: Skins, BP-XP-Bonus, Special-Cosmetics

### 14.3 NPC-Backfill (aus alt portiert)

- Bei < 20 echten Spielern in Tier: NPC-Spieler mit deterministischem Seed werden in Liste eingefügt
- NPC-Punkte werden simuliert (langsam ansteigend)
- Echte Spieler vor NPCs in Display

### 14.4 Daily-Race (separate Liga)

- 1 deterministisches Tages-Level (alle Spieler weltweit identisch)
- Schnellster Komplett-Run gewinnt
- Tier-Belohnungen: Top-100 / Top-1000 / Top-10k pro Region

---

## 15. Battle Pass + Saisons

### 15.1 Saison-Struktur

**Dauer:** 8 Wochen (statt 4 wie alt)
**Tiers:** 60 (mit 25 Sofort-Tiers bei Premium-Plus)
**Free-Track:** 40 von 60 Tiers
**Premium-Track:** Alle 60 Tiers + Saison-Skin
**Premium-Plus-Track:** Premium + 25 Sofort-Tiers + Exclusive-Cosmetic-Set

### 15.2 BP-Reward-Tier-Liste (Beispiel S1 "Aufstand")

| Tier | Free | Premium |
|------|------|---------|
| 1 | 100 Coins | 200 Coins + 1 Common-Affix |
| 5 | "Rookie"-Frame (Common) | "Cyber-Frame-S1" (Rare) |
| 10 | 1 Rare-Karte | Karte-Lv-Token (Skip 1 Lv) |
| 15 | 50 Gems | "Pyro-Suit-Skin" (Epic) für NOVA |
| 20 | 200 BP-XP-Boost | 200 BP-XP + Trail-Cosmetic |
| 25 | Coin-Multiplier 24h | "Mech-Glow-Trail" (Rare) |
| 30 | 1 Epic-Karte | 1 Legendary-Affix |
| 35 | Banner-Saison-S1 | 1 neuer Hero "PULSE" (für S2 vorausgesagt) — diese Saison: "Hero-Token" (Buy any Hero) |
| 40 | 100 Gems | 250 Gems |
| 45 | Frame-Rare | Frame-Epic + 1 Random-Skin |
| 50 | 300 Coins | "Aufstand"-Skin für PHANTOM |
| 55 | 50 Gems | 200 Gems + Victory-Animation |
| **60 (Top)** | 1 Epic-Karte | **Legendary-Hero-Skin "Cyber-NOVA"** + Frame + Trail + Victory + Banner |

### 15.3 Saison-Themes (16 geplant)

| Saison | Theme | Visual-Sprache |
|--------|-------|----------------|
| S1 | "Aufstand" | Klassisch-Cyber, Cyan/Magenta |
| S2 | "Mech-Wars" | Pacific-Rim, Industrial-Schwer |
| S3 | "Neon-Nights" | Synthwave, Pink/Purple |
| S4 | "Glitch in the System" | Hacker, Green-Code-Rain |
| S5 | "Halloween" | Spooky Cyber-Halloween |
| S6 | "Cyber-Winter" | Eis-Tech, Blau/Weiß |
| S7 | "Pacific Drift" | Tropical-Cyber, Sunset |
| S8 | "Underground" | Untergrund-Subkultur, Graffiti |
| S9 | "Crimson-Tide" | Apokalyptisch, Rot/Schwarz |
| S10 | "Sky-High" | Anti-Grav-Welt, Pastell-Sky |
| S11 | "Crystal-Cave" | Eis-Cave, Holo-Kristalle |
| S12 | "Mecha-Royale" | Royale-Hommage, Fantasy-Cyber |
| S13 | "Bio-Punk" | Mutanten, Toxic-Green |
| S14 | "Vaporwave-Heaven" | Vaporwave-Pastels |
| S15 | "Steam-Net" | Steampunk meets Cyber |
| S16 | "Re-Genesis" | True-Ending-Sequel-Theme |

---

## 16. Cosmetics + Player-Identity

### 16.1 Cosmetic-Typen + Volumen

| Typ | Beschreibung | Pool-Größe (Launch) |
|-----|--------------|---------------------|
| **Hero-Skins** | Komplettes Mech-Modell | 8 Helden × 5 Skins = 40 |
| **Bomb-Skins** | Bomb-Modell + VFX-Variante | 22 Karten × 3 Skins = 66 |
| **Map-Skins** | Welt-Theme tauschen | 10 Welten × 2 Themes = 20 |
| **Avatar-Frames** | Profilbild-Umrandung (animiert) | 33 (aus alt) + 10 neu = 43 |
| **Trail-Effects** | Spuren beim Bewegen | 32 (aus alt) + 8 neu = 40 |
| **Victory-Animations** | Spieler nach Match-Sieg | 33 (aus alt) + 10 neu = 43 |
| **Emotes** | 8 freischaltbare Match-Wheel-Emotes | 32 (4 pro Hero) |
| **Sprays** | Decals auf Spielfeld | 20 |
| **Match-Intros** | 2-3s Animation beim Match-Start | 15 |

**Total Launch:** ~320 Cosmetics. Plus Saison-Erweiterung: +60 pro Saison.

### 16.2 Cosmetic-Quellen

| Quelle | Anteil |
|--------|--------|
| Battle Pass (Free + Premium) | 30 % |
| Liga-Tier-Rewards | 10 % |
| Achievement-Rewards | 15 % |
| Saison-Event-Drops | 20 % |
| Cosmetic-Shop (Gems-Kauf) | 15 % |
| Premium-Hero-Skin-Direkt-Kauf (Real-Money) | 10 % |

### 16.3 Player-Identity

Jeder Spieler hat ein **Profil** mit:
- Username + Banner-Skin
- Avatar-Frame
- Aktiver Trail + Victory + Spray
- Hero-Display (max 3 Lieblings-Helden auf Profil)
- Showcase-Karten (3 Lieblingsbomben mit Affixen)
- Stats: Total-Kills, Win-Rate pro Modus, Saison-Highscore
- Achievements-Showcase (3 wichtigste)
- Banner-Animation für Diamond-Spieler

---

## 17. Onboarding + Tutorial (T1-T4)

### 17.1 Tutorial-Phasen

**T1: Movement & Basics** (Level 1, ~3 Min)
- Joystick + Bomb-Button kennenlernen
- Erste Bombe legen, Brick zerstören
- Exit finden, Level abschließen
- Belohnung: 50 Coins, 1 Common-Karte

**T2: Bomben & PowerUps** (Level 2-3, ~5 Min)
- BombUp, Fire, Speed-PowerUps erklären
- 2 Bomben gleichzeitig legen
- Erstes Gegner-Kill
- Belohnung: 100 Coins

**T3: PowerUps Advanced** (Level 4-5, ~5 Min)
- Detonator, Kick, Wallpass
- Combo-System einführen
- Erster Star-Rating-Reveal
- Belohnung: 1 Rare-Karte

**T4: Multiplayer-Intro** (nach L10 freigeschaltet, optional)
- Erster Co-op-Match mit Bot-Partner
- Erster PvP-Match gegen Bot (Glicko-2-Starter-Match)
- Friends-System-Einführung
- Belohnung: Welcome-Frame + 200 Gems

### 17.2 Feature-Unlock-Choreographie (aus alt portiert + erweitert)

| Lv | Feature | Tutorial-Overlay |
|----|---------|------------------|
| 1 | Story-Mode | Tutorial T1 |
| 2 | Coins + Shop | Erste-Coin-Hint |
| 5 | Hero-Auswahl (NOVA + Cryo + Titan) | Hero-Picker-Modal |
| 10 | Co-op (Bot first) | T4 Co-op-Tutorial |
| 12 | Daily-Challenge | Daily-Reward-Modal |
| 15 | Card-System + Deck | Deck-Builder-Tutorial |
| 20 | Talent-System | Talent-Tree-Reveal |
| 20 | PvP-Match (Bot first) | T4 PvP-Tutorial |
| 25 | Dungeon-Modus | Dungeon-Intro-Cutscene |
| 30 | Clan-System | Clan-Tutorial |
| 35 | Affix-System | Affix-Modal |
| 40 | Master-Mode (nach L100) | Master-Mode-Reveal |
| 50 | Boss-Rush | Boss-Rush-Intro |
| ach_master_100 | Champion-Skin | Achievement-Cinematic |

---

## 18. Achievements (66 + 20 neu)

> Aus alt portiert + Sci-Fi-Setting + Multiplayer-spezifische.

### 18.1 Kategorien

- **Story** (16 Achievements): Welt-1-komplettieren ... Welt-10, True-Ending, alle Memory-Fragmente
- **Skill** (12): Erste Combo ×5, ×10, ULTRA-Combo, 0-Death-Run, etc.
- **Collection** (12): Alle Karten, Alle PowerUps, Alle Affixe, Alle Helden-Skins
- **Multiplayer** (10): Erstes PvP-Win, 100 PvP-Wins, Saison-Diamond, Clan-War-Gewinn
- **Cooperative** (8): 50 Co-op-Matches mit Friend, 100-Dungeon-Floors
- **Mastery** (8): Lv60-Hero-Max, alle Talent-Capstones freigeschaltet

### 18.2 Neue Sci-Fi-spezifische Achievements (20)

- "Lyras Versprechen" (alle Memory-Fragments gesammelt)
- "Cyber-Diamond" (Diamond + 100 PvP-Wins in einer Saison)
- "Glitch-in-the-Matrix" (GLITCH-Hero Lv60 + Hack-Bomb 100 erfolgreich)
- "OmniCorp-Insider" (alle Council-Bosse erstesmal besiegt)
- "Nexus-Bezwinger" (True-Ending erreicht)
- "Bombenmeister" (alle 22 Karten Lv5 erreicht)
- "Acht-Gänger" (alle 8 Helden Lv60)
- "Talent-Master" (alle Capstones aller Helden freigeschaltet)
- "Clan-König" (Clan-Leader bei 50+ Clan-Wars)
- "Bobby Phenomenal" (Tournament Top-3 Plazierung)
- "Mech-Mechaniker" (50 Affixe gecraftet)
- "Stadtschützer" (100 Co-op-Story-Komplettierungen)
- "Phantom-Killer" (50 PvP-Wins als PHANTOM mit Phantom-Bomb Trigger)
- "Speed-Demon" (50 PvP-Wins als FLUX mit Blink-Trigger)
- "Veteran" (Account-Alter >1 Jahr + 365 tägliche Logins)
- "Comeback-King" (10× nach 0-1 Score-Deficit gewonnen)
- "Ultra-Combat" (×20 Combo erreicht — schwer aber machbar)
- "Tier-S-Champion" (Diamond + Saison-Top-100)
- "Buddy-System" (100 Co-op-Matches mit gleichem Friend)
- "OG-Bomber" (Migrations-Achievement für alt-BomberBlast-Spieler)

---

## 19. Daily + Weekly + Live-Events

### 19.1 Daily Quests (3 pro Tag, rotiert)

- "Spiele 1 PvP-Match" (+100 Coins)
- "Lege 50 Bomben" (+150 Coins)
- "Kille 30 Gegner in Story" (+200 Coins + 5 BP-XP)
- "Co-op-Run mit Friend" (+200 Coins + 1 Affix-Drop)
- "Tagesziel-3-Sterne in 3 Levels" (+1 Random-Karte)
- "10 Combo-Streaks ×3 oder höher" (+10 Gems)

### 19.2 Weekly Missions (5 pro Woche, Pool-Rotated)

- "10 Story-Levels mit 3 Sternen" (+1 Epic-Karte)
- "5 PvP-Wins" (+15 Liga-Punkte-Bonus + 100 Gems)
- "3 Co-op-Dungeon-Runs" (+1 Affix + Frame)
- "1 Boss-Rush komplettieren" (+1 Hero-Token)
- "Level-Up 2 Helden" (+1 Talent-Reset gratis)

### 19.3 Live-Events

| Event-Typ | Frequenz | Spezial |
|-----------|----------|---------|
| **Wochen-Event** | Alle 7 Tage | DoubleXP, DoubleCoins, CardRain, BossWeek, DungeonRush, LeagueRumble, MissionMadness, LuckyWeek (16 Events rotierend) |
| **Saison-Event** | Pro Saison 1× | Themed Limited-Time-Mode, exklusiver Cosmetic |
| **Wochenend-Tournament** | Alle 2 Wochen | PvP-Bracket, Top-100-Belohnungen |
| **Boss-Raid** | Monatlich | 4-Spieler-Mega-Boss, Raid-Loot |
| **Clan-War** | Alle 2 Wochen | 4v4-Clan-Async-Battles |
| **Saison-Story-Episode** | Pro Saison | Voiced Story-Episode, 30-60 min Content |
| **Limited-Time-Modes** | Random | "Bombe pro Sekunde", "Alle Helden zufällig", etc. |
| **Welt-Tour** | Halb-jährlich | Alle 10 Welten kurz hintereinander spielen, Reward-Run |

---

## 20. Economy + IAP-Pyramide

### 20.1 Währungen

| Währung | Source | Verwendung |
|---------|--------|-----------|
| **Coins** | Gameplay (Level-Komplettion, Win-Bonus) | Karten-Crafting, Talent-Reset (50 Gems oder 5k Coins), Shop-Cosmetics |
| **Gems** | IAP, Quests, BP, Liga | Battle-Pass-Skip, Premium-Karten, Hero-Direct-Buy |
| **BP-XP** | Gameplay (Quests, Achievements) | BP-Tier-Up |
| **Liga-Punkte** | PvP-Matches | Saison-End-Rewards |
| **Hero-Token** | Selten (BP Tier 35, Shop) | Direct-Hero-Unlock (statt Gem-Kauf) |
| **Saison-Coins** | Saison-Events | Saison-Cosmetic-Shop (Event-only) |
| **Dungeon-Coins** | Dungeon-Floor-Wins | Dungeon-Upgrades (permanente Buffs für Dungeon) |
| **Clan-Coins** | Clan-Activity | Clan-Upgrades, Clan-War-Boosts |

### 20.2 IAP-Pyramide

| Produkt | Preis | Inhalt |
|---------|-------|--------|
| **Gem-Pack: Tiny** | 0,99 EUR | 100 Gems |
| **Gem-Pack: Small** | 4,99 EUR | 500 Gems + 50 Bonus |
| **Gem-Pack: Medium** | 9,99 EUR | 1.000 Gems + 150 Bonus |
| **Gem-Pack: Large** | 19,99 EUR | 2.000 Gems + 350 Bonus |
| **Gem-Pack: XL** | 49,99 EUR | 5.000 Gems + 1.000 Bonus |
| **Gem-Pack: Mega** | 99,99 EUR | 10.000 Gems + 2.500 Bonus |
| **Starter-Pack** | 4,99 EUR | 5k Coins + 1k Gems + 1 Hero-Token (einmalig, erste 14 Tage) |
| **Founders-Pack** | 29,99 EUR | 3 exklusive Helden + 50k Coins + 5k Gems + Founders-Frame (einmalig, Launch-Window 30 Tage) |
| **Battle-Pass-Premium** | 9,99 EUR | Saison-Premium-Track |
| **Battle-Pass-Plus** | 19,99 EUR | Premium + 25 Sofort-Tiers + Plus-Skin |
| **Subscription "Bomber-Pro"** | 4,99 EUR/Monat | Tägliche 100 Gems + Werbefrei + Coin-Boost +50 % + Cosmetic-Shop -10 % |
| **Hero-Direkt-Kauf** | 9,99 EUR | Direkter Hero-Unlock (alt nicht-Token) |
| **Saison-Hero-Direct** | 12,99 EUR (Premium-Saison-Hero, exklusiv) | Saison-Held |
| **Remove-Ads** | 4,99 EUR | Banner-Ads weg, Rewarded-Ads bleiben (DSGVO) |
| **Saison-Bundle** | 14,99 EUR | Saison-Premium + Saison-Skin + 500 Gems |

### 20.3 Werbe-Modell

- Banner-Ads im Hub-Menü (entfällt bei Remove-Ads oder Bomber-Pro)
- Rewarded-Ads für Coins/Gems/Continue (bleibt immer, opt-in)
- Interstitial nach Match-Ende, max 1× alle 5 Min (kann auf "Off" gestellt werden)

### 20.4 Anti-Monetization-Ethik

- KEINE Lootboxen (gegen UK/China-Regulierung)
- LuckySpin behält transparente Drop-Rates + Pity-Counter
- Saison-Content immer auch über Gameplay erreichbar (Coin-Kauf nach Saison)
- Keine Pay-to-Win-Stats (alle Helden statistisch identisch in PvP-Ranked, nur cosmetic + Mechanik-Variation)
- DSGVO-konformes Marketing-Tracking-Opt-In (siehe Compliance-Sektion in ROADMAP.md)

---

## 21. Accessibility-Mandate

> Übernommen aus alt + erweitert für Voll-Voice und 3D-Visuals.

| Mandat | Implementierung |
|--------|-----------------|
| **Colorblind-Modes** | Deuteranopia, Protanopia, Tritanopia via ColorMatrix-Filter (URP-PostProcessing) |
| **HighContrast** | Outline-Pass auf allen Entities, +50 % Brightness auf HUD |
| **UI-Scale** | 0.75 / 1.0 / 1.25 / 1.5 für TextMeshPro-Texte + HUD-Elements |
| **Reduced-Motion** | Animationen 50 % reduziert, kein Screen-Shake, keine starken Particle-Effekte |
| **Subtitles** | Voll-Voice-Untertitel mit 4-Caption-Pool (alt portiert) |
| **Voice-Speed** | 0.75× / 1.0× / 1.25× (für hörgeschädigte Spieler) |
| **Touch-Optionen** | Joystick-Größe + Bomb-Button-Position konfigurierbar, Fixed vs Floating-Joystick |
| **Gamepad-Re-Map** | Buttons frei zuweisbar (PC + Mobile mit Bluetooth-Gamepad) |
| **Photosensitivity-Mode** | Reduziert Flash-Effekte (Combo-Flash, Damage-Flash) |
| **Audio-Descriptions** (Phase 2) | Optional Voice-Over für UI-Hinweise (vorlesen) |
| **Multi-Sprache** | DE/EN/ES/FR/IT/PT (alt-Parität), JP/KR (Phase 2) |

---

## 22. Audio-Design

### 22.1 Music

**Welt-Themes:** Pro Welt 1 Hauptloop mit 4 Layers:
- Layer 1 (Base): Drone-Synth + Sub-Bass
- Layer 2 (Standard): + Drums + Mid-Synth
- Layer 3 (Combat): + Lead-Synth + Effects
- Layer 4 (Boss-Battle): + Cinematic-Brass + Intensive-Beat
- Layer 5 (Victory): Sting + Triumph-Beat

**Transitions:** FMOD-Studio mit Crossfade-Markers. Layer-Switch in <500 ms.

**Hub-Music:** 1 Hauptloop (Chill-Cyberpunk-Vibe) + Subtle-Variations für verschiedene Tabs.

### 22.2 SFX

**Bomb-Tick + Explosion (pro Bomb-Typ):**
- Tick: Sample mit Pitch-Variation ±5 %
- Explosion: 3D-Spatial-Position-Sound mit Reverb-Pro-Welt
- Tail: Welt-spezifisches Echo (Cave-Welt = lang, Outdoor = kurz)

**Hero-VoiceLines:** Pro Hero ~10 Lines × 8 Helden = 80 Lines pro Sprache
- DE + EN Voice-Tracks für alle Helden
- AI-generiert via ElevenLabs (siehe Asset-Pipeline-Doku)
- 6 Sprachen für UI + Announcer + Stinger

### 22.3 Voice-Acting (AI-generiert)

**Workflow:**
1. **Text-Skript** in `Resources/Voices/` als Markdown-Files (1 pro Hero + Story)
2. **ElevenLabs API** mit Stimm-Profilen (8 Helden-Stimmen pro Sprache vorab gewählt)
3. **Render-Pipeline** generiert WAV-Files
4. **Quality-Gate**: Mensch hört alle Lines, Re-Render bei Issues
5. **Mastering**: ffmpeg-Pipeline für LUFS-Normalisierung auf −16 LUFS
6. **Lokalisierung**: Pro Sprache eigene Voice-Profile, gleicher Charakter aber sprachspezifische Stimme

**Lizenz-Mitigation:**
- ElevenLabs-Enterprise-Plan mit kommerzieller Voice-Lizenz
- Backup-Plan: Bei Lizenz-Konflikt schnell auf "stille Helden + Announcer-Only" downgraden
- Schlüssel-Charaktere (Director Vex, Sage) optional von Mensch-Sprechern (Premium-Quality)

---

## 23. UI/UX-Konzept

### 23.1 Haupt-UI-Bildschirme

| Bildschirm | Layout | Tech |
|-----------|--------|------|
| **Boot-Splash** | Logo + Loading-Bar | UGUI (statisch) |
| **Login/Register** | Tab-Auth (Email/Google/Apple) | UI Toolkit |
| **Main-Hub** | 5 Tabs (Home / Play / Shop / Clan / Profile) + 3D-Skybox | UI Toolkit (Tab-Bar) + UGUI (Animationen) |
| **Play-Tab** | 4 Modi-Karten (Story / PvP / Co-op / Dungeon) | UI Toolkit |
| **PvP-Lobby** | Hero-Pick + Map-Reveal + Player-Slots | UGUI (Animation-haftig) |
| **In-Game-HUD** | Joystick + Bomb-Button + Combo + Lives + Coins + Hero-Skill-Bar | UGUI (frame-perfect) |
| **Settlement-Modal** | Belohnungen-Reveal mit Animation | UGUI |
| **Shop** | Tabs (Cosmetics / Gems / Premium) | UI Toolkit |
| **Clan** | Chat + Member-List + War-Stats | UI Toolkit |
| **Settings** | Tabs (Audio / Graphics / Controls / Accessibility) | UI Toolkit |

### 23.2 HUD-Layout (In-Game)

**Linke Seite (Touch-Joystick-Bereich):**
- Joystick (75 dp Radius)

**Rechte Seite (Bomb-Button-Bereich):**
- Bomb-Button (52 dp)
- Detonator-Button (48 dp)
- Card-Quickswap (4 Buttons, je 36 dp) → Auswahl Bombe
- Hero-Skill-Buttons 1/2/3 + Ultimate (4 Buttons, je 44 dp)

**Oben:**
- Combo-Anzeige (Mid)
- Lives-Counter (Right)
- Coins-Anzeige (Right)
- Time-Remaining + Score (Mid-Right)

**Unten (kompakt):**
- Mini-Map (PvP/Co-op) — kleines 4×4-Grid mit Player-Positions

### 23.3 Modal-System

Zentraler `ModalService.ShowAsync<TViewModel>(args)`:
- Modal-Stack (max 3 tief)
- Back-Button schließt oberstes Modal
- IsHitTestVisible-Aggregat pro Layer (alt-Pattern übernommen)
- 200ms Slide-In + Fade-Background mit DOTween

---

## Änderungslog (DESIGN)

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-DESIGN.md mit 8 Helden, 10 Welten, 22 Karten, Story-Arc, PvP/Co-op-Specs | Robert Schneider + Claude |

---

> **Status:** Konzept finalisiert für Stakeholder-Review.
> **Nächste Schritte:** Concept-Art-Sprint, Sound-Library-Auswahl, Voice-Acting-Cast-Sheets.
