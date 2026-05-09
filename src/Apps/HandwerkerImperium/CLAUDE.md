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
│   ├── MainViewModel.cs             # Partial-Split über 7 Dateien
│   ├── HeaderViewModel.cs           # Source-of-Truth: Money, Level, GoldenScrews, ...
│   ├── PrestigeBannerViewModel.cs   # Prestige-Banner-Properties (18)
│   ├── DialogViewModel.cs           # Alle Dialog-States, implementiert IDialogService
│   ├── MissionsFeatureViewModel.cs  # Daily Challenges, Weekly Missions, QuickJobs, LuckySpin
│   ├── EconomyFeatureViewModel.cs   # Workshop-Kauf/Upgrade, Aufträge, Rush
│   ├── Guild/                       # GuildViewModel + 9 Sub-VMs (ViewLocator-Konvention)
│   └── MiniGames/                   # BaseMiniGameViewModel + 10 konkrete VMs
├── Views/
│   ├── MainView.axaml               # 5-Tab-Navigation, Dialoge als UserControls
│   ├── Dashboard/                   # DashboardView + UserControls (AutomationPanel, BannerStrip, ...)
│   ├── Dialogs/                     # Dialog-UserControls (AchievementDialog, StoryDialog, ...)
│   ├── Guild/                       # GuildView + Sub-Views (Research, Boss, Hall, War, ...)
│   └── MiniGames/                   # 10 MiniGame-Views
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

### MainViewModel Partial-Split

| Datei | Inhalt |
|-------|--------|
| `MainViewModel.cs` | Felder, Konstruktor, `ActivePage`-Enum, Event-Handler, GameTick, Dispose |
| `MainViewModel.Navigation.cs` | Tab-Auswahl, HandleBackPressed, Child-Navigation-Routing |
| `MainViewModel.Dialogs.cs` | Weiterleitungsmethoden an DialogVM, Prestige-Durchführungslogik |
| `MainViewModel.Economy.cs` | Workshop-Kauf/Upgrade, Aufträge, Rush, Lieferant, BulkBuy |
| `MainViewModel.Missions.cs` | LuckySpin-Overlay-Steuerung |
| `MainViewModel.Init.cs` | InitializeAsync, Cloud-Save, Offline-Earnings, Daily Reward |
| `MainViewModel.Host.cs` | INavigationHost-Implementierung (115 Zeilen) |

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
| `WelcomeFlowViewModel` | 13 (Dialog-Sichtbarkeiten, Texte) | Events an MainViewModel | DI Singleton |
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

**IGuildFacade**: Service-Container-Facade bündelt 9 Gilden-Services über Properties.
GuildViewModel bekommt nur `IGuildFacade` injiziert (7 Parameter statt 14).

---

## Game-Mechaniken

### 5-Tab Navigation

| Tab | Index | View | Inhalt |
|-----|-------|------|--------|
| Werkstatt | 0 | DashboardView | City-Szene, Workshop-Karten, Automation-Panel, Quick-Jobs |
| Imperium | 1 | ImperiumView | Gebäude, Crafting+Research, Workers/Manager/MasterTools, Prestige |
| Missionen | 2 | MissionenView | Heute (Daily, Quick-Jobs, Glücksrad) + Wettbewerbe (Weekly, Turnier, BattlePass) |
| Gilde | 3 | GuildView | 5-Tab-Hub (Übersicht/Kampf/Forschung/Chat/Mitglieder) |
| Shop | 4 | ShopView | IAP, Goldschrauben-Pakete, Ausrüstungs-Shop |

**Imperium-Sub-Tabs** (ImperiumSubTab Enum): Workshops / Workers / Research / Equipment / Ascension.
Ascension-Tab gesperrt bis `LegendeCount >= 3`, aber IMMER sichtbar (Lock-Icon-Overlay statt Ausblenden).

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

### SaveGame-Versionen

| Version | Beschreibung |
|---------|-------------|
| 1 | Legacy (Altes Worker-System) |
| 2 | Neues Worker-System, Buildings, Research, Events, Prestige, Reputation |
| 3 | Workshop Rebirth Stars (WorkshopStars Dictionary) |
| 4 | Settings, Statistics, Tutorial in Sub-Objekte extrahiert |
| 5 | Boosts, DailyProgress, Cosmetics in Sub-Objekte extrahiert |
| 6 | ParallelOrdersByWorkshop (Multi-Auftrag), PausedAt/AccumulatedPauseDuration |

`GameState.CurrentStateVersion = 6` (const) — Cloud-Save mit höherer Version triggert Alert statt Download.

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

### Whale-IAP-Tiers (über 4,99-Ceiling, AAA-Audit P0)

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
- **Desktop** (AAA-Audit P2): `DesktopAudioService` (Windows: NAudio + NAudio.Vorbis;
  Linux/macOS: ffplay-Process-Fallback). Wird in `Program.cs` als `App.AudioServiceFactory` registriert.
- **Assets**: 82 SFX in `HandwerkerImperium.Shared/Assets/Sounds/*.ogg` + 4 Music-Loops in
  `HandwerkerImperium.Shared/Assets/Music/*.ogg`. Android linkt via `<AndroidAsset Include="..\HandwerkerImperium.Shared\Assets\Sounds\**" />`.
- **Generator**: `tools/SoundForge/generate_audio.py` (Python + FFMPEG, algorithmische Synthese).

`MusicTrack`-Enum: `IdleWorkshop`, `BossOrTournament`, `Celebration`. Crossfade default 800 ms.

---

## Cross-Promotion (House-Ads, AAA-Audit P1)

`ICrossPromoService` + `CrossPromoService` mit statischem Catalog der 11 Apps.
Tagesrotation: `DayOfYear % AppCount`. Eigene App (HandwerkerImperium) wird gefiltert.

UI: `CrossPromoCard.axaml` in `Views/Settings/` — eingebettet in SettingsView nach Premium-Card.
Klick öffnet Play-Store-Deep-Link via `UriLauncher.OpenUri()`. Analytics-Event `cross_promo_click`.

RESX-Keys pro App: `CrossPromo_{AppId}_Name` + `CrossPromo_{AppId}_Hook` (DE/EN/ES/FR/IT/PT).

---

## FTUE-Foundation (AAA-Audit P0)

`IFtueService` + `FtueService` als State-Machine + Analytics-Hooks. UI-Spotlight-Overlay
ist Folge-Sprint, Foundation hier ist build-fest.

10-Step-Default-Sequenz: Welcome → ErstesUpgrade → ErsterAuftrag → ErstesMiniGame →
MoneyExplained → ErsterWorker → XpExplained → ScrewsExplained → ImperiumIntro → Complete.

State persistiert in `GameState.Tutorial.Ftue` (FtueState, Default-Init genügt).
Telemetrie-Events: `ftue_started`, `ftue_step_completed`, `ftue_skipped`, `ftue_completed`.

---

## Reset-Hierarchie-Pacing (AAA-Audit P0)

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
| Reduce Motion | `Classes.NoMotion` per `ReduceMotion = Settings.GraphicsQuality == Low` |
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
DailyBundleServiceTests, CjkFontResolverTests, HeadlessSmokeTests (Avalonia.Headless + XUnit).
CI: `.github/workflows/ci.yml` (Build + Test + Firebase-Rules-Lint).

---

## Verweise

- Haupt-CLAUDE.md: Build, generische Conventions (MVVM, DateTime, DI, Naming) → [../../../CLAUDE.md](../../../CLAUDE.md)
- Firebase Security Rules: `database.rules.json` (Repo-Root)
- Balancing-Werte: `~/.claude/projects/.../balancing.md`
- ComfyUI Asset-Generierung: `F:\AI\ComfyUI_workflows\handwerkerimperium\`
- Icon-Generierung: `F:\AI\ComfyUI_workflows\handwerkerimperium\generate_icons_test.py`
