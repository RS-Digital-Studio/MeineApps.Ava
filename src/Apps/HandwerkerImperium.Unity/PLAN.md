# HandwerkerImperium-Unity — Designplan v1.0

> **Status:** Konzept (Pre-MVP) — Mai 2026
> **Ziel:** Komplette Neuentwicklung von HandwerkerImperium in **Unity 6 (LTS)** parallel zur bestehenden Avalonia-Version. Tech-Foundation analog ArcaneKingdom. Avalonia-Version bleibt produktiv und wird weiter gepflegt, bis die Unity-Version Feature-Parität erreicht hat.
> **Codename:** *HWI-Unity* (intern), Play-Store-Titel bleibt **HandwerkerImperium**
> **Avalonia-Referenz:** v2.1.1 (~28.000 Zeilen C#, 91 Services, 77 Models, 80 ViewModels, 74 Views). Verbindliche Werte/Mechaniken: [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) + [DESIGN.md](DESIGN.md).
> **Unity-Referenz:** ArcaneKingdom v0.0.2 (Unity 6000.4.8f1, VContainer, UniTask, Firebase, Addressables, URP)

---

## Querverweise

| Bereich | Datei |
|---------|-------|
| Conventions & bekannte Stolperfallen | [CLAUDE.md](CLAUDE.md) |
| Tech-Architektur (URP, Asmdefs, Addressables, LOD) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Game Design Document (alle Werte, Mechaniken, Mega-Projekte) | [DESIGN.md](DESIGN.md) |
| KI-Asset-Pipeline (3D-Meshes + PBR + Audio + Meister-Hans-Voice, EU-konform) | [ASSETS_AI.md](ASSETS_AI.md) |
| Wochenplan & Roadmap | [ROADMAP.md](ROADMAP.md) |

---

## Inhaltsverzeichnis

1. [Vision & strategische Ziele](#1-vision--strategische-ziele)
2. [Tech-Stack](#2-tech-stack)
3. [Projekt-Struktur](#3-projekt-struktur)
4. [Architektur](#4-architektur)
5. [Was wird 1:1 portiert, was umgebaut, was neu](#5-was-wird-11-portiert-was-umgebaut-was-neu)
6. [Spielmechaniken im Detail](#6-spielmechaniken-im-detail)
7. [UI-Konzept](#7-ui-konzept)
8. [Online & Multiplayer](#8-online--multiplayer)
9. [Unity-spezifische Neuerungen (Was Avalonia/SkiaSharp nicht konnte)](#9-unity-spezifische-neuerungen)
10. [Persistenz & Save-System](#10-persistenz--save-system)
11. [Ökonomie & Live-Ops](#11-ökonomie--live-ops)
12. [Tools & Editor-Erweiterungen](#12-tools--editor-erweiterungen)
13. [Test-Strategie](#13-test-strategie)
14. [Build & Deployment](#14-build--deployment)
15. [Roadmap](#15-roadmap)
16. [MVP-Definition](#16-mvp-definition)
17. [Risiken & Migrations-Pfad](#17-risiken--migrations-pfad)

---

## 1. Vision & strategische Ziele

### 1.1 Warum Unity?

Die Avalonia-Version hat 10 fertige Werkstätten, 10 Mini-Games, vollständige Gilden-Features und ist live im Play Store. **Aber**: Es gibt fundamentale Limitationen, die nicht mehr lösbar sind, ohne den Stack zu wechseln:

| Limitation Avalonia | Auswirkung | Unity löst es durch |
|---------------------|------------|---------------------|
| Pseudo-3D in SkiaSharp (RoofTilingRenderer, WorkshopScene) | Mathematische Transformationen statt echtem 3D | Native 3D-Engine, URP, Kameras |
| CPU-Partikel (max 200 Pool-Struct-Cap) | Limitierte GameJuice-Effekte | GPU-Instancing (10.000+ Particles), Particle System |
| Shader hardcoded in C# (SKShader.Create*) | Keine visuellen Iterations-Loops | Shader Graph (Visual), Hot-Reload |
| MainView RenderTimer + Scroll-Pause-Hacks | Komplexe Orchestrierung für 30 fps | Unity-Renderer mit automatischer Canvas-Culling |
| Keine Page-Transitions (CSS-translateY-Hacks) | Tab-Wechsel ohne Übergänge | Animator + DOTween + Coroutines |
| Audio: 3 Platform-Implementationen (Android/NAudio/ffplay) | Plattform-spezifische Bugs | Unity AudioMixer (1 API für alle Plattformen) |
| TextMeshPro fehlt, Custom-Font-Resolver für CJK | Begrenzte Typografie | TextMesh Pro + Font-Assets |
| WorkshopCardHitTester manuell | Komplexe Hit-Test-Logik | InputSystem + EventSystem (Raycaster) |
| ~28k LoC, ~91 DI-Service-Wirings manuell | Boilerplate-heavy | VContainer mit Auto-Wiring (Pattern aus ArcaneKingdom) |
| Build: 3 Projekte (Shared/Android/Desktop) + Linked-Files | Verteilte Plattform-Logik | 1 Unity-Projekt, Build-Targets pro Plattform |
| Konstanten-Compile-Time (GameBalanceConstants 491 Z.) | Live-Balancing nur via RemoteConfig | ScriptableObjects + Remote-Catalog → Hot-Reload |

### 1.2 Was wir BEHALTEN wollen

- **Spielmechanik komplett** (Idle + Mini-Games + Prestige + Gilden) — das funktioniert
- **Balancing-Werte** (GameBalanceConstants — empirisch getunte Kurven)
- **Firebase-Schema** (Gilden, Co-op, Auktionen, MegaProjects) — Save-Format bleibt kompatibel
- **Story-Chapters, Achievement-Texte, Lokalisierung** (6 Sprachen RESX → Unity Localization)
- **PlayerId-System** (stabile UUID, nicht Firebase-UID)
- **HMAC-Signierung, Anti-Cheat-Patterns**

### 1.3 Was wir VERBESSERN wollen

1. **Visueller Sprung um 1-2 Generationen** — 3D-Werkstätten, GPU-Partikel, Shader-Effekte, Post-Processing
2. **Game Feel** — Echte Animationen statt CSS-Hacks, Audio-Ducking, Camera-Shake mit Motion-Blur
3. **Performance auf Budget-Phones** — Unity-Renderer ist auf Mobile optimiert, IL2CPP statt JIT
4. **Iteration-Speed** — Shader Graph + ScriptableObjects + Hot-Reload statt Recompile-Wartezeiten
5. **Mini-Games als Erlebnis** — Aus 2D-SkiaSharp-Renderern werden 3D-Szenen mit Physik/Sound/FX
6. **Tutorial & Onboarding** — Native Highlight-Overlays mit Pulse-Animations, geführte 3D-Kamera-Touren
7. **Cross-Plattform-Audio ohne Workarounds** — 1 API, gleiches Verhalten überall

### 1.4 Was NICHT Ziel ist

- **Kein Reboot der Spielmechanik** — Es ist eine **Re-Implementation**, kein neues Spiel. Alle Mechaniken, Formeln und Balancing-Werte bleiben **1:1 identisch** zum Avalonia-Original (verbindlich: [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) + [DESIGN.md](DESIGN.md)). "Besser/3D" betrifft ausschließlich die Präsentation (Grafik, 3D, Hub, Cinematics, Audio, Input, UI-Tech) — niemals Mechanik oder Balance.
- **Kein MMO** — Bleibt Idle/Async, kein Live-PvP (außer optional Phase 2)
- **Kein Cross-Save** zwischen Avalonia & Unity — beide Versionen werden parallel betrieben, getrennte Save-Slots
- **Kein Re-Launch im Play Store** — Update der bestehenden App im Hauptpaket (`com.meineapps.handwerkerimperium`), Migration in der App via Save-Konverter
- **iOS nicht in Phase 1** — Vorbereitet im Code (Unity ist Cross-Platform), aber Build/Submission später

---

## 2. Tech-Stack

### 2.1 Engine & Sprache

| Komponente | Wahl | Begründung |
|------------|------|------------|
| **Unity-Version** | 6000.4.8f1 (LTS) | Gleiche Version wie ArcaneKingdom → Engine-Patches geteilt |
| **Sprache** | C# 12 (Unity 6 Backend) | Modernes C#, records, pattern matching, file-scoped namespaces |
| **Scripting Backend** | IL2CPP (Release), Mono (Editor) | AOT für Mobile, JIT für Iteration |
| **Architecture** | ARM64 only (Phase 1) | Wie bestehende Avalonia-Version |
| **API-Level** | Android API 24+ (Android 7+) | Wie bestehende Avalonia-Version |
| **Render-Pipeline** | URP 17.0.4 | 2D + 3D fähig, Mobile-optimiert, Shader Graph kompatibel |

### 2.2 Pflicht-Packages (UPM)

| Package | Version | Zweck |
|---------|---------|-------|
| `com.unity.render-pipelines.universal` | 17.0.4 | URP für 2D-UI + 3D-Werkstattszenen |
| `com.unity.inputsystem` | 1.19.0 | Multi-Touch, Gesture-Recognition |
| `com.unity.addressables` | 2.9.1 | Asset-Bundles, Lazy-Loading, Remote Catalog (Phase 2) |
| `com.unity.localization` | 1.5.11 | String Tables (6 Sprachen + CJK), Asset-Lokalisierung |
| `com.unity.mobile.notifications` | 2.4.3 | 8 Push-Trigger (Reminder-System portiert) |
| `com.unity.timeline` | 1.8.12 | Prestige-Cinematic, Tutorial-Sequenzen |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | JSON-Kompatibilität mit bestehendem Save-Format |
| `com.unity.textmeshpro` | (built-in) | Bessere Typografie als uGUI Text |
| `com.unity.test-framework` | 1.5.1 | NUnit Domain-Tests (EditMode + PlayMode) |
| `com.cysharp.unitask` | 2.5.10 | Async ohne GC-Allokation (statt `Task<T>`) |
| `jp.hadashikick.vcontainer` | 1.16.9 | DI-Container (AOT-kompatibel für IL2CPP) |
| `com.demigiant.dotween` | Pro / Free | Tweening (UI, Camera, Effects) |
| `com.firebase.unity.app` | latest | Firebase Core |
| `com.firebase.unity.auth` | latest | Anonymous Auth + Google Sign-In |
| `com.firebase.unity.database` | latest | Realtime DB (gleiche Pfade wie Avalonia) |
| `com.firebase.unity.functions` | latest | Cloud Functions Calls |
| `com.firebase.unity.messaging` | latest | FCM Push-Notifications |
| `com.firebase.unity.remoteconfig` | latest | Live-Balancing |
| `com.firebase.unity.analytics` | latest | Telemetrie |
| `com.firebase.unity.crashlytics` | latest | Error-Tracking |
| `com.google.play.billing` | 6.x | IAP (Premium, Whale-Bundles) |
| `com.google.android.ads.mobile` | latest | AdMob (Banner + Rewarded) |
| `com.google.play.review` | latest | In-App Review |

### 2.3 Optionale Packages

| Package | Wann | Zweck |
|---------|------|-------|
| `com.neuecc.unirx` | Bei Bedarf | Reactive Streams für komplexe Event-Flows |
| `com.photonengine.realtime` | Phase 2 | Live-Chat, optional Live-PvP |
| `com.photonengine.chat` | Phase 2 | Guild-Chat (alternativ zu Firebase) |
| `com.unity.netcode.gameobjects` | Phase 3 | Falls Live-PvP gewünscht |

### 2.4 Tools & Asset-Pipeline

| Tool | Zweck |
|------|-------|
| **Spine 2D** oder **Live2D Cubism** | Worker-Avatare animiert (statt statisches Pixel-Art) |
| **Blender** | 3D-Werkstatt-Modelle (low-poly stylized) |
| **Substance Painter** | Material-Texturing für Werkstätten |
| **Aseprite** | Pixel-Art für Mini-Game-Assets (falls beibehalten) |
| **Audacity / Reaper** | Audio-Bearbeitung |
| **Pyxel Edit** | Icon-Erstellung (für ScriptableObject-Icons) |

---

## 3. Projekt-Struktur

### 3.1 Top-Level

```
src/Apps/HandwerkerImperium.Unity/
├── CLAUDE.md                       # Projekt-Conventions (für Claude Code)
├── PLAN.md                         # Dieser Plan
├── DESIGN.md                       # Game Design Document (folgt)
├── ARCHITECTURE.md                 # Tech-Entscheidungen (folgt)
├── README.md                       # Quickstart (folgt)
├── SETUP.md                        # First-Time-Setup (folgt)
├── ROADMAP.md                      # Detailroadmap (folgt)
│
├── Unity/                          # Unity-Projekt-Root
│   ├── ProjectSettings/
│   ├── Packages/
│   │   └── manifest.json
│   └── Assets/
│       ├── _Project/               # Unser Code (Underscore sortiert nach oben)
│       │   ├── Scripts/            # 7 Assembly Definitions
│       │   │   ├── Bootstrap/
│       │   │   ├── Core/
│       │   │   ├── Domain/
│       │   │   ├── Game/
│       │   │   ├── UI/
│       │   │   ├── Editor/
│       │   │   └── Tests/
│       │   ├── ScriptableObjects/  # Configs als Assets
│       │   │   ├── Workshops/      # 10 WorkshopDefinitions
│       │   │   ├── Workers/        # WorkerTier-Definitions (10 Tiers)
│       │   │   ├── Recipes/        # 33 CraftingRecipes (T1 10 + T2 10 + T3 10 + T4 3)
│       │   │   ├── Research/       # 72 ResearchNodes (Tools 20 + Mgmt 20 + Marketing 20 + Logistics 12)
│       │   │   ├── Achievements/   # 109 AchievementDefinitions (Spieler) + 33 Gilden
│       │   │   ├── Quests/         # Daily/Weekly/Story (60 Story-Kapitel)
│       │   │   ├── Equipment/      # 4 Rarity-Tiers (KEIN Legendary) × 3 Slots
│       │   │   ├── GuildBuildings/ # 10 HallBuildings
│       │   │   ├── Events/         # 4 Live-Event-Templates + 8 Random-Events
│       │   │   ├── BattlePass/     # Season-Definitionen
│       │   │   ├── Localization/   # String Tables
│       │   │   └── Config/         # BalancingConfig, GameSettings
│       │   ├── Scenes/             # Persistente Scenes
│       │   │   ├── Boot.unity      # DontDestroyOnLoad, RootLifetimeScope
│       │   │   ├── Hub.unity       # Haupt-Hub mit 5 Tabs (additive)
│       │   │   ├── MiniGame.unity  # Container für 3D-Mini-Games (additive)
│       │   │   ├── Workshop.unity  # 3D-Werkstatt-Detail-Szene (additive)
│       │   │   ├── Prestige.unity  # Cinematic-Szene (additive)
│       │   │   └── Guild.unity     # Gilden-Hub (additive)
│       │   ├── Prefabs/
│       │   │   ├── UI/             # Buttons, Cards, Dialogs
│       │   │   ├── Workshops/      # 3D-Werkstatt-Prefabs
│       │   │   ├── Workers/        # Worker-Avatar-Prefabs
│       │   │   ├── FX/             # Particle-Systems, Coin-Fly
│       │   │   └── Tutorials/      # Highlight-Pulse-Overlays
│       │   ├── Art/
│       │   │   ├── 3D/             # Werkstatt-Modelle
│       │   │   ├── 2D/             # UI-Icons, Card-Artworks
│       │   │   ├── Spine/          # Animierte Worker-Avatare
│       │   │   ├── Shaders/        # Shader-Graphs
│       │   │   ├── Materials/
│       │   │   └── Textures/
│       │   ├── Audio/
│       │   │   ├── BGM/            # 4 Music-Loops
│       │   │   ├── SFX/            # 82+ Sound-Effects
│       │   │   └── Voice/          # Optional: Worker-Voice-Lines
│       │   ├── Addressables/       # Groups + .bin-Kataloge
│       │   └── Resources/          # Bootstrap-only
│       │
│       ├── ThirdParty/             # DOTween, Firebase, AdMob
│       └── StreamingAssets/        # Migrations-JSON aus Avalonia
│
└── Server/                         # Cloud Functions (TypeScript)
    ├── CloudFunctions/             # Anti-Cheat, Saison-Settle
    ├── DatabaseRules/              # Firebase Security Rules
    └── SERVEROPS.md                # Deployment-Doku
```

### 3.2 Assembly Definitions (7 Module)

Analog ArcaneKingdom, aber für HandwerkerImperium-Domain:

```
HandwerkerImperium.Core         (Logger, Result<T>, Extensions, GameClock)
  └── HandwerkerImperium.Domain         (POCOs, pure C#, testbar ohne Unity)
       └── HandwerkerImperium.Game           (Services, Controllers, MonoBehaviour-Wrapper)
            └── HandwerkerImperium.UI             (Views, ViewBinder, Screen-Bindings)
                 └── HandwerkerImperium.Bootstrap      (DI-Setup, EntryPoints)

Zusätzlich:
- HandwerkerImperium.Editor       (DataImporter, Inspectors, Build-Scripts)
- HandwerkerImperium.Domain.Tests (NUnit, 200+ Tests)
```

**Wichtig:** Domain-Layer **darf keine UnityEngine-Referenzen** haben → 100% unit-testbar mit reinem NUnit-Runner.

### 3.3 Namespace-Konventionen

```
HandwerkerImperium.Core
HandwerkerImperium.Domain.Workshops
HandwerkerImperium.Domain.Workers
HandwerkerImperium.Domain.Orders
HandwerkerImperium.Domain.Research
HandwerkerImperium.Domain.Prestige
HandwerkerImperium.Domain.Guild
HandwerkerImperium.Domain.Crafting
HandwerkerImperium.Domain.Save
HandwerkerImperium.Game.Services
HandwerkerImperium.Game.Controllers
HandwerkerImperium.Game.Platform
HandwerkerImperium.UI.Screens
HandwerkerImperium.UI.Foundation
HandwerkerImperium.Bootstrap
```

---

## 4. Architektur

### 4.1 Layer-Modell

```
┌─────────────────────────────────────────────────────────────┐
│                      Bootstrap Layer                         │
│  RootLifetimeScope + GameInstaller (VContainer)              │
│  → registriert alle Services, startet Boot-Sequenz           │
└──────────────────┬──────────────────────────────────────────┘
                   ▼
┌─────────────────────────────────────────────────────────────┐
│                         UI Layer                             │
│  Screens (UI Toolkit/uGUI) + ViewModels + ViewBinder         │
│  → DataBinding, User-Input, Animationen                      │
└──────────────────┬──────────────────────────────────────────┘
                   ▼
┌─────────────────────────────────────────────────────────────┐
│                        Game Layer                            │
│  Services (Singletons) + Controllers (Feature-orchestriert)  │
│  → Firebase, AdMob, Audio, Scene-Loading, Game-Loop          │
└──────────────────┬──────────────────────────────────────────┘
                   ▼
┌─────────────────────────────────────────────────────────────┐
│                       Domain Layer                           │
│  Pure C#: POCOs, Calculators, Rules, State-Machines          │
│  → IncomeCalculator, OrderGenerator, PrestigeRules, etc.     │
└──────────────────┬──────────────────────────────────────────┘
                   ▼
┌─────────────────────────────────────────────────────────────┐
│                        Core Layer                            │
│  Logger, Result<T>, Extensions, GameClock, EventBus          │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 DI mit VContainer (Pattern aus ArcaneKingdom)

**Lifetime-Scopes:**

- **RootLifetimeScope** (Boot.unity, DontDestroyOnLoad) — alle Singletons
- **HubLifetimeScope** (Hub.unity) — Hub-spezifische Services
- **MiniGameLifetimeScope** (MiniGame.unity) — transient pro Mini-Game-Session
- **WorkshopLifetimeScope** (Workshop.unity) — transient pro Werkstatt-Detail-Ansicht

**Registrierungs-Beispiel:**

```csharp
public class GameInstaller : LifetimeScope
{
    [SerializeField] private BalancingConfig _balancingConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        // Core
        builder.Register<ILogger, UnityLogger>(Lifetime.Singleton);
        builder.Register<IGameClock, RealtimeGameClock>(Lifetime.Singleton);
        builder.Register<IEventBus, EventBus>(Lifetime.Singleton);

        // Persistence
        builder.Register<ISaveService<HwiSave>, FirebaseSaveService<HwiSave>>(Lifetime.Singleton);
        builder.Register<ICloudSaveService, CloudSaveService>(Lifetime.Singleton);
        builder.Register<ISaveMigrator<HwiSave>, HwiSaveMigrator>(Lifetime.Singleton);

        // Platform
        builder.Register<IAuthService, FirebaseAuthService>(Lifetime.Singleton);
        builder.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton);
        builder.Register<IRewardedAdService, AdMobRewardedAdService>(Lifetime.Singleton);
        builder.Register<IPurchaseService, GooglePlayBillingService>(Lifetime.Singleton);
        builder.Register<INotificationService, MobileNotificationService>(Lifetime.Singleton);
        builder.RegisterComponent(audioService).AsImplementedInterfaces();

        // Domain (Calculators, Rules — pure C#)
        builder.Register<IIncomeCalculator, IncomeCalculator>(Lifetime.Singleton);
        builder.Register<IOrderGenerator, OrderGenerator>(Lifetime.Singleton);
        builder.Register<IPrestigeRules, PrestigeRules>(Lifetime.Singleton);
        builder.Register<ICraftingRules, CraftingRules>(Lifetime.Singleton);

        // Game-Services
        builder.Register<GameLoopService>(Lifetime.Singleton);
        builder.Register<GameStateService>(Lifetime.Singleton);
        builder.Register<WorkshopService>(Lifetime.Singleton);
        builder.Register<WorkerService>(Lifetime.Singleton);
        builder.Register<OrderService>(Lifetime.Singleton);
        builder.Register<ResearchService>(Lifetime.Singleton);
        builder.Register<PrestigeService>(Lifetime.Singleton);
        builder.Register<CraftingService>(Lifetime.Singleton);
        builder.Register<WarehouseService>(Lifetime.Singleton);
        builder.Register<MarketService>(Lifetime.Singleton);
        builder.Register<EventService>(Lifetime.Singleton);
        builder.Register<AchievementService>(Lifetime.Singleton);
        builder.Register<DailyChallengeService>(Lifetime.Singleton);
        builder.Register<WeeklyMissionService>(Lifetime.Singleton);
        builder.Register<BattlePassService>(Lifetime.Singleton);
        builder.Register<LiveEventService>(Lifetime.Singleton);
        builder.Register<TutorialService>(Lifetime.Singleton);

        // Guild (Facade-Pattern aus Avalonia übernommen!)
        builder.Register<IGuildFacade, GuildFacade>(Lifetime.Singleton);
        builder.Register<GuildService>(Lifetime.Singleton);
        builder.Register<GuildCoopOrderService>(Lifetime.Singleton);
        builder.Register<WorkerAuctionService>(Lifetime.Singleton);
        builder.Register<GuildBossService>(Lifetime.Singleton);
        builder.Register<GuildHallService>(Lifetime.Singleton);
        builder.Register<GuildWarSeasonService>(Lifetime.Singleton);
        builder.Register<GuildMegaProjectService>(Lifetime.Singleton);
        builder.Register<GuildChatService>(Lifetime.Singleton);
        builder.Register<GuildAchievementService>(Lifetime.Singleton);

        // Coordinators (Pattern aus Avalonia übernommen!)
        builder.Register<GameStartupCoordinator>(Lifetime.Singleton);
        builder.Register<ProgressionFeedbackCoordinator>(Lifetime.Singleton);
        builder.Register<GameTickCoordinator>(Lifetime.Singleton);
        builder.Register<CinematicCoordinator>(Lifetime.Singleton);

        // Config
        builder.RegisterInstance(_balancingConfig);

        // EntryPoint (startet das Spiel)
        builder.RegisterEntryPoint<BootEntryPoint>();
    }
}
```

### 4.3 Game-Loop (Service-Tick-Strategie)

Übernommen aus Avalonia (GameLoopService 1s-Takt + Coroutines für längere Intervalle):

```csharp
public class GameLoopService : IGameLoop, ITickable, IInitializable
{
    public async UniTask InitializeAsync(CancellationToken ct)
    {
        // Boot-Phase: Save laden, Cloud-Sync, FTUE
        await _gameStateService.LoadAsync(ct);
        await _cloudSaveService.SyncAsync(ct);
    }

    public void Tick()
    {
        // Unity-Tick (Update-Loop) - aber per VContainer
        var dt = Time.deltaTime;
        _gameClock.Advance(dt);

        // Sekündlich (akkumuliert)
        if (_gameClock.SecondElapsed)
        {
            _workshopService.TickIncome();
            _workerService.TickStates();
        }

        // Längere Intervalle via Periodic-Service (siehe ArcaneKingdom)
        _periodicService.Tick(dt);
    }
}
```

**Periodic-Intervalle (unverändert aus Avalonia):**

| Interval | Aktion |
|----------|--------|
| 1s | Passives Einkommen, Worker-States |
| 3s | Live-Orders ablaufen |
| 5s | Automation (AutoCollect, AutoAccept) |
| 25s | Live-Order-Spawn (50% Chance) |
| 60s | QuickJob-Rotation, Order-Expiry, AutoAssign |
| 60s/Off20 | Guild Boss Tick |
| 60s/Off40 | Guild Hall Tick |
| 180s | AutoCraft T1 |
| 300s | Event-Check, Guild-Achievements, War-Season |
| 360s | AutoCraft T2-T4 |

### 4.4 MVVM-Light Pattern (Unity-Adaption)

```
View (UXML/Prefab)
  └── ViewBinder (MonoBehaviour, holt UI-Refs)
        └── ViewModel (POCO, ObservableProperty<T>)
              ├── Commands (RelayCommand)
              └── Services (per VContainer injected)
```

**Kein Code-Behind in der View** — View ist nur Markup, ViewBinder verdrahtet UI-Elemente an das VM. VM ist **Unity-frei** und damit unit-testbar.

### 4.5 EventBus für lose Kopplung

```csharp
public interface IEventBus
{
    void Publish<T>(T evt) where T : IGameEvent;
    IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent;
}

// Beispiele für Game-Events:
public record MoneyChangedEvent(decimal NewAmount, decimal Delta);
public record OrderCompletedEvent(string OrderId, OrderResult Result);
public record LevelUpEvent(int NewLevel);
public record AchievementUnlockedEvent(AchievementId Id);
public record PrestigeTriggeredEvent(PrestigeTier Tier, int Count);
```

Coordinators und Feature-VMs subscriben nur auf Events, die sie wirklich brauchen → weniger Service-Sprawl als in Avalonia (wo direkte Service-Events üblich waren).

---

## 5. Was wird 1:1 portiert, was neu präsentiert, was ergänzt

> **Grundsatz:** Die **gesamte Spiel-Logik** (Mechaniken, Formeln, Balancing, Save-Schema, Firebase-Schema, Anti-Cheat) wird **1:1** portiert — sie ist verbindlich in [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md)/[DESIGN.md](DESIGN.md) dokumentiert. **Nichts Mechanisches wird "umgebaut".** Geändert wird ausschließlich die **Präsentation** (2D→3D, Audio, Input, UI-Tech) — das ist die legitime Verbesserung, niemals ein Eingriff in Mechanik oder Balance.

### 5.1 1:1 portierte Logik (Domain-Layer, keine mechanischen Anpassungen)

| Komponente | Avalonia-Quelle | Unity-Ziel |
|------------|-----------------|------------|
| **Datenmodelle (113 Files)** | `Models/*.cs` | `Domain/Models/*.cs` (records statt classes) |
| `GameState` (1032 Z., v7) | `Models/GameState.cs` | `Domain/HwiSave.cs` |
| `Workshop`, `Worker`, `Order` | dito | dito |
| **Berechnungs-Logik (IncomeCalculator)** | `Services/IncomeCalculatorService.cs` (335 Z.) | `Domain/Calculators/IncomeCalculator.cs` |
| **Balancing-Konstanten** | `Models/GameBalanceConstants.cs` (491 Z.) | `ScriptableObjects/Config/BalancingConfig.asset` (laufzeit-änderbar!) |
| **Order-Generator** | `Services/OrderGeneratorService.cs` (936 Z.) | `Domain/Orders/OrderGenerator.cs` |
| **Prestige-Logik** | `Services/PrestigeService.cs` (971 Z.) | `Domain/Prestige/PrestigeRules.cs` + `Game/PrestigeService.cs` |
| **Crafting-Rules** | `Services/CraftingService.cs` (~500 Z.) | `Domain/Crafting/CraftingRules.cs` + `Game/CraftingService.cs` |
| **Worker-Stats** | Worker-Model + Service | `Domain/Workers/*` |
| **Achievement-Definitions** | `Services/AchievementService.cs` (488 Z.) | `ScriptableObjects/Achievements/*.asset` |
| **Daily-Challenges** | `Services/DailyChallengeService.cs` (664 Z.) | `Domain/Challenges/*` + Service |
| **Weekly-Missions** | `Services/WeeklyMissionService.cs` (588 Z.) | dito |
| **BattlePass** | `Services/BattlePassService.cs` (226 Z.) | dito |
| **Save-Migration V1→V7** | `Services/SaveGameService.cs` | `Domain/Save/SaveMigrator.cs` (mit V8 für Unity-Erweiterungen) |
| **HMAC-Signierung** | Diverse Services | `Game/Security/HmacSigner.cs` |
| **Firebase-Pfade** | Diverse Services | `Game/Cloud/FirebasePaths.cs` |
| **Profanity-Filter** | `Services/ProfanityFilter.cs` | `Domain/Chat/ProfanityFilter.cs` |
| **Lokalisierungs-Strings** | `Resources/*.resx` (6 Sprachen) | Unity Localization String-Tables (Import-Skript) |
| **Story-Chapters** | `Models/StoryChapter.cs` | `ScriptableObjects/Story/Chapter_*.asset` |

**Geschätzte Ersparnis:** ~3-4 Wochen Implementierungs-Zeit durch Reuse der Domain-Logik. Die gelisteten Avalonia-Quellgrößen (Zeilenzahlen) sind grobe Orientierungswerte — maßgeblich für jede Formel/Wert bleibt [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md).

### 5.2 Neu präsentiert (UI + Rendering — reine Präsentationsschicht, KEINE Mechanik-Änderung)

> Hier wird ausschließlich die **Darstellungs-Technik** ausgetauscht (2D→3D, neuer UI-Stack, Audio/Input/Animation). Die dahinterliegende Logik bleibt die 1:1 portierte Domain aus § 5.1.

| Avalonia-Komponente | Unity-Equivalent | Aufwand |
|---------------------|-------------------|---------|
| 74 Avalonia-Views (.axaml + .axaml.cs) | UI Toolkit (UXML/USS) + uGUI für animierte Screens | 8-12 Wochen |
| 80 ViewModels | Unity-MVVM-Light (Unity-frei, gleiche Logik) | 4-6 Wochen |
| MainView Tab-Router | Hub.unity + Canvas-Stack + ScreenManager | 1 Woche |
| 59 SkiaSharp-Renderer | Mix aus: UI Toolkit (Codex), Shader Graph (FX), 3D-Prefabs (Werkstätten), uGUI+Animator (Mini-Games) | 8-10 Wochen |
| WorkerAvatarControl | Spine 2D-Animationen oder Unity-Mecanim | 2 Wochen |
| GameIcon (PathIcon-Subklasse, 224 Icons) | TextMeshPro-SpriteAsset oder ScriptableObject-IconAtlas | 1 Woche |
| `App.axaml.cs` ServiceCollection (~91 Services) | VContainer-Installer (siehe 4.2) | 1 Woche |
| Android-LinkedFiles (`AdMobHelper`, `RewardedAdHelper`, etc.) | Google Mobile Ads Unity SDK + Plugin | 1 Woche |
| Platform-Audio (3 Implementierungen) | Unity AudioMixer + Addressables | 0.5 Wochen |

### 5.3 Komplett NEU (was Unity ermöglicht)

| Feature | Beschreibung | Aufwand |
|---------|-------------|---------|
| **3D-Werkstattszenen** | 10 Werkstätten als low-poly 3D-Modelle, Worker-Avatare laufen physisch durch die Welt | 8-12 Wochen |
| **3D-Mini-Games** | Sawing/Forge/Pipe/Wiring als 3D-Erlebnis mit Physik, Funken, Holz-Splittern | 6-8 Wochen |
| **GPU-Particle-System** | Pooling für Coins/Confetti/Sparkle/Money-Burst mit 10.000+ Particles | 1-2 Wochen |
| **Shader-Graph-Effekte** | Workshop-Glow (Level-abhängig), Holographic-Hover, Dissolve-Transitionen, Card-Foil | 2-3 Wochen |
| **Post-Processing-Stack** | Bloom, Vignette, Chromatic Aberration, Color Grading, Film Grain | 1 Woche |
| **Camera-System** | Orbit-Camera für Werkstatt-Wechsel, Cinemachine für Prestige-Sequenzen | 2 Wochen |
| **Animator-State-Machines** | Worker-Animations (Idle, Work, Talk, Drink, Eat), Tutorial-Pulse | 3 Wochen |
| **Timeline-Sequenzen** | Prestige-Cinematic (statt SkiaSharp-PrestigeCinematicRenderer) | 1-2 Wochen |
| **Worker-Voice-Lines** | Optional: 6 Voice-Lines pro Tier (Hire, Promotion, Goodbye) | 4 Wochen (Voice-Acting) |
| **Procedural-Audio** | Dynamische Sound-Layer (Idle-Workshop → Boss-Music smooth-blend) | 1 Woche |
| **Haptic Feedback** | Native Vibration bei Level-Up, Prestige, Mini-Game-Perfect | 0.5 Wochen |
| **Day/Night-Cycle** | Optional: Werkstatt-Beleuchtung wechselt mit Tageszeit | 1 Woche |
| **Weather-System (Real-Weather)** | Optional: API-Wetter beeinflusst Werkstatt-Outdoor-Effekte | 1 Woche |
| **Achievement-Notifications mit 3D-Trophäe** | Statt Dialog-Popup: 3D-Trophäe rotiert mit Glow | 1 Woche |
| **Tutorial-Highlight-Overlays** | Pulsierender Outline-Effekt um UI-Elemente, animiert | 1 Woche |
| **Cinemachine-Camera-Shake** | Mit Motion-Blur (statt SkiaSharp-Translate) | 0.5 Wochen |

---

## 6. Spielmechaniken im Detail

> Alle Mechaniken, Formeln und Balancing-Werte werden **1:1** aus der Avalonia-Version übernommen.
> **Verbindliche Werte stehen in [DESIGN.md](DESIGN.md) (vollständiges GDD) und [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) (Single Source of Truth).**
> Dieser Abschnitt beschreibt **nur** die Unity-spezifische **Präsentation** (3D, FX, Audio) — er dupliziert
> bewusst **keine** Werte mehr, um Abweichungen zu vermeiden.

### 6.1 Werkstätten (10 Typen)

**Werte/Mechanik:** siehe [DESIGN.md § 4](DESIGN.md#4-werkstätten-10-typen) (Tabelle der 10 Typen, Unlock-Level/-Kosten, Income-Multiplikatoren, Income-Formel § 4.3, Milestones § 4.4, Upgrade-Kosten § 4.5, Slots § 4.6, Spezialisierung § 4.7, Rebirth § 4.8, Manager § 4.9). Stammwerte: [ORIGINAL_WERTE.md § 01/02](ORIGINAL_WERTE.md).

Kurz-Eckdaten (keine abweichende Zweitquelle — bei Konflikt gilt DESIGN/ORIGINAL_WERTE):
- **Max-Level 1000** pro Werkstatt (`WorkshopMaxLevel = 1000`) — KEIN "1-1500+".
- **Income** = Summe über alle eingesetzten Worker (`1.02^(Level-1) × TypeMultiplier × MilestoneMultiplier × …`); **ohne Worker kein Einkommen** — KEIN globaler `BaseValue × Multiplier`-Term (siehe DESIGN § 4.3).
- **Rebirth (0–5 Sterne):** Trigger ist **Level 1000** (nicht "alle 100 Level"); gestaffelte Boni je Stern (+15/35/60/100/150 % Einkommen, −5..−25 % Upgrade-Kosten, +0/1/1/2/2/3 Worker-Slots) — DESIGN § 4.8.
- **Spezialisierung:** Efficiency / Quality / Economy ab Level 50 (nicht "Speed/Quality/Income") — DESIGN § 4.7.
- **Manager:** 14 fest definierte Manager (kein freier Slot pro Werkstatt) — DESIGN § 4.9.

**Unity-Visualisierung (Präsentation):**
- Jede Werkstatt als 3D-Szene (Workshop.unity, additive)
- Camera schwenkt von Hub zu Detail-Ansicht
- Worker laufen sichtbar zwischen Arbeitsstationen
- Level-Up: Sterne erscheinen über der Werkstatt, Funken-FX

### 6.2 Arbeiter (10 Tiers)

**Werte/Mechanik:** siehe [DESIGN.md § 5](DESIGN.md#5-arbeiter-10-tiers) (Tier-Tabelle mit Effizienz-Spannen, Löhnen, Hire-Kosten, Unlock-Level, Aura-Bonus, Level-Resistance; Stats/EffectiveEfficiency § 5.2; Mood § 5.3; Fatigue § 5.4; Training § 5.6; Personalities § 5.5b; Affinity § 5.7; Markt § 5.8; Auktionen § 5.9). Stammwerte: [ORIGINAL_WERTE.md § 01](ORIGINAL_WERTE.md).

Kurz-Eckdaten (keine abweichende Zweitquelle):
- 10 Tiers F → Legendary; **Effizienz ist eine Min/Max-Spanne pro Tier** (z.B. F 0.30–0.50x, Legendary 13.0–22.0x) — die früheren festen Faktoren (1.0x … 65x) waren falsch.
- Worker-Stats: **Erfahrungslevel (`ExperienceLevel` 1–10)**, XP, Mood (0–100), Fatigue (0–100), Talent (1–5 Sterne), Personality (6 Typen), MaterialAffinity, 1 Equipment-Slot. (1–1000 ist der **Workshop**-Level, NICHT der Worker — siehe DESIGN § 5.2.)
- Auktionen sind ein **Gilden-Feature** (Worker-Markt vs. Gilden-Auktion) — exakte Cycle-/Bid-/Bot-Werte siehe DESIGN § 5.8/§ 5.9.

**Unity-Visualisierung (Präsentation):**
- Worker als **Spine 2D-Animationen** oder **animierte 3D-Charaktere**
- Mood-Anzeige als Schwebe-Icon (Glücklich/Genervt/Erschöpft)
- Idle-Bobbing + Blinzeln + Trink-Animation
- Bei Promotion: Konfetti-Burst, neuer Avatar-Rahmen

### 6.3 Aufträge (6 Types)

**Werte/Mechanik (verbindlich):** [DESIGN.md § 6](DESIGN.md#6-aufträge-6-types--3-strategien) — Order-Types § 6.1, Reward-/XP-Formel § 6.1a, Strategien § 6.2, Live-Orders § 6.3, Material-Orders § 6.4. Die folgende Übersicht ist nur eine Kurz-Orientierung.

| Type | Reward-Multiplier | Special |
|------|--------------------|---------|
| Quick-Order | 0.6x | 5min Deadline, niedriges Risiko |
| Standard | 1.0x | Standard-Auftrag |
| Large | 1.8x | Mehr Tasks, längere Deadline |
| Cooperation | 2.5x | Mehrere Worker, Gilden-Co-op möglich |
| Weekly | 3.0x | 7 Tage, sehr hoch belohnt |
| MaterialOrder | 1.8x | Verbrauchen Crafting-Inventar |

**3 Strategien pro Auftrag:**
- **Safe:** 0.75x Reward, +50% Zone, +30% Zeit
- **Standard:** 1.0x Reward
- **Risk:** 2.0x Reward, -50% Zone, +30% Tempo, Hard-Fail → 0 € + -10 Rep

**Live-Orders:**
- VIP-Kunden, 25s-Spawn, max 5 gleichzeitig
- 3x Reward, 2.5x XP
- Auto-Accept-Option (Premium)

**Unity-Verbesserungen:**
- Live-Orders als **3D-Kunden-NPCs** die zur Werkstatt laufen
- Strategie-Wahl via animiertes Risiko-Meter
- Mini-Game-Erfolg sichtbar in Werkstatt-Szene (Worker animiert es)

### 6.4 Mini-Games (13 Enum-Typen, 10 Renderer — als 3D-Erlebnis neu gedacht)

**Werte/Mechanik (verbindlich):** [DESIGN.md § 7](DESIGN.md#7-mini-games-13-typen-10-renderer). **13 MiniGame-Enum-Typen, aber nur 10 distinkte Routen/Renderer** (Planing, TileLaying, Measuring teilen die Sawing-Route); 8 davon sind "perfekt-zählbar" (Achievement `all_minigames_perfect`). Die untenstehende Tabelle zeigt die **10 Renderer** als 3D-Konzept.

| Mini-Game (Renderer) | Avalonia (2D-SkiaSharp) | Unity (3D + Physik) |
|-----------|--------------------------|---------------------|
| **Sawing** | Bezier-Maserung-Pfad, 2D-Säge | 3D-Holzbrett mit Maserung, Säge folgt Maus/Finger, Holz-Splitter-Partikel, Klang reagiert auf Druck |
| **Pipe Puzzle** | 2D-BFS Wasser-Durchfluss | 3D-Rohrleitungs-Anlage, drehen mit Touch, Wasser fließt sichtbar mit Particles |
| **Wiring** | 2D-SKPath-Pulse | 3D-Schaltkreis-Board, Funken bei korrektem Timing |
| **Painting** | 2D-Combo-Badge, Pinselstrich | 3D-Wand, Pinsel-Spuren bleiben, Farbe tropft mit Physics |
| **Blueprint** | 2D-Grid-Memorisierung | 3D-Bauplan-Tisch, Pläne werden physisch umgedreht |
| **RoofTiling** | 2D-Pseudo-3D-Ziegel | Echtes 3D-Dach, Ziegel platzieren mit Physik |
| **DesignPuzzle** | 2D-Architekten-Plan | 3D-Raum-Layout-Editor, Möbel drag-and-drop |
| **Inspection** | 2D-Vektor-Icons, pulsierende Lupe | 3D-Gebäude, Lupe scannt Wände, Mängel als rote Glyphen |
| **ForgeGame** | 2D-Amboss, Temperatur-Zonen | 3D-Schmiede mit echtem Feuer-Shader, Hammer-Schlag mit Funken |
| **InventGame** | 2D-Circuit-Pulse | 3D-Labor mit verbindbaren Modulen |

**Rating-System bleibt:** Perfect (100%), Good (75%), Ok (50%), Miss (0%)

**Auto-Complete-Tickets bleiben:** 30 Perfects → Auto-Complete (Premium: 15)

### 6.5 Forschung (72 Nodes, 4 Branches)

**Werte/Mechanik (verbindlich):** [DESIGN.md § 8](DESIGN.md#8-forschung-72-nodes-4-branches) — alle 72 Nodes, Effekt-Felder § 8.2, Effekt-Aggregation § 8.7, Mechaniken § 8.8.

| Branch | Enum | Nodes | Fokus |
|--------|------|-------|-------|
| Tools | 0 | 20 | Effizienz, MiniGame-Zone, Bau-Kosten, Auto-Material, Ascension |
| Management | 1 | 20 | Lohn-Reduktion, Worker-Slots, Worker-Tiers-Unlock, Training, Auto-Assign |
| Marketing | 2 | 20 | Reward-Multiplikator, Order-Slots, Reputation, Premium-Order-Chance |
| Logistics | 3 | 12 | Lager-Slots, Stack-Limit, Markt, Auto-Sell, Crafting-Speed, Tier-4, Erbstücke |

(Summe: 20 + 20 + 20 + 12 = **72**.)

**Unity-Visualisierung:**
- Forschungsbaum als **3D-Skill-Tree** mit Cinemachine-Kamera
- Aktive Forschung: Particle-Strom zwischen Nodes
- Abgeschlossen: Goldene Aura

### 6.6 Prestige (7 Tiers + Ascension)

**Werte/Mechanik (verbindlich):** [DESIGN.md § 9](DESIGN.md#9-prestige-7-tiers--ascension) — Tier-Tabelle § 9.1, PP-Berechnung § 9.2, permanenter Multiplikator + Diminishing Returns + **Hard-Cap 20×** § 9.3, Prestige-Shop (**25 Items**) § 9.7, Ascension § 9.8.

Kurz-Orientierung (permanenter Einkommens-Bonus pro Tier, `GetPermanentMultiplierBonus`):

| Tier | Permanent-Mult-Bonus | Preservation (kumulativ) |
|------|----------------------|--------------------------|
| Bronze | +20% | Prestige-Daten, Achievements, Premium, Settings, Tutorial |
| Silver | +35% | + dito |
| Gold | +50% | + Research |
| Platin | +100% | + Prestige-Shop-Items |
| Diamant | +200% | + Master-Tools |
| Meister | +400% | + Gebäude (Lv→1), Equipment |
| Legende | +800% | + Manager (Lv→1), Top-3 Worker/WS |

> **Wichtig (siehe DESIGN § 9.1/§ 9.3):** Die PP-Multiplikator-Spalte (1×…64×) skaliert nur die Prestige-**Punkte**. Der **permanente Einkommens-Multiplikator** (akkumuliert aus obigen Bonus-Werten) ist davon getrennt und **hart bei 20× gedeckelt** (`MaxPermanentMultiplier = 20.0`).

**Nach 3× Legende: Ascension freigeschaltet** (DESIGN § 9.8).

**Unity-Cinematic (statt SkiaSharp):**
- Timeline-Sequenz (10-15s)
- Phase 1: Geld zerspringt in Sterne (Particles)
- Phase 2: Badge schwebt nach oben mit Bloom-Effekt
- Phase 3: Multiplikator-Zahl zoomt mit Distortion-Shader
- Phase 4: Belohnungs-Karte fliegt zum Spieler
- Auto-Dismiss nach 12s

### 6.7 Gilden (V7-Features 1:1)

**Werte/Mechanik (verbindlich):** [DESIGN.md § 17](DESIGN.md#17-gilden--multiplayer) — Struktur § 17.1 (max **20** Basis-Mitglieder, 3 Rollen), Guild-Research **18 Nodes** § 17.2, **6 Bosse** § 17.4, **10 Hall-Gebäude** § 17.5, Mega-Projekte (2 Templates) § 17.6, Achievements (33) § 17.10, Firebase-Schema § 17.12. Alle Gilden-Features bleiben strukturell identisch zur Avalonia-Version — Firebase-Pfade kompatibel.

| Feature | Status |
|---------|--------|
| Gilden-CRUD | 1:1 (GuildService aus Avalonia) |
| 6-stellige Invite-Codes | 1:1 |
| Co-op-Aufträge (HMAC, atomar) | 1:1 |
| Worker-Auktionen | 1:1 |
| Boss-Kämpfe (6 Bosse) | 1:1 + 3D-Boss-Modelle |
| Hall-Gebäude (10) | 1:1 + 3D-Gebäude in Guild-Hub-Szene |
| Kriegssaison | 1:1 |
| Mega-Projekte | 1:1 |
| Chat (DE/EN/ES/FR/IT/PT Profanity-Filter) | 1:1 |

**Unity-Verbesserungen:**
- **Gilden-Hub als 3D-Szene** (Guild.unity additive)
- Hall-Gebäude sind physisch sichtbar (Level = Größe + Glow)
- Boss-Kämpfe mit animiertem 3D-Boss + Particle-Damage
- Chat mit TextMesh Pro (Emoji-Support, Rich Text)

### 6.8 Lager & Crafting (V7)

**Werte/Mechanik (verbindlich):** Crafting [DESIGN.md § 10](DESIGN.md#10-crafting-33-rezepte-4-tiers), Lager [DESIGN.md § 11](DESIGN.md#11-lager-warehouse-v7).

- 20-200 Slots, Stack-Limit (siehe DESIGN § 11.1)
- **33 Rezepte** (T1 10 + T2 10 + T3 10 + T4 3) — DESIGN § 10
- Auto-Verkaufs-Regeln (Unlock via logi_07) — DESIGN § 11.5
- Material-Affinität (+20% Crafting-Speed bei Match) — DESIGN § 12.5

**Unity-Visualisierung:**
- Lager als **3D-Regalsystem** (animiert sich auffüllend)
- Crafting-Tisch mit sichtbarer Produktion-Animation
- Material-Icons als TextMeshPro-SpriteAsset

### 6.9 Markt

- Tagespreis-Sinus-Welle (±50%)
- Buy/Sell, Event-Modulation

**Unity:** Markt als **animiertes Diagramm** (LineRenderer in UI Toolkit), Preise springen mit Tween-Animation.

### 6.10 Achievements/Quests/BattlePass

**Werte/Mechanik (verbindlich):** Achievements [DESIGN.md § 18](DESIGN.md#18-achievements-109-spieler-achievements-17-kategorien), Daily-Reward § 19, Lucky-Spin § 20, BattlePass § 21, Live-/Random-Events § 22, Saisonale Events § 23.

- **109 Spieler-Achievements** (17 Kategorien) — DESIGN § 18
- Daily-Reward (30-Tage-Zyklus) + Lucky-Spin (8 Slots) — DESIGN § 19/§ 20
- BattlePass: **50 Tiers**, 30-Tage-Saison (Free + Premium) — DESIGN § 21
- 4 Live-Event-Templates + 8 Random-Events + 4 saisonale Events/Jahr — DESIGN § 22/§ 23

**Unity-Verbesserung:**
- Achievement-Unlock mit **3D-Trophäen-Cinematic** (statt 2D-Dialog)
- BattlePass-Tier-Up mit Stinger-FX + Sound

---

## 7. UI-Konzept

### 7.1 Hub (Hub.unity, 5 Tabs)

```
┌──────────────────────────────────────────────────┐
│  Header: Money | Level | XP-Bar | Gold-Screws    │
├──────────────────────────────────────────────────┤
│                                                  │
│             [3D-Hub-Szene mit Werkstätten]       │
│             (Camera-Pan zwischen Tabs)            │
│                                                  │
│  ┌────────────┬────────┐                         │
│  │ Auto-Bar   │ News   │                         │
│  └────────────┴────────┘                         │
├──────────────────────────────────────────────────┤
│ [Dashboard] [Imperium] [Missionen] [Gilde][Shop] │
└──────────────────────────────────────────────────┘
```

**Tabs:**
1. **Dashboard:** 3D-City-Übersicht, Auto-Collect-Buttons, Live-Orders
2. **Imperium:** Sub-Tab-Router (Workshops/Warehouse/Workers/Research/Equipment/Ascension)
3. **Missionen:** Daily/Weekly/QuickJobs/LuckySpin/BattlePass
4. **Gilde:** Übersicht/Boss/Research/Chat/Mitglieder (eigene 3D-Szene optional)
5. **Shop:** IAP, Goldschrauben-Pakete, Equipment

**Stack:** UI Toolkit für statische Screens, uGUI für animierte (Mini-Games, Dialoge mit Stinger-FX).

### 7.2 Werkstatt-Detail (Workshop.unity, additive)

- Tap auf Werkstatt-Karte → Camera-Pan zur 3D-Detail-Szene
- Worker arbeiten sichtbar an Arbeitsstationen
- Upgrade/Spezialisierung/Manager als Floating-UI-Panels
- Schwenk zurück mit Cinemachine

### 7.3 Mini-Game-Container (MiniGame.unity, additive)

- Wird beim Start eines Mini-Games geladen
- Container für jedes der 10 Mini-Games (Prefab pro Game)
- Loading-Screen mit Tipp-Karte (Animation)

### 7.4 Dialoge & Modals

| Dialog | Pattern |
|--------|---------|
| Achievement-Dialog | 3D-Trophäe + Glow + Sound |
| Story-Dialog | Vollbild mit Parallax-BG, Worker-Avatar links |
| Prestige-Confirm | Vorher/Nachher-Vergleich mit animierten Zahlen |
| Daily-Reward | 7-Tage-Kalender, Slot-Highlight |
| Offline-Earnings | Animierte Geld-Anzeige (Odometer-Rolle) |
| Welcome-Back-Offer | Stinger-FX, Countdown-Timer |
| Notification-Center | Chronologische Event-Liste |
| Worker-Profile | 3D-Worker-Modell rotiert, Stats-Bars |

### 7.5 Animation-Stack

- **Animator** für Worker-States
- **DOTween** für UI-Animationen (Buttons, Cards, Numbers)
- **Timeline** für längere Sequenzen (Prestige-Cinematic)
- **Cinemachine** für Camera-Movements

### 7.6 Lokalisierung

Unity Localization Package mit String-Tables:
- DE (primär), EN, ES, FR, IT, PT
- Import-Skript: RESX → String-Table (Editor-Tool, einmalig beim Migrieren)
- TextMesh Pro mit Font-Assets für CJK-Erweiterung (Phase 2)

---

## 8. Online & Multiplayer

### 8.1 Firebase Realtime DB (Schema 1:1 wie Avalonia)

Alle Knoten sind **Top-Level** — KEIN verschachteltes `guilds/{guildId}/everything` und KEIN erfundener `players/{playerId}/`-Sammelknoten (den gibt es im Original nicht). Vollstaendige Pfad-Liste: [ARCHITECTURE.md § 10.1](ARCHITECTURE.md). Kurzueberblick:

```
auth_to_player/{uid}                         → PlayerId-Mapping
cloud_saves/{playerId}/{metadata,data}       → Cloud-Save (Metadata-Preview + State-JSON-String)
available_players/{uid}                      → Suchindex
player_guilds/{playerId}                     → GuildId-Lookup
guilds/{guildId}                             → FirebaseGuildData (+ coopOrders/, megaProjects/active darunter)
guild_members/{guildId}/{uid}                → Mitglieder
guild_research/{guildId}/{researchId}        → 18 Research-Nodes
guild_hall/{guildId}/buildings/{buildingId}  → 10 Gebäude
guild_bosses/{guildId}                       → 6 Boss-Types (+ guild_boss_damage/{guildId}/{uid})
guild_achievements/{guildId}/{achievementId} → 33 Achievements
guild_chat/{guildId}/messages/{messageId}    → Chat
guild_war_seasons/{seasonId}                 → Kriegssaison (+ leagues/, guild_wars/, guild_war_scores/)
```

**Pfad-Schema:** identisch zur Avalonia-Version (vollstaendig in [ARCHITECTURE.md § 10.1](ARCHITECTURE.md)). Cloud-Save liegt unter `cloud_saves/{playerId}/`, der GameState ist ein **flaches JSON** (kein `players/{playerId}/progress`-Baum). Die parallele Beta läuft über eine **eigene Firebase-Datenbank-Instanz / ein eigenes Projekt** (Migrations-Strategie § 17.2), nicht über einen abweichenden Pfad-Prefix.

### 8.2 Authentifizierung

- **Firebase Auth Anonymous** (default)
- **Google Sign-In** (für Cloud-Save zwischen Devices)
- Stabile **PlayerId** (UUID, nicht Firebase-UID) — beim ersten Login generiert, dauerhaft mit dem Account verknüpft

### 8.3 Cloud-Save

- **Lokaler Save ist primär** (atomar: Save/Backup/Temp); AutoSave alle 30s + bei wichtigen Events; Cloud-Upload rate-limitiert (alle 2 min, NICHT jeder Tick).
- Conflict-Resolution: **Local-First** — beim Laden SavedAt-Vergleich (+5s-Toleranz), bei Konflikt **User-Confirmation**, Version-Outdated-Schutz, Neu-Signierung beim Download (Details [ARCHITECTURE.md § 8.4](ARCHITECTURE.md)). NICHT "Server wins".
- Lokaler JSON-Fallback (persistentDataPath)
- Cross-Device-Sync per Google Account

### 8.4 Cloud Functions (Anti-Cheat)

Übernommen aus Avalonia + ArcaneKingdom-Patterns:

| Function | Zweck |
|----------|-------|
| `validateIapReceipt` | Google Play Receipt validieren |
| `validateMiniGameScore` | Score-Sanity-Check (Max-Score pro Mini-Game) |
| `settleBattlePassRewards` | Saison-Reward-Verteilung |
| `createGuild` | Tag-Eindeutigkeits-Transaction |
| `onPlayerWriteValidate` | DB-Listener für Schema + Cap-Prüfung |
| `onReportReceived` | Auto-Mute nach N Reports |
| `onWarSeasonCompleted` | Belohnungs-Verteilung |
| `livEventRefresh` | Live-Event-Score-Tabelle alle 4h |

### 8.5 Multiplayer-Features (1:1 aus Avalonia)

| Feature | Implementierung |
|---------|-----------------|
| Gilden | Firebase RTDB + HMAC-Signierung |
| Co-op-Orders | Firebase PATCH-atomar |
| Worker-Auktionen | Master-Client-Pattern, NPC-Bots |
| Boss-Kämpfe | Realtime-Schadens-Tracking |
| Chat | Firebase + Profanity-Filter (alternativ Photon Chat in Phase 2) |
| Live-Events | LiveEventService + Remote Config |
| Referrals | 6-stellige Codes, 3-Tier-Reward |

### 8.6 Push-Notifications (8 Trigger)

Unity Mobile Notifications Package:
1. ResearchDone
2. DeliveryReminder
3. RushAvailable
4. DailyReward
5. WorkerMoodCritical
6. OfflineEarningsCapped
7. BattlePassExpiring
8. LiveOrderAvailable

### 8.7 Offline-Modus

- Vollständig spielbar offline
- Belohnungen werden beim nächsten Connect synchronisiert
- Offline-Banner sichtbar wenn keine Connection
- Gilde-Features benötigen Connection

---

## 9. Unity-spezifische Neuerungen

Hier kommen die **wirklich neuen** Features, die Avalonia/SkiaSharp nicht leisten konnte.

### 9.1 3D-Werkstatt-Visualisierung

**Konzept:** Statt 2D-WorkshopCards mit AI-Bitmaps + Overlays haben wir echte **3D-Modelle** der Werkstätten — low-poly stylized, mit Day/Night-Beleuchtung.

**Technisch:**
- Asset-Erstellung: Blender (low-poly, max 5k Tris pro Werkstatt)
- Material: Substance Painter (4K Atlas, kann auf 2K für Mobile reduziert werden)
- Beleuchtung: Mixed Lighting (Baked Ambient + Realtime Sun/Spot)
- Camera: Cinemachine Orbit-Camera, schwenkbar mit Touch
- Worker laufen sichtbar zwischen Arbeitsstationen (NavMesh)

**Impact:**
- Visueller Sprung um eine ganze Generation
- Tab-Wechsel mit Camera-Pan statt Crossfade
- Werkstatt-Upgrades sichtbar (Werkstatt wird größer, mehr Geräte erscheinen)
- Rebirth-Sterne als physische Trophäen über der Werkstatt

### 9.2 GPU-Particle-System

**Pooling-Strategy:**
- **Coin-Fly:** 200 Particles, Bezier-Pfad mit Magnet-Effekt zum Wallet
- **Confetti:** 500 Particles, Physics-fall mit Drag
- **Sparkle:** 1000 Particles, kurze Lebensdauer, hoher Glow
- **Money-Burst:** 100 große €-Symbole, Outward-Burst
- **Sägemehl:** 300 Particles bei Sawing-Mini-Game
- **Funken:** 500 Particles bei Forge/Wiring

**Performance:**
- GPU-Instancing für Particles ohne Physics
- Compute-Shader für Particles mit komplexer Logik
- Mobile-Profil: 60% reduzierter Pool

**Vergleich zur Avalonia:**
- Avalonia: max 200 Struct-Particles auf CPU
- Unity: 10.000+ GPU-Particles ohne FPS-Drop

### 9.3 Shader-Graph-Effekte

**Geplante Shader (alle als Shader-Graph für visuelle Iteration):**

| Shader | Verwendung |
|--------|-----------|
| **Workshop-Glow** | Aura um Werkstätten, Intensität = Level / 100 |
| **Holographic-Worker** | Legendary/Mythic-Worker mit Foil-Effekt |
| **Money-Shimmer** | Goldschrauben-Counter mit Shimmer-Pulse |
| **Dissolve-Transition** | View-Wechsel-Animation |
| **Card-Foil** | Equipment-Karten mit Rarity-abhängigem Foil |
| **Prestige-Distortion** | Bei Prestige: Heat-Wave-Distortion |
| **Water-Flow** | Pipe-Puzzle-Wasser |
| **Fire** | Forge-Mini-Game |
| **Electricity** | Wiring-Mini-Game |
| **Hologram** | Tutorial-Overlay-Outlines |

### 9.4 Post-Processing-Stack

URP Post-Processing für Mobile:
- **Bloom** (für Glow-Effekte)
- **Vignette** (Fokus während Dialog)
- **Color Grading** (Tageszeit-Stimmung)
- **Chromatic Aberration** (sparsam, für Prestige-Cinematic)
- **Film Grain** (off-default, einschaltbar)
- **Depth-of-Field** (Background-Blur bei Modal-Open)

### 9.5 Animation & Timeline

**Animator-Controllers:**
- Worker: Idle/Walk/Work/Drink/Talk/Sleep
- Buttons: Idle/Hover/Press/Disabled
- Dialog: Open/Idle/Close

**DOTween-Sequences:**
- Tab-Wechsel: 200ms ease-out
- Money-Counter-Rolle: Variable-Speed-Tween
- Coin-Fly: Bezier-Path mit Magnetwirkung
- Achievement-Pop: Scale-Bounce + Glow

**Timeline-Sequenzen:**
- Prestige-Cinematic: 12s, vollanimiert
- Erste-Werkstatt-Tour: 30s, Tutorial-Sequenz
- Saison-Wechsel-Stinger: 5s

### 9.6 Audio (AudioMixer)

**Mixer-Gruppen:**
- Master
  - Music (BGM)
  - SFX
    - UI-SFX
    - Game-SFX (Workshop, Worker)
    - MiniGame-SFX (Sawing, Forge, etc.)
  - Voice (Worker-Lines, optional)
  - Ambience (Workshop-Background)

**Effekte:**
- **Ducking:** Music duckt bei Dialog (-12dB)
- **Reverb-Zones:** Pro Werkstatt eigener Reverb
- **3D-Positional-Audio:** Worker-Stimmen aus deren Position
- **Snapshot-Transitions:** Boss-Kampf wechselt Audio-Snapshot (smooth)

**Music-System:**
- 4 Loops aus Avalonia portiert
- Cross-Fade mit Tempo-Match (DOTween-Volume)
- Optional: Procedural-Layering (Idle → Boss durch Hinzufügen von Drum-Layer)

### 9.7 Haptic Feedback

Unity Vibration API:
- Level-Up: Heavy Pulse
- Prestige: Long Vibration mit Pattern
- Mini-Game-Perfect: Light Tap
- Achievement: Medium Pulse
- Goldschrauben-Erhalt: Soft Pulse

### 9.8 Camera-System (Cinemachine)

**Cameras im Hub:**
- **HubCamera (Default):** Top-Down auf alle Werkstätten
- **WorkshopOrbitCamera:** Orbit um aktive Werkstatt
- **PrestigeCamera:** Cinematic-Schwenk für Prestige
- **DialogCamera:** Zoom für Dialog-Fokus

**Transitions:** Cinemachine Blend (smooth ease).

### 9.9 Day/Night-Cycle (Optional)

- Echtzeit-Tageszeit beeinflusst Beleuchtung
- Werkstatt-Innenbeleuchtung sichtbar bei Nacht
- Wechsel alle 6h (4 Phasen: Morgen/Tag/Abend/Nacht)
- Lichteinfluss auf Worker-Mood (klein, +5% Mood am Tag)

### 9.10 Live-Wetter (Optional)

- Geo-IP basiert (oder manuell wählbar in Settings)
- Regen-Particles + Audio
- Sonne + Bloom
- Schnee in Wintermonaten
- Außenwerkstätten (Maurerei, Dach) reagieren visuell

### 9.11 Achievement-Notifications mit 3D-Trophäe

Statt 2D-Dialog:
- Trophäe spawnt am Bildschirmrand
- Rotiert 3x mit Glow
- Floating-Text mit Achievement-Name
- Sound + Haptic
- 5s sichtbar, dann Smooth-Fade

### 9.12 Tutorial-Highlight-Overlays

- Pulsierende Outlines um UI-Elemente
- Animierter Finger-Tap-Indicator (3D, schwebt)
- Stage-Lighting (Spotlight auf hervorgehobenes Element)
- Smooth-Transition zwischen Tutorial-Steps

### 9.13 Cinematic Camera Shake

Cinemachine Impulse Source:
- Bei Boss-Hit, großen Achievements, Prestige
- Konfigurierbar pro Event (Stärke, Duration, Falloff)
- Mit Motion-Blur (URP)

### 9.14 Procedural-Audio (Optional, Phase 2)

- Workshop-Sounds passen sich an Worker-Anzahl an (mehr Worker = lauter)
- Boss-Music wächst progressiv mit Schaden
- Mini-Game-Timing-Sounds reagieren auf Performance

### 9.15 Interaktive Tutorials

- 3D-Kamera führt Spieler durch Hub
- "Tippe hier" mit animiertem Finger
- Story-NPC erscheint kurz und gibt Tipps
- Stage-Lighting fokussiert wichtigen Bereich

### 9.16 Meister-Hans-Voice (im MVP — ElevenLabs Standard-Voice)

- **Persona-Anker:** "Meister Hans" — der Großonkel des Spielers, der die Werkstatt vererbt hat
- **6 Sprachen** (DE/EN/ES/FR/IT/PT)
- **Voice-Strategie (final Mai 2026):** **Vorgefertigte ElevenLabs-Standard-Voice** aus der Voice-Library (kein Voice-Cloning, kein eigener Sprecher)
  - **Auswahl-Kriterium:** Warm, freundlich, leicht karikiert, "older craftsman" Vibe
  - **Multilingual v2-Modell:** Eine Stimme funktioniert in allen 6 Sprachen
  - **Vorteil:** Schneller Setup, keine Sprecher-Freigabe-PDF, keine rechtlichen Risiken
- **~250 Lines × 6 Sprachen = 1500 Voice-Files**:
  - 10 Tutorial-Hints
  - 100 Story-Lines (20 Chapters × 5 Lines)
  - 20 Idle-Tipps (random)
  - 30+ Achievement-Stinger
  - 30 Diverse (Live-Events, Notifications, Premium-Promotion, etc.)
- **Mastering:** −16 LUFS (Mobile-Standard)
- **Output:** `F:\AI\audio_output\handwerkerimperium_unity\voice_meister_hans\{de,en,es,fr,it,pt}\`
- **API-Cost-Schätzung:** ElevenLabs Pro (22 €/Mo) reicht für 1500 Lines × 100k chars/month Pro-Limit
- **Re-Generation:** Bei neuen Story-Chapters jederzeit nachbatchbar via API

> **Hinweis:** Voice-Cloning mit eigener Aufnahme wurde verworfen → Standard-Voice ist einfacher, schneller, ohne rechtliche Risiken. ASSETS_AI.md § 11.3 ist entsprechend zu aktualisieren (Voice-Cloning-Sektion entfallen). Worker-Tier-spezifische Voice-Lines (separate Stimmen pro Worker) kommen erst in Phase 2.

### 9.17 Replay-System (Optional)

- Prestige-Highlights aufnehmen (5s Clips)
- Teilen via Share-Sheet
- Lokales Storage, kein Upload

### 9.18 Cloud-Catalog für Assets

- **Phase 1:** Alles im Build (~120 MB)
- **Phase 2:** Remote-Catalog via Firebase Storage
- Worker-Cosmetics, Workshop-Skins als DLC-Drops
- LiveOps kann neue Assets pushen ohne App-Update

### 9.19 Live-Balancing-Dashboard (Editor)

- Unity-Editor-Window
- BalancingConfig-Werte editierbar zur Laufzeit
- Hot-Reload (kein Recompile)
- Export zu Firebase Remote Config

### 9.20 Save-Editor (Editor + Cheat-Window in Dev-Builds)

- Editor-Tool: Save-Files öffnen, editieren, validieren
- Dev-Build: Cheats-Window (Money setzen, Workshop-Level, etc.)
- Disabled in Release-Build

---

## 10. Persistenz & Save-System

### 10.1 Save-Schema (Erweiterung von Avalonia V7 auf Unity V8)

**Bestehendes Schema bleibt** (Avalonia V7):
```
HwiSave v7 {
  GameState (Money, Level, ...)
  Workshops, Workers, Orders, Research
  Crafting, Warehouse
  PrestigeData, AscensionData
  GuildMembership
  Equipment, Statistics
  ...
}
```

**Neue Slices (V8):**
```
HwiSave v8 = v7 + {
  CosmeticSlice         // Skins, Cosmetic-Items
  UnitySpecificSettings { GraphicsQuality, PostFx, Vibration, Audio-Mixer-Levels }
  TutorialProgressDetail // Welche 3D-Touren wurden gesehen
  ReplayClips           // Liste lokaler Replays
  AssetCatalogVersion   // Für Remote-Catalog-Sync (Phase 2)
}
```

### 10.2 SaveMigrator

```csharp
public class HwiSaveMigrator : ISaveMigrator<HwiSave>
{
    public int CurrentSchemaVersion => 8;

    public HwiSave Migrate(HwiSave save, int fromVersion)
    {
        if (fromVersion < 2) save = MigrateV1ToV2(save);
        if (fromVersion < 3) save = MigrateV2ToV3(save);
        if (fromVersion < 4) save = MigrateV3ToV4(save);
        if (fromVersion < 5) save = MigrateV4ToV5(save);
        if (fromVersion < 6) save = MigrateV5ToV6(save);
        if (fromVersion < 7) save = MigrateV6ToV7(save);
        if (fromVersion < 8) save = MigrateV7ToV8(save);
        return save;
    }
}
```

### 10.3 Avalonia → Unity Save-Konverter (Phase 7)

Optional, wenn Spieler von Avalonia migrieren wollen:
- Avalonia-Save (JSON) wird hochgeladen / lokal eingelesen
- Konverter erstellt Unity-Save (V8)
- Migration in der App (One-Click)
- Belohnung: 100 GS als Migrations-Bonus

### 10.4 Save-Trigger

- Sofort nach Order-Complete
- Sofort nach Prestige
- Sofort nach Workshop-Kauf
- Sofort nach Worker-Hire
- Alle 30s (AutoSave)
- App-Pause/Background

### 10.5 Cloud-Save mit Conflict-Resolution

- Lokaler Save ist primär (Background-Thread, Lock-frei via UniTask; atomar Save/Backup/Temp)
- Cloud-Upload rate-limitiert (alle 2 min), Metadata-Preview + State-JSON unter `cloud_saves/{playerId}/`
- Konflikt: **Local-First** — SavedAt-Vergleich (+5s-Toleranz), Version-Outdated-Schutz; NICHT "Server wins"
- **User-Confirmation** bei Konflikt (Spieler entscheidet, welcher Stand gilt); Details [ARCHITECTURE.md § 8.4](ARCHITECTURE.md)

### 10.6 Sanitize / Anti-Cheat

- SaveGame-Sanitize-Service (aus Avalonia 1:1)
- Validiert Heirloom-Items, Worker-Stats, Level-Caps
- Entfernt Orphan-Reservierungen
- Validiert HMAC für kritische Werte (Money, Goldscrews)

---

## 11. Ökonomie & Live-Ops

### 11.1 Balancing-Werte (aus Avalonia, in ScriptableObject)

> **Verbindliche Werte:** [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) (`GameBalanceConstants`) + [DESIGN.md § 30](DESIGN.md#30-ökonomie-formel-zusammenfassung). Der ScriptableObject spiegelt **exakt** diese Konstanten — bei Abweichung gilt ORIGINAL_WERTE.

```csharp
[CreateAssetMenu(menuName = "HWI/BalancingConfig")]
public class BalancingConfig : ScriptableObject
{
    [Header("Workshop")]
    public float IncomeBaseMultiplier = 1.02f;        // 1.02^(Level-1)
    public float UpgradeCostExponent = 1.07f;         // Lv 2-500
    public float UpgradeCostReducedExponent = 1.06f;  // ab Lv 501
    public int   WorkshopMaxLevel = 1000;             // Rebirth-Trigger (NICHT 1500)

    [Header("Income Soft-Cap (MULTIPLIKATOR-Cap, KEIN €/s-Cap)")]
    // Schwelle ist eine Multiplikator-Einheit (Default 8.0), tier-/ascension-skaliert.
    // Dämpfung: softened = threshold + log2(1 + (mult - threshold)). Details: DESIGN § 30.2.
    public float SoftCapThresholdDefault = 8.0f;      // KEIN 8_000_000 — das war ein €-Wert-Fehler

    [Header("Prestige (Permanent-Mult-Bonus pro Tier, Hard-Cap 20x)")]
    public float PrestigeBronzeBonus = 0.20f;
    public float PrestigeSilverBonus = 0.35f;
    // ... bis Legende +8.00; MaxPermanentMultiplier = 20.0 (DESIGN § 9.3)

    [Header("Premium-Multipliers")]
    public float PremiumIncomeMultiplier = 1.5f;
    public float PremiumGoldenScrewMultiplier = 2.0f;

    [Header("Crafting")]
    public float CraftingSellMultiplier = 1.0f;       // Soft-Cap 8.0, Hard-Cap 12.0 (DESIGN § 30)
    public float MaterialAffinityBonus = 0.2f;        // +20% Crafting-Speed bei Match
}
```

**Income-Soft-Cap (verbindlich, DESIGN § 30.2):** Der Soft-Cap dämpft den **effektiven Multiplikator** (`grossIncome / TotalIncomePerSecond`), **nicht** einen absoluten €/s-Betrag. Schwelle ist tier-abhängig (None 4.0 … Legende 20.0, Ascension-Floor bis 30.0); `softened = threshold + log₂(1 + (mult − threshold))`.

**Offline-Income (verbindlich, 4-stufige Staffelung, DESIGN § 30.3):** `netPerSecond × (80% in 0–2h, 35% in 2–4h, 15% in 4–8h, 5% ab 8h)`. MaxOfflineHours: 4h Basis / 8h Video-erweitert / 16h Premium (+4h aus Prestige-Shop). Zeitmanipulations-Schutz: `lastPlayed > now → 0`, `offlineDuration < 60s → 0`.

**Editor-Tool:** "Balancing Dashboard" zum Live-Editieren (Hot-Reload).

### 11.2 Live-Ops via Remote Config

- BalancingConfig kann live übersteuert werden
- Event-Multiplier, Reward-Boosts, Live-Event-Termine
- A/B-Tests möglich (10% der Spieler bekommen anderen Wert)

### 11.3 GoldenScrew-Quellen (1:1 aus Avalonia)

- Mini-Games: 3-10 GS
- Daily Challenges: ~12 GS
- Achievements: 5-50 GS
- Rewarded Ad: 10 GS
- Daily Login: 1-25 GS
- Workshop-Milestones: 2-50 GS
- Premium verdoppelt Gameplay-Quellen (nicht IAP-Käufe)

### 11.4 IAPs (1:1 aus Avalonia)

Premium (`+50% Income`, `+100% GS` auf Gameplay-Quellen, keine Ads) entspricht dem Premium-Flag aus
ORIGINAL_WERTE. Goldschrauben-/Geld-Bundles und ihre konkreten Inhalte/Preise stammen aus dem
**Store-IAP-Katalog** (im Code nicht hartkodiert — vgl. [ORIGINAL_WERTE.md § 06](ORIGINAL_WERTE.md),
`battle_pass_season`, `DailyBundleSkus`). Die finale Bundle-Tabelle ist 1:1 aus dem produktiven
Play-Store-Katalog zu übernehmen, **nicht** zu erfinden.

| Bundle | Inhalt (Beispiel — gegen Live-Katalog verifizieren) |
|--------|-----------------------------------------------------|
| Premium | +50% Income, +100% GS (Gameplay), keine Ads (Premium-Flag, ORIGINAL_WERTE) |
| GS-Bundles | Goldschrauben-Pakete + Speed-Boost — Preise/Mengen aus Store-Katalog |
| Daily Bundle | 7-Slot-Rotation, RemoteConfig-getrieben (`DailyBundleSkus`), Default deaktiviert |
| Prestige-Pass / Battle-Pass | `battle_pass_season` (Consumable) — Preis aus Store |

### 11.5 Ads (1:1 aus Avalonia)

Über Google Mobile Ads Unity SDK. **Verbindliche Cooldowns/Reward-Werte (RemoteConfig, ORIGINAL_WERTE § 06):**
`golden_screws` 4h-Cooldown (`GoldenScrewAdReward = 8`), `shop_reward` 3h-Cooldown, max **3** Premium-Ad-Rewards/Tag.
Weitere Rewarded-Placements (1:1 aus Avalonia, exakte Liste gegen den AdReward-Enum verifizieren):
score_double, market_refresh, workshop_speedup, workshop_unlock, worker_hire_bonus, research_speedup,
daily_challenge_retry, achievement_boost, offline_double, rush_boost, lucky_spin.

### 11.6 Saison-System

- 30-Tage-BattlePass
- 4 Live-Events parallel pro Jahr
- Saison-Story-Chapters
- Saison-spezifische Cosmetics

---

## 12. Tools & Editor-Erweiterungen

### 12.1 First-Time-Setup-Wizard

Analog ArcaneKingdom — 3-Klick-Initialisierung:
1. BalancingConfig.asset erstellen
2. ScriptableObjects aus JSON importieren
3. Build-Scenes registrieren

**Menü:** `HandwerkerImperium → Setup → First-Time Setup Wizard`

### 12.2 DataImporter

JSON → ScriptableObject-Pipeline:
- Input: Bestehende Avalonia-JSON-Daten (kopiert nach StreamingAssets)
- Output: ScriptableObjects unter `Assets/_Project/ScriptableObjects/`
- Validierung: Konstanten-Bereiche, Referenzen, Cap-Checks

**Menü:** `HandwerkerImperium → Data → Import All`

### 12.3 BalancingDashboard

- Editor-Window
- Live-Editing aller BalancingConfig-Werte
- Hot-Reload im laufenden Spiel
- Export zu Firebase Remote Config

### 12.4 LocalizationCheckTool

- Prüft RESX-Vollständigkeit (6 Sprachen)
- Listet fehlende Keys
- Validiert Placeholder-Konsistenz

### 12.5 SaveGameEditor

- Liest persistentDataPath/save.json
- Tree-View für GameState
- Editable Fields
- Validation vor Save

### 12.6 CheatsWindow (Dev-Build only)

- Money setzen
- Workshop-Level setzen
- Achievement entsperren
- Prestige forcieren
- Live-Event triggern

### 12.7 PerformanceProfiler-Integration

- FrameDebugger-Hooks
- GPU-Frame-Time-Tracking
- Memory-Profile (Texture, Audio, Mesh)
- Build-Size-Reporter

### 12.8 BuildScripts

- `Build → Android Release` (signed AAB)
- `Build → Android Dev` (debug, unsigned)
- `Build → iOS Release` (Phase 2)
- `Build → Desktop Test` (Windows, für Editor-Testing)

### 12.9 CI/CD (GitHub Actions)

- Auto-Build bei Push auf `unity-main`
- AAB-Upload zu Internal Track
- Crashlytics-Symbol-Upload

---

## 13. Test-Strategie

### 13.1 Domain-Tests (Pure C#, NUnit)

**Coverage-Ziel:** >80% für Domain-Layer

**Erwartete Test-Klassen (200+ Tests):**
- `IncomeCalculatorTests` — Workshop-Income-Formeln, Soft-Cap, Multipliers
- `OrderGeneratorTests` — Order-Spawn-Logik, Live-Orders, Strategy-Multipliers
- `PrestigeRulesTests` — PP-Berechnung, Tier-Transitions, Heirloom
- `CraftingRulesTests` — Rezepte, Material-Affinität, Auto-Sell
- `WorkerStatsTests` — Mood, Fatigue, Effizienz-Formel
- `SaveMigratorTests` — V1→V8 Migration, Sanity-Checks
- `AchievementTriggerTests` — Achievement-Auslöser
- `DailyChallengeTests` — Challenge-Rotation, Tracking
- `WeeklyMissionTests` — Mission-Reset, Reward-Distribution
- `BattlePassTests` — Tier-Progression, Free/Premium
- `GuildBossTests` — Schadens-Verteilung, Reward
- `WorkerAuctionTests` — Bid-Logic, NPC-Bots, HMAC
- `CoopOrderTests` — Co-op-Score-Tracking, atomare Updates
- `HmacSignerTests` — Signing + Verification
- `ProfanityFilterTests` — DE/EN/ES/FR/IT/PT
- `EconomyTests` — Cross-Service-Integration

### 13.2 PlayMode-Tests (Unity-Test-Framework)

**Coverage-Ziel:** >50% für Game-Layer

- `GameLoopIntegrationTest` — Boot → Loop → AutoSave
- `WorkshopUpgradeFlowTest` — Buy → Upgrade → Income
- `OrderCompletionFlowTest` — Generate → Accept → Mini-Game → Reward
- `PrestigeFlowTest` — Vorher/Nachher-Balance
- `GuildJoinTest` — Firebase-Stub, Membership

### 13.3 UI-Tests (Optional, Phase 2)

- Unity Test Framework UI-Tests
- Screenshot-Vergleiche

### 13.4 Performance-Budgets

| Metrik | Ziel (Mobile, ARM64, 4GB-Phone) |
|--------|----------------------------------|
| FPS Hub-Idle | 60 |
| FPS Hub-Workshop-Detail (3D) | 60 |
| FPS Mini-Game | 60 |
| Memory (RAM) | <400 MB |
| Storage (APK) | <120 MB |
| Cold-Start | <3s |
| Warm-Start | <1s |
| First-Frame Time | <1.5s |

### 13.5 Manuelle QA-Checklisten

- 5-Min-Smoketest pro Build
- Pre-Release-QA: 2h-Spielsession durch Tester
- Geräte-Matrix: Pixel 6a, Samsung A23, Xiaomi Redmi 9 (Low-End-Referenz)

---

## 14. Build & Deployment

### 14.1 Android-Build

```bash
# CI/CD via Unity Cloud Build oder GitHub Actions
unity-builder \
  --projectPath Unity \
  --buildTarget Android \
  --androidAppBundle true \
  --androidKeystoreName meineapps.keystore \
  --androidKeystorePass MeineApps2025 \
  --androidKeyaliasName meineapps \
  --androidKeyaliasPass MeineApps2025 \
  --output Releases/HandwerkerImperium-Unity-v1.0.0.aab
```

### 14.2 Versionierung

- **MAJOR.MINOR.PATCH** (z.B. `1.0.0`)
- **bundleVersionCode:** Auto-inkrementiert in CI
- **GitTag:** `hwi-unity-v1.0.0`

### 14.3 Play-Store-Tracks

| Track | Frequenz | Tester |
|-------|----------|--------|
| Internal | Pro Commit | Dev-Team (5) |
| Closed Alpha | Wöchentlich | 20 Tester |
| Closed Beta | Bi-weekly | 100 Tester (3 Monate vor Launch) |
| Open Beta | Monatlich | Public (1 Monat vor Launch) |
| Production | Auf Knopfdruck | Global |

### 14.4 Migration-Strategie (Avalonia → Unity)

**Option A: Beide Versionen parallel im Play Store**
- Avalonia: Bestehende App-ID
- Unity: Neue App-ID (`com.meineapps.handwerkerimperium2`)
- Save-Konverter zum Migrieren

**Option B (empfohlen): Update der bestehenden App**
- Unity-Version wird auf bestehender App-ID released
- In-App Save-Migration beim ersten Start
- Avalonia-Version wird depubliziert (oder als Legacy belassen)

**Empfehlung:** Option A für Beta (Risiko minimieren), Option B für Production-Launch.

### 14.5 iOS (Phase 2, ab Q4 2026)

- Unity ist Cross-Platform → minimaler Mehraufwand
- Apple Developer Account erforderlich
- Apple Sign-In statt Google Sign-In als Default

---

## 15. Roadmap

### Übersicht

| Phase | Zeitraum | Meilenstein | Spielbar? |
|-------|----------|-------------|-----------|
| 1 | Monat 1-2 | Tech-Foundation | Boot-Scene |
| 2 | Monat 2-3 | Core-Loop-Prototyp | 1 Werkstatt + 1 Mini-Game |
| 3 | Monat 3-5 | Werkstätten + Worker + Orders | Alle 10 Werkstätten |
| 4 | Monat 5-7 | Forschung + Prestige + Crafting | Single-Player komplett |
| 5 | Monat 7-9 | Gilden + Multiplayer | Online-Features |
| 6 | Monat 9-11 | Polish: 3D-Visualisierung, Shader, Audio | Beta-Ready |
| 7 | Monat 11-12 | Beta + Launch | Production |

### Phase 1: Tech-Foundation (Monat 1-2)

**Ziele:**
- Unity-Projekt aufgesetzt (Unity 6, URP, IL2CPP, Android)
- 7 Assembly-Definitions
- VContainer-DI mit Boot-Scene
- Firebase-Auth + Anonymous Login
- Save-Service (Local + Cloud-Stub)
- Erstes ScriptableObject (BalancingConfig)
- DataImporter aus Avalonia-JSON

**Output:**
- Boot-Scene läuft
- Player kann sich anonym einloggen
- Save wird lokal gespeichert

### Phase 2: Core-Loop-Prototyp (Monat 2-3)

**Ziele:**
- Hub.unity mit 1 Werkstatt (Holzwerkstatt)
- 1 Mini-Game (Sawing, 2D-Version reicht erstmal)
- Order-System (Generate, Accept, Complete)
- Income-Calculation
- Simple UI (UI Toolkit)
- 1 Worker hired

**Output:**
- Spielbarer Loop: Order accepten → Mini-Game → Reward
- Money kann verdient werden
- Werkstatt-Upgrade funktioniert

### Phase 3: Werkstätten + Worker + Orders (Monat 3-5)

**Ziele:**
- Alle 10 Werkstätten implementiert (UI, Income, Upgrade)
- Alle 10 Worker-Tiers (Hire, Stats, Markt)
- Worker-Auktionen (Stub, ohne Firebase)
- Alle 6 Order-Types
- Live-Orders
- Order-Strategien (Safe/Standard/Risk)
- Equipment-System

**Output:**
- Single-Player-Kern komplett
- ~30% der Avalonia-Features

### Phase 4: Forschung + Prestige + Crafting (Monat 5-7)

**Ziele:**
- 72 Research-Nodes (4 Branches)
- 7 Prestige-Tiers
- Ascension-System
- 33 Crafting-Recipes (T1-T4)
- Warehouse mit 20-200 Slots
- Markt mit Tagespreis-Sinus
- 109 Achievements (17 Kategorien)
- Daily-Challenges, Weekly-Missions
- BattlePass (50 Tiers, 30-Tage-Saison)

**Output:**
- Single-Player-Komplett
- ~60% der Avalonia-Features

### Phase 5: Gilden + Multiplayer (Monat 7-9)

**Ziele:**
- Firebase Realtime DB Integration
- Gilden-CRUD
- Co-op-Orders (HMAC, atomar)
- Worker-Auktionen (Firebase + NPC-Bots)
- Boss-Kämpfe (6 Bosse)
- Hall-Gebäude (10)
- Kriegssaison
- Chat (mit Profanity-Filter)
- Cloud-Save mit Conflict-Resolution

**Output:**
- Multiplayer komplett
- ~90% Feature-Parität mit Avalonia

### Phase 6: Polish (Monat 9-11)

**Ziele:**
- 3D-Werkstattszenen (alle 10)
- 3D-Mini-Games (mindestens 5 von 10)
- Shader-Graph-Effekte
- Particle-System (Coin-Fly, Confetti, etc.)
- Post-Processing
- Cinemachine-Camera
- DOTween-Animationen
- AudioMixer mit Ducking
- Tutorial-Highlight-Overlays
- Achievement-3D-Trophäen
- Lokalisierung komplett (DE + EN + ES + FR + IT + PT)

**Output:**
- Beta-Ready (Visuell beeindruckend)
- 100% Feature-Parität + Unity-Sonderfeatures

### Phase 7: Beta + Launch (Monat 11-12)

**Ziele:**
- Closed Beta (100 Tester)
- Performance-Profiling auf 5 Geräten
- Bug-Fixes
- Open Beta
- Production-Release

**Output:**
- HandwerkerImperium-Unity v1.0.0 im Play Store

---

## 16. MVP-Definition

> Mechanik/Werte aller MVP-Inhalte sind verbindlich in [DESIGN.md](DESIGN.md)/[ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) festgelegt; "im MVP" = im Production-Launch enthalten.

### IM MVP (zum Production-Launch):

**Single-Player:**
- Alle 10 Werkstätten (Max-Level **1000**, Rebirth-Trigger; DESIGN § 4)
- Alle 10 Worker-Tiers
- Alle 6 Order-Types (Quick, Standard, Large, Coop, Weekly, Material)
- Live-Orders mit VIP-Multiplikator
- 3 Strategien (Safe/Standard/Risk)
- **Alle 10 Mini-Game-Renderer als 3D-Version** (Sawing, Pipes, Wiring, Painting, RoofTiling, Blueprint, DesignPuzzle, Inspection, Forge, Invent — 13 Enum-Typen, 10 Renderer) — kompromisslose 3D-Vision
- **72 Research-Nodes** (4 Branches)
- Prestige bis Tier 5 (Diamant) — Meister + Legende kommen post-Launch
- Ascension nicht im MVP (kommt 1 Monat nach Launch)
- **33 Crafting-Recipes** (T1-T4)
- Warehouse mit V7-Features
- Markt
- **109 Achievements** (17 Kategorien)
- Daily-Reward (30-Tage) + Lucky-Spin (8 Slots)
- 5 Daily-Challenge-Types
- 4 Weekly-Missions
- BattlePass: **50 Tiers**, 30-Tage-Saison (Free + Premium)
- Tutorial / FTUE (**10 Schritte**)

**Multiplayer:**
- Gilden (Create, Join, Manage; max **20** Mitglieder Basis)
- Co-op-Orders
- Worker-Auktionen (Gilden-Feature)
- Boss-Kämpfe (2 von 6 Bossen — Rest kommt nach)
- Hall-Gebäude (5 von 10)
- Chat
- Kriegssaison: **NICHT im MVP** (kommt 1 Monat nach Launch)
- Mega-Projekte: **NICHT im MVP** (kommt 2 Monate nach Launch)

**Online:**
- Firebase Auth (Anonymous + Google)
- Cloud-Save mit Sync
- Push-Notifications (8 Trigger)
- Anti-Cheat (HMAC + Cloud Functions)
- Live-Events (1 zum Launch)

**Visuell:**
- 3D-Hub-Szene mit allen Werkstätten
- Cinemachine-Camera
- Particle-System (Coin-Fly, Confetti, Sparkle)
- Post-Processing (Bloom, Vignette, Color Grading)
- Shader-Effekte (Workshop-Glow, Holographic-Worker, Money-Shimmer)
- DOTween-Animationen
- AudioMixer mit Ducking
- Haptic Feedback
- Prestige-Cinematic via Timeline
- Day/Night-Cycle: **NICHT im MVP** (Phase 2)
- Live-Wetter: **NICHT im MVP** (Phase 2)
- **Meister-Hans-Voice in allen 6 Sprachen** (ElevenLabs Standard-Voice, multilingual v2 — **kein** Voice-Cloning; siehe § 9.16)
- Worker-Tier-Voice-Lines: **NICHT im MVP** (separate Stimmen pro Worker — Phase 2)

**Lokalisierung:**
- Deutsch (primär, vollständig — inkl. Meister-Hans-Voice)
- Englisch (vollständig — inkl. Meister-Hans-Voice)
- ES, FR, IT, PT (Auto-Übersetzung + 1-2 Pässe Review — inkl. Meister-Hans-Voice via ElevenLabs Multilingual-Standard-Voice)

**Plattform:**
- Android API 24+ (ARM64)
- iOS: **NICHT im MVP** (Phase 2)
- Desktop: **NICHT im MVP** (Phase 3, falls Nachfrage)

### NACH MVP (Post-Launch in den ersten 3-6 Monaten):

- Ascension-System
- Prestige Meister + Legende
- Mega-Projekte (V7)
- Kriegssaison
- Boss 3-6
- Hall-Gebäude 6-10
- 5 weitere Mini-Games als 3D
- Day/Night-Cycle
- Live-Wetter
- Worker-Voice-Lines
- Saisonale Live-Events
- iOS-Launch
- Cosmetic-DLC

---

## 17. Risiken & Migrations-Pfad

### 17.1 Risiken

| Risiko | Impact | Mitigation |
|--------|--------|-----------|
| **Unity-Lernkurve im Team** | High | ArcaneKingdom-Patterns als Referenz, 2-Wochen-Bootcamp am Anfang, Unity Pro-Support |
| **3D-Asset-Pipeline aufwendig** | High | **KI-Pipeline (TRELLIS 2 + Blender + Mixamo)** statt klassisches Outsourcing → ~330 Asset-Slots in ~6-8 Wochen statt 6 Monate. Cloud-Fallback (Rodin Gen-2.5) für Hero-Assets. Vollständige Spec: [ASSETS_AI.md](ASSETS_AI.md). |
| **KI-Asset-Qualität reicht nicht für Hero-Assets** | Medium | Cloud-Polish via Rodin Gen-2.5 (5 Prestige-Cinematic-Assets, Cathedral, HQ-Skyscraper). Hand-Polish via Substance 3D Painter wo nötig. |
| **KI-Asset-Lizenz-Probleme (EU)** | Low | Bewusst **Hunyuan-frei** (EU-Lizenz-Ausschluss). TRELLIS 2 (MIT), SPAR3D (Stability Community), Stable Audio 3 (lizenzierte Trainingsdaten). Suno/Udio gemieden. Pro-Asset-Metadata mit Lizenz-Source. |
| **Avalonia-Save-Konverter fehlerhaft** | Medium | Migration-QA mit 100 echten Saves vor Launch, Backup-Strategy |
| **Firebase-Kosten steigen mit MAU** | Medium | Blaze-Plan mit Quotas, Monitoring-Alerts, Read-Optimierungen |
| **Build-Größe >150 MB** | Medium | Addressables + Remote Catalog (Phase 2), Texture-Compression-Audit |
| **Performance auf Low-End-Phones** | High | Quality-Settings (Low/Mid/High), 2D-Fallback für 3D-Effekte |
| **Doppelte Wartung Avalonia + Unity** | High | Avalonia in Maintenance-Mode (nur Bugfixes), Unity bekommt alle neuen Features |
| **Play Store Review-Verzögerung** | Low | Schrittweise Rollout-Strategy, Closed-Beta vor Production |
| **Save-Inkompatibilität** | High | Save-Migration-Tests + Backup vor Migration + Server-Side-Backup |
| **Spieler-Abwanderung in der Beta** | Medium | Daily-Reward, Migration-Bonus (100 GS), Migration-Storytelling |

### 17.2 Migrations-Strategie (entschieden)

**Gewählt: Closed Beta parallel zur Avalonia-Production**

- **Avalonia bleibt aktiv** in voller Entwicklung (neue Features, Bug-Fixes, Releases auf bestehender App-ID)
- **Unity-Version** ausschließlich in Closed Beta (eigene Play-Store-App-ID `com.meineapps.handwerkerimperium2.beta` oder Internal-Test-Track)
- **Kein Save-Konverter** in Phase 1 → Beta-Tester starten frisch (oder spielen beide parallel)
- **Entscheidung über finalen Cutover** wird vertagt: Erst wenn Unity-Beta stabil ist und Spieler-Feedback positiv → später Migration via Option A/B/C neu bewerten
- **Vorteile:** Keine Migrations-Bugs blocken Avalonia-User, Unity-Entwicklung ohne Zeitdruck, A/B-Vergleich beider Versionen möglich

**Konsequenzen für die Roadmap:**
- Avalonia-Codebase muss in Phase 1-6 NICHT eingefroren werden
- Beta-Tester können auch Avalonia weiter spielen ohne Konflikt
- Save-Konverter wird nicht zum MVP gebaut (spart 2-3 Wochen)
- Erste Unity-Build hat noch kein Multi-Device-Cloud-Save zu Avalonia (nicht nötig in Beta)

**Spätere Optionen (post-Beta):**

| Option | Beschreibung | Risiko |
|--------|-------------|--------|
| **A: Hard Cutover** | Avalonia depubliziert, Unity ersetzt | Hoch — Spieler-Verlust durch Bugs |
| **B: Parallel-Betrieb dauerhaft** | Avalonia im Maintenance, Unity als Premium-Variante | Niedrig — doppelte Wartung |
| **C: In-Place Update** | Unity ersetzt Avalonia auf gleicher App-ID via Update | Mittel — Migration-Bugs möglich |

→ **Entscheidung wird nach Closed Beta neu bewertet**, vermutlich Option B oder C.

### 17.3 Rollback-Plan

Falls Unity-Version kritische Bugs hat:
- Avalonia bleibt verfügbar (im Maintenance-Mode)
- Unity-Spieler können (mit Save-Backup) zurück zu Avalonia
- Hotfix-Pipeline: <24h von Bug-Report bis Play-Store-Update

---

## Anhang A: Vergleichs-Matrix Avalonia vs Unity

| Feature | Avalonia | Unity |
|---------|----------|-------|
| **Engine** | Avalonia 12 + .NET 10 + SkiaSharp | Unity 6 (LTS) + URP + IL2CPP |
| **UI** | XAML (Compiled Bindings) | UI Toolkit + uGUI |
| **3D** | Pseudo-3D in SkiaSharp | Native 3D |
| **Particles** | CPU-Pool (max 200) | GPU (10.000+) |
| **Shader** | C# hardcoded | Shader Graph |
| **Audio** | 3 Platform-Impls | AudioMixer (1 API) |
| **Animation** | CSS-Hacks + DoubleTransition | Animator + DOTween + Timeline |
| **Camera** | N/A (2D) | Cinemachine |
| **Post-Processing** | N/A | URP Post-FX |
| **DI** | Manual ServiceCollection (~91 Services) | VContainer Auto-Wiring |
| **Build** | 3 Projekte | 1 Projekt + Build-Targets |
| **Test** | xUnit (CalcLib only) | NUnit + Unity Test Framework |
| **Code-Size (gesamt)** | ~28.000 Zeilen C# (91 Services, 77 Models, 80 ViewModels, 74 Views) | ~30.000 Zeilen (geschätzt, inkl. Unity-UI) |
| **Firebase** | Custom REST-Client | Firebase Unity SDK |
| **Mobile-Performance** | Mittel (SkiaSharp-Stutter auf Low-End) | Hoch (Mobile-optimiert) |
| **Iteration-Speed** | Mittel (Recompile + XAML-Hotreload) | Hoch (ScriptableObject-Hotreload + Shader Graph) |
| **Asset-Pipeline** | WebP + Embedded Resources | Addressables + Remote Catalog |
| **Lokalisierung** | RESX | Unity Localization |
| **Plattformen** | Windows, Linux, Android | Windows, Mac, Linux, Android, iOS, WebGL |

---

## Anhang B: Konkrete erste Schritte

### Tag 1 (Setup)

1. Unity 6000.4.8f1 installieren (gleiche Version wie ArcaneKingdom)
2. Neues Unity-Projekt anlegen: `src/Apps/HandwerkerImperium.Unity/Unity/`
3. URP-Template wählen
4. Packages installieren (siehe 2.2)
5. Git: `unity-main`-Branch erstellen, Avalonia bleibt auf `master`

### Woche 1 (Foundation)

1. Assembly-Definitions anlegen (7 Module)
2. VContainer-DI mit Boot.unity
3. Firebase Auth (Anonymous)
4. Save-Service Stub
5. Erstes ScriptableObject: BalancingConfig
6. CLAUDE.md für HandwerkerImperium.Unity schreiben

### Woche 2-4 (Domain-Port)

1. Datenmodelle aus Avalonia portieren (113 Files → ~80 records)
2. Domain-Tests aus Avalonia portieren (Unit-Tests sind bereits unabhängig von UI)
3. IncomeCalculator, OrderGenerator, PrestigeRules portieren
4. DataImporter implementieren

### Woche 5-8 (Service-Port)

1. GameLoopService
2. WorkshopService
3. WorkerService
4. OrderService
5. AchievementService
6. SaveGameService mit Cloud-Sync

### Woche 9-12 (UI Bootstrap)

1. Hub.unity mit Header + 5 Tabs (UI Toolkit, statisch erstmal)
2. Erste Werkstatt-Detail-Ansicht (3D-Prefab)
3. Order-Akzept-Flow
4. Erstes Mini-Game (Sawing als 2D-Prototyp)

→ Danach iterativ pro Phase weiter.

---

## Anhang C: Asset-Liste (Schätzung)

### Asset-Strategie: KI-Pipeline statt Outsourcing

**Strategischer Wechsel:** Statt klassisches 3D-Outsourcing nutzen wir eine **lokal-laufende KI-Pipeline** (siehe [ASSETS_AI.md](ASSETS_AI.md) für volle Spezifikation). Das spart **~3-4 Monate Asset-Lieferzeit** und reduziert das Budget drastisch.

**Pipeline-Stack (EU-konform, kein Hunyuan):**
- **3D:** ComfyUI + TRELLIS 2 (Microsoft, MIT) + SPAR3D (Stability) + Blender 4.3+ + Mixamo (Auto-Rig)
- **Cloud-Polish:** Rodin Gen-2.5 für 5 Hero-Assets (Prestige-Cinematic, Mega-Projekte)
- **Texturing:** Adobe Substance 3D Sampler + Material-Decals für 5 Upgrade-Stufen pro Werkstatt
- **Audio:** Stable Audio 3 (Musik + SFX, lizenzierte Trainingsdaten)
- **Voice:** ElevenLabs Pro (Meister-Hans-Standard-Voice, multilingual v2 — **kein** Voice-Cloning; siehe § 9.16)
- **Animation:** Mixamo (Standard) + Cascadeur (Custom Mood-States)

**Asset-Übersicht (~330 unique + ~130 Re-Texture-Varianten):**

| Kategorie | Anzahl | KI-Pipeline | Aufwand |
|-----------|--------|-------------|---------|
| 10 Werkstätten + Sub-Module | 10 + 30 Module | TRELLIS 2 + Modular-Split | 2 Wochen |
| 50 Workshop-Upgrade-Decals (Lv 1-5 × 10) | 50 Material-Sets | Substance Sampler + ComfyUI | 1 Woche |
| 30 Workshop-Specialization-Skins | 30 (Material-Override) | Substance Sampler | 0.5 Wochen |
| 20 Worker-Basis (m/w × 10 Tiers) | 20 | TRELLIS 2 + Mixamo | 1.5 Wochen |
| 80 Worker-Mood-Face-Textures | 80 (Tex) | SDXL + ControlNet | 0.5 Wochen |
| 5 Worker-Affinity-Props | 5 | SPAR3D | 0.5 Wochen |
| 30 Crafting-Items (T1-T3 × 10) | 30 | SPAR3D + TripoSG | 1 Woche |
| 3 T4-Hero-Crafting (Villa, Skyscraper, HQ) | 3 | TRELLIS 2 + Cloud-Polish | 1 Woche |
| 12 Master-Tools mit Glow-Emissive | 12 | SPAR3D | 0.5 Wochen |
| 80 City-Tiles (10 Welten × 8) | 80 | TripoSG Batch (Nacht-Run) | 1 Woche |
| 30 Mini-Game-Props | 30 | SPAR3D / InstantMesh | 1 Woche |
| 2 Mega-Projekte mit 5 Bauphasen | 2 + 10 Phasen | Rodin Gen-2.5 (Cloud) | 1.5 Wochen |
| 5 Prestige-Cinematic-Hero | 5 | Rodin Gen-2.5 + Substance Painter | 1.5 Wochen |
| **3D-Summe** | **~380 Slots** | — | **~12 Wochen** |
| 10 BGM-Tracks | 10 | Stable Audio 3 | 0.5 Wochen |
| 150 SFX | 150 | Stable Audio Open Small | 1 Woche |
| 1500 Meister-Hans-Voice-Lines (6 Sprachen) | 1500 | ElevenLabs Standard-Voice (multilingual v2) | 2 Wochen |
| **Audio-Summe** | **~1660 Files** | — | **~3.5 Wochen** |

**Geschätzter monetärer Aufwand (Stand Mai 2026):**

| Posten | Kosten/Monat | Dauer | Gesamt |
|--------|--------------|-------|--------|
| Adobe Creative Cloud (Substance 3D Sampler/Painter) | 60 € | 4 Monate | 240 € |
| ElevenLabs Pro (Voice-Cloning) | 22 € | 4 Monate | 88 € |
| Rodin Gen-2.5 (Hero-Assets, 5-10 Assets/Monat) | 0 € (Free-Tier) bis 50 € | 4 Monate | 0-200 € |
| Cascadeur Indie (Free unter $100k Revenue) | 0 € | — | 0 € |
| ComfyUI + TRELLIS 2 + SPAR3D + Stable Audio | 0 € (lokal) | — | 0 € |
| Unity Pro (optional, für CI/Cloud-Build) | 185 € | 12 Monate | 2.220 € |
| Hardware-Strom (RTX 4090, ~400W über Pipeline-Zeiträume) | ~30 € | 4 Monate | 120 € |
| **Total Asset + Tools** | — | — | **~2.700-3.000 €** |

> **Ersparnis vs. Outsourcing:** ~14.000 € (klassisches 3D-Outsourcing wäre 17.000 € gewesen).

**Zeit-Vorteil:** Pipeline läuft parallel zur Code-Entwicklung — keine Lieferanten-Wartezeit, iterativ.

> Vollständige Pipeline-Spec, Pilot-Plan (5 Assets vor Skalierung), Risiken & Lizenz-Recherche: [ASSETS_AI.md](ASSETS_AI.md).

---

## Schluss

Dies ist ein vollständiger, gründlich durchdachter Plan. Nächste Schritte:

1. **Diesen Plan reviewen** und Anpassungen besprechen
2. **First-Time-Setup** durchführen ([SETUP.md](SETUP.md))
3. **Erstes Setup** (Unity-Projekt + Foundation, Woche 1)
4. **KI-Asset-Pilot starten** (5 Assets, parallel zur Code-Phase 1)

**Geschätzter Zeitraum:** 12 Monate vom Start bis Production-Launch (1 Entwickler + KI-Asset-Pipeline).

**Geschätzter Aufwand:** ~3.000 € Asset-Tools-Budget (Adobe CC + ElevenLabs + optional Rodin/Unity Pro). Pipeline-Spec: [ASSETS_AI.md](ASSETS_AI.md).
