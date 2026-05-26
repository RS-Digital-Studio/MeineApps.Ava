# ROADMAP.md — HandwerkerImperium-Unity (12-Monate Wochenplan)

> **Vollständige Wochenweise-Aufschlüsselung der 7 Phasen.**
> Annahme: 1 Entwickler (Vollzeit) + Asset-Outsourcing (3D-Modelle, Animationen, Audio).
> **Visualisierungs-Ansatz:** Kompromissloses 3D von Anfang an → Asset-Pipeline parallel zur Code-Entwicklung.
> **Migrations-Ansatz:** Closed Beta parallel zur Avalonia-Production → Avalonia bleibt aktiv.

---

## Übersicht

| Phase | Wochen | Monate | Meilenstein |
|-------|--------|--------|-------------|
| **1: Tech-Foundation** | 1-8 | M 1-2 | Boot-Scene + DI + Save + Auth |
| **2: Core-Loop-Prototyp** | 9-12 | M 2-3 | 1 Werkstatt + 1 Mini-Game spielbar |
| **3: Werkstätten + Worker + Orders** | 13-20 | M 3-5 | Alle 10 Werkstätten, 6 Order-Types |
| **4: Forschung + Prestige + Crafting** | 21-28 | M 5-7 | Single-Player komplett |
| **5: Gilden + Multiplayer** | 29-36 | M 7-9 | Online-Features (Gilde, Co-op, Auktionen) |
| **6: Polish (3D, Shader, Audio)** | 37-44 | M 9-11 | Beta-Ready, alle 3D-Effekte |
| **7: Beta + Launch** | 45-52 | M 11-12 | Closed Beta → Production-Launch |

**Asset-Pipeline (parallel):** Woche 4 → Woche 40 (3D-Modelle, Audio, Animationen werden outgesourced).

---

## Phase 1: Tech-Foundation (Woche 1-8)

### Woche 1: Projekt-Setup

**Ziele:**
- Unity-Projekt anlegen
- Git-Branch `unity-main`
- VContainer + Firebase installiert
- Boot-Scene funktioniert

**Tasks:**
- [ ] Unity 6000.4.8f1 installieren
- [ ] Projekt `src/Apps/HandwerkerImperium.Unity/Unity/` erstellen, URP-Template
- [ ] Packages aus PLAN.md § 2.2 installieren
- [ ] Boot-Scene erstellen mit RootLifetimeScope (leer)
- [ ] CLAUDE.md, ARCHITECTURE.md, DESIGN.md committen
- [ ] Git: `unity-main` Branch, Erst-Commit

**Output:**
- Unity-Projekt baut Android-Dev-Build (leerer Screen)
- Boot-Scene lädt

### Woche 2: Assembly-Definitions + Domain-Layer

**Ziele:**
- 7 Asmdefs erstellt
- Erste Domain-Klassen (records, Enums)

**Tasks:**
- [ ] `HandwerkerImperium.Core.asmdef` erstellen
- [ ] `HandwerkerImperium.Domain.asmdef` (mit `noEngineReferences: true`)
- [ ] `HandwerkerImperium.Game.asmdef`
- [ ] `HandwerkerImperium.UI.asmdef`
- [ ] `HandwerkerImperium.Bootstrap.asmdef`
- [ ] `HandwerkerImperium.Editor.asmdef`
- [ ] `HandwerkerImperium.Domain.Tests.asmdef`
- [ ] Domain-Records: `Workshop`, `Worker`, `Order`, `Money`, `WorkerTier`, `WorkshopType` (alle aus Avalonia portiert)
- [ ] Enums: `Race`, `Rarity`, `OrderType`, `OrderStrategy`, etc.

**Output:**
- 7 Modules kompilieren
- Domain ist Unity-frei

### Woche 3: Core-Services (Logger, EventBus, GameClock)

**Tasks:**
- [ ] `ILogger`, `UnityLogger`
- [ ] `IEventBus`, `EventBus` (mit Lock, exception-safe)
- [ ] `IGameClock`, `RealtimeGameClock`
- [ ] `IRandomProvider`, `UnityRandomProvider`
- [ ] `Result<T>`-Type (für Fehler-Returns)
- [ ] Extension-Methods (`AddTo`, `DisposableAction`, etc.)
- [ ] **Tests:** EventBus-Tests, Result-Tests (NUnit, 10+ Tests)

**Output:**
- Core-Layer 100% testbar
- 10+ Domain-Tests grün

### Woche 4: Save-Pipeline + Migration

**Tasks:**
- [ ] `HwiSave` (Root-Klasse, 22 Slices)
- [ ] `JsonSaveSerializer<T>` (mit Newtonsoft.Json)
- [ ] `ISaveMigrator<HwiSave>` mit V1→V8 Pipeline
- [ ] `ISaveSanitizer` (Heirloom-Catalog-Check, Money-Cap)
- [ ] `LocalFirstSaveService<HwiSave>` (Local + Cloud-Stub)
- [ ] **Tests:** Migration-Tests V7→V8, Sanitize-Tests (20+ Tests)

**Asset-Pipeline-Start (parallel):**
- [ ] Briefing an 3D-Artist (3D-Werkstatt-Konzept-Sheet)
- [ ] Style-Guide schreiben (low-poly, stylized, low-fi)

**Output:**
- Save-Schema komplett
- Migration ist getestet

### Woche 5: VContainer-Setup + DI

**Tasks:**
- [ ] `RootLifetimeScope` mit allen Service-Registrierungen (siehe ARCHITECTURE.md § 3.1)
- [ ] `GameInstaller` als separate File
- [ ] `BootEntryPoint` (IAsyncStartable)
- [ ] `BalancingConfig.asset` (ScriptableObject)
- [ ] **Editor-Tool:** `FirstTimeSetupWizard`
- [ ] **Editor-Tool:** `BalancingDashboard` (Skeleton)

**Output:**
- DI funktioniert
- BootEntryPoint läuft

### Woche 6: Firebase-Integration

**Tasks:**
- [ ] Firebase-Config (google-services.json) für Beta-App-ID `com.meineapps.handwerkerimperium2.beta`
- [ ] `FirebaseAuthService` (Anonymous + Google Sign-In)
- [ ] `FirebaseAnalyticsService`
- [ ] `FirebaseCrashlyticsService`
- [ ] `FirebaseRemoteConfigService`
- [ ] `FirebaseCloudFunctionsService`
- [ ] `FirebaseCloudSaveService` (Realtime DB)
- [ ] **Test:** Login + Save + Sync funktioniert in Editor

**Output:**
- Player kann sich anonym einloggen
- Save wird zu Firebase hochgeladen

### Woche 7: Lokalisierung + RESX-Import

**Tasks:**
- [ ] Unity Localization Package konfigurieren
- [ ] String-Tables für DE, EN, ES, FR, IT, PT
- [ ] TextMesh Pro Font-Assets
- [ ] **Editor-Tool:** RESX-Import-Skript (`HandwerkerImperium → Localization → Import from RESX`)
- [ ] Import aller Avalonia-RESX-Dateien
- [ ] **Editor-Tool:** `LocalizationCheckTool` (fehlende Keys, Placeholder-Konsistenz)

**Output:**
- ~3.000 String-Keys importiert
- 6 Sprachen verfügbar

### Woche 8: Asset-Pipeline + Addressables

**Tasks:**
- [ ] Addressables-Setup
- [ ] Asset-Groups definiert (Bootstrap, UI.Common, Workshops.*, etc.)
- [ ] `AddressableLoader` mit LRU-Cache
- [ ] Beispiel-Assets (Test-3D-Model) addressable
- [ ] **Editor-Tool:** Addressables-Catalog-Builder im Menü

**Asset-Pipeline:**
- [ ] Erste 3D-Werkstatt-Modelle vom Artist (Holzwerkstatt — Test-Asset)

**Output:**
- Addressables läuft
- Phase 1 abgeschlossen → Foundation steht

---

## Phase 2: Core-Loop-Prototyp (Woche 9-12)

### Woche 9: Hub-Scene + erste Werkstatt

**Tasks:**
- [ ] Hub.unity (additive) mit `HubLifetimeScope`
- [ ] Persistent-Canvas in Boot mit FadeIn/Out
- [ ] `SceneLoaderService` (lädt Hub additive)
- [ ] 3D-Holzwerkstatt im Hub platziert
- [ ] Cinemachine-Hub-Camera
- [ ] Header-UI (Money/Level/GS) als UI Toolkit

**Output:**
- Spieler sieht Hub mit 1 Werkstatt

### Woche 10: GameLoop + Income

**Tasks:**
- [ ] `GameLoopService` mit Multi-Interval-Ticking (siehe ARCHITECTURE.md § 7.1)
- [ ] `GameStateService` mit Lock + ExecuteWithLock
- [ ] `WorkshopService.TickIncome()` (1s)
- [ ] `IncomeCalculator` (BaseValue × 1.02^Level)
- [ ] `BalancingConfig.WorkshopList` mit Holzwerkstatt-Definition
- [ ] **Tests:** Income-Berechnung (10+ Tests)

**Output:**
- Werkstatt verdient passiv Geld
- Money steigt im Header

### Woche 11: Order-System + erstes Mini-Game

**Tasks:**
- [ ] `OrderGenerator` (Quick-Order, einfachstes Type)
- [ ] `OrderService` (Generate, Accept, Complete)
- [ ] Mini-Game-Scene `MiniGame.unity`
- [ ] `IMiniGameRegistry` + `SawingMiniGameController`
- [ ] **Sawing-Mini-Game (3D-Prototyp):** 3D-Holzbrett, Säge folgt Touch, Splitter-Particles
- [ ] Order-Akzept-Flow (Dashboard → Mini-Game → Reward)

**Asset-Pipeline:**
- [ ] Sawing-Mini-Game 3D-Assets (Säge, Holzbrett, Werkbank)

**Output:**
- Spielbarer Loop: Auftrag annehmen → Sägen → Reward

### Woche 12: ScriptableObjects + DataImporter

**Tasks:**
- [ ] `WorkshopDefinition` ScriptableObject
- [ ] `WorkerTierDefinition` ScriptableObject
- [ ] `OrderTypeDefinition` ScriptableObject
- [ ] **Editor-Tool:** `DataImporter` (JSON → ScriptableObjects)
- [ ] Avalonia-Daten als JSON nach `StreamingAssets/Data/` kopieren
- [ ] Import läuft, validiert, generiert Assets

**Output:**
- Phase 2 abgeschlossen → Core-Loop spielbar mit 1 Werkstatt + 1 Mini-Game

---

## Phase 3: Werkstätten + Worker + Orders (Woche 13-20)

### Woche 13: Alle 10 Werkstätten

**Tasks:**
- [ ] Workshop-Liste vervollständigt (10 ScriptableObjects)
- [ ] Workshop-Unlock-Logik (Level-basiert)
- [ ] Workshop-Upgrade (Cost-Formel)
- [ ] Workshop-Rebirth-System (5 Sterne)
- [ ] Workshop-Spezialisierung (3 Typen)
- [ ] **Tests:** Upgrade-Cost-Formel, Rebirth-Logik (15+ Tests)

**Asset-Pipeline:**
- [ ] Werkstatt-3D-Modelle: Maurerei, Maler, Elektrik

**Output:**
- Alle 10 Werkstätten kaufbar + leveln

### Woche 14: 3D-Hub-Szene mit allen Werkstätten

**Tasks:**
- [ ] Hub-Layout mit 10 Werkstätten (isometrische Anordnung)
- [ ] Camera-Pan zwischen Werkstätten
- [ ] Workshop-Detail-Tap → Workshop.unity additive
- [ ] `WorkshopOrbitCamera` (Cinemachine)
- [ ] Workshop-Upgrade-Panel (UI Toolkit)

**Asset-Pipeline:**
- [ ] 3D-Modelle: Sanitär, Architekt, Designer

**Output:**
- 3D-Hub mit allen Werkstätten visualisiert

### Woche 15: Worker-System

**Tasks:**
- [ ] `Worker`-Record (10 Tiers, Stats, Mood, Fatigue)
- [ ] `WorkerService` (Hire, Fire, Train)
- [ ] `WorkerStatsCalculator` (Effizienz-Formel)
- [ ] Worker-Markt (15min Cycle)
- [ ] Worker-Training (3 Types, 5 Stufen)
- [ ] **Tests:** Stats-Formel, Mood-Decay, Training (20+ Tests)

**Output:**
- Worker können gehired werden

### Woche 16: Worker-3D-Avatare

**Tasks:**
- [ ] Spine 2D-Setup ODER Unity-Mecanim-Setup
- [ ] Worker-Avatar-Prefab (animiert)
- [ ] Worker-Idle-Animationen (Bobbing, Blinzeln)
- [ ] Worker laufen sichtbar im 3D-Hub (NavMesh)
- [ ] Mood-Indicator (Schwebe-Icon über Worker)

**Asset-Pipeline:**
- [ ] Worker-Avatar-Animationen (10 Tiers, 6 Hauttöne)
- [ ] 3D-Modelle: Bauinspektion, Schmiede, Tüftler

**Output:**
- Worker physisch in der 3D-Welt

### Woche 17: Order-Types & Strategien

**Tasks:**
- [ ] Alle 6 Order-Types implementiert (Quick, Standard, Large, Coop, Weekly, Material)
- [ ] 3 Strategien (Safe, Standard, Risk)
- [ ] Strategy-UI (3D-Risiko-Meter)
- [ ] Order-Reward-Formel mit allen Multipliern
- [ ] **Tests:** Order-Generation (15+ Tests)

**Output:**
- Vollständige Auftrag-Auswahl

### Woche 18: Live-Orders + VIP

**Tasks:**
- [ ] Live-Order-Spawn (25s, 50% Chance, max 5)
- [ ] VIP-Customer als 3D-NPC läuft zur Werkstatt
- [ ] Live-Order-Decay (3min)
- [ ] Stammkunden-Logik
- [ ] **Tests:** Live-Order-Spawn-Rates (10+ Tests)

**Output:**
- Live-Orders fühlen sich lebendig an

### Woche 19: Reputation + Tiers

**Tasks:**
- [ ] Reputation-System (0-100, Tier-Stufen)
- [ ] Reputation-Tier-Effekte (Reward 0.7x - 1.5x)
- [ ] Tier-Up-Celebration (3D-Trophäe)
- [ ] Hard-Fail bei Risk-Strategy (-10 Rep, 0 € Reward)
- [ ] **Tests:** Reputation-Tier-Transitions (10+ Tests)

**Output:**
- Reputation-Loop geschlossen

### Woche 20: Mini-Games (2D-Prototypen für die anderen 9)

**Tasks:**
- [ ] Alle 10 Mini-Games als 2D-Prototyp (für Game-Logic-Test)
- [ ] Mini-Game-Registry mit allen 10 Types
- [ ] Score-Rating-System (Perfect/Good/Ok/Miss)
- [ ] Auto-Complete-Ticket-Logik
- [ ] **Tests:** Mini-Game-Score-Caps (10+ Tests)

**Output:**
- Phase 3 abgeschlossen → 10 Werkstätten + Worker + 6 Order-Types spielbar

---

## Phase 4: Forschung + Prestige + Crafting (Woche 21-28)

### Woche 21: Forschungs-System (45 Nodes)

**Tasks:**
- [ ] `ResearchService` mit Timer + Effekt-Cache
- [ ] 45 `ResearchNodeDefinition` ScriptableObjects
- [ ] Branch-Logik (4 Branches mit Prerequisites)
- [ ] **Tests:** Research-Effects, Cache-Invalidation (15+ Tests)

**Output:**
- Forschung funktional (UI noch simpel)

### Woche 22: 3D-Forschungsbaum

**Tasks:**
- [ ] Research.unity oder Research-Panel im Hub
- [ ] 3D-Skill-Tree-Visualisierung
- [ ] Particle-Strom bei aktiver Forschung
- [ ] Goldene Aura bei abgeschlossenen Nodes
- [ ] Camera-Zoom zu Branch

**Asset-Pipeline:**
- [ ] Research-Icons (45 Stück)

**Output:**
- Beeindruckender 3D-Forschungsbaum

### Woche 23: Prestige-System

**Tasks:**
- [ ] `PrestigeService` (PP-Formel, Tier-Transitions)
- [ ] 7 Tiers (Bronze → Legende)
- [ ] Diminishing Returns
- [ ] Preservation-Regeln pro Tier
- [ ] **Tests:** Prestige-Berechnung (20+ Tests)

**Output:**
- Prestige-Reset funktioniert

### Woche 24: Prestige-Cinematic (Timeline)

**Tasks:**
- [ ] Prestige.unity (additive)
- [ ] Timeline-Sequenz (12s, 4 Phasen)
- [ ] Particle-Effekte (Geld zerspringt, Sterne)
- [ ] Bloom-Effekt für Badge
- [ ] Distortion-Shader für Multiplikator
- [ ] Cinemachine-Camera-Sequenz

**Output:**
- Beeindruckende Prestige-Cinematic

### Woche 25: Challenges + Heirloom

**Tasks:**
- [ ] Challenge-System (3 Slots pro Prestige)
- [ ] Heirloom-System (3 Slots, Premium 4)
- [ ] Ascension-Vorbereitung (nach 3× Legende, im MVP nicht aktiv)
- [ ] Eternal-Mastery (linear +0.5% pro Prestige)
- [ ] **Tests:** Challenge-Multiplier, Heirloom-Selection (15+ Tests)

**Output:**
- Meta-Progression komplett

### Woche 26: Crafting-System

**Tasks:**
- [ ] `CraftingService` mit 30 Recipes
- [ ] T1-T4 Material-Tiers
- [ ] Auto-Produktion (180s/360s)
- [ ] Cross-Workshop-Inputs (V7)
- [ ] **Tests:** Recipe-Berechnung, Material-Affinity (15+ Tests)

**Output:**
- Crafting läuft

### Woche 27: Lager (Warehouse V7)

**Tasks:**
- [ ] `WarehouseService` (20-200 Slots)
- [ ] Stack-Limits
- [ ] Auto-Sell-Regeln
- [ ] Material-Affinität (+20%)
- [ ] **Tests:** Warehouse-Overflow, Auto-Sell (10+ Tests)

**Output:**
- Vollständiges V7-System

### Woche 28: Markt + Achievements

**Tasks:**
- [ ] `MarketService` mit Sinus-Welle
- [ ] Buy/Sell mit Strafzoll
- [ ] Event-Modulation
- [ ] **AchievementService** mit 60+ Achievements
- [ ] 3D-Trophäen-Cinematic
- [ ] **Tests:** Market-Pricing, Achievement-Trigger (20+ Tests)

**Output:**
- Phase 4 abgeschlossen → Single-Player komplett

---

## Phase 5: Gilden + Multiplayer (Woche 29-36)

### Woche 29: Firebase-Realtime-DB-Integration

**Tasks:**
- [ ] `IFirebaseDatabase` (GetAsync, SetAsync, UpdateAsync, RunTransaction, Subscribe)
- [ ] `database.rules.json` aus Avalonia portiert + erweitert
- [ ] `.indexOn` für alle relevanten Pfade
- [ ] Cloud-Save mit Conflict-Resolution
- [ ] **Tests:** Firebase-Stubs für Tests

**Output:**
- Firebase-Layer komplett

### Woche 30: Gilden-CRUD

**Tasks:**
- [ ] `GuildService` (Create, Join, Leave)
- [ ] 6-stellige Invite-Codes
- [ ] Cloud-Function: `createGuild` (TypeScript)
- [ ] Member-Liste mit Rollen
- [ ] Wochenziele
- [ ] **Tests:** Guild-Lifecycle (15+ Tests)

**Output:**
- Spieler kann Gilden erstellen + beitreten

### Woche 31: Co-op-Orders + Auktionen

**Tasks:**
- [ ] `GuildCoopOrderService` mit HMAC + atomar PATCH
- [ ] `WorkerAuctionService` mit Bid-Logic + NPC-Bots (35%)
- [ ] HMAC-Signierung
- [ ] **Tests:** HMAC-Verify, Auction-Bid-Logic (20+ Tests)

**Output:**
- Co-op + Auktionen funktional

### Woche 32: 3D-Gilden-Hub

**Tasks:**
- [ ] Guild.unity (additive, optional eigenes Scene)
- [ ] Hall-Gebäude als 3D-Modelle
- [ ] Member-Avatare auf Hub-Karte
- [ ] Online-Indicator

**Asset-Pipeline:**
- [ ] Hall-Gebäude-Modelle (10 Stück)

**Output:**
- 3D-Gilden-Hub beeindruckt

### Woche 33: Boss-Kämpfe

**Tasks:**
- [ ] `GuildBossService` (6 Bosse)
- [ ] HMAC-signed Damage-Tracking
- [ ] Boss-3D-Modelle mit Animator
- [ ] Particle-Damage-FX
- [ ] **Tests:** Boss-Damage-Berechnung (15+ Tests)

**Asset-Pipeline:**
- [ ] Boss-3D-Modelle (6 Stück)

**Output:**
- Bosse spielbar

### Woche 34: Hall-Gebäude + Wochenziele

**Tasks:**
- [ ] `GuildHallService` (10 Gebäude, Level-Upgrades)
- [ ] Hall-Effekte (Income/Speed/Slots/etc.)
- [ ] Wochenziele-Tracking
- [ ] Belohnungs-Verteilung
- [ ] **Tests:** Hall-Effects, Wochenziele (15+ Tests)

**Output:**
- Gilden-Upgrade-System komplett

### Woche 35: Chat + Push-Notifications

**Tasks:**
- [ ] `GuildChatService` mit Firebase
- [ ] Profanity-Filter (DE/EN/ES/FR/IT/PT)
- [ ] `PushNotificationService` (8 Trigger)
- [ ] Notification-Scheduler
- [ ] **Tests:** Profanity-Filter (10+ Tests)

**Output:**
- Chat + Notifications live

### Woche 36: Anti-Cheat + Cloud-Functions

**Tasks:**
- [ ] Server-Side: 8 Cloud Functions deployed (TypeScript)
  - validateIapReceipt
  - validateMiniGameScore
  - settleBattlePassRewards
  - createGuild
  - onPlayerWriteValidate
  - onReportReceived
  - onWarSeasonCompleted
  - liveEventRefresh
- [ ] `SaveSanitizer`-Edge-Cases
- [ ] Rate-Limits in Firebase Security Rules
- [ ] **Manual-Test:** Cheat-Versuche detektiert

**Output:**
- Phase 5 abgeschlossen → Multiplayer + Anti-Cheat funktional

---

## Phase 6: Polish (Woche 37-44)

### Woche 37: 3D-Mini-Games (Sawing, Forge)

**Tasks:**
- [ ] Sawing-Mini-Game als finales 3D (mit Procedural-Holzmaserung, Splitter-Particles, Sound)
- [ ] Forge-Mini-Game (3D-Amboss, Feuer-Shader, Hammer-Funken)

**Asset-Pipeline:**
- [ ] Sawing + Forge 3D-Assets final

### Woche 38: 3D-Mini-Games (Pipe, Wiring, Painting)

**Tasks:**
- [ ] Pipe-Puzzle (3D-Rohrleitungs-Anlage, Wasser-Particles)
- [ ] Wiring (3D-Schaltkreis, Funken-FX)
- [ ] Painting (3D-Wand, Pinsel-Spuren, Tropf-Physik)

**Asset-Pipeline:**
- [ ] 3D-Assets dieser 3 Mini-Games

### Woche 39: 3D-Mini-Games (Blueprint, RoofTiling, Design)

**Tasks:**
- [ ] Blueprint (3D-Bauplan-Tisch)
- [ ] RoofTiling (3D-Dach, Ziegel-Physik)
- [ ] DesignPuzzle (3D-Raum-Editor)

**Asset-Pipeline:**
- [ ] 3D-Assets dieser 3 Mini-Games

### Woche 40: 3D-Mini-Games (Inspection, InventGame)

**Tasks:**
- [ ] Inspection (3D-Gebäude, Lupe scannt Wände)
- [ ] InventGame (3D-Labor, verbindbare Module)

**Asset-Pipeline:**
- [ ] 3D-Assets der letzten 2 Mini-Games

**Output:**
- Alle 10 Mini-Games als 3D-Erlebnis

### Woche 41: Particle-System + Shader-Graphs

**Tasks:**
- [ ] GPU-Particle-System (Coin-Fly, Confetti, Sparkle, Money-Burst)
- [ ] Shader-Graphs: WorkshopGlow, HolographicCard, MoneyShimmer, Dissolve
- [ ] Object-Pooling für Particles
- [ ] Mobile-Quality-Settings

### Woche 42: Post-Processing + Audio

**Tasks:**
- [ ] URP Post-Processing-Profile (Low/Med/High/Cinematic)
- [ ] AudioMixer mit Ducking + Snapshots
- [ ] BGM-Tracks (aus Avalonia + neue)
- [ ] SFX-Pool (82 Sounds + neue)
- [ ] 3D-Positional-Audio

**Asset-Pipeline:**
- [ ] Audio-Mastering
- [ ] Neue SFX für 3D-Mini-Games

### Woche 43: Daily/Weekly/BattlePass

**Tasks:**
- [ ] `DailyChallengeService` (5 Types, Rotation)
- [ ] `WeeklyMissionService` (4 Missions)
- [ ] `BattlePassService` (30 Tage, Free + Premium)
- [ ] BattlePass-UI mit Animationen
- [ ] **Tests:** Challenge-Rotation, BattlePass-Tier (20+ Tests)

### Woche 44: Tutorial + FTUE-Polish

**Tasks:**
- [ ] `TutorialService` mit 8 Schritten
- [ ] 3D-Tutorial-Highlight-Overlays
- [ ] Animierter Finger-Indikator
- [ ] Stage-Lighting für Tutorial-Fokus
- [ ] Story-Chapter 1-5 implementiert
- [ ] **QA:** Vollständiger Tutorial-Durchlauf

**Output:**
- Phase 6 abgeschlossen → Beta-Ready

---

## Phase 7: Beta + Launch (Woche 45-52)

### Woche 45: Closed Internal-Test

**Tasks:**
- [ ] Build → Internal Track (Dev-Team, 5 Tester)
- [ ] Bug-Bash-Session
- [ ] Performance-Profiling auf 5 Geräten
- [ ] Memory-Leaks fixen
- [ ] Crashes analysieren (Crashlytics)

### Woche 46: Bug-Fixing

**Tasks:**
- [ ] Top-20 Bug-Tickets abarbeiten
- [ ] Performance-Optimierungen (Texture-Compression, LOD)
- [ ] Stutter-Fixes

### Woche 47: Closed Alpha (20 Tester)

**Tasks:**
- [ ] Erweiterte Tester-Gruppe
- [ ] Telemetrie-Analyse (Funnel-Drops)
- [ ] Balancing-Feedback einarbeiten
- [ ] Bug-Fixes

### Woche 48: Lokalisierung-Review

**Tasks:**
- [ ] DE + EN: vollständige Review
- [ ] ES, FR, IT, PT: Auto-Translation-Review (durch Native-Speaker)
- [ ] Layout-Tests (lange Strings)
- [ ] Missing-Keys-Check

### Woche 49: Closed Beta (100 Tester)

**Tasks:**
- [ ] Closed Beta-Track im Play Store
- [ ] Vorbereitung: Beta-Anmeldungs-Link
- [ ] Marketing: Discord-Announce, Social-Media
- [ ] Tracking: Active-User-Metrics, Retention

### Woche 50: Performance-Pass

**Tasks:**
- [ ] FPS-Profile auf Low-/Mid-/High-End-Geräten
- [ ] APK-Size-Optimierung (<120 MB)
- [ ] Texture-Atlas-Audit
- [ ] Audio-Compression-Audit
- [ ] Memory-Watcher final

### Woche 51: Pre-Launch-QA

**Tasks:**
- [ ] Vollständiger Pre-Release-Checker (analog Avalonia)
- [ ] Save-Migration-Test (V1→V8)
- [ ] Network-Failure-Test (Offline-Modus)
- [ ] App-Pause/Resume-Test
- [ ] Final-Bug-Fixes

### Woche 52: Production-Launch

**Tasks:**
- [ ] Build → Closed Beta Track (final)
- [ ] Soft-Launch in DACH-Region (DE/AT/CH)
- [ ] Monitoring: Crashlytics, Analytics
- [ ] Live-Operations-Setup
- [ ] Marketing-Push
- [ ] **🎉 v1.0.0 Beta Released!**

**Output:**
- Phase 7 abgeschlossen → Beta-Launch live

---

## KI-Asset-Pipeline (parallel zur Code-Entwicklung)

> **Vollständige Pipeline-Spec:** [ASSETS_AI.md](ASSETS_AI.md) (924 Zeilen, EU-konform, kein Hunyuan)

### Asset-Pipeline-Phasen

**Phase A: Pilot (Woche 4-6, vor Skalierung)**
- 5 Pilot-Assets durchlaufen vollständige Pipeline:
  - Carpenter Lv1-5 (Workshop mit Modul-Split)
  - C-Tier Worker (m) mit 4 Mood-States
  - Tier-2-Crafting "Wooden Furniture"
  - Master-Tool "Golden Hammer" mit Emissive
  - City-Tile "Sunny Day Plaza"
- Audio-Pilot: Workshop-Idle-Loop "Carpenter" (10s seamless)
- Voice-Pilot: Meister-Hans "Bauauftrag bereit!" (DE)
- Workshop-Specialization-Test: Carpenter Efficiency-Skin
- **Output:** `F:\AI\ComfyUI_workflows\handwerkerimperium_unity\pilot_log.md`
- **Skalierungs-Freigabe:** 5/5 Pilots OK → Phase B

**Phase B: Skalierung (Woche 7-32)**

| Woche | KI-Pipeline-Output |
|-------|---------------------|
| 7-8 | Style-LoRA `handwerkerimperium_toon_v2` Training abgeschlossen (Kohya_ss, 4-8h auf RTX 4090) |
| 9-10 | 10 Werkstatt-Basis-Modelle (TRELLIS 2 → Blender-Cleanup → Modul-Split) |
| 11-12 | 50 Workshop-Upgrade-Decals (Substance Sampler + ComfyUI Glow-Maps) |
| 13-14 | 20 Worker-Basis (m/w × 10 Tiers, TRELLIS 2 → Mixamo Auto-Rig) |
| 15-16 | 80 Worker-Mood-Face-Textures (SDXL + ControlNet Inpainting) |
| 17-18 | 30 Crafting-Items T1-T3 (SPAR3D + TripoSG Batch) |
| 19-20 | 12 Master-Tools mit Emissive (SPAR3D) |
| 21-22 | 80 City-Tiles (TripoSG Batch, über Nacht ~12h) |
| 23-24 | 5 Affinity-Props + 30 Specialization-Skins |
| 25-26 | 30 Mini-Game-Props (SPAR3D / InstantMesh) |
| 27-28 | 3 T4-Hero-Crafting (TRELLIS 2 + Cloud-Polish Rodin Gen-2.5) |
| 29-30 | 2 Mega-Projekte mit 10 Bauphasen (Rodin Gen-2.5 für Hero) |
| 31-32 | 5 Prestige-Cinematic-Hero (Rodin Gen-2.5 + Substance Painter) |

**Phase C: Audio-Pipeline (Woche 33-36)**

| Woche | Audio-Output |
|-------|--------------|
| 33 | 10 BGM-Tracks (Stable Audio 3, 2-3min Loops) |
| 34 | 150 SFX (Stable Audio Open Small) |
| 35-36 | 1500 Meister-Hans-Voice-Lines (ElevenLabs Standard-Voice + Multilingual v2, 6 Sprachen × 250 Lines, batchable via Python-Skript) |

**Phase D: Integration & Polish (Woche 37-44)**

Während Code-Phase 6 (Polish):
- Unity-Import aller Assets (Addressables-Groups)
- Mastering Audio (−16 LUFS)
- Lizenz-Archiv (`F:\AI\Licenses\handwerkerimperium_unity\`)
- Asset-Metadata-JSON pro Asset

### Asset-Tool-Budget

| Posten | Kosten/Monat | Dauer | Gesamt |
|--------|--------------|-------|--------|
| Adobe Creative Cloud (Substance Sampler + Painter) | 60 € | 4 Monate | 240 € |
| ElevenLabs Pro | 22 € | 4 Monate | 88 € |
| Rodin Gen-2.5 (Hero-Assets, Free-Tier mit Sub-Top-Up) | 0-50 € | 4 Monate | 0-200 € |
| Cascadeur Indie (Free unter $100k Revenue) | 0 € | — | 0 € |
| ComfyUI + TRELLIS 2 + SPAR3D + Stable Audio (lokal) | 0 € | — | 0 € |
| Hardware-Strom (RTX 4090, ~400W über Pipeline-Zeiträume) | ~30 € | 4 Monate | 120 € |
| Unity Pro (optional für CI/Cloud-Build) | 185 € | 12 Monate | 2.220 € |
| **Total (ohne Unity Pro)** | — | — | **~650 €** |
| **Total (mit Unity Pro)** | — | — | **~2.870 €** |

> **Vergleich klassisches Outsourcing:** Wäre 17.000 € gewesen (3D-Artists, Audio-Designer, Voice-Actor).
> **Ersparnis:** ~14.000 € durch KI-Pipeline.

### Lizenz-Archiv (Pflicht für EU-AI-Act-Compliance)

`F:\AI\Licenses\handwerkerimperium_unity\` mit PDFs aller Tool-Lizenzen + Pro-Asset-Metadata mit `license_source` und `compliance_status`.

---

## Risiken & Mitigations

| Woche | Risiko | Mitigation |
|-------|--------|-----------|
| 4-8 | Firebase-Setup-Probleme | Frühe Validation, Stubs vorbereiten |
| 13-17 | 3D-Asset-Delays | Fallback: 2D-Stubs, später ersetzen |
| 20-25 | Komplexität Prestige | Pure-Tests früh, Mock-Daten |
| 29-36 | Cloud-Functions-Deployment | Lokaler Emulator, dann Deploy |
| 37-44 | Performance auf Low-End | Quality-Settings-Tiers, Mobile-Profil |
| 45-52 | Bug-Berg in Beta | Buffer von 2 Wochen, früher Beta-Start |

---

## Milestones & Meilenstein-Checks

### Meilenstein 1: Foundation (Ende Woche 8)
- [ ] Boot + DI + Save + Auth läuft
- [ ] 7 Asmdefs kompilieren
- [ ] 50+ Domain-Tests grün
- [ ] **Go/No-Go-Decision:** Weiter mit Phase 2?

### Meilenstein 2: Core-Loop (Ende Woche 12)
- [ ] Spielbar: 1 Werkstatt + 1 Mini-Game
- [ ] DataImporter funktioniert
- [ ] **Go/No-Go-Decision:** Asset-Pipeline-Tempo OK?

### Meilenstein 3: Werkstätten + Worker (Ende Woche 20)
- [ ] Alle 10 Werkstätten spielbar
- [ ] Worker physisch im 3D-Hub
- [ ] 100+ Tests grün
- [ ] **Go/No-Go-Decision:** Single-Player-Vision erreicht?

### Meilenstein 4: Single-Player komplett (Ende Woche 28)
- [ ] Prestige + Crafting + Achievements läuft
- [ ] 200+ Tests grün
- [ ] **Go/No-Go-Decision:** Multiplayer starten?

### Meilenstein 5: Multiplayer (Ende Woche 36)
- [ ] Gilden + Co-op + Auktionen + Bosse
- [ ] Anti-Cheat aktiv
- [ ] **Go/No-Go-Decision:** Polish-Phase starten?

### Meilenstein 6: Beta-Ready (Ende Woche 44)
- [ ] Alle 3D-Mini-Games fertig
- [ ] Audio + Shader + Post-FX
- [ ] Lokalisierung komplett
- [ ] **Go/No-Go-Decision:** Beta-Launch?

### Meilenstein 7: Production (Ende Woche 52)
- [ ] Beta-Launch DACH
- [ ] Crashlytics-Rate <0.5%
- [ ] FPS-Median 60 auf Mid-Tier
- [ ] **🎉 Released!**

---

## Post-Launch-Roadmap (Monate 13-24)

| Monat | Features |
|-------|----------|
| 13 | Ascension freischalten, Mega-Projekte (V2) |
| 14 | War-Season, Boss 3-6 |
| 15 | Hall-Gebäude 6-10, Day/Night-Cycle |
| 16 | Saisonale Live-Events (mehr), Live-Wetter |
| 17 | Worker-Tier-Voice-Lines (separate ElevenLabs-Voices pro Worker-Tier), Replay-Highlights |
| 18 | Cosmetic-DLC (Workshop-Skins, Worker-Outfits) |
| 19-21 | **Photon Fusion Live-PvP** (Echtzeit-Klan-Matches 5v5, neue Architektur-Schicht) — Tech-Investment + Closed-Beta-Phase |
| 22-23 | iOS-Launch Entscheidung + ggf. Apple Developer Account + Closed Beta auf iOS |
| 24 | iOS Production-Launch (falls Entscheidung positiv) |

### Phase 2 Major-Features

**Photon Fusion Live-PvP (Monat 19-21):**
- Stack: Photon Fusion 2.x für Netcode + Photon Cloud
- Use-Case: Echtzeit-Klan-Match 5v5 (statt async Boss-Schaden)
- Match-Format: Beide Klans bauen gegeneinander, höchster Output gewinnt
- Tech-Architektur: Neue Scene `LivePvP.unity` + dedicated PhotonLifetimeScope
- Server-Region: europe-west1 (Photon Cloud)
- Anti-Cheat: Server-Authoritative State (statt Client-HMAC)
- Geschätzter Aufwand: 8-10 Wochen (1 Entwickler)
- **Voraussetzung:** Beta-Spielerbasis > 1000 MAU, sonst nicht wirtschaftlich

**Worker-Tier-Voice-Lines (Monat 17):**
- Separate ElevenLabs-Voice pro Worker-Tier (10 Tiers × 6 Lines × 6 Sprachen = 360 Lines)
- Voices aus Library wählen (z.B. "junge Stimme" für F-Tier, "veteran" für SSS)
- Re-Generation jederzeit möglich

**iOS-Launch (Monat 22-24):**
- Voraussetzung: Beta-Erfolg + DAU > 5000
- Apple Developer Account 99$/Jahr
- Tech-Migration: Unity Cross-Platform = minimaler Aufwand
- Plattform-spezifische Anpassungen: Apple Sign-In statt Google, StoreKit statt Play Billing, Notch-Layout

---

## Tooling-Updates (kontinuierlich)

| Woche | Tool |
|-------|------|
| 5 | FirstTimeSetupWizard |
| 5 | BalancingDashboard |
| 7 | LocalizationCheckTool |
| 12 | DataImporter |
| 22 | SaveGameEditor |
| 22 | CheatsWindow (Dev-Build) |
| 28 | PerformanceProfiler-Integration |
| 36 | Cloud-Function-Deploy-Skripte |
| 44 | Build-Pipeline-Hardening |

---

## Team-Allocation (1 Entwickler + KI-Pipeline)

| Rolle | Auslastung |
|-------|-----------|
| **Entwickler (Robert)** | Vollzeit, alle Code-Bereiche |
| **KI-Pipeline-Operator (Robert)** | Wochen 4-36, je 5-10h/Woche (parallel zur Code-Arbeit). ComfyUI-Workflows + Blender-Cleanup + Mixamo + ElevenLabs-Voice-Cloning |
| **Hand-Polish (optional, Robert)** | Wochen 28-32, für Hero-Assets in Substance Painter |
| **Translator-Review (Native-Speaker)** | Wochen 7 + 48, einmalige Lieferung (4-8h/Sprache à 50 €) |
| **Beta-Tester (Community)** | Wochen 45-52, 5-100 Personen |

**Wichtig:** Die KI-Pipeline ist parallel-fähig — während Code kompiliert oder Tests laufen, kann ComfyUI im Hintergrund Batches generieren (City-Tiles über Nacht, Worker-Mood-Textures während Refactoring).

---

## Sprint-Cadence

- **Sprint-Länge:** 1 Woche (Mo-Fr Code, Sa-So Asset-Reviews + Planning)
- **Daily-Check:** Eigenkontrolle gegen Wochen-Ziele
- **Wöchentliche Retrospektive:** Was ist liegen geblieben? Was muss in nächste Woche?
- **Milestone-Reviews:** Alle 4-8 Wochen, Go/No-Go-Decision

---

## Verzögerungs-Strategie

Falls ein Sprint überzieht:
1. **Liegen-gelassene Tasks dokumentieren** (z.B. Asset-Polish, Optionales)
2. **Phase-Kritische Tasks** priorisieren (Single-Player-Loop > Visualisierung)
3. **Buffer einplanen** — Phase 7 hat 8 Wochen für Beta, kann auf 6 Wochen verkürzt werden
4. **Scope-Cut:** 2D-Fallback für 3D-Mini-Games als Backup
5. **KI-Pipeline-Backup:** Falls TRELLIS 2 für komplexe Werkstatt-Architektur versagt → Cloud-Fallback Rodin Gen-2.5 (50 € Sub-Top-Up reicht für ~50-100 Hero-Assets)

---

## Links

- [PLAN.md](PLAN.md) — Strategischer Plan
- [CLAUDE.md](CLAUDE.md) — Conventions
- [ARCHITECTURE.md](ARCHITECTURE.md) — Tech-Details
- [DESIGN.md](DESIGN.md) — Game Design Document
