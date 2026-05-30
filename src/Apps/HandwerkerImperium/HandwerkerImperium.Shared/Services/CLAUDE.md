# Services — Game-Loop, Domain-Logik, Gilden, Live-Ops

Alle Services sind **Singleton** (DI-Container). Services die Events abonnieren implementieren
`IDisposable` und werden über `App.DisposeServices()` → `ServiceProvider.Dispose()` aufgeräumt.
Generische Service-Patterns → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Service-Hierarchie (Lifecycle-Reihenfolge)

```
GameLoopService (1s-Takt, Partial: cs + Automation + PeriodicChecks + PrestigeCache)
├── GameStateService (lock + ExecuteWithLock, Partial: cs + Money + Xp + Workshop + Orders)
│   ├── SaveGameService (IO-Lock, AutoSave alle 30s, Background-Thread)
│   └── IncomeCalculatorService (Brutto/Netto-Berechnung: Prestige × Research × Events × MasterTools × Guild × VIP × SoftCap)
├── WorkerService (Mood-Decay, Fatigue, Training, Markt-Generierung)
├── OrderGeneratorService (6 OrderTypes, Live-Spawn, Stammkunden, MaterialOffer-Roll)
├── ResearchService (45 Nodes, Timer, Effekt-Cache)
├── EventService (8 Typen, Intervall-Skalierung per Tier)
├── AutoProductionService (Tier-1 alle 180s/Offset 90, Higher-Tier alle 360s/Offset 270)
├── GuildCoopOrderService (Firebase PATCH-atomar, HMAC-signiert)
├── WorkerAuctionService (Bid-Logik, NPC-Bots, Refund-Idempotenz)
└── GuildTickService (Facade über 5 Gilde-Tick-Services, 1 Dependency statt 5)
    ├── GuildBossService (60s/Offset 20)
    ├── GuildHallService (60s/Offset 40)
    ├── GuildAchievementService (300s/Offset 250)
    ├── GuildWarSeasonService (300s/Offset 260)
    └── WorkerAuctionService (Spawn 300s/Offset 90, NPC-Bot-Tick 5s/Offset 1)
```

---

## Kern-Services

| Service / Interface | Schlüssel-Verantwortung |
|---------------------|------------------------|
| `GameStateService` | Thread-sicherer Zugriff via `ExecuteWithLock(Action)`. Partials: Money, Xp, Workshop, Orders. Externe Callbacks für GS-Bonus + ChallengeConstraints (vermeidet DI-Zirkel) |
| `SaveGameService` | JSON-Load/Save, V1→V7-Migration, IO-Lock per `SemaphoreSlim`. AutoSave alle 30s auf Background-Thread via `Task.Run + ExecuteWithLock` |
| `GameLoopService` | 1s DispatcherTimer. Offsets verhindern Frame-Spikes. Paused/Resumed via App-Lifecycle |
| `IncomeCalculatorService` | Zentrales Income/Cost. `GetTotalHeirloomBonus` summiert Run-Erbstücke + Ascension-Permanent |
| `GameIntegrityService` | SaveGame-Sanitize, Orphan-Reservierungs-Bereinigung, HeirloomItems-Validierung |

---

## Coordinator-Services

| Service | Aus MainViewModel extrahiert | Schlüssel-Pattern |
|---------|------------------------------|------------------|
| `GameStartupCoordinator` | `MainViewModel.Init.cs` | Lädt Spielstand, Cloud-Save-Abgleich, Sprach-Sync, GameLoop-Start. `MainViewModel.InitializeAsync()` ist nur Forwarder |
| `ProgressionFeedbackCoordinator` | `MainViewModel.EventHandlers.cs` | Subscribed selbst auf Level/GoldenScrews/Xp/Workshop/Worker/MasterTool/Achievement/Prestige-Events. Feuert FloatingText/Celebration/Sound über `IUiEffectBus` |
| `GameTickCoordinator` | `MainViewModel.GameTick.cs` (alt) | Per-Tick-UI-Updates an Feature-VMs, tab-spezifisch gated |
| `CinematicCoordinator` | `MainViewModel.EventHandlers.cs` | Subscribed auf `IPrestigeService.CinematicReady`, steuert Music-Track + View-Trigger |
| `ReputationTierEffects` | `MainViewModel.OnReputationTierChanged` | FloatingText + Celebration + Audio + Achievement-Dialog |

---

## Navigation-Services

| Service | Verantwortung |
|---------|--------------|
| `NavigationService` | `NavigateToRoute(string)`, alle `SelectXxxTab`-Methoden |
| `DialogOrchestrator` | Back-Press → Dialog-Dismiss-Kaskade |
| `MiniGameNavigator` | MiniGame-Route-Mapping, QuickJob/Tournament-Abbruch |

---

## Facaden (Service-Bündel)

| Facade | Bündelt | Konsument |
|--------|---------|-----------|
| `IGuildFacade` | 7 Gilde-Services (Guild, Invite, Research, Chat, WarSeason, Boss, Hall, Tip, Achievement) | `GuildViewModel` |
| `IGuildTickService` | 5 Tick-Services (Boss, Hall, Achievement, WarSeason, WorkerAuction) | `GameLoopService` |
| `IMissionsFacade` | 5 Mission-Services (DailyChallenge, WeeklyMission, LuckySpin, QuickJob, Goal) | `MissionsFeatureViewModel` |

Facaden sind reines Pass-Through — kein State, nur Constructor-Injection-Reduktion.

---

## IUiEffectBus

Zentraler Singleton-Bus für FloatingText / Celebration / Ceremony. Auslöser (MainViewModel,
Coordinators, Feature-VMs) injizieren `IUiEffectBus` und rufen `RaiseXxx(...)` auf.
Views abonnieren den Bus direkt im Code-Behind (analog `IFrameClock`).

---

## IFrameClock

`FrameClockService` als 30Hz-Render-Tick. Subscriber-Pattern (idempotent), Stopwatch-DeltaSeconds,
Auto-Stop bei 0 Subscribern, Pause/Resume für App-Lifecycle.

---

## Gilde-Services (Firebase Realtime Database)

| Service | Zweck |
|---------|-------|
| `GuildService` | CRUD, Wochenziele, Mitglieder, Rollen, Stale-Bereinigung |
| `GuildInviteService` | 6-stellige Invite-Codes, Spieler-Browser, Einladungs-Inbox. Delegiert Beitritte an `IGuildService.JoinGuildAsync` (kein doppelter Lock) |
| `GuildResearchService` | 18 Forschungen, Timer, Effekt-Cache, `SemaphoreSlim` für Thread-Safety + Firebase-Rollback bei ContributeAsync |
| `GuildWarSeasonService` | Saison-Krieg, Matchmaking, Scoring, Ligen |
| `GuildBossService` | 6 Boss-Typen, Schadens-Tracking, Spawn/Despawn |
| `GuildHallService` | 10 HQ-Gebäude, Upgrade-Timer, Effekt-Cache |
| `GuildTipService` | Kontextuelle Tipps, 24h-Cooldown |
| `GuildAchievementService` | 30 Achievements (10 Typen × 3 Tiers) |
| `GuildCoopOrderService` | Firebase CRUD, HMAC-Signierung, PATCH-atomar. Rewards via `TryClaimCompletedReward` (idempotent via `ClaimedCoopOrderIds`) |
| `WorkerAuctionService` | Bid-Logik (10% Mindest-Erhöhung, 1s-Cooldown), HMAC, Refund-Idempotenz via `ClaimedAuctionIds`. NPC-Bots (35% Chance/Tick). Master-Client-Pattern: lexikografisch kleinste PlayerId spawnt Auktionen |
| `GuildMegaProjectService` | Material-Spenden-Pipeline. HMAC-signiert, PATCH-atomar, Sunset-Regel 30 Tage |

---

## Live-Ops-Services

| Service | Zweck |
|---------|-------|
| `LiveEventService` | 4 Templates: DoubleReward / BossRush / CoopMarathon / MiniGameMastery. RemoteConfig-getrieben, 3-Tier-Reward |
| `LiveEventScoreTracker` | Verdrahtet `AddScore()` mit OrderCompleted/PerfectRatingIncremented. `IDisposable`, Container-Dispose |
| `SeasonalEventService` | 4 saisonale Events/Jahr, SP-Währung, Event-Shop |
| `BattlePassService` | 30-Tier Saison (30 Tage), Free/Premium-Track, XP-Boost |
| `WhatsNewService` | Versionierter Feature-Dialog. RESX-Key-Miss gibt Key-Namen zurück (nie null) — `L(key, default)`-Helper erkennt den Miss |
| `RemoteConfigService` | `DefaultsRemoteConfigService` liest eingebettete Config — App funktioniert ohne Firebase-Backend |
| `CloudSaveService` | Local-First, Push-Debounce 5s |
| `FirebaseService` | Firebase REST: Anonymous Auth + Realtime Database |

---

## Domänen-Services

| Service | Schlüssel-Detail |
|---------|-----------------|
| `PrestigeService` | PP-Formel, Tier-Auswahl, Challenges-Constraints, Heirloom-Selektion VOR Reset |
| `ChallengeConstraintService` | Consumer-Services fragen `IChallengeConstraintService.IsXxxAllowed()` ab |
| `CraftingService` | Stack-Limit-Prüfung VOR Start + VOR Collect. Cross-Workshop-Inputs ab Spielerlevel 100 |
| `WarehouseService` | `EffectiveSlotCount` = SlotCount + Research-Bonus. `Available = Inventory - ReservedInventory` |
| `MarketService` | Deterministischer Tagespreis-Seed: `PlayerGuid ^ utcDay ^ productId`. ±50% Sinus-Welle, Spread 5% |
| `EternalMasteryService` | Permanenter Bonus: +0.5%/Prestige linear + 5er/10er-Stufen-Boni. Kein Reset bei Ascension |
| `MiniGameMasteryService` | Bronze/Silver/Gold-Mastery pro MiniGame-Typ, subscribed auf `PerfectRatingIncremented`. Wird eager in `App.axaml.cs` aufgelöst |
| `FtueService` | 10-Step State-Machine, Analytics-Hooks, persistiert in `GameState.Tutorial.Ftue` |
| `FtueProgressTracker` | Verdrahtet FTUE-Fortschritt mit Game-Events (ohne Tracker schreitet FTUE nicht voran) |
| `DailyBundleService` | Tages-Bundle-Foundation (UI-Wiring in Folge-Sprint) |
| `NotificationCenterService` | Bell-UI-Benachrichtigungsliste. Alle Mutationen via `ExecuteWithLock` (verhindert "Collection was modified") |

---

## Gotchas

| Problem | Ursache | Lösung |
|---------|---------|--------|
| Service-Caches stale nach Prestige/Import/Reset | Services mit internen Caches subscriben nicht auf `StateLoaded` | ALLE Services mit Caches MÜSSEN `StateLoaded += ResetCaches` im Konstruktor haben |
| SaveAsync UI-Thread-Freeze | `JsonSerializer.Serialize` blockierte UI-Thread | Serialize auf Background-Thread via `Task.Run + ExecuteWithLock` |
| Co-op Score: Last-Write-Wins | `SetAsync` (PUT) überschreibt anderen Submitter | `UpdateAsync` (PATCH) — atomar nur eigenes Score-Feld |
| GuildInviteService: doppelter Lock | `JoinGuildAsync` direkt in `GuildInviteService` statt Delegation | IMMER an `IGuildService.JoinGuildAsync` delegieren |
| Rewarded Ad Belohnung kommt nicht an | `LoadAndShowAsync`-Timeout deckt Laden + Anzeige | `CancellationTokenSource` nur für Lade-Phase, bei Ad-Load gecancelled |
| LuckySpin Timer-Leak bei Exception | `OnSpinTick`-catch stoppte Timer nicht | Timer stoppen + Event unsubscriben + `IsSpinning=false` im catch |
| NotificationCenterService "Collection was modified" | Parallele Mutations-Aufrufe ohne Lock | Alle 5 Mutationen auf `_gameStateService.ExecuteWithLock(...)` |
