# ArcaneKingdom — Software-Architektur

> Engine: Unity 6 (6000.4.x) | Sprache: C# (.NET Standard 2.1) | Render-Pipeline: URP 17.x
> Geltungsbereich: alle Code-Entscheidungen, Folder-Layout, Konventionen.

Diese Doku begleitet [DESIGN.md](DESIGN.md). DESIGN.md beschreibt **was**, ARCHITECTURE.md **wie**.

---

## Inhaltsverzeichnis

1. Tech-Stack-Entscheidungen
2. Folder-Layout
3. Assembly Definitions (asmdef)
4. Dependency Injection mit VContainer
5. Daten-Architektur (ScriptableObjects, Save, Cloud)
6. Networking-Strategie (Firebase + Photon)
7. Scene-Architektur
8. UI-Architektur (UI Toolkit vs UGUI)
9. Asset-Pipeline (Addressables)
10. Game-Loop & State-Machine
11. Conventions (Naming, Code-Style, Asserts)
12. Build & Deployment

---

## 1. Tech-Stack-Entscheidungen

| Bereich | Entscheidung | Begruendung |
|---------|--------------|-------------|
| Engine | Unity 6 (6000.4.x) | Aktueller LTS-Stand, UI Toolkit built-in, URP 17 default, kostenlos < 200k USD Umsatz/Jahr |
| Render-Pipeline | URP (Universal RP) | 2D/UI-fokussiert, Shader Graph, gute Performance auf mid-tier Android |
| Input | New Input System | Action-basierte Architektur, Multi-Touch nativ, leichter testbar |
| UI-Framework | UI Toolkit (UIElements) fuer statische UIs, UGUI fuer animations-heavy Kampf-UI | UI Toolkit ist deklarativ + USS-stylebar; UGUI bleibt fuer DOTween-haeufige UIs vorteilhaft |
| DI | VContainer | AOT-kompatibel (IL2CPP), schnell, kleine Code-Base, vertraut |
| Async | UniTask | Allokations-arm im Vergleich zu Task<T>, deterministische Frame-Sync |
| Reactive | UniRx | Event-Streams fuer UI-Aktualisierungen, Drop-In wenn benoetigt |
| JSON | Newtonsoft.Json (Unity-Variante) | Robust, sqlite-net-aehnliche Erfahrung, Backend-kompatibel |
| Localization | com.unity.localization | First-Party, String-Tables + Asset-Tables, baut auf Addressables auf |
| Persistenz lokal | PlayerPrefs (Settings), JSON-File (Game-State Backup), Firebase Realtime DB (Source of Truth) | Vermeidet SQLite-Overhead (Unity-Plugin); JSON-File als Last-Known-Good Fallback |
| Animation | DOTween + Unity Timeline + Visual Effect Graph | DOTween fuer Tweens, Timeline fuer Sequenzen, VFX-Graph fuer FX-Lichtshow |
| Audio | Unity Audio + Resonance Audio (optional 3D), Wwise nur wenn Budget erlaubt | Wwise lohnt sich erst bei >100 Soundvarianten |
| Backend | Firebase (Auth, Realtime DB, Cloud Messaging, Remote Config, Analytics, Crashlytics) | One-Stop-Backend, gratis-Tier reicht fuer Beta |
| Realtime | Photon Realtime + Photon Chat | Dieb-HP-Sync, Klan-Match-Match-Hub, Live-Chat; Photon Fusion fuer Live-PvP in Phase 2 |
| IAP | Unity IAP + Google Play Billing v6 | Standard, Unity-managed |
| Ads | Google AdMob (optional, evtl. nicht in Phase 1) | TCG-Spieler reagieren empfindlich auf Ads — eher streichen |
| Build-Tooling | Unity Cloud Build oder GitHub Actions + Unity Builder Action | GitHub Actions fuer Reproduzierbarkeit, Cloud Build als Fallback |
| AOT-Target | IL2CPP, ARM64 only (Phase 1), API 24+ (Android 7+) | Play-Store-Vorgabe seit 2019; ARM32 erst Phase 2 fuer Asia |

---

## 2. Folder-Layout

```
Unity/
├── Assets/
│   ├── _Project/                        <- Project-Code, Unterstrich sortiert zuoberst
│   │   ├── Scripts/                     <- C#-Code, gegliedert nach Feature
│   │   │   ├── Bootstrap/               <- App-Startup, DI-Container-Setup
│   │   │   ├── Core/                    <- Universelle Helper, Logger, Extensions
│   │   │   ├── Services/                <- Cross-Cutting Services (Auth, Save, Network)
│   │   │   ├── Cards/                   <- Card-Modelle, Card-View, CardDefinition
│   │   │   ├── Runes/                   <- Rune-Modelle, Rune-View
│   │   │   ├── Player/                  <- Player-Datenmodell, Inventory
│   │   │   ├── World/                   <- Welt-Karte, Welten, Nodes
│   │   │   ├── Battle/                  <- Kampf-Engine, Battle-Controller, KI
│   │   │   ├── Hub/                     <- Hub-Welt-Logik, Navigation
│   │   │   ├── Arena/                   <- Arena-PvP-Logik
│   │   │   ├── Guild/                   <- Gilden-System
│   │   │   ├── Thief/                   <- Dieb-Event-Logik
│   │   │   ├── Chat/                    <- Chat-System
│   │   │   ├── Economy/                 <- Waehrungen, Quests, Merit
│   │   │   └── UI/                      <- UI-Bindings, View-Models, Custom Controls
│   │   ├── ScriptableObjects/           <- Konfigurations-Daten
│   │   │   ├── Cards/                   <- 150x CardDefinition.asset
│   │   │   ├── Runes/                   <- ca. 20x RuneDefinition.asset
│   │   │   ├── Worlds/                  <- 9x WorldDefinition.asset
│   │   │   └── Config/                  <- Game-Constants, BalancingConfig
│   │   ├── Scenes/                      <- Unity-Scenes
│   │   │   ├── Boot/Boot.unity          <- Bootstrap-Scene (DI, Splash)
│   │   │   ├── Hub/Hub.unity            <- Haupt-Hub
│   │   │   ├── Battle/Battle.unity      <- Kampf-Scene (additive geladen)
│   │   │   ├── Arena/Arena.unity        <- Arena-Listenansicht
│   │   │   ├── Guild/Guild.unity        <- Gilden-Verwaltung
│   │   │   └── GuildWorld/GuildWorld.unity <- Gilden-Weltkarte
│   │   ├── Prefabs/                     <- Wiederverwendbare Prefabs
│   │   │   ├── Cards/                   <- CardView-Prefab, FloatingDamage
│   │   │   ├── UI/                      <- Modals, Buttons, Tooltips
│   │   │   └── FX/                      <- Particle-Prefabs
│   │   ├── Art/                         <- Sprites, Texturen, Materialien, Shader
│   │   │   ├── Cards/                   <- Karten-Artworks (Addressable)
│   │   │   ├── Worlds/                  <- Welt-Hintergruende
│   │   │   ├── UI/                      <- UI-Sprites, Icons
│   │   │   └── Shaders/                 <- URP-Shader (Glow, Holo, Dissolve)
│   │   ├── Audio/                       <- BGM, SFX, Voicelines
│   │   ├── Addressables/                <- Addressables-Groups + .bin
│   │   └── Resources/                   <- Last-Resort fuer Bootstrap-Assets (Splash-Logo, Default-Avatar)
│   ├── ThirdParty/                      <- Externe Assets (Plugins, Asset-Store-Imports)
│   │   ├── DOTween/
│   │   ├── VContainer/
│   │   ├── UniTask/
│   │   ├── UniRx/
│   │   └── Firebase/                    <- Firebase Unity SDK
│   └── StreamingAssets/                 <- Addressables-Build-Output, kein Versions-Tracking
├── Packages/
│   └── manifest.json                    <- Unity Package Manager
├── ProjectSettings/                     <- Unity Project Settings (Versions-Tracking!)
├── UserSettings/                        <- NICHT in Git
├── Library/                             <- NICHT in Git (generiert)
├── Temp/                                <- NICHT in Git (generiert)
└── Logs/                                <- NICHT in Git (generiert)
```

### Begruendung "_Project"

Der Underscore sorgt fuer alphabetische Sortierung oben — eigenes Projekt-Code sofort sichtbar,
ThirdParty / Resources rutschen nach unten. Standard in Unity-Studios (z.B. Schell Games-Style).

---

## 3. Assembly Definitions (asmdef)

Ziel: **schnelle Iterationszeit** durch klare Compilation-Units. Keine zirkulaeren Referenzen.

```
ArcaneKingdom.Core             <- _Project/Scripts/Core, Services (Auth, Save)
   └── ArcaneKingdom.Domain    <- _Project/Scripts/Cards, Runes, Player, Economy
        └── ArcaneKingdom.Game <- _Project/Scripts/World, Battle, Hub, Arena, Guild, Thief, Chat
             └── ArcaneKingdom.UI <- _Project/Scripts/UI
                  └── ArcaneKingdom.Bootstrap <- _Project/Scripts/Bootstrap
```

- **Core:** Logger, Extension-Methods, Result-Type, GameClock
- **Domain:** Datentypen, ScriptableObject-Definitionen, reine Logik (testbar ohne Unity-API)
- **Game:** Feature-Code mit Unity-Abhaengigkeit (MonoBehaviours, Coroutines)
- **UI:** Views, Controller, Bindings — abhaengig von Game und Domain
- **Bootstrap:** App-Einstieg, LifetimeScope-Setup

**Test-Assemblies (separat):**
- `ArcaneKingdom.Domain.Tests` (NUnit, ohne Unity-API)
- `ArcaneKingdom.Game.PlayMode.Tests` (Unity Test Framework, PlayMode)

---

## 4. Dependency Injection mit VContainer

### 4.1 LifetimeScopes

**Aktuelle Implementierung (Stand v5.3):** Alle Services sind im `RootLifetimeScope`
als Singleton registriert (siehe `GameInstaller.RegisterServices`). Eine spaetere
Aufteilung in Sub-Scopes pro Scene ist vorgesehen sobald die Scenes komplette
ViewModels haben.

```
RootLifetimeScope (Boot-Scene, DontDestroyOnLoad)
│
├─ Cross-Cutting (Interface-basiert)
│  ├─ IAuthService                  → FirebaseAuthService (Stub)
│  ├─ ISaveService<PlayerSave>      → FirebaseSaveService (lokaler JSON-Fallback)
│  ├─ IAnalyticsService             → FirebaseAnalyticsService (Stub)
│  ├─ ISceneLoaderService           → AdditiveSceneLoaderService
│  ├─ IAudioService                 → UnityAudioService (MonoBehaviour, Boot-Scene-GameObject)
│  ├─ INotificationService          → NotificationService (Local-Notifications-Stub)
│  └─ IIapService                   → UnityIapService (Stub)
│
├─ Feature-Controller (Singletons)
│  ├─ LoginController                (VContainer EntryPoint)
│  ├─ HubController                  (Energie-Regen-Tick, Navigation)
│  ├─ BattleController                (Welt-Kampf-Orchestrierung)
│  ├─ ArenaController                 (Async-PvP + Glicko-Rang)
│  ├─ GuildController                 (Create/Join/Leave/Donate)
│  ├─ ThiefController                 (Angriff + Reward-Tier)
│  ├─ ChatController                  (Length+Cooldown+Profanity)
│  └─ ShopController                  (Pack-Kauf, Energie-Direktkauf)
│
└─ Services (Singletons)
   ├─ ProgressionService              (EXP → Level-Up + Belohnungen)
   ├─ HeroService                     (Helden-Auswahl)
   ├─ QuestService                    (Event-Hooks + Quest-Progress)
   ├─ DailyRewardService              (7-Tage-Login-Zyklus)
   ├─ SeasonResetService              (Daily/Weekly/Saison-Reset)
   ├─ ReplayService                   (Snapshot-Aufzeichnung)
   ├─ DeckBuilderService              (Suggest-Deck)
   ├─ CollectionService               (Material-Sets, Exchange)
   ├─ TutorialService                 (8-Schritt-FTUE)
   └─ CodexService                    (Karten/Helden/Welten-Lexikon)
```

**Sub-Scopes (geplant, MVP-Phase Monat 4+):**

```
HubLifetimeScope (Hub-Scene)
└── HubUIBinder (View-Layer)

BattleLifetimeScope (Battle-Scene, transient)
├── BattleEngine-Instanz (pro Kampf neu mit Seed)
├── BattleAI-Instanz
└── BattleUIBinder

ArenaLifetimeScope, GuildLifetimeScope, GuildWorldLifetimeScope, ... analog
```

### 4.2 Service-Lifetimes

- **Singleton (Root):** Stateful Services, die ueber Scenes hinweg leben
- **Scope (Sub-Scope):** Scene-spezifische Controller, die mit Scene-Wechsel resetten
- **Transient:** ViewModels (pro Modal-Aufruf neu)

### 4.3 Cross-Cutting-Concerns

| Konzern | Loesung |
|---------|---------|
| Logging | `Logger.Log(tag, msg)` — kann sinkbasiert (Console, Sentry, Firebase) konfiguriert werden |
| Telemetrie | `IAnalyticsService.Track(eventName, props)` — Firebase + lokales Caching bei Offline |
| Lokalisierung | `[Localized]`-Attribute auf TMP-Felder, ueber LocalizationService gefuettert |
| Error-Handling | `Result<T>`-Type fuer fehlbare Operationen, kein Exception-Driven-Flow im Game-Loop |

---

## 5. Daten-Architektur

### 5.1 ScriptableObjects (statische Spielkonfiguration)

Inhalt:
- `CardDefinition` (150x) — alle Karten-Stammdaten
- `RuneDefinition` (20x) — Runen-Stammdaten
- `WorldDefinition` (9x) — Welten-Daten (Name, Element, Hintergrund, Nodes)
- `NodeDefinition` (90x) — Welt-Nodes (Gegner-Deck, Belohnungen)
- `BalancingConfig` (1x) — globale Konstanten (Energie-Cap, EXP-Formel, Drop-Rates)
- `LevelCurve` (1x) — Spieler-Level-EXP-Tabelle
- `CardUpgradeCurve` (1x) — Karten-Level-Upgrade-Kosten

**Pattern:**
- ScriptableObjects sind **read-only zur Laufzeit** — Mutationen IMMER auf Runtime-Instanzen
- Editor-Tools: Card-Editor Custom Window fuer Massen-Editing

### 5.2 Save-Daten (Cloud Source of Truth)

```
Firebase Realtime Database Struktur:
/players/{uid}/
   ├── profile { displayName, server, level, exp }
   ├── currencies { gold, diamonds, energy, energyBonus, ... }
   ├── inventory
   │     ├── cards { cardId: { instanceId, level, exp }, ... }
   │     └── runes { runeId: { instanceId, level }, ... }
   ├── decks { 0: { cards: [...], runes: [...] }, 1: {...}, ... }
   ├── worldProgress { worldId: { nodeId: stars, ... }, ... }
   ├── arena { rank, points, season, last5Battles: [...] }
   ├── achievements { ... }
   └── lastSeenServerTime  (ServerValue.TIMESTAMP fuer Anti-Cheat)

/guilds/{guildId}/  (Firestore)
   ├── info { name, tag, slogan, level, leaderId }
   ├── members { uid: { joinedAt, contribution, role, lastSeen }, ... }
   ├── territories { territoryId: { capturedAt }, ... }
   └── tech { techId: { level, unlockedAt }, ... }

/territories/{territoryId}/  (Firestore)
   ├── info { name, rarity, defaultBonus }
   ├── currentOwner { guildId, capturedAt }
   ├── activeBids { guildId: { amount, bidAt }, ... }
   └── matchHistory { ... }
```

### 5.3 Local Save (Fallback)

- **JSON-Backup** der Cloud-Daten im persistentDataPath
- **Conflict-Resolution:** Server wins, local backup nur fuer Offline-Continue
- **Save-Trigger:** Nach jedem Kampf-Ende, Karten-Drop, Deck-Aenderung, Stunden-Tick
- **Migration:** Versions-Feld im JSON, Migration-Pipeline pro Aenderung

### 5.4 Anti-Cheat-Massnahmen

- **Cloud Functions** validieren kritische Operationen (Karten-Drop, Saison-Belohnung, Klan-Match-Result)
- **Server-Timestamps** fuer Energie-Regen (kein Client-Time)
- **Replay-Determinismus** im BattleEngine (Seed-basiert, Replay reproduzierbar)
- **Rate-Limits** auf Firebase Security Rules (max. N Schreibvorgaenge pro Sekunde pro Spieler)

---

## 6. Networking-Strategie

### 6.1 Datenfluss-Diagramm

```
   ┌──────────┐
   │  Client  │
   └────┬─────┘
        │
        │  Lokale Mutationen (Optimistic Update)
        ▼
   ┌──────────────────────────────────────────┐
   │   Local State (Cache)                    │
   └────┬──────────────────────┬──────────────┘
        │                      │
  Read (low-latency)     Write (mit Validation)
        ▼                      ▼
   ┌─────────────┐       ┌──────────────────┐
   │ Firebase    │       │ Cloud Functions  │
   │ Realtime DB │       │ (Validation,     │
   │ (Source of  │◄──────┤  Anti-Cheat)     │
   │  Truth)     │       └──────────────────┘
   └─────────────┘
        │
        │ Subscribe (z.B. Dieb-HP)
        ▼
   ┌──────────┐
   │  Client  │
   └──────────┘
```

### 6.2 Photon-Verwendung

| Use Case | Photon-Komponente |
|----------|-------------------|
| Welt-Chat | Photon Chat (Room per Server) |
| Gilden-Chat | Photon Chat (Room per Guild) |
| Privat-Chat | Photon Chat (Direct Message) |
| Dieb-HP-Sync | Photon Realtime (Room per Dieb) |
| Klan-Match-Match-Hub | Photon Realtime (Room per Match) |
| Live-PvP Phase 2 | Photon Fusion (deterministisch) |

**Photon-Auth:** Spieler authentifiziert sich mit Firebase-Token, Photon validiert via Custom Authentication Webhook.

### 6.3 Offline-Modus

- PvE-Welten-Kaempfe sind offline spielbar, lokaler State wird beim naechsten Connect synchronisiert
- Belohnungen aus Offline-Kaempfen werden serverseitig validiert (Replay-Hash)
- Arena, Dieb, Chat, Klan-Match brauchen Verbindung — Offline-Banner sichtbar

---

## 7. Scene-Architektur

### 7.1 Scene-Lifecycle

```
Boot.unity (Dauer-Scene, DontDestroyOnLoad)
   ├── RootLifetimeScope (VContainer)
   ├── SplashScreen
   └── SceneLoaderService → laedt Hub.unity additive
        │
        ▼
Hub.unity (additive)
   ├── HubLifetimeScope
   └── HubController
        │ Tap "Welt 1"
        ▼
Battle.unity (additive, ueber Hub)
   ├── BattleLifetimeScope
   ├── BattleController
   └── BattleUI
        │ Kampf-Ende
        ▼
Hub.unity bleibt aktiv → Battle.unity wird unloaded
```

**Boot bleibt immer geladen** als RootScope-Container. Andere Scenes werden additive geladen/entladen.

### 7.2 Scene-Loading

- `SceneLoaderService.LoadAsync<TScope>(sceneName)` — laedt Scene, wartet auf LifetimeScope.Ready
- `SceneLoaderService.UnloadAsync(sceneName)` — entlaedt, gibt VContainer-Scope frei
- Transitions: Cross-Fade ueber persistenten Canvas in Boot-Scene

---

## 8. UI-Architektur

### 8.1 UI Toolkit (UIElements) — fuer statische UIs

- Hub-UI, Deck-Verwaltung, Gilden-Liste, Settings, Inventory, Chat-Listen
- USS fuer Styling, UXML fuer Struktur
- Bindings via `INotifyValueChanged<T>` (eingebaut)
- Vorteil: Web-aehnliches Layout, schneller iterierbar

### 8.2 UGUI — fuer animations-haeufige UIs

- Kampf-UI (Drag-and-Drop Karten, Floating Damage, Mana-Orbs)
- Tutorial-Overlays mit komplexen Tween-Sequenzen
- Card-Reveal-Animationen (Pack-Oeffnen)
- Vorteil: DOTween + RectTransform-Animation flexibler

### 8.3 MVVM-Light-Pattern

```
View (UXML/UGUI)
   ├── ViewBinder (MonoBehaviour, holt Refs, registriert UI-Events)
   └── ViewModel (POCO, ObservableProperty<T>)
        ├── Commands (RelayCommand)
        └── Services (per VContainer injected)
```

- ViewModels sind **Unity-frei** und damit unit-testbar
- ViewBinder ist der Adapter zur Unity-UI

### 8.4 Tooltip & Modal-System

- Zentraler `ModalService.Show<TModalView>(TViewModel)` injiziert per VContainer
- Modal-Stack (max. 3 tief): Settings → Confirm-Dialog
- Back-Button (Android Hardware-Back) schliesst oberstes Modal

---

## 9. Asset-Pipeline (Addressables)

### 9.1 Addressable Groups

| Group | Inhalt | Loading-Strategy |
|-------|--------|------------------|
| `Bootstrap` | Splash-Logo, Default-Font | Sync, im Build enthalten |
| `Cards.Common` | Common-Karten-Artworks (40 Sprites) | Lazy, Cache-LRU |
| `Cards.Uncommon` | Ungewoehnliche Karten (35) | Lazy |
| `Cards.Rare` | Selten (30) | Lazy |
| `Cards.Epic` | Epic (25) | Pre-Load wenn freigeschaltet |
| `Cards.Legendary` | Legendaer (20) | Pre-Load + High-Res |
| `Worlds.{N}` | Pro Welt: Hintergrund + Musik | Pre-Load bei Welt-Eintritt |
| `FX.Battle` | Kampf-Partikel-Prefabs | Pre-Load mit Battle.unity |
| `Audio.BGM` | Background-Music-Loops | Streaming |
| `Audio.SFX` | Sound-Effects | Pre-Load relevant pro Scene |

### 9.2 Remote vs. Local Catalog

- **Phase 1:** Lokal (alles im AAB), max. 150 MB
- **Phase 2:** Remote-Catalog ueber CDN (Firebase Storage), Update-Manager im Hub
  - Pflicht-Update bei Major-Version
  - Optional-Update bei Saison-Content (Karten-Skins, neue Welten)

### 9.3 Cleanup-Strategie

- LRU-Cache: max. 50 Karten-Artworks gleichzeitig in Memory
- `Addressables.Release` nach Scene-Unload
- Memory-Watcher: bei < 100 MB free → aggressives Unload

---

## 10. Game-Loop & State-Machine

### 10.1 Game-State (top-level)

```
                  ┌──────────┐
                  │   Boot   │
                  └────┬─────┘
                       │
                  ┌────▼─────┐
                  │  Login   │
                  └────┬─────┘
                       │
                  ┌────▼─────┐
                  │   Hub    │◄──────────┐
                  └┬──┬──┬──┬┘           │
                   │  │  │  │            │
                   ▼  ▼  ▼  ▼            │
                Battle Arena Guild Other ─┘
```

State-Wechsel via `IGameStateMachine.Transition<TState>()`.

### 10.2 Battle-State-Machine (intra-state)

```
   ┌─────────────┐
   │ Setup       │  (Decks shuffle, Mana = 3, Hand 4 Karten)
   └──────┬──────┘
          │
   ┌──────▼──────┐
   │ PlayerTurn  │◄─────┐
   └──────┬──────┘      │
          │             │ Auto/Manual
   ┌──────▼──────┐      │
   │ EnemyTurn   │──────┘
   └──────┬──────┘
          │
   ┌──────▼──────┐
   │ TurnEnd     │  (Rundenwarten--, Karten ATK auflösen)
   └──────┬──────┘
          │
          ▼ Check VictoryCondition
   ┌─────────────┐
   │ Settlement  │  (Belohnungen, Save, Ergebnis)
   └─────────────┘
```

BattleEngine ist **deterministisch** (seed-basiert), damit Replays funktionieren.

### 10.3 Tick-System

- Hub-Tick: Energie-Regeneration, Dieb-Spawn-Check, Quest-Progress
- Battle-Tick: 60fps Update fuer Animationen, deterministische Logik in Discrete-Step (kein dt-basierter Schaden)

---

## 11. Conventions

### 11.1 Naming

| Element | Convention | Beispiel |
|---------|-----------|----------|
| Namespace | `ArcaneKingdom.{Module}` | `ArcaneKingdom.Battle` |
| Class | PascalCase | `BattleController`, `CardDefinition` |
| Interface | `I`-Prefix + PascalCase | `IBattleEngine`, `ISaveService` |
| Method | PascalCase | `PlayCard(CardInstance card)` |
| Field (private) | `_camelCase` | `private int _currentMana` |
| Property | PascalCase | `public int CurrentMana { get; }` |
| Constant | `UPPER_SNAKE` | `private const int MAX_HAND_SIZE = 5` |
| Event | `On{Verb}{Past}` | `OnCardPlayed`, `OnBattleEnded` |
| ScriptableObject Asset | PascalCase + `.asset` | `Card_Drachenherrscher.asset` |
| Scene | PascalCase | `Hub.unity`, `Battle.unity` |
| Prefab | PascalCase | `CardView_Prefab` |
| asmdef | `ArcaneKingdom.{Module}` | `ArcaneKingdom.Game.asmdef` |

### 11.2 Code-Style

- **Modernes C#** (record types fuer DTOs, expression-bodied members, switch expressions)
- **Nullable Reference Types** aktiv (`<Nullable>enable</Nullable>` im csproj)
- **`var`** wenn Typ aus Kontext klar, sonst expliziter Typ
- **Async-Konventionen:** `*Async`-Suffix, `CancellationToken` als letzter Parameter
- **UniTask** statt Task<T>, ausser bei reinen Library-Calls
- **Kommentare auf Deutsch** (siehe globale Conventions)
- **Keine TODOs ohne Issue-Verweis** (`// TODO(#42): ...`)

### 11.3 Asserts und Defensive Programming

- `UnityEngine.Assertions.Assert` fuer Editor/Development-Builds
- Production: `Result<T>`-Type fuer fehlbare Operationen, kein silent-fail
- Logging mit Kontext-Tags: `Logger.Log("[Battle]", "Card played: ...")`

### 11.4 Test-Conventions

- Tests in `_Project/Tests/{Domain,Game}/`
- Naming: `{Subject}Tests.cs`, Test-Methoden `MethodName_Scenario_ExpectedResult`
- Mock-Framework: NSubstitute
- Coverage-Ziel: Domain >= 80 %, Game-Logik >= 60 %, UI nicht Pflicht

---

## 12. Build & Deployment

### 12.1 Build-Pipeline (CI/CD)

```
GitHub Actions Workflow (.github/workflows/unity-android.yml):
1. Checkout
2. Cache Library/
3. Activate Unity Pro (License via Secret)
4. Build Android AAB (Release mode, signed)
5. Upload Artifact (AAB + Mapping File)
6. (Optional) Upload to Google Play Internal Track via Fastlane
```

### 12.2 Versionierung

- **MAJOR.MINOR.PATCH** (z.B. 1.0.0, 1.1.0, 1.1.5)
- **bundleVersionCode** (int) inkrementiert automatisch per CI
- **Versions-Tags** im Repo: `arcanekingdom-v1.0.0`

### 12.3 Signing

- **Keystore:** `F:\Meine_Apps_Ava\Releases\meineapps.keystore` (gemeinsamer Keystore aller Apps)
- **Alias:** `meineapps`
- **Passwort:** `MeineApps2025` (in Directory.Build.targets oder CI-Secret)
- **Package-ID:** `com.meineapps.arcanekingdom` (vorlaeufig, finalisieren vor Launch)

### 12.4 Release-Tracks

| Track | Zielgruppe | Verteilung |
|-------|-----------|------------|
| Internal | Dev-Team | Direkt, jeder Build |
| Closed Alpha | 50 Beta-Tester | Wochenweise |
| Closed Beta | 500 Tester (Monat 22-23) | 2 Builds/Woche |
| Open Beta | Region: SEA (Monat 23-24) | Bi-weekly |
| Production | Global | Monatlich nach Launch |

---

## Naechste Schritte (Konzept-Phase Monat 1-2)

1. **DESIGN.md TBDs schliessen** (15 offene Punkte)
2. **Karten-Set v1 designen:** 30 Start-Karten (8 Common, 8 Uncommon, 6 Rare, 5 Epic, 3 Legendaer)
3. **BalancingConfig Pilot-Werte erstellen** (EXP-Kurve, Drop-Rates, Diamant-Preise)
4. **Unity-Projekt initialisieren** (Boot-Scene, RootLifetimeScope, erste Stub-Services)
5. **Card-View-Prefab + CardDefinition-Editor-Tool** als erstes konkretes Werkzeug

---

> Dokument-Ende. Naechste Aktualisierung nach Konzept-Phase oder bei Architektur-Aenderungen.
