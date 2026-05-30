# BomberBlast 3D — Roadmap, Team & Produktion

> Production-Plan, Team-Struktur, Marketing/Launch, Compliance und Risiko-Register.
> Komplementär zu [PLAN.md](PLAN.md), [DESIGN.md](DESIGN.md) und [ARCHITECTURE.md](ARCHITECTURE.md).
> Stand 2026-05-26.

---

## Inhaltsverzeichnis

1. [Team](#1-team)
2. [Budget-Indikation](#2-budget-indikation)
3. [18-Monats-Roadmap (Detail)](#3-18-monats-roadmap-detail)
4. [Sprint-Struktur](#4-sprint-struktur)
5. [Marketing & Launch-Plan](#5-marketing--launch-plan)
6. [Community-Building](#6-community-building)
7. [Compliance (DSGVO, COPPA, PEGI, Lootbox, AI-Voice)](#7-compliance-dsgvo-coppa-pegi-lootbox-ai-voice)
8. [Anti-Cheat & Trust & Safety](#8-anti-cheat--trust--safety)
9. [Live-Service-Operations (LiveOps)](#9-live-service-operations-liveops)
10. [Hot-Patch + Krisen-Reaktion](#10-hot-patch--krisen-reaktion)
11. [Esports-Foundation (Phase 2+)](#11-esports-foundation-phase-2)
12. [Risiken (vollständiges Register)](#12-risiken-vollständiges-register)
13. [Offene Entscheidungen](#13-offene-entscheidungen)
14. [Erfolgs-Checkliste (Pre-Launch)](#14-erfolgs-checkliste-pre-launch)
15. [Post-Launch-Roadmap (Saison 1-6)](#15-post-launch-roadmap-saison-1-6)

---

## 1. Team

### 1.1 Rollen-Spec (5+ Personen, Vollzeit-Äquivalent)

#### 1.1.1 Game Director / Producer (1 FTE)

- Vision-Owner, Eskalations-Instanz für Game-Design-Entscheidungen
- Sprint-Planung, Roadmap-Pflege, Stakeholder-Communication
- Saison-Story-Treatment-Lead
- Daten-Auswertung (KPIs, Cohort-Analysis)
- Skills: Mobile-F2P-Erfahrung, Live-Service-Bias, Erfahrung mit Photon/Multiplayer-Mobile-Games

#### 1.1.2 Lead Developer (1 FTE) + Junior Developer (1 FTE)

**Lead Developer:**
- Architektur-Owner (Asmdefs, DI, Netcode)
- Photon Fusion + Realtime + Anti-Cheat-Pipeline
- Cloud Functions + Domain-Code-Port-Lead
- Code-Review-Pflicht für Junior + Asset-Pipeline-Integration
- Skills: 5+ Jahre Unity, Photon-Erfahrung, C#-Senior, Server-Side-TypeScript

**Junior Developer:**
- Feature-Implementation (Helden-Skills, Card-System, UI-Verkabelung)
- Test-Coverage-Aufbau (Domain + PlayMode)
- Tooling (Editor-Tools, Daten-Importer)
- Skills: 1-2 Jahre Unity, C#, Lust auf Multiplayer-Komplexität

#### 1.1.3 Lead Artist / 3D-Lead (1 FTE)

- Concept-Art-Lead (Mech-Designs, Welt-Themes)
- 3D-Modellierungs-Pipeline (Blender → Unity)
- Animation-Lead (Helden-Animator, Mech-Movements)
- VFX Graph + Shader Graph-Visuals
- Skills: Mobile-Game-Erfahrung, stilisierter Arcade-/Neon-Look (3D), Blender + Substance Painter

#### 1.1.4 UI/UX Designer (0.5-1 FTE, Teil-Zeit ok)

- Figma-Mockups für alle Screens
- USS-Stylesheets für UI Toolkit
- HUD-Layout + Interaction-Design
- Onboarding-Flow + Tutorial-Wireframes
- Skills: Mobile-First, F2P-UX-Erfahrung, Game-UX-Patterns

#### 1.1.5 Sound Designer / Audio Lead (0.5 FTE, Freelance)

- Audio-Mastering-Pipeline (LUFS, Mixer-Snapshots)
- Music-Komposition (10 Welt-Themes, Hub, Combat)
- SFX-Design (22 Bomb-Sounds, 12 PowerUp-Sounds, Hero-VoiceLines-Curation)
- FMOD-Studio-Integration (optional)
- AI-Voice-Pipeline-Management (ElevenLabs)
- Skills: Mobile-Audio-Erfahrung, FMOD/Wwise, Adaptive Music

#### 1.1.6 Community Manager / Marketing Lead (0.5 FTE, später Vollzeit)

- Discord-Community-Aufbau (Beta-Phase)
- Twitter/Reddit/TikTok-Präsenz
- Influencer-Outreach
- Press-Kits, App-Store-Optimization (ASO)
- Krisen-Communication bei LiveOps-Issues
- Skills: Mobile-Gaming-Community-Erfahrung, deutscher Markt für Soft-Launch

### 1.2 Optional / Freelance

- *(optional, nur falls Voice eingeführt wird — im Original deferred)* **Voice-Director**: 1-2 Wochen, ~5-10k
- **Story-Writer** (für Welt-Mythologie, Memory-Fragmente, Dialoge): 2-4 Wochen, ~10-15k
- **Localization-Service**: pro Sprache ~2-3k (DE/EN nativ, ES/FR/IT/PT freelance)
- **QA-Tester** (für Closed Beta): 2-3 Wochen, ~5-8k oder via Discord-Community
- **Anti-Cheat-Security-Consultant** (Phase 4 PvP-Launch): 1-2 Wochen, ~5-10k
- **Steam-Publishing-Consultant** (Phase 9): 1 Woche, ~3-5k

### 1.3 Team-Onboarding-Plan (erste 2 Wochen)

- **Tag 1-2**: Repo-Setup, Tools-Installation (Unity 6, IDE, Slack/Discord), Code-of-Conduct
- **Tag 3-5**: Codebase-Tour (Architektur-Walkthroughs, Asmdef-Erklärung)
- **Tag 6-10**: Erste kleine Issues (Bug-Fix oder Junior-Feature)
- **Tag 11-14**: Erster PR durch CI-Gate, ggf. Code-Review
- **Sprint-1-Onboarding**: 2 Wochen Sprint mit Domain-Code-Port als Hauptaufgabe

### 1.4 Communication-Stack

| Tool | Zweck |
|------|-------|
| **Slack** | Team-Chat, Channels pro Disziplin |
| **Discord** | Community + Beta-Tester |
| **Linear** oder **GitHub Projects** | Sprint-Planung, Issue-Tracking |
| **GitHub** | Code, PRs, Code-Review |
| **Figma** | UI-Mockups, Asset-Library |
| **Notion** oder **Confluence** | Living-Docs, Design-Decisions |
| **Daily-Standup** | 15 Min, async via Slack-Bot bei verteilten Teams |
| **Weekly-Demo** | Freitags, 1 h, alle zeigen Progress |
| **Sprint-Retro** | Alle 2 Wochen, 1 h, Team-Improvement |

---

## 2. Budget-Indikation

> Schätzung Vollzeit-Studio (5+ Personen) für 12 Monate bis Soft-Launch + 6 Monate Live-Service.

### 2.1 Personal-Kosten (12 Monate Pre-Launch)

| Rolle | FTE | Monatsgehalt (DE Brutto) | Total 12 Monate |
|-------|-----|-------------------------|-----------------|
| Game Director / Producer | 1.0 | 6.500 EUR | 78.000 |
| Lead Developer | 1.0 | 8.000 EUR | 96.000 |
| Junior Developer | 1.0 | 4.500 EUR | 54.000 |
| Lead Artist / 3D-Lead | 1.0 | 5.500 EUR | 66.000 |
| UI/UX Designer | 0.75 | 5.000 EUR | 45.000 |
| Sound Designer (Freelance) | 0.5 | 4.500 EUR | 27.000 |
| Community Manager (steigert) | 0.25→1.0 | 4.000 EUR | 24.000 |
| **Total Personal-Kosten** | | | **390.000 EUR** |

### 2.2 Tool/Software-Kosten (12 Monate)

| Tool | Kosten 12 Mo |
|------|-------------|
| Unity Pro (5 Seats × 1.800 EUR/Yr) | 9.000 |
| GitHub Team (5 Users × 4 EUR/Mo) | 240 |
| Figma Professional (5 Seats × 12 EUR/Mo) | 720 |
| Slack Business+ (8 Seats × 12 EUR/Mo) | 1.150 |
| JetBrains Rider/IntelliJ (5 Seats × 200 EUR/Yr) | 1.000 |
| Blender (kostenlos) | 0 |
| Substance Painter (5 Seats × 240 EUR/Yr) | 1.200 |
| FMOD Studio Indie (1× 5.000 EUR/Yr) | 5.000 |
| ElevenLabs Enterprise | 6.000 |
| DOTween Pro (Asset Store, 5× 15 EUR) | 75 |
| Photon Fusion 2 (Pricing siehe unten) | 0-3.000 |
| Linear / GitHub Projects | 600 |
| Notion Team | 720 |
| ComfyUI/Hunyuan3D (siehe ASSETS_AI.md, mostly free) | 200 (GPU-Cloud-Backup) |
| **Total Tools** | **28.905 EUR** |

### 2.3 Infrastruktur-Kosten (12 Monate Pre-Launch, niedrige Nutzung)

| Service | 12 Mo |
|---------|-------|
| Firebase (Auth + RTDB + Functions + Storage + Crashlytics) | 1.500 (Beta-Phase) |
| Photon Fusion/Realtime/Chat (CCU-basiert) | 0 (Free-Tier reicht) |
| GitHub Actions (Build-Minuten) | 600 |
| Cloud Run (Server-Worker) | 600 |
| Domain + Webspace (Marketing-Site) | 200 |
| Apple Developer Program | 99 |
| Google Play Developer Account (einmalig) | 25 |
| **Total Infra (Pre-Launch)** | **3.024 EUR** |

### 2.4 Marketing-Kosten (Launch-Window 6 Mo)

| Channel | Kosten |
|---------|--------|
| Influencer-Outreach (Twitch/YouTube DACH) | 30.000 |
| ASO-Optimization | 5.000 |
| Press-Kit + PR-Agent | 10.000 |
| Discord-Community-Tools (Premium-Bots) | 600 |
| Google/Meta Ads (Soft-Launch DACH) | 50.000 |
| **Marketing Pre-Launch** | **95.600 EUR** |

### 2.5 Asset-Produktions-Kosten (Außerhalb Personal)

| Bereich | Kosten |
|---------|--------|
| *(optional/deferred)* Voice-Acting (nur falls eingeführt — Original ist voice-los) | 0 (deferred) / ~15.000 falls dazu |
| Music-Komposition (Optional Custom-Composer für Hauptthemes) | 20.000 |
| 3D-Asset-Store-Kostümierung (z.B. Background-Props) | 5.000 |
| Localization-Service (6 Sprachen Initial) | 18.000 |
| QA-Studio (für Closed Beta, optional) | 10.000 |
| **Total Asset-Produktion** | **68.000 EUR** |

### 2.6 Gesamt-Budget (12 Monate Pre-Launch)

| Kategorie | Kosten |
|-----------|--------|
| Personal | 390.000 |
| Tools | 28.905 |
| Infrastruktur | 3.024 |
| Marketing | 95.600 |
| Asset-Produktion | 68.000 |
| **Subtotal** | **585.529 EUR** |
| Buffer (10 %) | 58.553 |
| **Total** | **~645.000 EUR** |

### 2.7 Live-Service-Phase (6 Monate Post-Launch)

| Kategorie | Kosten |
|-----------|--------|
| Personal (gleiche Team-Größe + Community-Manager Vollzeit) | 250.000 |
| Infrastruktur (Skala: 100k MAU bei Saison 3) | 25.000 |
| Marketing (laufende Akquise) | 80.000 |
| Hero/Saison-Content-Produktion | 60.000 |
| **Total Live-Service 6 Mo** | **415.000 EUR** |

### 2.8 Photon-Kosten-Skala (Wichtige Annahme)

Photon Fusion Pricing (Stand 2026): ~0,001 EUR pro CCU pro Match-Minute.

| Spieler-Skala | CCU-Schätzung | Photon-Kosten/Monat |
|---------------|---------------|---------------------|
| 10k MAU | 200 CCU | ~30 EUR |
| 100k MAU | 2.000 CCU | ~300 EUR |
| 500k MAU | 10.000 CCU | ~1.500 EUR |
| 1M MAU | 20.000 CCU | ~3.000 EUR |
| 5M MAU | 100.000 CCU | ~15.000 EUR |

**Bei massivem Erfolg (>500k MAU): Evaluiere Custom-Nakama oder Self-Hosted-Mirror-Server** als Cost-Cut.

---

## 3. 18-Monats-Roadmap (Detail)

> **Annahme:** Studio mit 5+ Personen, Vollzeit. Bei kleinerem Team Zeit-Faktor 1.5-2× anwenden.

### Phase 0 — Setup & Foundation (Monat 1)

**Hauptziel:** Codebase steht, CI/CD funktioniert, Firebase + Photon verkabelt.

**Deliverables:**
- [ ] Unity-Projekt-Skelett angelegt (`src/Apps/BomberBlast.Unity/Unity/`)
- [ ] 7 Asmdefs angelegt (Core, Domain, Game, Multiplayer, UI, LiveOps, Bootstrap)
- [ ] VContainer + UniTask + R3 + Photon Fusion + Firebase Unity SDK integriert
- [ ] Boot-Scene + MainMenu-Skelett mit Splash + Anonymous-Auth
- [ ] CI/CD-Pipeline (game-ci/unity-builder für Android-AAB, EditMode-Tests pro PR)
- [ ] Firebase-Projekt `bomberblast-arena` (Auth + RTDB + Functions + Storage + Crashlytics)
- [ ] Photon-AppIds: Dev / Stage / Prod für Fusion + Realtime + Chat
- [ ] Domain-Code-Port: `DeterministicRandom`, `ComboSystem`, `ReplayCapture`, `FixedTimestepRunner`
- [ ] **Concept-Art-Sprint**: 3 MVP-Helden (Default, SpeedySam, BrickBoris) + Welt-1-Theme (im bestehenden Neon-Arcade-Stil, jetzt 3D)
- [ ] **Parity-Matrix** anlegen (jedes Original-System → Unity-Äquivalent + Port-Status)

**Team-Fokus:**
- Lead Dev + Junior Dev: Unity-Setup, Asmdefs, DI, Pure-Domain-Code-Port
- Lead Artist: Concept-Art-Sprint
- Game Director: Parity-Matrix + Sprint-Planning für Phase 1 (Game-Design steht = Original)
- Sound: Reference-Library aus dem bestehenden Audio-Set zusammenstellen

### Phase 1 — Single-Player-Core (Monat 2-4)

**Hauptziel:** Story-Mode mit 100 Levels in 10 Welten durchspielbar (Placeholder-Visuals OK).

**Deliverables:**
- [ ] Grid-System (15×10) mit GameGrid, CellType, GridUtils
- [ ] 3 MVP-Helden (Default, SpeedySam, BrickBoris) mit echten Start-Stats + HeroTrait (keine Skills/Talente)
- [ ] Hero-Switching im Hub
- [ ] MVP-Bomben (Standard + Shop-Bomben Ice/Fire/Sticky)
- [ ] 12 PowerUps + Cure mit echten Effekten + Unlock-Levels
- [ ] 12 Enemy-Typen (8 klassisch + Tanker/Ghost/Splitter/Mimic) + Elite-Flag + A*-Pathfinding (portiert)
- [ ] 5 Bosse (StoneGolem/IceDragon/FireDemon/ShadowMaster/FinalBoss) + Phase/Enrage (1-2 davon polished)
- [ ] 10 Welten × 10 Level = 100 Levels (mit Placeholder-Visuals)
- [ ] LevelLayoutGenerator (portiert) mit 12 Layouts + 5 Mutatoren
- [ ] Combo-System (portiert)
- [ ] HUD (Joystick, Bomb-Button, Detonator, Card-Quickswap, Combo-Anzeige, Lives, Coins)
- [ ] Vibration + Audio-Hooks
- [ ] Pause-Menü + Settings-Stub

**Team-Fokus:**
- Lead Dev: Battle-Controller, Mode-Plugin-Foundation
- Junior Dev: Enemy-AI, Boss-Phase-Logic, HUD-Verkabelung
- Lead Artist: 3 Helden-3D-Modelle + Welt-1-Art-Assets (Neon-Arcade in 3D)
- UI/UX: HUD-Mockup + Hub-Mockup
- Sound: Welt-1 + 2 Music-Loops + SFX (aus bestehendem Set, Placeholder OK)

**Milestone-Gate:**
- 1-Stunden-Playtest mit Story-Mode L1-L20 durchspielbar
- Frame-Rate stabil 30+ FPS auf Mid-Tier-Test-Device

### Phase 2 — Meta-Layer + Persistenz (Monat 4-6)

**Hauptziel:** Vollständige Meta-Loop. Spieler kann grinden, leveln, BP claimen.

**Deliverables:**
- [ ] Coin/Gem/DungeonCoin-Economy (Overflow-Guard, portiert)
- [ ] Shop mit 9 permanenten Upgrades (UpgradeType — Haupt-Coin-Sink) + 3 Shop-Bomben
- [ ] Card-System: Deck (4 Basis + 1 freischaltbar), Quickswap, Crafting (Coin-Sink)
- [ ] Deck-Builder UI
- [ ] Battle Pass v1 (30 Tiers, Free/Premium, 10 Themes)
- [ ] Daily Reward (7-Tage-Zyklus + Comeback, portiert)
- [ ] Daily Missions (3/Tag) + Weekly Missions (5/Woche)
- [ ] 72 Achievements (5 Kategorien, portiert) — Achievement-IDs für Hero-Unlocks erhalten
- [ ] Cloud-Save via Firebase RTDB (`bomberblast-arena`) + **Legacy-Save-Import** aus `bomberblast-league`
- [ ] Account-Login: Anonymous + Email + Google SignIn
- [ ] Settings-Screen + Accessibility-Optionen (Colorblind/HighContrast/UiScale/Subtitles)
- [ ] Localization-Foundation (DE + EN voll, ES/FR/IT/PT Stub) — RESX-Migration aus Original
- [ ] Tutorial T1-T3 voll spielbar + Feature-Unlock-Choreographie
- [ ] Cloud Functions: `accountDelete`, `dataExport`, `migrateSchema`, `importLegacySave`

**Team-Fokus:**
- Lead Dev: BP-Logik, Cloud-Save + Legacy-Import, IAP-Verkabelung
- Junior Dev: Achievement-System, Missions, Shop/Deck-UI
- Lead Artist: Restliche 2 Helden-Modelle (TwinTina, LuckyLola) + Welt 2-3
- UI/UX: Vollständige Hub-UI + Shop + Deck-Builder Mockups
- Sound: Welt 3-5 Music (aus bestehendem Set, aufgewertet)

**Milestone-Gate:**
- 5-Stunden-Playthrough von Tutorial bis Welt-5
- BP-Loop voll, Spieler bekommt Belohnungen
- Cloud-Save funktioniert plattformübergreifend (Test mit Android + Editor)

### Phase 3 — Async + Co-op-Multiplayer (Monat 6-8)

**Hauptziel:** Live-Service + Roguelike-Dungeon (Solo-Kern) komplett; Clan + optionaler Co-op verkabelt.

**Deliverables (Solo-Kern):**
- [ ] Roguelike-Dungeon (16 Buffs, 5 Synergien, 5 Raum-Typen, 8 Floor-Modifier, Node-Map 10×3, 8 Dungeon-Upgrades)
- [ ] Master-Mode (Reborn nach L100) + Boss-Rush + Survival + Quick-Play
- [ ] Liga-System (5 Tiers × 3 Subs, 14-Tage-Saison, **Perzentil-Promotion**) — Domain-Port
- [ ] Liga-NPC-Backfill + Daily-Race-Modus (deterministisches Tageslevel)
- [ ] Live-Events: 8 Wochen-Events (ISO-Seed), saisonale Events, Lucky-Spin (9 Segmente, Pity), Rotating-Deals
- [ ] Profanity-Filter (portiert/extrahiert), Report-Funktion
- [ ] Clan-System (`FirebaseClanService` voll integriert: Create/Join/Chat/Leaderboard — portiert)
- [ ] Cloud Functions: `leagueReset` (14T), `battlePassReset` (30T), `validateDailyRace`, `reportPlayer`

**Deliverables (optionaler Multiplayer — Plus):**
- [ ] Friend-System (Friend-Codes + Search)
- [ ] Photon Realtime Co-op-Lobby + Co-op Story/Dungeon (2-4 Spieler)
- [ ] Chat-System (Photon Chat) + Block/Report

**Team-Fokus:**
- Lead Dev: Dungeon/Liga/Events-Port, Cloud Functions, Clan-Integration
- Junior Dev: Liga-UI, Dungeon-UI, Events-UI
- Lead Artist: Welt 5-7 + Dungeon-Visuals
- UI/UX: Liga-Bildschirm, Dungeon-Node-Map, Event-Banner
- Sound: Welt 6-8 Music + Stinger-Library

**Milestone-Gate:**
- Dungeon-Run komplett spielbar (Floor 1-10 + Buffs/Synergien)
- Liga-Saison-Reset via Cloud Function getestet, erste Daily-Race-Submissions
- Live-Event-Rotation deterministisch korrekt

### Phase 4 — Determinismus-Integration + Multiplayer-Foundation (Monat 8-9, optional/Plus)

**Hauptziel:** Determinismus integriert (Voraussetzung für Replay/Online-MP); Local-Coop/Versus spielbar.
**Online-Versus ist optionales Post-Launch-Feature — kein Launch-Blocker (siehe PLAN §9 Risiko #2/#5).**

**Deliverables:**
- [ ] **Determinismus-Integration:** alle Gameplay-Random über `IRngProvider`, Sim/Render-Trennung, 60-Hz-Fixed-Step
- [ ] Float-Determinismus-Strategie (Fixed-Point/Quantisierung vor State-Hash) — siehe ARCHITECTURE §13.0
- [ ] Determinismus-Replay-Suite (CI-Gate)
- [ ] Local-Coop/Versus (2 Spieler an einem Gerät/Gamepads, dual Input-Routing)
- [ ] *(später/optional)* Online-Co-op (Photon Realtime), Online-Versus (Photon Fusion) + Server-Re-Sim-Anti-Cheat

**Team-Fokus:**
- Lead Dev: Determinismus-Integration, Local-MP, (optional) Photon
- Junior Dev: Replay-Suite, Local-Coop-Camera/Input
- Lead Artist: Welt 8-10 + FinalBoss
- UI/UX: Local-MP-Setup + Settlement-Modal
- Sound: Welt 9-10 Music + Boss-Reveal-Cinematic-Audio

**Milestone-Gate:**
- Determinismus-Replay-Suite grün (Single-Player)
- Local-2-Spieler-Match spielbar
- (optional) Online-Co-op-Match Editor + Build

### Phase 5 — Polish + Visuals + Audio (Monat 9-11)

**Hauptziel:** Production-Quality-Look + Audio.

**Deliverables:**
- [ ] Alle 5 Helden 3D-Modelle finalisiert + Animationen (Neon-Arcade in 3D)
- [ ] Alle 10 Welten Art-Assets finalisiert (beide Visual-Styles: Classic HD + Neon)
- [ ] VFX Graph für 14 Bomben-Typen
- [ ] VFX Graph für Boss-Angriffe + Combo/ULTRA-Effekte
- [ ] Adaptive Music (FMOD-Integration optional), aufgewertete Loops aus bestehendem Set
- [ ] *(deferred/optional)* Voice — im Original bewusst abgewählt; nur falls Budget-Entscheidung dafür
- [ ] Welt-Story-Cutscenes in 3D (Timeline + Cinemachine) — die bestehenden 10 Intros + 9 Outros
- [ ] Cosmetics-Pipeline aktiviert: 98 Items (32 Trails / 33 Frames / 33 Victories) + Skins
- [ ] UI-Polish: Animationen, Modal-Transitions, Iris-Wipe
- [ ] Localization-Vervollständigung (alle 6 Sprachen voll, RESX-Migration aus Original)
- [ ] Performance-Optimization auf Min-Spec-Device (Galaxy A50 etc.) + Hardware-Tier-System
- [ ] Memory-Profiling + Crash-Reduction

**Team-Fokus:**
- Lead Artist: Final-Polish-Pass für alle Welt-Assets
- Junior Dev: Performance-Optimization, Memory-Profiling
- Sound: Final-Mastering (LUFS-Normalisierung)
- UI/UX: UI-Polish-Pass
- Lead Dev: Stability-Bugfixes, Anti-Cheat-Verfeinerung

**Milestone-Gate:**
- 60 FPS stabil auf Mid-Tier-Device über 30 Min Match
- Crash-Free-Rate intern > 99 %
- All-Hands-Internal-Playtest 2 h, Bug-Liste < 50 P0/P1

### Phase 6 — Closed Beta (Monat 10-11)

**Hauptziel:** 100-500 Tester live, Stress-Tests durchgeführt, LiveOps-Tooling steht.

**Deliverables:**
- [ ] Closed Beta DACH (100-500 Tester, Discord-Recruit + Reddit)
- [ ] NDA-Pflicht für Closed-Tester
- [ ] Bug-Tracking-System (GitHub Issues + Discord-Channel)
- [ ] Telemetry-Funnel aktiv (Firebase Analytics)
- [ ] A/B-Test-Framework via Remote Config aktiviert
- [ ] Crashlytics auf < 0.5 % Crash-Rate
- [ ] LiveOps-Tools: Event-Configurator, Remote-Config-Tweaks-UI
- [ ] Clan-System (Phase 1, Foundation aus altem System portiert + Cloud-Functions)
- [ ] Saison-1-Content erstellt (Saison "Aufstand")
- [ ] Marketing-Site live (bomberblast-arena.com)
- [ ] App-Store-Listings (Google Play + App Store) vorbereitet

**Team-Fokus:**
- Game Director: Bug-Triage, Feedback-Auswertung
- Lead Dev: Stability-Hotfix-Pipeline
- Lead Artist + Sound: Saison-1-Content-Production
- Community Manager (jetzt Vollzeit): Discord-Aufbau, Beta-Tester-Communication
- Marketing: Press-Kit, ASO

**Milestone-Gate:**
- 200+ active Closed-Beta-Tester
- D7-Retention bei Closed-Beta ≥ 20 % (gutes Signal)
- Pre-Launch-Marketing live (Twitter/Reddit/TikTok-Buzz)

### Phase 7 — Soft-Launch DACH (Monat 12)

**Hauptziel:** Public DACH-Launch + Saison 1 "Aufstand".

**Deliverables:**
- [ ] Open Beta DACH (Play-Store-Beta-Track) → später Vollrelease
- [ ] Saison 1 "Aufstand" live
- [ ] Marketing-Push DACH (Influencer + Google/Meta Ads)
- [ ] 24/7-Monitoring (Liga-Health, Crashes, Server-Load) via Dashboard
- [ ] Hotfix-Pipeline: Same-Day-Patches via Remote Config + Addressables
- [ ] Customer-Support-Tool für Player-Reports + Bans
- [ ] DSGVO-Compliance-Audit final (siehe Compliance-Sektion)

**KPIs Soft-Launch DACH (Wochen 1-8):**
- Downloads: 50k
- D1-Retention: ≥ 35 %
- D7-Retention: ≥ 15 %
- D30-Retention: ≥ 8 %
- ARPDAU: 0.30 EUR
- Crash-Free-Rate: ≥ 99 %

### Phase 8 — EU-Launch + iOS (Monat 13-14)

**Hauptziel:** EU-weit verfügbar, iOS-Build released, Saison 2.

**Deliverables:**
- [ ] EU-Release (alle EU-Länder)
- [ ] iOS-Submission + Approval
- [ ] Saison 2 live (neues BP-Theme aus Rotation, neue Cosmetics + Event)
- [ ] Cosmetics-Schub für S2 (neue Trails/Frames/Victories)
- [ ] Cross-Save voll funktional Mobile↔PC (für Steam-Vorbereitung)
- [ ] *(optional)* Friend-System voll, Cross-Play-Restrictions-Konfiguration

**KPIs (Wochen 9-16):**
- Downloads: 500k
- D1-Retention: ≥ 30 %
- ARPDAU: 0.35 EUR

### Phase 9 — Global-Launch + Steam-Demo (Monat 15-16)

**Hauptziel:** Globaler Launch, Steam-Demo live.

**Deliverables:**
- [ ] Global-Release (alle Märkte außer geo-restricted)
- [ ] Steam-Demo (Free-Demo mit 3 Welten + Dungeon-Probe)
- [ ] Saison 3 live (neues BP-Theme, neue Cosmetics + Event)
- [ ] Influencer-Push global

**KPIs (Wochen 17-26):**
- Downloads: 3-5M
- D7-Retention: ≥ 10 %
- ARPDAU: 0.40 EUR
- Crash-Free-Rate: ≥ 99.5 %

### Phase 10 — Steam-Full-Launch + Live-Service (Monat 17-18+)

**Hauptziel:** Steam-Full-Launch, Live-Service-Pipeline läuft autonom.

**Deliverables:**
- [ ] Steam-Vollrelease (Windows + Linux + macOS)
- [ ] Saison 4 live (neues BP-Theme, neue Cosmetics + Event)
- [ ] *(optional)* Online-Co-op global verfügbar
- [ ] *(optional, sehr ambitioniert)* Online-Versus-Erweiterung (setzt Float-Determinismus-Lösung voraus)
- [ ] LiveOps autonom mit Saison-Schedule (Liga 14T / BP 30T)

---

## 4. Sprint-Struktur

### 4.1 Sprint-Dauer & Rhythmus

- **Sprint-Länge:** 2 Wochen
- **Sprint-Plan:** Montag-Morgen (1 h)
- **Daily-Stand-up:** 15 Min, Live oder Async via Slack
- **Sprint-Demo:** Freitag-Nachmittag (1 h)
- **Sprint-Retro:** Alle 2 Wochen (Freitag, 30 Min)
- **Backlog-Refinement:** Mittwoch (30 Min, Game Director + Leads)

### 4.2 Sprint-Größe

| Rolle | Sprint-Capacity (Story-Points pro Sprint) |
|-------|-------------------------------------------|
| Lead Developer | 13-21 SP (komplexe Architektur + Code-Review-Zeit) |
| Junior Developer | 8-13 SP |
| Lead Artist | 8-13 SP (Asset-Production-Iterationen) |
| UI/UX Designer | 5-8 SP (Mockups + USS-Stylesheets) |
| Sound Designer (FT) | 3-5 SP |

**Story-Point-Skala:** 1, 2, 3, 5, 8, 13, 21 (Fibonacci). > 13 = sollte gesplittet werden.

### 4.3 Definition-of-Done

- [ ] Code implementiert nach Conventions (siehe CLAUDE.md)
- [ ] Unit-Tests für Domain-Code (mind. 1 Happy-Path + 1 Edge-Case)
- [ ] PlayMode-Test für Game-Feature (wenn passend)
- [ ] CI-Pipeline grün (Build + EditMode-Tests + Determinism-Suite)
- [ ] Code-Review approved (Lead Dev oder Peer)
- [ ] Manuelle QA durchgespielt (Acceptance-Criteria erfüllt)
- [ ] PR-Doku aktualisiert (Commit-Message + ggf. CLAUDE.md/DESIGN.md)
- [ ] Performance-Check auf Min-Spec-Device (wenn relevant)

---

## 5. Marketing & Launch-Plan

### 5.1 Soft-Launch DACH (Monat 12, Phase 7)

**Zielgruppe:** Deutschsprachiger Markt (DE/AT/CH), ~120 Mio Einwohner.

**Channels:**

| Channel | Budget | Erwarteter Effekt |
|---------|--------|-------------------|
| **Google/Meta Ads (DACH-only)** | 30.000 EUR | 30-50k Downloads in 8 Wochen |
| **Twitch-Influencer (DACH-Mobile-Gaming)** | 15.000 EUR | Reach ~200k Viewers |
| **Reddit DE-Subreddits (r/de, r/Spiele)** | 0 (organic) | 5-10k organic Downloads |
| **YouTube DACH-Gaming-Channels** | 10.000 EUR | Sponsored Reviews |
| **TikTok-Trend-Posts (DE)** | 5.000 EUR | Influencer-Snippets |
| **Press: golem.de, gamesindustry.biz** | 5.000 EUR (PR-Agent) | Brand-Awareness |
| **App-Store-Featuring (Google/Apple)** | 0 (Pitch-Deck) | Falls erfolgreich: +20k Downloads |
| **Discord-Community-Aktivierung** | 600 EUR | Loyal-User-Build |

**Total DACH-Launch-Marketing:** ~65k EUR

### 5.2 EU-Launch (Monat 13-14, Phase 8)

**Zielgruppe:** EU (alle Sprachen, Fokus DE+EN+ES+FR).

**Channels:**
- Google/Meta Ads (EU-broad) — 80.000 EUR
- Influencer (EU-Mobile-Gaming) — 30.000 EUR
- App-Store-Featuring EU-Märkte
- PR (UK + EU): gamesindustry, eurogamer, pcgamesn

### 5.3 Global-Launch (Monat 15-16, Phase 9)

**Zielgruppe:** US, LatAm, SEA, Korea.

**Channels:**
- Globale Ads (Meta + TikTok) — 200.000 EUR
- US-Influencer (englischer Markt) — 80.000 EUR
- App-Store-Featuring USA/Asia
- E3/Gamescom/GDC-Booth (falls Budget — optional)

### 5.4 Steam-Launch (Monat 17-18, Phase 10)

**Zielgruppe:** PC-Gaming-Community.

**Channels:**
- Steam-Wishlist-Kampagne (vorab via Demo)
- Steam-Featuring-Pitch
- Twitch-Streamer-Outreach (PC-Focused)
- Steam-Workshop-Hype (falls Community-Levels live)

### 5.5 ASO-Strategie (App-Store-Optimization)

**Keyword-Targets:**
- "bomberman" (high volume, kompetitiv)
- "bomberblast" (Brand)
- "mech bomber" (Niche, USP)
- "multiplayer arena" (Cross-Genre)
- "co-op roguelike" (Spezial-Audience)

**Screenshots:**
- 5 Screenshots pro Plattform: PvP-Action, Co-op-Dungeon, Hero-Showcase, Welt-Variety, Cosmetics
- A/B-Test 3 Varianten via Google Play Console

**App-Description:**
- DE: 4000 Zeichen, alle USPs hervorgehoben
- EN: identisch, optimiert für englische Keywords
- ES/FR/IT/PT: lokalisiert mit kulturellen Anpassungen

### 5.6 Trailer-Strategie

- **Cinematic-Trailer** (1:30 min) für Launch-Window — 3D-Before/After + FinalBoss-Reveal, gameplay-light
- **Gameplay-Trailer** (30 sec) für TikTok/Reels — schnelle Kills, Combo-Highlights
- **Hero-Reveal-Trailer** pro Saison (45 sec) — Lore + Gameplay
- **PvP-Highlight-Trailer** (2 min) für YouTube — best moments aus Closed Beta
- **Tutorial-Trailer** (60 sec) — für Onboarding-Confused-Users

---

## 6. Community-Building

### 6.1 Discord-Server-Struktur

```
BomberBlast (Discord-Server)
├── 📢 ANNOUNCEMENTS
├── 📜 RULES & FAQ
├── 💬 GENERAL
│   ├── #general-en
│   ├── #general-de
│   ├── #lore-and-story
│   └── #fan-art
├── 🎮 GAMEPLAY
│   ├── #pvp-strategies
│   ├── #coop-recruiting
│   ├── #clan-recruiting
│   ├── #esports-tournament
│   └── #tips-and-tricks
├── 🛠️ TECHNICAL
│   ├── #bug-reports
│   ├── #feature-requests
│   └── #performance-issues
├── 🌐 LANGUAGE-CHANNELS
│   ├── #de-deutsch
│   ├── #fr-français
│   ├── #es-español
│   └── ...
└── 🤖 BOTS & TOOLS
    ├── #help-bot (FAQ-Bot)
    └── #stats-bot (Player-Stats-Lookup)
```

### 6.2 Content-Calendar (Pre-Launch + Live)

**Pre-Launch (Monat 8-12):**
- Weekly Concept-Art-Drops (Twitter/Insta)
- Bi-weekly Dev-Logs (YouTube/Discord)
- Monthly Hero-Reveals (Trailer + Lore-Deep-Dive)
- Closed-Beta-Highlights-Reel

**Live (Monat 12+):**
- Daily Patch-Notes (kleine LiveOps-Updates)
- Weekly Community-Tournaments (Discord-organized)
- Bi-weekly Q&A mit Devs (Twitch-Stream)
- Monthly Saison-Reveal-Show
- Quarterly Roadmap-Update

### 6.3 Influencer-Outreach

**Tier 1 (Mobile-Mainstream):** 100k+ Subs, Mobile-Gaming-Fokus.
- Outreach: PR-Manager via Marketing-Agent
- Deals: Affiliate + bezahlte Sponsorships
- DACH: HandOfBlood, Trymacs, Knossi (falls Mobile-Focus)

**Tier 2 (Bomberman-Nische):** 5k-50k Subs, Spezial-Genre.
- Outreach: Community-Manager direkt
- Deals: Free-Game-Access + ggf. Small-Cash

**Tier 3 (Streamer-Discovery):** < 5k Subs, lokal/regional.
- Outreach: organic, Discord-Engagement
- Deals: Free-Game-Access, Founders-Pack-Code

---

## 7. Compliance (DSGVO, COPPA, PEGI, Lootbox, AI-Voice)

### 7.1 DSGVO (EU)

**Pflicht-Implementierungen:**

| Anforderung | Implementation |
|-------------|----------------|
| **Cookie-Consent (Web-Marketing-Site)** | Cookiebot oder OneTrust (~50 EUR/Mo) |
| **Privacy-Policy** | DE + EN (Anwalt: ~1.500 EUR) |
| **Data-Processing-Agreement (DPA)** mit Firebase, Photon, ElevenLabs | Standard-DPAs der Anbieter signieren |
| **Right-to-Access (Art. 15)** | Settings → "Daten exportieren" → Cloud Function `dataExport` |
| **Right-to-Deletion (Art. 17)** | Settings → "Account löschen" → Cloud Function `accountDelete` |
| **Right-to-Portability (Art. 20)** | Implizit via `dataExport` als JSON |
| **Consent-Manager** | In-App Settings → Analytics/Marketing/Voice-Chat opt-in |
| **Data-Minimization** | Nur notwendige Felder, keine PII ohne Zweck |
| **Pseudonymisierung** | UID statt Email in Logs |
| **Breach-Notification** (72h) | Incident-Response-Plan dokumentiert |

### 7.2 COPPA (US, Kinder < 13)

| Anforderung | Implementation |
|-------------|----------------|
| **Age-Gate** | Beim ersten App-Start: "Wie alt bist du?" (Settings: kein PII-Sammeln, eingeschränkte Features) |
| **Eltern-Zustimmung** | Bei < 13: Email an Eltern, Bestätigungslink |
| **Voice-Chat-Block** | < 13 kein Voice-Chat (auto-deaktiviert) |
| **Werbe-Restriktion** | < 13 kein behavioral Ads |
| **Personal-Data-Restriction** | < 13 keine Email-Sammlung |

### 7.3 PEGI / USK / ESRB-Rating

**Strategie:** Versuche **PEGI 12** / **USK 12** / **ESRB-Teen**.

**Kritische Inhalte:**
- Bombenexplosionen (kein Blut, stylisiert) → PEGI 7-OK
- Mech-Combat (Gewalt gegen Roboter, nicht Menschen) → PEGI 7-OK
- Stilisierte Boss-Kämpfe (Fantasy-Bosse wie IceDragon/FireDemon, kein Blut) → PEGI 7-12
- Story-Twist 3 (KI-Identitäts-Krise) → mild storytelling, PEGI 12

**Audit:** PEGI-Antrag (~600 EUR), USK-Beratung (Berlin-Office, ~1.000 EUR), ESRB-Self-Cert (kostenlos).

### 7.4 Lootbox-Regulierung

**Status (2026):**
- China: Verboten ohne Drop-Rate-Disclosure
- UK: PEGI-Empfehlung, US-Senate-Diskussion
- Belgien/NL: Lootboxen mit Real-Money verboten

**BomberBlast-Strategie:**
- **KEINE Lootboxen mit Real-Money-Käufen**
- LuckySpin (1× gratis/Tag) bleibt — transparente Drop-Rates + Pity-Counter
- Saison-Hero-Direkt-Kauf statt Gacha-System
- Battle-Pass = no-randomness (klare Rewards pro Tier)
- Karten-Drops aus Gameplay-Quellen (kein Pay-to-Roll)
- Cosmetic-Shop verkauft konkrete Items (keine Mystery-Boxes)

### 7.5 AI-Voice-Compliance (nur falls Voice eingeführt wird — DEFERRED)

> Das Original ist **bewusst voice-los** (Mandat "kein Geld"). Voice ist im Remake **deferred/optional**.
> Dieser Abschnitt gilt nur, falls Voice nachträglich als bewusste Erweiterung dazukommt — dann:
- Enterprise-Plan für kommerzielle Nutzung; Standard-Voices (kein Voice-Cloning ohne Consent)
- Disclosure in Credits: "Voice-Acting powered by <Anbieter> AI"
- Manual-QA-Pflicht: Mensch hört alle AI-Lines, Re-Render bei Issues
- Optional Mensch-Sprecher für Schlüssel-Lines (Announcer/Boss-Roar)

### 7.6 Datenschutz-Tracking-Strategie

| Tracker | Erlaubt mit Opt-In | Erlaubt ohne Opt-In |
|---------|---------------------|----------------------|
| Firebase Analytics | Ja | Anonym (kein UID) |
| Firebase Crashlytics | Ja (Default-on) | Ja (mit Anonymization) |
| Google AdMob | Nur mit Marketing-Opt-In | Non-Personalized-Ads |
| Photon (Match-Stats) | Ja | Ja (für Match-Funktion) |
| Voice-Chat-Recordings | Nur mit Voice-Opt-In | Nein |

---

## 8. Anti-Cheat & Trust & Safety

### 8.1 Anti-Cheat-Pipeline (siehe ARCHITECTURE.md Kap. 10)

**Operative Aspekte:**
- **Customer-Support-Tool** für Manual-Reviews (Web-Dashboard)
- **Ban-Wave-Strategie:** Wöchentliche Ban-Waves (statt Echtzeit) gegen "Cheater-Markt-Identifikation"
- **Appeal-Workflow:** Ban-Email → Appeal-Form → Review-Queue (max 5 Werktage)
- **Banned-Account-Database:** Permanent gespeichert (Hardware-ID, Account-ID), Re-Account-Erkennung

### 8.2 Player-Report-Pipeline

**Spieler-flow:**
1. Spieler meldet anderen Spieler via In-Game-Report-Button (Kategorie wählen: Cheating / Toxic / Spam / Other)
2. Cloud Function `reportPlayer` schreibt in `/reports/{reportedUid}/{reporterUid}`
3. Auto-Trigger: bei 5+ Reports in 24h → Auto-Review-Queue-Eintrag
4. Customer-Support-Mitarbeiter: Reviewt im Web-Dashboard
5. Action: Warning / 1-Tag-Mute / 7-Tag-Ban / Perma-Ban
6. Email an Reporter: "Danke für deine Meldung. Aktion wurde ergriffen."

### 8.3 Trust-Score

Pro Spieler interner Trust-Score (0-100):
- Start: 100
- Pro 5-Win-Streak: +1
- Pro Report: -10 (bei Verifikation)
- Pro Replay-Hash-Mismatch: -25
- < 50 Trust-Score: Match-Lockout (kann nicht in Ranked-Matchmaking)
- < 20 Trust-Score: Permanent-Ban-Empfehlung

### 8.4 Toxicity-Mitigation

- **Voice-Chat-Mute-Liste** persistiert pro Spieler
- **Auto-Mute bei 3+ Reports** in 24h
- **Profanity-Filter in allen Chats** (Globaler + Privater + Clan)
- **Emote-Throttling**: max 1 Emote/5s im Match (verhindert Spam-Flame)
- **Loss-Streak-Cool-Down**: Bei 3+ Losses in Folge: 5 Min Optional-Cooldown-Modal "Mach eine Pause?"

---

## 9. Live-Service-Operations (LiveOps)

### 9.1 Saison-Schedule

**Saison-Dauer:** 8 Wochen
**Saison-Cut:** Mittwoch 10:00 UTC

**Pre-Saison-Tasks (4 Wochen vorab):**
- Saison-Content-Lock (Karten, Helden, Cosmetics)
- Internal-QA-Run
- Marketing-Asset-Produktion (Trailer, Screenshots, Hero-Reveal)
- Closed-Beta-Test mit 50 Power-Usern

**Saison-Launch-Day-Tasks:**
- Deployment auf alle Plattformen (mit Stagger: DACH 8 h früher)
- Patch-Notes publishen (Web + In-App-Modal)
- Twitch-Reveal-Stream
- Influencer-Outreach (24h pre-launch)
- Monitoring (Crashes, Server-Load, Spieler-Feedback)

### 9.2 Live-Events-Calendar

| Event | Frequenz | LiveOps-Aufwand |
|-------|----------|-----------------|
| Wochen-Event | Wöchentlich | 2 h Vorbereitung (Modifier-Wahl, Reward-Tweak) |
| Weekend-Tournament | Bi-wöchentlich | 4 h Vorbereitung + 24 h Monitoring |
| Boss-Raid | Monatlich | 8 h Vorbereitung (Boss-Tweak, neue Mechanik) |
| Clan-War | Bi-wöchentlich | 2 h Vorbereitung |
| Saison-Story-Episode | Pro Saison | 40 h Voice + Dialogue-Polish |
| Limited-Time-Mode | Random (alle 2-3 Wo) | 8 h Vorbereitung |

### 9.3 LiveOps-Tools

**Remote-Config-UI** (Firebase-Console + Custom-Dashboard):
- Event-Toggle (Wochen-Event aktivieren/deaktivieren)
- Drop-Rate-Tweaking (mit Audit-Log)
- Preis-Anpassungen
- A/B-Test-Variant-Switching
- Notification-Template-Editing

**Player-Lookup-Tool** (Customer-Support-Web-Dashboard):
- UID-Search
- Account-Info-Anzeige (Save, Stats, Käufe)
- Ban-Status-Übersicht + Aktionen
- Refund-Initiierung (für legitime Beschwerden)
- Manual-Coin/Gem-Grant (z.B. nach Verbindungsabbruch)

### 9.4 Telemetrie-Auswertung

**Daily Dashboard:**
- DAU/WAU/MAU
- Retention-Cohorts (D1, D7, D30)
- Match-Counts pro Modus
- Crash-Rate
- ARPDAU + ARPPU
- Top-10 Helden + Top-10 Karten (Balancing-Hinweise)

**Weekly Review:**
- Funnel-Drop-Off-Analyse (Tutorial-Drop, Tier-Up-Conversion)
- Saison-Pass-Progress-Distribution (sind Spieler im Plan?)
- Liga-Tier-Distribution (zu schnell/langsam steigend?)
- Top-50-Player-Stats (Cheat-Verdacht?)

**Monthly Strategy Review:**
- KPI-Vergleich Soll/Ist
- Roadmap-Anpassung
- Content-Plan für nächste Saison
- A/B-Test-Auswertung

---

## 10. Hot-Patch + Krisen-Reaktion

### 10.1 Severity-Levels

| Level | Beispiel | SLA |
|-------|----------|-----|
| **P0 — Critical** | Server-Down, App crasht für >50 % Users, IAP funktioniert nicht | < 1 h Reaktion, < 4 h Fix |
| **P1 — Major** | Liga-Tabelle korrupt, Saison-Belohnung nicht verteilt, Anti-Cheat-False-Positive-Welle | < 4 h Reaktion, < 24 h Fix |
| **P2 — Minor** | Einzelnes Hero-Skill broken, Localization-Bug, UI-Glitch | < 24 h Reaktion, < 1 Woche Fix |
| **P3 — Cosmetic** | Tooltip falsch, Achievement-Counter off-by-one | < 1 Woche Reaktion, next Patch |

### 10.2 Hot-Fix-Pfade

```
Issue erkannt (Crashlytics-Alert / User-Report / Internal)
   ↓
Severity-Klassifizierung
   ↓
P0/P1: Sofortiger Lead-Dev-Alert (Slack-PagerDuty-Integration)
   ↓
Fix-Pfad-Wahl:
   ├── A) Remote-Config-Tweak (5-15 min)
   │     z.B. "Disable Event-X" via Flag
   ├── B) Addressables-Patch (1-2 h)
   │     z.B. Asset-Replacement ohne App-Update
   ├── C) Cloud-Function-Hotfix (30 min Deploy)
   │     z.B. Server-Validation-Bug
   ├── D) App-Update (Play-Store-Review: 1-3 Tage)
   │     z.B. Native-Code-Crash
   └── E) Notfall-Server-Maintenance (Photon-Pause)
         z.B. Massive Cheat-Exploit, alle Matches pausieren
```

### 10.3 Crisis-Communication-Plan

Bei P0/P1-Issues:
1. **Discord-Announcement** (innerhalb 30 min): "Wir wissen Bescheid, arbeiten an Fix"
2. **In-App-Modal** (via Remote Config): "Wartungsarbeiten — wir sind in 1h zurück"
3. **Twitter-Post** (parallel): Status-Update
4. **Email-Newsletter** (wenn Account-Daten betroffen)
5. **Post-Mortem-Post** (innerhalb 48h nach Fix): Was passiert, was wir gelernt haben

### 10.4 Refund-Policy

- **Spieler-Anfrage via In-Game-Support-Button** oder Email
- **Auto-Refund-Berechtigung:**
  - IAP innerhalb 7 Tage ohne Nutzung → automatischer Refund
  - Server-Down während Match-Win → Coins-Gutschrift (kein Cash-Refund)
- **Manual-Refund-Review:**
  - Saison-Pass-Refund nach Tier-Up → Customer-Support-Entscheidung
  - Hero-Skin nach 24h → typischerweise Ablehnung mit Höflichkeitsausgleich (200 Gems)

---

## 11. Esports-Foundation (optional, setzt Online-Versus voraus)

> **Hinweis:** Esports/Tournament setzt den optionalen **Online-Versus** voraus (der wiederum die
> Float-Determinismus-Lösung braucht, siehe ARCHITECTURE §13.0). Daher ein optionales Post-Launch-Thema,
> kein Teil des treuen Remake-Kerns.

### 11.1 Tournament-Mode

**Phase 2 (Monat 14+):** Open-Brackets-Tournament
- Spieler registrieren über In-Game-Tournament-Tab
- Wochenende-only (Sa-So)
- Automatic-Bracket-Generation (Single-Elimination)
- Prize: Saison-Trophy + Exclusive-Skin + Discord-Rolle
- Cap: 256 Spieler pro Region

**Phase 3 (Monat 17+):** Curated Esports-Tournaments
- Monthly Bracket mit Cash-Prize (1k-5k EUR Pool)
- Invitation-only oder via Qualifier
- Twitch-Streaming via Studio-Channel
- Caster-Talent-Vergabe

### 11.2 Spectator-Mode

**Phase 2:** In-Game-Spectator
- Spieler kann Friends' Matches zuschauen (mit Friend-Zustimmung)
- Cinemachine-Free-Camera mit Auto-Switching
- HUD mit Player-Stats + Match-Timer
- Spectator-Replay-Sharing (URL-basiert)

**Phase 3:** Producer-Mode für Casters
- Multiple-View-Composition für Stream-Overlay
- Player-Selection für Stream-Display
- Real-time-Stat-Overlay (Damage-Dealt, Bombs-Placed, Combos)
- Replay-Highlighting-Tools

### 11.3 Anti-Cheat-Esports-Grade

Bei Tournament-Mode:
- Verschärftes Replay-Re-Sim (alle Matches, nicht stichprobenartig)
- Mandatory-Webcam-Streaming für High-Stakes-Brackets (Manual-Verification)
- Hardware-ID-Lock pro Tournament
- Banned-Cheater dürfen nicht in 12 Monaten teilnehmen

---

## 12. Risiken (vollständiges Register)

### 12.1 Technische Risiken

| # | Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|--------|--------------------|--------|------------|
| T1 | Photon Fusion Determinismus auf Mobile schwierig | Mittel | Hoch | Frühe Prototypen, Lockstep-Fallback bereit halten |
| T2 | 4-Spieler-Mobile-Performance (60 FPS High / 30 FPS Low) | Mittel | Hoch | LOD, VFX-Skalierung, dedizierte Performance-Tests pro Sprint |
| T3 | Photon-Kosten bei 50k+ MAU explodieren | Niedrig (anfangs) | Hoch (bei Erfolg) | Bei Skala evaluieren: Custom Nakama, Mirror, Self-hosted |
| T4 | Cross-Platform-Save-Sync Inkonsistenz | Mittel | Mittel | Firebase als Single-Source-of-Truth, Auth-Provider-Tests |
| T5 | Anti-Cheat-Replay-Re-Sim auf Server zu langsam | Mittel | Hoch | C#-Worker mit .NET 10, isomorphes Domain-Modell |
| T6 | iOS Apple-Review-Verzögerung | Mittel | Mittel | Frühe Submission (4 Wo vor Launch), Apple-Reviewer-Beispiel-Account |
| T7 | Steam-Build-Macken (macOS+Linux) | Mittel | Niedrig | Steam-Deck als Test-Device, Continuous-Steam-Builds |
| T8 | Unity-Version-Update-Breaking-Changes | Niedrig | Mittel | Version pinnen, Update-Sprints planmäßig |
| T9 | Firebase-Quota-Limits (RTDB-Schreibrate) | Mittel | Hoch | Frühzeitig auf Firestore-Sharding evaluieren, Cache-Layer |
| T10 | *(nur falls Voice eingeführt)* AI-Voice-Qualität schwankt | Niedrig | Niedrig | Voice ist deferred/optional; Original ist voice-los — kein Launch-Risiko |

### 12.2 Game-Design-Risiken

| # | Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|--------|--------------------|--------|------------|
| D1 | Hero-Balancing (1-2 der 5 Helden zu stark) | Mittel | Mittel | Original-Balancing übernehmen, Telemetrie, Saisonale Patches |
| D2 | Karten-/Deck-Meta stagniert | Mittel | Mittel | Drop-/Crafting-Tuning, saisonale Cosmetic-Anreize (kein Pay-to-Win) |
| D3 | 3D-Lesbarkeit schlechter als 2D-Original (Top-Down-Klarheit) | Mittel | Hoch | Kamera-Tuning, HighContrast/Outline-Modi, Playtests gegen das 2D-Original |
| D4 | *(optional)* Co-op-Schwierigkeit zu hart/leicht | Mittel | Mittel | Beta-Test-Data, dynamische Skalierung (wie Original-Dungeon) |
| D5 | 3D-Remake verliert den Arcade-„Game-Juice" des Originals | Mittel | Hoch | Bestehende Juice-Patterns (Slow-Mo/Shake/Combo-Flash) 1:1 in 3D übernehmen |

### 12.3 Markt-Risiken

| # | Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|--------|--------------------|--------|------------|
| M1 | Bomberman-Genre Nische auf Mobile | Niedrig | Hoch | USPs (3D-Remake, tiefer Solo-Content, Cross-Save) + bestehende Spielerbasis als Anker |
| M2 | Konkurrenz von Bomb Squad/Bombsquad | Mittel | Mittel | Vorsprung durch 3D-Optik + tiefe Meta (Liga/Dungeon/100 Level), schnelle Iteration |
| M3 | *(nur falls Online-MP)* Mobile-PvP-Latency-Skepsis | Mittel | Mittel | Region-Server, Adaptive-Settings; MP ist optional, Solo-Kern unabhängig |
| M4 | Konkurrenz von ArcaneKingdom (eigene App-Family) | Mittel | Mittel | Unterschiedliche Genres, Cross-Promo möglich |
| M5 | Apple-Cross-Promo-Policy für iOS verhindert Mobile↔PC-Promotion | Hoch | Niedrig | Saubere Trennung, kein Cross-Save-Hint auf Apple-Build |
| M6 | DSGVO-Verschärfung wirft Datensammeln um | Mittel | Hoch | Datenminimierung von Tag 1, Server-EU |

### 12.4 Team-Risiken

| # | Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|--------|--------------------|--------|------------|
| P1 | Senior-Developer fällt aus (Knowledge-Silo) | Mittel | Hoch | Docs-Pflicht, Pair-Programming, Knowledge-Sharing-Sessions |
| P2 | Artist-Burnout durch Saison-Pipeline-Pressure | Mittel | Hoch | Realistische Sprint-Capacity, KI-Asset-Pipeline (ComfyUI) für Beschleunigung |
| P3 | Sound-Designer-Freelance fällt aus | Mittel | Niedrig | FMOD-Backup-Asset-Library, Freelance-Pool |
| P4 | Sprint-Drift durch Overcommitment | Hoch | Mittel | Konservative Sprint-Planning, Buffer-Tage |

### 12.5 Live-Service-Risiken

| # | Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|--------|--------------------|--------|------------|
| L1 | Saison-Content-Drift (zu wenig Inhalt für 8 Wochen) | Hoch | Hoch | 3 Saisons im Vorrat bei Launch, Content-Pipeline mit AI-Asset-Hilfe |
| L2 | Server-Crash am Saison-Launch-Day | Hoch | Hoch | Pre-Saison-Stress-Test, Graceful-Degradation-Plan |
| L3 | Saison-Pass-Conversion < Ziel | Mittel | Hoch | A/B-Test Saison-Themes, Marketing-Tweaks, Hero-Pre-Reveals |
| L4 | Player-Toxicity wächst (Reports steigern) | Mittel | Mittel | Schnellere Moderation, Auto-Mute, Voice-Chat-Opt-In |
| L5 | Cheat-Exploit wird viral (Twitter-Storm) | Niedrig | Sehr Hoch | Schnelle Patch-Pipeline (< 4h), Reverse-Engineering-Schutz |

---

## 13. Offene Entscheidungen

Diese Entscheidungen müssen vor oder während Phase 0-1 geklärt werden:

| # | Entscheidung | Frist | Status |
|---|--------------|-------|--------|
| 1 | Markenname: "BomberBlast" beibehalten (Default) oder Zusatz "3D"? | Vor Marketing-Start | Default: BomberBlast (Bestandsmarke) |
| 2 | FMOD vs. Unity Audio-only (Lizenz-Decision) | Phase 2 | Open |
| 3 | Voice überhaupt einführen? (Original ist bewusst voice-los) | Phase 5 | Default: Nein (deferred) |
| 4 | VIP-Subscription-Pricing/-Inhalt beibehalten oder anpassen | Phase 7 | Default: Original-Werte |
| 5 | **Online-Multiplayer** überhaupt bauen (Co-op/Versus) — und wenn ja, wann? | Post-Launch | Default: Local-MP ja, Online optional |
| 6 | **Legacy-Save-Import**: Migrations-Bonus für Alt-Spieler? | Phase 6 | Open (Mapping ist Pflicht, Bonus optional) |
| 7 | Steam-Build mit Cross-Match Mobile-PC erlauben | Phase 8 | Open (vermutlich Nein) |
| 8 | Hero-Anzahl Launch | Phase 1 | **Fix: 5 (= Original)**; 6. Held nur als bewusste [NEU]-Erweiterung |
| 9 | IAP-Preise: Original-Werte beibehalten oder re-pricen? | Phase 2 | Default: Original-Werte |
| 10 | Custom-Music-Composer vs. aufgewertete Bestands-Loops | Phase 5 | Open |
| 11 | Affiliate-Programm für Influencer | Phase 6+ | Open |

---

## 14. Erfolgs-Checkliste (Pre-Launch)

> Diese Checkliste muss VOR Phase 7 (Soft-Launch DACH) zu 100 % erfüllt sein.

### 14.1 Technisch

- [ ] EditMode-Tests pass-Rate 100 %, > 1.000 Tests
- [ ] PlayMode-Tests pass-Rate 100 %, > 50 Tests
- [ ] Determinismus-Suite: 1.000+ Replays pass-Rate 100 %
- [ ] Crashlytics: Crash-Free-Rate ≥ 99 % in Closed-Beta
- [ ] Build-Pipeline: AAB + Xcode-Project + Steam-Build via CI funktionieren
- [ ] Cloud-Save: Cross-Platform-Test Mobile↔PC ≥ 99 % Success
- [ ] **Legacy-Save-Import: Test mit echten Alt-Accounts ≥ 99 % Success** (35-Key-Mapping)
- [ ] *(optional, falls Online-MP)* Photon: simultane Matches getestet, Anti-Cheat erkennt Cheat-Attempts
- [ ] Performance: 60 FPS High-Tier, 30 FPS Low-Tier ≥ 95 % stabil
- [ ] Localization: alle 6 Sprachen voll, kein "_KeyMissing_"

### 14.2 Content (= Feature-Parität mit dem Original)

- [ ] 5 Helden mit echten Stats + HeroTrait + Unlock-Bedingungen
- [ ] 14 Bomben-Typen / 13 Karten (echte Rarities) + Deck/Crafting
- [ ] 12 PowerUps + Cure (echte Effekte + Unlock-Levels)
- [ ] 12 Gegner (+ Elite) + 5 Bosse (+ 8 Modifier, Duo-Encounter W9/W10) polished
- [ ] 10 Welten × 10 Levels = 100 Story-Levels (12 Layouts, 5 Mutatoren) + Master-Mode
- [ ] Roguelike-Dungeon (16 Buffs, 5 Synergien, 8 Upgrades) + Boss-Rush + Survival + Quick-Play
- [ ] Liga (5×3 + Perzentil) + Daily-Race + Daily/Weekly-Missions + 8 Wochen-Events + Lucky-Spin
- [ ] Battle Pass S1 mit 30 Tiers vollständig (10 Themes)
- [ ] 98 Cosmetics (32 Trails / 33 Frames / 33 Victories) + Skins
- [ ] 72 Achievements (5 Kategorien) — Hero-Unlock-IDs erhalten
- [ ] 10 Welt-Intro-Cutscenes + 9 Welt-Outro-Cutscenes (in 3D)
- [ ] *(optional)* Co-op Story/Dungeon + Local-Coop/Versus

### 14.3 Compliance

- [ ] PEGI-12 / USK-12 / ESRB-Teen-Rating bestätigt
- [ ] DSGVO: Privacy-Policy + Cookie-Consent + Right-to-Access + Right-to-Deletion
- [ ] COPPA: Age-Gate + Eltern-Zustimmung-Flow
- [ ] DPA-Verträge mit Firebase (+ Photon, falls Online-MP) signiert
- [ ] *(nur falls Voice nachträglich dazukommt)* AI-Voice-Disclosure in Credits

### 14.4 Marketing

- [ ] Discord-Server live mit Community
- [ ] Twitter/Instagram/TikTok-Accounts aktiv mit Followern
- [ ] 3 Trailers fertig: Cinematic + Gameplay + PvP-Highlight
- [ ] App-Store-Listings (Google + Apple) fertig in 6 Sprachen
- [ ] ASO-Keywords-Strategie
- [ ] Influencer-Pipeline mit 10+ DACH-Creators committed
- [ ] Press-Kit fertig (DE + EN)

### 14.5 Operations

- [ ] LiveOps-Tool-Dashboard live
- [ ] Customer-Support-Workflow (Email + In-App-Ticket)
- [ ] Crisis-Communication-Plan dokumentiert
- [ ] 24/7-Monitoring-Setup (PagerDuty oder ähnlich)
- [ ] Hot-Patch-Pipeline getestet (Remote-Config + Addressables-Patch)
- [ ] Saison-Calendar S1-S3 vorgeplant

---

## 15. Post-Launch-Roadmap (Saison 1-6)

> Saisons = **30-Tage-Battle-Pass-Zyklen** mit rotierendem Theme (aus den 10 bestehenden BP-Themes) +
> neuen Cosmetics + Live-Event. **Keine neuen Helden** (Original hat 5 feste Helden); neue Inhalte sind
> Cosmetics, Events, Balancing-Tuning und Plattform-Meilensteine. Echte neue Helden wären eine bewusste
> **[NEU]**-Erweiterung über den treuen Remake hinaus — separat zu entscheiden.

### Saison 1 (Monat 12-14)
- Launch-Saison, alle 5 Helden + alle 10 Welten verfügbar; BP-Theme: Classic
- LiveOps: Stabilität-Fokus, Tutorial-Funnel-Optimierung; Event: DoubleXP-Wochenende

### Saison 2 (Monat 14-16)
- BP-Theme aus Rotation (z.B. Cyberpunk); neue Cosmetics-Welle (Trails/Frames/Victories)
- iOS-Launch + EU-Launch (parallel); Event aus dem 8er-Wochen-Pool

### Saison 3 (Monat 16-18)
- BP-Theme aus Rotation; neue Cosmetics; Global-Launch
- Balancing-Pass (Karten/Liga) auf Basis der Telemetrie

### Saison 4 (Monat 18-20)
- BP-Theme aus Rotation (z.B. Halloween, saisonal); Steam-Demo-Phase
- *(optional)* Online-Co-op-Beta

### Saison 5 — Winter-Saison (Monat 20-22)
- BP-Theme: Winter; Christmas/NewYear-Special-Event; Steam-Full-Launch

### Saison 6 (Monat 22-24)
- BP-Theme aus Rotation; *(optional)* Online-Versus-Erweiterung (setzt Float-Determinismus-Lösung voraus)
- *(optional, bewusste [NEU]-Entscheidung)* Evaluierung eines 6. Helden als echte Content-Erweiterung

---

## Änderungslog (ROADMAP)

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-ROADMAP mit 18-Mo-Plan, Team, Marketing, Compliance, Risiken | Robert Schneider + Claude |
| 2026-05-30 | **v0.2** | **Auf treuen 3D-Remake ausgerichtet: Deliverables = Feature-Parität mit dem Original (5 Helden, 12 Gegner, 5 Bosse, 13 Karten, 30 BP-Tiers, 72 Achievements, 9 Shop-Upgrades, Dungeon, Liga 5×3+Perzentil). Phase 4 = Determinismus-Integration + Local-MP; Online-MP/Esports/Voice als optionale Post-Launch-Plus markiert. Sci-Fi-Reste (Vex/Mech-Wars/PULSE-VOLT/Royale/Affixe/Talente) entfernt; Legacy-Save-Import als Pflicht-Checklistenpunkt** | Robert Schneider + Claude |

---

> **Status:** Production-Plan v0.2 — treuer 3D-Remake. Bereit für Parity-Matrix + Sprint 0 Kickoff.
> **Nächste Schritte:** Parity-Matrix, Firebase-Setup (`bomberblast-arena` + Legacy-Import-Pfad), Unity-Projekt-Skelett.
