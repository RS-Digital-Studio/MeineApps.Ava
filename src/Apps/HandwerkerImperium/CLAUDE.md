# HandwerkerImperium (Avalonia)

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Idle-Game: Baue dein Handwerker-Imperium auf, stelle Mitarbeiter ein, kaufe Werkzeuge, erforsche Upgrades, schalte neue Workshop-Typen frei. Verdiene Geld durch automatische Aufträge oder spiele Mini-Games.

**Version:** 2.1.0 (VersionCode 50) | **Package-ID:** com.meineapps.handwerkerimperium | **Status:** Produktion

### Sprint 3 v2.1.0 (Stand 05.05.2026, Big Bets)

**Saison-Pass Storyline (Task 3.1)**
- `SeasonStoryline`-Model + `SeasonStorylineCatalog` mit 4 Saisons (Spring/Summer/Autumn/Winter)
  × 5 Kapiteln = 20 saisonale Story-Kapitel.
- `StoryChapter` um `RequiredBattlePassTier` + `RequiredSeasonTheme` erweitert.
  `StoryService.IsChapterUnlocked` prueft beide Bedingungen.
- `IBattlePassService.TierUpReached` Event (oldTier, newTier, seasonNumber) — feuert
  beim AddXp wenn Tier hoeher springt. MainViewModel abonniert + ruft
  `CheckForNewStoryChapter` auf der UI-Thread auf.
- Spring-Saison („Aufschwung der Stadt") komplett mit deutschem Text. Summer/Autumn/
  Winter haben Stub-Texte — Story-Writing-Pass kann sie spaeter ausarbeiten.

**Praktikanten-System (Task 3.5)**
- `Worker.IsIntern` + `InternProgressTicks` + `InternAwaitingPromotion` Properties.
- `IWorkerService.HireIntern` (kostenlos, F-Tier, 0 Lohn, Limit 2 gleichzeitig).
- `IWorkerService.PromoteIntern` (zu E-Tier, Lohn-pflichtig) und `DeclineInternPromotion`
  (Worker verlaesst Werkstatt).
- Promotions-Schwelle: 86400 aktive Ticks (24h aktive Spielzeit).
- `InternReadyForPromotion`-Event bei Promotion-Schwelle.
- Update-Logik in `WorkerService.UpdateWorkerStates` — IsResting blockiert Tick-Inkrement.

**Reputation-Shop (Task 3.3)**
- `IReputationShopService` als 3. Waehrung neben Geld + Goldschrauben — Items kosten
  Reputation-Score-Punkte. Sichtbar ab Reputation &gt;= 60.
- 5 Items mit Effekten: Stammkunden-Garantie (30 Rep, naechste 5 Auftraege),
  Schnelle Lieferung (20 Rep, +50% Speed/1h), Worker-Mood-Boost (25 Rep, +30 Mood),
  Workshop-Skin Holz-Premium (100 Rep, kosmetisch permanent), Reputation-Insurance
  (40 Rep, naechster Risk-Miss kostenlos).
- GameState-Properties: RepShopRegularCustomerCharges, RepShopFasterDeliveryUntil,
  RepShopWoodPremiumSkinUnlocked, RepShopInsuranceCharges.
- OrderGeneratorService respektiert Stammkunden-Charges (forciert Stammkunde wenn &gt; 0).

**Co-op-Auftraege Foundation (Task 3.2 — Phase A)**
- `CoopOrderState` + `CoopOrderStatus` Models. Firebase-Pfad
  `guilds/{guildId}/coopOrders/{orderId}`.
- `IGuildCoopOrderService` Skelett mit CRUD-Methoden (Create/Accept/Decline/SubmitScore).
- Phase B (Firebase-Implementierung, Polling-Loop, HMAC-Signierung, UI in GuildView,
  Echtzeit-MiniGame-Sync) braucht dedizierten Branch + 2-Player-Mock-Tests.

**Worker-Auktionen Foundation (Task 3.4 — Phase A)**
- `WorkerAuctionState` + `WorkerAuctionStatus` Models. Firebase-Pfad
  `guilds/{guildId}/auctions/{auctionId}`.
- `IWorkerAuctionService` Skelett. Phase B (Bidding-Logik, NPC-Bots fuer Solo,
  5min-Auktion-Cron, Refund) braucht dedizierten Branch + Firebase-Rules-Update.

### Sprint 2 v2.0.37 (Stand 05.05.2026, UX-Tiefe + Code-Haertung)

**Reputation-Tiers (Task 2.1)**
- `CustomerReputationTier` Enum (Beginner/CityKnown/RegionStar/IndustryLegend, Score-Mapping
  0-30 / 31-60 / 61-80 / 81-100). Computed Property `CustomerReputation.CurrentTier`.
- `IGameStateService.ReputationTierChanged`-Event mit `ReputationTierChangedEventArgs`
  (OldTier/NewTier/IsUp). Wird ausgeloest aus drei Stellen: CompleteActiveOrder (AddRating),
  GameLoopService.cs (event-ReputationChange), GameLoopService.PeriodicChecks (Showroom-Decay).
- Header-Badge in DashboardView: `ShowReputationTierBadge`/`ReputationTierName`/`ReputationTierColor`
  Properties am MainViewModel, Bronze/Silber/Gold-Farben.
- Spawn-Effekte in `OrderGeneratorService`: Stammkunden-Chance + Live-Order-Spawn-Chance werden
  per `tier.GetRegularCustomerBonus()` und `tier.GetLiveOrderSpawnChance()` modifiziert.
- Tier-Up-Celebration: MainViewModel.OnReputationTierChanged feuert FloatingText + Confetti +
  Sound nur bei Aufstieg (IsUp=true), nicht bei Abstieg.

**Prestige Single-Page-View (Task 2.2 — Foundation)**
- Neue `ActivePage.Prestige` + `IsPrestigeActive` + `PrestigeView.axaml/cs` als Vollbild-Page.
- View bindet auf `MainViewModel.DialogVM.*`-Properties (Tier-Optionen, ConfirmDialog-Texte,
  BonusPpPreview, ChallengePpPreview, RunDuration). Tier-Tabs + Live-Vorschau + grosser CTA.
- `GoBackCommand` als RelayCommand am MainViewModel (delegiert an `NavigateBack`).
- Existierender Modal-Pfad (`ShowPrestigeConfirmationAsync` → Bottom-Sheet) bleibt aktiv —
  Foundation fuer kompletten UI-Wechsel ist gelegt.

**Imperium-Sub-Tabs (Task 2.3 — Foundation)**
- `ImperiumSubTab` Enum (Workshops/Workers/Research/Equipment/Ascension).
- 5 `IsImperiumXxxActive` Bools + `SelectImperiumSubTabCommand` + `IsImperiumAscensionUnlocked`
  (LegendeCount &gt;= 3). Section-Migration der ImperiumView (~600 Zeilen) ist pendant.

**Goldschrauben-Oekonomie (Task 2.7)**
- Rebirth-Kosten Stern 4: 250 → 200 GS, Stern 5: 500 → 400 GS (10 Workshops = 1500 GS Ersparnis,
  ~14 Tage F2P-Reduktion). RebirthService.RebirthCosts-Tabelle.
- Rewarded GS-Ad: 8 → 12 GS (`ShopViewModel.cs::golden_screws_ad`-Case).
- Wiederholbarer Wochen-Meilenstein: alle 7 Prestiges +5 GS. Neuer State-Counter
  `PrestigeData.PrestigesSinceLastWeeklyReward` + `IncrementWeeklyPrestigeCounter()` +
  Reset-Logik in `CheckAndAwardMilestones`.

**Live-Orders pausieren (Task 2.8)**
- `Order.PausedAt` (DateTime?) + `Order.AccumulatedPauseDuration` (TimeSpan).
- `Order.IsExpired` + `LiveCountdownSeconds` nutzen `GetEffectiveNow()` — Pause-Dauer wird
  abgezogen, mit 5-Minuten-Cap gegen „Bunkern".
- `IGameStateService.PauseAllLiveOrders()` + `ResumeAllLiveOrders()`.
- `MainViewModel.PauseGameLoop()` + `ResumeGameLoop()` rufen Pause/Resume an. Android-Lifecycle
  (OnPause/OnResume) ist bereits an PauseGameLoop verdrahtet.

**Snapshot-Save (Task 2.4) — keine Aenderung**
- Aktueller `SaveGameService.SaveAsync` nutzt bereits `Task.Run + ExecuteWithLock` —
  off-thread Serialisierung. Echte CreateSnapshot-Implementierung wuerde Deep-Copy aller
  Sub-Objekte erfordern (~500 Zeilen) fuer unklaren Mehrwert.

**SkiaSharp-Disposal-Audit (Task 2.6)**
- `GameAssetService` hat IDisposable + ClearCache (Bestand).
- `GameIcon.ClearCache()` um `_pathMap.Clear()` erweitert (komplettes Cleanup).
- PipePuzzle-/Sawing-/etc. Renderer haben IDisposable mit umfassendem Dispose (Bestand).
- Static SKMaskFilter (Process-Lifetime) — bereinigt sich beim App-Shutdown.
- App.DisposeServices ruft GameLoopService/GameJuiceEngine/ServiceProvider/GameIcon/
  GameIconRenderer in der richtigen Reihenfolge auf.

**DialogViewModel-Aufspaltung (Task 2.5) — Phase 1 abgeschlossen**
- 4 Partial-Class-Files extrahiert: DialogViewModel.LevelUp.cs (30 Z.),
  DialogViewModel.Achievement.cs (25 Z.), DialogViewModel.Alert.cs (29 Z.),
  DialogViewModel.PrestigeSummary.cs (45 Z.).
- DialogViewModel.cs: 785 → 747 Zeilen. Bindings unveraendert (selbe Klasse via partial).
- Restliche Sektionen (Story/Hint/Confirm) bleiben in der Hauptdatei — sie sind eng mit
  Helper-Methoden (UpdatePrestigeDialogContent, ShowConfirmDialog mit TCS) verzahnt
  und brauchen tieferen Refactor.

### Sprint 3 Final-Pass (Stand 05.05.2026, alle Phase-A→B Aufgaben)

**3.1 Saison-Storyline RESX-Uebersetzungen** (Task 24)
- 240 RESX-Eintraege (4 Saisons × 5 Kapitel × 2 Texte × 6 Sprachen) in DE/EN/ES/FR/IT/PT.
- Zusaetzlich `SeasonStoryXxxTheme` Header-Strings.

**3.2 GuildCoopOrderService Firebase-Implementation** (Task 25)
- Vollstaendige Firebase-CRUD-Implementation: CreateInvite/Accept/Decline/SubmitScore/GetState/GetOpenForPlayer.
- HMAC-Signierung via `IGameIntegrityService.ComputeStringHmac` — Score-Tampering wird beim
  GetState-Read erkannt und Auftrag als Expired markiert.
- Reward-Berechnung: 50/50-Split + 25%-Bonus bei beidseitig Perfect (Score &gt;= 95).
- 5min Pending-Phase, 3min Active-Phase. UI in GuildView und Polling-Loop sind als
  separater Task pendant — Service-Logic ist build-fest.

**3.3 Reputation-Shop** (Sprint 3) — Phase 1 abgeschlossen (im Ursprungs-Sprint).

**3.4 WorkerAuctionService Firebase-Implementation** (Task 26)
- Bid-Logik: 10% Mindest-Erhoehung, 1s-Cooldown gegen Spam-Bidding, Geld-Locking via Delta-Subtraktion.
- HMAC-Signierung mit deterministischer AllBids-Sortierung (StringComparer.Ordinal).
- Refund-Logik: Verlierer bekommen ihr Geld zurueck im Settle.
- Spawn-Cron (5min-Intervall) und UI sind als separater Task pendant — Service ist build-fest.

**Imperium-Sub-Tabs UI** (Task 23)
- Sub-Tab-Leiste oben in ImperiumView mit 5 Buttons (Workshops/Workers/Research/Equipment/Ascension).
- Active-Indication via `Classes.SubTabActive`-Style. Ascension-Tab nur sichtbar nach
  3x Legende-Prestige (LegendeCount &gt;= 3).
- Section-Mapping innerhalb der View bleibt fuer einen tieferen UI-Refactor.

**RESX Sub-Tab-Keys** (Task 23 Bonus)
- 5 Keys (`ImperiumSubTabXxx`) in allen 6 Sprachen.

**Snapshot-Save Diagnostik** (Task 22)
- Stopwatch-Diagnostik im DEBUG-Build: Save-Snapshot-Latenz wird gemessen, &gt;50ms triggert
  Debug-Warnung. Aktuelles Pattern (Task.Run + ExecuteWithLock) bleibt.

**Game-Integrity ComputeStringHmac** (Sub-Task fuer 3.2/3.4)
- `IGameIntegrityService.ComputeStringHmac(string)` — generischer HMAC-SHA256 fuer Co-op +
  Auktions-Daten. Nutzt den gleichen Geraete-spezifischen Schluessel wie SaveGame-Signing.

### Sprint 3 Vollstaendiger Abschluss (Stand 05.05.2026, UI + Cron + Firebase-Rules)

**3.2 + 3.4 ViewModels mit Polling-Loop** (Tasks 28+29)
- `GuildCoopOrderViewModel` (ViewModels/Guild/): DispatcherTimer 2s waehrend offene Auftraege,
  ObservableCollection mit Items, Create/Accept/Decline-Commands, BadgeText fuer UI-Counter.
  Auto-Refresh auf `CoopOrderUpdated`-Event vom Service.
- `WorkerAuctionViewModel` (ViewModels/Auctions/): 1s-Polling waehrend aktiver Auktion +
  separater Countdown-Timer (1s) fuer EndsAt-Display. PlaceBid-Validation, BidError-Anzeige.
  HasActiveAuction/MinBidDisplay (10%-Erhoehung) als computed Properties.
- Beide IDisposable: stoppen Timer + unsubscribe-Events.

**Co-op + Auction Views** (Task 30)
- `Views/Guild/GuildCoopOrderView.axaml`: Card mit Empty-State, Item-Liste mit Accept/Decline-
  Buttons (MinHeight 44dp), Refresh-Button.
- `Views/Auctions/WorkerAuctionView.axaml`: Bid-Form mit Worker-Name, Hoechstgebot, Mindestgebot,
  Countdown-Badge, Bid-Input + Place-Bid-Button. Error-Feedback bei abgelehnten Bids.
- Beide MVVM-konform: nur `InitializeComponent()` im Code-Behind.
- 11 RESX-Keys (`CoopOrders*`, `Coop*`, `Auction*`, `Refresh`) in allen 6 Sprachen.

**Firebase Database-Rules** (Task 31)
- `database.rules.json` (beide Kopien synchron — Repo-Root + Apps-Ordner): neue Pfade
  `guilds/$id/coopOrders/$orderId` + `guilds/$id/auctions/$auctionId`. Schreib-Berechtigungen:
  Gildenmitglieder. Schema-Validation fuer alle Felder (createdBy/invitedPlayer string, status
  numerisch 0-3, Score 0-100, Bid 0-1e12, hmac max 128 Zeichen).
- Wert-Caps gegen Manipulation: BaseReward max 1 Billion EUR, allBids-Schluessel-Validation.
- **Deploy-Befehl:** `npx firebase-tools deploy --only database --project handwerkerimperium-487917`.

**WorkerAuction-Cron im GuildTickService** (Task 32)
- Optionaler `IWorkerAuctionService`-Parameter im Konstruktor. Alle 5min (Tick % 300 == 90)
  wird `RefreshAuctionAsync()` aufgerufen — pollt aktuelle Auktion + settled abgelaufene.
- Offset 90 vermeidet Kollision mit den 4 anderen Cron-Checks (Boss/Hall/Achievement/War).

**DI-Registration** (App.axaml.cs)
- `GuildCoopOrderViewModel` + `WorkerAuctionViewModel` als Singletons. Services bereits
  in v2.1.0-Phase-A registriert.

**MVVM-Status nach Aufspaltung**
- Alle 6 neuen Code-Behind-Dateien sauber: `InitializeComponent()` only.
- Keine `App.Services.GetRequiredService` Calls.
- Keine `DataContext = ...` Setter.
- Keine Business-Logik in Views.

### v2.0.37 Audit-Hotfixes (Stand 07.05.2026)

Basis: `HandwerkerImperium_Audit_v2.0.36.md` mit 40+ Befunden in 6 Kategorien.
Verifikation per 3 parallelen `code-review`-Agents — von 22 ueberprueften Befunden waren
14 bereits gefixt (K1, K2, P6, P7, L1-L4 u.a.), 8 echt offen.

**K4 + NC2 + NC4 + P1 — NotificationCenterService Race-Condition + Lock-Delegation + Cap + Cache**
- Eigenes `_lock` ersetzt durch `_gameStateService.ExecuteWithLock(...)` — gleicher Lock wie
  SaveGameService verhindert „Collection was modified" beim AutoSave waehrend Add/Dismiss.
- Inbox-Cap = 100 Items: aelteste werden ueber CreatedAt evicted bei Ueberlauf.
- `Items` Property cacht sortierte Liste mit Dirty-Flag — vermeidet ~60 Allokationen/s
  (LINQ `OrderByDescending().ToList()`) bei offener Bell.
- Alle 5 Mutationen (Add/Dismiss/Clear/MarkAllSeen/Contains) auf ExecuteWithLock umgestellt.

**K5 — V5→V6 Migration** (Phantom-Bug)
- Audit-Empfehlung war spekulativ: `Order.IsAccepted` existiert in V5 nicht. Pause-Mechanik
  (Order.PausedAt) wurde erst in V6 (Sprint 2) eingefuehrt. Der ursprueng­liche Migrations-
  Code mit nur `state.ActiveOrder` ist korrekt — kein Datenverlust-Pfad. Kommentar im Code
  dokumentiert die Audit-Annahme als nicht zutreffend.

**K6 — Cloud-Save Version-Mismatch-Schutz**
- `GameState.CurrentStateVersion` (const = 6) — referenzierbar fuer Version-Vergleiche.
- `MainViewModel.CheckCloudSaveAsync`: Wenn `metadata.StateVersion > CurrentStateVersion`
  zeigt die App einen Alert ("App-Update erforderlich") und bricht den Cloud-Download ab —
  schuetzt vor State-Korruption durch Wiederholungs-Migration.
- 2 neue RESX-Keys (`CloudSaveTooNewTitle`, `CloudSaveTooNewBody`) in 6 Sprachen.

**Co-op Race-Condition bei gleichzeitigem Score-Submit**
- Frueher: Beide Spieler `SetAsync(path, state)` (PUT) → Last-Write-Wins ueberschrieb den
  Score des ersten Submitters.
- Jetzt: `UpdateAsync(path, { player1Score: X })` (PATCH) — atomar nur das eigene Feld.
  Status-Uebergang in 2. Patch idempotent (`fresh.Status == Active && both scores set`).
- HMAC-Schema reduziert auf stabile Felder (OrderId, CreatedBy, InvitedPlayer, BaseReward,
  MiniGameType) — Score/Status werden inkrementell gepatcht, sind nicht im HMAC.
  Schutz ueber Firebase-Rules (Score 0-100 validate, Status 0-3).

**L5 — Reputation-Tier Hysterese**
- `CustomerReputation.CurrentTier` jetzt persistiert (war vorher computed). `RecomputeTier()`
  nutzt `FromScoreWithHysteresis(score, currentTier)` mit 3-Punkte-Buffer (Up bei 31/61/81,
  Down bei 28/58/78). Verhindert UI-Flackern an Tier-Boundaries (z.B. Stammkunden +1 /
  Decay -1 abwechselnd).
- `RaiseReputationTierChangedIfNeeded` ruft `RecomputeTier()` statt `FromScore`.

**U3 — Disabled-Button Visual-State**
- Globaler Style `Button:disabled` in App.axaml: Opacity 0.45 + TextMutedBrush.
  Vorher wirkte ein deaktivierter Button wie Render-Bug.

**U4 — Imperium Sub-Tab Symmetrie (Ascension)**
- Tab IMMER sichtbar (5 Tabs), gelocked-Variante zeigt Lock-Icon ueberlagert auf dem Star.
  `IsEnabled="{Binding IsImperiumAscensionUnlocked}"` — Layout springt nicht beim Unlock.

**P2 — WiringGameRenderer Background-Reference**
- `_background = null;` in Dispose. SKBitmap selbst gehoert dem GameAssetService-Cache —
  Renderer ist Reference-Holder, kein Owner. Re-Use-Szenarien (Cache-Eviction + erneutes
  Render) holen die Bitmap frisch.

### Audit-Befunde STATUS (Stand 08.05.2026)

| Sektion | Status |
|---------|--------|
| K1 (WiringRenderer Overflow) | gefixt vor v2.0.37 (Math.Min-Clamp + Difficulty-Tuple max=7) |
| K2 (ParallelOrders Lock-Race) | gefixt vor v2.0.37 (vollstaendige Lock-Abdeckung) |
| K3 (RollingResults List<bool>) | bewusste Design-Entscheidung — List<bool> bleibt fuer JSON-Roundtrip-Stabilitaet, Lock + N=20 macht O(20) trivial |
| K4 (NotificationCenter Lock) | **gefixt v2.0.37** |
| K5 (V5→V6 Migration) | nicht zutreffend (Audit-Annahme falsch) |
| K6 (Cloud-Save Version) | **gefixt v2.0.37** |
| P1 (Items LINQ-Cache) | **gefixt v2.0.37** (mit K4 zusammen) |
| P2 (Bitmap-Leak Wiring) | **gefixt v2.0.37** (Reference-Null-Pattern) |
| P3 (GameIcon Race) | akzeptabel — `_bitmapCache`/`_brushCache`/`_pathMap` sind ConcurrentDictionary, ClearCache nur bei App-Shutdown |
| P4 (Save-Lock) | akzeptabel (off-thread via Task.Run, Lock-Hold ~5-20ms im DEBUG-Stopwatch verifiziert) |
| P5 (ExpireOldLiveOrders Early-Exit) | **gefixt v2.0.39** (LiveOrderCount-Property + Early-Exit-Guard) |
| P6 (Prestige-Shop O(n)) | gefixt vor v2.0.37 |
| P7 (WorkshopLookupCache) | gefixt vor v2.0.37 |
| P8 (CraftingInventory Lazy-Init) | bereits Eager-Init im GameState (`= new()`) — Audit-Annahme veraltet |
| L1 (verwaiste ParallelOrders) | gefixt vor v2.0.37 |
| L2 (Pause-Overflow) | gefixt vor v2.0.37 |
| L3 (Mood-Decay negativ) | gefixt vor v2.0.37 (Math.Min-Cap) |
| L4 (DivByZero) | gefixt vor v2.0.37 (ApplyBoostsProRata Guard) |
| L5 (Reputation Hysterese) | **gefixt v2.0.37** |
| L6 (wasCapped ungenutzt) | bereits in MainViewModel.Init.cs umgesetzt (durationText "(Max. {h}h)" + OfflineEfficiencyHint) |
| U1 (WhatsNew-Dialog) | **gefixt v2.0.39** (WhatsNewService + Versions-Map kumulativ + 7 RESX-Keys × 7 Files) |
| U2 (Header-Density) | bereits konditional (Reputation-Badge ab Tier 2, Streak ab 7 Tagen) |
| U3 (Disabled-Button) | **gefixt v2.0.37** |
| U4 (Sub-Tab Symmetrie) | **gefixt v2.0.37** |
| U5 (Escape-Key Desktop) | **gefixt v2.0.39** (Tunnel-KeyDown-Handler in MainView delegiert HandleBackPressed) |
| U6 (Backdrop-Dismiss konsistent) | durch U5 abgedeckt (Escape schliesst obersten Dialog) + DialogVM-Dismiss-Methoden vorhanden |
| U7 (Reputation-Tier-Up Modal) | **gefixt v2.0.39** (Achievement-Dialog mit Tier-Effekten via 3 RESX-Keys × 7 Files) |
| U8 (Pull-to-Refresh) | OFFEN — separater Sprint (RefreshContainer-Behavior-Implementierung) |
| U9 (Praktikanten-Promotion-Visual) | **gefixt v2.0.39** (StatusInternReadyForPromotion ueberlagert alle anderen Status im WorkerProfile) |
| U10 (Long-Press-Hint) | **gefixt v2.0.39** (LongPressBulk-Hint nach 2. Workshop-Upgrade via ContextualHints) |

### AAA-Audit-Umsetzung 09.05.2026 (8 von 12 Punkten realisiert)

Vollständige Bearbeitung des `AAA_AUDIT_2026-05-08.md` (15 Wochen Plan).
Der Audit war teilweise überholt — wichtigste Befunde verifiziert:

| Audit-Befund | Verifikation | Umsetzung |
|--------------|--------------|-----------|
| P0.1 "Null Tests" | **FALSCH** — 38 Test-Dateien existieren, 996/1009 grün | + 22 neue Property-Based Migration- und Performance-Benchmark-Tests, alle grün |
| P0.2 "Kein CI/CD" | **PARTIELL** — `.github/workflows` leer | `ci.yml` (Build + Test + Firebase-Rules-Lint) |
| P0.3 "Cinematic fehlt" | korrekt | `PrestigeCinematicRenderer` (4-Phasen, 14s, Skip+Tap-To-Continue) |
| P1.1 "Live-Ops zu flach" | 11 Keys / 56 Events | + 18 RemoteConfig-Keys, + 17 Analytics-Events, A/B-Cohort-Tracking |
| P1.5 "MainVM 2422 Z." | korrekt | Helper-Variante: `PageNavigationHelper` extrahiert, MainVM 2422→2392 |
| P2.2 "Onboarding 4 Dialoge" | korrekt | Story-Skip-Button (Ch.1+Tutorial only) + Analytics-Funnel-Events |
| P2.3 "Kein Music" | korrekt (15 SFX, 0 Music) | `IAudioService.PlayMusicAsync(MusicTrack, crossfade)` + AudioFocus |
| P0.1 Layer 3 (Headless-UI) | als 1W-Sprint dokumentiert (zu groß für Session) | `CriticalPathHeadlessTests.cs` Skelett (skipped) |
| P1.2 CJK-Lokalisation | `DEFER` (5k€ Translation-Budget extern) | dokumentiert |
| P1.3 Monetization-Bundles | `DEFER` (4 Wochen) | dokumentiert |
| P2.1 Content-Pipeline | `DEFER` (Google Sheets) | dokumentiert |
| P2.4 Worker-Spine-Animation | `DEFER` (15k€) | dokumentiert |

**Neu erstellt:**
- `Models/PrestigeCinematicData.cs`, `Graphics/PrestigeCinematicRenderer.cs`, `Helpers/PageNavigationHelper.cs`
- `tests/HandwerkerImperium.Tests/{SaveGameMigrationTests,PerformanceBenchmarkTests,CriticalPathHeadlessTests}.cs`
- `.github/workflows/ci.yml`

**Erweitert:**
- `Models/AnalyticsEvents.cs` (+17 Events: Worker, Coop, Auction, RepShop, Equipment, Live-Order, Cinematic, Onboarding-Funnel)
- `Models/RemoteConfigKeys.cs` (+18 Keys: Difficulty/Live-Order/WorkerMarket-Weights, Premium-Fallback, Bundle-Foundation, Theme-Override, Kill-Switches, Cross-Promo, UX-Onboarding)
- `Models/AnalyticsUserProperties.cs` (+TestCohort, InstallCohortWeek, PlayerIdProperty)
- `Services/AnalyticsService.cs` (Cohort-Hash + ISO-Week)
- `Services/Interfaces/IAudioService.cs` + `AudioService.cs` + `AndroidAudioService.cs` (MusicTrack-Enum, Crossfade, AudioFocus)
- `Services/Interfaces/IPrestigeService.cs` + `PrestigeService.cs` (CinematicReady-Event mit Snapshot vor Reset)
- `ViewModels/MainViewModel.cs` (Helper-Delegation, Cinematic-Forward, StorySkip-Tracking)
- `ViewModels/DialogViewModel.cs` (CanSkipStory + SkipStoryCommand + StorySkipRequested-Event)
- `Views/MainView.axaml(.cs)` (PrestigeCinematicCanvas), `Views/Dialogs/StoryDialog.axaml` (Skip-Button)
- `HandwerkerImperium.Shared.csproj` (InternalsVisibleTo), `SaveGameService.cs::MigrateState` (private→internal)
- 7 RESX-Files (StorySkip in DE/EN/ES/FR/IT/PT + neutral)

**Stale Tests gefixt** (alle 8 vorherigen Fehler):
- `EquipmentTests.ShopPrice` — Theory-Werte auf 3/8/18/40 GS aktualisiert
- `RebirthServiceTests.GetRebirthCost_FuenfterStern` — Method+Assertion auf 400 GS angepasst
- `LevelThresholdsTests.QuickJobs_IstLevel5` → `IstLevel2` (Onboarding-Beschleunigung)
- `LevelThresholdsTests.ProgressiveDisclosureReihenfolge_IstLogisch` — Erwartung auf neue Reihenfolge angepasst
- `LevelThresholdsTests.HintLevels_SindKonsistentMitFeatureLevels` — fixt **Source-Bug**: `HintQuickJobs=5` widersprach `QuickJobs=2`. Source auf 2 korrigiert.

**Result:** 1004 von 1004 grün (5 Skip = Headless-Skelett für Layer-3-Sprint).

### Erweiterung: Foundation-Pässe für DEFER-Punkte

Auch die als "DEFER" markierten Audit-Punkte haben eine umsetzbare Foundation bekommen
(volle Realisierung erfordert externe Resourcen wie Translation-Budget oder Google-Sheet-Setup).

**P1.3 Daily-Bundle-Foundation:**
- `Models/DailyBundleOffer.cs` — Bundle-Slot mit SKU, Bonus-Items, Expiry
- `Services/Interfaces/IDailyBundleService.cs` + `Services/DailyBundleService.cs` —
  RemoteConfig-getriebene 7-Slot-Rotation, IAP-Flow, idempotente Bonus-Verbuchung
- `Services/RemoteConfigKeys.DailyBundleEnabled` + `DailyBundleSkus` (JSON-Array)
- DI-Registration in `App.axaml.cs`
- **Offen für Robert:** SKUs in Google Play Console anlegen + RemoteConfig-JSON setzen + ShopView-Bundle-Card

**P2.1 Content-Pipeline-Skelett:**
- `tools/ContentPipeline/sync_content.py` — Google-Sheets→C#+RESX Sync-Skript (Stub-Modus + Live-Modus)
- `tools/ContentPipeline/README.md` — Setup-Anleitung
- **Offen für Robert:** Google Cloud Service Account + Sheet anlegen + CI-Hook in `ci.yml`

**P1.2 CJK Phase 1 (ohne Translation-Budget):**
- `Graphics/CjkFontResolver.cs` — System-Font-Resolver für zh-CN/zh-TW/ja/ko (NotoSansCJK / PingFang / YuGothic / MalgunGothic Fallback-Kette)
- `tests/HandwerkerImperium.Tests/CjkFontResolverTests.cs` — 13 Property-Based-Tests, alle grün
- `Resources/Strings/AppStrings.{zh-CN,ja,ko}.resx` — Stub-RESX mit Test-Glyphen für Render-Validation
- **Offen für Robert:** Crowdin-Setup + ~5k€ Translation-Budget für ~3000 Strings × 3 Sprachen + Native-Speaker-QA

**P2.4 Worker-Spine-Animation — bleibt formal DEFER:**
- 6-8W Engineering + ~10-15k€ Asset-Budget für 50 animierte Charaktere
- Vor Soft-Launch entscheiden (Pixel-Art ist nicht inhärent ein AAA-Killer)

### AAA-Audit Loose-End-Pass 09.05.2026 (Polish + Hardening)

Nach der Umsetzung wurde die Codebase auf lose Enden geprüft und folgendes nachgezogen:

**Cinematic Auto-Dismiss + Audio-Trigger:**
- `PrestigeCinematicRenderer.Update()` schaltet sich nach 8s Reward-Phase auto-ab (Bug-Fix: vorher lief die Cinematic ewig wenn nicht getippt)
- `MainViewModel.OnPrestigeCinematicReady` triggert `MusicTrack.Celebration` mit Crossfade
- `OnPrestigeCinematicDismissed` schaltet zurück auf `MusicTrack.IdleWorkshop`

**AndroidAudioService Dispose:**
- `IDisposable` implementiert. Released: Crossfade-Timer, AudioFocus-Listener+Request, SoundPool, MediaPlayer (alle mit Try/Catch wegen Native-Resource-Verträge)

**DailyBundleService Init-Hook:**
- `HandwerkerImperiumLoadingPipeline` ruft `IDailyBundleService.InitializeAsync()` direkt nach `IRemoteConfigService.InitializeAsync()` auf — Bundle nutzt die jetzt verfügbaren Slots

**Cinematic-Renderer-Tests** (15 Tests):
- Phasen-Übergänge (Money→Badge→Multiplier→Reward)
- Skip-Logik (vor/nach 2s)
- Auto-Dismiss nach 8s in Reward-Phase
- Theory: Alle 7 Tier-Werte ohne Exception
- Dispose-Idempotenz

**DailyBundleService-Tests** (10 Tests):
- Disabled-Pfad ohne Feature-Flag
- JSON-Parse-Fehler-Handling (kein Crash)
- DayOfWeek-Mapping (Mo=0..So=6)
- Purchase-Flow: Verbuchung bei Erfolg, kein State-Change bei Fehler
- Default-Initialisierung ohne Bonus-Felder

**Analytics-Events live verdrahtet** (4 neue Live-Events):
- `WorkerPromoted` — `WorkerService.PromoteIntern` (Onboarding-Funnel)
- `WorkerQuit` — `WorkerService.UpdateWorkerStates` Mood-Quit-Pfad (Retention)
- `ManagerUnlocked` — `ManagerService.CheckAndUnlockManagers` (Mid-Game-Meilenstein)
- `EquipmentDropped` + `EquipmentEquipped` — `EquipmentService.TryGenerateDrop` + `EquipItem` (Engagement)

**Headless-UI Layer 3 LIVE statt Skelett** (2 Tests):
- `HeadlessSmokeTests` mit `[AvaloniaFact]` + `AvaloniaTestApplication`-Attribut
- `Avalonia.Headless` + `Avalonia.Headless.XUnit` zu `Directory.Packages.props` hinzugefügt
- `UseHeadlessDrawing=true` für CI ohne Display-Server
- Beweist: Window/StackPanel/TextBlock rendern + Layout-Pass funktioniert
- `CriticalPathHeadlessTests` bleibt als **Pattern-Dokumentation** für DI-Mock-aufwendige Tests

**Result:** 1050 von 1050 Tests grün (5 Skip = dokumentiertes Skelett). Build clean (Shared + Android).

**Neu erstellt:**
- `tests/HandwerkerImperium.Tests/{PrestigeCinematicRendererTests,DailyBundleServiceTests,HeadlessSmokeTests}.cs`

**Erweitert:**
- `Graphics/PrestigeCinematicRenderer.cs` (Auto-Dismiss-Logic in Update)
- `ViewModels/MainViewModel.cs` (Audio-Trigger im Cinematic-Forward + Idle-Track-Restore)
- `HandwerkerImperium.Android/AndroidAudioService.cs` (`IDisposable`-Pattern)
- `Loading/HandwerkerImperiumLoadingPipeline.cs` (Bundle-Init nach RemoteConfig)
- `Services/{Worker,Manager,Equipment}Service.cs` (`IAnalyticsService`-Injection + 4 Live-Event-Triggers)
- `tests/HandwerkerImperium.Tests/HandwerkerImperium.Tests.csproj` (Avalonia + Headless-Packages)
- `Directory.Packages.props` (`Avalonia.Headless` 11.3.13)

### Code-Review Final-Pass 09.05.2026 (alle 7 Findings sauber abgearbeitet)

Code-Review-Agent (`code-review`) hat 7 Findings gemeldet — **alle gefixt**:

| Finding | Schwere | Datei | Fix |
|---------|---------|-------|-----|
| 1 — RemoteConfig-Timeout-Race | KRITISCH | `Loading/HandwerkerImperiumLoadingPipeline.cs` | `ContinueWith`-Hook für deferred Bundle-Init bei Timeout (statt sofort mit leeren Werten zu initialisieren) |
| 2 — DailyBundle Tageswechsel-Race | HOCH | `Services/DailyBundleService.cs` | `_rotateLock` um Tageswechsel-Detection — verhindert Doppel-`BundleRotated`-Events |
| 3 — SkipButtonBounds-Stale | NIEDRIG | `Graphics/PrestigeCinematicRenderer.cs` | Bounds-Reset in `Start()` + Auto-Null in `Update()` wenn nicht `IsSkipEnabled` |
| 4 — CjkFontResolver Cache-null | MITTEL | `Graphics/CjkFontResolver.cs` | Sentinel-Flags `s_resolvedXx` verhindern wiederholtes `LoadFamily` bei null-Result |
| 5 — PageNavigationHelper O(n²) | MITTEL | `Helpers/CappedNavigationStack.cs` (NEU) | Ringbuffer mit O(1)-Push/Pop ersetzt Stack-Rebuild bei Cap-Überschreitung |
| 6 — NextRandom thread-affin | MITTEL | `Graphics/PrestigeCinematicRenderer.cs` | `Random.Shared` (thread-safe seit .NET 6) statt eigener xorshift32-State |
| 7 — Volume-Loss bei Duck→Loss | NIEDRIG | `HandwerkerImperium.Android/AndroidAudioService.cs` | `_currentMusicVolume`-Tracking — Pause merkt geduckten Wert statt MusicMaxVolume |

**Result:** 1050/1050 Tests grün, beide Builds clean (Shared + Android), alle 7 Findings adressiert.

### v2.0.39 Audit-Hotfixes (Stand 08.05.2026)

Letzter Pass des `HandwerkerImperium_Audit_v2.0.36.md` — verbleibende OFFEN-Punkte aus
v2.0.37 abgearbeitet. Assembly-Version Shared.csproj von 2.0.32 auf 2.1.0 synchronisiert
(matched Android `ApplicationDisplayVersion=2.1.0`).

**P5 — ExpireOldLiveOrders Early-Exit**
- Neue Property `IOrderGeneratorService.LiveOrderCount` als O(n)-Lock-freier Scan ueber
  `state.AvailableOrders` mit `IsLive`-Filter.
- `ExpireOldLiveOrders()` returnt frueh wenn `LiveOrderCount == 0` — vermeidet die
  RemoveAll-Iteration + Lock-Aequisition alle 3 Ticks (typisch leerer Pool).

**U1 — WhatsNew-Dialog beim Update**
- Neuer `IWhatsNewService` + `WhatsNewService` (Singleton) mit kumulativer Versions-Map
  `s_releases[(version, featureKeys[])]`. Aktuelle Eintraege fuer 2.0.36 + 2.0.37.
- `SettingsData.LastWhatsNewVersion` (default "0.0.0") persistiert die zuletzt gesehene
  Version. Bei Spielern mit `LastSavedAt == default || TotalOrdersCompleted == 0` wird
  der Dialog beim ersten Start uebersprungen, aber die Versions-Marke trotzdem gesetzt.
- `MainViewModel.ShowWhatsNewDeferredAsync()`: 2.5s Initial-Delay + bis zu 4s Wartezeit
  falls andere Startup-Dialoge offen sind. Fire-and-Forget, blockiert keinen Spielstart.
- 7 neue RESX-Keys (`WhatsNewTitle`, `WhatsNewBell`, `WhatsNewStrategyEV`,
  `WhatsNewReputation`, `WhatsNewReputationShop`, `WhatsNewImperiumTabs`,
  `WhatsNewWhatsNewItself`) in 7 Dateien (DE/EN/ES/FR/IT/PT + neutral).

**U5 — Escape-Key auf Desktop**
- `MainView` registriert in seinem Ctor einen Tunnel-Phase-`KeyDownEvent`-Handler. Bei
  `Key.Escape` wird `_vm.HandleBackPressed()` delegiert — selber Pfad wie Android-Back.
- Schliesst den obersten sichtbaren Dialog (Achievement, Confirm, Story, Hint etc.) und
  gibt `e.Handled = true` zurueck wenn etwas geschlossen wurde.

**U7 — Reputation-Tier-Up Achievement-Dialog**
- `OnReputationTierChanged` zeigt nun bei Aufstieg ueber Beginner einen
  `DialogVM.AchievementDialog` mit Tier-Name + Effekt-Beschreibung. Floating-Text +
  Confetti + Sound bleiben (kombinieren sich mit dem Modal). Bei Tier-Abstieg
  unveraendert still.
- 3 neue RESX-Keys (`RepTierCityKnownEffects`, `RepTierRegionStarEffects`,
  `RepTierIndustryLegendEffects`) in 7 Dateien.

**U9 — Praktikanten-Promotion Visual**
- `WorkerProfileViewModel.UpdateFromWorker()` zeigt fuer Praktikanten mit
  `InternAwaitingPromotion=true` den Status "Bereit zur Promotion" — ueberlagert
  IsTraining/IsResting/IsTired/IsWorking. Spieler erkennt die ausstehende Entscheidung
  ohne den Promotion-Dialog erst aufzumachen.
- 1 neuer RESX-Key (`StatusInternReadyForPromotion`) in 7 Dateien.

**U10 — Long-Press-Bulk-Discoverability**
- Neuer `ContextualHints.LongPressBulk` (2 RESX-Keys: `HintLongPressBulkTitle`,
  `HintLongPressBulkText` in 7 Dateien). Triggert in `MainViewModel.OnWorkshopUpgraded`
  nach dem 2. Workshop-Upgrade (`HasSeenHint(WorkshopDetail.Id)` + nicht
  `HasSeenHint(LongPressBulk.Id)`) und nicht waehrend aktivem Hold-to-Upgrade.

**Geaenderte / neue Dateien (v2.0.39)**

Neu:
- `Services/Interfaces/IWhatsNewService.cs`
- `Services/WhatsNewService.cs`

Erweitert:
- `App.axaml.cs` (DI-Registrierung)
- `Models/SettingsData.cs` (LastWhatsNewVersion)
- `Models/ContextualHint.cs` (LongPressBulk)
- `Services/Interfaces/IOrderGeneratorService.cs` (LiveOrderCount)
- `Services/OrderGeneratorService.cs` (LiveOrderCount + Early-Exit)
- `ViewModels/MainViewModel.cs` (WhatsNewService-Feld + Tier-Up-AchievementDialog +
  LongPressBulk-Hint-Trigger)
- `ViewModels/MainViewModel.Init.cs` (ShowWhatsNewDeferredAsync)
- `ViewModels/WorkerProfileViewModel.cs` (StatusInternReadyForPromotion)
- `Views/MainView.axaml.cs` (Escape-KeyDown-Handler)
- `HandwerkerImperium.Shared.csproj` (Version 2.0.32 → 2.1.0)
- 7 RESX-Dateien (12 neue Keys je Sprache: WhatsNew*7, RepTier*Effects*3, HintLongPressBulk*2,
  StatusInternReadyForPromotion)

### Sprint 3 Verdrahtungs-Pass (Stand 06.05.2026, Tasks 33/36/38/39/40)

**Co-op + Auktion in Combat-Tab (Task 33)**
- `GuildView` Combat-Tab bettet `GuildCoopOrderView` + `WorkerAuctionView` ein.
- `GuildViewModel.CoopOrderVM` + `AuctionVM` Properties (DI per Constructor).
- `OnActiveSubTabChanged`: Combat-Tab oeffnet/schliesst startet/stoppt Polling beider VMs
  (spart Firebase-Requests + Battery, wenn Tab nicht offen).
- Dispose stoppt Polling beider VMs sauber.

**Imperium-Sub-Tabs Section-Bindings (Task 36)**
- `ImperiumView` Sektionen umfassen jetzt jeweils einen `IsImperium*Active`-StackPanel-Container:
  Workshops (Buildings), Workers (Quick-Access), Research (ActiveProcesses), Equipment
  (Hinweis-Card), Ascension (Prestige-Bereich). Wechsel ueber die Sub-Tab-Leiste blendet
  jeweils nur eine Sektion ein.
- Neuer RESX-Key `EquipmentSectionHint` in allen 6 Sprachen + neutral.

**Auktions-Spawn + NPC-Bot-Bidding (Task 38)**
- `IWorkerAuctionService.SpawnAuctionIfMasterAsync()`: Master-Client-Pattern (Spieler mit
  lexikografisch kleinster `PlayerId` in der Mitgliederliste fuehrt Spawn aus, deterministisch
  ohne Server). Solo-Spieler ist immer Master.
- `IWorkerAuctionService.RunNpcBotTickAsync()`: Bots bieten zufaellig hoeher (35% Chance/Tick,
  5-25% Increment, Tier-spezifisches Bot-Maximum 50k/250k/1M EUR).
- `GuildTickService` ruft alle 5min `RefreshAuctionAsync` + `SpawnAuctionIfMasterAsync`
  sequentiell, alle 5s `RunNpcBotTickAsync` waehrend aktiver Auktion (Master-Side).
- Auktions-Worker werden mit zufaelligen Namen + Tier-Verteilung 70%/25%/5% (S/SS/SSS) erzeugt.

**Co-op SubmitScore-Hook im BaseMiniGameViewModel (Task 39)**
- `BaseMiniGameViewModel.ActiveCoopOrderId` + `ActiveCoopIsPlayer1` (static, vom Co-op-Flow
  vor Start gesetzt). Optionaler `IGuildCoopOrderService`-Konstruktor-Parameter.
- Beim Spielende (`IsLastTask`): `SubmitScoreAsync(orderId, score, isP1)` als Fire-and-Forget.
  Score-Mapping ueber alle Tasks: Perfect=100, Good=75, Ok=50, Miss=0 (Durchschnitt fuer
  Multi-Task-Orders, `ComputeCoopScore`-Helper).
- Alle 10 MiniGame-VMs haben den optionalen `coopOrderService`-Parameter im Konstruktor.

**Co-op Player-Picker (Task 40)**
- `GuildMemberDisplay.PlayerId` ergaenzt — wird aus `GuildMemberInfo.Uid` (= PlayerId) befuellt.
- `GuildCoopOrderViewModel.AvailableMembersProvider` (Func): liefert wahlbare Member.
  Verdrahtet im `GuildViewModel`-Konstruktor — filtert eigenen Spieler raus.
- `OpenPickerCommand` / `ClosePickerCommand` / `PickMemberCommand` + `IsPickerOpen` Property.
- `GuildCoopOrderView`: "Co-op erstellen"-Button + Modal-Overlay mit Member-Liste.
- Neue RESX-Keys `CoopCreateInvite` + `CoopPickMemberTitle` in allen 6 Sprachen.

**Auktions-Sieg → Worker-Pool (Task 42, Lueckenfix)**
- `WorkerAuctionService` bekommt `IWorkerService` injiziert. Bei Sieg
  (`HighestBidderId == PlayerId`) wird `Worker.CreateForTier` mit dem Auktions-Tier+Namen
  aufgerufen und via `IWorkerService.HireWorker` an den ersten freigeschalteten Workshop
  uebergeben (Default Carpenter). Spieler kann den Worker spaeter via TransferWorker umsetzen.

**Co-op Accept/Create → MiniGame-Navigation (Task 43, Lueckenfix)**
- `GuildCoopOrderViewModel.StartCoopMiniGame(state)`: setzt
  `BaseMiniGameViewModel.ActiveCoopOrderId` + `ActiveCoopIsPlayer1` und feuert
  `NavigationRequested` mit `state.MiniGameType.GetRoute()`.
- Aufruf in beiden Flows: `CreateInviteAsync` (Initiator startet sofort) und `AcceptAsync`
  (eingeladener Spieler startet bei erfolgreichem Accept).
- `IFirebaseService` als zweiter Konstruktor-Parameter im VM (DI-Container automatisch).
- Event-Forwarding im `GuildViewModel`: `CoopOrderVM.NavigationRequested → NavigationRequested`
  → MainViewModel routet ueber `INavigationService` ins MiniGame.
- Damit ist der Co-op-Loop geschlossen: Picker → Create/Accept → MiniGame → Score-Submit
  (an `coopId` aus `ActiveCoopOrderId`) → SettleRewards bei beiden Spielern.

**Co-op Reward Polling-basiert (Task 44, Race-Condition-Fix)**
- `SettleRewardsAsync` entfernt — frueher hat es nur auf dem Client gelaufen, dessen Score
  als zweiter eintraf. Der erste Submitter hat das Reward-Auszahlung verloren, weil sein
  Polling den Completed-Auftrag aus `GetOpenForPlayerAsync` rausgefiltert hat.
- Neuer Pfad: `TryClaimCompletedReward(state)` ist idempotent ueber `GameState.ClaimedCoopOrderIds`.
  Wird in `SubmitScoreAsync` (eigener Submit) UND in `GetOpenForPlayerAsync` (Polling-Pfad
  fuer den anderen Spieler) aufgerufen — beide Spieler bekommen ihren Anteil garantiert,
  niemals doppelt.
- Reward-Berechnung gleich wie zuvor: `BaseReward * RewardSplit * (bothPerfect ? 1.25 : 1.0)`.

**Auktion-Recovery nach App-Restart (Task 45, Idempotenz-Fix)**
- `GameState.ClaimedAuctionIds` (List<string>) — analog zu CoopOrders.
- `WorkerAuctionService.ApplyRefunds`: Idempotent ueber `ClaimedAuctionIds` — Doppel-Pay
  und Doppel-Worker-Hire bei wiederholtem Polling oder App-Restart vermieden.
- `DiscoverAndRecoverAsync`: Beim ersten Refresh nach App-Start (CurrentAuction == null)
  werden alle Auktionen der Gilde gelesen, ungeclaimte Settled-Auktionen werden ueber
  `ApplyRefunds` nachgeholt, und die juengste aktive Auktion wird als CurrentAuction gesetzt.
  Verhindert dass Spieler Geld + Worker verlieren wenn die Settle-Transition waehrend App
  geschlossen passiert ist.

### Sprint 1 v2.0.36 (Stand 05.05.2026, Quick-Wins-Sprint)

**Onboarding-Beschleunigung (Task 1.1)**
- `LevelThresholds.QuickJobs` von 5 → 2 (erstes MiniGame innerhalb 90 Sekunden).
- `StoryChapter` um `RequiredQuickJobsCompleted` erweitert. Story Ch.2 (`tutorial_orders`)
  triggert jetzt nach erstem QuickJob, nicht mehr Level-basiert.
- `INavigationHost.CheckForNewStoryChapter()` erlaubt Services (NavigationService),
  Story-Trigger nach QuickJob-Completion auszuloesen.

**Notification-Center / Bell-UI (Task 1.2)**
- Neue Service-Schicht: `INotificationCenterService` + `NotificationCenterService` (Singleton).
- `NotificationItem`, `NotificationKind` (OfflineEarnings/DailyReward/WelcomeBackOffer/
  AchievementUnlocked/StreakSaved/NewStoryChapter/LiveOrderAvailable).
- Persistenz in `GameState.NotificationInbox` (Default `[]`, V6-kompatibel).
- `NotificationCenterViewModel` haengt am Service, feuert `ItemActivated` an MainViewModel,
  das via Switch-Statement die richtige Aktion ausfuehrt.
- Bell-Button im DashboardView-Header zwischen DailyReward-Badge und Settings-Cog.
- Popup `NotificationCenterPopup.axaml` (rechts oben, MaxWidth 380, MaxHeight 540).
- Login-Flow (MainViewModel.Init.cs) angepasst: OfflineEarnings/CombinedWelcome bleiben Modal,
  DailyReward landet ab dem 2. Modal-Konflikt in der Bell statt verzoegert ins Modal.

**Risk/Reward Strategy-Anzeige (Task 1.3)**
- `StatisticsData.MiniGamePerformance` (Dictionary<MiniGameType, MiniGameStats>).
- `MiniGameStats` mit Sliding-Window Last-20-Plays (`RollingResults`), TotalPlays,
  PerfectRatings, Misses, LastPlayedAt.
- `IGameStateService.RecordMiniGameResult(rating, miniGameType)` Ueberladung fuettert die
  Stats; `GetMiniGameSuccessRate(type)` gibt -1 wenn weniger als 5 Plays.
- `OrderViewModel.UpdateStrategyStats(order)` berechnet pro Strategie Trefferquote +
  Erwartungswert (EV) mit linearer Reward-Skala (Safe=0,75x, Standard=1,0x, Risk=2,0x).
- `IsRiskWorseThanStandard` setzt Class `RiskWarning` auf Risk-Button → rote Border via Style.

**Auto-Play differenzieren (Task 1.4)**
- `AutomationSettings.AutoAcceptOnlyStandard` (Default true) — AutoAccept ueberspringt
  Live/VIP. `GameLoopService.Automation.cs::TryAutoAcceptOrder()` filtert.
- `AutomationSettings.AutoCompleteSkipLiveOrders` (Default true) — MiniGame-Auto-Complete
  in `BaseMiniGameViewModel.UpdateAutoCompleteStatus()` ist bei Live/VIP-Order ausgeblendet.
- 2 Sub-Toggles im Dashboard `AutomationPanel.axaml` (nur sichtbar wenn AutoAccept gelockt).

**GameLoopService Constructor-Validation (Task 1.6)**
- Pflicht-Services nicht-nullable: `IIncomeCalculatorService`, `IPrestigeService`,
  `IWorkerService`, `IResearchService` (zusaetzlich zu `IGameStateService`/`ISaveGameService`).
- `ArgumentNullException.ThrowIfNull(...)` im Konstruktor — DI-Fehlkonfiguration crasht laut.
- Optionale Plugin-Services (Guild/BattlePass/Vip/etc.) bleiben nullable.

**Reduce-Motion-Profil (Task 1.8)**
- `MainViewModel.ReduceMotion` (computed: `Settings.GraphicsQuality == Low`).
- DashboardView-Animations-Styles per `:not(.NoMotion)` Selektor: GoldenBadgeShimmer,
  TutorialHintPulse, BoostPulse, GoldenScrewBadgeShimmer ausgeschaltet wenn ReduceMotion.
- Element-Bindung `Classes.NoMotion="{Binding ReduceMotion}"` schaltet das ein.

**Touch-Targets (Task 1.5)**
- 9 MiniGame-Tutorial-Info-Buttons von MinHeight=32 auf 44 angehoben.
- ParallelOrders-Resume-Button von 40 auf 44.

**RESX-Luecken (Task 1.7)**
- 18 Keys, die zuvor `?? "Fallback"` im Code waren, sind jetzt in allen 6 RESX-Sprachen
  vollstaendig: Worker, TabWorkshop, TabMissions, TabGuild, InitError, DeliveryCollected,
  SoftCapIncome, ManagerUnlocked, ManagerUnlockedFormat, AscensionFailed, ResearchTime,
  StartReputationFormat, OrderStrategyRiskConfirmDesc, AbandonChallengeDesc,
  ParallelOrderLimitDesc, GuildWarTrainingRound, AtLevelShort.

### GuildService-Aufteilung (Stand 01.05.2026)
GuildInviteService aus GuildService (1571 Zeilen) extrahiert. Neuer Stand: GuildService ~1230 Zeilen
(Kern-CRUD, Wochenziele, Mitglieder-Verwaltung, Rollen, MemberCount-Synchronisation), GuildInviteService
~310 Zeilen (Invite-Codes, "Verfuegbare Spieler"-Browser, Einladungs-Inbox). FirebaseKeyValidator
in eigene Helper-Klasse extrahiert (`Helpers/FirebaseKeyValidator.cs`), beide Services nutzen sie.
Beitritts-Operationen (JoinByInviteCode, AcceptInvite) delegieren an `IGuildService.JoinGuildAsync`
um den globalen Beitritts-Lock und die Integritaets-Checks zentral zu halten — keine doppelte
Sperre, keine Race-Bedingung. RegisterAsAvailable/UnregisterAvailable bewusst privat im
GuildService dupliziert (`RegisterAsAvailableInternalAsync`/`UnregisterAvailableInternalAsync`,
je 12 Zeilen) um Circular DI mit GuildInviteService zu vermeiden — die Methoden sind reine
Firebase-Set/Delete-Calls ohne eigene Logik. IGuildFacade um `Invite`-Property erweitert,
GuildViewModel nutzt `_facade.Invite.X` fuer 8 Aufrufstellen statt zuvor `_facade.Guild.X`.
Keine Membership-Cleanup-Extraktion: Die Cleanup/Duplicate/Stale/MemberCount-Methoden sind
alle private Helfer ohne oeffentliche API — eine Extraktion wuerde shared State zerreissen
oder Circular Dependencies erzeugen.

### Async-void Handler (Stand 01.05.2026)
14 von 15 `async void`-Event-Handlern auf `AsyncExtensions.RunHandlerSafely` umgestellt
(Helpers/AsyncExtensions.cs). Eliminierte ~280 Zeilen redundantes try/catch-Logging.
NICHT konvertiert: `LuckySpinViewModel.OnSpinTick` (catch-Block stoppt Timer + unsubscribt
Event + setzt IsSpinning=false — echte Cleanup-Logik, keine reine Logging-Hülle).

Konvertiert: BottomSheetBehavior.OnIsOpenChanged, WorkshopView.OnUpgradeEffect,
PaintingGameView.OnComboIncreased + OnGameCompleted, RoofTilingGameView.OnVmPropertyChanged
+ OnGameCompleted, ForgeGameView.OnGameStarted + OnGameCompleted, SawingGameView.OnGameStarted
+ OnGameCompleted, sowie OnGameCompleted in Blueprint/DesignPuzzle/PipePuzzle/Invent/
Inspection/Wiring (zusätzlich MainViewModel.OnShowPrestigeDialog war bereits konvertiert).

### Lokalisierung (Stand 01.05.2026)
Alle deutschen Fallback-Strings in `?? "..."` Konstrukten wurden auf englische Strings umgestellt.
EN ist die Base-Sprache. Deutsche Fallbacks brechen das Fallback-System für alle nicht-deutschen Nutzer.

**Fehlende Keys in AppStrings.en.resx** (Fallbacks auf sinnvolles Englisch gesetzt):
- `Worker` → "Worker" (GoalService)
- `TabWorkshop` / `TabMissions` / `TabGuild` → "Workshop" / "Missions" / "Guild" (MainViewModel)
- `InitError` → "An error occurred while loading. Please restart the app."
- `DeliveryCollected` → "Delivery collected"
- `SoftCapIncome` → "Income"
- `ManagerUnlocked` → "New Foreman!"
- `ManagerUnlockedFormat` → "{0} is now available!"
- `AscensionFailed` → "Ascension failed. Requirements not met."
- `ResearchTime` → "Research time"
- `StartReputationFormat` → "Starting reputation: {0}"
- `PremiumIncomeComparison` / `PremiumIncomeCompare` → vorhanden, korrekt
- `OrderStrategyRiskConfirmDesc` / `OrderStrategyRiskConfirmMessage` → "Miss = no reward + reputation loss. Really risk it?"
- `AbandonChallengeDesc` → "You will receive 50% of the base prestige points (without challenge bonus). All challenges will be deactivated."
- `ParallelOrderLimitDesc` → "This workshop already has a running order or the global parallel limit has been reached."
- `GuildWarTrainingRound` / `GuildWarByeWeekTraining` → "Training round! Complete orders and mini-games to earn war points for the next round."
- `AtLevelShort` → "From Level {0}"

**IGameStateService.SwapToParallelOrder** fehlte im Interface (bereits vorhandener Fehler, behoben).

## Icon-System (Bitmap-Icons, AI + Programmatisch)

Kein Material.Icons.Avalonia. Alle 224 Icons sind WebP-Bitmaps (128x128) in `Assets/visuals/icons/`.

- **Hybrid-Generierung**: ~200 Objekt-Icons AI-generiert (ComfyUI DreamShaper XL, Cartoon-Stil), ~22 abstrakte UI-Icons programmatisch (PIL, geometrische Formen)
- **GameIconKind.cs**: Enum mit 224 Werten in Kategorien (Navigation, Status, Stars, Combat, Economy, Workers, Tools, Buildings, etc.)
- **GameIcon** (`Icons/GameIcon.cs`): Custom Control (erbt von `TemplatedControl`, hat `Foreground`). Render: Bitmap-Alpha als OpacityMask, Foreground als Füllfarbe
- **GameIconRenderer** (`Icons/GameIconRenderer.cs`): SkiaSharp-Renderer für SKCanvas. Bitmap + SKColorFilter.CreateBlendMode(color, SrcIn) für Tinting
- **Preloading**: `GameIcon.PreloadAllAsync()` in Loading-Pipeline (Step 1 parallel mit Shader+ViewModel+Purchases)
- **Pfad-Konvertierung**: PascalCase → snake_case → `icons/{name}.webp` (z.B. ArrowDown → icons/arrow_down.webp)
- **Tinting**: AXAML nutzt `Foreground="{StaticResource CraftGoldBrush}"`, SkiaSharp nutzt `paint.Color`
- **StringToGameIconKindConverter**: Konvertiert String-Iconnamen zu Enum-Werten in XAML-Bindings
- **Generierungs-Script**: `F:\AI\ComfyUI_workflows\handwerkerimperium\generate_icons_test.py`

## Haupt-Features

- **10 Workshop-Typen** (Schreiner, Klempner, Elektriker, Maler, Dachdecker, Bauunternehmer, Architekt, Generalunternehmer, Meisterschmiede, Innovationslabor)
- **10 Mini-Games** (Sawing, Pipe Puzzle, Wiring, Painting, RoofTiling, Blueprint, DesignPuzzle, Inspection, ForgeGame, InventGame)
- **Worker-System** mit 10 Tiers (F/E/D/C/B/A/S/SS/SSS/Legendary), Avatare, Training, Ausrüstung
- **Goldschrauben-Economy** (Premium-Währung für Boosts/Unlock)
- **Research Tree** (45 Upgrades in 3 Branches: Tools, Management, Marketing)
- **7 Gebäude** (Canteen, Storage, Office, Showroom, TrainingCenter, VehicleFleet, WorkshopExtension)
- **Daily Challenges** (3/Tag, 11 Typen inkl. TrainWorker/CompleteCrafting/AchievePerfectStreak/ReachWorkshopLevel) + **Weekly Missions** (5/Woche, 11 Typen, 50 Goldschrauben Komplett-Bonus)
- **Daily Login Rewards** (30-Tage-Zyklus) + **Streak-Rettung** (3 Goldschrauben)
- **Achievements** (110 Erfolge in 17 Kategorien inkl. Ascension/Rebirth) + **Milestone-Celebrations** (Spieler-Level + Workshop-Level)
- **Prestige-System** (7 Stufen Bronze-Legende, verschärfte Bewahrung, tier-skalierender Soft-Cap 4x→20x, Tier-skalierendes Startgeld, 3 wiederholbare Shop-Items, permanenter Prestige-Pass, Diminishing Returns auf Multiplikator, 6 Challenge-Modifikatoren, tier-multiplizierte Bonus-PP, Meilensteine, Speedrun-Tracking)
- **Events** (8 zufällige + saisonaler Multiplikator, Intervall skaliert mit Prestige)
- **Auftragstypen** (Standard/Large 1.8x/Weekly 3.0x/Cooperation 2.5x) + **Stammkunden** (bis 1.5x Bonus)
- **Bulk Buy** (x1/x10/x100/Max) + **Hold-to-Upgrade** (schnelles Hochleveln)
- **Nächstes-Ziel-System** (GoalService: dynamischer Gold-Banner, 10 Prioritäten: Anfänger→Meilenstein→Prestige→Workshop-Unlock→Gebäude→Worker→Rebirth→Ascension→AllMax→NextStar→Stretch)
- **Offline-Earnings** (80% erste 2h, 35% bis 4h, 15% bis 8h, 5% danach — ~2.8h Äquivalent für 8h Nacht)
- **Feierabend-Rush** (2h 2x-Boost, 1x täglich gratis, danach 10 Goldschrauben)
- **Meisterwerkzeuge** (12 Artefakte, 5 Seltenheiten, passive Einkommens-Boni)
- **Lieferant-System** (Variable Rewards alle 2-5 Min: Geld, Schrauben, XP, Mood, Speed)
- **Prestige-Shop** (23 Items in 4 Kategorien, 1 wiederholbar) + **Prestige-Pass** (2,99 EUR IAP, +50% Prestige-Punkte, permanent nach Kauf). Shop-Effekte: OfflineHoursBonus (GameState.MaxOfflineHours), CraftingSpeedBonus (CraftingService.StartCrafting), ExtraQuickJobLimit (QuickJobService.GetMaxQuickJobsPerDay), UpgradeDiscount (GameLoopService-Cache), Income/Rush/Delivery (GameLoopService-Cache), CostReduction/MoodDecay/XP (PrestigeService-Getter), GoldenScrews (GameStateService.AddGoldenScrews), StartMoney/StartWorkerTier (PrestigeService.ResetProgress)
- **Story-System** (35 Kapitel von NPC "Meister Hans" mit SkiaSharp-Portrait, dynamischer Kapitelzähler)
- **Kontextuelles Tutorial** (22 Tooltip-Bubbles + Welcome-Dialog, ContextualHintService, SeenHints-Tracking, inkl. Ascension/Rebirth/FirstStar/GoldenScrews/AcceptOrder-Hints, Hint-Verkettung: Welcome→FirstWorkshop→AcceptOrder)
- **In-App Review** (Level 20/50/100, erstes Prestige, 50 Aufträge)
- **Benachrichtigungen** (4 Typen, AlarmManager + BroadcastReceiver, BootReceiver, 6 Sprachen)
- **Google Play Games** (Leaderboards, kein Cloud-Save im NuGet v121.0.0.2)
- **Audio + Haptik** (15 Sounds via SoundPool, 7 Vibrations-Muster, Hintergrundmusik)
- **Vorarbeiter-System** (14 Manager, Lv.1-5, Workshop-Boni)
- **Turniere** (Wöchentlich, 9 simulierte Gegner, 3x/Tag gratis, alle 10 MiniGame-Typen)
- **Battle Pass** (50 Tiers, Free/Premium Track, 42-Tage-Saisons, Premium: 10 GS/3 Tiers + 50 GS Capstone, BattlePassRewardType: Standard/SpeedBoost)
- **Saisonale Events** (4/Jahr mit Saisonwährung und Event-Shop, 6 Items/Saison: 4 Basis + 2 saison-einzigartige)
- **Gilden/Innungen** (Firebase Realtime Database, Wochenziele, 18 Forschungen mit Timer+Auto-Completion, Einladungs-Inbox mit Accept/Decline)
- **Crafting-System** (20 Rezepte in 3 Tiers, Inventar + Verkauf, skalierende Preise)
- **Auto-Produktion** (alle 10 Workshops produzieren passiv Tier-1 Items ab Lv50, Rate: 180s/Worker Standard, 120s InnovationLab, 60s MasterSmith)
- **Lieferaufträge** (MaterialOrder: Kein MiniGame, Items liefern für sofortige Belohnung, 1.8x Reward, 4h Deadline, max 3/Tag)
- **Automatisierung** (Auto-Collect Lv15+, Auto-Accept Lv25+, Auto-Assign Lv20+, Auto-ClaimDaily Premium) — eigenes Dashboard-Panel (nicht in Settings)
- **Welcome Back Angebote** + **Glücksrad** (täglich gratis, Festpreis 5 GS pro Extra-Spin)
- **Ausrüstungs-System** (4 Typen x 4 Seltenheiten für Arbeiter, Equip/Unequip im Worker-Profil, Inventar-Browser)
- **Grafik-Einstellungen** (Low/Medium/High, GraphicsQuality in GameState, steuert Wetter-Effekte etc.)
- **MiniGame-Direktstart** (Auto-Start nach Tutorial-Check + 3-2-1-Countdown, kein Start-Button)
- **MiniGame Auto-Complete** (ab 30 Perfect-Ratings "Auto-Ergebnis" mit Perfect-Rating als Mastery-Belohnung, Premium ab 15, PerfectRatingCounts in GameState)
- **Gilden-Browser** (offene Gilden suchen+beitreten ohne Einladung, Firebase REST-Abfrage, Browse-UI in GuildView)
- **Soft-Cap-Transparenz** (IsSoftCapActive + SoftCapReductionPercent im GameState, UI-Indikator im Dashboard, tier-skalierende Schwelle: None=4x, Bronze=6x, Silver=8x, Gold=10x, Platin=12x, Diamant=14x, Meister=16x, Legende=20x)
- **Workshop Rebirth** (0-5 Sterne pro Workshop, permanent über Prestige+Ascension, Einkommens-Bonus +15-150%, Upgrade-Rabatt 5-25%, Extra-Worker +1-3)
- **Challenge-Abbruch** (Aufgeben-Button bei aktiven Prestige-Challenges, gibt 50% Basis-PP ohne Challenge-Bonus, Challenges werden deaktiviert)
- **Prestige-Freischaltung** (Nach erstem Prestige alle Tabs, Features und Automatisierungen permanent freigeschaltet — Spieler verliert nie Zugang zu Gilde/Forschung/etc.)
- **Ascension-System** (Meta-Prestige nach 3x Legende, 6 permanente Perks je MaxLevel 3 (61 AP gesamt), +2 AP/Ascension Skalierung, AscensionViewModel + Route "ascension" als Imperium-Sub-View)
- **Firebase-Telemetrie** (v2.0.33, REST via FirebaseService): IAnalyticsService mit Queue-Batching (30s-Flush, 500-Event-Cap), IRemoteConfigService (balancing/features-Overrides via `remote_config/`-Pfad mit Preferences-Cache), Events in `analytics_events/{YYYY-MM-DD}/$pushId` mit playerId-Validierung. Event-Katalog: `Models/AnalyticsEvents.cs` (38 Event-Typen), `Models/RemoteConfigKeys.cs` (12 Keys).
- **DSGVO-Consent** (v2.0.33): Opt-In-Dialog beim allerersten Start (AnalyticsConsentShown + AnalyticsEnabled in SettingsData). User kann in Settings → Datenschutz jederzeit aendern. Bei OFF: Keine Events, Queue wird geleert.
- **Firebase-Cloud-Save** (v2.0.33): Plattformuebergreifend via REST (ersetzt den nicht-funktionalen Play-Games-v2-Snapshots-Stub). Pfad `cloud_saves/{playerId}/{metadata|data}`. Auto-Upload beim lokalen Save (Rate-Limit 2min), Konflikt-Dialog beim Start wenn Cloud neuer (Toleranz 5s Clock-Skew). Settings: Cloud-Save-Toggle jetzt sichtbar sobald Firebase ODER Play Games verfuegbar (CanUseCloudSave = IsCloudSaveOnline || IsPlayGamesSignedIn).

### Prestige-System (Details)

**PP-Formel**: `floor(sqrt(CurrentRunMoney / 100_000))` - nur Geld aus dem aktuellen Durchlauf zählt (nicht kumulativ). `CurrentRunMoney` wird bei jedem Prestige auf 0 zurückgesetzt.

**Multiplikator mit Diminishing Returns**: Bonus pro Prestige sinkt mit Anzahl bereits durchgeführter Prestiges desselben Tiers. Formel: `baseBonus * 1/(1 + 0.1 * tierCount)`. Erster Prestige voller Bonus, 10. nur noch 50%. Cap bei 20x.

**Tier-Multiplikator-Boni (Basis)**: Bronze +20%, Silver +35%, Gold +50%, Platin +100%, Diamant +200%, Meister +400%, Legende +800%. (Silver auf +35% angehoben am 18.04.2026, Weber-Gesetz — siehe Review-Fixes-Eintrag)

**Verschärfte Erhaltung (eine Stufe höher als original)**:

| Tier | Erhaltung |
|------|-----------|
| Bronze/Silver | Nur Basis (Achievements, Premium, Settings, PrestigeData, Tutorial) |
| Gold+ | + Research bleibt |
| Platin+ | + Prestige-Shop Items bleiben |
| Diamant+ | + MasterTools bleiben |
| Meister+ | + Gebäude (Level→1) + Equipment |
| Legende | + Manager (Level→1) + beste Worker |

### Prestige-Herausforderungen (Run-Modifikatoren)

Optionale Erschwerungen, wählbar VOR dem Prestige. Max 3 gleichzeitig. PP-Boni stacken additiv.
Daten in `PrestigeData.ActiveChallenges`. Constraints zentral via `IChallengeConstraintService`.

| Challenge | Effekt | PP-Bonus |
|-----------|--------|----------|
| Spartaner | Max 3 Worker | +45% |
| OhneForschung | Keine Forschung möglich | +30% |
| Inflationszeit | Doppelte Upgrade-Kosten | +25% |
| SoloMeister | Nur 1 Workshop | +50% |
| Sprint | Kein Offline-Einkommen | +35% |
| KeinNetz | Keine Lieferanten | +20% |

**Constraint-Enforcement**: `ChallengeConstraintService` (Singleton) → Consumer-Services fragen `IChallengeConstraintService` ab.
**SoloMeister + QuickStart**: Inkompatibel (Toggle wird abgelehnt).
**Enum**: `Models/Enums/PrestigeChallengeType.cs`

### Bonus-PP-Quellen (flat, NACH Tier-Multi)

Kleine Boni für Spielleistung im Run, addiert NACH dem Tier-Multiplikator (nicht multipliziert).

| Bedingung | Bonus-PP | Cap |
|-----------|----------|-----|
| Je 10 Perfect Ratings | +1 PP | max +5 |
| Volle Research-Branch | +2 PP | max +6 |
| Alle 7 Gebäude Lv5 | +1 PP | — |
| Pro Level über Tier-Min | +0.05 PP | max +5 |

Konstanten in `GameBalanceConstants.cs`. Berechnung in `PrestigeService.CalculateBonusPrestigePoints()`.

### Prestige-Meilensteine (permanent)

GS-Belohnungen für kumulative Prestige-Zähler. Werden NICHT bei Ascension zurückgesetzt.
Daten in `PrestigeData.ClaimedMilestones`. Event: `IPrestigeService.MilestoneReached`.

| Prestiges | ID | GS |
|-----------|-----|-----|
| 1 | pm_first | 10 |
| 5 | pm_5 | 20 |
| 10 | pm_10 | 35 |
| 25 | pm_25 | 50 |
| 50 | pm_50 | 75 |
| 100 | pm_100 | 100 |

### Speedrun-Tracking

- `PrestigeData.RunStartTime` (UTC, gesetzt bei jedem Prestige-Reset)
- `PrestigeData.BestRunTimes` (Dictionary pro Tier, max 7 Einträge)
- `PrestigeHistoryEntry.RunDurationTicks` + `.Challenges` + `.BonusPrestigePoints`
- Kein Balance-Impact, rein motivational

### Prestige-Shop-Erweiterung (27 Items, davon 3 wiederholbar)

4 neue Items (2 wiederholbar, 1 Tier-locked):

| ID | Typ | Kosten | Effekt | Tier-Lock |
|----|-----|--------|--------|-----------|
| pp_order_reward_rep | Wiederholbar | 20 (×2) | +5% Auftragsbelohnungen | — |
| pp_delivery_interval_rep | Wiederholbar | 25 (×2) | +10% schnellerer Lieferant | — |
| pp_research_speed_tier | Einmalig | 45 PP | -25% Forschungszeit | Diamant+ |

Tier-Lock: Items mit `RequiredTier` nur sichtbar wenn Tier erreicht ODER bereits gekauft.
Effekt-Verdrahtung: `OrderRewardBonus` → `GameStateService.GetPrestigeShopOrderRewardBonus()`, `ResearchSpeedBonus` → `ResearchService.CalculateEffectiveDuration()`.

### Workshop Rebirth (Late-Game)

Pro Workshop können 0-5 Rebirth-Sterne verdient werden. Sterne sind permanent (überleben Prestige + Ascension).
Daten in `GameState.WorkshopStars` (Dictionary<string, int>), Runtime-Properties auf `Workshop` (JsonIgnore).

| Sterne | Einkommens-Bonus | Upgrade-Rabatt | Extra Worker | GS-Kosten |
|--------|-----------------|----------------|-------------|-----------|
| 1 | +15% | -5% | +1 | 100 |
| 2 | +35% | -10% | +1 | 250 |
| 3 | +60% | -15% | +2 | 500 |
| 4 | +100% | -20% | +2 | 500 |
| 5 | +150% | -25% | +3 | 1000 |

### Late-Game Achievements (Phase 4)

17 Achievement-Kategorien. 11 neue Achievements für Ascension/Rebirth/Late-Game:

| ID | Name | Bedingung | Belohnung |
|----|------|-----------|-----------|
| asc_first | Erster Aufstieg | 1 Ascension | 500k + 5000 XP + 100 GS |
| asc_5 | Meister-Aufsteiger | 5 Ascensions | 5M + 12500 XP + 250 GS |
| asc_10 | Transzendenz | 10 Ascensions | 50M + 25000 XP + 500 GS |
| asc_perk_first | Perk-Enthusiast | 1 Perk kaufen | 100k + 2500 XP + 50 GS |
| asc_perks_max | Voll ausgebaut | Alle 6 Perks Max | 100M + 50000 XP + 1000 GS |
| rebirth_first | Erster Stern | 1 Rebirth | 1M + 5000 XP + 100 GS |
| rebirth_stars_10 | Sternensammler | 10 Sterne gesamt | 10M + 15000 XP + 300 GS |
| rebirth_ws_5stars | Vollendung | 1 WS auf 5 Sterne | 25M + 20000 XP + 500 GS |
| rebirth_all_ws | Galaxie | Alle 8 WS 1+ Stern | 50M + 25000 XP + 750 GS |
| all_ws_level1000 | Auf dem Gipfel | Alle 8 WS Level 1000 | 100M + 50000 XP + 1000 GS |

### Contextual Hints (Late-Game, Phase 4)

3 neue Dialog-Hints für Late-Game-Features:
- **ascension_available**: Erscheint nach 3. Legende-Prestige (OnPrestigeCompleted)
- **rebirth_ready**: Erscheint wenn ein Workshop Level 1000 erreicht (OnWorkshopUpgraded)
- **first_star**: Erscheint nach erstem Rebirth (OnRebirthCompleted)

### SaveGame-Versionen

| Version | Beschreibung |
|---------|-------------|
| 1 | Legacy (Altes Worker-System) |
| 2 | Neues Worker-System, Buildings, Research, Events, Prestige, Reputation |
| 3 | Workshop Rebirth Stars (WorkshopStars Dictionary) |
| 3+ | Worker: ResumeTrainingType (Auto-Resume nach Ruhe), KeptWorkers indiziert (Top 3 bei Legende) |
| 4 | Settings, Statistics, Tutorial in Sub-Objekte extrahiert (SettingsData, StatisticsData, TutorialState) |
| 5 | Boosts, DailyProgress, Cosmetics in Sub-Objekte extrahiert (BoostData, DailyProgressData, CosmeticData). Legacy-Weiterleitungs-Properties auf GameState für volle Backward-Kompatibilität |

### Worker-System Details

- **EffectiveEfficiency-Formel**: `BaseEff * XpBonus(+3%/Lv) * MoodFactor * FatigueFactor * (1+SpecBonus+EquipBonus) * PersonalityMult * TalentBonus(1★=1.0x bis 5★=1.20x)`
- **XP-Bonus**: +3% pro ExperienceLevel auf EffectiveEfficiency (Training lohnt sich immer, auch am Tier-Maximum)
- **Auto-Resume Training**: Worker setzt Training automatisch fort nach Fatigue-bedingter Ruhe (ResumeTrainingType Property)
- **Offline-Kündigung**: Worker kündigen auch offline nach 24h bei Mood<20 (konsistent mit Online-Verhalten)
- **Legende-Prestige**: Top 3 Worker pro Workshop gesichert (statt nur 1). Keys: "Type", "Type_1", "Type_2" (backward-compatible)
- **Markt-Preisberechnung**: Dreifacher Floor: qualityPrice, incomeFloor (3min Netto-Einkommen), levelFloor (Basis-Anstellungskosten * Level)
- **Personality-Icons**: GetIcon() gibt GameIconKind-Namen zurück (ShieldHalfFull, StarFourPoints, EmoticonHappy, RocketLaunch, WeatherSunset, Wrench)

### Neuer-Spieler-Einstieg

- **Startgeld**: 1.000 EUR (statt 250)
- **Start-Werkstatt**: Schreinerei mit 2 Arbeitern (statt 1), `AssignedWorkshop` explizit auf `Carpenter` gesetzt
- Workshop-Karten zeigen werkstatt-spezifische Icons (GameIconRenderer) auf Upgrade-Button und dimmed auf Locked-Karten
- **Daily Reward am Tag 1**: Wird still eingesammelt (kein Dialog), ab Tag 2 normaler Dialog
- **Dialog-Reihenfolge**: Story Kapitel 1 (Meister Hans) → FirstWorkshop-Hint → AcceptOrder-Hint (Welcome-Hint entfällt, da Story Ch.1 die Begrüßung abdeckt)
- **Error-Handling**: InitializeAsync loggt Exceptions und zeigt Fehlerdialog statt still zu schlucken

## Premium & Ads

### Premium-Modell
- **Preis**: 4,99 EUR (Lifetime)
- **Vorteile**: +50% Einkommen, +100% Goldschrauben aus Mini-Games, keine Werbung
- **Shop Live-Vergleich**: `PremiumIncomeComparison` Property im ShopVM zeigt Nicht-Premium-Spielern "Dein Einkommen: X/s -> Mit Premium: Y/s"
- **Starter-Offer**: Einmaliges Angebot ab Level 10, 24h-Countdown, Properties `StarterOfferShown`/`StarterOfferTimestamp` in GameState

### Rewarded (13 Placements, BAL-AD Rebalancing 04.04.2026)
1. `golden_screws` - 8 GS (eigener 4h-Cooldown, BAL-AD-1: von 5 auf 8, getrennt von Shop-Cooldown)
2. `shop_reward` - Cash/Boost-Ads im Shop (3h-Cooldown, BAL-AD-1: getrennt von GS)
3. `score_double` - Mini-Game Score verdoppeln
4. `market_refresh` - Arbeitermarkt-Pool neu würfeln
5. `workshop_speedup` - 2h Brutto-Ertrag eines WS (BAL-AD-3: von 30min auf 2h erhöht)
6. `workshop_unlock` - 30% Rabatt auf Workshop-Kauf
7. `worker_hire_bonus` - +1 Worker-Slot persistent (max 3/WS)
8. `research_speedup` - Forschungszeit -50% (BAL-AD-4: nur ab 30min Restzeit) + GS-Sofortfertigstellung
9. `daily_challenge_retry` - Challenge-Fortschritt zurücksetzen
10. `achievement_boost` - Achievement Progress +20% (BAL-AD-7: nur bei TargetValue>5)
11. `offline_double` - 2x Offline-Earnings
12. `rush_boost` - 1h Rush per Video (BAL-AD-5: NEU, Alternative zu 10 GS für 2h Rush)
13. `lucky_spin` - 1x/Tag Ad-Spin im Glücksrad (BAL-AD-6: NEU, nach Gratis-Spin)
- **Zeitsprung (skip_time_1h)**: 2h Netto-Einkommen + Worker-Erholung + Forschungsbeschleunigung (BAL-AD-2)

## Architektur-Besonderheiten

### Architektur-Refactoring (05.04.2026)

**IGameStateService Interface Segregation**: Composite Interface Pattern - 3 neue Sub-Interfaces extrahiert:
- `IGameCurrencyService` (Geld, Goldschrauben, XP + zugehörige Events)
- `IGameWorkshopService` (Workshop-Operationen + Events)
- `IGameOrderService` (Auftrags-Operationen + Events)
- `IGameStateService : IGameCurrencyService, IGameWorkshopService, IGameOrderService` (100% backward-kompatibel)

**MainViewModel INavigable-Pattern**: NavigationRequested-Wiring per Schleife über `_navigableChildren[]` statt 19+ Einzelzeilen. LuckySpinVM hat eigenen Handler.

**GameState Sub-Models Phase 2**: 3 neue Sub-Objekte (V5 Migration):
- `BoostData` (SpeedBoost, XpBoost, Rush, SoftCap)
- `DailyProgressData` (DailyReward, QuickJobs, WelcomeBack, WeeklyMissions)
- `CosmeticData` (Cosmetics, Themes, Skins)
- Legacy-Properties auf GameState leiten an Sub-Objekte weiter (Backward-Kompatibilität)

**GameIntegrityService**: HMAC-SHA256-Signierung für Gilden-relevante Werte (Level, Prestige, Money, GoldenScrews, Orders). Geräte-spezifischer Schlüssel (Package-Name + Installations-GUID). GuildService prüft Signatur vor Firebase-Updates.

**Firebase Path Validation**: `IsValidFirebaseKey()` in GuildService prüft guildId/playerId auf Firebase-verbotene Zeichen an 8 Entry-Points.

### Architektur-Refactoring (03.04.2026)

**Event-Harmonisierung**: 4 ViewModels (`WorkerMarketVM`, `WorkerProfileVM`, `BuildingsVM`, `ResearchVM`) von `EventHandler<string>` auf `Action<string>` NavigationRequested umgestellt. Wrapper-Delegates in MainViewModel eliminiert. Alle VMs nutzen jetzt einheitlich `Action<string>` für Navigation-Events.

**IGameStateService Lock-Delegation**: `ExecuteWithLock(Action)` und `ExecuteWithLock<T>(Func<T>)` auf IGameStateService für zukünftige Service-Extraktion. 5 Event-Raising-Methoden (`RaiseWorkshopUpgraded`, `RaiseWorkerHired`, `RaiseOrderCompleted`, `RaiseMiniGameResultRecorded`, `RaiseMoneyChanged`) ermöglichen externen Services Events zu feuern.

**BaseMiniGameViewModel.StopGame()**: Von `protected` auf `public` geändert (MainViewModel.Navigation.cs muss es aufrufen).

**EconomyFeatureViewModel** (Extraktion): Economy-Geschäftslogik (~1.100 Zeilen) aus MainViewModel.Economy.cs in eigenständige Klasse `EconomyFeatureViewModel.cs` extrahiert. Properties bleiben auf MainViewModel (AXAML-Bindings unverändert). Economy.cs enthält nur noch ~114 Zeilen Forwarding-Stubs mit `[RelayCommand]`. EconomyFeatureVM greift über `_host`-Referenz auf MainViewModel-Properties zu. Events `FloatingTextRequested` und `CelebrationRequested` werden an MainViewModel weitergeleitet.

**MiniGameViewModels-Aggregat**: 10 einzelne MiniGame-VM-Properties (`SawingGameViewModel` etc.) durch `MiniGames`-Container ersetzt. `ActiveMiniGameViewModel` als zentraler Zugriffspunkt. `IsAnyMiniGamePlaying()` und `StopCurrentMiniGame()` nutzen ActiveMiniGameViewModel statt 10er-Switch. Subscribe/Unsubscribe per Schleife über `MiniGames.All`.

**GameState-Partitionierung**: 3 neue Sub-Objekte extrahiert (V3→V4 Migration in SaveGameService):
- `SettingsData` (8 Props: Sound, Music, Haptics, Notifications, Graphics, CloudSave, Language)
- `StatisticsData` (~18 Props: alle Tracking-Zähler)
- `TutorialState` (5 Props: Tutorial-Tracking, SeenHints)
Zugriff: `state.Settings.SoundEnabled`, `state.Statistics.TotalOrdersCompleted`, `state.Tutorial.SeenHints`

### BaseMiniGameViewModel (Refactoring 20.03.2026, Bugfixes 29.03.2026)

Alle 10 MiniGame-ViewModels erben von `BaseMiniGameViewModel` (ViewModels/MiniGames/BaseMiniGameViewModel.cs).
Eliminiert ~2.500 Zeilen Duplikation. Basis-Klasse enthält:
- 27 gemeinsame ObservableProperties (OrderId, Difficulty, IsPlaying, Stars, Tutorial, AutoComplete, IntermediateAverage, CanShowTutorialInfo etc.)
- 9 gemeinsame Commands (StartGame, Continue, Cancel, WatchAd, DismissTutorial, AutoComplete, ShowTutorialInfo)
- Countdown-Logik (3-2-1-Los, **verkürzt auf 350ms nach 50+ Spielen**), Timer-Management, Ergebnis-Anzeige mit Sterne-Animation
- SetOrderId(), ShowResultAsync(), StopGame(), CheckAndShowTutorial(), UpdateAutoCompleteStatus()
- **QuickJob-Support**: Difficulty wird vom QuickJob-Model übernommen, Auto-Complete nur bei echten Aufträgen
- **Zwischen-Ergebnis**: IntermediateAverage zeigt bisherigen Durchschnitt bei Multi-Task-Orders
- **Tutorial-Wiederholung**: ShowTutorialInfo-Command + Info-Button in allen Views
- Virtual Hooks: InitializeGame(), OnGameTimerTick(), OnPreGameStartAsync(), CalculateAndSetRewards(), GetCurrentMiniGameType()
- SawingGame/ForgeGame überschreiben GetCurrentMiniGameType() für dynamische Sub-Typen
- SawingGame: 4 Sub-Typen mit differenziertem Gameplay und **einzigartigen Tutorial-Texten** (Sawing=Standard, Planing=langsamer+kleinere Zonen, TileLaying=beschleunigend, Measuring=driftende Zielzone)
- PaintingGame überschreibt CalculateAndSetRewards() für Combo-Multiplikator (Staffel: +0.25x pro 5 fehlerfreie Treffer)
- Blueprint/InventGame nutzen OnPreGameStartAsync() für Memorisierungsphase
- **PipePuzzle optimalMoves** basiert auf drehbarer Pfad-Länge (nicht Grid-Größe)
- **Auto-Complete Schwellen**: Differenziert - Timing-Spiele 30/15, Puzzle/Memory-Spiele 20/10 Perfects

### GameBalanceConstants (20.03.2026)

`Models/GameBalanceConstants.cs` — Zentrale Balancing-Konstanten statt Magic Numbers. Enthält:
- Workshop-Einkommen (IncomeBaseMultiplier=1.02, MilestoneMultipliers-Array mit Lv650, Upgrade-Kosten mit abgeflachtem Exponent ab Lv500)
- Workshop-Rebirth (Einkommensboni, Rabatte, Extra-Worker als Arrays)
- Upgrade-Kosten: UpgradeCostExponent=1.07 bis Lv500, UpgradeCostReducedExponent=1.06 ab Lv500
- Worker-Stimmung/Müdigkeit (Mood-Thresholds, Fatigue-Raten, XP-Werte, Level-Fit-Parameter)
- Building-Boni (Kosten-Exponenten, Kantine/Lager/Fuhrpark-Boni als Arrays)
- WorkshopFormulas.cs und Workshop.cs referenzieren diese Konstanten

### Workshop-Farben (zentrale Quelle)

`WorkshopTypeExtensions.GetColorHex()` in `Models/Enums/WorkshopType.cs` ist die einzige Quelle der Wahrheit für Workshop-Farben. Alle Consumer leiten davon ab:
- `WorkshopCardRenderer.GetWorkshopColor()` — SKColor (SkiaSharp), gecacht per `BuildColorCache()`
- `WorkshopSceneRenderer` — delegiert an `WorkshopCardRenderer.GetWorkshopColor()`
- `WorkshopColorConverter` — Avalonia Color, gecacht per `BuildColorCache()`
- `WorkshopGameCardRenderer` — nutzt `WorkshopCardRenderer.GetWorkshopColor()`

### WorkshopGameCardRenderer (lokalisierte Strings)

Statische SkiaSharp-Renderer ohne DI-Zugang nutzen `UpdateLocalizedStrings()` für UI-Texte.
Aufruf bei Init (`MainViewModel.Economy.cs`) und Sprachwechsel (`MainViewModel.OnLanguageChanged()`).
RESX-Keys: `TapToUnlock`, `AtLevelShort`.

### Dispose / Memory Leak Prevention

`App.DisposeServices()` gibt alle IDisposable-Singletons frei (IGameLoopService, GameJuiceEngine, MainViewModel, IFirebaseService).
- **Desktop**: `desktop.ShutdownRequested += (_, _) => DisposeServices();`
- **Android**: `MainActivity.OnDestroy()` ruft `App.DisposeServices()` als ERSTE Zeile auf (vor AdMob-Dispose)
- Pattern identisch mit BomberBlast

### MainViewModel Partial-Class-Split

MainViewModel ist in 6 partielle Dateien aufgeteilt. Architektur-Refactoring (19.03.2026):

**ActivePage Enum-Navigation**: 35+ einzelne `IsXxxActive` Bool-Properties ersetzt durch eine einzige `ActivePage`-Enum-Property (`Models/Enums/ActivePage.cs`). Die Bool-Properties existieren weiterhin als berechnete Properties (XAML-kompatibel), aber die Source-of-Truth ist `ActivePage`. `DeactivateAllTabs()` (39 Zeilen) und `NotifyTabBarVisibility()` eliminiert. `IsTabBarVisible` basiert auf `HashSet<ActivePage>` (5 Haupt-Tabs). Nur 2-3 PropertyChanged-Notifications pro Seitenwechsel statt 36+. Worker-Profile und LuckySpin sind Overlay-States (eigene `[ObservableProperty]` Bools, ActivePage bleibt erhalten).

| Datei | Inhalt |
|-------|--------|
| `MainViewModel.cs` | Felder, Constructor, Properties, ActivePage-Enum + berechnete IsXxxActive, Event-Handler, GameTick, Dispose |
| `MainViewModel.Navigation.cs` | Tab-Auswahl via `ActivePage = ...`, HandleBackPressed (Switch auf ActivePage), Child-Navigation-Routing |
| `MainViewModel.Dialogs.cs` | Weiterleitungsmethoden an DialogVM, Prestige-Durchführungslogik |
| `MainViewModel.Economy.cs` | Workshop-Kauf/Upgrade, Aufträge, Rush, Lieferant, BulkBuy, Hold-to-Upgrade |
| `MainViewModel.Missions.cs` | Nur noch LuckySpin-Overlay-Steuerung (ShowLuckySpin/HideLuckySpinOverlay). Gesamte Logik extrahiert nach MissionsFeatureViewModel |
| `MainViewModel.Init.cs` | InitializeAsync, Cloud-Save, Offline-Earnings, Daily Reward |

### MissionsFeatureViewModel (extrahiert 19.03.2026)

`ViewModels/MissionsFeatureViewModel.cs` — Eigenständiges ViewModel für den Missionen-Bereich. Als Singleton in DI registriert. Zugriff via `MainViewModel.MissionsVM`.

**Enthält**: Daily Challenges, Weekly Missions, Quick Jobs, Lucky Spin, Streak-Rettung, Welcome-Back-Angebote, Meisterwerkzeuge-Info. Alle zugehoerigen Commands und ObservableProperties.

**Services** (per Constructor Injection): IWeeklyMissionService, ILuckySpinService, IQuickJobService, IDailyChallengeService, IGameStateService, IAudioService, ILocalizationService, IDialogService, IRewardedAdService, IWelcomeBackService, IAdService, IContextualHintService, LuckySpinViewModel.

**Events** (Kommunikation zurück zu MainViewModel):
- `FloatingTextRequested` → MainViewModel.FloatingTextRequested
- `CelebrationRequested` → MainViewModel.CelebrationRequested
- `NavigateToMiniGameRequested` → MainViewModel setzt QuickJob-State + NavigateToMiniGame()
- `CheckDeferredDialogsRequested` → MainViewModel.CheckDeferredDialogs()
- `StreakRescued` → MainViewModel aktualisiert LoginStreak/ShowStreakBadge Properties

**GameTick-Integration**: `UpdateQuickJobTimer()` (jede Sekunde), `UpdatePeriodicState()` (alle 10 Ticks für LuckySpin + WelcomeBack-Timer).

**AXAML-Bindings**: DailyChallengeSection und WeeklyMissionSection nutzen `x:DataType="vm:MissionsFeatureViewModel"` und bekommen `DataContext="{Binding MissionsVM}"`. WelcomeBackOfferDialog ebenso. DashboardView/MissionenView/ImperiumView nutzen `MissionsVM.PropertyName` Bindings. DailyRewardDialog nutzt `MissionsVM.CanRescueStreak` etc.

### IncomeCalculatorService (extrahiert 19.03.2026)

`Services/IncomeCalculatorService.cs` (IIncomeCalculatorService) — Zentrale Einkommens- und Kostenberechnung.
Eliminiert die Code-Duplikation zwischen GameLoopService (pro Tick) und OfflineProgressService (bei App-Start).
3 Methoden: `CalculateGrossIncome()`, `CalculateCosts()`, `ApplySoftCap()`.
Alle Modifikatoren (Prestige-Shop, Research, Events, MasterTools, Gilden, VIP, Soft-Cap) an einer Stelle.
Änderungen an der Einkommens-Pipeline müssen NUR NOCH HIER gemacht werden.

### DialogViewModel + IDialogService (per DI injiziert)

`DialogViewModel.cs` (785 Zeilen, 45 ObservableProperties) — als Singleton in DI registriert. Implementiert `IDialogService` (Interface in `Services/Interfaces/IDialogService.cs`). Enthält alle Dialog-bezogenen Properties und Methoden:
- **Alert-Dialog**: ShowAlertDialog(), DismissAlertDialog
- **Confirm-Dialog**: ShowConfirmDialog() mit TaskCompletionSource, ConfirmDialogAccept/Cancel
- **Story-Dialog**: CheckForNewStoryChapter(), ShowStoryDialog(), DismissStoryDialog (Meister Hans NPC)
- **Achievement-Dialog**: AchievementName/Description, DismissAchievementDialog
- **LevelUp-Dialog**: IsLevelUpPulsing, DismissLevelUpDialog
- **Hint-Dialog**: OnHintChanged(), DismissHint (kontextuelle Tooltips/Dialoge)
- **Prestige-Summary**: ShowPrestigeSummary(), DismissPrestigeSummary, GoToShop
- **Prestige-Tier-Auswahl**: ShowPrestigeConfirmationDialogAsync(), SelectPrestigeTier, UpdatePrestigeDialogContent
- **IsAnyDialogVisible**: Aggregierte Property für alle Dialog-Sichtbarkeiten

**IDialogService-Pattern**: Child-ViewModels nutzen `IDialogService` direkt per Constructor Injection (kein Event-Routing über MainViewModel). 13 ViewModels refactored (19.03.2026): SettingsVM, ShopVM, OrderVM, StatisticsVM, WorkerMarketVM, WorkerProfileVM, BuildingsVM, ResearchVM, TournamentVM, BattlePassVM, GuildVM, AchievementsVM, AscensionVM. AlertRequested/ConfirmationRequested Events durch `_dialogService.ShowAlertDialog()` / `_dialogService.ShowConfirmDialog()` ersetzt.

**Kommunikation mit MainViewModel** via Events:
- `DeferredDialogCheckRequested` -> MainViewModel.CheckDeferredDialogs()
- `PrestigeSummaryGoToShopRequested` -> MainViewModel.SelectBuildingsTab()
- `FloatingTextRequested` -> MainViewModel.FloatingTextRequested Event

MainViewModel erstellt DialogVM im Constructor und verdrahtet Events. Dialog-Views in MainView.axaml binden per `DataContext="{Binding DialogVM}"`.

**Konventionen**: Jede Datei hat eigene `using`-Direktiven + `namespace HandwerkerImperium.ViewModels;` + `public partial class MainViewModel`. Event-Handler für BuildingsViewModel.FloatingTextRequested als benanntes Delegate-Feld mit korrektem Unsubscribe in Dispose().

### Dialog-UserControls (Views/Dialogs/)

MainView-Dialoge in eigenständige UserControls extrahiert (reduziert MainView.axaml um ~650 Zeilen):
`OfflineEarningsDialog`, `DailyRewardDialog`, `WelcomeBackOfferDialog`, `AchievementDialog`, `ContextualHintDialog` (Tooltip-Bubble/Dialog, ersetzt TutorialDialog), `StoryDialog` (Hans-Blinzel-Animation via StoryDialogControl.UpdateHansAnimation()), `AlertDialog`, `ConfirmDialog`, `WorkerProfileDialog`.
Die Dialog-Controls `AchievementDialog`, `ContextualHintDialog`, `StoryDialog`, `AlertDialog`, `ConfirmDialog`, `PrestigeSummaryDialog` binden per `DataContext="{Binding DialogVM}"` an DialogViewModel (x:DataType="vm:DialogViewModel"). `OfflineEarningsDialog`, `DailyRewardDialog`, `WelcomeBackOfferDialog`, `WorkerProfileDialog` erben weiterhin `DataContext="{Binding}"` vom MainViewModel. Backdrop-Dismiss im Code-Behind wo noetig.

### 5-Tab Navigation

| Tab | Index | View | Inhalt |
|-----|-------|------|--------|
| Werkstatt | 0 | DashboardView | City-Szene (CityRenderer), Workshop-Karten, Automation-Panel, Quick-Jobs |
| Imperium | 1 | ImperiumView | Gebäude, Crafting+Research, Workers/Manager/MasterTools, Prestige (am Ende) |
| Missionen | 2 | MissionenView | 2 Sub-Tabs: "Heute" (Daily Challenges, Quick Jobs, Glücksrad) + "Wettbewerbe" (Weekly Missions, Turnier/BattlePass/SaisonEvent/Erfolge). Tab-State in MissionsFeatureViewModel (IsTodayTabActive/IsCompetitionsTabActive) |
| Gilde | 3 | GuildView | Guild-Hub, Research/Members/Invite Sub-Seiten |
| Shop | 4 | ShopView | IAP, Goldschrauben-Pakete, Ausrüstungs-Shop |

**Tab-Bar Badges**: Tab 0 (HasPendingDelivery+CanActivateRush), Tab 1 (HasWorkerWarning), Tab 2 (ClaimableMissionsCount+HasFreeSpin). SkiaSharp-Tab-Bar (GameTabBarRenderer, 64dp).

### Dashboard-Header (UI-Entschlackung Phase 1+2)

- **Zeile 1**: Geld + Einkommen/s + Goldschrauben + DailyReward + Settings-Gear. MasterTools-Badge entfernt (CL-1, existiert im Imperium Quick-Access). Netto-Einkommen nur bei negativem Wert sichtbar (`IsVisible="{Binding IsNetIncomeNegative}"`)
- **Zeile 2**: Level+XP (immer), Prestige-Badge, Boost-Indikator, Reputation (bedingt), Streak (bedingt)
- **Statistics-Zugang**: Über Missionen-Tab Wettbewerbe-Grid (IA-3) UND SettingsView
- **Workshop-Canvas**: Dynamische Höhe via `WorkshopCanvasHeight` (2 Spalten, ~160dp/Reihe) statt fixe 800dp

### UX-Optimierungen (04.04.2026)

- **Touch-Targets**: Challenge-Chips (`Padding="10,6" MinHeight="36"`), Worker-Chips (`MinHeight="44"`), Aufgeben-Button (`MinHeight="44"`), BulkBuy-Button (`MinHeight="36"`), Undo-Button (`MinHeight="44"`)
- **Goldschrauben-Badge klickbar**: `Border` → `Button` mit `SelectShopTabCommand` (Dashboard-Header)
- **Aktiver-Auftrag-Banner**: Zeigt "Aktiver Auftrag: {Title}" über Order-Liste wenn Start-Buttons disabled
- **Challenge-Effekt-Subtitles**: Jeder Challenge-Chip zeigt Effekt-Beschreibung als 8pt-Text (RESX-Key `Challenge_{Type}_Desc`)
- **Speedrun-Timer Kontrast**: `#FFFFFFAA` auf `#10FFFFFF` → `TextSecondaryBrush` auf `#25FFFFFF`
- **AutomationPanel**: Gesperrte Toggles durch goldene "Unlock bei Lv.X" Teaser-Badges ersetzt (kein grauer ToggleSwitch)
- **Offline-Effizienz-Hinweis**: "~X% deiner Online-Einnahmen" im OfflineEarningsDialog (Property `OfflineEfficiencyHint`)
- **Lokalisierung**: AscensionView → `AscensionLevelDisplay`/`AvailablePointsDisplay`/`TotalPointsDisplay`/`PendingPointsDisplay`, TournamentView → `BestScoreDisplay`
- **Feature-Brushes**: `CraftBossAccentBrush`, `CraftResearchAccentBrush`, `CraftChatAccentBrush` in App.axaml (statt hardcodierter Farben in GuildView)
- **AutomationIds**: `Missionen_Btn_Statistics`, 6x `Prestige_Btn_Challenge_{Type}` ergänzt
- **ImperiumView Bottom-Spacer**: 24dp Border am Ende für kleine Screens

### Progressive Disclosure (Phase 2)

Level-basierte Section-Visibility innerhalb der Views:

| Property | Level | Betroffene Section |
|----------|-------|--------------------|
| `ShowBannerStrip` | 3 | Dashboard BannerStrip (Events/Boosts) |
| `IsQuickJobsUnlocked` | 5 | Dashboard QuickJobs-Tab |
| `ShowCraftingResearch` | 8 | Imperium Crafting+Forschung |
| `HasLockedBuildings` (BuildingsVM) | datenbasiert | Imperium gesperrte Workshops (zeigt nur wenn welche existieren) |
| `ShowManagerSection` | 10 | Imperium Vorarbeiter Quick-Access |
| `ShowMasterToolsSection` | 20 | Imperium Meisterwerkzeuge Quick-Access |
| `QuickAccessColumns` | dynamisch | Imperium Quick-Access UniformGrid (1-3 Spalten) |
| `ShowTournamentSection` | 35 | Missionen Turnier-Button (PD-1: von Lv50 gesenkt) |
| `ShowSeasonalEventSection` | 45 | Missionen Saison-Event-Button (PD-1: von Lv60 gesenkt) |
| `ShowBattlePassSection` | 55 | Missionen Battle-Pass-Button (PD-1: von Lv70 gesenkt) |

Zusätzlich existieren Tab-Level-Gates (`TabUnlockLevels`): Werkstatt=1, Shop=3, Imperium=5, Missionen=8, Gilde=15.

**Alle Level-Schwellenwerte sind in `Models/LevelThresholds.cs` zentralisiert** (Feature-Unlocks, Automation, Tabs, Hints, Reputation, Daten-Caps). Keine Magic Numbers in Services/ViewModels - immer `LevelThresholds.*` verwenden. GameLoopService nutzt `GameStateService.IsAuto*Unlocked` Properties statt eigener Level-Checks.

### Dashboard-UserControls (Views/Dashboard/)

`DailyChallengeSection`, `WeeklyMissionSection`, `BannerStrip` (mit Fade-Edge Scroll-Indikator rechts, TOUCH-3), `AutomationPanel` (Lv15+ sichtbar, 2x2 Grid statt 4 Spalten, PD-3) - erben DataContext vom Parent (MainViewModel). PaintSurface-Handler nutzen `IProgressProvider`-Interface.

### Gilden-Sub-Seiten (Views/Guild/)

GuildView als Hub mit Sub-ViewModel-Delegation. GuildViewModel leitet an 3 Sub-VMs weiter:
- `GuildWarSeasonViewModel` → `GuildWarSeasonView` (War-Dashboard, Log, Bonus-Missionen)
- `GuildBossViewModel` → `GuildBossView` (Boss-Silhouette, HP, Damage-Feed, Leaderboard)
- `GuildHallViewModel` → `GuildHallView` (Isometrisches HQ, 10 Gebäude, Upgrades)

Weitere Sub-Seiten:
- `GuildResearchView` - SkiaSharp 2D-Forschungsbaum (18 Items, 6 Kategorien, Bezier-Verbindungen)
- `GuildMembersView` - Mitglieder-Liste mit Avatar/Name/Rolle/Beitrag
- `GuildInviteView` - 6-stelliger Invite-Code, Spieler-Browser
- `GuildAchievementsView` - 30 Achievements (10 Typen x 3 Tiers) mit SkiaSharp-Renderer
- `GuildChatView` - Messenger-UI mit Bubble-Layout (eigene rechts, fremde links), Auto-Scroll, 15s Polling
- `GuildWarView` - Kriegs-Detail mit VS-Anzeige, Score-Balken, Timer, Kampf-Button, Saison-Link

Navigation via `NavigationRequested` Events. Sub-VM-Events werden an GuildViewModel propagiert. Zurück-Navigation (".." oder Android-Back) führt zum Guild-Hub. Routes: `guild_chat` (startet Chat-Polling), `guild_war` (lädt War-Status).

### Game Loop

- **GameLoopService** (1s-Takt via DispatcherTimer) → Idle-Einkommen, Kosten, Worker-States, Research-Timer, Event-Check
- **AutoSave** alle 30 Sekunden → GameState → JSON via SaveGameService
- **Research-/Gebäude-Effekte** werden pro Tick angewendet

### Workshop-Typen

Enum: `Carpenter`, `Plumber`, `Electrician`, `Painter`, `Roofer`, `Contractor`, `Architect`, `GeneralContractor`, `MasterSmith`, `InnovationLab`
Jeder Typ hat: `BaseIncomeMultiplier`, `UnlockLevel`, `UnlockCost`, `RequiredPrestige`
**Spezial-Effekte**: MasterSmith produziert passiv Crafting-Materialien, InnovationLab verdoppelt Research-Geschwindigkeit
**Spezialisierung** (ab Level 100, frei wechselbar):

| Typ | Einkommen | Kosten | Effizienz | Worker |
|-----|-----------|--------|-----------|--------|
| Efficiency | +30% | - | - | -1 Slot |
| Quality | - | +15% | +20% | - |
| Economy | -5% | -25% | - | - |

Modifikatoren wirken direkt auf `Workshop.GrossIncomePerSecond` und `Workshop.TotalCostsPerHour`. Daten in `Workshop.WorkshopSpecialization` (JSON-persistiert). UI in WorkshopView ab Level 100. `WorkshopSpecialization.cs` (Model), `SpecializationType` Enum, Farben: Efficiency=#FF9800, Quality=#2196F3, Economy=#4CAF50.

### Worker-System

10 Tiers: `F` (0.4x), `E` (0.65x), `D` (1.0x), `C` (1.5x), `B` (2.25x), `A` (3.35x), `S` (4.9x), `SS` (7.25x), `SSS` (11.25x), `Legendary` (17.5x)
Löhne ~1.8x pro Tier (5-900 EUR/h). ROI sinkt ~15%/Tier.
`HireWorker()` → individuelle Marktpreise: Tier-Basis * Level * Talent (0.7-1.3x) * Persönlichkeit * Spezialisierung * Effizienz-Position. A+ Tiers kosten zusätzlich Goldschrauben.
**HiringCost wird persistiert** (`[JsonPropertyName("hiringCost")]`) → Marktpreise bleiben nach App-Neustart korrekt.
Tier-Farben: F=#9E9E9E(Grau), E=#4CAF50(Grün), D=#2196F3(Blau), C=#9C27B0(Lila), B=#FFC107(Gold), A=#F44336(Rot), S=#FF9800(Orange), SS=#E040FB(Pink), SSS=#7C4DFF(DeepPurple), Legendary=#FFD700(Gold)
**S-Tier+ Freischaltung**: Research `mgmt_10` (UnlocksSTierWorkers) muss erforscht sein → WorkerService liest ResearchEffects und übergibt `hasSTierResearch` an `GeneratePool()`. Ebenso `mgmt_04` (UnlocksHeadhunter) → Pool-Größe 5→8.
**3 Training-Typen**: Efficiency (XP→Level→+Effizienz), Endurance (senkt Fatigue, max -50%), Morale (senkt MoodDecay, max -50%)
**Training Auto-Rest**: Bei 100% Fatigue wird Training automatisch beendet und Ruhe gestartet (identisch mit Arbeits-Modus).
**Worker-Avatare**: WorkerAvatarRenderer (Pixel-Art), Worker.IsFemale deterministisch, RarityFrameRenderer (Tier→Rarity), Idle-Animationen (Atem+Blinzeln ab 56dp)
**Worker-Spezialisierung Tier-abhängig**: Chance F/E=40%, D/C=50%, B/A=65%, S+=85% (statt fix 50%)
**Worker-Markt-Gewichtung**: F=20, E=22, D=22, C=14, B=10, A=6, S=3, SS=1.5, SSS=0.5, Legendary=0.1 (D-Tier sichtbarer)
**Worker-Aura Cap**: S-Tier+ Worker geben Aura-Bonus (5-20%), gedeckelt bei 50% gesamt (GameBalanceConstants.MaxAuraBonus)
**Worker-Namen**: 60 Vornamen × 50 Nachnamen = 3000 Kombinationen (statische Arrays, keine Allokation pro Aufruf)
**GiveBonus**: Kostet 8h Lohn (1 Tageslohn), +30 Mood, bricht QuitDeadline ab

### Goldschrauben-Quellen

1. Mini-Games (3-10), 2. Daily Challenges (~12, BAL-9: von ~19 reduziert), 3. Achievements (5-50), 4. Rewarded Ad (10, BAL-3), 5. IAP (50/150/450), 6. Daily Login (1-25), 7. Spieler-Meilensteine (3-200), 8. Workshop-Meilensteine (2-50)

**Premium +100% GS**: `AddGoldenScrews(amount, fromPurchase)` verdoppelt Gameplay-Quellen für Premium-Spieler. IAP-Käufe (`fromPurchase: true`) werden nicht verdoppelt. Prestige-Shop-Bonus stackt additiv.

### Research Tree

45 Upgrades in 3 Branches a 15 Level: Tools (Effizienz + MiniGame-Zone), Management (Löhne + Worker-Slots), Marketing (Belohnungen + Order-Slots)
Kosten: 500 bis 1B. Dauer: 10min bis 72h (Echtzeit).
**UI**: 2D-Baum-Layout mit 6 SKCanvasViews (Header, ActiveResearch, Tabs, BranchBanner, Tree, Celebration). 30fps Render-Loop.
**Renderer**: ResearchTreeRenderer, ResearchIconRenderer (12 Icons), ResearchActiveRenderer, ResearchBranchBannerRenderer, ResearchTabRenderer, ResearchCelebrationRenderer, ResearchLabRenderer.

### Mini-Games (alle SkiaSharp-basiert)

Alle 10 Mini-Games nutzen dedizierte SkiaSharp-Renderer. Header, Result-Display, Countdown und Buttons bleiben XAML. Jeder Renderer hat `Render()` + `HitTest()`, View hat 30fps Render-Loop, Touch via `PointerPressed` + DPI-Skalierung.
**Tutorial-System**: Erstes Spielen zeigt Overlay (Tracking via `GameState.SeenMiniGameTutorials`). Info-Button (InformationOutline-Icon) in Column=2 aller 10 MiniGame-Views: Binding `ShowTutorialInfoCommand` + `CanShowTutorialInfo`. Sichtbar nur wenn `CanShowTutorialInfo=true` (nach Tutorial bereits gesehen). Bei Views mit Difficulty-TextBlock (Sawing, Forge): StackPanel ersetzt den TextBlock. Bei Views mit Timer-Border (Pipe, Wiring, Painting, Blueprint, RoofTiling, DesignPuzzle, Inspection, Invent): StackPanel wrapping Timer-Border + Info-Button darüber.
**Direktstart**: Alle MiniGames starten automatisch nach Tutorial-Check (kein Start-Button). Start-Buttons sind per `<Panel IsVisible="False">` versteckt. Bei Tutorial-Dismiss wird `StartGameAsync()` aufgerufen.
**Belohnungsanzeige**: NUR bei letzter Aufgabe als Gesamt-Belohnung. Berechnung: `order.FinalReward * GetOrderRewardMultiplier(order)` (inkl. Research, Gebäude, Reputation, Events, Stammkunden). PaintingGame zusätzlich `* comboMult`. Rewarded-Ad setzt `order.IsScoreDoubled = true`, PaintingGame setzt `order.ComboMultiplier`. `CompleteActiveOrder()` wendet beides bei Auszahlung an.
**Ergebnis-Animation**: Zwischen-Runden sofort, letzte Runde staggered (100ms Delay, 250ms Duration).
**Dashboard-Belohnung**: Bindet an `EstimatedReward`/`EstimatedXp` (inkl. Difficulty + OrderType, mit "~"-Präfix).

| MiniGame | Renderer | Besonderheit |
|----------|----------|-------------|
| Sawing | SawingGameRenderer | Holzbrett mit Bezier-Maserung, Schnitt-Animation, Sägemehl+Splitter-Partikel |
| Pipe Puzzle | PipePuzzleRenderer | Metall-Rohre, progressive Wasser-Durchfluss-Animation (BFS), Blasen+Splash |
| Wiring | WiringGameRenderer | Sicherungskasten, Bezier-Kabel, elektrische Pulse (SKPathMeasure) |
| Painting | PaintingGameRenderer | Putzwand, Pinselstrich-Textur, Farbspritzer, Combo-Badge |
| Blueprint | BlueprintGameRenderer | Blaupausen-Grid, Circuit-Verbindungen, Memorisierungs-Scan-Linie |
| RoofTiling | RoofTilingRenderer | Holz-Dachstuhl, 3D-Ziegel, Platzierungs-Funken |
| DesignPuzzle | DesignPuzzleRenderer | Architektenplan, Tuer-Öffnungen, Grundriss-Glow |
| Inspection | InspectionGameRenderer | Beton-Baustelle, pulsierende Lupe, 16 Vektor-Icons (8 gut+8 defekt) |
| ForgeGame | ForgeGameRenderer | Amboss+Esse, Temperatur-Zonen, Hammer-Schlag-Animation |
| InventGame | InventGameRenderer | Violettes Puzzle, 12 Bauteil-Icons, Circuit-Pulse entlang Verbindungen |

Alle Renderer: Struct-basierte Partikel (kein GC), 30fps Render-Loop.

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| `LogService` | Zentraler Logging-Service (Debug-Output). ILogService injiziert in GuildService, GuildWarSeasonService, FirebaseService |
| `GameLoopService` | 1s-Takt: Einkommen, Kosten, Worker-States, AutoSave (30s) |
| `GameStateService` | Zentraler State mit Thread-Safety (lock), GetOrderRewardMultiplier(), AddXp() (aus GameState verschoben) |
| `SaveGameService` | JSON-Persistenz (Load/Save/Import/Export/Reset), MigrateFromV1() (aus GameState verschoben) |
| `WorkerService` | Mood, Fatigue, Training, Ruhe, Kündigung, ReinstateWorker, Research-basierte Markt-Generierung (S-Tier+Headhunter) |
| `PrestigeService` | 7-Tier Prestige + Shop-Effekte + verschärfte Bewahrung + Diminishing Returns (Cap 20x) + Bonus-PP + Meilensteine + Speedrun-Tracking (Partial: PrestigeService.Challenges.cs) |
| `ChallengeConstraintService` | Zentrales Constraint-Interface für 6 Prestige-Challenges (Spartaner/OhneForschung/Inflationszeit/SoloMeister/Sprint/KeinNetz) |
| `AscensionService` | Meta-Prestige nach 3x Legende, 6 permanente Perks (AP-basiert), kompletter Reset inkl. Prestige-Daten |
| `ResearchService` | 45 Research-Nodes, Timer, Effekt-Berechnung |
| `EventService` | 8 Event-Typen + saisonaler Multiplikator |
| `DailyChallengeService` | 3 Challenges/Tag (00:00 Reset) |
| `DailyRewardService` | 30-Tage Login-Zyklus |
| `QuickJobService` | Schnelle MiniGame-Jobs (Rotation 8-15min, Limit 20-40/Tag) |
| `StoryService` | 37 Kapitel von Meister Hans (inkl. 2 Zwischen-Kapitel Lv70/80), nichtlineare Freischaltung |
| `AchievementService` | 110 Erfolge + Goldschrauben-Rewards, PrestigeCompleted/AscensionCompleted/RebirthCompleted-Events |
| `RebirthService` | Workshop-Rebirth (0-5 Sterne), permanent über Prestige+Ascension, Einkommens-Bonus/Upgrade-Rabatt/Extra-Worker |
| `VipService` | VIP-System basierend auf IAP-Gesamtausgaben (None/Bronze/Silver/Gold/Platin), primär QoL-Vorteile (AutoClaim, DeliveryTimer, ExclusiveFrame), minimale Gameplay-Boni (max +5% Income, +5% XP, keine Kosten-Reduktion), Extra-Challenges |
| `OfflineProgressService` | Offline-Einnahmen (Staffelung 80%/25%/10%/3%, tier-skalierender Soft-Cap 4x→20x, Events+Boosts anteilig) + Worker-State-Simulation (2-Phasen: Aktivität→Rest-Recovery, dynamische Fatigue, Training-Fortschritt+Kosten, Arbeits-XP, Level-Ups, alle GameLoop-Modifikatoren) |
| `GoalService` | Dynamisches Nächstes-Ziel-System (10 Prioritaeten inkl. Late-Game: Rebirth/Ascension/AllMax/NextStar/Stretch, Cache mit Dirty-Flag) |
| `OrderGeneratorService` | 4 OrderTypes, Stammkunden-Zuweisung, Reputation beeinflusst Qualität |
| `ReviewService` | In-App Review (14-Tage Cooldown, 5 Trigger) |
| `AudioService` | SoundPool (15 Sounds), Vibrator (7 Muster), MediaPlayer (Musik). Factory-Pattern |
| `NotificationService` | 4 Typen, AlarmManager+BroadcastReceiver, BootReceiver, 6 Sprachen |
| `PlayGamesService` | Leaderboards, kein Cloud-Save (NuGet-Limitation). Factory-Pattern |
| `ManagerService` | 14 Vorarbeiter: Unlock/Upgrade (Lv.1-5), Workshop-Boni |
| `TournamentService` | Wöchentliche MiniGame-Turniere (alle 10 MiniGame-Typen), 9 simulierte Gegner |
| `BattlePassService` | 50-Tier Battle Pass, Free/Premium, 42-Tage-Saisons |
| `SeasonalEventService` | 4 Events/Jahr (Mär/Jun/Sep/Dez, 1.-14.), SP-Währung (5+Bonus pro Auftrag), Event-Shop (6 Items/Saison: 4 Basis + 2 einzigartige), IDisposable (OrderCompleted-Subscription) |
| `GuildService` | Firebase REST API, Gilden-CRUD, Wochenziele, GetMaxMembers(), CountAndSyncMemberCountAsync (Race-Condition-frei), Duplikat-Erkennung, Verwaiste-Mitglieder-Bereinigung (>30d inaktiv), Rollen-Management (Promote/Demote/Kick/TransferLeadership) |
| `GuildInviteService` | Einladungs-Subsystem (extrahiert aus GuildService am 01.05.2026): 6-stellige Invite-Codes, Spieler-Browser (verfuegbare Spieler), Direkt-Einladungs-Inbox (Send/Receive/Accept/Decline). Beitritts-Operationen delegieren an `IGuildService.JoinGuildAsync` (kein doppelter Lock) |
| `GuildResearchService` | 18 Gilden-Forschungen (6 Kategorien), Timer, Effekt-Cache, SemaphoreSlim |
| `GuildWarSeasonService` | Einziger Gilden-Krieg-Service (Saison-System, Matchmaking, Scoring, Ligen, Bonus-Missionen). Legacy GuildWarService entfernt |
| `GuildHallService` | Interaktives Gilden-HQ mit 10 Gebäuden (Level 1-5), Upgrade-Timer, Effekt-Cache |
| `GuildBossService` | Kooperative Gilden-Bosse (6 Typen), Schaden-Tracking, Spawn/Despawn, Belohnungen |
| `GuildTipService` | Kontextuelle Gilden-Tipps (Preferences-basiert, 24h Cooldown) |
| `GuildAchievementService` | 30 Gilden-Achievements (10 Typen x 3 Tiers), Firebase-Tracking |
| `FirebaseService` | Anonymous Auth, Token-Refresh (55min, Retry bei Netzwerkfehler), CRUD, 5s Timeout, SemaphoreSlim. PlayerId-GUID (stabile Spieler-Identität, überlebt Account-Wechsel), auth_to_player Mapping via SyncAuthToPlayerMappingAsync() |
| `GameIntegrityService` | HMAC-SHA256-Signierung für Gilden-relevante GameState-Werte (Level, Prestige, Money, GoldenScrews, Orders). Geräte-spezifischer Schlüssel (Package-Name + Installations-GUID). GuildService prüft Signatur vor Firebase-Updates |
| `GameAssetService` | LRU-Cache 50MB, WebP→SKBitmap + animierte WebP Multi-Frame, PlatformAssetLoader. Statischer `GameAssetService.Current` Zugriff für Views (kein Service-Locator) |
| `CraftingService` | 20 Rezepte in 3 Tiers, Produktionsketten, Echtzeit-Timer, skalierende Verkaufspreise (log₂-Formel × alle Einkommens-Multiplikatoren) |
| `AutoProductionService` | Automatische Tier-1-Produktion: 180s/Worker (Standard), 120s (InnovationLab), 60s (MasterSmith). Unlock ab WS-Level 50. Offline-Produktion mit Staffelung |
| `WeeklyMissionService` | 5 Wochenmissionen, Montag-Reset, 50 Goldschrauben Bonus |
| `WelcomeBackService` | Angebote nach 24h+ Abwesenheit, Starter-Paket (einmalig) |
| `LuckySpinService` | Täglicher Gratis-Spin, 8 Preiskategorien (gewichtet) |
| `EquipmentService` | 4 Typen x 4 Seltenheiten, Drop nach MiniGames (5-20% skaliert nach Schwierigkeit, +5% bei Perfect), Shop-Rotation |

## Game Juice (kompakte Übersicht)

| Feature | Beschreibung |
|---------|-------------|
| Workshop Cards | Farbiges Border + WorkshopCardRenderer (10 thematische Szenen, 48dp) |
| Worker Avatars | Pixel-Art (6 Hauttöne/Haare/Kleidung, Tier-Farbe+Sterne, Mood, RarityFrame) |
| Meister Hans Portrait | 4 Stimmungen, Idle-Bobbing, Blinzel-Animation, 120x120 |
| Golden Screw Icon | Gold-Shimmer CSS-Animation (scale+rotate) |
| Level-Up | XP-Bar Puls, CelebrationOverlay + Sound bei Meilensteinen |
| Income FloatingText | Grüner Text, +100px, 1.5s |
| TapScale-Effekt | Globale CSS-Styles: scale(0.95) bei :pressed, 80ms CubicEaseOut |
| Tab-Bar CraftTextures | CraftTextures.DrawWoodGrain() mit Holz-Maserung |
| Combo Badge | Gold-Badge mit Fire-Icon bei Combo >= 3 (PaintingGame) |
| Bottom Sheets | CSS translateY(800→0px), CubicEaseOut |
| Hold-to-Upgrade | DispatcherTimer 120ms, stilles Upgrade, Zusammenfassung am Ende |
| Tab-Wechsel | FadeIn 150ms CubicEaseOut |
| Workshop-Szenen | WorkshopSceneRenderer: AI-Bitmap + Level-Overlays (Sterne Lv250+, Gold-Aura Lv500+, Shimmer Lv1000) |
| MiniGame Result | Staggered Stars, Rating-Farbe, Border-Pulse, MiniGameEffectHelper |
| MiniGame Countdown | Pulsierendes 3-2-1-GO! Overlay |
| Münz-Partikel | Goldene Coin-Partikel im City-Header via AnimationManager |
| Money-Display Flash | Opacity-Flash 400ms bei Geld-Einnahmen |
| Confetti | AddLevelUpConfetti bei Level-Up und Goldschrauben-Events |
| Offline-Earnings Burst | FloatingText "money" → Münz-Partikel + Money-Flash |
| GameJuiceEngine | Zentrale Effekt-Engine: ScreenShake, RadialBurst, CoinsFlyToWallet, SparkleEffect etc. Struct-Pool (max 200) |
| OdometerRenderer | Animierte Geld-Anzeige mit rollenden Ziffern, Suffix-Crossfade, Gold-Flash |
| CoinFlyAnimation | 8-16 Münzen auf Bezier-Kurven, Euro-Praegung, HUD-Pulse bei Ankunft |
| SkiaShimmerEffect | GPU-Shimmer auf Goldschrauben-Bereich (permanent wenn > 0) |
| City-Szene | CityRenderer: AI-Bitmap + Wetter-Overlay (kein HitTest, kein prozeduraler Fallback) |
| City Weather | CityWeatherSystem: Regen+Regenbogen, Sonne+Shimmer, Blätter, Schnee (80 Struct-Pool, canvas.ClipRect auf City-Bounds, nur bei GraphicsQuality >= Medium) |
| Reward-Zeremonie | Full-Screen Overlay: Scale-In, Confetti (120), Feuerwerk, 5 CeremonyTypes, 4s Tap-to-Dismiss |
| Loading-Screen | Zahnraeder, Funken-Partikel, Gradient-Fortschrittsbalken, rotierende Tipps |
| Splash-Screen | "Die Schmiede": Zahnraeder, Amboss, Hammer-Animation, Glut-Partikel |
| Glücksrad | LuckySpinWheelRenderer: 8 Segmente, Nieten-Rand, SkiaSharp-Icons, Spin-Animation ~60fps. Segment-Reihenfolge (0-7): MoneySmall, MoneyMedium, MoneyLarge, XpBoost, GoldenScrews, SpeedBoost, ToolUpgrade, Jackpot. Winkelberechnung: `360 - segmentCenter` (Rad dreht im Uhrzeigersinn, Zeiger oben) |
| Workshop-Icons | WorkshopGameCardRenderer: Werkstatt-spezifische GameIconRenderer-Icons auf Upgrade-Button + dimmed auf Locked-Karten |
| Grafik-Qualität | GraphicsQuality (Low/Medium/High) steuert Wetter, Partikel etc. in SettingsView |
| Gilden-Forschungsbaum | 18 Items, Bezier-Verbindungen, Flow-Partikel, GuildHallHeader (Steinmauer, Fackeln, Emblem), Node-Namen+Kosten/Effekt-Labels, Lock-Badges, Drop-Shadow, Inner-Highlight |
| Research-Labor | ResearchLabRenderer: Werkstatt-Szene, Zahnraeder, Dampf, Glühbirne |
| Research-Baum | 2D Top-Heroes-Style, Branch-Farben, Flow-Partikel, Branch-Banner, Celebration-Confetti |
| Forschungs-Hintergrund | ResearchBackgroundRenderer: Nussholz, Holzmaserung, Zahnrad-Wasserzeichen, Vignette |

## Gameplay-Suchtfaktor-Mechaniken (24.04.2026, v2.0.35 — Gameplay-Struktur-Wandel)

Vier fundamentale Mechaniken die den Flow veraendern — nicht "mehr Einkommen", sondern **echte Spieler-Entscheidungen**:

### Feature B: Risk/Reward-Strategie pro Auftrag

Vor jedem MiniGame-Start waehlt der Spieler eine von drei Strategien:

| Strategie | Reward | MiniGame | Miss-Handling |
|-----------|--------|----------|---------------|
| **Safe**     | 0,75x | +50% breitere Zonen, +30% Zeit | Normales Rating |
| **Standard** | 1,0x  | Baseline | Normales Rating |
| **Risk**     | 2,0x  | -50% Zonen, +30% Tempo, -30% Zeit | **0 Reward + −10 Reputation** |

Orthogonal zu `OrderDifficulty` (die am Auftrag fest ist). Risk-Wahl triggert Bestaetigungs-Dialog.
`Order.Strategy` (persistiert), `Order.HasHardFailed` (bool, bei Risk+Miss=true).
`BaseMiniGameViewModel.CurrentStrategy` wird in `SetOrderId` aus Order gelesen.

Alle 10 MiniGames integriert:
- SawingGame/ForgeGame: Zone * ToleranceMultiplier, Speed * SpeedMultiplier
- 7 Time-basierte (Pipe/Wiring/Painting/RoofTiling/Inspection/Blueprint/DesignPuzzle/InventGame):
  MaxTime * TimeMultiplier (mit Math.Max-Lower-Bound)

UI: `Views/OrderView.axaml` ersetzt "Starten"-Button durch 3 Strategy-Buttons mit Icon +
Multi-Zeilen-Beschreibung + Multiplikator-Anzeige. Risk-Button ist rot.

### Feature D: Live-Auftrags-Stream mit Deadlines

Auftraege landen nicht mehr als statische Liste im Pool — sie kommen live rein:
- `GameLoopService` spawnt alle 25s mit 50% Chance einen neuen Live-Auftrag
- Cap 5 Live-Auftraege gleichzeitig (neben regulaeren/Material-Orders)
- `ExpiresAt` 45-180s (Standard) bzw. 45-90s (Premium/VIP)
- Alle 3 Ticks: `ExpireOldLiveOrders` entfernt abgelaufene ohne Penalty

Premium/VIP (5% Spawn-Chance):
- 3x BaseReward, 2.5x BaseXp, kuerzere Deadline
- Lila VIP-Badge im Dashboard + Crown-Icon

`Order.IsLive`, `Order.IsPremium`, `Order.ExpiresAt`, `LiveCountdownSeconds/Text` (computed).
UI: Roter LIVE-Badge + Countdown-Text, lila VIP-Badge auf Order-Cards.

### Feature C: Workshop-Spezialisierungs-Pfade

Das System war bereits implementiert (Efficiency/Quality/Economy-Pfade mit Income/Cost/Efficiency/
Worker-Capacity-Modifiers in `WorkshopSpecialization.cs`). Balance-Update:
- **Unlock-Level 100 → 50** (Mid-Game-Build-Entscheidung statt Late-Game-Luxus)
- **Re-Spec-Kosten 20 Goldschrauben** (erste Wahl kostenlos, Wechsel kostet, Entfernen kostenlos)

Workshop bekommt damit eine Identitaet — Spezialisierung auf Efficiency (+30% Einkommen, -1 Worker-Slot)
vs Quality (+20% Worker-Effizienz, +15% Kosten) vs Economy (-25% Kosten, -5% Einkommen) ist eine
echte Build-Entscheidung bei Lv50.

### Feature A: Parallele Werkstaetten (Multi-Order)

Kernarchitektur-Umbau — der Spieler kann bis zu **3 Auftraege gleichzeitig** in unterschiedlichen
Werkstaetten laufen lassen.

**Architektur:**
- `GameState.ParallelOrdersByWorkshop` (Dictionary<WorkshopType, Order>) — pro Workshop max 1
- `GameState.ActiveOrder` bleibt als Vordergrund-Slot (aktuell im MiniGame bearbeiteter Auftrag)
- Max 3 parallel via `GameBalanceConstants.MaxParallelOrders`
- SaveGame Version 5→6 mit automatischer Migration (alter `ActiveOrder` landet im Dictionary)

**GameStateService-API:**
- `StartOrder(Order)`: Order landet in ParallelOrdersByWorkshop UND in ActiveOrder
- `GetParallelOrder(WorkshopType)`: liefert wartenden Auftrag pro Werkstatt
- `ResumeParallelOrder(WorkshopType)`: setzt pausierten Auftrag wieder als ActiveOrder
- `PauseActiveOrder()`: ActiveOrder=null, Auftrag bleibt im Dictionary
- `CanStartParallelOrder(WorkshopType)`: prueft Workshop-Slot + globales Cap

**Flow:**
- Spieler akzeptiert Auftrag A in Workshop X → StartOrder → A ist Vordergrund + in Dict[X]
- Spieler klickt Back im OrderDetail → PauseActiveOrder → A bleibt in Dict[X], Vordergrund leer
- Spieler akzeptiert Auftrag B in Workshop Y → A bleibt in Dict[X], B wird neuer Vordergrund in Dict[Y]
- Spieler klickt auf A im Fortsetzen-Banner → ResumeParallelOrder(X) → B wird pausiert, A ist Vordergrund
- MiniGame fertig fuer A → CompleteActiveOrder → A raus aus Dict[X], Vordergrund leer
- Cancel Order A → CancelActiveOrder → A raus aus Dict[X], zurueck in AvailableOrders

**UI (DashboardView.axaml):**
- Neues **Fortsetzen-Banner** oben im Auftrags-Bereich (nur sichtbar wenn HasParallelOrders=true)
- Gradient-Hintergrund, Zaehler, ItemsControl mit Tap-to-Resume-Button pro Order
- Alter "Nur ein Auftrag gleichzeitig"-Block weg: Spieler kann waehrend laufendem Auftrag neue annehmen

**Neue MainViewModel-Properties:**
- `ParallelOrders` (ObservableCollection<Order>) + `HasParallelOrders` (bool)
- `ResumeParallelOrderCommand(string workshopTypeName)` (XAML-kompatibel)

### Geaenderte / neue Dateien (v2.0.35)

**Neu:**
- `Models/Enums/OrderStrategy.cs` + Extension-Methoden

**Erweitert:**
- `Models/Order.cs` (Strategy, HasHardFailed, IsLive, IsPremium, LiveCountdownSeconds/Text, ExpiresAt in IsExpired)
- `Models/GameState.cs` (ParallelOrdersByWorkshop, Version 5→6)
- `Models/GameBalanceConstants.cs` (SpecializationUnlockLevel 100→50, RespecCost=20 GS, MaxParallelOrders=3)
- `Services/Interfaces/IGameStateService.cs` + `IOrderGeneratorService.cs` (neue Methoden)
- `Services/GameStateService.Orders.cs` (Multi-Order-API)
- `Services/OrderGeneratorService.cs` (GenerateLiveOrder, ExpireOldLiveOrders, Event OrderSpawned)
- `Services/GameLoopService.cs` + `.PeriodicChecks.cs` (IOrderGeneratorService injiziert, Live-Tick-Checks)
- `Services/SaveGameService.cs` (V5→V6 Migration, Sanitize ParallelOrdersByWorkshop)
- `ViewModels/OrderViewModel.cs` (3 Strategy-Buttons via StartWithStrategyCommand, Back=Pause)
- `ViewModels/MiniGames/BaseMiniGameViewModel.cs` (CurrentStrategy + Hard-Fail-Handling)
- Alle 10 MiniGame-VMs (Strategy-Modifier in InitializeGame)
- `ViewModels/EconomyFeatureViewModel.cs` (CanStartParallelOrder + PauseActiveOrder + ResumeParallelOrderAsync + RefreshParallelOrders)
- `ViewModels/MainViewModel.cs` + `.Economy.cs` (ParallelOrders, HasParallelOrders, ResumeParallelOrderCommand)
- `ViewModels/WorkshopViewModel.cs` (Re-Spec-Kosten via TrySpendGoldenScrews)
- `Views/OrderView.axaml` (3 Strategy-Buttons)
- `Views/DashboardView.axaml` (LIVE/VIP-Badges auf Orders + Fortsetzen-Banner ueber AvailableOrders)
- 7 RESX-Dateien (ca. 21 neue Keys: 10 fuer Strategy, 2 fuer Live/VIP, 3 fuer Parallel, 6 fuer Confirm-Dialoge)

---

## FPS-Profile + Gilde-UX (24.04.2026, v2.0.34)

### FPS-Profile (`Graphics/FpsProfile.cs`) — plattformadaptives Rendering

Bis v2.0.33 war der `GraphicsQuality`-Enum nur ein Wetter/Shimmer-Schalter. Alle Render-Timer waren hartcoded auf 30fps. `FpsProfile.cs` liefert jetzt pro Grafikqualitaet eigene Intervalle:

| View                       | Low          | Medium       | High         |
|----------------------------|--------------|--------------|--------------|
| MiniGame (10 Views)        | 24fps (42ms) | 30fps (33ms) | 30fps (33ms) |
| Research / Workshop /      | 15fps (66ms) | 20fps (50ms) | 24fps (42ms) |
|   GuildResearch            |              |              |              |
| Dashboard Idle             | 5fps (200ms) | 10fps (100ms)| 10fps (100ms)|
| Dashboard bei Effekten     | 15fps (66ms) | 24fps (42ms) | 30fps (33ms) |
| WorkerAvatar shared        | 5fps (200ms) | 8fps (125ms) | 10fps (100ms)|
| MainView (BG + TabBar)     | 10fps (100ms)| 15fps (66ms) | 15fps (66ms) |

Integrationspunkte:
- `App.axaml.Initialize`: Plattform-Default setzen (Android=Medium, Desktop=High)
- `MainViewModel.InitializeAsync`: Nach State-Load `FpsProfile.SetCurrent(state.Settings.GraphicsQuality)`
- `SettingsViewModel.OnSelectedGraphicsQualityChanged`: `FpsProfile.SetCurrent(...)` bei Slider-Aenderung
- `FpsProfile.CurrentChanged`-Event: `WorkerAvatarControl` abonniert den Event und aktualisiert seinen Shared-Timer sofort
- Alle anderen Views lesen das Intervall beim naechsten `StartRenderLoop()` (Tab-Wechsel/IsVisible-Toggle) neu

Selbst bei `High` wurden Werte konservativ gesenkt (Research/Workshop 30→24fps, WorkerAvatar 20→10fps). Visuell nicht unterscheidbar (24fps = Kino-Standard), durchgaengig weniger Battery.

**Verifiziert: Performance-Analyse vom 24.04.2026 ergab, dass alle 10 MiniGames bereits korrekt bei `IsResultShown=true` ihren 30fps-Timer stoppen (via `OnPaintSurface`-Guard). Der Performance-Agent hatte sich geirrt — kein Fix noetig.**

### Gilde-UX-Refactor — 5 Tabs statt 7er-Scroll-Liste

`Views/GuildView.axaml` (950 Zeilen) hatte im `IsInGuildState`-Container:
- Banner + Wochenziel + Contribute-Slider
- Redundantes 2x2 Quick-Status-Grid (Krieg/Boss/Hall/Forschung — zeigte dasselbe wie Nav-Karten darunter)
- Kontextueller Tipp permanent sichtbar
- **9 Nav-Karten vertikal** (Krieg, Boss, Hall, Erfolge, Chat, Krieg-Detail [!], Forschung, Mitglieder, Einladen)
- **Doppelte War-Navigation**: `NavigateToWarSeason` + `NavigateToWar` als separate Buttons

Ergebnis: 3-4 Bildschirme Scroll-Strecke um alle Features zu sehen. Keine Prioritaets-Signale, gleichwertig aussehende Karten.

**Neue Struktur:**
- Banner (bleibt, kompakt)
- **5-Tab-Leiste** (`UniformGrid Columns=5`, `Classes.Active`-Style fuer aktiven Tab mit `CraftPrimaryLightBrush`-Hintergrund)
  - Uebersicht (Home-Icon): Wochenziel + Contribute + kontextueller Tipp (nur hier sichtbar)
  - Kampf (Sword): War-Saison + Boss + (optional) aktueller War-Detail wenn `HasActiveWar`
  - Forschung (FlaskOutline): Nav-Karte zu GuildResearchView
  - Chat (ChatBubble): Nav-Karte zu GuildChatView
  - Mitglieder (AccountGroup): Members + Hall + Erfolge + Einladen
- "Gilde verlassen" bleibt immer sichtbar am Ende

**Entfernt:**
- 2x2 Quick-Status-Grid (redundant)
- Doppelter War-Navigations-Button (konsolidiert auf War-Season mit optionalem Detail-Button bei aktivem Krieg)

**Neu in `GuildViewModel.cs`:**
- Enum `GuildSubTab` (Overview/Combat/Research/Chat/Members)
- `ActiveSubTab` Observable-Property mit `[NotifyPropertyChangedFor]` fuer 5 `IsXxxTabActive`-Properties
- `SelectSubTabCommand(string tabName)` — tab-parameter als String (XAML-freundlich)

**Neue RESX-Keys (v2.0.34):** `GuildTabOverview`, `GuildTabCombat`, `GuildTabResearch`, `GuildTabChat`, `GuildTabMembers`, `GuildWarDetailTitle` — in 7 Dateien (neutral + 6 Sprachen).

### Geaenderte / neue Dateien (v2.0.34)

**Neu:**
- `Graphics/FpsProfile.cs` — zentrale FPS-Tabelle mit 6 Method-Gruppen (MiniGame/ScrollView/DashboardIdle/DashboardActive/WorkerAvatar/MainView)

**Erweitert:**
- `App.axaml.cs` (Plattform-Default in `Initialize`)
- `ViewModels/MainViewModel.Init.cs` (`FpsProfile.SetCurrent` nach State-Load)
- `ViewModels/SettingsViewModel.cs` (`FpsProfile.SetCurrent` bei Graphics-Quality-Change)
- `ViewModels/GuildViewModel.cs` (`GuildSubTab` enum + `ActiveSubTab` + `SelectSubTabCommand`)
- `Views/MainView.axaml.cs` (`FpsProfile.MainView()`)
- `Views/DashboardView.axaml.cs` (`FpsProfile.DashboardIdle()`/`DashboardActive()`)
- `Views/WorkshopView.axaml.cs` (`FpsProfile.ScrollView()`)
- `Views/ResearchView.axaml.cs` (`FpsProfile.ScrollView()`)
- `Views/Guild/GuildResearchView.axaml.cs` (`FpsProfile.ScrollView()`)
- `Controls/WorkerAvatarControl.cs` (`FpsProfile.WorkerAvatar()` + `CurrentChanged`-Event-Subscribe fuer Live-Update)
- 10 `Views/MiniGames/*.axaml.cs` (`FpsProfile.MiniGame()` statt hartcoded `TimeSpan.FromMilliseconds(33)`)
- `Views/GuildView.axaml` (Tab-Leiste + Content-Gruppierung + `UserControl.Styles`-Block fuer `Button.Active`)
- 7 RESX-Dateien (6 neue Keys je Sprache)

---

## System-Erweiterung (24.04.2026, v2.0.33 — Analytics + Cloud-Save + Saison-Visuals)

Vollstaendige Umsetzung der strategischen Optimierungs-Roadmap (6 Teilaufgaben):

### P1a — Firebase Analytics + Remote Config (Fundament fuer Daten-getriebene Optimierung)

**Warum:** Ohne Telemetrie ist jede Balance-Entscheidung Bauchgefuehl. Ohne Remote-Config erfordert jede Balancing-Aenderung einen Store-Release (Tage).

**Umsetzung:**
- `Services/Interfaces/IAnalyticsService.cs` + `Services/AnalyticsService.cs`: Queue-basiertes Batching (30s-Intervall, 500-Event-Cap), DSGVO-IsEnabled-Gate, Events → `analytics_events/{YYYY-MM-DD}/$pushId` (Firebase REST). Auto-Flush bei Dispose (max 2s Shutdown-Blockierung).
- `Services/Interfaces/IRemoteConfigService.cs` + `Services/RemoteConfigService.cs`: Lokaler Preferences-Cache (offline-tolerant), JSON-Dot-Notation (z.B. `balancing.starter_offer_min_level`). Lade-Timeout 5s in Loading-Pipeline — blockiert App-Start nicht.
- `Models/AnalyticsEvents.cs`: 38 Event-Konstanten (Session, Tutorial, Progression, Monetisierung, Gilden, Fehler).
- `Models/AnalyticsUserProperties.cs`: 8 User-Properties (language, premium, prestige_tier, etc.).
- `Models/RemoteConfigKeys.cs`: 12 vorbereitete Keys (Balancing-Overrides, Feature-Flags, Promo).

**Instrumentierung (wichtigste Events):**
- `PrestigeService.DoPrestige` → `prestige_done` mit {tier, points_earned, bonus_pp, tier_count_after, run_minutes, challenges_active, prestige_pass}
- `AscensionService.PerformAscension` → `ascension_done` mit {ascension_level, ap_gained, total_ap}
- `RebirthService.DoRebirth` → `rebirth_done` mit {workshop, star_level}
- `AchievementService.Unlock` → `achievement_unlocked` mit {id, category, xp_reward, screw_reward}
- `EconomyFeatureViewModel.TryPurchaseWorkshopAndNotify` → `workshop_unlocked` mit {type, player_level, total_earned}
- `ShopViewModel.PurchaseItemAsync` → `iap_item_viewed` (beim Antippen) + `iap_purchase_started/success/failed` (im Flow)
- `SettingsViewModel.BuyPremiumAsync` → `iap_purchase_started/success/failed`
- `MainViewModel.CheckCloudSaveAsync` → `cloud_save_downloaded`

### P1b — DSGVO-Consent-Dialog (Play-Store-Compliance)

- `SettingsData.AnalyticsEnabled` + `AnalyticsConsentShown` (beide default false, Opt-In)
- `MainViewModel.ShowAnalyticsConsentIfNeededAsync()`: Nicht-blockierender Dialog (fire-and-forget nach 1.5s Delay + 2.5s bei Dialog-Konflikt). Nutzt bestehenden `ShowConfirmDialog` — keine neue View noetig.
- Settings-Toggle in "Datenschutz"-Card unter Cloud-Save (6 Sprachen: `AnalyticsLabel`, `AnalyticsDesc`).
- Bei Opt-Out: Queue wird sofort geleert, Flush-Timer gestoppt. Bei Opt-In: InitializeAsync + session_start-Event.

### P2 — Firebase-Cloud-Save (ersetzt nicht-funktionalen Play-Games-Stub)

**Warum:** Spieler mit 50h Spielzeit verlieren bei Geraetewechsel/Reinstall ihren Stand. Retention-killer. Play Games v2 unterstuetzt keine Snapshots. Firebase war bereits fuer Gilden integriert.

**Umsetzung:**
- `Services/Interfaces/ICloudSaveService.cs` + `Services/CloudSaveService.cs`: REST via bestehenden FirebaseService. Struktur `cloud_saves/{playerId}/{metadata|data}`.
- `Models/CloudSaveMetadata.cs`: Header-only (Level, Money, GoldenScrews, PrestigePoints, AscensionLevel, SavedAtIso, StateVersion, AppVersion) — vermeidet unnoetigen Traffic bei Konflikt-Check.
- HMAC-Neusignierung beim Download: Der IntegrityService-Key ist geraetegebunden — CloudSaveService signiert den State fuer das aktuelle Geraet neu. Cloud-Save schuetzt gegen Geraeteverlust, nicht gegen Save-Editing (das bleibt lokal via HMAC wirksam).
- `SaveGameService`: Auto-Cloud-Upload beim lokalen Save, Rate-Limit 2min (Firebase-Kosten-Kontrolle).
- `MainViewModel.CheckCloudSaveAsync`: Lokaler Stand gegen Cloud-Metadaten (Toleranz 5s Clock-Skew). Bei neuerem Cloud-Stand → Konflikt-Dialog mit Level+Money beider Staende.
- `database.rules.json`: `cloud_saves/{playerId}` — read+write nur fuer eigenen playerId via auth_to_player-Mapping, Metadaten mit Schema-Validation (Level max 10000, State-Blob max 1 MB).

### P3 — DashboardView-Refactor (Hit-Tester-Extraktion)

- `Helpers/WorkshopCardHitTester.cs` (statische Klasse): Koordinaten-Konvertierung (Avalonia → Skia), Grid-Berechnung (2 Spalten, dynamische Zeilen), Upgrade-Button-Hit-Test. Plus statische Check-Helfer: `IsScrollDistance`, `HasScrollViewerMoved`, `IsTapDuration`. Konstanten zentralisiert (TapDistanceThreshold, TapMaxDurationMs, ScrollOffsetThreshold).
- `DashboardView.axaml.cs` wird schlanker (~60 Zeilen Logik in die statische Hilfsklasse verschoben). State-Machine (_workshopPressedTarget etc.) bleibt in der View — sie ist eng an Avalonia-PointerEventArgs gebunden.

### P4 — Saison-Events visuell differenzieren (Game Juice)

**Warum:** 4 Saisons/Jahr hatten gleiche Optik (nur Monat-basiertes Wetter). Aktives SeasonalEvent war visuell unsichtbar.

**Umsetzung in `CityWeatherSystem.cs`:**
- Neuer WeatherType `Blossoms` (Kirschblueten — 4-Farben-Palette in `s_blossomColors`, rosa-weisse Palette, rotierte Ovals mit heller Randlinie fuer 3D-Look).
- `Refresh(SeasonalEvent? activeEvent)` ersetzt `SetWeatherByMonth`: Bei aktivem Event wird Wetter aus `activeEvent.Season` abgeleitet (Spring → Blossoms, Summer → Sunshine, Autumn → Leaves, Winter → Snow) + `IntensityMultiplier = 2.0f` (doppelte Partikel-Dichte). Ohne Event fallback auf Monat-Wetter.
- `MaxParticles` von 80 auf 160 erhoeht (fuer Intensified-Modus).
- `CityRenderer.Render`: 5s-Refresh-Timer der `weather.Refresh(state.CurrentSeasonalEvent)` periodisch aufruft (Event-Start/-Ende + Mitternachts-Monatswechsel setzen sich durch).

### P5 — AAB-Groesse (Analyse)

**Ergebnis:** Assets sind nur 3.4 MB (Icons 1.9 MB, Rest City/Worker/Splash/Minigames). Die 65 MB AAB-Groesse kommt zu >90% aus .NET-Runtime + SkiaSharp native + Android-Bindings (Xamarin-Libraries).

**Blockiert:** Die wirkungsvollen Hebel (Trimming, R8/Proguard, Profiled-AOT) sind durch bekannte Crash-Bugs deaktiviert (siehe Haupt-CLAUDE.md Troubleshooting: "Release-Build crasht / OOM beim Start").

**Realitaet:** Play-Store-User-Download durch ABI-Split-AAB nur ~25-30 MB (nicht 65 MB — das ist die Bundle-Groesse fuer alle Architekturen kombiniert).

**Empfehlung:** Bei naechstem .NET-Upgrade erneut pruefen ob AOT+Trimming stabil ist. Kein Risiko-Eingriff ohne Stabilitaets-Garantie.

### Geaenderte / neue Dateien (v2.0.33)

**Neu:**
- `Services/Interfaces/IAnalyticsService.cs`
- `Services/Interfaces/IRemoteConfigService.cs`
- `Services/Interfaces/ICloudSaveService.cs`
- `Services/AnalyticsService.cs`
- `Services/RemoteConfigService.cs`
- `Services/CloudSaveService.cs`
- `Models/AnalyticsEvents.cs`
- `Models/RemoteConfigKeys.cs`
- `Models/CloudSaveMetadata.cs`
- `Helpers/WorkshopCardHitTester.cs`

**Erweitert:**
- `App.axaml.cs` (DI-Registrierung fuer 3 neue Services)
- `Models/SettingsData.cs` (AnalyticsEnabled + AnalyticsConsentShown)
- `Services/SaveGameService.cs` (Cloud-Save via ICloudSaveService statt IPlayGamesService)
- `ViewModels/MainViewModel.cs` (Ctor + Felder)
- `ViewModels/MainViewModel.Init.cs` (CheckCloudSaveAsync auf Firebase + ShowAnalyticsConsentIfNeededAsync)
- `ViewModels/MainViewModel.Economy.cs` (Analytics an EconomyVM weiterreichen)
- `ViewModels/EconomyFeatureViewModel.cs` (Workshop-Unlock-Tracking)
- `ViewModels/SettingsViewModel.cs` (AnalyticsEnabled + CanUseCloudSave + IAP-Events)
- `ViewModels/ShopViewModel.cs` (IAP-Funnel-Events)
- `Services/PrestigeService.cs` / `AscensionService.cs` / `RebirthService.cs` / `AchievementService.cs` (optionaler IAnalyticsService-Parameter + TrackEvent)
- `Views/SettingsView.axaml` (Datenschutz-Card + CanUseCloudSave-Binding)
- `Graphics/CityWeatherSystem.cs` (WeatherType.Blossoms + Refresh + IntensityMultiplier)
- `Graphics/CityRenderer.cs` (5s-Refresh-Timer fuer Event-Wetter)
- `Loading/HandwerkerImperiumLoadingPipeline.cs` (RemoteConfig-Step)
- `Views/DashboardView.axaml.cs` (Hit-Testing an Helper delegiert)
- `database.rules.json` (cloud_saves + remote_config + analytics_events Pfade)
- 7 RESX-Dateien (Privacy, Analytics*, CloudSave*, SplashStep_Config — 10 Keys je 6 Sprachen + neutral)

### Firebase-Rules-Deployment noetig

Vor dem Release muss `database.rules.json` deployt werden:
```
npx firebase-tools deploy --only database --project handwerkerimperium-487917
```

## Review-Fixes (20.04.2026, Multi-Agent-Review + Bug-Session)

Fuenfter umfassender Review (6 Agenten parallel: code-review, health, game-audit, performance, security, ui) plus 2 User-gemeldete Bugs. Gesundheits-Score: B+ (87).

### Kritisch / Hoch

**Security (Multiplayer-Cheating-Vektoren):**
- **Gilden-Boss HP One-Shot-Cheat**: `database.rules.json` — `current_hp.validate` verlangte nur `>= 0`. Jetzt: `<= data.val()` wenn boss_id gleich bleibt (Damage), sonst frei (Respawn via boss_id-Wechsel). Analog `max_hp`: `== data.val()` oder boss_id-Wechsel. `damage_log` komplett entfernt (tote Rule, Code nutzt `guild_boss_damage/*/$uid`).
- **Gilden-Forschung beliebig abschliessbar**: `guild_research/*/$researchId` hatte keine `.validate`. Jetzt: `progress` monoton steigend, `completed` nur false→true, `researchStartedAt`/`completedAt` string + length-cap, `$other: validate false` gegen unerwartete Felder.
- **Gilden-Achievements ohne Validierung**: Analog Forschung — Schema-Schutz + Monotonie.

**UI Touch-Targets (Google-Play-HIG-Verstoss):**
- `DashboardView.axaml:595` QuickJob-Start-Button: `Padding="10,4"` ohne MinHeight (~22dp) → `Padding="12,8" MinHeight="44"`
- `Dialogs/ConfirmDialog.axaml:54` Prestige-Tier-Chips: `MinHeight="0"` ueberschrieb globalen Button.Text-Style → auf `MinHeight="44"` korrigiert

**Balancing (Late-Game-Climax):**
- `AscensionPerk.cs`: Level-3-Kosten der 4 teuersten Perks reduziert (eternal_tools 8→5, quick_start 7→5, start_capital 6→5, golden_era 6→5). Erste Ascension (~8 AP) kann jetzt einen Max-Perk von timeless_research/legendary_reputation voll durchziehen. Gesamt-AP 61→54 (breite Kauf-Strategie dominiert weiterhin).

**Bugs aus User-Reports:**
- **Multi-Task-Order: nur erster Task spielbar** — MiniGame-Views stoppten Render-Timer bei `IsResultShown=true`, bei Task-Wechsel mit gleichem Typ blieb `ActivePage` konstant → `OnDataContextChanged` feuerte nicht → Timer nie neu gestartet → View eingefroren. Fix: `BaseMiniGameViewModel.GameRestarted`-Event, gefeuert in `SetOrderId()` vor `StartGameAsync()`. Alle 10 MiniGame-Views abonnieren es und starten ihren Render-Loop neu. Zusaetzlich: `ContinueCommand` hat Reentrancy-Guard `if (!IsResultShown) return;` gegen Doppel-Tap-Race.
- **Gilden-Mitglieder verschwinden, eigener Spieler in Liste unsichtbar** — `IGuildService.UpdateLastActiveAsync()` war deklariert aber nirgends aufgerufen → `LastActiveAt` nur beim Beitritt gesetzt → nach 30 Tagen filtert `IsStaleMember()` alles. Fix: Keep-Alive-Call am Anfang von `RefreshGuildDetailsAsync()` + explizite `isSelf`-Guards in Duplikat- und Stale-Filter (eigener UID nie gefiltert).

### Mittel

**Security (Play-Store-Compliance UGC):**
- Gilden-Name / Spielername ohne ProfanityFilter: `CreateGuildAsync` und `SetPlayerName` rufen jetzt `ProfanityFilter.Clean` nach Unicode-Format-Filterung auf. Firebase-Rules fuer `guilds/$guildId/name` + `description` + `icon` + `color` mit Length-/Typ-Validation ergaenzt.
- Tote Pfade `friends/`, `friend_requests/`, `gifts/` aus `database.rules.json` entfernt (keine Code-Nutzung, nur Gift-Model-Stub existiert).

**Health (Tote Abstraktion):**
- `IGameCurrencyService` / `IGameWorkshopService` / `IGameOrderService` ersatzlos geloescht (213 Zeilen, 0 externe Injektionen). Inhalt in `IGameStateService` direkt integriert.
- 5 `Raise*`-Methoden aus `IGameStateService` + `GameStateService.Orders.cs` entfernt (0 externe Aufrufer, Service-Extraktion-Vorbereitung die nie eintrat).

**Game-Design (Economy):**
- `Equipment.ShopPrice` 5/15/30/60 GS → 3/8/18/40 GS. F2P-Spieler (~35 GS/Tag) kann jetzt Epic-Teile in ~1 Tag kaufen statt 2 Tage reines Farming.
- Premium-GS-Verdopplung jetzt im Shop-Live-Compare sichtbar: `UpdatePremiumIncomeComparison` fuegt zweite Zeile `PremiumBenefitGoldenScrews` hinzu (psychologisch staerkster Kaufgrund fuer Rebirth-Ziel-Spieler).

### Niedrig

- `AchievementsView.axaml:167`: `Description`-TextBlock mit `TextWrapping="Wrap" MaxLines="2" TextTrimming="CharacterEllipsis"`
- `GuildView.axaml:174`: `"OK"` → `{loc:Translate Confirm}`
- `StatisticsView.axaml:815`: `"Lv."` → `{loc:Translate LevelPrefix}`
- Starter-Offer Level-Gate: 15 → 10 (nach Plumber-Unlock-Shift auf Lv5). Sweet-Spot: Plumber etabliert, VOR Electrician-Schock bei Lv15 (250k EUR).

### Geaenderte Dateien
- `database.rules.json` (3 Security-HOCH + Name-Validate + tote Pfade)
- `Services/GuildService.cs` (ProfanityFilter + CreateGuild-Length / SetPlayerName)
- `Services/Interfaces/IGameStateService.cs` (neu geschrieben, Sub-Interfaces inline + Raise* entfernt)
- `Services/Interfaces/IGameCurrencyService.cs` + `IGameWorkshopService.cs` + `IGameOrderService.cs` — GELOESCHT
- `Services/GameStateService.Orders.cs` (Raise*-Methoden entfernt, 40 Zeilen raus)
- `ViewModels/MiniGames/BaseMiniGameViewModel.cs` (GameRestarted-Event + Continue-Reentrancy-Guard)
- Alle 10 `Views/MiniGames/*.axaml.cs` (GameRestarted-Subscribe/Unsubscribe + OnGameRestarted-Handler)
- `Views/DashboardView.axaml` (QuickJob-Button Touch-Target)
- `Views/Dialogs/ConfirmDialog.axaml` (Prestige-Chip Touch-Target)
- `Views/AchievementsView.axaml` (Description TextWrapping)
- `Views/GuildView.axaml` (Confirm-Key)
- `Views/StatisticsView.axaml` (LevelPrefix-Key)
- `Models/AscensionPerk.cs` (4 Perks L3-Kosten reduziert)
- `Models/Equipment.cs` (ShopPrice Rebalancing)
- `ViewModels/ShopViewModel.cs` (Premium-GS-Compare)
- `ViewModels/MainViewModel.Init.cs` (Starter-Offer Lv15→10)

### Offen (nicht gefixt, bewusste Entscheidung)
- **DashboardView.axaml.cs 802 Zeilen**: Gesture-State-Machine extrahieren wuerde ~150 Zeilen in `Helpers/WorkshopCardGestureRecognizer.cs` verschieben. Reiner Refactor ohne Bug-Fix, in separater Session.
- **UI-Polish "PP"/"VS"/"+20%" hardcodiert**: Universelle Abkuerzungen, minimaler Lokalisierungs-Impact.

---

## Review-Fixes (18.04.2026, Multi-Agent-Review)

Fünfter umfassender Review (7 Agenten parallel: code-review, mvvm-auditor, health, game-audit, performance, security, ui). Gesundheits-Score 87/100. Keine Release-Blocker, aber 2 KRITISCHE Bugs (Premium-Kundenverlust + State-Race). Alle 11 Tasks gefixt.

### Kritisch
- **Premium +50% Crafting-Bonus fehlte** (`IncomeCalculatorService.CalculateCraftingSellMultiplier`). Fix vom 06.04.2026 hatte Premium-Kunden-Wert von bis zu 10.000 EUR/Crafting-Item gekostet. Bonus jetzt konsistent in `CalculateCraftingSellMultiplier` (Crafting-Verkäufe durchlaufen NICHT `CalculateGrossIncome`).
- **DoRebirth state.Money-Race** (`RebirthService.DoRebirth`). Alte Logik: `state.Money -= moneyCost` ohne atomaren Check — konnte durch TaxAudit/WorkerStrike zwischenzeitlich negative Money erzeugen. Fix: `TrySpendMoney` mit Goldschrauben-Rollback bei false.

### Hoch
- **LuckySpin Timer-Leak im Catch**: `OnSpinTick` catch-Block stoppte Timer nicht → Endlos-Exception-Schleife nach erster Exception. Fix: Timer stoppen + Event unsubscriben + `IsSpinning=false` im catch.
- **SaveAsync UI-Thread-Freeze (50-100ms alle 30s)**: `JsonSerializer.Serialize` blockierte UI-Thread. Fix: Serialize auf Background-Thread via `Task.Run` + `IGameStateService.ExecuteWithLock` (GameLoop wartet max ~100ms alle 30s, UI bleibt flüssig). cloudLevel unter Lock snapshotten gegen Level-Up-Race.
- **MainView RenderTimer lief bei App-Pause weiter** (Battery-Drain): Neues Event `MainViewModel.PauseStateChanged` feuert bei PauseGameLoop/ResumeGameLoop. MainView abonniert, stoppt/startet Timer — 0% Hintergrund-CPU bei minimierter App.

### Security
- **Firebase Rules verschärft** (`database.rules.json`):
  - `player_invites/$playerId/$guildId`: Write nur wenn Absender Gildenmitglied der angegebenen Gilde ist (Spam-Schutz). Löschen nur durch Empfänger.
  - `invite_code_to_guild/$code`: Überschreiben nur durch Gildenmitglieder der referenzierten Gilde (Code-Takeover-Schutz).
  - `guild_invite_codes/$guildId`: Überschreiben nur durch Gildenmitglieder.
  - `guild_wars/$warId`: Read+Write nur für Teilnehmer-Gilden-Mitglieder (via `guildAId`/`guildBId`-Lookup).
  - `guild_invites`-Rule entfernt (Dead-Rule, nirgends im Code verwendet).

### UI
- **Touch-Targets global gefixt**: `Button.Text`-Style in App.axaml hat jetzt `MinHeight=44, MinWidth=44` (iOS/Android HIG). Fixt 8 MiniGame-Cancel-Buttons + AscensionView-Back-Button + Tutorial-Info-Buttons in einem Schlag.

### Balancing (Game-Audit)
- **Plumber-Unlock früher** (Day-1-Retention): 12.000 EUR / Level 8 → 5.000 EUR / Level 5. Erster neuer Workshop-Unlock in <15min statt 30-45min.
- **MasterSmith Rang-Konsistenz**: 500M → 10 Mrd (war 50× billiger als GeneralContractor 25 Mrd — fühlte sich wie Copy-Paste-Bug an). InnovationLab entsprechend 5 Mrd → 50 Mrd.
- **Silver-Prestige-Bonus**: +25% → +35% (Weber-Gesetz-Schwelle überschreiten, zweiter Prestige muss spürbar sein).

### Dokumentation
- CLAUDE.md Version-Header v2.0.29 → v2.0.30 (war seit Release stale).
- `App.axaml.cs:138` Splash-Fallback v2.0.22 → v2.0.30 (8 Releases hinter).
- MVVM-Audit: `MainWindow.axaml` Compiled Bindings ergänzt (Score: 87/100).

### Geänderte Dateien
- `Services/IncomeCalculatorService.cs` (Premium-Bonus in `CalculateCraftingSellMultiplier`)
- `Services/RebirthService.cs` (TrySpendMoney mit Goldschrauben-Rollback)
- `Services/SaveGameService.cs` (Serialize auf Background-Thread via ExecuteWithLock)
- `ViewModels/LuckySpinViewModel.cs` (Timer-Cleanup im catch-Block)
- `ViewModels/MainViewModel.cs` (PauseStateChanged-Event)
- `Views/MainView.axaml.cs` (PauseStateChanged-Handler → Render-Timer-Start/Stop)
- `Models/Enums/WorkshopType.cs` (Plumber + MasterSmith + InnovationLab Balancing)
- `Models/Enums/PrestigeTier.cs` (Silver-Prestige +35%)
- `App.axaml` (Button.Text Touch-Target-Mindestgröße 44dp)
- `App.axaml.cs` (Splash-Fallback-Version)
- `database.rules.json` (Firebase Rules Anti-Griefing)

## Review-Fixes (17.04.2026, Release v2.0.30 — Phase 1 + Architektur)

Vierter umfassender Review. Findings von `code-review`, `health`, `game-audit`, `mvvm-auditor`. 7 Kategorien adressiert.

### Release-Blocker (Kritisch/Hoch)
- **Worker.AssignedWorkshop Gotcha wiederholt**: `PrestigeService.RestoreKeptWorkers` (3 Worker-Add-Pfade), `AscensionService.cs:132`, `GameStateService.Workshop.cs:162 (HireWorker)`. Fix: `Worker.CreateForTier(tier, WorkshopType? assignedWorkshop = null)` mit optionalem Parameter, alle Call-Sites mit bekanntem Workshop nutzen ihn. `GameState.CreateNew()` vereinfacht (Inline statt +=). Bugfix verhindert IsWorking=false → keine Fatigue/Einkommen nach Prestige/Ascension/Hire zur Laufzeit.
- **SaveGameService.ImportSaveAsync ohne IO-Lock**: GameLoop konnte zwischen `Initialize(state)` und `SaveAsync()` ticken und importierten State überschreiben. Fix: `SaveInternalAsync()` extrahiert (lock-frei), `SaveAsync()` + `ImportSaveAsync()` halten `_ioLock` für den gesamten Pfad. Idempotent.
- **Cloud-Save PlayerLevel-Race**: `state.PlayerLevel` wurde INNERHALB `Task.Run` gelesen → konnte von GameLoop zwischen Serialize und Cloud-Save hochgezogen werden. Fix: `int cloudLevel = state.PlayerLevel;` VOR `Task.Run` extrahiert. Zusätzlich `cloudSvc` als lokale Kopie gegen null-Race.
- **Mini-Game `async void` Timer-Ticks** (10 ViewModels): Ungefangene Exceptions in DispatcherTimer-async-void-Handlern würden den Prozess zerreißen. Fix: `protected abstract void OnGameTimerTick` → `protected abstract Task OnGameTimerTickAsync` in BaseMiniGameViewModel. Neuer Wrapper `private async void HandleTimerTick` mit try/catch + Timer-Stop bei Fehler. Alle 10 VMs (Wiring/RoofTiling/PipePuzzle/Painting/Invent/Inspection/Blueprint/DesignPuzzle/Sawing/Forge) migriert. Sawing+Forge sync bleiben sync aber mit `return Task.CompletedTask`.
- **DisposeServices Silent-Leak-Risiko**: `App.axaml.cs:191-240` hatte 13 hardkodierte Dispose-Aufrufe → neue IDisposable-Services vergessen. Fix: Kritische Services (GameLoopService, GameJuiceEngine) weiter explizit (Reihenfolge), danach `Services as IDisposable`.Dispose() kaskadiert ALLE registrierten IDisposable-Singletons. Idempotent via `_servicesDisposed`-Flag.

### Architektur
- **IGuildFacade**: Neues Service-Container-Interface buendelt 9 Gilden-Services (IGuildService/Invite/Research/Chat/WarSeason/Boss/Hall/Tip/Achievement) über Properties. GuildViewModel-Ctor von 14 auf 7 Parameter reduziert. Service-Container-Pattern (keine Methoden-Delegation) — Code bleibt semantisch identisch: `_guildService.X` → `_facade.Guild.X`. Sub-VMs (WarSeason/Boss/Hall) bleiben unveraendert (eigene DI). Facade ist Singleton, disposed die inneren Services NICHT (DI-Container-Ownership).
- **CloseWorkerProfileCommand**: Backdrop-Klick im `WorkerProfileDialog` rief `MainViewModel.HandleBackPressed()` auf (überdimensioniert + semantisch falsch). Dedizierter `[RelayCommand] CloseWorkerProfile` auf MainViewModel. Code-Behind ruft diesen statt HandleBackPressed — keine Seiten-Effekte auf andere Overlays.
- **AscensionView `Text="MAX"`** → `{loc:Translate PerkMaxLevel}` (Key existierte in allen 6 RESX, nur Binding fehlte).

### Balancing (Game-Audit)
- **TrainingCenter-Speed Late-Game**: `TrainingCenterSpeedPerLevel` 0.5 → 1.0 (GameBalanceConstants). Bei Building-Level 5: 2.5x → 6.5x. Building.cs referenziert jetzt die Konstante (vorher hardkodiert). Behebt Late-Game-Frust: Training von High-Tier-Workers war vs. Idle-Einkommen unrentabel.
- **MaterialOrdersPerDay 3 → 5** (GameBalanceConstants). Power-User-Beschwerde: Lieferaufträge (einzige MiniGame-freie Content-Sekunde) zu stark limitiert.

### Architektur Phase 2 (Foundation + Navigation-Umbau) — UMGESETZT
- **INavigationService + NavigationService** (398 Zeilen): Zentrale Navigations-API. `NavigateToRoute(string)` übernimmt die alte `MainViewModel.OnChildNavigation`-Logik (Stack-Push, Route-Parsing, Deep-Links). Alle `SelectXxxTab()`-Methoden hier, MainViewModel behält nur `[RelayCommand]`-Wrapper.
- **IDialogOrchestrator + DialogOrchestrator**: Koordiniert Dialog-Dismiss-Kaskade für Back-Press (1:1 identische Reihenfolge wie vorheriger `HandleBackPressed`).
- **IMiniGameNavigator + MiniGameNavigator**: MiniGame-Route-Mapping (10 Routen), QuickJob/Tournament-Abbruch-Bestätigung, `ActiveMiniGameViewModel`-Lifecycle.
- **INavigationHost**: Host-Ref-Interface. MainViewModel implementiert explizit. Services greifen auf MainViewModel-Properties nur über diesen Contract zu (unit-testbar, Mock-Host möglich). Neuer Partial: `MainViewModel.Host.cs` (115 Zeilen).

Nebeneffekt: `MainViewModel.Navigation.cs` von 561 auf 243 Zeilen reduziert. MainViewModel.cs stabil bei 2303 Zeilen.

### Architektur Phase 3 (Feature-VMs) — UMGESETZT
Alle 4 Feature-VMs sind Source-of-Truth und über DI auf MainViewModel verdrahtet. MainViewModel
exponiert sie als Properties (`HeaderVM`, `PrestigeBannerVM`, `GoalBannerVM`, `WelcomeFlowVM`) UND
behält Delegate-Properties für alle fruehren [ObservableProperty]-Felder. AXAML-Bindings funktionieren
damit unveraendert weiter (`{Binding Money}` → MainViewModel.Money Property → delegiert an HeaderVM.Money).
Property-Änderungen werden via `HeaderVM.PropertyChanged`-Forward an MainViewModel propagiert,
inkl. NotifyPropertyChangedFor-Effekte (ShowCraftingResearch, ShowManagerSection, QuickAccessColumns etc.).

- **HeaderViewModel** (KOMPLETT): Source-of-Truth für 16 Properties (Money, MoneyDisplay,
  IncomePerSecond, IncomeDisplay, NetIncomeHeaderDisplay, IsNetIncomeNegative, NetIncomeColor,
  WorkerWarningText, HasWorkerWarning, IsSoftCapActive, SoftCapText, PlayerLevel, CurrentXp,
  XpForNextLevel, LevelProgress, GoldenScrewsDisplay). Plus Vorbereitung für Prestige-Badge/Boost/Rush/Delivery (Properties vorhanden, Sync noch nicht verdrahtet — für zukuenftige Erweiterungen).
- **PrestigeBannerViewModel** (KOMPLETT): Source-of-Truth für 18 Properties (IsPrestigeAvailable,
  PrestigePointsPreview, PrestigePreviewGains/Losses/SpeedUp/TierName, HasNextPrestigeTier,
  ActiveChallengeCount, ActiveChallengesText, 6× IsChallengeXxxActive, CurrentRunDuration,
  NextPrestigeTierHint, NextPrestigeTierProgress).
- **GoalBannerViewModel** (KOMPLETT): Eigenständige Klasse mit CurrentGoal-Props + NavigateToGoalCommand.
  IGoalService + INavigationService injiziert. DashboardView.axaml Bindings auf `GoalBannerVM.X` umgestellt.
- **WelcomeFlowViewModel** (KOMPLETT): Source-of-Truth für 13 Properties (IsCombinedWelcome*,
  IsStarterOffer*, IsOfflineEarnings*, IsDailyRewardDialog*, alle zugehoerigen Text-Felder).

**Forward-Pattern** im MainViewModel-Ctor (nur Side-Effects für computed Properties):
```csharp
HeaderVM.PropertyChanged += (_, e) => {
    if (e.PropertyName == nameof(HeaderViewModel.PlayerLevel)) {
        OnPropertyChanged(nameof(ShowCraftingResearch));
        OnPropertyChanged(nameof(ShowManagerSection));
        /* ... */
    }
};
```

**Migration der Code-Konsumenten** (Phase 3 Final):
- `EconomyFeatureViewModel`: `_host.Money` → `_host.HeaderVM.Money`, `_host.IsPrestigeAvailable` → `_host.PrestigeBannerVM.IsPrestigeAvailable` etc.
- `MainViewModel.Init.cs`: `IsOfflineEarningsDialogVisible = X` → `WelcomeFlowVM.IsOfflineEarningsDialogVisible = X`
- `MainViewModel.cs` intern: `Money = X` → `HeaderVM.Money = X`, `PlayerLevel = X` → `HeaderVM.PlayerLevel = X`
- Views Code-Behind (`DashboardView.axaml.cs`, `MainView.axaml.cs`): `_vm.GoldenScrewsDisplay` → `_vm.HeaderVM.GoldenScrewsDisplay`

### Architektur Phase 4 (ViewLocator-Migration) — UMGESETZT
MainView.axaml hatte 9 direkte `<guild:XxxView DataContext=...>`-Instanzen (Z.160-186).
Diese sind nun auf `<ContentControl Content="{Binding GuildViewModel.XxxVM}">` + ViewLocator
umgestellt (BingXBot-Pattern).

**9 Guild-Sub-VMs im Namespace `HandwerkerImperium.ViewModels.Guild`:**
- `GuildWarSeasonViewModel`, `GuildBossViewModel`, `GuildHallViewModel` — bestehende VMs
  (Namespace von `ViewModels` nach `ViewModels.Guild` verschoben für ViewLocator-Konvention
  `ViewModels.Guild.X → Views.Guild.X`).
- `GuildResearchViewModel`, `GuildMembersViewModel`, `GuildInviteViewModel`,
  `GuildAchievementsViewModel`, `GuildChatViewModel`, `GuildWarViewModel` — 6 neue Thin-Wrapper-VMs
  mit `Guild` Property auf das Parent-GuildViewModel. Alle Bindings in den 6 Views wurden mit
  `{Binding Guild.X}` Prefix versehen.

**ViewLocator-Konvention:**
`HandwerkerImperium.ViewModels.Guild.GuildResearchViewModel` → Replace `.ViewModels.` mit `.Views.`
→ `HandwerkerImperium.Views.Guild.GuildResearchView`.

**Thin-Wrapper-Pattern** (Beispiel):
```csharp
namespace HandwerkerImperium.ViewModels.Guild;
public sealed class GuildResearchViewModel : ViewModelBase
{
    public GuildViewModel Guild { get; }
    public GuildResearchViewModel(GuildViewModel guild) { Guild = guild; }
}
```
AXAML: `<TextBlock Text="{Binding Guild.GuildResearchSummary}" />`

**Wiring:** GuildViewModel erstellt die 6 Thin-Wrapper im Ctor (`new GuildResearchViewModel(this)`).
Zirkuläre DI vermieden (keine DI-Registrierung der Thin-Wrapper).

### Architektur-Roadmap (verbleibend — rein kosmetisch)
- **Partials-Mergen**: MainViewModel.Navigation.cs (243 Zeilen) + Dialogs.cs (94 Zeilen) können
  in MainViewModel.cs inline werden. Funktional aequivalent, rein kosmetisch, widerspricht der
  angestrebten Zerlegungsrichtung → NICHT empfohlen.
- **Dialog-Controls → ViewLocator**: `<dialogs:AchievementDialog DataContext="{Binding DialogVM}">`
  etc. könnten analog zu Guild-Sub-VMs als Thin-Wrapper-Pattern migriert werden. Aktuell korrekt
  (Dialoge sind inline-UserControls mit DialogVM als Host), nur Stil-Inkonsistenz mit BingXBot.

### Neue Dateien
- `Services/Interfaces/IGuildFacade.cs` — Service-Container-Facade
- `Services/GuildFacade.cs` — Impl (Pass-Through)
- `Services/Interfaces/INavigationService.cs` + `NavigationService.cs` (398 Zeilen)
- `Services/Interfaces/IDialogOrchestrator.cs` + `DialogOrchestrator.cs`
- `Services/Interfaces/IMiniGameNavigator.cs` + `MiniGameNavigator.cs`
- `Services/Interfaces/INavigationHost.cs`
- `ViewModels/MainViewModel.Host.cs` — Partial mit INavigationHost-Implementierung
- `ViewModels/GoalBannerViewModel.cs` — komplett migrierte Feature-VM (IGoalService + INavigationService)
- `ViewModels/HeaderViewModel.cs` — Source-of-Truth für 16 Dashboard-Header-Properties
- `ViewModels/PrestigeBannerViewModel.cs` — Source-of-Truth für 18 Prestige-Banner-Properties
- `ViewModels/WelcomeFlowViewModel.cs` — Source-of-Truth für 13 Welcome-Flow-Properties
- `ViewModels/Guild/GuildResearchViewModel.cs`, `GuildMembersViewModel.cs`, `GuildInviteViewModel.cs`,
  `GuildAchievementsViewModel.cs`, `GuildChatViewModel.cs`, `GuildWarViewModel.cs` — 6 Thin-Wrapper-VMs
  für ViewLocator-Mapping (Phase 4)
- `ViewModels/Guild/GuildWarSeasonViewModel.cs`, `GuildBossViewModel.cs`, `GuildHallViewModel.cs` —
  bestehende Sub-VMs, verschoben nach ViewModels.Guild Namespace (Phase 4)

## Review-Fixes (06.04.2026, 25 Findings)

Umfassender Review durch 8 spezialisierte Agenten (Code, Game-Design, UI, Performance, SkiaSharp, Security, Lokalisierung, Health).

### Kritisch + Hoch
- **ActivateRush**: `async void` → `async Task` (Crash-Risiko bei Ad-Fehler behoben)
- **Premium-Bonus +50%**: In `CalculateGrossIncome()` ergänzt (fehlte für Online UND Offline). Doppel-Bonus in `CalculateCraftingSellMultiplier()` entfernt
- **LuckySpinService.Spin()**: Return-Typ nullable (`LuckySpinPrizeType?`), null bei fehlgeschlagenem GS-Kauf statt stiller MoneySmall-Rückgabe
- **ForgeGameRenderer**: 4 SKPath-Objekte (`_anvilBodyPath` etc.) in `Dispose()` ergänzt
- **Firebase Rules**: `database.rules.json` als Referenz-Datei im Repo. READ-Rules für `guild_members`, `guild_research`, `guild_bosses` etc. auf Mitgliedschafts-Prüfung verschärft (analog guild_chat)

### UI-Fixes
- **ResearchView**: Bottom-Margin 0→84dp (Content hinter Tab-Bar), Back-Button MinHeight/MinWidth 44, TabCanvas Height 44→48, Overlay-Buttons MinHeight 48
- **ShopView**: Back-Button MinHeight/MinWidth 44
- **PaintingGameView**: Tutorial-Button MinHeight/MinWidth 44 (Touch-Target)
- **WorkerProfileView**: Bottom-Sheet Margin 84→24dp (kein Tab-Bar im Sheet)
- **DashboardView**: BannerStrip AutomationId, Saison-Chip im Header (GAM-4)

### Performance
- **MiniGame Render-Loop**: Alle 10 Views stoppen 30fps Timer bei `IsResultShown` (statisches Ergebnis braucht keine Animation)

### Balancing
- **Lieferant GS-Drop**: 1-3 → 2-5 GS (bedeutsamer als Belohnung)
- **Glücksrad Jackpot**: Gewicht 1→2 (2% statt 1%, motivierender)
- **SoloMeister Challenge**: PP-Bonus 60%→50% (dominierte Meta)
- **Spartaner Challenge**: PP-Bonus 40%→45% (näher an SoloMeister für Diversität)
- **Bronze-Prestige Speed-Boost**: 30min→15min (verhindert Rush-Stacking zu 4-6x)
- **Saison-Chip**: SeasonalModifierText im Dashboard-Header sichtbar (war unsichtbar für Spieler)

### Code-Qualität + Security
- **GetPrestigeIncomeBonus**: Aus 2 Duplikaten (OfflineProgress+Crafting) in `IncomeCalculatorService` zentralisiert
- **AscensionService**: Leeren StateLoaded-Handler entfernt (No-Op)
- **AutoAssign XML-Doc**: Kommentar korrigiert ("Reaktiviert ruhende Worker" statt "Weist idle Worker zu")
- **ProfanityFilter**: Neuer `Helpers/ProfanityFilter.cs` für Gilden-Chat (Blacklist DE/EN/ES/FR/IT/PT, Play Store Compliance)
- **Spielernamen Unicode-Filter**: Zero-Width-Characters und Format-Zeichen in `SetPlayerName()` entfernt
- **`.gitignore`**: `**/google-services.json` hinzugefügt

### Neue Dateien
- `Helpers/ProfanityFilter.cs` — Profanity-Filter für Chat + Namen
- `database.rules.json` — Firebase Security Rules Referenz-Datei

## UX-Verbesserungen (03.04.2026, 16 Fixes)

Basierend auf umfassender UX-Analyse (17 Findings in 10 Kategorien).

### Geänderte Dateien

| Datei | Änderung |
|-------|----------|
| `Views/ImperiumView.axaml` | Challenge-Labels lokalisiert (CON-2), aktiv/inaktiv-Styling (PP-2), Prestige Quick-Access (IA-1), Speedrun+NextTier komprimiert (CL-2) |
| `Views/DashboardView.axaml` | MasterTools-Badge entfernt (CL-1), Netto nur bei Verlust sichtbar (CL-1) |
| `Views/MissionenView.axaml` | QuickJobs-Dopplung entfernt (IA-2), Statistiken-Button im Wettbewerbe-Tab (IA-3/PD-2) |
| `Views/Dashboard/AutomationPanel.axaml` | 2x2 Grid statt 4 Spalten (PD-3/TOUCH-2) |
| `Views/Dashboard/BannerStrip.axaml` | Fade-Edge Scroll-Indikator rechts (TOUCH-3) |
| `ViewModels/MainViewModel.cs` | 6 Challenge-Active Properties, OrderExpired-Feedback (FB-1) |
| `ViewModels/MainViewModel.Economy.cs` | RefreshChallengeDisplay mit Active-States, Reputation-Hint (ONB-2), Auftragstypen-Hint (ONB-1) |
| `ViewModels/MainViewModel.Navigation.cs` | MiniGame-Abbruch-Warnung mit ConfirmDialog (NAV-2) |
| `Models/LevelThresholds.cs` | Turnier Lv35, Events Lv45, BattlePass Lv55 (PD-1) |
| `Models/ContextualHint.cs` | Neue Hints: OrderTypes, ReputationHint |
| `Converters/BoolToChallengeBackgroundConverter.cs` | Neuer Converter für Challenge-Chip-Hintergrund |

### Neue RESX-Keys

`HintOrderTypesTitle`, `HintOrderTypesText`, `HintReputationTitle`, `HintReputationText`, `MiniGameAbortTitle`, `MiniGameAbortMessage`, `MiniGameAbortConfirm`, `OrderExpiredNotification`, `StatisticsTitle`, `ChallengeChip_*` (6 Keys)

## Farbkonsistenz (Craft-Palette)

- **Buttons**: Immer Craft-Orange/Braun via App.axaml Style-Overrides (keine `{DynamicResource PrimaryBrush}`)
- **Workshop-Farben**: Carpenter=#A0522D, Plumber=#0E7490, Electrician=#F97316, Painter=#EC4899, Roofer=#DC2626, Contractor=#EA580C, Architect=#78716C, GeneralContractor=#FFD700, MasterSmith=#D4A373, InnovationLab=#6A5ACD
- **Tier-Farben**: F=Grau, E=Grün, D=#0E7490, C=#B45309, B=Amber, A=Rot, S=Gold
- **Branch-Farben**: Tools=#EA580C, Management=#92400E, Marketing=#65A30D
- **Feature-Farben** (App.axaml): Tournament=#DC2626, SeasonalEvent=#059669, BattlePass=#7C3AED, MasterSmith=#B91C1C, InnovationLab=#6D28D9
- **Overlay-Farben**: DialogOverlay=#AA000000 → `DialogOverlayBrush`, RewardOverlay=#CC000000 → `RewardOverlayBrush`
- **Semantische Farben**: `SuccessBrush` (#22C55E), `ErrorBrush` (#EF4444), `WarningBrush` (#F59E0B) - alle in AppPalette.axaml definiert
- **Kontrast-Farbe**: `PrimaryContrastBrush` (#FFFFFF) statt `Foreground="White"` in allen Views
- **Hardcodierte Farben**: Alle in ~35 Views durch DynamicResource/StaticResource ersetzt. Ausnahme: Alpha-Kanal-Hintergründe (#20D97706 etc.), GradientStops, Opacity-Varianten (#FFFFFFCC) und SkiaSharp-Code bleiben hardcodiert
- **AppPalette bereinigt** (03.04.2026): ZeitManager/Rechner-Reste entfernt (TimerAccentColor, StopwatchAccentColor, AlarmAccentColor, PomodoroAccentColor + Brushes, EqualsGradientBrush, DisplayGradientBrush, OperatorGlowShadow, DigitButtonBrush, DigitButtonHoverBrush)
- **Lokalisierung bereinigt** (03.04.2026): Hardcodierte Strings "Max", "Lv.", "Mini-Games", 6 Prestige-Challenge-Texte durch `{loc:Translate}` mit bestehenden/neuen RESX-Keys ersetzt (LevelPrefix, MiniGamesLabel, PerkMaxLevel, Challenge_*)
- **Button RenderTransform** (03.04.2026): Initialer `RenderTransform="scale(1)"` im globalen Button-Style (App.axaml) gesetzt — verhindert Crash auf Android bei null→scale()-Transition
- **Touch-Target** (03.04.2026): WorkshopView Spezialisierung-entfernen-Button von MinHeight=36 auf 44dp erhöht
- **BattlePassView** (03.04.2026): ListBox von StackPanel- in Grid-Container verschoben für korrekte Virtualisierung
- **80dp Bottom-Spacer entfernt** (03.04.2026): Alle unnötigen Ad-Banner-Spacer aus 18 Views entfernt (kein Banner-Ad in HandwerkerImperium). Margin 80→16dp, Border-Height-80-Spacer komplett gelöscht

## Visual Upgrade (Phase 0-3, deployt)

AI-generierte Stylized-Cartoon-Hintergründe via ComfyUI + DreamShaper XL / Juggernaut-X. Reines Bitmap-Rendering: AI-Hintergrund (DrawBitmap) + minimale Overlays (Level-Effekte, Wetter). Kein prozeduraler Fallback.

- **Status:** 47 Assets deployt (~6.2 MB WebP), Shared+Android Build OK
- **GameAssetService** (IGameAssetService): LRU-Cache 50MB, WebP→SKBitmap + animierte WebP Multi-Frame-Decodierung, PlatformAssetLoader
- **Animations-API**: `GetAnimationFrames(path)` / `LoadAnimationAsync(path, targetW, targetH)` — decodiert animierte WebP (SKCodec), skaliert Frames, LRU-Cache
- **Assets:** `Assets/visuals/{city,workshops,workers,minigames,meister_hans,splash}/` (WebP, quality 85)
- **Animierte Assets (Phase 3):** `Assets/visuals/workshops/animated/*.webp` (10 Workshop-Animationen, je 16 Frames @ 8fps, ~4.6 MB) + `Assets/visuals/city/animated/city_background.webp` (~316 KB)
- **AnimateDiff**: SDXL Motion Model (mm_sdxl_v10_beta.ckpt), motion_scale 0.4 für stabile Frames
- **Checkpoints**: DreamShaper XL (Umgebungen), Juggernaut-X v10 (Personen-Portrait-Szenen)
- **Workshop-Karten**: `WorkshopGameCardRenderer` zeigt animierte Frames im Header (8fps Loop, Fallback auf statisch)
- **City-Header**: `CityRenderer` zeigt animierten Hintergrund (8fps Loop, skaliert auf 512x192, Fallback auf statisch)
- **Shared csproj:** `<AvaloniaResource Include="Assets\**" />` (Wildcard)
- **Android csproj:** `<AndroidAsset Include="..\..\Shared\Assets\visuals\**\*.webp" Link="..." />`
- **Generierungs-Script**: `F:\AI\ComfyUI_workflows\handwerkerimperium\generate_animated_scenes.py`
- **Erledigt:** Prozedurale Hintergründe komplett entfernt (18.03.2026). WorkshopSceneRenderer: 2570→200 Zeilen, CityRenderer: 1297→352 Zeilen

## IsBusy-Pattern

`private bool _isBusy` + try/finally Guard in GuildVM, SettingsVM, ShopVM, WorkerMarketVM für alle async-Methoden.

## Daily Challenge Tracking

- `MiniGameResultRecorded` Event auf `IGameStateService` → `DailyChallengeService` subscribt
- Score-Mapping: Perfect=100%, Good=75%, Ok=50%, Miss=0%

## Reputation-System

- **CustomerReputation** (0-100, Start 50): Beeinflusst Auftragsbelohnungen (0.7x-1.5x)
- **AddRating()** bei Auftragsabschluss (MiniGame-Rating → 1-5 Sterne)
- **Showroom-Gebäude**: Passive Reputation-Steigerung (0.5-2.5/Tag)
- **DecayReputation()**: Langsamer Abbau >50 (1/Tag)
- **ExtraOrderSlots**: >=70 → +1, >=90 → +2
- **OrderQualityBonus**: <30 → -10%, >=80 → +20%

## Auftragstypen (OrderType)

| Typ | Freischaltung | Belohnung | Besonderheit |
|-----|---------------|-----------|-------------|
| Standard | Immer | 1.0x | Basis |
| Large | WS-Level 10+ | 1.8x | Mehr Aufgaben |
| Cooperation | WS-Level 15+, >=2 Workshops | 2.5x | Gemischte Aufgaben |
| Weekly | WS-Level 20+ | 3.0x | 7-Tage-Deadline (BAL-14: von 2.5 auf 3.0, eigene Identität vs. Cooperation) |
| MaterialOrder | WS-Level 50+ | 1.8x | Kein MiniGame, Items liefern, 4h Deadline, max 3/Tag |

- **Stammkunden**: 20% Chance, BonusMultiplier 1.1-1.5x, max 20
- **Abgelaufene Orders**: GameLoop prüft alle 60 Ticks
- **Lieferaufträge**: Erfordern Crafting-Items aus Inventar, keine MiniGames, sofortige Belohnung. Cross-Workshop-Items ab Spieler-Level 100

## Auto-Produktions-System (NEU 21.03.2026)

Alle 10 Workshops produzieren passiv Tier-1 Items basierend auf arbeitenden Workern.

### Produktionsraten

| Workshop | Intervall | Items/h (5 Worker) |
|----------|-----------|---------------------|
| Standard (8 WS) | 180s/Worker | 100 |
| InnovationLab | 120s/Worker | 150 |
| MasterSmith | 60s/Worker (Spezialeffekt) | 300 |

**Unlock:** Workshop Level 50 (gleich wie erstes Crafting-Rezept)
**Offline:** Gleiche Staffelung wie Offline-Earnings (80%/25%/10%/3%)
**GameLoop:** Alle 180 Ticks (Offset 90), `AutoProductionService.ProduceForAllWorkshops()`

### Skalierende Verkaufspreise

Formel: `BaseValue × (1 + log₂(1 + Level/25)) × CraftingSellMultiplier`

CraftingSellMultiplier = Prestige × PrestigeShop × Research × Events × MasterTools × Gilden × VIP × Rebirth × Premium (KEIN Soft-Cap, KEIN Speed/Rush)

| Level | Level-Multi | Beispiel Bretter (Basis 500€) |
|-------|------------|-------------------------------|
| 50 | ×2.0 | 1.000€ |
| 100 | ×3.3 | 1.650€ |
| 200 | ×4.2 | 2.100€ |
| 500 | ×5.4 | 2.700€ |

### 20 Rezepte (7 neue + 13 bestehende)

| Workshop | Tier-1 (Lv50) | Tier-2 (Lv150) | Tier-3 (Lv300) |
|----------|--------------|----------------|----------------|
| Schreiner | planks 500€ | furniture 2.500€ | luxury_furniture 10.000€ |
| Klempner | pipes 500€ | plumbing_system 2.500€ | bathroom_installation 10.000€ |
| Elektriker | cables 500€ | circuit 2.500€ | smart_home 10.000€ |
| Maler | paint_mix 400€ | wall_design 2.000€ | **artwork 8.000€ (NEU)** |
| Dachdecker | roof_tiles 600€ | roofing_system 3.000€ | **roof_structure 12.000€ (NEU)** |
| Bauunternehmer | **concrete 800€** | - | - |
| Architekt | **blueprint 1.000€** | - | - |
| Generalunternehmer | **contract 1.500€** | - | - |
| Meisterschmiede | **fittings 1.200€** | - | - |
| Innovationslabor | **prototype 2.000€** | - | - |

### GameBalanceConstants (Auto-Produktion)

| Konstante | Wert | Beschreibung |
|-----------|------|-------------|
| AutoProductionIntervalSeconds | 180 | Standard-Rate |
| AutoProductionInnovationLabInterval | 120 | InnovationLab |
| AutoProductionMasterSmithInterval | 60 | MasterSmith |
| AutoProductionUnlockLevel | 50 | Unlock-Level |
| CraftingSellPriceLogDivisor | 25.0 | Log₂-Skalierung |
| MaterialOrderRewardMultiplier | 1.8 | Lieferauftrags-Belohnung |
| MaterialOrdersPerDay | 3 | Max pro Tag |
| MaterialOrderDeadlineHours | 4 | Deadline |

## Event-Mechanik

- **AffectedWorkshop**: HighDemand/MaterialShortage betreffen zufälligen Workshop-Typ
- **MarketRestriction**: WorkerStrike → nur Tier C und niedriger
- **Intervall-Skalierung**: Kein Prestige 8h/30%, Bronze 6h/35%, Silver 4h/40%, Gold+ 3h/50%
- **TaxAudit**: 10% Steuer auf Brutto (dauerhaft während Event)
- **WorkerStrike**: Alle Worker-Stimmungen -20 (einmalig bei Start)
- Event-ID-Tracking verhindert doppelte Anwendung

## Gilden-Forschungssystem

Kollaboratives System: Mitglieder tragen Geld bei → gemeinsamer Fortschritt. Permanente Boni, kein Weekly-Reset.

### 18 Forschungen in 6 Kategorien

| Kategorie | ID | Kosten | Effekt |
|-----------|-----|--------|--------|
| Infrastruktur | guild_expand_1/2/3 | 50M/500M/5B | Max. Mitglieder +5/+5/+10 (20→40) |
| Wirtschaft | guild_income_1/2/3/4 | 10M-10B | +5%/+15% Einkommen, -10% Kosten, +10% Auftragsbelohnungen |
| Wissen | guild_knowledge_1/2/3 | 25M-2.5B | +10% XP, +5% Worker-Effizienz, +15% MiniGame-Belohnungen |
| Logistik | guild_logistics_1/2/3 | 75M-3B | +1 Auftragsslot, +15% Order-Qualität, +20% Auftragsbelohnungen |
| Arbeitsmarkt | guild_workforce_1/2/3 | 150M-5B | +1 Worker-Slot, +25% Training-Speed, -20% Ermüdung/Stimmung |
| Meisterschaft | guild_mastery_1/2 | 500M/7.5B | +20% Forschungs-Speed, +10% Prestige-Punkte |

**Gesamtkosten**: ~37,4 Mrd. EUR | **Linear pro Kategorie**

### Firebase-Datenstruktur

`/guild_research/{guildId}/{researchId}` → `{ progress: long, completed: bool, completedAt: string?, researchStartedAt: string? }`
`/guild_invites/{guildId}/{recipientUserId}` → `{ senderId, senderName, guildId, guildName, sentAt }`

### Effekt-Integration (14 Effekt-Typen)

Effekte über `GuildMembership`-Properties gecacht:
- **GameLoopService**: IncomeBonus, CostReduction, EfficiencyBonus, WorkerSlotBonus
- **OrderGeneratorService**: OrderSlotBonus, OrderQualityBonus, RewardBonus, XpBonus
- **WorkerService**: TrainingSpeedBonus, FatigueReduction
- **ResearchService**: ResearchSpeedBonus
- **PrestigeService**: PrestigePointBonus
- **GuildService**: MaxMembersBonus (Base=20 + Expand-Boni)

### Gilden-Dateien

#### Models
- `Models/GuildEnums.cs`: Zentrale Enums (GuildRole, GuildLeague, WarPhase, BossStatus, GuildBuildingId, GuildBossType, GuildAchievementCategory, AchievementTier)
- `Models/GuildResearch.cs`: Kategorien, Effekt-Typen, Definitionen, States, Display
- `Models/GuildWarSeason.cs`: Saison-System (GuildWarSeasonData, GuildLeagueEntry, GuildWarPlayerScore, GuildWarLogEntry, WarBonusMission, WarSeasonDisplayData)
- `Models/GuildBoss.cs`: Boss-System (FirebaseGuildBoss, GuildBossDamage, GuildBossDefinition mit 6 Bossen, GuildBossDisplayData, BossDamageEntry)
- `Models/GuildHall.cs`: Hauptquartier (GuildBuildingState, GuildBuildingCost, GuildBuildingDefinition mit 10 Gebäuden, GuildBuildingDisplay, GuildHallEffects)
- `Models/GuildAchievement.cs`: Achievements (GuildAchievementState, GuildAchievementDefinition mit 30 Achievements = 10 Typen x 3 Tiers, GuildAchievementDisplay)
- `Models/Guild.cs`: GuildMembership +14 Research-Properties + ApplyResearchEffects() + 6 Hall-Properties + ApplyHallEffects() + guildHallLevel + leagueId
- `Models/Firebase/FirebaseGuildData.cs`: +maxMembers, leagueId, leaguePoints, hallLevel, description
- `Models/Firebase/FirebaseGuildMember.cs`: +lastActiveAt, weeklyWarScore, totalWarScore
- `Models/Firebase/GuildWar.cs`: +guildALevel, guildBLevel, phase, phaseEndsAt

#### Services & Views
- `Services/GuildService.cs`: Gilden-CRUD, Wochenziele, Einladungen (SendInvite, AcceptInvite, DeclineInvite), GetMaxMembers(). Research-Logik nach GuildResearchService extrahiert
- `Services/GuildResearchService.cs`: Extrahierte Research-Logik (IGuildResearchService). GetGuildResearchAsync(), ContributeToResearchAsync() (mit Firebase-Rollback), CheckResearchCompletionAsync(), GetCachedEffects(), RefreshResearchCacheAsync(). SemaphoreSlim Thread-Safety
- `Services/GuildWarSeasonService.cs`: Saison-basierter Gilden-Krieg (Matchmaking, Scoring, Ligen-Auf/Abstieg, Bonus-Missionen)
- `Services/GuildHallService.cs`: 10 Gebäude mit Upgrade-Timer (1-12h), Kosten (GS+Gildengeld), Effekt-Cache auf GuildMembership
- `Services/GuildBossService.cs`: 6 Boss-Typen, Schadensbeitrag (Crafting/Orders/MiniGames/Donations), Spawn/Despawn-Logik, Belohnungen
- `Services/GuildTipService.cs`: Kontextuelle Tipps (Preferences-basiert, 24h Cooldown, IsBusy-Guard)
- `Services/GuildAchievementService.cs`: 30 Achievements (10 Typen x 3 Tiers), Firebase-State-Tracking, Fortschrittsberechnung
- `ViewModels/GuildViewModel.cs`: Research + Timer auto-completion, ContributeDialog, Einladungs-Inbox, nutzt IGuildResearchService
- `Views/Guild/GuildResearchView.axaml(.cs)`: 3 Renderer, 30fps, DPI-skalierter HitTest, ToList-Cache
- `Graphics/GuildResearchBackgroundRenderer.cs`: Pergament + Zahnrad-Wasserzeichen
- `Graphics/GuildResearchTreeRenderer.cs`: 18 Items, Bezier, Flow-Partikel, HitTest, Instanz-Paints, struct FlowParticle
- `Graphics/GuildResearchIconRenderer.cs`: 18 Vektor-Icons
- `Graphics/GuildHallHeaderRenderer.cs`: Steinmauer, Fackeln-Partikelsystem, Emblem, Shader-Cache
- `Graphics/GuildLeagueBadgeRenderer.cs`: Liga-Wappen mit Schild, Tier-Farben (Bronze/Silber/Gold/Diamant), Gold-Shimmer
- `Graphics/GuildBossRenderer.cs`: Boss-Silhouette mit Atem-Animation, HP-Balken mit Trail-Effekt, Damage-Feed (Swap-Remove, max 8)
- `Graphics/GuildWarDashboardRenderer.cs`: Versus-Anzeige, Score-Balken, Phasen-Timeline (ATK/DEF/END), Bonus-Missionen, eingebetteter GuildLeagueBadgeRenderer
- `Graphics/GuildWarLogRenderer.cs`: Kriegs-Log mit Zebra-Streifen, Glow für neue Einträge
- `Graphics/GuildAchievementRenderer.cs`: Achievement-Karten mit Tier-Akzent, Fortschrittsbalken, Checkmark, Gold-Shimmer
- `Graphics/GuildHallSceneRenderer.cs`: Isometrisches 8x6 Grid, 10 Gebäude-Positionen, Offscreen-Cache, Rauch-Partikel, Fenster-Glow, Fahne
- `ViewModels/GuildWarSeasonViewModel.cs`: Sub-VM für War-Dashboard, Log, Bonus-Missionen
- `ViewModels/GuildBossViewModel.cs`: Sub-VM für Boss-Anzeige, Schadens-Leaderboard
- `ViewModels/GuildHallViewModel.cs`: Sub-VM für Hauptquartier-Gebäude, Upgrade-Aktionen
- `Views/Guild/GuildWarSeasonView.axaml`: War-Dashboard mit SkiaSharp-Renderern
- `Views/Guild/GuildBossView.axaml`: Boss-Anzeige mit SkiaSharp-Renderer
- `Views/Guild/GuildHallView.axaml`: Hauptquartier-Szene mit SkiaSharp-Renderer
- `Views/Guild/GuildAchievementsView.axaml`: Achievement-Liste mit SkiaSharp-Renderer

### Firebase-Identitätssystem

- **PlayerId** (GUID) ist die stabile Spieler-Identität. Überlebt Firebase-Account-Wechsel, Geräte-Wechsel und Preferences-Verlust.
- **Initialisierung** (Priorität): 1. Preferences (`player_id`), 2. GameState.PlayerGuid (Backup), 3. neue GUID generieren
- **Firebase-UID** (`Uid`) ist nur intern für die Authentifizierung. Alle Daten-Pfade verwenden `PlayerId`, NICHT `Uid`.
- **auth_to_player Mapping**: `/auth_to_player/{uid}` → PlayerId in Realtime Database. Wird nach jedem Token-Refresh geschrieben (fire-and-forget via `SyncAuthToPlayerMappingAsync()`).
- **Security Rules**: PlayerId-basierte Autorisierung via auth_to_player Lookup. Rules-Datei: `database.rules.json` im Repo-Root. Deploy: `npx firebase-tools deploy --only database --project handwerkerimperium-487917`
- **Alle 11 Guild-Services** nutzen `_firebase.PlayerId` statt `_firebase.Uid` für Datenbankpfade.

### GameLoop-Integration (neue Gilden-Services)

4 neue Services im GameLoopService (1s-Takt) mit gestaffelten Offsets:
- `GuildBossService.CheckBossStatusAsync()` + `SpawnBossIfNeededAsync()` alle 60s (Offset 20)
- `GuildHallService.CheckUpgradeCompletionAsync()` alle 60s (Offset 40)
- `GuildAchievementService.CheckAllAchievementsAsync()` alle 300s (Offset 250)
- `GuildWarSeasonService.CheckPhaseTransitionAsync()` + `CheckSeasonEndAsync()` alle 300s (Offset 260)

## Feierabend-Rush

- 2h 2x-Boost, 1x/Tag gratis, danach 10 Goldschrauben
- Stackt mit SpeedBoost (bis 4x), Prestige-Shop "Rush-Verstaerker" erhöht auf 3x
- GameState: `RushBoostEndTime`, `LastFreeRushUsed`, `IsRushBoostActive`, `IsFreeRushAvailable`

## Meisterwerkzeuge (12 Artefakte)

5 Seltenheiten (Common/Uncommon/Rare/Epic/Legendary), permanente Einkommens-Boni (+2% bis +15%, gesamt +74%).
Prüfung alle 2 Minuten im GameLoop. `MasterToolUnlocked` Event → FloatingText + Celebration.

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

## Lieferant-System

- Zufällige Lieferungen alle **2-5 Minuten** (Prestige-Bonus reduziert Intervall)
- 5 Typen: Geld (35%), Goldschrauben (20%), XP (20%), Mood-Boost (15%), Speed-Boost (10%)
- 2 Minuten Abholzeit, sonst verfaellt
- GameState: `NextDeliveryTime`, `PendingDelivery`, `TotalDeliveriesClaimed`

## SKPath/SKFont-Caching

Renderer nutzen gecachte Instanz-/Klassenfelder statt `using var` pro Frame (GC-Reduktion bei 60fps):

| Renderer | Gecachte Felder |
|----------|----------------|
| InventGameRenderer | `_cachedPath` |
| BlueprintGameRenderer | `_cachedPath` |
| SawingGameRenderer | `_cachedPath` + `_cachedBladeShader`/`_cachedHandleShader` (Toleranz 2dp, vermeidet 60 Shader-Allokationen/s) |
| InspectionGameRenderer | `_cachedPath` |
| WiringGameRenderer | 8 SKPaint + 3 MaskFilter + `_cachedPath` + `_cachedFont` |
| DesignPuzzleRenderer | 7 SKPaint + `_cachedFont` |
| PipePuzzleRenderer | `_cachedPath` |
| RewardCeremonyRenderer | `_iconPath` |
| WorkshopCardRenderer | `_cachedPath` (static, ersetzt 13 `using var SKPath` pro Render-Aufruf) |
| ResearchIconRenderer | `_cachedPath` + `_labelFont` + `_crownFont` (static, ersetzt 17 `using var SKPath` + 2 `using var SKFont` — alle Icon-Methoden sequenziell) |
| GuildResearchIconRenderer | `_cachedPath` (static, ersetzt 12 `using var SKPath` — alle Icon-Methoden sequenziell) |

**WorkerAvatarRenderer**: Statische wiederverwendbare Paints (s_fillNoAA, s_fillAA, s_strokeNoAA) + s_cachedPath. Kein IDisposable (static readonly Felder leben bis Prozessende). **GameCardRenderer**, **ResearchIconRenderer** und **GuildResearchIconRenderer** sind statische Klassen.

**SKFont-Migration (neue SkiaSharp-API)**: Alle Renderer verwenden `canvas.DrawText(text, x, y, align, font, paint)` statt deprecated `paint.TextSize`/`paint.TextAlign`/`paint.FakeBoldText`/`canvas.DrawText(text, x, y, paint)`. Font-Felder: static readonly bei fester Größe (WorkshopGameCardRenderer: 8 Fonts, RewardCeremonyRenderer: 2 Fonts, GameCardRenderer: 2 Fonts), Instanz-Felder bei dynamischer Größe (PrestigeRoadmapRenderer: 1 Font, LuckySpinWheelRenderer: 1 Font, ForgeGameRenderer: 1 Font). `SKFont.MeasureText()` nutzt `out SKRect` statt `ref SKRect`.

**WorkerAvatarControl**: Gemeinsamer statischer Timer (`s_sharedTimer`) für alle Instanzen statt pro-Instanz Timer. Statische `s_bitmapPaint` + `s_blinkPaint` (keine Allokation pro Frame). WeakReference-Liste für Auto-Cleanup.

## Scroll-Performance-Optimierungen

| Optimierung | Effekt |
|-------------|--------|
| **Scroll-Pause ALLE Canvases** (30.03.2026) | Während Scroll: City-Canvas + Workshop-Cards + Background + TabBar komplett pausiert. 0 InvalidateSurface/s statt ~16/s. 250ms Ruhezeit nach letztem ScrollChanged. DashboardView.IsScrolling Property, MainView fragt ab |
| **MiniGame-ContentControl Konsolidierung** (30.03.2026) | 10 separate ContentControls → 1 einziges mit ActiveMiniGameViewModel. Spart 9 View-Instanzen + SkiaSharp-Renderer im Ruhezustand (~5-10 MB). ViewLocator erstellt/zerstört Views bei MiniGame-Wechsel statt alle 10 permanent im Visual Tree |
| **Max-Modus Debounce** (30.03.2026) | GetMaxAffordableUpgrades (Math.Pow-Schleife × 10 Workshops) nur auf Dashboard sofort, sonst alle 2s. Spart ~2000 Math.Pow/s im Late-Game auf nicht-sichtbaren Tabs |
| **FadeIn-Animation gecacht** (30.03.2026) | s_fadeInAnimation statisch statt neue Animation pro Tab-Wechsel |
| MainView BackgroundCanvas ~1fps statt 25fps | Background alle 15 Ticks (~1fps), während Scroll komplett pausiert |
| Dashboard City-Canvas 10fps, 0fps bei Scroll | Adaptiv: 10fps Basis, 30fps bei Effekten, 0fps während Scroll. Header-Parallax via RenderTransform (GPU) |
| WorkerAvatarControl Shared Timer | 1 Timer statt N (bei 8 Avataren: 20 statt 160 Ticks/s) |
| GameTick Tab-Awareness | PropertyChanged nur für sichtbare Tabs (spart ~20 Notifications/s) |
| BoxShadow→Opacity Animationen | GPU-beschleunigt statt CPU-Blur auf Android |
| LINQ→For-Schleifen | Kein Enumerator+Closure-GC in OnMoneyChanged, RefreshFeatureStatusTexts, Workshop-Lookups |
| MiniGame Views: Gecachte Render-Arrays | WiringGameView/PaintingGameView: .Select().ToArray() → gecachte Arrays mit For-Schleife (0 Allokation/Frame) |
| MiniGame Views: SKColor.Parse-Cache | InspectionGameView/RoofTilingGameView: Dictionary-Cache für Hex→SKColor/uint (0 String-Parsing/Frame) |
| PaintingGameView: Farb-Cache | SelectedColor nur bei Änderung neu geparst statt pro Frame |
| MiniGame Shader-Cache | ForgeGame (6), Wiring (3), Sawing (1), CraftTextures (1): Bounds-basierter Cache statt pro-Frame-Erstellung |

## IDisposable auf allen Renderern

Alle SkiaSharp-Renderer mit Instanz-Feldern (SKPaint, SKFont, SKPath, SKShader, SKMaskFilter) implementieren `IDisposable` mit `_disposed`-Guard. Statische Felder werden NICHT disposed.

| Renderer | Disposed Ressourcen |
|----------|---------------------|
| CityWeatherSystem | 3 SKPaint + 1 SKPath |
| CoinFlyAnimation | 4 SKPaint |
| ScreenTransitionRenderer | 3 SKPaint + 1 SKMaskFilter |
| OdometerRenderer | 5 SKPaint + 3 SKFont |
| ResearchTabRenderer | 1 SKFont + 1 SKPath |
| ResearchCelebrationRenderer | 2 SKFont |
| ResearchActiveRenderer | 4 SKFont |
| ResearchBranchBannerRenderer | 2 SKFont + 1 SKPath |
| GameBackgroundRenderer | 6 SKPaint + 1 SKShader |
| GameJuiceEngine | 6 SKPaint + 1 SKFont + 1 SKPath |
| GameTabBarRenderer | 5 SKPaint + 1 SKFont + 2 MaskFilter + 7 SKPath |
| CityRenderer | 3 SKPaint + 1 SKFont + CityWeatherSystem |
| GuildResearchTreeRenderer | 4 SKPaint (_fill, _stroke, _text, _glowPaint) + 3 SKFont + 3 SKPath |
| GuildHallHeaderRenderer | 1 SKShader |
| GuildLeagueBadgeRenderer | 3 SKPaint + 2 SKFont + 1 SKPath |
| GuildBossRenderer | 4 SKPaint + 3 SKFont + 1 SKPath |
| GuildWarDashboardRenderer | 2 SKPaint + 3 SKFont + GuildLeagueBadgeRenderer |
| GuildWarLogRenderer | 1 SKPaint + 2 SKFont |
| GuildAchievementRenderer | 2 SKPaint + 3 SKFont + 1 SKPath |
| GuildHallSceneRenderer | 2 SKPaint + 1 SKFont + 1 SKPath + 1 SKBitmap (Cache) |
| GuildResearchBackgroundRenderer | 1 SKShader + 4 SKPath |
| ResearchBackgroundRenderer | 1 SKShader + 5 SKPath |
| ResearchLabRenderer | 6 SKPaint |
| ForgeGameRenderer | 10 SKPaint + 1 SKFont + 6 SKShader (gecacht) |
| PipePuzzleRenderer | 6 SKPaint + 3 SKMaskFilter + 1 SKPath |
| SawingGameRenderer | 10 SKPaint + 1 SKPath + 1 SKMaskFilter + 3 SKShader (gecacht: Holz + Blatt + Griff) |
| BlueprintGameRenderer | 1 SKPath + 21 SKPaint (Instanz) + 3 SKFont + ~40 static readonly + 2 static MaskFilter + 1 SKShader (BG-Cache, per Bounds-Änderung neu) + static float[] DashIntervals |
| InventGameRenderer | 23 SKPaint + 1 SKPath + 1 SKShader (BG-Cache, per Bounds-Änderung neu) + static float[] DashIntervals |
| WiringGameRenderer | 8 SKPaint + 3 SKMaskFilter + 1 SKPath + 1 SKFont + 3 SKShader (gecacht) |
| DesignPuzzleRenderer | 7 SKPaint + 1 SKFont |
| WorkshopSceneRenderer | 2 SKPaint (nur Level-Overlays + Idle-Warnung) |
| PaintingGameRenderer | 13 SKPaint + 2 SKShader (gecacht: _vigShaderCache, _reflexShaderCache) |
| InspectionGameRenderer | 8 SKPaint + 1 SKPath (_fillNoAA, _fillAA, _fillAA2, _fillAA3, _strokeNoAA, _strokeAA, _strokeAA2, _strokeAA3, _cachedPath) |
| RoofTilingRenderer | 5 SKPaint (_fillPaint, _strokePaint, _iconPaint, _fillPaintAA, _borderPaint) |
| LuckySpinWheelRenderer | 11 SKPaint + 1 SKFont + 2 SKPath (_starPathCache, _hexPathCache) + 3 gecachte Paths (_segPath, _iconPathA, _iconPathB) + 13 gecachte SKShader (8 Segment + 3 Coin + Star + Hex + Bolt + Head + Shaft + Crown + Hub + Pointer) + 2 gecachte SKMaskFilter (Money/Speed Glow, dynamisch) |
| PrestigeRoadmapRenderer | 5 SKPaint + 1 SKFont + 1 SKMaskFilter |
| RewardCeremonyRenderer | 1 SKPath (_iconPath) |
| ResearchTreeRenderer | 3 SKFont + 2 SKPath |

**Kein IDisposable nötig** (nur static readonly Felder): `FireworksRenderer`, `LoadingScreenRenderer`, `WorkerAvatarRenderer`, `GameCardRenderer`, `ResearchIconRenderer`.

---

## Bekannte Gotchas

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Service-Caches stale nach Prestige/Import/Reset | GameLoopService/CraftingService subscriben nicht auf StateLoaded → Caches zeigen auf verwaiste Objekte | ALLE Services mit internen Caches MÜSSEN `StateLoaded += ResetCaches` im Konstruktor haben |
| Gilden-Mitglieder doppelt angezeigt | App-Datenverlust → neue PlayerId → Spieler tritt erneut bei → alter Eintrag bleibt in Firebase | 3-Maßnahmen-Fix: (1) `RemoveDuplicateMemberAsync` beim Join prüft auf gleichen Namen, (2) `CleanupStaleMembersAsync` entfernt >30d inaktive beim Laden, (3) UID→PlayerId Migration mit Retry auf DeleteAsync |
| CanGiveBonus Button grau obwohl genug Geld | `CanGiveBonus` prüfte 24h Lohn, `GiveBonus` kostete nur 8h → Button zu restriktiv | Alle 3 Stellen auf 8h harmonisiert (WorkerProfileViewModel + WorkerService) |
| Worker.AssignedWorkshop null bei Neustart | `GameState.CreateNew()` setzt `AssignedWorkshop` nicht → `IsWorking=false` → keine Fatigue-Akkumulation, falscher UI-Status | `AssignedWorkshop = WorkshopType.Carpenter` explizit setzen in `CreateNew()`. `SanitizeState` läuft nur bei geladenen Spielständen |
| 5 Dialoge am allerersten Start | Daily Reward→Story→Welcome→FirstWorkshop→AcceptOrder erschlagen neue Spieler | Daily Reward Tag 1 still einsammeln. Welcome-Hint überspringen wenn Story Ch.1 gezeigt wurde (redundant) |
| RecordMiniGameResult ignoriert QuickJobs | Early-Return bei `ActiveOrder == null` → Stats, Events, PerfectStreak nie aktualisiert bei QuickJobs → Belohnungen gehen verloren | `order.RecordTaskResult()` nur bei ActiveOrder, Stats+Events IMMER feuern |
| Auto-Complete bei QuickJobs Navigation-Loop | `CanAutoComplete` wird true, aber `AutoCompleteGameAsync()` findet kein ActiveOrder → NavigateBack | `UpdateAutoCompleteStatus()` prüft `GetActiveOrder() != null` vor Auto-Complete |
| PipePuzzle Rating zu großzügig | `optimalMoves = GridCols * GridRows` statt Pfad-Länge → moveEfficiency immer > 1.0 | `optimalMoves = Tiles.Count(t => t.IsPartOfSolution && !t.IsLocked)` |
| QuickJob Ad-Verdopplung nur in UI | `WatchAdAsync` setzt nur `order.IsScoreDoubled`, QuickJobs haben kein solches Flag | `QuickJob.IsScoreDoubled` Property + Verdopplung in MainViewModel.Navigation.cs |
| Gilde zeigt immer "Keine Internetverbindung" | 3 Bugs: (1) `GetAsync()` setzte `IsOnline=true` NICHT bei 200 OK mit "null"-Body, (2) `EnsureAuthenticatedAsync()` warf Exception statt Fallback auf neuen Account, (3) `GuildViewModel` catch-Block setzte IMMER Offline | (1) `IsOnline=true` VOR null-Check in GetAsync/QueryAsync, (2) Fallback `SignUpAnonymouslyAsync()` statt throw (sicher seit PlayerId-Migration), (3) catch prüft `IsOnline` statt blind Offline zu setzen. Zusätzlich: `SyncAuthToPlayerMappingAsync()` in GuildService.InitializeAsync() awaiten statt fire-and-forget |
| Firebase-Pfad unsichtbar (Permission denied still) | `database.rules.json` hat keinen Eintrag für den Pfad → Firebase gibt `null` zurück statt Daten, kein Error-Log (GetAsync fängt 200+null) | JEDEN neuen Firebase-Pfad auch in `database.rules.json` eintragen. Checkliste: player_guilds, player_invites, available_players, guild_invite_codes, invite_code_to_guild |
| Firebase orderBy-Query liefert keine Daten | Kein `.indexOn` für das abgefragte Feld in den Security Rules | `.indexOn: ["feldname"]` unter dem Pfad in `database.rules.json` hinzufügen |
| Firebase guilds-Write schlägt fehl bei Create | Write-Rule verlangt guild_members-Existenz, aber Member wird erst nach guilds geschrieben | `\|\| !data.exists()` zur Write-Rule hinzufügen (erlaubt Erstellen neuer Einträge) |
| Firebase Rate-Limit gegen Script-Cheating | Rules können keine `Date.parse()` auf ISO-Strings ausführen und clientseitige Timestamps sind manipulierbar | Server-Timestamp-Sentinel: Client setzt Feld als `{".sv":"timestamp"}` (Dict im C#-Model als `object?`), Firebase löst serverseitig zur Server-Zeit in ms auf. Rule: `(now - data.child('updatedMs').val()) >= 60000` |
| Multi-Task-Order: nur erster Task spielbar | MiniGame-Views stoppen ihren 30fps Render-Timer bei `IsResultShown=true` und setzen ihn auf `null`. Bei Task-Wechsel mit gleichem MiniGame-Typ (OrderGenerator rotiert über `template.GameTypes`) bleibt `ActivePage` konstant → `OnDataContextChanged` + `IsVisibleProperty` feuern nicht → Render-Timer wird nie neu gestartet. View friert ein, Canvas zeigt keine Animationen → Spieler kann Task 2+ nicht spielen, bekommt Miss oder bricht Order ab (Belohnung nur für Task 1) | `BaseMiniGameViewModel.GameRestarted`-Event: wird in `SetOrderId()` nach `InitializeGame()` gefeuert (vor `StartGameAsync()`). Alle 10 MiniGame-Views abonnieren es in `OnDataContextChanged` und rufen `StartRenderLoop()` auf wenn `_renderTimer == null`. Zusätzlich: `ContinueCommand` hat Reentrancy-Guard `if (!IsResultShown) return;` gegen Doppel-Tap-Race (zwei schnelle Taps würden SetOrderId re-entrant aufrufen während alter Countdown-Task.Delay noch läuft → alter Countdown liefe auf neu initialisiertem Spielfeld) |
| Gilden-Mitglieder verschwinden, eigener Spieler in Liste unsichtbar | `IGuildService.UpdateLastActiveAsync()` war deklariert aber NIRGENDS aufgerufen → `LastActiveAt` wird nur beim Gilden-Beitritt gesetzt. Nach 30 Tagen filtert `IsStaleMember()` den eigenen Eintrag aus der Anzeige. Zusätzlich: Duplikat-Filter (gleicher Name) kann den eigenen UID verwerfen wenn alte Account-Leiche mit neuerer `LastActiveAt` existiert | `RefreshGuildDetailsAsync()` ruft `UpdateLastActiveAsync().SafeFireAndForget()` auf (Keep-Alive für eigenen Eintrag) + explizite `isSelf`-Guards in beiden Filtern (Duplikat + Stale): eigener UID wird niemals gefiltert. Kein DTO-Patch auf `membersRaw` (vermeidet Cache-Vergiftung falls FirebaseService je einen Response-Cache einführt) |
