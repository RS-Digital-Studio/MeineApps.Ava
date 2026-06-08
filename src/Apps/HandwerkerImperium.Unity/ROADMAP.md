# ROADMAP.md — HandwerkerImperium-Unity (Wochenplan)

> ⚠️ **STATUS (8.6.2026): an GDD-Phasen anzugleichen.** Dieser Wochenplan folgt der abgelösten 1:1-Richtung.
> Maßgeblich sind die Phasen **P0–P4** in **[3D_IDLE_GAME_PLAN.md §14](3D_IDLE_GAME_PLAN.md)** (Greybox-Loop →
> Vertical Slice → Content → Social/Beta → Polish/Cutover). Bis zur Neufassung gilt diese Datei nur für
> Tech-/Pipeline-Aufwände als grobe Referenz.

> **Vollständige Wochenweise-Aufschlüsselung der Entwicklungs-Phasen.**
> Annahme: 1 Entwickler (Vollzeit) + KI-Asset-Pipeline (3D-Modelle, Animationen, Audio inhouse via ComfyUI/Cloud, KEIN Outsourcing — siehe [ASSETS_AI.md](ASSETS_AI.md)).
> **Visualisierungs-Ansatz:** Kompromissloses 3D von Anfang an → KI-Asset-Pipeline parallel zur Code-Entwicklung.
> **Migrations-Ansatz:** Closed Beta parallel zur Avalonia-Production → Avalonia bleibt aktiv.

---

## Grundsatz & Vollständigkeits-Anspruch

**Das Endprodukt ist 1:1 vollständig** — GENAU DASSELBE SPIEL wie die produktive Avalonia-Version
(gleiche Mechaniken, Formeln, Balancing-Werte), NUR in 3D und mit besserer Präsentation. JEDES in
[DESIGN.md](DESIGN.md) dokumentierte System muss in dieser Roadmap eingeplant sein und im finalen
Release enthalten sein. "Besser/3D" betrifft ausschließlich Präsentation (Grafik, Hub, Cinematics,
Audio, Input, UI-Tech) — niemals Mechanik oder Balancing.

**Realismus-Hinweis (verbindlich):** Eine vollständige 1:1-Reimplementierung des Original-Umfangs
(91 Services, 77 Models, 80 ViewModels, 74 Views, ~28k LOC C# — siehe [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md))
PLUS komplette KI-3D-Asset-Produktion durch **1 Entwickler** ist in 52 Wochen **nicht** in voller Tiefe
erreichbar. Diese Roadmap ist daher **Beta-gestuft**: Sie liefert nach 52 Wochen eine **funktional
vollständige Closed Beta** (alle Systeme spielbar, ggf. mit 2D-Asset-Platzhaltern und reduziertem
Inhalts-Polish), und führt die **Vollständigkeit auf 1:1-Niveau** in der Stabilisierungs-Phase
(Beta → 1.0, Wochen 53-72) sowie der Post-1.0-Roadmap fort. **Kein System wird weggelassen** — die
Stufung betrifft Reihenfolge und Polish-Tiefe, nicht den Funktionsumfang.

| Beta-Stufe | Bedeutung |
|------------|-----------|
| **Closed Beta (Ende W52)** | Alle Kern- und Meta-Systeme spielbar (Single-Player + Multiplayer + Live-Ops), Inhalt teils reduziert (z.B. Story-Kapitel 1-20 statt 60, Asset-Platzhalter wo KI-Pipeline noch nachzieht) |
| **Content-Complete (W53-66)** | Voller Inhalt: alle 60 Story-Kapitel, alle 109+33 Achievements, alle 33 Rezepte, alle 72 Forschungs-Nodes verifiziert, finale 3D-Assets statt Platzhalter |
| **1.0 Vollständig (W67-72)** | 1:1-Parität zur Avalonia-Version verifiziert (Checkliste § Vollständigkeits-Matrix), Production-Launch |

---

## Übersicht

| Phase | Wochen | Monate | Meilenstein |
|-------|--------|--------|-------------|
| **1: Tech-Foundation** | 1-8 | M 1-2 | Boot-Scene + DI + Save + Auth |
| **2: Core-Loop-Prototyp** | 9-12 | M 2-3 | 1 Werkstatt + 1 Mini-Game spielbar |
| **3: Werkstätten + Worker + Orders** | 13-20 | M 3-5 | Alle 10 Werkstätten, 6 Order-Types |
| **4: Forschung + Prestige + Crafting** | 21-28 | M 5-7 | Single-Player komplett |
| **5: Gilden + Multiplayer + Live-Ops** | 29-38 | M 7-9 | Online-Features + Daily/Weekly/BattlePass/Live-Events |
| **6: Polish (3D, Shader, Audio)** | 39-46 | M 9-11 | Beta-Ready, alle 3D-Effekte |
| **7: Closed Beta** | 47-52 | M 11-12 | Funktional vollständige Closed Beta (alle Systeme spielbar) |
| **8: Content-Complete + 1.0** | 53-72 | M 13-18 | Voller Inhalt + verifizierte 1:1-Parität → Production-Launch |

**KI-Asset-Pipeline (parallel):** Woche 4 → Woche 44 (3D-Modelle, Audio, Animationen inhouse via
ComfyUI/lokale Modelle + Cloud-Polish für Hero-Assets — kein klassisches Outsourcing).

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
- [ ] KI-Asset-Pipeline aufsetzen (3D-Werkstatt-Konzept-Sheet als ComfyUI/TRELLIS-Prompt-Vorlage)
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
- [ ] Erste 3D-Werkstatt-Modelle aus der KI-Pipeline (Holzwerkstatt — Test-Asset)

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

### Woche 20: Mini-Games (2D-Prototypen für alle Renderer)

> **13 MiniGame-Enum-Typen, 10 distinkte Routen/Renderer** (Planing/TileLaying/Measuring teilen sich
> die Sawing-Route). Maßgeblich: 13 Enum-Typen, 10 Renderer, 8 perfekt-zählbar (DESIGN.md § 7).

**Tasks:**
- [ ] Alle 13 MiniGame-Enum-Typen in der Registry (10 Renderer-Routen + 3 geteilte Sawing-Familie)
- [ ] Mini-Game-Registry mit allen 13 Types (`IMiniGameRegistry`)
- [ ] Alle 10 distinkten Renderer als 2D-Prototyp (für Game-Logic-Test)
- [ ] Score-Rating-System (Perfect 100% / Good 75% / Ok 50% / Miss 0% — DESIGN.md § 7.2)
- [ ] Mini-Game-Mastery (Bronze/Silver/Gold — DESIGN.md § 7.4) + Master-Tool-Kopplung (§ 7.6)
- [ ] Auto-Complete-Ticket-Logik (DESIGN.md § 7.3, Schwellen § 30.11)
- [ ] MiniGameNavigator (Route-Map + Abbruch, DESIGN.md § 7.5)
- [ ] **Tests:** Mini-Game-Score-Caps, Auto-Complete-Schwellen (10+ Tests)

**Output:**
- Phase 3 abgeschlossen → 10 Werkstätten + Worker + 6 Order-Types spielbar

---

## Phase 4: Forschung + Prestige + Crafting (Woche 21-28)

### Woche 21: Forschungs-System (72 Nodes, 4 Branches)

**Tasks:**
- [ ] `ResearchService` mit Timer + Effekt-Cache + Effekt-Aggregation (DESIGN.md § 8.7/8.8)
- [ ] 72 `ResearchNodeDefinition` ScriptableObjects (Tools 20, Management 20, Marketing 20, Logistics 12 — DESIGN.md § 8.3-8.6)
- [ ] Branch-Logik (4 Branches mit Prerequisites)
- [ ] InstantFinish-GS-Kosten pro Level ab Level 8 (DESIGN.md § 8.9)
- [ ] **Tests:** Research-Effects, Effekt-Aggregation, Cache-Invalidation (15+ Tests)

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
- [ ] Research-Icons (72 Stück, 4 Branches)

**Output:**
- Beeindruckender 3D-Forschungsbaum

### Woche 23: Prestige-System + Prestige-Shop

**Tasks:**
- [ ] `PrestigeService` (PP-Formel `CalculateTotalPrestigePoints`, Tier-Transitions — DESIGN.md § 9.2)
- [ ] 7 Tiers (Bronze → Legende — DESIGN.md § 9.1)
- [ ] Permanenter Multiplikator + Diminishing Returns + Cap 20× (DESIGN.md § 9.3)
- [ ] Prestige-Bonus-PP (flat, nach Tier-Multiplikator — § 9.5) + Meilensteine (GS, § 9.4)
- [ ] Reset-Preservierung pro Tier (`ResetProgress` — DESIGN.md § 9.9)
- [ ] **Prestige-Shop (25 Items)** mit PP-Währung (DESIGN.md § 9.7)
- [ ] Prestige-Pass (IAP, permanent — § 9.3a) + Speedrun-Belohnungen (§ 9.10)
- [ ] **Tests:** Prestige-Berechnung, Prestige-Shop-Käufe, Speedrun-Brackets (25+ Tests)

**Output:**
- Prestige-Reset + Prestige-Shop funktionieren

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

### Woche 25: Challenges + Heirloom + Ascension + Eternal-Mastery

**Tasks:**
- [ ] Prestige-Challenge-System (max 3 parallel, additiv — DESIGN.md § 9.6)
- [ ] Heirloom-System (Run-Heirlooms beim Prestige, Pool = nur Tier-4-Crafting-Items — DESIGN.md § 24)
- [ ] Ascension-System (Meta-Prestige nach 3× Legende, Perks + Permanent-Heirlooms — DESIGN.md § 9.8 / § 24.3). Vollständig implementieren; Live-Schaltung gemäß Beta-Stufung (Content-Complete)
- [ ] Eternal-Mastery (permanenter Einkommens-Bonus mit Soft-Cap-Dämpfung — DESIGN.md § 25, NICHT linear)
- [ ] `IProgressionFacade` (Prestige + Rebirth + Ascension + EternalMastery)
- [ ] **Tests:** Challenge-Multiplier, Heirloom-Selection, Eternal-Mastery-Soft-Cap (15+ Tests)

**Output:**
- Meta-Progression komplett

### Woche 26: Crafting-System

**Tasks:**
- [ ] `CraftingService` mit **33 Rezepten** (T1: 10, T2: 10, T3: 10, T4: 3 — DESIGN.md § 10.1-10.4)
- [ ] T1-T4 Material-Tiers + StartCrafting-Ablauf inkl. Tier-1-Goldkosten + Stack-Schutz (§ 10.4a)
- [ ] Auto-Produktion (180s/360s — § 10.6) + Crafting-Speed-Bonus (§ 10.5)
- [ ] Cross-Workshop-Inputs (V7 — § 10.7)
- [ ] **Tools:** 8 aufrüstbare Werkzeuge (Goldschrauben — DESIGN.md § 10.8)
- [ ] **Tests:** Recipe-Berechnung, Material-Affinity, Tool-Upgrades (15+ Tests)

**Output:**
- Crafting (33 Rezepte) + Tools laufen

### Woche 27: Lager (Warehouse V7)

**Tasks:**
- [ ] `WarehouseService` (20-200 Slots)
- [ ] Stack-Limits
- [ ] Auto-Sell-Regeln
- [ ] Material-Affinität (+20%)
- [ ] **Tests:** Warehouse-Overflow, Auto-Sell (10+ Tests)

**Output:**
- Vollständiges V7-System

### Woche 28 (Mehr-Wochen-Block): Markt + Reputation + Equipment + QuickJob + Master-Tools + Gebäude + Achievements

> **Scope-Hinweis (Realismus):** Dies ist der dichteste Single-Player-Block — sieben Subsysteme inkl.
> 12 Master-Tools, 7 Gebäuden und 109 Achievements lassen sich **nicht** in einer einzelnen Kalenderwoche
> bauen. Der Block ist daher als **Mehr-Wochen-Scope** (realistisch ~2-3 Wochen) zu lesen und wird in zwei
> Teil-Pässe (28a/28b) gegliedert. Funktional vollständig; Inhalts-Polish (alle 109 Achievement-Texte,
> finale 3D-Trophäen) wird in der Content-Complete-Phase verifiziert. Die Phase-4-Grenze (W28) markiert das
> Ende des Single-Player-Funktionsumfangs, nicht eine harte 7-Tage-Kapazität.

**Teil-Pass 28a — Wirtschaft & Progression:**
- [ ] `MarketService` mit Sinus-Welle, Buy/Sell mit Strafzoll, Event-Modulation (DESIGN.md § 12, Unlock via logi_05)
- [ ] **Reputation-System** (Score 0-100, Tiers, Reward-Multiplikator, Decay, Tier-Up-Effekte — DESIGN.md § 13) inkl. **Reputation-Shop (5 Items, Unlock ab Score 60 — § 13.8)**
- [ ] **Equipment-System** (4 Rarity, Typen/Slots, 3 Stats, Erwerb, Prestige-Preservation — DESIGN.md § 16)
- [ ] **QuickJob / Schnellaufträge (eigenständiges Subsystem):** `QuickJobService` (Rotation alle 15/12/10/8 min nach Prestige, 5 Jobs gleichzeitig, Tages-Limit 20-40 + Prestige-Shop-Bonus, MiniGame-Kopplung pro Workshop, eigene Reward-/Difficulty-Formel, Tages-Reset — ORIGINAL_WERTE § QuickJob)

**Teil-Pass 28b — Artefakte, Gebäude & Achievements:**
- [ ] **Master-Tools (12 Artefakte)** inkl. Mini-Game-Kopplung (DESIGN.md § 14)
- [ ] **Gebäude (7 Stück)** mit Effekten (DESIGN.md § 15)
- [ ] **AchievementService** mit **109 Spieler-Achievements (17 Kategorien — DESIGN.md § 18)** + 3D-Trophäen-Cinematic
- [ ] **Tests:** Market-Pricing, Reputation-Tiers, Equipment-Rolls, QuickJob-Rotation/Rewards, Master-Tool-Boni, Achievement-Trigger (35+ Tests, über beide Teil-Pässe verteilt)

**Output:**
- Phase 4 abgeschlossen → Single-Player funktional vollständig (Inhalts-Verifikation in Content-Complete-Phase)

---

## Phase 5: Gilden + Multiplayer + Live-Ops (Woche 29-38)

> Deckt **alle** Online- und Live-Ops-Systeme aus DESIGN.md ab: komplettes Gilden-System (§ 17),
> Daily-Reward (§ 19), Lucky-Spin (§ 20), BattlePass (§ 21), Daily-Challenge + Weekly-Mission +
> Tournament (eigenständige Missions-/Wettbewerbs-Systeme), Live-/Random-Events (§ 22), Saisonale
> Events + Event-Shop (§ 23), Story-Chapters (§ 26), Notifications + WelcomeBack + WhatsNew (§ 28)
> und Monetarisierung (§ 29).

### Woche 29: Firebase-Realtime-DB-Integration

**Tasks:**
- [ ] `IFirebaseDatabase` (GetAsync, SetAsync, UpdateAsync, RunTransaction, Subscribe)
- [ ] `database.rules.json` aus Avalonia portiert + erweitert
- [ ] `.indexOn` für alle relevanten Pfade
- [ ] Cloud-Save mit Conflict-Resolution
- [ ] **Tests:** Firebase-Stubs für Tests

**Output:**
- Firebase-Layer komplett

### Woche 30: Gilden-CRUD + Guild-Research

**Tasks:**
- [ ] `GuildService` (Create, Join, Leave — **max 20 Mitglieder**, DESIGN.md § 17.1)
- [ ] 6-stellige Invite-Codes
- [ ] Cloud-Function: `createGuild` (TypeScript)
- [ ] Member-Liste mit Rollen
- [ ] **Guild-Research (18 Nodes, 6 Kategorien — DESIGN.md § 17.2)** mit Research-Cache
- [ ] Gilden-Tipps (`GuildTips` — § 17.11)
- [ ] **Tests:** Guild-Lifecycle, Guild-Research-Effekte (15+ Tests)

**Output:**
- Spieler kann Gilden erstellen + beitreten, Guild-Research aktiv

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

### Woche 34: Hall-Gebäude + Mega-Projekte + War-Season + Guild-Achievements

**Tasks:**
- [ ] `GuildHallService` (**10 Gebäude**, Level-Upgrades, Hall-Effekte — DESIGN.md § 17.5)
- [ ] **Mega-Projekte (2 Templates: Cathedral, HQ — DESIGN.md § 17.6 / § 34)** mit Bauphasen + Donations
- [ ] **Kriegssaison (`GuildWarSeasonService` — DESIGN.md § 17.7)**
- [ ] Wochenziele-Tracking + Belohnungs-Verteilung
- [ ] **Guild-Achievement-System (33 Einträge — DESIGN.md § 17.10)**
- [ ] `IGuildFacade` komplett (9 Gilden-Services) + Tick-Offsets + HMAC (§ 17.13)
- [ ] **Tests:** Hall-Effects, Mega-Projekt-Donations, War-Season-Scoring, Guild-Achievements (25+ Tests)

**Output:**
- Gilden-System vollständig (alle 9 Services aus § 17)

### Woche 35: Chat + Push-Notifications + In-App-Bell

**Tasks:**
- [ ] `GuildChatService` mit Firebase
- [ ] Profanity-Filter (DE/EN/ES/FR/IT/PT)
- [ ] `PushNotificationService` (**8 Trigger — DESIGN.md § 28.1**) + Notification-Scheduler
- [ ] **In-App-Bell / NotificationCenter (DESIGN.md § 28.2)** + Lokalisierung (§ 28.3)
- [ ] **WelcomeBack (3 Offer-Typen):** `WelcomeBackService` (StarterPack einmalig ab Level 5, Premium ab 72h Abwesenheit, Standard ab 24h; Geld-/GS-/XP-Belohnung mit harten Money-Caps, 24h-Ablauf, Angebot in der In-App-Bell sammelbar — ORIGINAL_WERTE § WelcomeBack)
- [ ] **WhatsNew (Versions-Feature-Dialog):** `WhatsNewService` (kumulativer Update-Dialog, sortierte Versions-/Feature-Key-Arrays, `LastWhatsNewVersion`-Vergleich, Brandneue-Spieler-Skip, `lastSeen`-Persistenz vor Render gegen Doppel-Anzeige — ORIGINAL_WERTE § WhatsNew)
- [ ] **Tests:** Profanity-Filter, Notification-Trigger, WelcomeBack-Offer-Auswahl/Caps, WhatsNew-Versions-Diff (15+ Tests)

**Output:**
- Chat + Notifications (Push + In-App-Bell) live

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
- [ ] HMAC-Signierung kritischer Werte (DESIGN.md § 31.1) + Server-Side-Validation (§ 31.2)
- [ ] `SaveSanitizer`-Edge-Cases (Reparatur statt Ablehnung — § 31.3) + Offline-Zeit-Schutz (§ 31.4)
- [ ] Rate-Limits in Firebase Security Rules (§ 31.5)
- [ ] **Manual-Test:** Cheat-Versuche detektiert

**Output:**
- Multiplayer + Anti-Cheat funktional

### Woche 37: Daily-Reward + Lucky-Spin + BattlePass

**Tasks:**
- [ ] **Daily-Reward** (30-Tage-Zyklus, Streak + Skalierung, VIP Auto-Claim — DESIGN.md § 19)
- [ ] **Lucky-Spin** (8 Slots, Gewichte/Belohnungen, Verfügbarkeits-Priorität — DESIGN.md § 20)
- [ ] **BattlePass** (`BattlePassService`, 50 Tier, 30-Tage-Saison, Free + Premium-Track, XP-Schwellen, Saison-Theme-Rotation, Premium-Lock-in — DESIGN.md § 21)
- [ ] **Daily-Challenge (eigenständiges Solo-System):** `DailyChallengeService` (15 Challenge-Types, 3/Tag + VIP-Bonus, Tier-Skalierung, Auto-Tracking via GameState-Events, Alle-fertig-Bonus — DESIGN.md / ORIGINAL_WERTE § Daily-Challenge). NICHT mit Daily-Reward verwechseln.
- [ ] **Weekly-Mission (eigenständiges Solo-System):** `WeeklyMissionService` (15 Mission-Types, 5/Woche + VIP-Bonus, höhere Belohnungen als Daily, Wochen-Reset, Alle-fertig-Bonus — ORIGINAL_WERTE § Weekly-Mission). NICHT mit dem Gilden-Wochenziel verwechseln.
- [ ] **Tournament (wöchentliches MiniGame-Turnier):** `TournamentService` (eigener Service + Save-Slice `CurrentTournament`, wöchentlicher Zufalls-MiniGame-Typ, 3 Gratis-Teilnahmen/Tag dann 5 GS Entry, 9 simulierte Gegner als Fallback, Play-Games-Leaderboards, Gold/Silver/Bronze-Reward nach Rang — ORIGINAL_WERTE § Tournament)
- [ ] `IMissionsFacade` (bündelt DailyChallenge, WeeklyMission, LuckySpin, QuickJob, Goal)
- [ ] **Tests:** Daily-Streak, Spin-Gewichte, BattlePass-Tier/XP, Daily-Challenge-Rotation, Weekly-Mission-Reset, Tournament-Reward-Tiers (30+ Tests)

**Output:**
- Tägliche/wöchentliche Live-Ops-Schleifen komplett

### Woche 38: Live-Events + Saisonale Events + Monetarisierung

**Tasks:**
- [ ] **Live Events** (4 Templates, Limited-Time — DESIGN.md § 22.A) + **Random Events** (8 Typen + saisonal — § 22.B)
- [ ] **Saisonale Events (4/Jahr)** + Event-Shop (Basis + Saison-einzigartige Items — DESIGN.md § 23)
- [ ] **Monetarisierung:** Imperium-Pass (4,99 €), IAP-Bundles, **13 Ad-Placements (§ 29.3)**, GS-Quellen (§ 29.4), Daily-Bundle (§ 29.5), Shop Daily Offer (§ 29.6), Cross-Promotion (§ 29.7), VIP (§ 29.8), Referral (§ 29.9)
- [ ] `IPurchaseService` / `IRewardedAdService` via DI (Platform-Services)
- [ ] **Tests:** Live-Event-Lifecycle, Seasonal-Item-Effekte, IAP-Grant-Logik (25+ Tests)

**Output:**
- Phase 5 abgeschlossen → Multiplayer + komplette Live-Ops + Monetarisierung funktional

---

## Phase 6: Polish (Woche 39-46)

### Woche 39: 3D-Mini-Games (Sawing, Forge)

**Tasks:**
- [ ] Sawing-Mini-Game als finales 3D (mit Procedural-Holzmaserung, Splitter-Particles, Sound)
- [ ] Forge-Mini-Game (3D-Amboss, Feuer-Shader, Hammer-Funken)

**Asset-Pipeline:**
- [ ] Sawing + Forge 3D-Assets final

### Woche 40: 3D-Mini-Games (Pipe, Wiring, Painting)

**Tasks:**
- [ ] Pipe-Puzzle (3D-Rohrleitungs-Anlage, Wasser-Particles)
- [ ] Wiring (3D-Schaltkreis, Funken-FX)
- [ ] Painting (3D-Wand, Pinsel-Spuren, Tropf-Physik)

**Asset-Pipeline:**
- [ ] 3D-Assets dieser 3 Mini-Games

### Woche 41: 3D-Mini-Games (Blueprint, RoofTiling, Design)

**Tasks:**
- [ ] Blueprint (3D-Bauplan-Tisch)
- [ ] RoofTiling (3D-Dach, Ziegel-Physik)
- [ ] DesignPuzzle (3D-Raum-Editor)

**Asset-Pipeline:**
- [ ] 3D-Assets dieser 3 Mini-Games

### Woche 42: 3D-Mini-Games (Inspection, InventGame)

**Tasks:**
- [ ] Inspection (3D-Gebäude, Lupe scannt Wände)
- [ ] InventGame (3D-Labor, verbindbare Module)

**Asset-Pipeline:**
- [ ] 3D-Assets der letzten 2 Mini-Games

**Output:**
- Alle 10 distinkten Mini-Game-Renderer als 3D-Erlebnis (deckt alle 13 Enum-Typen ab — Sawing-Familie geteilt)

### Woche 43: Particle-System + Shader-Graphs

**Tasks:**
- [ ] GPU-Particle-System (Coin-Fly, Confetti, Sparkle, Money-Burst)
- [ ] Shader-Graphs: WorkshopGlow, HolographicCard, MoneyShimmer, Dissolve
- [ ] Object-Pooling für Particles
- [ ] Mobile-Quality-Settings

### Woche 44: Post-Processing + Audio

**Tasks:**
- [ ] URP Post-Processing-Profile (Low/Med/High/Cinematic)
- [ ] AudioMixer mit Ducking + Snapshots
- [ ] BGM-Tracks (aus Avalonia + neue)
- [ ] SFX-Pool (82 Sounds + neue)
- [ ] 3D-Positional-Audio

**Asset-Pipeline:**
- [ ] Audio-Mastering
- [ ] Neue SFX für 3D-Mini-Games

### Woche 45: Tutorial / FTUE + Story-Anbindung

**Tasks:**
- [ ] `FtueService` mit **10 Schritten** (`s_defaultSteps`, Order 0-9 — DESIGN.md § 27.1, NICHT 8) + `FtueProgressTracker`-Verdrahtung (§ 27.3)
- [ ] **ContextualHints (32 — DESIGN.md § 27.4)**
- [ ] 3D-Tutorial-Highlight-Overlays + animierter Finger-Indikator + Stage-Lighting für Tutorial-Fokus
- [ ] **Story-Chapters (60 = 40 Haupt + 20 Saison — DESIGN.md § 26)**: Modell, Freischalt-Logik, Belohnungs-Auszahlung; Kapitel-Inhalte werden in der Content-Complete-Phase vervollständigt (Beta: 1-20)
- [ ] **QA:** Vollständiger Tutorial-Durchlauf

**Output:**
- FTUE (10 Schritte) + Story-Gerüst stehen

### Woche 46: Visual-Polish + Game-Juice

**Tasks:**
- [ ] City-Hub-Design final (DESIGN.md § 33): Stadt-Wachstum, City-Tiles, Camera-System, Worker-Bewegung
- [ ] Celebration-/Floating-Text-Effekte über alle Systeme (Order-Complete, Level-Up, Achievement)
- [ ] Final-Pass Game-Juice (Gold-Shimmer, Micro-Animations)
- [ ] Telemetrie-Events verdrahtet (DESIGN.md § 32)
- [ ] **QA:** Visueller Konsistenz-Check über alle Screens

**Output:**
- Phase 6 abgeschlossen → Beta-Ready

---

## Phase 7: Closed Beta (Woche 47-52)

> Ziel: **funktional vollständige Closed Beta** — alle Systeme aus DESIGN.md spielbar (ggf. mit
> 2D-Asset-Platzhaltern und reduziertem Inhalt, z.B. Story 1-20). Voll-Inhalt + 1:1-Parität folgt in Phase 8.

### Woche 47: Closed Internal-Test

**Tasks:**
- [ ] Build → Internal Track (Dev-Team, 5 Tester)
- [ ] Bug-Bash-Session
- [ ] Performance-Profiling auf 5 Geräten
- [ ] Memory-Leaks fixen
- [ ] Crashes analysieren (Crashlytics)

### Woche 48: Bug-Fixing + Closed Alpha (20 Tester)

**Tasks:**
- [ ] Top-20 Bug-Tickets abarbeiten
- [ ] Performance-Optimierungen (Texture-Compression, LOD), Stutter-Fixes
- [ ] Erweiterte Tester-Gruppe (20)
- [ ] Telemetrie-Analyse (Funnel-Drops) + Balancing-Feedback einarbeiten

### Woche 49: Lokalisierung-Review

**Tasks:**
- [ ] DE + EN: vollständige Review
- [ ] ES, FR, IT, PT: Auto-Translation-Review (durch Native-Speaker)
- [ ] Layout-Tests (lange Strings)
- [ ] Missing-Keys-Check (`LocalizationCheckTool`, 6 Sprachen)

### Woche 50: Closed Beta (100 Tester)

**Tasks:**
- [ ] Closed Beta-Track im Play Store (App-ID `com.meineapps.handwerkerimperium2.beta`)
- [ ] Vorbereitung: Beta-Anmeldungs-Link
- [ ] Marketing: Discord-Announce, Social-Media
- [ ] Tracking: Active-User-Metrics, Retention

### Woche 51: Performance-Pass + Pre-Launch-QA

**Tasks:**
- [ ] FPS-Profile auf Low-/Mid-/High-End-Geräten, APK-Size-Optimierung (<120 MB)
- [ ] Texture-Atlas-Audit, Audio-Compression-Audit, Memory-Watcher final
- [ ] Vollständiger Pre-Release-Checker (analog Avalonia) + Save-Migration-Test (V1→V8)
- [ ] Network-Failure-Test (Offline-Modus), App-Pause/Resume-Test, Final-Bug-Fixes

### Woche 52: Closed-Beta-Release

**Tasks:**
- [ ] Build → Closed Beta Track (final)
- [ ] Soft-Launch der Beta in DACH-Region (DE/AT/CH)
- [ ] Monitoring: Crashlytics, Analytics
- [ ] Live-Operations-Setup
- [ ] Marketing-Push

**Output:**
- Phase 7 abgeschlossen → funktional vollständige Closed Beta live (Inhalt/Polish-Vervollständigung in Phase 8)

---

## Phase 8: Content-Complete + 1.0 (Woche 53-72)

> Ziel: **vollständige 1:1-Parität** zur produktiven Avalonia-Version verifizieren — voller Inhalt,
> finale 3D-Assets statt Platzhalter, alle Systeme über die Vollständigkeits-Matrix abgehakt.

### Woche 53-58: Content-Vervollständigung

**Tasks:**
- [ ] **Story:** alle 60 Kapitel (40 Haupt + 20 Saison) inhaltlich fertig + Meister-Hans-Voice (DESIGN.md § 26)
- [ ] **Achievements:** alle 109 Spieler-Achievements + 33 Gilden-Achievements mit finalen Texten/Triggern verifiziert
- [ ] **Forschung:** alle 72 Nodes (4 Branches) mit korrekten Effekten gegen Original verifiziert
- [ ] **Crafting:** alle 33 Rezepte (T1-T4) gegen Original-Werte verifiziert
- [ ] **Prestige-Shop (25)** + **Reputation-Shop (5)** + **Event-Shop** Items vollständig + balanciert
- [ ] Saison-Kapitel, Saison-Themes, Saisonale Events über vollen Jahres-Zyklus geprüft
- [ ] Finale KI-3D-Assets ersetzen alle 2D-Platzhalter (siehe KI-Asset-Pipeline Phase E)

### Woche 59-64: Balancing-Parität

**Tasks:**
- [ ] Sämtliche Formeln (Income, Offline-Income, Upgrade-Kosten, Order-Reward, XP-Kurve, Soft-Caps) numerisch gegen Original-Code (ORIGINAL_WERTE.md) gegengerechnet
- [ ] Balancing-Werte (Workshop-BaseValues, Worker-Tiers, Manager 14, Master-Tools 12, Gebäude 7) 1:1 abgeglichen
- [ ] Ascension live geschaltet + verifiziert (3× Legende → Permanent-Heirlooms)
- [ ] Speedrun-Belohnungen, Eternal-Mastery-Soft-Cap, Heirloom-Boni gegengeprüft
- [ ] End-to-End-Telemetrie-Funnel über vollständigen Spielverlauf

### Woche 65-72: 1.0-Launch

**Tasks:**
- [ ] Vollständigkeits-Matrix (siehe unten) zu 100% abgehakt
- [ ] Open-Beta → Production-Track (Cutover-Entscheidung ggü. Avalonia erst nach erfolgreicher Beta)
- [ ] Final-Performance-Pass auf voller Asset-Last
- [ ] Production-Launch v1.0.0, Live-Ops-Betrieb, Marketing-Push

**Output:**
- Phase 8 abgeschlossen → 1:1-vollständige 3D-Version, Production-Launch

---

## Vollständigkeits-Matrix (1:1-Parität — Pflicht vor 1.0)

> Jedes System aus [DESIGN.md](DESIGN.md) muss vor dem 1.0-Launch implementiert UND gegen das
> Original verifiziert sein. Referenzzahlen aus [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) / DESIGN.md.

| System | Referenz-Umfang | Eingeplant (Phase/Woche) |
|--------|-----------------|--------------------------|
| Werkstätten | 10 Typen + Rebirth (0-5★) + Spezialisierung + Manager (14) | Phase 3 (W13-14) |
| Arbeiter | 10 Tiers, Mood/Fatigue, Training, Praktikanten, Personalities (6) | Phase 3 (W15-16) |
| Aufträge | 6 Types + 3 Strategien + Live-Orders/VIP + Material-Orders | Phase 3 (W17-18) |
| Reputation + Reputation-Shop | Score 0-100, Tiers, Shop (5 Items) | Phase 4 (W28) |
| Mini-Games | 13 Enum-Typen / 10 Renderer / 8 perfekt-zählbar + Mastery | Phase 3 (W20) + Phase 6 (W39-42) |
| Forschung | 72 Nodes, 4 Branches | Phase 4 (W21-22) |
| Prestige + Prestige-Shop | 7 Tiers, Shop (25 Items), Challenges, Speedrun | Phase 4 (W23-25) |
| Ascension + Heirloom + Eternal-Mastery | Meta-Prestige, Permanent-Heirlooms, Soft-Cap-Bonus | Phase 4 (W25) + live ab Phase 8 |
| Crafting | 33 Rezepte (T1-T4) + 8 Tools + Lager (V7) + Markt | Phase 4 (W26-28) |
| Equipment | 4 Rarity, Slots, Stats | Phase 4 (W28) |
| QuickJob / Schnellaufträge | eigenes Subsystem: Rotation 15/12/10/8 min, 5 Jobs, Tages-Limit 20-40 + Shop-Bonus, MiniGame-Kopplung | Phase 4 (W28) |
| Master-Tools | 12 Artefakte | Phase 4 (W28) |
| Gebäude | 7 Stück | Phase 4 (W28) |
| Gilden | max 20 Mitglieder, Guild-Research (18), Hall (10), Bosse (6), Mega-Projekte (2), War-Season, Chat, Guild-Achievements (33) | Phase 5 (W30-36) |
| Daily-Reward / Lucky-Spin / BattlePass | 30-Tage / 8 Slots / 50 Tier | Phase 5 (W37) |
| Daily-Challenge / Weekly-Mission / Tournament | Solo-Systeme: Daily 9 Types (3/Tag) · Weekly 9 Types (5/Woche) · Tournament wöchentl. MiniGame (eigener Service + Save-Slice, 9 Sim-Gegner, Play-Games-Leaderboard) | Phase 5 (W37) |
| Live-/Random-/Saisonale Events + Event-Shop | 4 Live + 8 Random + 4 Saisons | Phase 5 (W38) |
| Monetarisierung | Imperium-Pass, IAP-Bundles, 13 Ad-Placements, VIP, Referral | Phase 5 (W38) |
| Story | 60 Kapitel (40 + 20) + Meister-Hans-Voice | Phase 6 (W45) + Inhalt Phase 8 |
| FTUE + ContextualHints | 10 Schritte + 32 Hints | Phase 6 (W45) |
| Notifications | 8 Push-Trigger + In-App-Bell | Phase 5 (W35) |
| WelcomeBack + WhatsNew | WelcomeBack (3 Offer-Typen: StarterPack/Premium/Standard, Money-Caps) · WhatsNew (kumulativer Versions-Feature-Dialog) | Phase 5 (W35) |
| Anti-Cheat | HMAC, Server-Validation, Sanitizer, Rate-Limits | Phase 5 (W36) |
| Telemetrie | Event-Katalog (snake_case), 11 User-Properties | Phase 6 (W46) |
| City-Hub | 80 City-Tiles, Camera, Worker-Bewegung | Phase 3/6 + Assets |

---

## KI-Asset-Pipeline (parallel zur Code-Entwicklung)

> **Vollständige Pipeline-Spec:** [ASSETS_AI.md](ASSETS_AI.md) (EU-konform, kein Hunyuan, KI-Pipeline statt Outsourcing)

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
| 17-18 | 30 Crafting-Items T1-T3 (SPAR3D + TripoSG Batch) — T4 folgt in Woche 27-28 (insgesamt 33 Rezepte) |
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

**Phase D: Integration & Polish (Woche 39-46)**

Während Code-Phase 6 (Polish):
- Unity-Import aller Assets (Addressables-Groups)
- Mastering Audio (−16 LUFS)
- Lizenz-Archiv (`F:\AI\Licenses\handwerkerimperium_unity\`)
- Asset-Metadata-JSON pro Asset

**Phase E: Content-Complete-Assets (Woche 53-66, parallel zu Code-Phase 8)**

Finalisierung für 1:1-Vollständigkeit — ersetzt alle 2D-Beta-Platzhalter durch finale 3D-Assets:
- Restliche City-Tiles bis 80, Specialization-Skins, Affinity-Props vollständig
- Story-Kapitel-Hintergründe + Saison-Visuals (4 Saisons)
- Worker-Mood-Texturen über alle 10 Tiers + 6 Hauttöne vollständig
- Audio: vollständige Meister-Hans-Voice-Bank (alle 60 Story-Kapitel, 6 Sprachen)
- Final-QA gegen Vollständigkeits-Matrix

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
| 29-38 | Cloud-Functions-Deployment + Live-Ops-Breite | Lokaler Emulator, dann Deploy; Live-Ops modular pro Woche |
| 39-46 | Performance auf Low-End | Quality-Settings-Tiers, Mobile-Profil |
| 47-52 | Bug-Berg in Beta | Buffer einplanen, früher Beta-Start |
| 53-72 | 1:1-Parität unvollständig | Vollständigkeits-Matrix als harte Gate vor 1.0; Inhalts-Backlog priorisiert |
| Gesamt | Scope vs. 1-Entwickler-Kapazität | Beta-Stufung (W52 funktional, W72 inhaltlich vollständig); kein System gestrichen, nur Reihenfolge/Polish gestuft |

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

### Meilenstein 4: Single-Player funktional vollständig (Ende Woche 28)
- [ ] Forschung (72 Nodes), Prestige + Prestige-Shop (25), Crafting (33 Rezepte), Reputation + Reputation-Shop (5), Equipment, QuickJob, Master-Tools (12), Gebäude (7), Achievements (109) laufen
- [ ] 200+ Tests grün
- [ ] **Go/No-Go-Decision:** Multiplayer + Live-Ops starten?

### Meilenstein 5: Multiplayer + Live-Ops (Ende Woche 38)
- [ ] Gilden komplett (max 20 Mitglieder, Guild-Research 18, Hall 10, Bosse 6, Mega-Projekte 2, War-Season, Chat, Guild-Achievements 33)
- [ ] Daily-Reward + Lucky-Spin + BattlePass + Daily-Challenge + Weekly-Mission + Tournament + Live-/Saison-Events + Monetarisierung
- [ ] Notifications + WelcomeBack + WhatsNew
- [ ] Anti-Cheat aktiv
- [ ] **Go/No-Go-Decision:** Polish-Phase starten?

### Meilenstein 6: Beta-Ready (Ende Woche 46)
- [ ] Alle 10 Mini-Game-Renderer als 3D fertig (deckt 13 Enum-Typen ab)
- [ ] Audio + Shader + Post-FX + FTUE (10 Schritte) + Story-Gerüst (60 Kapitel)
- [ ] Lokalisierung 6 Sprachen
- [ ] **Go/No-Go-Decision:** Closed Beta starten?

### Meilenstein 7: Funktional vollständige Closed Beta (Ende Woche 52)
- [ ] Alle Systeme aus DESIGN.md spielbar (ggf. mit 2D-Platzhaltern + reduziertem Inhalt)
- [ ] Crashlytics-Rate <0.5%
- [ ] FPS-Median 60 auf Mid-Tier
- [ ] **Go/No-Go-Decision:** Content-Complete-Phase starten?

### Meilenstein 8: 1.0 Vollständig (Ende Woche 72)
- [ ] Vollständigkeits-Matrix zu 100% abgehakt (1:1-Parität zur Avalonia-Version)
- [ ] Voller Inhalt: 60 Story-Kapitel, 109+33 Achievements, finale 3D-Assets statt Platzhalter
- [ ] Balancing-Werte + Formeln numerisch gegen Original (ORIGINAL_WERTE.md) verifiziert
- [ ] Production-Launch v1.0.0

---

## Post-Launch-Roadmap (Phase 2 — über das Original hinaus)

> **Abgrenzung (DESIGN.md § 35):** ALLE Original-Systeme — inkl. Ascension, Mega-Projekte, War-Season,
> alle 6 Bosse, alle 10 Hall-Gebäude, 4 Saison-Events + 8 Live-Event-Templates, 12 Master-Tools — sind
> Teil des **1:1-Ports** und in den Phasen 1-8 (bis Woche 72) eingeplant, NICHT hier. Post-Launch enthält
> ausschließlich Erweiterungen, die das Original NICHT kennt (neue Mechanik/Balancing) oder reine
> Plattform-/Präsentations-Aufsätze. Diese werden erst nach der Cutover-Entscheidung erwogen.

**(A) Präsentation & Plattform (kein Mechanik-Eingriff):**

| Thema | Inhalt |
|-------|--------|
| iOS-Launch | Entscheidung nach Beta-Erfolg (DAU > 5000), Apple Developer Account, Cross-Platform via Unity |
| Day/Night-Cycle | Rein visuell (DESIGN.md § 33.2) |
| Live-Wetter | API-gestützte Wetter-Visuals; saisonale Income-Multiplikatoren bleiben unverändert |
| Worker-Tier-Voice-Lines | Separate ElevenLabs-Voices pro Worker-Tier (kann ins MVP, wenn Audio-Budget reicht) |
| Replay-Highlights | Prestige-Cinematic-Clips zum Teilen |
| Cosmetic-DLC | Workshop-Skins, Worker-Outfits — rein kosmetisch, keine Gameplay-Boni |
| Cross-Save Avalonia ↔ Unity | Nur falls Hard-Cutover entschieden wird (Save-Schema-Brücke) |

**(B) Inhaltliche Erweiterungen (ändern Mechanik/Balancing — strikt Phase 2):**

| Thema | Inhalt |
|-------|--------|
| Live-PvP via Photon Fusion | Echtzeit-Klan-Matches 5v5 — neue Mechanik (Original kennt nur async Gilden-Inhalte) |
| Kriegssaison-Ligen erweitern | Über die Original-Kriegssaison hinaus |
| Mehr Saisonale/Live-Events | Über die 4 Saison-Events + 8 Live-Templates des Originals hinaus |
| Ascension-Perks erweitern | Original hat 6 Perks × 3 Level (54 AP); Erweiterung = neues Balancing |
| Master-Tools erweitern | Original hat 12 Artefakte; Erweiterung auf 18+ = neuer Content |

### Phase 2 Major-Features

**Photon Fusion Live-PvP (nach 1.0):**
- Stack: Photon Fusion 2.x für Netcode + Photon Cloud
- Use-Case: Echtzeit-Klan-Match 5v5 (zusätzlich zu den async Gilden-Inhalten des Originals)
- Match-Format: Beide Klans bauen gegeneinander, höchster Output gewinnt
- Tech-Architektur: Neue Scene `LivePvP.unity` + dedicated PhotonLifetimeScope
- Server-Region: europe-west1 (Photon Cloud)
- Anti-Cheat: Server-Authoritative State (statt Client-HMAC)
- Geschätzter Aufwand: 8-10 Wochen (1 Entwickler)
- **Voraussetzung:** Beta-Spielerbasis > 1000 MAU, sonst nicht wirtschaftlich

**Worker-Tier-Voice-Lines (nach 1.0):**
- Separate ElevenLabs-Standard-Voice pro Worker-Tier (10 Tiers × 6 Lines × 6 Sprachen = 360 Lines, KEIN Voice-Cloning)
- Voices aus Library wählen (z.B. "junge Stimme" für F-Tier, "veteran" für SSS)
- Re-Generation jederzeit möglich

**iOS-Launch (nach 1.0):**
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
| 46 | Build-Pipeline-Hardening |
| 51 | Pre-Release-Checker (analog Avalonia AppChecker) |

---

## Team-Allocation (1 Entwickler + KI-Pipeline)

| Rolle | Auslastung |
|-------|-----------|
| **Entwickler (Robert)** | Vollzeit, alle Code-Bereiche |
| **KI-Pipeline-Operator (Robert)** | Wochen 4-44 (+ Content-Complete 53-66), je 5-10h/Woche (parallel zur Code-Arbeit). ComfyUI-Workflows + Blender-Cleanup + Mixamo + ElevenLabs-Standard-Voice (KEIN Cloning) |
| **Hand-Polish (optional, Robert)** | Wochen 28-32, für Hero-Assets in Substance Painter |
| **Translator-Review (Native-Speaker)** | Wochen 7 + 49, einmalige Lieferung (4-8h/Sprache à 50 €) |
| **Beta-Tester (Community)** | Wochen 47-72, 5-100+ Personen |

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
1. **Liegen-gelassene Tasks dokumentieren** (z.B. Asset-Polish, Inhalts-Vervollständigung)
2. **Phase-Kritische Tasks** priorisieren (funktionale Vollständigkeit > Visualisierung > Inhalts-Polish)
3. **Beta-Stufung nutzen** — Inhalt/Polish darf in Phase 8 (Content-Complete, W53-72) rutschen, aber **kein System wird gestrichen**; die 1:1-Vollständigkeit bleibt verbindliches 1.0-Gate (Vollständigkeits-Matrix)
4. **Scope-Verschiebung statt Scope-Cut:** 2D-Platzhalter für 3D-Assets als Brücke bis zur finalen KI-Asset-Lieferung — Mechanik bleibt vollständig
5. **KI-Pipeline-Backup:** Falls TRELLIS 2 für komplexe Werkstatt-Architektur versagt → Cloud-Fallback Rodin Gen-2.5 (50 € Sub-Top-Up reicht für ~50-100 Hero-Assets)

---

## Links

- [PLAN.md](PLAN.md) — Strategischer Plan
- [CLAUDE.md](CLAUDE.md) — Conventions
- [ARCHITECTURE.md](ARCHITECTURE.md) — Tech-Details
- [DESIGN.md](DESIGN.md) — Game Design Document
