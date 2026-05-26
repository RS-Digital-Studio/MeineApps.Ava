# BomberBlast Unity — Master-Plan

> **Status:** Konzept-Phase (Stand 2026-05-26)
> **Arbeitstitel:** BomberBlast / **Marken-Vorschlag:** "BomberBlast Arena"
> **Genre:** 3D-Top-Down Bomberman-Action mit Meta-Progression, Real-time-PvP und Co-op
> **Setting:** Sci-Fi/Cyberpunk mit Mech-Bombern (eigene Welt, nicht mit ArcaneKingdom verbunden)
> **Plattformen:** Android + iOS + Steam (Windows/macOS/Linux), Cross-Save aber separate PvP-Pools
> **Team:** Full Studio (5+ Personen) — siehe [ROADMAP.md](ROADMAP.md#team)
> **Launch-Strategie:** Soft-Launch DACH → EU → Global

Dieses Dokument ist die **Master-Übersicht**. Tiefe in:

| Bereich | Datei |
|---------|-------|
| Game-Design (Helden, Welten, Story, Karten, Modi, Live-Service) | [DESIGN.md](DESIGN.md) |
| Tech-Architektur (Stack, Asmdefs, Netcode, Anti-Cheat, Performance) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Produktion (Roadmap, Team, Marketing, Compliance, Risiken) | [ROADMAP.md](ROADMAP.md) |
| Code-Conventions, bekannte Stolperfallen | [CLAUDE.md](CLAUDE.md) |
| KI-Asset-Pipeline (3D-Meshes + PBR-Texturen via ComfyUI/Hunyuan3D) | [ASSETS_AI.md](ASSETS_AI.md) |
| First-Time-Setup für Entwickler (folgt nach Projekt-Anlage) | SETUP.md |
| Cloud-Functions-Server-Doku (folgt) | Server/SERVEROPS.md |

---

## Inhaltsverzeichnis

1. [Vision & Pitch](#1-vision--pitch)
2. [Zielgruppe & Personas](#2-zielgruppe--personas)
3. [Strategische Entscheidungen (Stand 2026-05-26)](#3-strategische-entscheidungen-stand-2026-05-26)
4. [Erfolgs-KPIs](#4-erfolgs-kpis)
5. [Was bleibt vom alten BomberBlast?](#5-was-bleibt-vom-alten-bomberblast)
6. [Was ändert sich fundamental?](#6-was-ändert-sich-fundamental)
7. [USPs (Unique Selling Points)](#7-usps-unique-selling-points)
8. [High-Level-Roadmap](#8-high-level-roadmap)
9. [Risiko-Summary](#9-risiko-summary)
10. [Nächste konkrete Schritte](#10-nächste-konkrete-schritte)

---

## 1. Vision & Pitch

### 1.1 Elevator-Pitch

> **BomberBlast Arena** ist ein **3D-Top-Down Bomberman-Action-Spiel** in einer dystopischen
> Sci-Fi-Welt, in der **Mech-Bomber-Piloten** in 4-Spieler-Real-time-Arena-Battles um die
> Vorherrschaft kämpfen. Spieler sammeln 8+ Helden mit eigenen Mech-Designs, Talent-Bäumen und
> 14+ Bomben-Karten. Das Spiel kombiniert Action-PvP, kooperative Roguelike-Dungeons und einen
> tiefen Story-Modus (100 Level in 10 Welten) mit Welt-Mythologie und dem zentralen
> "Bombenmeister-Krieg" gegen den Megakonzern OmniCorp.

### 1.2 Brand-Identität

| Aspekt | Wert |
|--------|------|
| **Markenfarben** | Cyan #22D3EE (Akzent 1) + Magenta #EC4899 (Akzent 2) + Tiefdunkel #0F172A (Base) |
| **Tonalität** | Edgy-Cyber, aber nicht zu düster. Slay-the-Spire-Selbstironie statt Cyberpunk-2077-Düsternis |
| **Audio-Sprache** | Synthwave + Drum-and-Bass + leichte 80er-Anleihen |
| **Visual-Sprache** | Mech-Sprites, Neon-Akzente, holografische HUDs, glitch-Effekte als Stil-Element |
| **Story-Setting** | Nahe Zukunft, ~2087, nach dem "Großen Crash". Megakonzern OmniCorp kontrolliert die letzten lebbaren Megacities |

---

## 2. Zielgruppe & Personas

### 2.1 Kern-Zielgruppe

**Persona A: "Der nostalgische Wettkämpfer" (40 % der Zielgruppe)**
- Alter: 28-42, primär männlich
- Spielte Bomberman in Kindheit (SNES, NES, PS1)
- Will modernes Multiplayer-Bomberman auf Mobile/PC
- Bereit für Battle Pass + Cosmetics, ablehnend gegenüber Lootboxen
- Spielt 30-60 Min pro Session, 4-5× Woche
- ARPPU-Ziel: 25-40 EUR/Saison

**Persona B: "Der Casual-Mobile-Action-Gamer" (35 %)**
- Alter: 18-35, gemischt
- Spielt Brawl Stars, Clash Royale, Among Us
- Kommt für 10-15-Min-Sessions, mehrmals täglich
- Free-to-Play akzeptiert, kauft selten (2-3× Jahr)
- ARPPU-Ziel: 10-20 EUR/Jahr

**Persona C: "Der Streamer/Content-Creator" (15 %)**
- Alter: 20-30
- Twitch/YouTube/TikTok-Inhalte produziert
- Sucht eintaktbare PvP-Action für Stream
- Will Replay-Sharing, Spectator-Mode, Highlights-Tool
- Indirekter Wert: Marketing-Multiplikator, kein direkter ARPPU

**Persona D: "Der Hardcore-Esports-Aspirant" (10 %)**
- Alter: 16-25
- Spielt Rocket League, Apex, Valorant kompetitiv
- Will Ranking-Liga, Tournament-Modus, Skill-Ceiling
- ARPPU-Ziel: 50-100 EUR/Saison (Founders-Pack, Saison-Skins)

### 2.2 Sekundär-Märkte

- **Family-Co-op**: Eltern spielen mit Kindern Co-op-Story (USK 12, PEGI 7)
- **Lokale LAN-Parties** (PC-Couch-Coop): 2-4 Spieler an einem Gerät via Gamepads
- **Mobile-Bus/Bahn-Spieler**: Async-Modi (Daily-Race, Liga) ohne Online-Verbindung

---

## 3. Strategische Entscheidungen (Stand 2026-05-26)

Diese 8 Weichen wurden bewusst gestellt und bilden das Fundament des Plans:

| # | Frage | Entscheidung |
|---|-------|--------------|
| 1 | Genre-Vision | **Hybrid:** Action-Core + Meta-Progression (Helden, Talente, Karten 2.0, Welt-Mythologie) |
| 2 | Multiplayer-Schwerpunkt | **Voll-Stack:** Real-time PvP (Photon Fusion) + Co-op (Photon Realtime) + Async-Liga (Firebase) |
| 3 | Visueller Stil | **Voll-3D Top-Down** mit URP, VFX Graph, Cinemachine — Mobile + PC |
| 4 | Hero-Pool zum Launch | **8 Helden** + 1 neuer Hero pro Saison |
| 5 | Welt-Story-Setting | **Eigene Welt: Sci-Fi/Cyber mit Mech-Bombern** (keine ArcaneKingdom-Verbindung) |
| 6 | Monetization | **Hybrid F2P:** Battle Pass + Cosmetic-Shop + saisonale Helden via Direct-Kauf (keine Lootboxen) |
| 7 | Launch-Region | **Soft-Launch DACH → EU → Global** |
| 8 | Team-Setup | **Full Studio (5+ Personen)** — Budget 500k+, 10-12 Monate bis Launch |
| 9 | Performance-Target | **60 FPS High-End, 30 FPS Low-End** mit dynamischer Tier-Skalierung |
| 10 | Cross-Play | **Separate Pools Mobile/PC**, gemeinsamer Account + Cross-Save |
| 11 | Voice-Acting | **AI-generiert (ElevenLabs)** mit Lizenz-Mitigation und menschlichem QA-Editing |

---

## 4. Erfolgs-KPIs

> **Wichtig:** Wir messen über drei Saisons (~6 Monate post-Launch). Erfolg wird **nicht** am Tag-1-DAU
> sondern an Retention + Monetization-Konsistenz definiert.

### 4.1 Akquisitions-KPIs (Launch-Window, erste 90 Tage)

| KPI | Soft-Launch DACH (Wochen 1-8) | EU-Launch (Wochen 9-16) | Global (Wochen 17-26) |
|-----|-------------------------------|--------------------------|------------------------|
| Downloads | 50k | 500k | 3-5M |
| D1-Retention | ≥ 35 % | ≥ 30 % | ≥ 25 % |
| D7-Retention | ≥ 15 % | ≥ 12 % | ≥ 10 % |
| D30-Retention | ≥ 8 % | ≥ 6 % | ≥ 5 % |
| Crash-Free-Users | ≥ 99 % | ≥ 99.3 % | ≥ 99.5 % |
| App-Store-Rating | ≥ 4.3 ⭐ | ≥ 4.4 ⭐ | ≥ 4.5 ⭐ |

### 4.2 Engagement-KPIs

| KPI | Ziel |
|-----|------|
| Sessions pro DAU | ≥ 3.5 |
| Session-Länge median | 8-12 Min |
| Daily PvP-Matches pro Aktiv-Spieler | ≥ 4 |
| Daily Co-op-Matches pro Aktiv-Spieler | ≥ 1.5 |
| Tutorial-Completion (T1-T3) | ≥ 85 % |
| Erste-Saison-BP-Tier-50-Reach | ≥ 30 % der Premium-Käufer |
| Clan-Membership-Rate | ≥ 25 % der MAU |

### 4.3 Monetarisierungs-KPIs

| KPI | Ziel |
|-----|------|
| ARPDAU (Average Revenue per Daily Active User) | 0.30-0.50 EUR |
| Conversion (Free→Pay) | ≥ 4 % |
| Battle-Pass-Premium-Conversion (pro Saison) | ≥ 12 % der MAU |
| Subscription "Bomber-Pro" Conversion | ≥ 2 % der MAU (Stretch-Goal) |
| ARPPU (Average Revenue per Paying User pro Saison) | 25-40 EUR |
| Saisonaler Founders-Pack-Sell-Through | ≥ 3 % der Launch-Cohort |

### 4.4 Technische KPIs

| KPI | Ziel |
|-----|------|
| Average Frame-Rate (High-End) | 60 FPS (± 2) |
| Average Frame-Rate (Low-End) | 30 FPS (± 3) |
| Match-Latency (P95) | < 80 ms in EU-Region |
| Anti-Cheat False-Positives | < 0.05 % |
| Cloud-Save-Sync-Success | ≥ 99.5 % |
| Server-Downtime (über 90 Tage) | < 0.5 % (~3.5 h) |
| App-Größe (Android-AAB) | < 250 MB |
| App-Größe nach Install (mit Addressables) | < 800 MB |

---

## 5. Was bleibt vom alten BomberBlast?

> Dem alten BomberBlast danken wir das stabile **Fundament an Domain-Code und Game-Design-Lehrgeld**.
> Folgendes wird **portiert oder konzeptionell übernommen**.

### 5.1 Direkt portierbare Domain-Logik

- **Determinismus-Foundation**: `DeterministicRandom` (xoshiro256+) + `ReplayCapture` (1 Byte/Tick)
- **A\*-Pathfinding**: Object-Pooled PriorityQueue + BFS-Safe-Cell-Finder
- **Combo-System**: Kills innerhalb 2s-Fenster mit Multiplier-Logik
- **Dungeon-Synergie-Resolver**: 5 Synergien aus 16 Buffs
- **Level-Layout-Generator**: 11 Layouts + Welt-Pool + Daily-Race-Seed
- **Liga-Logik**: 5 Tiers × 3 Sub-Tiers + Diamant + NPC-Backfill + Profanity-Filter
- **Battle-Pass-Logik**: XP-Tier-Berechnung + Free/Premium-Track
- **Achievement-Definitionen**: 66 Achievements als Templates
- **Coin/Gem-Overflow-Guard**: Anti-Hack-Pattern
- **Daily-Challenge-Determinismus**: ISO-Wochen-Seed-Pattern
- **Lucky-Spin Pity-Counter**: Lootbox-Compliance-konform
- **Anti-Cheat-Hybridtimer**: TickCount64 + UTC-Datetime gegen Datum-Manipulation
- **Profanity-Filter**: Unicode-NFKD-Normalisierung + Leetspeak-Wörterbuch

### 5.2 Strukturell übernommene Mechaniken

- **15×10 Grid** als kanonisches Spielfeld
- **14 Bomben-Karten** als Module-Slot-System (erweitert auf 22 mit 8 neuen Sci-Fi-Karten)
- **12 PowerUp-Typen** im Spielfeld
- **12 Enemy-Typen** + 5 Bosse + Boss-Modifier
- **10 Welten × 10 Level + Master-Mode** als Story-Spine
- **Roguelike-Dungeon** (16 Buffs, 5 Synergien, Node-Map à la Slay-the-Spire)
- **Cosmetic-Pipeline**: 98+ Items (Trails, Frames, Victories, Avatare, Emotes, Sprays)
- **Accessibility**: Colorblind, HighContrast, UiScale, Subtitles, Reduced-Motion
- **DSGVO**: Account-Delete + Data-Export

### 5.3 Conceptional Lessons-Learned (Was wir vermeiden)

Aus dem alten BomberBlast haben wir Patterns gelernt, die wir **bewusst anders machen**:

| Lesson Learned | Konsequenz im neuen Spiel |
|----------------|---------------------------|
| GameEngine-God-Class (~5.100 LOC) wurde mühsam in Partials gesplittet | Von Anfang an MonoBehaviour-Components statt einer Mega-Engine |
| `On`-Prefix-Verzicht bei Events war Avalonia-spezifisch | Unity-Standard: `OnXxxHappened` mit Past-Tense |
| Custom Icon-System (152 Icons) erforderte AppChecker-Ausnahme | Sprite-Atlases + Asset-Store-Icons (Standard-Workflow) |
| AdMob-Crashlytics-Konflikt war Wartungs-Hölle | Unity SDKs gehen sauberer mit AdMob+Crashlytics um |
| Cloud-Save-Schema-Migration v1→v2→v3 war reaktiv | Schema-Versionierung mit Forward-Migration-Pflicht von Tag 1 |
| Multiplayer war Foundation-only (nie integriert) | Multiplayer als First-Class-Citizen ab MVP |
| 643 Tests im xUnit-Stil, aber kein Determinismus-Sweep | Determinismus-Replay-Suite als Pflicht-CI-Check |
| Music + Voice-Mandat "kein Geld" lieferte Kenney-CC0-Schicht | AI-Voice (ElevenLabs) + kuratierte Premium-Libraries (Splice/Soundsnap) |

---

## 6. Was ändert sich fundamental?

| Achse | Alt (Avalonia/SkiaSharp) | Neu (Unity 6) |
|-------|--------------------------|---------------|
| **Engine** | Avalonia 12 + SkiaSharp CPU | Unity 6 + URP 17 + GPU |
| **Renderer** | 2D-Top-Down prozedural, Code-only-Mandat | 3D-Top-Down mit URP, Lighting, Shadows, VFX Graph |
| **Spieler-Fokus** | Solo + Async-Liga | Solo + Async + **Real-time PvP 2-4** + **Co-op 2-4** |
| **Visuals** | Eigene Neon-Icon-Engine (152 Icons) | 3D-Modelle (Blender), VFX Graph, Shader Graph |
| **Audio** | Kenney CC0 + Android SoundPool | Kuratierte Library + AI-Voice (ElevenLabs) + FMOD/Wwise |
| **Plattform** | Android only | Android + iOS + Steam (Win/macOS/Linux) |
| **Netcode** | Firebase REST (async-only) | Photon Fusion (Real-time PvP) + Photon Realtime (Co-op) + Firebase (Meta) |
| **Persistenz** | sqlite-net-pcl + Cloud-Save v3 | Firebase RTDB als Source-of-Truth + JSON-Fallback |
| **Tooling** | dotnet build + AppChecker | Unity Cloud Build / GitHub Actions + Addressables |
| **Tests** | xUnit (643 Tests) | Unity Test Framework EditMode+PlayMode + Determinismus-Replay-Suite |
| **CI/CD** | GitHub Actions Android-AAB | GitHub Actions Android+iOS+Steam + Cloud Functions Deploy |
| **Team** | 1 Solo-Entwickler | 5+ Personen Full Studio (siehe ROADMAP) |
| **Setting** | Generisch (10 Welt-Themes ohne übergeordnete Story) | Cohärente Sci-Fi-Welt mit Bombenmeister-Krieg-Story-Arc |
| **Monetization** | 1,99 EUR Remove-Ads + Battle Pass 9,99 + Cosmetics | Hybrid-F2P: BP + Cosmetic-Shop + saisonale Hero-Direct-Kauf (keine Lootboxen) |
| **Voice** | Komplett stumm | Voll-Voice (AI-generiert) DE+EN |
| **Liga** | NPC-Backfill bei < 20 echten Spielern | Echte Real-time-PvP-Liga + Co-op-Liga + Daily-Race |
| **Anti-Cheat** | Firebase-Server-Rules + Hybridtimer | Server-Replay-Re-Sim via Cloud Functions + Photon Webhooks + ML-Pattern-Detection (Phase 3+) |

---

## 7. USPs (Unique Selling Points)

Diese 5 USPs sind unsere Marketing-Anker und Differenzierung gegenüber der Konkurrenz:

### 7.1 USP 1: "Echtes 4-Spieler-Bomberman in Real-time auf Mobile"

- **Konkurrenz:** Bomb Squad (PC/Mobile) und Super Bomberman R (Konsole). Niemand bietet *kompetitives* Real-time-PvP auf Mobile mit Rollback-Netcode.
- **Marketing-Hook:** Gameplay-Footage von 4-Spieler-FFA mit < 80 ms Latenz.

### 7.2 USP 2: "Mech-Bomber statt Mensch-Charaktere"

- **Konkurrenz:** Sieht alle aus wie Anime-Kinder-Charaktere (Brawl Stars, Bombsquad). Wir gehen ernster: Mech-Designs mit eigener Personality.
- **Marketing-Hook:** Concept-Art-Drops auf Twitter/Instagram, jeder Hero kriegt ein Reveal-Video.

### 7.3 USP 3: "Story-Modus mit Welt-Mythologie"

- **Konkurrenz:** Brawl Stars + Bombsquad haben *keinen* echten Story-Modus. Wir bauen 100 Level mit dem Bombenmeister-Krieg-Arc.
- **Marketing-Hook:** Cinematic-Trailer mit Direktor-Vex-Antagonist-Reveal.

### 7.4 USP 4: "Cross-Save Mobile↔PC mit einem Account"

- **Konkurrenz:** Praktisch keine Mobile-PvP-Spiele mit echtem Cross-Save. Brawl Stars hat nichts auf PC.
- **Marketing-Hook:** "Pendel-Spiel": Im Bus auf Mobile, abends am PC weiterspielen.

### 7.5 USP 5: "Co-op-Roguelike-Dungeon mit 4 Spielern"

- **Konkurrenz:** Vampire Survivors hat den Single-Player. Niemand hat einen 4-Spieler-Co-op-Bomberman-Roguelike.
- **Marketing-Hook:** "Bomberman trifft Slay-the-Spire trifft Vampire-Survivors."

---

## 8. High-Level-Roadmap

> Detaillierte Sprint-Planung in [ROADMAP.md](ROADMAP.md). Hier nur die Phasen-Übersicht.

| Phase | Zeitrahmen | Hauptziel |
|-------|-----------|-----------|
| **Phase 0** | Monat 1 | Setup: Unity-Skelett, CI, Firebase, Photon. Domain-Code-Port starten. |
| **Phase 1** | Monat 2-4 | Single-Player-Core: Grid, 3 Helden, 5 Bomben, 10 Welten, 100 Levels, HUD. |
| **Phase 2** | Monat 4-6 | Meta-Layer: Economy, Shop, Talent-Bäume, Card-System, BP, Cloud-Save. |
| **Phase 3** | Monat 6-8 | Async + Co-op-Multiplayer: Liga, Friends, Photon Realtime, Co-op-Dungeon. |
| **Phase 4** | Monat 8-10 | Real-time PvP: Photon Fusion, Matchmaking, Anti-Cheat, PvP-Liga. |
| **Phase 5** | Monat 9-11 | Polish: 3D-Art, VFX, AI-Voice DE/EN, Welt-Cutscenes, Cosmetics 40+. |
| **Phase 6** | Monat 10-12 | Closed Beta DACH (500 Tester), Stress-Test, LiveOps-Tooling. |
| **Phase 7** | Monat 12 | **Soft-Launch DACH** + Saison 1 ("Aufstand"). |
| **Phase 8** | Monat 13-14 | **EU-Launch** + Saison 2. iOS-Release. |
| **Phase 9** | Monat 15-16 | **Global-Launch** + Saison 3. Steam-Demo. |
| **Phase 10** | Monat 17-18+ | Steam-Full-Launch, Tournament-Modus, Voice-Chat-Rollout |

**Realistischer Launch im 12. Monat (Q1 2027), Global Q3 2027.**

---

## 9. Risiko-Summary

> Vollständiges Risiko-Register in [ROADMAP.md](ROADMAP.md#risiken). Top-5 hier:

| # | Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|--------|--------------------|--------|------------|
| 1 | Photon Fusion Determinismus auf Mobile schwierig | Mittel | Hoch | Frühe Prototypen, evtl. Lockstep statt Rollback als Fallback |
| 2 | 4-Spieler-Mobile-Performance (60 FPS High / 30 FPS Low) | Mittel | Hoch | LOD-System, VFX-Skalierung, dedizierte Performance-Tests pro Sprint |
| 3 | AI-Voice (ElevenLabs) Lizenz-Risiko + Sterilität | Mittel | Mittel | Backup-Plan: Mensch-Sprecher für Schlüssel-Lines (Heroes, Antagonist) |
| 4 | Photon-Kosten bei 50k+ MAU explodieren | Niedrig (anfangs) | Hoch (bei Erfolg) | Bei Skala evaluieren: Custom Nakama, Self-hosted-Server, Mirror |
| 5 | Anti-Cheat-Replay-Re-Sim auf Server-Worker zu langsam | Mittel | Hoch | Domain-Code muss isomorph (gleiche Logik Client+Server). C#-Worker mit .NET 10 |

---

## 10. Nächste konkrete Schritte

### Sofort (Woche 1-2)

1. **Stakeholder-Review** dieses Plans (heute) → Feedback einarbeiten
2. **Firebase-Projekt anlegen** `bomberblast-arena` (Auth + RTDB + Functions + Storage + Crashlytics)
3. **Photon-Account einrichten** + 3 AppIds: Dev/Stage/Prod für Fusion, Realtime, Chat
4. **Unity-Projekt anlegen** `src/Apps/BomberBlast.Unity/Unity/` (Unity 6 mit URP)
5. **GitHub Repo** (oder Monorepo-Branch) anlegen mit `.gitignore` für Unity
6. **Domain-Code-Port-Sprint planen**: DeterministicRandom + ComboSystem + ReplayCapture als erstes
7. **Team-Recruiting starten** (siehe [ROADMAP.md](ROADMAP.md#team) für Rollen-Specs)

### Mittelfrist (Monat 1)

8. **CI/CD-Pipeline** mit game-ci/unity-builder (Android-Build, EditMode-Tests pro PR)
9. **Concept-Art-Sprint** für 3 MVP-Helden (Nova, Cryo, Titan) + Cyber-Slum-Welt 1
10. **Erste Boot.unity-Scene** mit VContainer + Splash + Anonymous Auth
11. **Game-Design-Doc** finalisieren ([DESIGN.md](DESIGN.md) verfeinern mit Feedback)
12. **Tech-Architektur-Doc** finalisieren ([ARCHITECTURE.md](ARCHITECTURE.md) verfeinern)

### Langfrist (Monat 2-12)

Folge [ROADMAP.md](ROADMAP.md).

---

## Änderungslog

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-Version aus 3 Weichen-Antworten | Robert Schneider + Claude |
| 2026-05-26 | v0.2 | Komplettes Restruktur: PLAN als Master + DESIGN/ARCHITECTURE/ROADMAP/CLAUDE Sub-Files. 8 Weichen-Antworten vertieft (Helden, Welten, Setting, Team, Performance, Cross-Play, Voice, Launch-Region) | Robert Schneider + Claude |

> **Status:** Konzept-Phase. Bereit für Team-Recruiting + Setup-Sprint.
