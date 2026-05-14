# HandwerkerImperium — Idle-Game

Handwerker-Imperium aufbauen: Werkstätten kaufen und upgraden, Arbeiter einstellen, Aufträge abarbeiten,
forschen, Gilden beitreten. Idle-Einkommen läuft passiv weiter, Mini-Games bringen aktive Belohnungen.

| Aspekt | Wert |
|--------|------|
| Aktuelle Version | v2.1.0 (VersionCode 50) |
| Status | Produktion |
| Package-ID | com.meineapps.handwerkerimperium |
| Ads | Banner + Rewarded (13 Placements) |
| Premium | 4,99 EUR Lifetime |

Für generische Build-Befehle, Conventions und Troubleshooting: [Haupt-CLAUDE.md](../../../CLAUDE.md)

---

## Architektur

### Projekt-Struktur

```
HandwerkerImperium.Shared/
├── App.axaml(.cs)                   # DI-Registrierung aller Services + ViewModels
├── Loading/
│   └── HandwerkerImperiumLoadingPipeline.cs  # Startup-Sequenz (parallel: Icons, Shader, Purchases, RemoteConfig, DailyBundle)
├── Models/
│   ├── GameState.cs                 # Root-Persistenz-Objekt (Version 6)
│   ├── GameBalanceConstants.cs      # Alle Balancing-Zahlen an einem Ort
│   ├── LevelThresholds.cs          # Alle Level-Gates (Feature-Unlocks, Tabs, Automation)
│   ├── Enums/                       # ActivePage, WorkshopType, PrestigeTier, OrderType, ...
│   └── Firebase/                    # Firebase-DTOs (FirebaseGuildData, FirebaseGuildMember, ...)
├── Services/
│   ├── Interfaces/                  # Alle Service-Interfaces
│   ├── GameStateService.cs          # Zentraler State-Zugriff mit lock (ExecuteWithLock)
│   ├── GameLoopService.cs           # 1s-Takt DispatcherTimer, AutoSave alle 30s
│   ├── SaveGameService.cs           # JSON-Load/Save mit V1→V6-Migration, IO-Lock
│   └── ...                          # 40+ weitere Services
├── ViewModels/
│   ├── MainViewModel.cs             # Partial-Split über 13 Dateien (Routing-/Composition-Layer)
│   ├── HeaderViewModel.cs           # Source-of-Truth: Money, Level, GoldenScrews, ...
│   ├── PrestigeBannerViewModel.cs   # Prestige-Banner-Properties (18)
│   ├── DialogViewModel.cs           # Alle Dialog-States, implementiert IDialogService
│   ├── MissionsFeatureViewModel.cs  # Daily Challenges, Weekly Missions, QuickJobs, LuckySpin
│   ├── EconomyFeatureViewModel.cs   # Workshop-Kauf/Upgrade, Aufträge, Rush
│   ├── Guild/                       # GuildViewModel + 9 Sub-VMs (ViewLocator-Konvention)
│   └── MiniGames/                   # BaseMiniGameViewModel + 10 konkrete VMs
├── Views/
│   ├── MainView.axaml               # 5-Tab-Navigation, Dialoge als UserControls
│   ├── DashboardView.axaml          # 423 Z., Header+City+Workshop-Grid (Code-Behind)
│   ├── Dashboard/                   # AutomationPanel, BannerStrip, OrdersQuickJobsSection,
│   │                                #   DailyChallengeSection, WeeklyMissionSection
│   ├── ImperiumView.axaml           # 171 Z., Sub-Tab-Router (kein Code-Behind)
│   ├── Imperium/                    # WorkshopsSection, ResearchSection, WorkersSection,
│   │                                #   EquipmentSection, AscensionSection (5 Sub-Tabs)
│   ├── Dialogs/                     # Dialog-UserControls (AchievementDialog, StoryDialog, ...)
│   ├── Guild/                       # GuildView + Sub-Views (Research, Boss, Hall, War, ...)
│   └── MiniGames/                   # 10 MiniGame-Views
├── Controls/
│   └── EmptyStateCard.axaml         # Wiederverwendbarer Empty-State (Icon + Title + Subtitle + ActionButton)
├── Graphics/                        # ~35 SkiaSharp-Renderer (alle IDisposable)
├── Icons/
│   ├── GameIconKind.cs              # Enum mit 224 Werten
│   ├── GameIcon.cs                  # Custom Control (erbt PathIcon, StyleKeyOverride)
│   └── GameIconRenderer.cs          # SkiaSharp-Renderer für Icons auf Canvas
└── Helpers/
    ├── AsyncExtensions.cs           # RunHandlerSafely (ersetzt 14 async-void-Handler)
    ├── ProfanityFilter.cs           # Chat + Namen (DE/EN/ES/FR/IT/PT, Play Store)
    ├── PageNavigationHelper.cs      # CappedNavigationStack (O(1)-Ringbuffer)
    └── WorkshopCardHitTester.cs     # Koordinaten-Mapping für Workshop-Card-Touches
```

### MainViewModel Partial-Split (13 Files)

**MainViewModel.cs: 478 Zeilen**, gesamter Partial-Split ~2960 Z. (urspruenglich 2483 Z.,
zwischenzeitlich auf 4161 Z. ueber 14 Files angewachsen). Die echte Feature-Logik (Startup,
Progression-Feedback, Welcome-Flow, Per-Tick-Orchestrierung, UI-Effekte) liegt in
eigenstaendigen Coordinator-Services + Feature-VMs — siehe "MainViewModel-Extraktion" unten.
Was im MainViewModel bleibt: Composition (Ctor + Service-Wiring), Binding-Anker (Properties,
Tab-State, RelayCommand-Forwarder) und 5 Host-Interface-Implementierungen.

| Datei | Z. | Inhalt |
|-------|----|----|
| `MainViewModel.cs` | 478 | Service-Felder, Konstruktor, Coordinator-/Host-Wiring |
| `MainViewModel.EventHandlers.cs` | 401 | Verbleibende Handler: Money, Order, Lieferant, Event-System, Cinematic, Reputation, State-Loaded, Premium, Sprachwechsel |
| `MainViewModel.Properties.cs` | 399 | ObservableProperties, Computed-Properties, Events, Child-VM-Exposures |
| `MainViewModel.Tabs.cs` | 368 | ActivePage-Enum, IsXxxActive, ActivePageContent, Imperium-Sub-Tabs |
| `MainViewModel.Navigation.cs` | 296 | Tab-Auswahl-Commands, HandleBackPressed, Child-Navigation-Routing |
| `MainViewModel.Helpers.cs` | 182 | FormatMoney, UpdateNetIncomeHeader, UpdateWorkerWarning, Money-Animation, EternalMastery-Refresh |
| `MainViewModel.Dialogs.cs` | 171 | Dialog-Weiterleitungen, Prestige-Durchfuehrung, Notification-Center-Routing |
| `MainViewModel.Lifecycle.cs` | 169 | PauseGameLoop, ResumeGameLoop, OnLiveOrderSpawned, Dispose |
| `MainViewModel.Host.cs` | 163 | 5 Host-Interface-Implementierungen (Navigation/WelcomeFlow/Startup/ProgressionFeedback/GameTick) |
| `MainViewModel.Economy.cs` | 161 | RelayCommand-Forwarder zu EconomyFeatureViewModel |
| `MainViewModel.Automation.cs` | 132 | Automation-Property-Wrapper (GameState.Automation), Reputation-Tier-Properties |
| `MainViewModel.Missions.cs` | 28 | LuckySpin-Overlay-Steuerung |
| `MainViewModel.Init.cs` | 15 | InitializeAsync-Forwarder an GameStartupCoordinator |

### DialogViewModel Struktur

`DialogViewModel.cs` ist der Coordinator + Confirm-Dialog-Properties. Pro Dialog-Typ
gibt es eine Partial-Datei. Prestige-Tier-Auswahl ist als **eigenständige VM**
herausgezogen (P0, 12.05.2026, "echter Strukturschnitt").

| Datei | Zeilen | Inhalt |
|-------|--------|--------|
| `DialogViewModel.cs` | 269 | Service-Felder, Konstruktor, Confirm-Dialog-Properties, ShowAlert/Confirm, IsAnyDialogVisible, ShowPrestigeSummary, Reputation-Info, Cleanup, Delegations an PrestigeConfirmation |
| `DialogViewModel.Achievement.cs` | 25 | Achievement-Dialog (Partial) |
| `DialogViewModel.Alert.cs` | 29 | Alert-Dialog (Partial) |
| `DialogViewModel.Hint.cs` | 106 | Hint-Dialog (Partial) |
| `DialogViewModel.LevelUp.cs` | 30 | LevelUp-Dialog (Partial) |
| `DialogViewModel.PrestigeSummary.cs` | 45 | Post-Prestige-Summary (Partial) |
| `DialogViewModel.Story.cs` | 152 | Story-Dialog (Partial) |
| **`PrestigeConfirmationViewModel.cs`** | **380** | **Eigenständige VM (kein Partial)** — Prestige-Tier-Auswahl, Composition-Property auf DialogVM. XAML-Bindings: `DialogVM.PrestigeConfirmation.X` |

### IFrameClock (zentraler Render-Tick)

Service `IFrameClock` + `FrameClockService` als 30Hz-Render-Tick fuer Visual-Renderer.
Subscriber-Pattern (idempotent), Stopwatch-DeltaSeconds, Auto-Stop bei 0 Subscribern,
Pause/Resume fuer App-Lifecycle. Foundation fuer schrittweise Migration der ~35
existierenden Renderer-Timer auf den zentralen Clock.

**ActivePage-Pattern**: Eine einzige `ActivePage`-Enum-Property ist Source-of-Truth. Alle
`IsXxxActive`-Properties sind berechnete Properties, die darauf basieren. Kein `DeactivateAllTabs()`.

### Service-Hierarchie (Lifecycle)

Alle Services sind Singletons. DI-Container owned die Dispose-Aufrufe.
`App.DisposeServices()` ruft kritische Services explizit in Reihenfolge auf, dann kaskadiert
`Services as IDisposable`.

```
GameLoopService (1s-Takt, partial: cs + Automation + PeriodicChecks + PrestigeCache)
├── GameStateService         (lock + ExecuteWithLock, partial: cs + Money + Xp + Workshop + Orders)
│   ├── SaveGameService      (IO-Lock, AutoSave alle 30s, Background-Thread)
│   └── IncomeCalculatorService  (zentrales Income/Cost-Berechnung)
├── WorkerService            (Mood, Fatigue, Training, Markt-Generierung)
├── OrderGeneratorService    (6 OrderTypes inkl. Quick+MaterialOrder, Live-Spawn, Stammkunden)
├── ResearchService          (45 Nodes, Timer, Effekt-Cache)
├── EventService             (8 Typen, Intervall-Skalierung per Tier)
├── AutoProductionService    (Tier-1 alle 180s/Offset 90, Higher-Tier alle 360s/Offset 270)
├── GuildCoopOrderService    (Firebase Co-op-Aufträge, PATCH-atomar, HMAC-signiert)
├── WorkerAuctionService     (Auktionen, NPC-Bots, Bid-Idempotenz)
└── GuildTickService         (Facade über 5 Gilden-Tick-Services, 1 Dependency statt 4)
    ├── GuildBossService         (60s/Offset 20)
    ├── GuildHallService         (60s/Offset 40)
    ├── GuildAchievementService  (300s/Offset 250)
    ├── GuildWarSeasonService    (300s/Offset 260)
    └── WorkerAuctionService     (Spawn 300s/Offset 90, NPC-Bot-Tick 5s/Offset 1)
```

### ViewLocator-Konvention (Guild-Sub-VMs)

`HandwerkerImperium.ViewModels.Guild.GuildResearchViewModel`
→ Namespace-Ersatz `.ViewModels.` → `.Views.`
→ `HandwerkerImperium.Views.Guild.GuildResearchView`

Thin-Wrapper-Pattern: Sub-VM hat nur `GuildViewModel Guild { get; }`, Bindings via `{Binding Guild.X}`.

### Feature-ViewModels (Source-of-Truth, kein MainViewModel-State)

| ViewModel | Properties | Kommunikation zurück | Instanziierung |
|-----------|-----------|----------------------|----------------|
| `HeaderViewModel` | 16 (Money, Level, GoldenScrews, XP, ...) | PropertyChanged-Forward in MainVM | DI Singleton |
| `PrestigeBannerViewModel` | 18 (IsPrestigeAvailable, Preview-Props, ...) | dito | DI Singleton |
| `GoalBannerViewModel` | CurrentGoal + NavigateToGoalCommand | INavigationService direkt | DI Singleton |
| `WelcomeFlowViewModel` | 14 Props + Welcome-Flow-Logik (Offline-Earnings, Daily-Reward, Starter-Offer, verzoegerte Dialog-Kaskade) — `WelcomeFlowViewModel.Logic.cs` | `IWelcomeFlowHost`-Bruecke (IsHoldingUpgrade + NavigateToShop) | DI Singleton |
| `MissionsFeatureViewModel` | Daily/Weekly/QuickJobs/LuckySpin-Props | NavigateToMiniGameRequested | DI Singleton |
| `EconomyFeatureViewModel` | Workshop/Order/Rush-Commands | FloatingTextRequested, CelebrationRequested | `new` in MainViewModel.Economy.cs (KEIN DI) |
| `DialogViewModel` | 45 Props, implementiert IDialogService | Events an MainViewModel | DI Singleton |

### Navigation-Services

| Service | Verantwortung |
|---------|--------------|
| `INavigationService` + `NavigationService` | `NavigateToRoute(string)`, alle SelectXxxTab-Methoden |
| `IDialogOrchestrator` + `DialogOrchestrator` | Back-Press → Dialog-Dismiss-Kaskade |
| `IMiniGameNavigator` + `MiniGameNavigator` | MiniGame-Route-Mapping, QuickJob/Tournament-Abbruch |
| `INavigationHost` | Host-Ref-Interface, MainViewModel implementiert explizit |

### DI-Registrierung (App.axaml.cs)

- Services → Singleton (70+ Services)
- MainViewModel → Singleton
- Child-VMs → Singleton (HeaderVM, PrestigeBannerVM, DialogVM, MissionsFeatureVM, GuildCoopOrderVM, WorkerAuctionVM, NotificationCenterVM, ReputationShopVM, ...)
- Guild-Sub-VMs (GuildWarSeasonVM, GuildBossVM, GuildHallVM) → Singleton
- `EconomyFeatureViewModel` → per `new` in `MainViewModel.Economy.cs` erstellt (KEIN DI, braucht mainVM-Kontext)
- Thin-Wrapper-VMs (GuildResearchVM, ...) → im GuildViewModel-Ctor erstellt (kein DI-Container)

**Service-Container-Facaden** (Service-Sprawl-Reduction, 12.05.2026):
Bündeln verwandte Services für Konsumenten die sonst 3-9 einzelne Dependencies
injizieren müssten. Pure Pass-Through-Container, kein State.

| Facade | Bündelt | Primärer Konsument |
|--------|---------|---------------------|
| `IGuildFacade` | 9 Gilden-Services (Guild, Invite, Research, Chat, WarSeason, Boss, Hall, Tip, Achievement) | GuildViewModel |
| `IWorkerFacade` | Worker + WorkerAuction | (neu — additiv) |
| `IProgressionFacade` | Prestige + Rebirth + Ascension + EternalMastery + ReputationShop | (neu — additiv) |
| `IMissionsFacade` | DailyChallenge + WeeklyMission + LuckySpin + QuickJob + Goal | (neu — additiv, MissionsFeatureViewModel-Kandidat) |

Worker/Progression/Missions sind additiv eingeführt — bestehende Konsumenten der Einzel-Services
funktionieren unverändert. Neue Code-Stellen können optional die Facade injizieren.

---

## Game-Mechaniken

### 5-Tab Navigation

| Tab | Index | View | Inhalt |
|-----|-------|------|--------|
| Werkstatt | 0 | DashboardView | City-Szene, Workshop-Karten, Automation-Panel, Quick-Jobs |
| Imperium | 1 | ImperiumView | Gebäude, Crafting+Research, Workers/Manager/MasterTools, Lager, Prestige |
| Missionen | 2 | MissionenView | Heute (Daily, Quick-Jobs, Glücksrad) + Wettbewerbe (Weekly, Turnier, BattlePass) |
| Gilde | 3 | GuildView | 5-Tab-Hub (Übersicht/Kampf/Forschung/Chat/Mitglieder) |
| Shop | 4 | ShopView | IAP, Goldschrauben-Pakete, Ausrüstungs-Shop |

**Imperium-Sub-Tabs** (ImperiumSubTab Enum, V7): Workshops / **Warehouse** / Workers / Research / Equipment / Ascension.
Warehouse-Tab gesperrt bis Spielerlevel 50, Ascension-Tab gesperrt bis `LegendeCount >= 3` — beide IMMER sichtbar (Lock-Icon-Overlay statt Ausblenden).

### Game Loop (GameLoopService, 1s-Takt)

```
Jede Sekunde:
  Idle-Einkommen (IncomeCalculatorService: Prestige × Research × Events × MasterTools × Guild × VIP × SoftCap)
  Kosten abziehen (Worker-Löhne, Gebäude)
  Worker-States (Mood-Decay, Fatigue, Training-Fortschritt, Kündigung bei Mood<20)
  Offline-Kündigung: Worker kündigen nach 24h bei Mood<20 (konsistent mit Online)
  AutoSave alle 30s (JSON, Background-Thread via Task.Run + ExecuteWithLock)

Alle 3 Ticks: ExpireOldLiveOrders (Early-Exit wenn LiveOrderCount == 0)
Alle 5 Ticks: Automation (AutoCollect, AutoAccept) (Offset 3)
Alle 10 Ticks: Lieferant-Check
Alle 25 Ticks: Live-Auftrag spawnen (50% Chance, max 5 parallel) (Offset 17)
Alle 60 Ticks: QuickJob-Rotation, Order-Expiry, WeeklyMission-Check (Offset 15), AutoAssign (Offset 30), MasterSmith-Materialien (Offset 45)
Alle 120 Ticks: Manager-Unlock-Check (Offset 60), MasterTool-Check
Alle 180 Ticks: AutoProduktion Tier-1 (Offset 90)
Alle 300 Ticks: Event-Check, Saison-Check (Offset 150), BattlePass-Check (Offset 200)
Alle 360 Ticks: Auto-Craft höherer Tiers (Offset 270)
Jeder Tick: GuildTickService.ProcessTick() → Boss (60s/Offset 20), Hall (60s/Offset 40),
             Achievements (300s/Offset 250), WarSeason (300s/Offset 260),
             Auktions-Spawn (300s/Offset 90), NPC-Bot-Tick (5s/Offset 1)
```

**IsBusy-Guard**: `private bool _isBusy` + try/finally in GuildVM, SettingsVM, ShopVM, WorkerMarketVM
für alle async-Methoden gegen Doppel-Tap-Race.

### Werkstätten-System

**10 Workshop-Typen**: Carpenter, Plumber, Electrician, Painter, Roofer, Contractor, Architect,
GeneralContractor, MasterSmith, InnovationLab

Spezial-Effekte:
- MasterSmith: 60s Auto-Produktion (Standard 180s), passive Crafting-Materialien
- InnovationLab: 120s Auto-Produktion, verdoppelt Research-Geschwindigkeit

**Workshop-Spezialisierung** (ab Level 50, erste Wahl gratis, Re-Spec 20 GS):

| Typ | Einkommen | Kosten | Worker-Effizienz | Worker-Slots |
|-----|-----------|--------|-----------------|--------------|
| Efficiency | +30% | — | — | −1 |
| Quality | — | +15% | +20% | — |
| Economy | −5% | −25% | — | — |

**Farben**: Carpenter=#A0522D, Plumber=#0E7490, Electrician=#F97316, Painter=#EC4899, Roofer=#DC2626,
Contractor=#EA580C, Architect=#78716C, GeneralContractor=#FFD700, MasterSmith=#D4A373, InnovationLab=#6A5ACD

`WorkshopTypeExtensions.GetColorHex()` ist die einzige Farb-Quelle. Alle Consumer leiten davon ab:
`WorkshopCardRenderer.GetWorkshopColor()`, `WorkshopSceneRenderer`, `WorkshopColorConverter`, `WorkshopGameCardRenderer`.

**Workshop Rebirth** (0–5 Sterne, permanent über Prestige+Ascension):

| Sterne | Einkommens-Bonus | Upgrade-Rabatt | Extra Worker | GS-Kosten |
|--------|-----------------|----------------|-------------|-----------|
| 1 | +15% | −5% | +1 | 100 |
| 2 | +35% | −10% | +1 | 250 |
| 3 | +60% | −15% | +2 | 500 |
| 4 | +100% | −20% | +2 | 500 |
| 5 | +150% | −25% | +3 | 1000 |

### Arbeiter-System

**10 Tiers** (F/E/D/C/B/A/S/SS/SSS/Legendary), Löhne ~1.8x pro Tier.

EffectiveEfficiency-Formel:
`BaseEff × XpBonus(+3%/Lv) × MoodFactor × FatigueFactor × (1+SpecBonus+EquipBonus) × PersonalityMult × TalentBonus(1★=1.0x..5★=1.20x)`

**Hiring-Flow**: Marktpreis = `TierBasis × Level × Talent(0.7–1.3x) × Persönlichkeit × Spezialisierung`.
`HiringCost` wird persistiert (`[JsonPropertyName("hiringCost")]`) — Marktpreise bleiben nach Neustart korrekt.

**S-Tier+**: Erst nach Research `mgmt_10` (UnlocksSTierWorkers) im Pool sichtbar.
`mgmt_04` (UnlocksHeadhunter) erhöht Pool-Größe 5→8.

**Aura-Cap**: S-Tier+ Worker geben Aura-Bonus (5–20%), gedeckelt bei 50% gesamt
(`GameBalanceConstants.MaxAuraBonus`).

**3 Training-Typen**: Efficiency (XP→Level→+Effizienz), Endurance (−Fatigue max 50%),
Morale (−MoodDecay max 50%). Auto-Rest bei 100% Fatigue → Auto-Resume nach Erholung.

**Praktikanten**: IsIntern=true, kostenlos, F-Tier, max 2 gleichzeitig. Promotion nach 86400 aktiven Ticks
(24h Spielzeit) zu E-Tier (Lohn-pflichtig). `InternAwaitingPromotion`-Flag → Status-Override in WorkerProfileVM.

**Legende-Prestige**: Top 3 Worker pro Workshop gesichert (Keys: "Type", "Type_1", "Type_2").

**Worker-Markt-Gewichtung**: F=20, E=22, D=22, C=14, B=10, A=6, S=3, SS=1.5, SSS=0.5, Legendary=0.1

### Auftragstypen (OrderType)

| Typ | Freischaltung | Belohnung | Aufgaben | Besonderheit |
|-----|---------------|-----------|----------|-------------|
| Quick | Immer | 0.6x | 1 | Schnellauftrag, kein Deadline |
| Standard | Immer | 1.0x | 2–3 | Basis |
| Large | Level 10+ | 1.8x | 4–6 | Mehr Aufgaben |
| Cooperation | Level 15+, ≥2 Workshops | 2.5x | 3 (gemischt) | Mehrere Werkstatt-Typen |
| Weekly | Level 20+ | 3.0x | 10 | 7-Tage-Deadline |
| MaterialOrder | Level 50+ (AutoProductionUnlockLevel) | 1.8x | 0 | Kein MiniGame, Items liefern, 4h Deadline |

**Stammkunden**: 20% Chance, BonusMultiplier 1.1–1.5x.

**Live-Aufträge**: `ExpiresAt` 45–180s. VIP (5% Spawn-Chance): 3x Reward, 2.5x XP, kürzere Deadline.
Live-Orders können pausiert werden (`PausedAt`, `AccumulatedPauseDuration`, 5-Minuten-Cap gegen Bunkern).

**Parallele Aufträge**: Bis zu 3 gleichzeitig (`GameState.ParallelOrdersByWorkshop`, Dictionary<WorkshopType, Order>).
`ActiveOrder` = Vordergrund-Slot (aktiv im MiniGame). `ResumeParallelOrder()` tauscht Vordergrund.

**Risk/Reward-Strategie** (pro Auftrag wählbar vor MiniGame-Start):

| Strategie | Reward | MiniGame | Miss-Handling |
|-----------|--------|----------|---------------|
| Safe | 0.75x | +50% breitere Zonen, +30% Zeit | Normales Rating |
| Standard | 1.0x | Baseline | Normales Rating |
| Risk | 2.0x | −50% Zonen, +30% Tempo, −30% Zeit | 0 Reward + −10 Reputation |

### Prestige-System (7 Tiers)

**PP-Formel**: `floor(sqrt(CurrentRunMoney / 100_000))` — nur Geld des aktuellen Durchlaufs.

**Tier-Boni (Basis)**: Bronze +20%, Silver +35%, Gold +50%, Platin +100%, Diamant +200%,
Meister +400%, Legende +800%.

**Diminishing Returns**: `baseBonus × 1/(1 + 0.1 × tierCount)`. Cap 20x.

**Verschärfte Bewahrung**:

| Tier | Was bleibt erhalten |
|------|---------------------|
| Bronze/Silver | Achievements, Premium, Settings, PrestigeData, Tutorial |
| Gold+ | + Research |
| Platin+ | + Prestige-Shop Items |
| Diamant+ | + MasterTools |
| Meister+ | + Gebäude (Level→1) + Equipment |
| Legende | + Manager (Level→1) + beste Worker (Top 3/WS) |

**Herausforderungen** (max 3 gleichzeitig, PP-Boni stacken additiv):

| Challenge | Effekt | PP-Bonus |
|-----------|--------|----------|
| Spartaner | Max 3 Worker | +45% |
| OhneForschung | Keine Forschung | +30% |
| Inflationszeit | Doppelte Upgrade-Kosten | +25% |
| SoloMeister | Nur 1 Workshop | +50% |
| Sprint | Kein Offline-Einkommen | +35% |
| KeinNetz | Keine Lieferanten | +20% |

`ChallengeConstraintService` → Consumer-Services fragen `IChallengeConstraintService` ab.
SoloMeister + QuickStart: inkompatibel (Toggle wird abgelehnt).

**Bonus-PP** (NACH Tier-Multiplikator):

| Bedingung | Bonus-PP | Cap |
|-----------|----------|-----|
| Je 10 Perfect Ratings | +1 PP | max +5 |
| Volle Research-Branch | +2 PP | max +6 |
| Alle 7 Gebäude Lv5 | +1 PP | — |
| Pro Level über Tier-Min | +0.05 PP | max +5 |

**Meilensteine** (kumulativ, überleben Ascension):

| Prestiges | ID | GS |
|-----------|-----|-----|
| 1 | pm_first | 10 |
| 5 | pm_5 | 20 |
| 10 | pm_10 | 35 |
| 25 | pm_25 | 50 |
| 50 | pm_50 | 75 |
| 100 | pm_100 | 100 |

**Wiederholbarer Wochen-Meilenstein**: Alle 7 Prestiges +5 GS
(`PrestigeData.PrestigesSinceLastWeeklyReward`).

### Ascension-System (Meta-Prestige nach 3× Legende)

6 permanente Perks (AP-basiert, je MaxLevel 3, 54 AP gesamt). +2 AP pro Ascension-Skalierung.
Vollständiger Reset inkl. Prestige-Daten.

### Reputation-System

`CustomerReputation` (0–100, Start 50). Beeinflusst Auftragsbelohnungen (0.7x–1.5x).

**Tier-System** (`CustomerReputationTier`): Beginner(0–30), CityKnown(31–60), RegionStar(61–80),
IndustryLegend(81–100).

**Hysterese**: `FromScoreWithHysteresis(score, currentTier)` mit 3-Punkte-Buffer (Up bei 31/61/81,
Down bei 28/58/78). Verhindert UI-Flackern an Tier-Boundaries. `CurrentTier` wird persistiert.

**Quellen**: `AddRating()` bei Auftragsabschluss (MiniGame-Rating → 1–5 Sterne),
Showroom-Gebäude (0.5–2.5/Tag), `DecayReputation()` langsamer Abbau >50 (1/Tag).

**Tier-Up-Celebration**: FloatingText + Confetti + Sound + Achievement-Dialog (nur bei Aufstieg).

**Reputation-Shop** (sichtbar ab Score ≥ 60, 5 Items):
Stammkunden-Garantie (30 Rep), Schnelle Lieferung (20 Rep), Worker-Mood-Boost (25 Rep),
Workshop-Skin Holz-Premium (100 Rep, kosmetisch), Reputation-Insurance (40 Rep, nächster Risk-Miss gratis).

### Events / Feierabend-Rush

**8 zufällige Events**:
- TaxAudit: 10% Steuer auf Brutto (dauerhaft während Event)
- WorkerStrike: Alle Stimmungen −20 (einmalig beim Start)
- HighDemand/MaterialShortage: betreffen zufälligen Workshop-Typ
- MarketRestriction: Nur Tier C und niedriger
- (weitere saisonale)

**Intervall-Skalierung**: Kein Prestige 8h/30%, Bronze 6h/35%, Silver 4h/40%, Gold+ 3h/50%.

**Feierabend-Rush**: 2h 2x-Boost, 1x/Tag gratis, danach 10 GS. Stackt mit SpeedBoost (bis 4x).

**Saisonale Events** (4/Jahr): SP-Währung (5+Bonus pro Auftrag), Event-Shop (6 Items).
Bei aktivem Event: CityWeatherSystem zeigt saisonales Wetter mit 2x Partikel-Dichte.

### Gilden-Forschungssystem

18 Forschungen in 6 Kategorien, kollaborativ (Mitglieder tragen Geld bei), permanente Boni.

| Kategorie | IDs | Kosten-Range | Effekte |
|-----------|-----|-------------|---------|
| Infrastruktur | guild_expand_1/2/3 | 50M–5B | Max. Mitglieder +5/+5/+10 |
| Wirtschaft | guild_income_1/2/3/4 | 10M–10B | +Einkommen, −Kosten, +Auftragsbelohnungen |
| Wissen | guild_knowledge_1/2/3 | 25M–2.5B | +XP, +Worker-Effizienz, +MiniGame-Belohnungen |
| Logistik | guild_logistics_1/2/3 | 75M–3B | +Auftragsslot, +Order-Qualität, +Belohnungen |
| Arbeitsmarkt | guild_workforce_1/2/3 | 150M–5B | +Worker-Slot, +Training-Speed, −Ermüdung |
| Meisterschaft | guild_mastery_1/2 | 500M–7.5B | +Research-Speed, +Prestige-Punkte |

**Effekt-Integration**: GuildMembership-Properties gecacht, Consumer-Services fragen davon ab.
`GuildResearchService` (SemaphoreSlim Thread-Safety, Firebase-Rollback bei ContributeAsync).

### Gilden-Architektur (Firebase Realtime Database)

**PlayerId** (GUID) ist stabile Spieler-Identität. Überlebt Firebase-Account-Wechsel und Geräte-Wechsel.
Alle Datenbankpfade verwenden `PlayerId`, NICHT `Uid`.

**Firebase-Identitätssystem**:
1. Preferences (`player_id`) — höchste Priorität
2. GameState.PlayerGuid — Backup
3. Neue GUID generieren

**auth_to_player-Mapping**: `/auth_to_player/{uid}` → PlayerId. Nach jedem Token-Refresh geschrieben.
Security Rules verwenden dieses Mapping für PlayerId-basierte Autorisierung.

**Firebase-Rules-Deployment**:
```bash
npx firebase-tools deploy --only database --project handwerkerimperium-487917
```

**Sub-Services hinter IGuildFacade**:

| Service | Zweck |
|---------|-------|
| `GuildService` | CRUD, Wochenziele, Mitglieder, Rollen, Duplikat-/Stale-Bereinigung |
| `GuildInviteService` | 6-stellige Invite-Codes, Spieler-Browser, Einladungs-Inbox. Beitritts-Ops delegieren an `IGuildService.JoinGuildAsync` (kein doppelter Lock) |
| `GuildResearchService` | 18 Forschungen, Timer, Effekt-Cache, SemaphoreSlim |
| `GuildWarSeasonService` | Saison-Krieg, Matchmaking, Scoring, Ligen |
| `GuildBossService` | 6 Boss-Typen, Schadens-Tracking, Spawn/Despawn |
| `GuildHallService` | 10 HQ-Gebäude, Upgrade-Timer, Effekt-Cache |
| `GuildTipService` | Kontextuelle Tipps, 24h-Cooldown |
| `GuildAchievementService` | 30 Achievements (10 Typen × 3 Tiers) |

**Gilden-Multiplayer** (Co-op + Auktionen):
- `GuildCoopOrderService`: Firebase-CRUD, HMAC-Signierung (stabile Felder only), PATCH statt PUT
  (atomar, kein Last-Write-Wins). Rewards via `TryClaimCompletedReward` (idempotent über `ClaimedCoopOrderIds`).
- `WorkerAuctionService`: Bid-Logik (10% Mindest-Erhöhung, 1s-Cooldown), HMAC, Refund-Idempotenz
  über `ClaimedAuctionIds`. NPC-Bots (35% Chance/Tick, tier-spezifisches Maximum).
- Master-Client-Pattern: Spieler mit lexikografisch kleinster PlayerId spawnt Auktionen (deterministisch).

### Meisterwerkzeuge (12 Artefakte)

5 Seltenheiten (Common/Uncommon/Rare/Epic/Legendary), permanente Einkommens-Boni, gesamt +74%.
`MasterToolUnlocked` Event → FloatingText + Celebration. Prüfung alle 2 Minuten im GameLoop.

| ID | Seltenheit | Bonus | Bedingung |
|----|-----------|-------|-----------|
| mt_golden_hammer | Common | +2% | Workshop Lv.75 |
| mt_diamond_saw | Common | +2% | Workshop Lv.150 |
| mt_titanium_pliers | Common | +3% | 150 Aufträge |
| mt_brass_level | Common | +3% | 300 Minispiele |
| mt_silver_wrench | Uncommon | +5% | Workshop Lv.300 |
| mt_jade_brush | Uncommon | +5% | 75 Perfect Ratings |
| mt_crystal_chisel | Uncommon | +5% | Bronze Prestige |
| mt_obsidian_drill | Rare | +7% | Workshop Lv.750 |
| mt_ruby_blade | Rare | +7% | Silver Prestige |
| mt_emerald_toolbox | Epic | +10% | Workshop Lv.1500 |
| mt_dragon_anvil | Epic | +10% | Gold Prestige |
| mt_master_crown | Legendary | +15% | Alle 11 Tools |

### Lieferant-System

Zufällige Lieferungen alle 2–5 Minuten. 5 Typen: Geld (35%), GS (20%), XP (20%), Mood-Boost (15%),
Speed-Boost (10%). 2 Minuten Abholzeit.

### Auto-Produktions-System

Alle 10 Workshops produzieren passiv Tier-1 Items (Unlock ab WS-Level 50):

| Workshop | Intervall | Items/h (5 Worker) |
|----------|-----------|---------------------|
| Standard (8 WS) | 180s/Worker | 100 |
| InnovationLab | 120s/Worker | 150 |
| MasterSmith | 60s/Worker | 300 |

Skalierender Verkaufspreis: `BaseValue × (1 + log₂(1 + Level/25)) × CraftingSellMultiplier`
(kein Soft-Cap, kein Speed/Rush im Multiplier).

### Gilden-Mega-Projekte (V7 — , Plan Section 3.9)

`IGuildMegaProjectService` + `GuildMegaProjectService` — wochenlange Material-Spenden-Pipeline
mit permanenter Gildenbonus-Belohnung.

**2 Mega-Projekt-Templates** (`GuildMegaProjectTemplates`):
- **Cathedral**: 50× luxury_furniture, 40× roof_structure, 30× artwork, 20× smart_home, 1× villa.
  Belohnung: +5% Crafting-Speed, +10% Auto-Verkaufs-Preis, +3 Lager-Slots — permanent fuer alle Mitglieder.
- **Headquarters**: 80× skyscraper_frame, 60× smart_home, 50× bathroom, 30× master_blueprint,
  30× masterpiece_fittings, 2× villa, 1× skyscraper. Belohnung: +10% / +20% / +5 Slots.

**Firebase-Pfad**: `guilds/{guildId}/megaProjects/active` (single-active per Gilde).
- HMAC-signiert ueber stabile Felder (`ProjectId`, `Type`, `CreatedAt`).
- Spende: `UpdateAsync` (PATCH) atomar — nur die Subpfade Contributions, Donations werden geschrieben.
- Sunset-Regel (Plan Section 4 Risiken): Projekte aelter als 30 Tage werden geblockt
  (`AbandonmentSunsetDays`), neue Spenden faellen ab.
- `state.ClaimedGuildProjectIds` verhindert Doppel-Belohnung pro Spieler.

**Boni-Integration**:
- `GuildMembership.MegaProjectCraftingSpeedBonus` flieskt in `CraftingService.StartCrafting` ein.
- `GuildMembership.MegaProjectAutoSellPriceBonus` modifiziert den Marktpreis im Overflow-Auto-Sell.
- `GuildMembership.MegaProjectBonusWarehouseSlots` addiert sich zu `WarehouseService.EffectiveSlotCount`.

**UI**:
- `GuildBuildSiteView` als eigene Seite (ActivePage.GuildBuildSite, Route `guild_build_site`).
- Erreichbar ueber neue Karte im Combat-Tab der GuildView.
- Drei Donate-Stufen pro Material (1 / 10 / Alles), Top-Spender-Leaderboard (Top 5),
  Fortschrittsbalken pro Anforderung + Gesamt-%, Bonus-Vorschau.

### Tier-4 + Erbstuecke + Worker-Affinitaet (V7 — )

**Tier-4-Produkte** (Plan Section 3.2): 3 Imperiums-Manufaktur-Items am GeneralContractor
ab WS-Lv 500. Alle haben `IsHeirloomEligible = true`.

| Produkt | Inputs | BaseValue | Dauer |
|---------|--------|-----------|-------|
| villa | 5×luxury_furniture + 3×smart_home + 2×roof_structure + 1×artwork | 2.5 Mio. | 30 min |
| skyscraper | 5×skyscraper_frame + 3×bathroom + 3×smart_home + 2×artwork | 4.0 Mio. | 40 min |
| imperium_hq | je 2× alle 10 T3-Produkte (au&szlig;er general_contract: 1×) | 5.0 Mio. | 60 min |

**Erbstuecke** (Plan Section 3.8):
- Beim Prestige werden Tier-4-Items aus `HeirloomItems` (max 3) NICHT gerese ttet — sie ueberleben den Run.
- Jedes aktive Erbstueck gibt **+2% Globales Einkommen** im naechsten Run.
- Bei Ascension wandern alle Erbstuecke in `state.Ascension.PermanentHeirlooms` → **+0.5% forever** pro Stueck.
- `IncomeCalculatorService.GetTotalHeirloomBonus(state)` summiert beide Beitraege.
- `PrestigeService` filtert das Crafting-Inventar bei Reset: nur HeirloomItems mit `IsHeirloomEligible == true` bleiben.
- `ReservedInventory.Clear()` beim Prestige (alle laufenden Material-Offer-Reservierungen verfallen).
- SaveGame-Sanitize validiert HeirloomItems gegen den Produkt-Katalog (Save-Editor-Schutz).

**Worker-Material-Affinitaet** (Plan Section 3.7):
- `MaterialAffinity` Enum: Wood/Metal/Stone/Art/Tech.
- `Worker.MaterialAffinity` neu — wird beim Hiring gleichverteilt gerollt (20% pro Achse).
- Alte Worker bekommen die Affinitaet deterministisch via WorkerId-Hash (SaveGame-Migration).
- `CraftingService.GetMaterialAffinityBonus(state, recipe)` addiert bis zu **+20% Crafting-Speed** wenn
  alle arbeitenden Worker des Workshops mit der Material-Affinitaet des Output-Produkts matchen.
- Anteilig pro Worker (3 von 5 matchend → +12% Speed).
- `MaterialAffinityExtensions.GetMaterialAffinity(productId)` ordnet jedes Material einer Achse zu.

**Imperium-Pass (4,99 € Lifetime)** (Plan Section 10.2): Der bestehende Premium-Kauf wird
inhaltlich zum "Imperium-Pass" repositioniert (Preis identisch, Versprechen greifbarer).
Beinhaltet ×2 Rewarded-Belohnungen, +50% Offline-Einkommen, Markt-Insider-Heatmap,
Auto-Verkaufs-Regeln, +1 Erbstueck-Slot (3 → 4), 2× Lucky-Spin/Tag, Auto-ClaimDaily,
+100% GS. Spieler mit bestehendem `IsPremium` bekommen den Pass automatisch.
*Implementation der UI-Repositionierung ist als naechster Schritt — Bundle-Boni
sind in den Service-Layern bereits implementiert.*

**Heirloom-Wahl-UI** (Plan Section 3.8): PrestigeView zeigt ueber dem Confirm-CTA eine
Heirloom-Selection-Sektion mit ItemsControl + Toggle-Buttons + IsSelected-Indikator. Top-N
nach BaseValue wird automatisch vorselektiert (Cap aus `GetEffectiveHeirloomSlots(IsPremium)`).
`PrestigeConfirmationViewModel.ApplyHeirloomSelection()` schreibt die Wahl in
`state.HeirloomItems` VOR `_prestigeService.DoPrestige(...)`, sodass der Reset sie bewahrt.
RESX-Keys: `HeirloomSlotsFormat`, `HeirloomSectionTitle`, `HeirloomSectionHint` (6 Sprachen).
XAML-Bindings nutzen `ElementName=PrestigeRoot` + `((vm:MainViewModel)DataContext).` Cast
fuer das Command (analog zur Tier-Auswahl). Background/Border werden ueber zwei dedizierte
`BoolToBrushConverter`-Resourcen in `App.axaml` gestyled (`HeirloomSelectedBgConverter`,
`HeirloomSelectedBorderConverter`) — der Core-Library-Converter unterstuetzt keinen
ConverterParameter, daher pro Use-Case eine Resource-Instanz.

### Material-Markt + Heatmap-Detail (V7 — )

`IMarketService` + `MarketService` — deterministische Tagespreis-Logik pro Spieler.

**Preis-Engine**:
- Seed: `PlayerGuid.GetHashCode() ^ utcDay ^ productId.GetHashCode()` → pro Spieler/Tag/Material individuell.
- Sinus-Welle ueber 24h mit ±50% Amplitude um den BaseValue. Phase-Offset aus dem Seed.
- Event-Modulatoren: `MaterialShortage` ×3, `HighDemand` ×2 fuer Produkte aus dem `AffectedWorkshop`.
- Spread: Verkauf = Kauf × 0.95 (5% Maklergebuehr — verhindert Sofort-Arbitrage).

**Markt-Zugang**: Nur sichtbar wenn `logi_05` Research abgeschlossen. UI zeigt sonst Locked-Hint.

**Lifecycle**:
- `TryBuy(productId, count)`: Stack-Limit-Check via `WarehouseService.CanAddToInventory`,
  Geld-Abzug, Inventar-Add. Bei Slot-Voll wird kein Kauf moeglich (kein Geld-Verlust).
- `TrySell(productId, count)`: Verkauft nur nicht-reserviertes Material (ReservedInventory ausgeschlossen).

### Logistik-Forschungsbranch (V7 — )

Neuer 4. `ResearchBranch.Logistics` (Amber #D97706, Package-Icon). 12 Nodes:

| Node | Effekt | Voraussetzung |
|------|--------|---------------|
| logi_01 | +5 Lager-Slots | — |
| logi_02 | Stack-Limit ×2 | logi_01 |
| logi_05 | Markt freigeschaltet | logi_02 |
| logi_04 | +10 Lager-Slots | logi_05 |
| logi_08 | Lieferanten-Material-Bonus +50% | logi_04 |
| logi_07 | Auto-Verkaufs-Regeln freigeschaltet | logi_08 |
| logi_10 | Crafting-Speed +20% | logi_07 |
| logi_11 | Stack-Limit ×5 | logi_10 |
| logi_09 | T4-Rezepte freigeschaltet () | logi_11 |
| logi_03 | +25 Lager-Slots | logi_09 |
| logi_12 | Erbstuecke ueberleben Prestige () | logi_03 |
| logi_06 | Crafting-Speed +30% + 25 Slots | logi_12 |

**Integration**:
- `WarehouseService.EffectiveSlotCount = state.WarehouseSlotCount + BonusWarehouseSlots` (aus Research).
- `WarehouseService.CurrentStackLimit = state.WarehouseStackLimit × StackLimitMultiplier` (max 9999).
- `CraftingService.StartCrafting` addiert `CraftingSpeedBonus` aus Research zu Prestige-Shop-Bonus.
- Markt-Verfuegbarkeit: `MarketService.IsMarketAvailable` prueft `logi_05.IsResearched`.

### Lieferant-Material-Variante (V7 — )

`SupplierDelivery.GenerateRandom` rollt mit 25% Chance (ab Spielerlevel 50) eine
`DeliveryType.Material`-Lieferung statt Geld. 1–10 Stueck eines zufaelligen Tier-1-Materials
aus den freigeschalteten Workshops. Research `logi_08` `SupplierMaterialBonus` erhoeht
die Menge proportional.

### Material-Offer in Auftraegen (V7 — )

**Mechanik**: Jeder regulaere Auftrag (ausser MaterialOrder) kann beim Spawn ein optionales
Material-Angebot bekommen. Wenn der Spieler die geforderten Materialien liefert, gibt es einen
Bonus-Multiplikator auf Reward + XP.

**Spawn-Regeln** (in `OrderGeneratorService.TryRollMaterialOffer`):
- Spielerlevel >= `GameBalanceConstants.MaterialOfferUnlockLevel` (= 30).
- 35% Chance pro Auftrag (`MaterialOfferChance`).
- Pool je nach OrderType (Plan Section 3.3): Quick 1×T1 +25%, Standard 2×T1 +30%, Large 1×T2+3×T1 +40%, Cooperation 2×T2 (Cross-Workshop) +50%, Weekly 1×T3+2×T2 +60%.
- MaterialOrder hat eigene Logik (RequiredMaterials) — kein zusaetzliches Offer.

**Lifecycle**:
1. `Order.MaterialOffer` + `MaterialOfferBonusMultiplier` werden im Generator gesetzt.
2. Spieler waehlt im UI zwischen "Start" und "Mit Material" (zweiter Button erscheint nur
   wenn `HasMaterialOffer == true`).
3. `IGameStateService.TryAcceptMaterialOffer(order)` reserviert die Materialien atomar in
   `ReservedInventory` und setzt `MaterialOfferAccepted = true`. Bei nicht ausreichend
   verfuegbarem Material wird Alert gezeigt und der Auftrag NICHT gestartet.
4. Bei Order-Complete (`CompleteActiveOrder`):
   - `MaterialOfferAccepted == true` → Bonus × (1 + Multiplier) auf Money/XP, Material wird
     consumed (CraftingInventory + ReservedInventory atomar reduziert).
   - Bei HardFail (Risk-Miss) bleibt der Bonus 0, aber Material wird trotzdem konsumiert
     ("echtes Risiko", siehe Plan Section 3.3).
5. Bei CancelActiveOrder: Reservierung wird freigegeben, kein Verbrauch.
6. Bei Order-Expiry (GameLoop): Reservierung wird freigegeben.
7. Bei SaveGame-Sanitize: Orphan-Reservierungen (Reserved > Summe ActiveOrder+ParallelOrders)
   werden geloescht.

### Crafting & Warehouse (V7 — )

**Rezept-Pool**: 30 Rezepte (10 Workshops × 3 Tiers). Jeder Workshop hat T1/T2/T3, freigeschaltet bei
Workshop-Level 50/150/300. **Cross-Workshop-Inputs** an T2/T3-Rezepten greifen erst ab Spielerlevel
100 (`GameBalanceConstants.MaterialOrderCrossWorkshopLevel`). Unter dem Schwellwert filtert
`CraftingRecipe.GetEffectiveInputs()` Cross-Inputs raus (Onboarding-Schutz).

**Lager-System** (`IWarehouseService`):
- 20 Slots Start, Stack-Limit 50 pro Slot. Upgrade-Pfad +5 Slots, Basis-Kosten 50.000 €, Faktor 1.5x.
- Max-Cap: 200 Slots (`WarehouseService.MaxSlots`).
- Verfuegbarkeit-Berechnung: `Available = CraftingInventory[id] - ReservedInventory[id]` —
   reserviert akzeptierte Auftraege gegen Doppelverbrauch.
- Auto-Sell-Regel pro Slot: Bei Stack-Overflow Auto-Verkauf zum aktuellen Marktpreis statt Pause.
- Ohne Auto-Sell: Workshop pausiert (Event `WorkshopPaused`), UI zeigt gelben Warn-Badge.

**Stack-Safety**:
- `CraftingService.StartCrafting()` prueft Output-Stack-Limit vor Job-Start (kein Material-Burn).
- `CraftingService.CollectProduct()` prueft Stack-Limit bei Einsammeln (Job bleibt completed bei vollem Lager).
- `AutoProductionService.AutoCraftHigherTiers()` prueft Output-Limit vor jedem Auto-Craft.
- `SellProducts()` schliesst reservierte Mengen aus — Spieler kann nur freies Material verkaufen.

**Save-Migration V6→V7**: Defaults setzen, ueberlaufende Stacks auf Limit kuerzen, Differenz × BaseValue
als Geld auszahlen (kein Wert-Verlust).

### SaveGame-Versionen

| Version | Beschreibung |
|---------|-------------|
| 1 | Legacy (Altes Worker-System) |
| 2 | Neues Worker-System, Buildings, Research, Events, Prestige, Reputation |
| 3 | Workshop Rebirth Stars (WorkshopStars Dictionary) |
| 4 | Settings, Statistics, Tutorial in Sub-Objekte extrahiert |
| 5 | Boosts, DailyProgress, Cosmetics in Sub-Objekte extrahiert |
| 6 | ParallelOrdersByWorkshop (Multi-Auftrag), PausedAt/AccumulatedPauseDuration |
| 7 | Warehouse (SlotCount 20, StackLimit 50), ReservedInventory, AutoSellRules, HeirloomItems (). Migration kuerzt ueberlaufende Stacks und zahlt BaseValue als Geld aus. |

`GameState.CurrentStateVersion = 7` (const) — Cloud-Save mit höherer Version triggert Alert statt Download.

### Daily Challenge Tracking

`MiniGameResultRecorded` Event auf `IGameStateService` → `DailyChallengeService` subscribt.
Score-Mapping: Perfect=100%, Good=75%, Ok=50%, Miss=0%.

---

## Mini-Games (SkiaSharp-basiert)

Alle 10 Mini-Games nutzen dedizierte SkiaSharp-Renderer. Header, Result, Countdown und Buttons bleiben XAML.

**BaseMiniGameViewModel** (alle 10 VMs erben):
- 27 gemeinsame ObservableProperties, 9 Commands
- Direktstart: keine Start-Buttons (kein Tutorial → `StartGameAsync()` sofort)
- **Multi-Task-Race-Fix**: `GameRestarted`-Event in `SetOrderId()` — alle 10 Views abonnieren es
  und rufen `StartRenderLoop()` auf, auch wenn `ActivePage` konstant bleibt
- `ContinueCommand`: Reentrancy-Guard `if (!IsResultShown) return;` gegen Doppel-Tap
- Countdown verkürzt auf 350ms nach 50+ Spielen
- `OnGameTimerTickAsync` (abstract Task) mit Wrapper `HandleTimerTick` (async void + try/catch)

| MiniGame | Renderer | Besonderheit |
|----------|----------|-------------|
| Sawing | SawingGameRenderer | Bezier-Maserung, Sägemehl-Partikel, 4 Sub-Typen |
| Pipe Puzzle | PipePuzzleRenderer | BFS Wasser-Durchfluss, Blasen+Splash |
| Wiring | WiringGameRenderer | SKPathMeasure Pulse, Sicherungskasten |
| Painting | PaintingGameRenderer | Combo-Badge, Pinselstrich-Textur |
| Blueprint | BlueprintGameRenderer | Blaupausen-Grid, Memorisierungs-Scan |
| RoofTiling | RoofTilingRenderer | 3D-Ziegel, Platzierungs-Funken |
| DesignPuzzle | DesignPuzzleRenderer | Architektenplan, Tür-Öffnungen |
| Inspection | InspectionGameRenderer | 16 Vektor-Icons (8 gut/8 defekt), pulsierende Lupe |
| ForgeGame | ForgeGameRenderer | Amboss, Temperatur-Zonen, Hammer-Animation |
| InventGame | InventGameRenderer | Circuit-Pulse entlang Verbindungen |

**Rating-Score** (alle Spiele): Perfect=100%, Good=75%, Ok=50%, Miss=0%.
**Auto-Complete**: Timing-Spiele ab 30 Perfects (Premium 15), Puzzle/Memory ab 20 Perfects (Premium 10).

---

## SkiaSharp-Rendering-Patterns

### IDisposable-Pattern (ALLE Renderer mit Instanz-Feldern)

Renderer mit Instanz-SKPaint/SKFont/SKPath/SKShader/SKMaskFilter implementieren `IDisposable`
mit `_disposed`-Guard. Statische Felder werden NICHT disposed.

`App.DisposeServices()` → `GameJuiceEngine.Dispose()` → alle Renderer-Dispose-Kaskaden.

**Statische Renderer** (`static class`, kein IDisposable nötig):
`GameCardRenderer`, `WorkshopGameCardRenderer`, `ResearchIconRenderer`.

**Instanz-Renderer ohne Instanz-Felder** (sealed class, aber alle Felder static readonly):
`FireworksRenderer`, `LoadingScreenRenderer` — kein IDisposable erforderlich, da keine
Instanz-SKPaint-Felder vorhanden sind.

### SKPath/SKFont-Caching-Pattern (GC-Reduktion bei 30fps)

Gecachte Instanz- oder Klassenfelder statt `using var` pro Frame:

| Renderer | Gecachte Felder |
|----------|----------------|
| WiringGameRenderer | 8 SKPaint + 3 MaskFilter + 1 SKPath + 1 SKFont |
| SawingGameRenderer | 10 SKPaint + 1 SKPath + 3 SKShader (Bounds-basierter Cache, Toleranz 2dp) |
| BlueprintGameRenderer | 21 SKPaint + 3 SKFont + 1 SKPath + 1 SKShader (BG-Cache per Bounds) |
| LuckySpinWheelRenderer | 11 SKPaint + 1 SKFont + 13 gecachte SKShader + 2 SKMaskFilter |
| ResearchIconRenderer | _cachedPath + _labelFont + _crownFont (static, alle Icons sequenziell) |
| GuildResearchIconRenderer | _cachedPath (static, alle Icons sequenziell) |
| MaterialIconRenderer | 3 SKPaint + 1 SKFont + Bitmap-Cache pro ProductId (DI-Singleton, IDisposable, 128×128 procedural) |

**Shader-Cache-Pattern**: Nur bei Bounds-Änderung neu erstellen (ForgeGame, Wiring, Sawing, CraftTextures).

### WorkerAvatarControl (`Controls/WorkerAvatarControl.cs`)

Custom `Control`-Ableitung (kein TemplatedControl, kein SKCanvasView direkt).
Gemeinsamer statischer DispatcherTimer (`s_sharedTimer`) für alle Instanzen — ein Tick für alle statt
pro-Instanz-Timer. Statische `s_bitmapPaint` + `s_blinkPaint` ohne Allokation pro Frame.
WeakReference-Liste (`s_instances`) für Auto-Cleanup toter Controls.
`FpsProfile.CurrentChanged`-Event für Live-Intervall-Update bei Qualitätswechsel.
`WorkerAvatarRenderer` (sealed class) rendert das Pixel-Art-Bitmap, gecacht im `GameAssetService`.

### FPS-Profile (FpsProfile.cs, plattformadaptiv)

| Kontext | Low | Medium | High |
|---------|-----|--------|------|
| MiniGame | 24fps | 30fps | 30fps |
| Research/Workshop/GuildResearch | 15fps | 20fps | 24fps |
| Dashboard Idle | 5fps | 10fps | 10fps |
| Dashboard bei Effekten | 15fps | 24fps | 30fps |
| WorkerAvatar | 5fps | 8fps | 10fps |
| MainView (BG+TabBar) | 10fps | 15fps | 15fps |

Platform-Default: Android=Medium, Desktop=High (in `App.axaml.Initialize`).
`FpsProfile.CurrentChanged`-Event: WorkerAvatarControl subscribt für Live-Update.

### Scroll-Performance

Während Scroll: City-Canvas + Workshop-Cards + Background + TabBar komplett pausiert (0 InvalidateSurface/s).
250ms Ruhezeit nach letztem ScrollChanged. `DashboardView.IsScrolling` → MainView pausiert alle Canvases.

Max-Modus Debounce: `GetMaxAffordableUpgrades` (Math.Pow-Schleife) auf nicht-sichtbaren Tabs nur alle 2s.

10 MiniGame-ContentControls → 1 einziges mit ActiveMiniGameViewModel (ViewLocator erstellt/zerstört Views).

### Icon-System (224 Bitmap-Icons)

Kein Material.Icons.Avalonia. Alle Icons sind WebP-Bitmaps (128×128) in `Assets/visuals/icons/`.
`GameIcon : PathIcon` → `StyleKeyOverride => typeof(PathIcon)` (sonst unsichtbar in Avalonia 11).
`GameIconRenderer` für SkiaSharp-Canvas: Bitmap + `SKColorFilter.CreateBlendMode(color, SrcIn)`.

**GameAssetService**: LRU-Cache 50MB, WebP→SKBitmap + animierte WebP Multi-Frame.

### Cinematic-Pattern (PrestigeCinematicRenderer)

4-Phasen (Money→Badge→Multiplier→Reward), 14s, Skip+Tap-To-Continue.
`Update()` schaltet sich nach 8s Reward-Phase auto-ab (vorher musste Spieler tippen).
Audio: `MusicTrack.Celebration` mit Crossfade beim Start, `MusicTrack.IdleWorkshop` nach Dismiss.

---

## Premium & Ads

### Premium (4,99 EUR Lifetime)

+50% Einkommen (in `IncomeCalculatorService.CalculateGrossIncome()` UND `CalculateCraftingSellMultiplier()`),
+100% Goldschrauben (Gameplay-Quellen, nicht IAP), keine Werbung, Auto-ClaimDaily.

**Shop-Live-Vergleich**: `PremiumIncomeComparison` zeigt Einkommen ohne/mit Premium + GS-Verdopplung
(psychologisch stärkster Kaufgrund für Rebirth-Ziel-Spieler).

### Rewarded Ads (13 Placements)

1. `golden_screws` — 10 GS (4h-Cooldown, getrennt von Shop)
2. `shop_reward` — Cash/Boost im Shop (3h-Cooldown)
3. `score_double` — Mini-Game Score verdoppeln
4. `market_refresh` — Worker-Markt neu würfeln
5. `workshop_speedup` — 2h Brutto-Ertrag
6. `workshop_unlock` — 30% Rabatt
7. `worker_hire_bonus` — +1 Worker-Slot (max 3/WS)
8. `research_speedup` — −50% Restzeit (nur ab 30min)
9. `daily_challenge_retry` — Fortschritt zurücksetzen
10. `achievement_boost` — Achievement Progress +20% (nur bei TargetValue>5)
11. `offline_double` — 2x Offline-Earnings
12. `rush_boost` — 1h Rush per Video
13. `lucky_spin` — 1x/Tag Ad-Spin nach Gratis-Spin

### Goldschrauben-Quellen

Mini-Games (3–10), Daily Challenges (~12), Achievements (5–50), Rewarded Ad (10), IAP (50/150/450),
Daily Login (1–25), Spieler-Meilensteine (3–200), Workshop-Meilensteine (2–50).

**Premium +100% GS**: `AddGoldenScrews(amount, fromPurchase)` verdoppelt Gameplay-Quellen.
IAP-Käufe (`fromPurchase: true`) werden NICHT verdoppelt.

### Whale-IAP-Tiers (über 4,99-Ceiling)

3 Bundles im ShopViewModel, geben mehr als die Summe der Einzelkäufe:

| ID | Preis | Inhalt |
|----|-------|--------|
| `bundle_mid` | 9,99 € | 1500 GS + 8 h Speed-Boost |
| `bundle_big` | 19,99 € | 4000 GS + 48 h Speed-Boost + 25 Mio. EUR |
| `bundle_mega` | 49,99 € | 12 000 GS + 7 Tage Speed-Boost + 200 Mio. EUR + Premium |

VIP-Tracking via `RecordVipPurchase()` mit korrekten Beträgen.

---

## Audio (Cross-Platform)

- **Android**: `AndroidAudioService` (SoundPool für SFX, MediaPlayer für Music + Crossfade,
  AudioFocus-Listener für Telefonanrufe).
- **Desktop**: `DesktopAudioService` (Windows: NAudio + NAudio.Vorbis;
  Linux/macOS: ffplay-Process-Fallback). Wird in `Program.cs` als `App.AudioServiceFactory` registriert.
- **Assets**: 82 SFX in `HandwerkerImperium.Shared/Assets/Sounds/*.ogg` + 4 Music-Loops in
  `HandwerkerImperium.Shared/Assets/Music/*.ogg`. Android linkt via `<AndroidAsset Include="..\HandwerkerImperium.Shared\Assets\Sounds\**" />`.
- **Generator**: `tools/SoundForge/generate_audio.py` (Python + FFMPEG, algorithmische Synthese).

`MusicTrack`-Enum: `IdleWorkshop`, `BossOrTournament`, `Celebration`. Crossfade default 800 ms.

---

## Cross-Promotion (House-Ads)

`ICrossPromoService` + `CrossPromoService` mit statischem Catalog der 11 Apps.
Tagesrotation: `DayOfYear % AppCount`. Eigene App (HandwerkerImperium) wird gefiltert.

UI: `CrossPromoCard.axaml` in `Views/Settings/` — eingebettet in SettingsView nach Premium-Card.
Klick öffnet Play-Store-Deep-Link via `UriLauncher.OpenUri()`. Analytics-Event `cross_promo_click`.

RESX-Keys pro App: `CrossPromo_{AppId}_Name` + `CrossPromo_{AppId}_Hook` (DE/EN/ES/FR/IT/PT).

---

## FTUE (`IFtueService`)

State-Machine + Analytics-Hooks. UI-Spotlight-Overlay separat als naechster Schritt.

10-Step-Default-Sequenz: Welcome → ErstesUpgrade → ErsterAuftrag → ErstesMiniGame →
MoneyExplained → ErsterWorker → XpExplained → ScrewsExplained → ImperiumIntro → Complete.

State persistiert in `GameState.Tutorial.Ftue` (FtueState, Default-Init genügt).
Telemetrie-Events: `ftue_started`, `ftue_step_completed`, `ftue_skipped`, `ftue_completed`.

---

## Live-Ops

### LiveEventService

`ILiveEventService` + `LiveEventService` — RemoteConfig-getrieben, 4 Templates:
DoubleReward / BossRush / CoopMarathon / MiniGameMastery. Score-Tracking + 3-Tier-Reward
(25/75/200 GS bei 100/500/2000 Punkten). Game-Code hängt `AddScore`-Aufrufe ein
(z.B. nach Order-Complete bei DoubleReward). RemoteConfig-Schluessel:
`live_event.id|template|starts_at|ends_at`.

### Push-Notifications (8 Trigger)

`AndroidNotificationService` plant 8 Notifications statt 4: ResearchComplete, DeliveryReminder,
RushAvailable, DailyReward + neu WorkerMoodCritical (30min nach Close), OfflineEarningsCapped (4h),
BattlePassExpiring (3 Tage vor Saison-Ende), LiveOrderAvailable (1h, ab WS-Lv25).
Alle Texte mit Meister-Hans-Persona-Praefix („Meister Hans: ‚...'").

### Friend-Invite Reward-Loop

`IReferralService` + `ReferralService` mit 6-stelligem Code-Generator + 3-Tier-Reward
(50/200/500 GS bei 1/5/10 erfolgreichen Empfehlungen) + permanenter +5% Income-Boost ab Tier 10.
Server-seitiges Anti-Cheat (Geraete-Fingerprint gegen Self-Referral) als separater Service.

### BattlePass-Saison-Update

Saison-Dauer 42 → 30 Tage (12 Saisons/Jahr statt 8.7). Premium-Spread auf ~3x Free
(baseMoney *120→*180, Capstone-GS 100→150, Milestone-40 30→50, Tier-0-29 GS 10/2→12/3).

### Telemetrie-Events (V7 — Material-Loop, Plan Section 8.1)

`IAnalyticsService.TrackEvent(name, props)` ist die einzige Schnittstelle — Services injizieren
das Interface als optionales Konstruktor-Argument (`IAnalyticsService? analytics = null`), damit
bestehende Tests/DI-Setups ohne Mock funktionieren.

| Event | Trigger | Properties |
|-------|---------|-----------|
| `material_crafted` | `CraftingService.CollectProduct` | product_id, tier, workshop, count |
| `material_sold` | `CraftingService.SellProducts` | product_id, tier, count, price_per_unit, total_revenue, source |
| `warehouse_full_pause` | `WarehouseService.RegisterStackOverflow` | product_id, slot_count, stack_limit |
| `material_market_trade` | `MarketService.TryBuy/TrySell` | product_id, direction, count, unit_price, total |
| `guild_mega_project_donation` | `GuildMegaProjectService.DonateAsync` | project_id, material_id, donated_count, total_progress_percent |
| `heirloom_chosen` | `PrestigeService.ApplyHeirloomSelection` (indirekt via PrestigeService) | product_id, base_value, slot_count |
| `order_accepted_with_material` | `EconomyFeatureViewModel.AcceptMaterialOfferAsync` | order_id, order_type, bonus_multiplier, materials_count |

Story-Chapter (38/39/40) sind ebenfalls Telemetrie-getrieben — beim Anzeigen feuert
`StoryService.MarkChapterRead` → kein eigenes Event, sondern Reuse des bestehenden
`story_chapter_completed`.

---

## MainView Lazy-Loading

Statt 25+ einzelner ContentControls mit `IsVisible`-Bindings nutzt das ContentPanel:

- 4 Direct-Bound-Views (DashboardView/ImperiumView/MissionenView/PrestigeView) mit
  `DataContext = MainViewModel` und `IsVisible="{Binding IsXxxActive}"`.
- Ein einzelnes `ContentControl Content="{Binding ActivePageContent}"` fuer alle anderen
  Sub-Pages. Der ViewLocator findet die passende View automatisch.

`ActivePageContent`-Switch im MainViewModel mappt 25+ ActivePage-Werte auf das passende VM
(oder null fuer Direct-Bound). Beim Page-Switch feuern `OnActivePageChanged` die
PropertyChanged-Notifies fuer `ActivePageContent` + `HasActivePageContent`.

**Effekt:** Cold-Start vermeidet die Materialisierung von ~25 Sub-Views inkl. SkiaSharp-
Renderer, View-Locator-DataTemplates rendern nur die aktive Seite.

---

## MainViewModel-Extraktion (Logik-Klumpen-Auslagerung)

Die echte Feature-Logik liegt in eigenstaendigen Coordinator-Services und Feature-VMs.
MainViewModel ist Composition-, Binding-Anker- und Host-Layer.

### Host-Pattern (Coordinator ↔ MainViewModel)

Coordinator-Services greifen auf MainViewModel ausschliesslich ueber schmale, explizit
implementierte Host-Interfaces zu (`AttachHost(this)` im MainViewModel-Ctor). Jeder
Coordinator subscribed selbst auf seine Service-Events (analog `CinematicCoordinator`) und
ist dadurch isoliert unit-testbar (Mock-Host). MainViewModel implementiert 5 Host-Interfaces:
`INavigationHost`, `IWelcomeFlowHost`, `IStartupHost`, `IProgressionFeedbackHost`, `IGameTickHost`.

### IUiEffectBus + UiEffectBus

Zentraler Singleton-Bus fuer FloatingText / Celebration / Ceremony. Ausloeser (MainViewModel,
Coordinators, Feature-VMs) injizieren `IUiEffectBus` und rufen `RaiseXxx(...)`. Die Views
(`DashboardView`, `MainView`) abonnieren den Bus direkt im Code-Behind (analog `IFrameClock`)
— die frueheren `FloatingTextRequested/CelebrationRequested/CeremonyRequested`-Events am
MainViewModel entfallen.

### GameStartupCoordinator

`IGameStartupCoordinator` + `GameStartupCoordinator` — die komplette Startup-Sequenz
(Spielstand laden, Cloud-Save-Abgleich, Sprach-Sync, Order-/Mission-Init, GameLoop-Start,
verzoegerte WhatsNew-/Analytics-Consent-Dialoge). `MainViewModel.InitializeAsync()` ist nur
noch ein Forwarder; `IStartupHost` liefert `IsLoading` + die EconomyVM-Refreshes.

### ProgressionFeedbackCoordinator

`IProgressionFeedbackCoordinator` + `ProgressionFeedbackCoordinator` — subscribed selbst auf
Level/GoldenScrews/Xp/Workshop/Worker/MasterTool/Achievement/Prestige/Rebirth-Events und feuert
FloatingText/Celebration/Zeremonie/Sound/Hints ueber den `IUiEffectBus`. Haelt den Level-Up-
Pulse-Timer. `IProgressionFeedbackHost` liefert EconomyVM-Refreshes + Property-Notifies.
`CheckReviewPrompt()` ist public — `MainViewModel.OnOrderCompleted` nutzt es ueber das Interface.

### GameTickCoordinator

`IGameTickCoordinator` + `GameTickCoordinator` — subscribed auf `IGameLoopService.OnTick` und
verteilt die Per-Tick-UI-Updates an die Feature-VMs (tab-spezifisch gated). `IGameTickHost`
ist bewusst breiter (~15 Member) — Per-Tick-Orchestrierung beruehrt inhaerent den ganzen
UI-State; der explizite Contract macht das aber sichtbar + testbar.

### CinematicCoordinator

`ICinematicCoordinator` + `CinematicCoordinator` — subscribed auf `IPrestigeService.CinematicReady`,
lokalisiert Tier-Namen, schaltet Music-Track auf `MusicTrack.Celebration` und feuert das
View-Trigger-Event. MainViewModel ruft im Ctor `StartListening()` auf und delegiert
`OnPrestigeCinematicSkipped/Dismissed` an den Coordinator.

### ReputationTierEffects

`IReputationTierEffects` + `ReputationTierEffects` — bündelt FloatingText + Celebration +
LevelUp-Audio + Achievement-Dialog mit Tier-Effekten. `MainViewModel.OnReputationTierChanged`
feuert nur die Property-Notifies und delegiert die Effekt-Logik an den Service.

### Feature-ViewModels (Source-of-Truth)

- HeaderViewModel (16 Properties)
- PrestigeBannerViewModel (18 Properties)
- GoalBannerViewModel
- WelcomeFlowViewModel (14 Props + Welcome-Flow-Logik in `WelcomeFlowViewModel.Logic.cs`)
- MissionsFeatureViewModel (Daily Challenges + Weekly Missions + LuckySpin)
- EconomyFeatureViewModel (Workshop-Kauf/Upgrade)
- DialogViewModel + 6 Partial-Files (Story/Hint/Confirm/Achievement/LevelUp/Alert/PrestigeSummary)
- NavigationService + DialogOrchestrator + MiniGameNavigator

### Bewusst beibehalten im MainViewModel

- **Tab-Select-Commands** + RelayCommand-Forwarder (`Navigation.cs`, `Economy.cs`): AXAML-Bindings
  zeigen direkt darauf — Auslagerung wuerde Bindings brechen ohne strukturellen Gewinn.
- **EventHandlers.cs** (`OnMoneyChanged`, Order-Handler, Event-System-Display, `OnLanguageChanged`,
  `OnReputationTierChanged`, `OnStateLoaded`): setzen AXAML-gebundene MainViewModel-Properties
  direkt — bleiben Teil des Binding-Layers.

---

## Reset-Hierarchie-Pacing

Drei Reset-Layer (Rebirth/Prestige/Ascension) sind UX-getrennt:

- **Ascension-Tab**: Immer sichtbar (Layout-Stabilität U4), aber `IsEnabled` nur wenn
  `LegendeCount >= 3`. Lock-Icon overlayed das Star-Icon vor Unlock.
- **Foreshadowing-Hint**: Nach 1. Prestige zeigt `ContextualHints.AscensionPath` einen
  erklärenden Dialog → Spieler kennt das Konzept lange bevor er es nutzen kann.
- **Action-Hint**: Nach 3x Legende-Prestige zeigt `ContextualHints.AscensionAvailable` einen
  Action-Dialog ("Du kannst jetzt aufsteigen!").

---

## Game Juice / Visual-Patterns

| Effekt | Pattern |
|--------|---------|
| Workshop Cards | WorkshopCardRenderer: 10 thematische Szenen (AI-Bitmap + Level-Overlays) |
| Worker Avatare | Pixel-Art (6 Hauttöne, Tier-Farbe+Sterne, Mood, RarityFrame, Idle-Bobbing+Blinzeln) |
| Meister Hans Portrait | 4 Stimmungen, Idle-Bobbing, Blinzel-Animation (120×120) |
| Golden Screw Icon | Gold-Shimmer CSS-Animation (scale+rotate), SkiaShimmerEffect (permanent wenn > 0) |
| Level-Up | XP-Bar Puls, CelebrationOverlay + Sound bei Meilensteinen, Confetti |
| Income FloatingText | Grüner Text, +100px, 1.5s |
| TapScale-Effekt | CSS: scale(0.95) bei :pressed, 80ms CubicEaseOut (global in App.axaml) |
| OdometerRenderer | Animierte Geld-Anzeige, rollende Ziffern, Suffix-Crossfade, Gold-Flash |
| CoinFlyAnimation | 8–16 Münzen auf Bezier-Kurven, HUD-Pulse bei Ankunft |
| GameJuiceEngine | ScreenShake, RadialBurst, CoinsFlyToWallet, SparkleEffect (Struct-Pool max 200) |
| Combo Badge | Gold-Badge mit Fire-Icon bei Combo ≥ 3 (PaintingGame) |
| Bottom Sheets | CSS translateY(800→0px), CubicEaseOut |
| Reward-Zeremonie | Full-Screen: Scale-In, Confetti (120), Feuerwerk, 5 CeremonyTypes, 4s Tap-to-Dismiss |
| Glücksrad | LuckySpinWheelRenderer: 8 Segmente, Nieten-Rand, Spin-Animation ~60fps |
| City-Szene | CityRenderer: AI-Bitmap + Wetter-Overlay (saisonal, Event-gesteuert, 2x Intensität) |
| City Weather | Regen+Regenbogen, Sonne+Shimmer, Blätter, Schnee, Kirschblüten (80 Struct-Pool) |
| Workshop-Szenen | AI-Bitmap + Level-Overlays (Sterne Lv250+, Gold-Aura Lv500+, Shimmer Lv1000) |
| Reduce Motion | `Classes.NoMotion` per `ReduceMotion = Settings.GraphicsQuality == Low` PLUS `GameJuiceEngine.ReduceMotion` daempft ScreenShake auf 30%, schaltet Confetti/RadialBurst/Shockwave ab, halbiert Coin-Fly-Count |
| PrestigeCinematic | 4-Phasen-Renderer, 14s, Auto-Dismiss nach 8s Reward-Phase |

**Farbpalette** (Craft-Theme):
- Buttons: Craft-Orange/Braun (App.axaml-Overrides, kein PrimaryBrush)
- Semantisch: SuccessBrush=#22C55E, ErrorBrush=#EF4444, WarningBrush=#F59E0B
- Feature-Akzente: Tournament=#DC2626, SeasonalEvent=#059669, BattlePass=#7C3AED

---

## Firebase-Security-Rules-Patterns

Alle relevanten Pfade müssen in `database.rules.json` eingetragen sein. Fehlender Eintrag →
Firebase gibt `null` ohne Error zurück (kein Exception-Log).

**Monotonie-Validierung** (verhindert Cheat-Manipulation):
```json
"progress": { ".validate": "newData.isNumber() && newData.val() >= data.val()" },
"completed": { ".validate": "!data.exists() || (data.val() == false && newData.val() == true)" }
```

**Server-Timestamp** gegen client-seitige Manipulation:
```csharp
// C# Model sendet Sentinel-Wert, Firebase löst serverseitig auf
private static readonly Dictionary<string, string> FirebaseServerTimestamp =
    new() { [".sv"] = "timestamp" };
```

**Rate-Limit per Server-Zeit** (Rules-Ebene):
```json
".validate": "(now - data.child('updatedMs').val()) >= 60000"
```

**Firebase orderBy-Queries** brauchen `.indexOn` für das abgefragte Feld in den Rules.
`|| !data.exists()` zur Write-Rule hinzufügen wenn Erstellen neuer Einträge erlaubt sein soll.

---

## Aktive Gotchas

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Service-Caches stale nach Prestige/Import/Reset | Services mit internen Caches subscriben nicht auf `StateLoaded` → Caches zeigen auf verwaiste Objekte | ALLE Services mit Caches MÜSSEN `StateLoaded += ResetCaches` im Konstruktor haben |
| Multi-Task-Order: nur erster Task spielbar | MiniGame-View stoppt 30fps Timer bei `IsResultShown=true`, setzt ihn null. Bei gleicher MiniGame-Art bleibt `ActivePage` konstant → `OnDataContextChanged` feuert nicht → Timer nie neu gestartet | `BaseMiniGameViewModel.GameRestarted`-Event in `SetOrderId()`. Alle 10 Views rufen `StartRenderLoop()` auf. `ContinueCommand` hat Reentrancy-Guard `if (!IsResultShown) return;` |
| Gilden-Mitglieder verschwinden / eigener Spieler unsichtbar | `UpdateLastActiveAsync()` nie aufgerufen → `LastActiveAt` nur beim Beitritt gesetzt → `IsStaleMember()` filtert nach 30 Tagen | `RefreshGuildDetailsAsync()` ruft `UpdateLastActiveAsync().SafeFireAndForget()` auf. Explizite `isSelf`-Guards in Duplikat- und Stale-Filter — eigener UID wird nie gefiltert |
| Gilde zeigt "Keine Internetverbindung" fälschlicherweise | `GetAsync()` setzte `IsOnline=true` nicht bei 200 OK mit null-Body; `EnsureAuthenticatedAsync()` warf Exception statt Fallback; catch-Block setzte blind Offline | `IsOnline=true` VOR null-Check in GetAsync/QueryAsync. Fallback zu `SignUpAnonymouslyAsync()` statt throw. catch prüft `IsOnline` statt blind Offline setzen |
| Firebase-Pfad gibt immer null zurück | Kein Eintrag in `database.rules.json` → Permission denied wird als null geliefert, kein Error-Log | Jeden neuen Firebase-Pfad in `database.rules.json` eintragen. Checkliste: player_guilds, player_invites, available_players, guild_invite_codes, invite_code_to_guild |
| Firebase orderBy liefert keine Daten | Kein `.indexOn` für das abgefragte Feld in den Rules | `.indexOn: ["feldname"]` unter dem Pfad in `database.rules.json` eintragen |
| Worker.AssignedWorkshop null bei Neustart | `GameState.CreateNew()` setzt `AssignedWorkshop` nicht explizit → `IsWorking=false` → keine Fatigue, falscher UI-Status | `AssignedWorkshop = WorkshopType.Carpenter` explizit in `CreateNew()` und in `Worker.CreateForTier()` mit optionalem Parameter setzen |
| 5 Dialoge am ersten Start | Daily Reward→Story→Welcome→FirstWorkshop→AcceptOrder überfluten neue Spieler | Daily Reward Tag 1 still einsammeln. Welcome-Hint überspringen wenn Story Ch.1 gezeigt wurde |
| RecordMiniGameResult ignoriert QuickJobs | Early-Return bei `ActiveOrder == null` → Stats, Events, PerfectStreak nie aktualisiert | `order.RecordTaskResult()` nur bei ActiveOrder, Stats+Events IMMER feuern |
| Co-op Score: Last-Write-Wins überschreibt ersten Submitter | `SetAsync()` (PUT) überschreibt den State des ersten Submitters | `UpdateAsync()` (PATCH) — atomar nur das eigene Score-Feld. Status-Übergang in 2. Patch idempotent |
| SaveAsync UI-Thread-Freeze (50–100ms alle 30s) | `JsonSerializer.Serialize` blockierte UI-Thread | Serialize auf Background-Thread via `Task.Run` + `IGameStateService.ExecuteWithLock` |
| CanGiveBonus Button grau obwohl genug Geld | `CanGiveBonus` prüfte 24h-Lohn, `GiveBonus` kostet nur 8h | Alle 3 Stellen auf 8h harmonisieren (WorkerProfileVM + WorkerService) |
| PipePuzzle Rating zu großzügig | `optimalMoves = GridCols * GridRows` statt Pfad-Länge | `optimalMoves = Tiles.Count(t => t.IsPartOfSolution && !t.IsLocked)` |
| `async void` Event-Handler crashen Prozess | Timer-Ticks als `async void` ohne try/catch | `RunHandlerSafely` aus `AsyncExtensions` oder `protected abstract Task OnGameTimerTickAsync` + Wrapper |
| NotificationCenterService „Collection was modified" | Eigener Lock, AutoSave-Lock und Add/Dismiss laufen parallel | Alle 5 Mutationen auf `_gameStateService.ExecuteWithLock(...)` umstellen |
| Rewarded Ad Belohnung kommt nicht an | LoadAndShowAsync-Timeout deckt Laden + Anzeige ab → feuert während Video | `CancellationTokenSource` im Callback: Timeout nur für Lade-Phase, wird gecancellt wenn Ad geladen+gezeigt |
| LuckySpin Timer-Leak bei Exception | `OnSpinTick` catch-Block stoppte Timer nicht → Endlos-Exception-Schleife | Timer stoppen + Event unsubscriben + `IsSpinning=false` im catch |
| MainView RenderTimer läuft bei App-Pause | Battery-Drain im Hintergrund | `MainViewModel.PauseStateChanged`-Event → MainView stoppt/startet Timer |

---

## Build & Test

```bash
# Shared-Build (Kompilier-Check)
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared

# Android Release AAB
dotnet publish src/Apps/HandwerkerImperium/HandwerkerImperium.Android -c Release

# Desktop (Entwicklung/Test)
dotnet run --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop

# Tests (1050+ Tests)
dotnet test tests/HandwerkerImperium.Tests

# AppChecker
dotnet run --project tools/AppChecker HandwerkerImperium

# Firebase-Rules deployen
npx firebase-tools deploy --only database --project handwerkerimperium-487917
```

**Tests**: SaveGameMigrationTests, PerformanceBenchmarkTests, PrestigeCinematicRendererTests,
DailyBundleServiceTests, CjkFontResolverTests, HeadlessSmokeTests (Avalonia.Headless + XUnit),
EternalMasteryServiceTests (Long-Term-Engagement). 1058+ Tests, alle grün.
CI: `.github/workflows/ci.yml` (Build + Test + Firebase-Rules-Lint).

---

## Eternal Mastery (Long-Term-Engagement post-Lv1000)

`IEternalMasteryService` + `EternalMasteryService` — permanenter Einkommens-Bonus der mit
jedem abgeschlossenen Prestige skaliert. Soft-Cap ab `EternalMasterySoftCapThreshold`
Prestiges (logarithmische Daempfung), kein Reset bei Ascension. Stellt Late-Game-Progression
post-Lv1000 sicher, damit Spieler nach Ascension nicht auf einer Plateau-Phase stehen.

**Berechnung** (`GameBalanceConstants.EternalMastery*`):
- Linear: +0.5% pro Prestige (jeder Tier zählt)
- 5er-Stufen-Bonus: +2.5% alle 5 Prestiges
- 10er-Mega-Stufen-Bonus: +5% alle 10 Prestiges

Bei 100 Prestiges = +150% Income (50% linear + 50% 5er-Stufen + 50% 10er-Mega).

**Integration**: `IncomeCalculatorService.CalculateGrossIncome` multipliziert nach Premium-Bonus.
Header-Badge im DashboardView (gold-shimmer, sichtbar wenn `HeaderVM.HasEternalMastery`).
Update via `MainViewModel.Helpers.RefreshEternalMastery()` bei OnPrestigeCompleted + OnStateLoaded.

---

## Verweise

- Haupt-CLAUDE.md: Build, generische Conventions (MVVM, DateTime, DI, Naming) → [../../../CLAUDE.md](../../../CLAUDE.md)
- Firebase Security Rules: `database.rules.json` (Repo-Root)
- Balancing-Werte: `~/.claude/projects/.../balancing.md`
- ComfyUI Asset-Generierung: `F:\AI\ComfyUI_workflows\handwerkerimperium\`
- Icon-Generierung: `F:\AI\ComfyUI_workflows\handwerkerimperium\generate_icons_test.py`
