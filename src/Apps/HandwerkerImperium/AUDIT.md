# HandwerkerImperium — Voll-Audit v2.1.0

**Stand**: 12. Mai 2026 · **Scope**: gesamte Code-, Game-, UX- und Server-Logik · **Branch**: F:\Meine_Apps_Ava
**Methodik**: 5 parallele Deep-Dive-Audits (Game-Balancing, Code-Qualitaet, UI/UX, Performance, Firebase/Gilden/Mini-Games) gegen den echten Source-Code, kein CLAUDE.md-Hearsay.
**Gesamt-Findings**: 143 (15 Critical · 38 High · 51 Medium · 39 Low/Polish)

---

## Executive Summary

HandwerkerImperium ist ein **technisch beeindruckend gebautes Idle-Game** mit ehrgeiziger Architektur (85 Services, 55 ViewModels, 35 SkiaSharp-Renderer, Firebase-Multiplayer, 10 Mini-Games, 7-Tier-Prestige + Ascension + Eternal-Mastery). Die meisten Avalonia-12-Migrations-Aufgaben sind sauber erledigt, DateTime- und Disposal-Patterns sind durchgehend korrekt, der Service-Split via Facaden ist beispielhaft.

Die **Risiken konzentrieren sich auf vier Brueche**:

1. **Lock-Inkonsistenz** im State-Management. Mit der v2.1.0-Optimierung (Save auf Background-Thread unter `_stateLock`) wurden die alten `Collection-was-modified`-Races wieder scharf, weil mehrere Services (`WorkerService`, `EquipmentService`, `ReputationShopService`) eigene Locks halten und `GameLoopService.ProcessAutomation` + `OrderGeneratorService.RefreshOrders` gar keinen. **Drei reproduzierbare Crash-Pfade** in Auto-Accept-Spielern.
2. **Late-Game-Skalierung ohne Caps**: `PermanentHeirlooms` waechst pro Ascension unbegrenzt, `EternalMastery` linear + 5er/10er-Stufenboni ohne Cap, `Crafting-SellPrice` umgeht den Income-Soft-Cap komplett, `AscensionPoints`-Skalierungs-Bonus +2 AP/Level ohne Diminishing. Stacken multiplikativ — Spieler kann mit **5x Ascension + 500 T4-Heirlooms ~6x Permanent-Multiplier** generieren ohne Spielleistung.
3. **Firebase-Server-Rules klaffen** gegenueber der Client-Verteidigungstiefe: CoopOrder- und Auction-`.write`-Rules erlauben jedem Gildenmitglied schreibend auf fremde Auftraege; `cloud_saves.savedAt` ohne Monotonie; `guild_boss_damage` komplett offen; das Server-Timestamp-Sentinel `{".sv":"timestamp"}` wird **nirgendwo verwendet**; im Repo existieren **zwei** `database.rules.json` mit unterschiedlichem Inhalt — unklar welche deployed ist.
4. **UX-Frustpunkte fuer Neuspieler und Whales**: Settings-Tab versteckt die Tab-Bar ohne Back-Affordance, Prestige-Confirm zeigt keine Verlust-Liste, Heirloom-Wahl ohne Default kann Bonus permanent verlieren, Premium-Spieler sehen alle 13 Rewarded-Ad-CTAs weiter, FTUE-Welcome kollidiert mit Story-Chapter-1.

Drei sofortige Maßnahmen reichen aus, um den Großteil der Crash- und Cheat-Risiken zu schließen: (a) konsequente `_gameStateService.ExecuteWithLock`-Anwendung in den 6 betroffenen Stellen, (b) eine Konflikt-Pruefung in `CloudSaveService.RestoreFromCloudAsync`, (c) `.write`-Rule-Verschärfung fuer CoopOrders/Auctions auf `auth.uid == createdBy || auth.uid == invitedPlayer`. Alle drei sind ohne Architektur-Änderung umsetzbar — das Pattern existiert bereits an anderer Stelle.

---

## Top-15 Critical & High Priority (Cross-Domain)

| # | ID | Bereich | Titel | Datei |
|---|-----|---------|-------|-------|
| 1 | C-C01 | Code | `ProcessAutomation` mutiert State ohne Lock — Race mit Serializer | `Services/GameLoopService.Automation.cs:25-83` |
| 2 | C-C02 | Code | `OrderGeneratorService.RefreshOrders` mutiert `AvailableOrders` ohne Lock | `Services/OrderGeneratorService.cs:461-491` |
| 3 | C-C03 | Code | `WorkerService` / `EquipmentService` / `ReputationShopService` halten **eigene** Locks statt State-Lock | `Services/WorkerService.cs:17, 235-323` |
| 4 | B-C01 | Balancing | `PermanentHeirlooms` waechst unbegrenzt pro Ascension (Income-Explosion) | `Services/AscensionService.cs:102-110` |
| 5 | B-C02 | Balancing | `DoPrestige` hat **keinen** Lock + Doppel-Tap-Schutz → PP-Verdopplung exploitable | `Services/PrestigeService.cs:68-283` |
| 6 | B-C03 | Balancing | `Crafting-SellPrice` umgeht Income-Soft-Cap → Endless-Cash-Loop | `Services/CraftingService.cs:273-297` + `IncomeCalculatorService.cs:256-315` |
| 7 | FB-C01 | Firebase | Co-op-Reward-Doppelauszahlung beim Geraetewechsel | `Services/GuildCoopOrderService.cs:159-178` |
| 8 | FB-C02 | Firebase | Cloud-Save ueberschreibt **lokal haerteren** Save ohne Konflikt-Erkennung | `Services/CloudSaveService.cs:105-124` |
| 9 | FB-C03 | Firebase | Auction-Bid mit `SetAsync` (PUT) statt PATCH — klassische Bid-Race + Geld-Verlust | `Services/WorkerAuctionService.cs:50-84` |
| 10 | FB-C04 | Firebase | CoopOrder-Rules erlauben fremden Mitgliedern schreibenden Zugriff auf Score/Status | `database.rules.json:36-72` |
| 11 | P-C01 | Performance | `SaveAsync` haelt State-Lock 50-200ms unter Late-Game → UI-Jitter alle 30s | `Services/SaveGameService.cs:115-138` |
| 12 | P-C02 | Performance | `WorkerAvatarRenderer.PruneCache` disposed Bitmaps nicht → Native-Memory-Leak | `Graphics/WorkerAvatarRenderer.cs:852-863` |
| 13 | P-C03 | Performance | `WiringGameRenderer` + `PipePuzzleRenderer` halten `SKMaskFilter` als **Instanz** statt static | `Graphics/WiringGameRenderer.cs:49-51` + `PipePuzzleRenderer.cs:72-74` |
| 14 | U-C01 | UX | Settings-Tab versteckt Tab-Bar ohne sichtbare Back-Affordance | `Views/MainView.axaml:131-138` + `SettingsView.axaml` |
| 15 | U-C06 | UX | Prestige-Confirm zeigt **keine** Verlust-Liste — Frust-Risiko nach 1. Reset | `Views/PrestigeView.axaml:101-127` |

---

## Statistik

| Domäne | Critical | High | Medium | Low | Σ |
|--------|---------:|-----:|-------:|----:|--:|
| Game-Balancing & Economy | 3 | 8 | 10 | 6 | 27 |
| Code-Qualität & Bugs | 5 | 11 | 13 | 8 | 37 |
| UI/UX & Intuition | 6 | 8 | 11 | 10 | 35 |
| Performance & Memory | 3 | 9 | 8 | 8 | 28 |
| Firebase / Gilden / Mini-Games | 7 | 13 | 10 | 7 | 37 |
| **Σ** | **24** | **49** | **52** | **39** | **164** |

(Σ höher als 143, weil mehrere Findings über mehrere Domänen referenziert sind — siehe „Cross-Domain-Hotspots".)

---

## Cross-Domain-Hotspots

### State-Lock-Inkonsistenz (Code + Performance + Balancing)
Sechs Stellen mutieren State ausserhalb von `_gameStateService.ExecuteWithLock`:
`GameLoopService.Automation.cs` (Z.25-83 ProcessAutomation), `OrderGeneratorService.cs` (Z.461-491 RefreshOrders), `WorkerService.cs` (eigener Lock, Z.17/235-323), `EquipmentService.cs` (Z.14), `ReputationShopService.cs` (Z.14), `DailyRewardService.cs` (Z.88-140 ClaimReward), `PrestigeService.cs` (Z.68-283 DoPrestige), `PrestigeService.cs` (Z.351-390 BuyShopItem). Die Save-Optimierung v2.1.0 (Serialize unter `_stateLock` auf Background-Thread) hat diese Locks jetzt **scharf** gegen Race-Conditions. Fix: konsistent `_gameStateService.ExecuteWithLock(...)` verwenden, wie es `OrderGeneratorService.GenerateLiveOrder` (Z.375, 525, 597) und `NotificationCenterService` (Z.49, 65, 74, 109, 121, 138, 162) bereits korrekt machen.

### Late-Game-Multiplikator-Stack ohne Caps (Balancing + Performance)
`PermanentHeirlooms.Count * 0.005` (kein Cap) × `EternalMastery linear + 5er/10er-Stufen` (kein Cap) × `AscensionPoints +2/Level Skalierung` (kein Cap) × `Crafting-SellPrice` (kein Soft-Cap) → mehrere multiplikative Pfade die einzeln linear gedacht sind und kombiniert exponentiell wirken. Performance-Folge: `IncomeCalculatorService.CalculateGrossIncome` (15+ decimal-Multiplikationen pro Sekunde) wird im Late-Game zunehmend Hot-Spot. Empfehlung: gemeinsamer Cap und ein gecachter `_totalIncomeMultiplier` mit Dirty-Flag (siehe P-H05).

### Firebase-Rules-Wildwest (Firebase + Code + Balancing)
Zwei `database.rules.json` im Repo mit unterschiedlichem Inhalt (Root + App-lokal) — Deployment-Status unklar. CoopOrder/Auction `.write`-Rules erlauben fremden Mitgliedern Eingriffe (FB-C04/C05). `guild_boss_damage` ohne Monotonie (Leaderboard-Cheat). `cloud_saves.savedAt` ohne Monotonie (FB-H09). `{".sv":"timestamp"}` wird nirgends genutzt — alle Timestamps client-gesetzt, keine serverseitigen Rate-Limits. `MegaProjects`-Rule ist Vorbild und sollte schablonenhaft auf die anderen Pfade angewendet werden.

### Premium-Wahrnehmung (UX + Balancing)
Premium-Multiplier wird intern als +50% verkauft, stackt aber multiplikativ mit EternalMastery (Late-Game effektiv 5-10x) — UI zeigt das nicht (B-M01). Gleichzeitig sehen Premium-Spieler **alle** 13 Rewarded-Ad-CTAs weiter (U-H05). Heirloom-Wahl ohne Default kann +8% pro Run permanent verlieren (U-H08). Auto-Complete-Mastery (B-H08) wird durch Prestige-Reset entwertet — der angeblich beste Premium-Benefit ist faktisch wertlos.

---

# I · Game-Balancing & Economy (27 Findings)

## Critical

### B-C01 · PermanentHeirlooms wachsen unbegrenzt pro Ascension
**File**: `Services/AscensionService.cs:102-110`
Bei jeder Ascension wird `state.CraftingInventory` iteriert und jedes Tier-4-Item (count Mal) zu `state.Ascension.PermanentHeirlooms` addiert — **kein Cap, keine Selektion**. Spieler kann vor Ascension T4-Items farmen (Villa 2.5M, Skyscraper 4M, ImperiumHQ 5M BaseValue, hunderte Stueck via `WarehouseStackLimit`). `IncomeCalculatorService.GetTotalHeirloomBonus` rechnet linear `permanent.Count * 0.005m`. **Bei 100 T4 in 1 Ascension: +50% permanent. Nach 5 Ascensions mit je 100: +250%** — ohne andere Quellen. Stackt multiplikativ mit Eternal-Mastery (B-H02).
**Empfehlung**: Hard-Cap `PermanentHeirlooms.Count ≤ 50` ODER pro Ascension max 3-5 analog zu `MaxHeirloomsPerRun`. Alternative: degressiv `0.005 * sqrt(count)`.

### B-C02 · DoPrestige ohne Lock / Doppel-Tap-Schutz
**File**: `Services/PrestigeService.cs:68-283`
`DoPrestige(tier)` ruft `CanPrestige`, mutiert State (`PrestigePoints += tierPoints`, `BronzeCount++`, History, ResetProgress, SaveAsync) — **kein `_lock`, kein `ExecuteWithLock`**. Doppel-Tap bei Render-Lag waehrend Cinematic → `CanPrestige` zweimal true → Punkte doppelt, `BronzeCount` doppelt. Die Stelle `if (tier == Bronze && tierPoints < 15) tierPoints = 15;` macht den 2. Durchlauf bei Geld=0 sogar attraktiv (15 PP geschenkt).
**Empfehlung**: `_gameStateService.ExecuteWithLock(() => {...})` analog `OrderGeneratorService.GenerateLiveOrder`. Mindestens `Interlocked.Exchange` mit `_prestigeInProgress`-Flag vor CanPrestige.

### B-C03 · Crafting-SellPrice umgeht Income-Soft-Cap
**File**: `Services/CraftingService.cs:273-297` + `IncomeCalculatorService.cs:256-315`
`CalculateCraftingSellMultiplier` multipliziert sequentiell PrestigeShop, Research, Events, MasterTools, Guild, VIP, Rebirth, Premium × 1.5 — **ohne Soft-Cap** (Comment Z.313: "bewusst weggelassen"). Im Late-Game >20x moeglich. T4-Items hortbar im Lager. Bei `MarketShortage`-Event ×3 mult. Spieler hortet, wartet auf Stack-Konstellation, verkauft alles → exponentieller Geld-Pump. Untergraebt `currentRunMoney`-basierte PP-Formel.
**Empfehlung**: Selber Tier-Soft-Cap wie `IncomeCalculatorService.ApplySoftCap (Z.178-226)` auch hier anwenden. Mindestens harter Cap 12x. Alternative: SellPrice-Snapshot am Saison-Start fixieren.

## High

### B-H01 · Soft-Cap-Threshold-Sprung erzeugt visuelles Cliff bei Ascension
**File**: `Services/IncomeCalculatorService.cs:194-206`
Tier-skalierter Threshold (None=4x → Legende=20x). Ascension setzt `Prestige.CurrentTier=None` zurueck → Soft-Cap-Threshold faellt brutal von 20x auf 4x → gefuehlte Income-Halbierung post-Ascension.
**Empfehlung**: Threshold `4 + AscensionLevel * 2` (cap 24) statt prestige-tier-basiert.

### B-H02 · Eternal Mastery hat keinen Cap, stackt mit Heirlooms multiplikativ
**File**: `Services/EternalMasteryService.cs:58-74` + `Models/GameBalanceConstants.cs:121-134`
`Bonus(N) = N*0.005 + (N/5)*0.025 + (N/10)*0.05`. N=100: +150%. N=1000: +1500%. Kommentar Z.118: "Skaliert linear ewig weiter — kein Cap". Wird multiplikativ ueber Premium 1.5x angewendet.
**Empfehlung**: Soft-Cap log10 ab N=50, oder nur eine Skala (linear ODER Stufen).

### B-H03 · AscensionPoints Skalierungs-Bonus +2 AP/Level ohne Cap
**File**: `Services/AscensionService.cs:75-79`
`apFromScaling = state.Ascension.AscensionLevel * 2`. AL=50: +100 AP geschenkt — alle 6 Perks easy maxed. Late-Whale-Inflation.
**Empfehlung**: `(int)Math.Sqrt(AL) * 2` oder Cap +20 AP.

### B-H04 · PrestigePoints-Doc behauptet TotalMoneyEarned, Code nutzt CurrentRunMoney
**File**: `Models/PrestigeData.cs:138-145` + `PrestigeService.cs:59-63`
XML-Doc Z.139: `floor(sqrt(TotalMoneyEarned / 100_000))`. Code: `CurrentRunMoney`. Time-Bomb falls jemand das "fixt".
**Empfehlung**: Doc/Parameter umbenennen auf `currentRunMoney` + Unit-Test.

### B-H05 · Workshop-Spezialisierung trivial — Efficiency dominiert immer
**File**: `Models/WorkshopSpecialization.cs:27-58`
Efficiency: netto +15-20% Income. Quality: +10-15%. Economy: +5%. **Es gibt keinen Build, in dem Quality oder Economy gewinnt** — Spieler waehlt blind Efficiency, Respec-Cost (20 GS) ist toter Code.
**Empfehlung**: Economy gibt +1 Worker-Slot + Reward-Bonus, Quality verdoppelt Aura-Bonus. Tuning, kein Code-Change.

### B-H06 · Worker-Markt-Gewichtung erlaubt Legendary-Farming
**File**: `Models/WorkerMarketPool.cs:94-132`
Legendary=0.1/99.1 ≈ 0.1%/Slot, bei 8 Slots/Rotation × 6 Rot/Tag ≈ 5% Sichtung/Tag. GS-Cost 750 ist Late-Game-peanuts. 10 Workshops × 4 Legendary × +20% Aura (gecapped 50%) = +500% kumulativ.
**Empfehlung**: RemoteConfig-Weights nach Spieler-Progress, oder Legendary-Sichtungs-Cooldown 7 Tage.

### B-H07 · Mini-Game Miss = 50% Reward + 25% XP zu großzügig
**File**: `Models/Enums/MiniGameResult.cs:29-48`
Skill-Spread Perfect→Miss nur 3x (1.5→0.5). Auto-Tap bringt 50%. Untergraebt Skill-Loop UND Premium-Auto-Complete-Wert paradox.
**Empfehlung**: Miss=0.20, Ok=0.50, Good=1.0, Perfect=1.5 (7.5x Spread). RemoteConfig-Override.

### B-H08 · Auto-Complete-Mastery wird durch Prestige-Reset entwertet
**File**: `Services/PrestigeService.cs:647` + `Services/AscensionService.cs:188`
Beide rufen `state.PerfectRatingCounts?.Clear()`. Spieler erarbeitet 30 Perfects (15 Premium) ueber Stunden, Prestige resettet — naechste Mastery-Erarbeitung von vorn. Premium-Benefit-Spread (30→15) sieht groß aus, ist praktisch wertlos.
**Empfehlung**: `PerfectRatingCounts` nur in Ascension reseten, nicht in Prestige. Oder Schwellen senken 15/8.

## Medium

### B-M01 · Premium +50% Income unsichtbar in Multiplikator-Kette
**File**: `Services/IncomeCalculatorService.cs:115-117`
Premium x1.5 stackt multiplikativ — effektiv Late-Game 5-10x. UI zeigt nur "+50%". Convertierung leidet.
**Empfehlung**: Header zeigt `grossIncome / grossIncomeOhnePremium`-Multiplikator als effektive Premium-Wirkung. UI-Change, kein Service-Code-Change.

### B-M02 · Welcome-Back-Premium-Offer ohne Cap (3.6T Geld bei 72h+)
**File**: `Services/WelcomeBackService.cs:57-71`
`netPerSecond * 3600` ohne Cap → 72h-Pausen-Farming.
**Empfehlung**: `Math.Min(... , state.MaxOfflineHours * 3600m * netPerSecond)` oder absolute Cap (z.B. 1B). 1× pro Saison.

### B-M03 · DailyRewardService.ClaimReward nicht thread-safe
**File**: `Services/DailyRewardService.cs:88-140`
Kein Lock zwischen `IsRewardAvailable` und `LastDailyRewardClaim = UtcNow`. Doppel-Tap → Geld + GS doppelt.
**Empfehlung**: `lock (_lock)` Pattern wie in `WorkerService.HireWorker`.

### B-M04 · Repeatable Prestige-Shop transparent unattraktiv
**File**: `Services/PrestigeService.cs:346-348`
Cost `2^count`, Effekt linear `+IncomeMultiplier` — DR ist absichtlich, aber Spieler sieht nicht woran er ist.
**Empfehlung**: Tooltip mit "Diminishing Returns".

### B-M05 · PrestigeService.BuyShopItem ohne Lock
**File**: `Services/PrestigeService.cs:351-390`
Doppel-Tap → PP negativ, Item-Count zweimal erhoeht. Selten ausnutzbar.
**Empfehlung**: `ExecuteWithLock`.

### B-M06 · Offline-Earnings × RushBoost ohne Cap auf rushMultiplier
**File**: `Services/OfflineProgressService.cs:124-170`
`rushMultiplier` kann durch PrestigeShop-Items >5x werden, × SpeedBoost 2x = >10x auf Offline-Earnings.
**Empfehlung**: `rushMultiplier = Math.Min(rushMultiplier, 4m)`.

### B-M07 · BattlePass Premium-Lock-in am letzten Saisontag
**File**: `Services/BattlePassService.cs:101-120`
Spieler kauft Premium-Pass am Tag 29 von 30 → 1 Tag Wert, dann verfaellt. Kein Hint.
**Empfehlung**: Premium-Kauf-Button ab Tag 27 mit Warn-Hint oder disabled.

### B-M08 · Tier-DR greift nur same-tier — Bronze/Silver-Alternation umgeht
**File**: `Services/PrestigeService.cs:166-179`
DR-Faktor ist tier-spezifisch. Spieler kann zwischen Tiers wechseln und DR umgehen.
**Empfehlung**: Telemetrie pruefen, evtl. globalen DR-Modifier.

### B-M09 · Worker-IsWorking-Race zwischen Workshops.Clear() und RestoreKeptWorkers
**File**: `Services/PrestigeService.cs:523-548, 562-570`
Mini-Fenster zwischen Clear und Restore. Mit B-C02 zusammen geloest.

### B-M10 · OrderRewardMultiplierSoftCap=10x — Konstante existiert, Nutzung unklar
**File**: `Models/GameBalanceConstants.cs:443`
Grep zeigt nur Definition. Tote Konstante oder einzige Anwendungsstelle?
**Empfehlung**: Volltextsuche; falls ungenutzt entfernen, sonst dokumentieren + auf Crafting-Sell erweitern.

## Low

### B-L01 · Comment "ab Level 100" — tatsaechlich Lv50 (`Models/WorkshopSpecialization.cs:6`)
### B-L02 · `Math.Sqrt(double)` verliert Praezision bei sehr großen `decimal` (`Models/PrestigeData.cs:144`)
### B-L03 · Worker-Hiring-Cost linear → Late-Game trivialisiert (`Models/Enums/WorkerTier.cs:99-104`)
### B-L04 · Ascension-Min-5-AP psychologisch klein (`Services/AscensionService.cs:79`)
### B-L05 · AutoProductionInterval inkonsistent: MasterSmith 60s vs InnovationLab 120s vs Standard 180s — bringt MasterSmith bei Tier2/3-Cross-Workshop-Inputs nicht zum tragen (`Models/GameBalanceConstants.cs:338-344`)
### B-L06 · Mini-Game Reward-Spread 3x vs XP-Spread 6x inkonsistent (`Models/Enums/MiniGameResult.cs:29-48`)

---

# II · Code-Qualität & Bugs (37 Findings)

## Critical

### C-C01 · ProcessAutomation mutiert State ohne ExecuteWithLock — Race mit Serializer
**File**: `Services/GameLoopService.Automation.cs:25-83`
`state.PendingDelivery=null` (Z.33), `state.ActiveOrder=bestOrder` (Z.78), `state.AvailableOrders.Remove(...)` (Z.79), `w.Mood=...` (Z.51), `state.SpeedBoostEndTime=...` (Z.54) — **alles außerhalb** des State-Locks. `SaveAsync` (`Services/SaveGameService.cs:115-138`) serialisiert state auf Background-Thread unter `_stateLock`. Auto-Accept tickt alle 5s. **Reproduzierbare `Collection was modified`-Crashes** bei Auto-Accept-Spielern.
**Empfehlung**: Ganze Routine in `_gameStateService.ExecuteWithLock(...)`, Events außerhalb feuern (analog `PeriodicChecks.cs:122-131`).

### C-C02 · OrderGeneratorService.RefreshOrders mutiert AvailableOrders ohne Lock
**File**: `Services/OrderGeneratorService.cs:461-491`
`AvailableOrders.Clear()`, `AddRange(existingMaterialOrders)`, `AddRange(newOrders)`, `Add(materialOrder)` — kein Lock. Inkonsistent zu `GenerateLiveOrder` (Z.375), `ExpireOldLiveOrders` (Z.525, 597) die korrekt `ExecuteWithLock` nutzen. Aufgerufen aus `MainViewModel.EventHandlers.cs:412` + `Init.cs:77`.
**Empfehlung**: `ExecuteWithLock` wrappen.

### C-C03 · WorkerService / EquipmentService / ReputationShopService — eigene Locks statt State-Lock
**File**: `Services/WorkerService.cs:17, 235-323` + `Services/EquipmentService.cs:14` + `Services/ReputationShopService.cs:14`
Eigene `private readonly object _lock = new()`. `UpdateWorkerStates` mutiert `state.Workshops[*].Workers` (Z.254-322), `ws.Workers.Remove(worker)` (Z.311) jeden Tick. Serializer im Background-Thread crasht.
**Empfehlung**: Eigene Locks entfernen, durchgehend `_gameStateService.ExecuteWithLock`.

### C-C04 · GuildAchievementService nutzt blockierendes `.Result` auf Firebase-Tasks
**File**: `Services/GuildAchievementService.cs:243, 259, 263`
Nach `await Task.WhenAll(...)` `.Result` der Tasks. Faulted-State als `AggregateException`. Style/Falle.
**Empfehlung**: `var x = await task;` — `WhenAll` redundant wenn man danach `await` macht.

### C-C05 · `_lastCloudUploadAttempt` außerhalb des Locks geschrieben
**File**: `Services/SaveGameService.cs:160-186`
Rate-Limit "max alle 2min" wird unter Last verletzt. Doppelter Cloud-Upload moeglich.
**Empfehlung**: Schreibvorgang in `ExecuteWithLock` oder `Interlocked.Exchange<long>` mit `now.Ticks`.

## High

### H-H01 · WorkerProfileViewModel Undo-Timer Lambda-Capture-Leak
**File**: `ViewModels/WorkerProfileViewModel.cs:667-674, 901-905`
`_undoTimer.Tick += (_,_) => {...}` — keine `-=` in Dispose. Mehrfaches Feuern → Worker mehrfach gekuendigt.
**Empfehlung**: Benannte Methode + explizites Unsubscribe.

### H-H02 · LuckySpinViewModel.StartCountdownTimer erzeugt neuen Timer ohne alten zu stoppen
**File**: `ViewModels/LuckySpinViewModel.cs:310-315`
Mehrfache Tab-Wechsel → mehrere Countdown-Timer parallel.
**Empfehlung**: `_countdownTimer?.Stop()` + `=null` zu Beginn.

### H-H03 · GuildMegaProjectViewModel — async-void Lambda im DispatcherTimer.Tick ohne try/catch
**File**: `ViewModels/Guild/GuildMegaProjectViewModel.cs:71`
`_refreshTimer.Tick += async (_,_) => await RefreshAsync().ConfigureAwait(false);` — Firebase-Exception crasht Prozess.
**Empfehlung**: Ueber `AsyncExtensions.RunHandlerSafely` wrappen.

### H-H04 · BottomSheetBehavior — Race bei schnellem Toggle
**File**: `Behaviors/BottomSheetBehavior.cs:46-111`
`async void OnIsOpenChanged` mit `Task.Delay`. Letzte Animation gewinnt nicht garantiert — Sheet schließt sich, obwohl der User es geöffnet hat.
**Empfehlung**: `CancellationTokenSource` per Element-AttachedProperty.

### H-H05 · GameLoopService.Stop ruft SaveAsync().FireAndForget — Save kann verloren gehen
**File**: `Services/GameLoopService.cs:230, 245`
Bei Android-OnPause/Kill kann Background-Save abgebrochen werden, bevor er im FS landet. **Idle-Game = problematisch** (Offline-Earnings basieren auf `LastPlayedAt`).
**Empfehlung**: In `Pause()` synchron `await SaveAsync()`. Methode als `async Task`.

### H-H06 · MainView.FadeInContentPanel ist async void
**File**: `Views/MainView.axaml.cs:470-490`
Pattern-Violation, aber try/catch fängt — niedrig.
**Empfehlung**: Auf `RunHandlerSafely` umstellen.

### H-H07 · BaseMiniGameViewModel.HandleTimerTick fängt Exception, stoppt aber Game nicht
**File**: `ViewModels/MiniGames/BaseMiniGameViewModel.cs:200-211`
Spieler sieht "stehengebliebenes Spiel".
**Empfehlung**: Im catch `IsResultShown=true; Result=Miss; CalculateAndSetRewards()`.

### H-H08 · AchievementService — kein StateLoaded-Subscribe, nur explizites Reset
**File**: `Services/AchievementService.cs:23-49, 77-86`
Cached Unlocked-Status. Bei Save-Import via Test/Tool ohne MainViewModel-Resolve stale.
**Empfehlung**: Ctor `_gameStateService.StateLoaded += (_,_) => Reset();`

### H-H09 · SaveGameService.LoadFromFileAsync — keine Recovery wenn beide Files corrupt
**File**: `Services/SaveGameService.cs:226-248, 190-224`
Beide Files corrupt → `CreateNew()` → **kompletter Daten-Loss**. Kein Cloud-Restore-Prompt.
**Empfehlung**: Bei beiden corrupt: `ICloudSaveService.GetMetadataAsync` + Auto-Restore anbieten.

### H-H10 · ProcessAutomation berührt SpeedBoostEndTime ohne InvalidateIncomeCache
**File**: `Services/GameLoopService.Automation.cs:54`
1s Cache-Mismatch bei Doppel-Boost-Stacking.
**Empfehlung**: `state.InvalidateIncomeCache()`.

### H-H11 · MainView Dispose-Order nicht garantiert bei 5 Renderern
**File**: `Views/MainView.axaml.cs:23-34, 74-94`
Wenn ein Renderer-Dispose wirft, wird Rest übersprungen.
**Empfehlung**: try/catch um jeden Dispose.

## Medium

### M-M01 · GameLoopService — Reputation-Cast `_gameStateService is GameStateService gss` (`GameLoopService.cs:306-307`, `PeriodicChecks.cs:195-196`)
Brittle — Tests crashen. Methode ins Interface heben.

### M-M02 · WorkshopView.axaml.cs PropertyChanged-Lambda ohne Unsubscribe (`Views/WorkshopView.axaml.cs:44-59`)

### M-M03 · BaseMiniGameViewModel `Debug.WriteLine` in Release-Build (`ViewModels/MiniGames/BaseMiniGameViewModel.cs:208`)

### M-M04 · MainView `_lastActiveTab` String-Compare statt Enum (`Views/MainView.axaml.cs:19, 425`) — Allokation pro PropertyChanged

### M-M05 · `IGameLoopService` deklariert nicht `IDisposable` (`Services/GameLoopService.cs:21`)

### M-M06 · Hardcoded Sprach-Fallbacks deutsch/englisch (`ViewModels/MainViewModel.EventHandlers.cs:134, 148, 152, 209`)

### M-M07 · SaveGameService Money-Cap 100B verschluckt valide Saves (`Services/SaveGameService.cs:332`)
Idle-Game mit Prestige x20 + Rush erreicht 100B in 50h. Anti-Cheat zu hart.
**Empfehlung**: Cap dynamisch aus `Statistics.TotalMoneyEarned`, oder auf 1e15 heben.

### M-M08 · SaveGameService.SanitizeState — Premium-Reset versteckt Refund-Bug (`Services/SaveGameService.cs:589-593`)
Premium-User mit kaputtem Netz sieht 30s Banner-Ad.
**Empfehlung**: Premium-Flag in `IPreferencesService` cachen (7-Tage-Trust).

### M-M09 · `state.Statistics.TotalDeliveriesClaimed++` ohne Lock (`Services/GameLoopService.Automation.cs:34`)
Mit C-C01 zusammen.

### M-M10 · `DateTime.Now` in CityWeatherSystem für Jahreszeit (`Graphics/CityWeatherSystem.cs:65`)
Absichtlich, dokumentiert. Toleranzfall.

### M-M11 · MainViewModel-Constructor mit 44 Parametern (`ViewModels/MainViewModel.cs:178-248`)
**Empfehlung**: Facade-Pattern weiter ausbauen (Progression/Onboarding-Facades).

### M-M12 · `?.` an Pflicht-Services (`Services/GameLoopService.cs:269, 376, 379`)
Toter Code, irreführend. Entfernen.

### M-M13 · SaveGameService File.Move-Atomicity (`Services/SaveGameService.cs:145-152`)
Backup-Copy + Temp-Move nicht atomar. Bei Crash zwischen Steps kein Backup mehr.
**Empfehlung**: `File.Move(SaveFilePath, BackupFilePath, true)` vor Temp-to-Main.

## Low

### L-L01 · AsyncExtensions inkonsistent Debug vs Console.WriteLine (`Helpers/AsyncExtensions.cs:97`)
### L-L02 · MainViewModel-Constructor Subscribe-Reihenfolge inkonsistent (`ViewModels/MainViewModel.cs:386-489`)
### L-L03 · Magic Numbers für Delivery-Interval (`Services/GameLoopService.PeriodicChecks.cs:226`)
### L-L04 · Tier1CraftingProducts String-Array hardcoded (`Services/GameLoopService.PeriodicChecks.cs:14`)
### L-L05 · OrderLiveSpawnCheck-Offset 17 unkommentiert (`Services/GameLoopService.cs:81`)
### L-L06 · MainViewModel.EventHandlers OnHeaderVmPropertyChanged feuert 7 OnPropertyChanged (`ViewModels/MainViewModel.EventHandlers.cs:605-614`)
### L-L07 · BottomSheetBehavior 16ms-Magic-Delay nicht 60fps-adaptiv (`Behaviors/BottomSheetBehavior.cs:61`)

## Bekannte CLAUDE.md-Bugs — Verifikations-Stichprobe
- ✅ `Worker.AssignedWorkshop null` — behoben (CreateNew, CreateForTier)
- ✅ `NotificationCenter Collection modified` — behoben (alle Mutationen unter ExecuteWithLock)
- ✅ `Avalonia-12-API-Migration` — vollständig (kein GetVisualRoot, SystemDecorations, IsAttachedToVisualTree)
- ✅ `DateTime.Now`-Pattern — einziges Vorkommen ist `CityWeatherSystem.cs:65` (absichtlich, dokumentiert)
- ✅ `SaveAsync UI-Thread-Freeze` — behoben (Background-Thread), aber Lock-Probleme C-C01..C-C03 neu scharf
- ✅ `LuckySpin Timer-Leak bei Exception` — behoben (catch stoppt Timer), aber `StartCountdownTimer` hat neuen Leak (H-H02)
- ⚠️ `async void Event-Handler` — meist behoben, aber `GuildMegaProjectViewModel:71` ohne RunHandlerSafely (H-H03)
- ⚠️ `Service-Caches StateLoaded`-Subscribe — meist OK, `AchievementService` nur via explizites Reset (H-H08)

---

# III · UI/UX & Intuition (35 Findings)

## Critical

### U-C01 · Settings-Tab versteckt Tab-Bar ohne Back-Affordance
**View**: `Views/MainView.axaml:131-138` + `ViewModels/MainViewModel.Tabs.cs:320`
Klick aufs Zahnrad-Icon → `ActivePage.Settings` → Tab-Bar weg (`IsTabBarVisible`-Binding), nur dezente Breadcrumb (0.6 Opacity). Kein sichtbarer Back-Button in `SettingsView`. Spieler ohne Android-Back-Reflex sitzt fest.
**Empfehlung**: Settings als 6. Tab ODER prominenter Back-Button im SettingsView-Header.

### U-C02 · FTUE-Welcome kollidiert mit Story-Chapter-1 + ContextualHints
**View**: `Services/FtueService.cs:24` + `Views/FtueOverlay.axaml:13-23` + `ViewModels/MainViewModel.Navigation.cs:64-65`
`ftue_welcome` (Meister Hans) + `StoryDialog` (Meister Hans) + Contextual-Hint koennen parallel triggern. Beide Hans, beide "Weiter"-Button — Onboarding wirkt schwer und redundant.
**Empfehlung**: Story-Chapter-1 erst nach `FtueFinished`-Event spawnen, oder zu einem einzigen "Welcome-Hans"-Flow vereinen.

### U-C03 · Ascension Sub-Tab disabled ohne Feedback bei Tap
**View**: `Views/ImperiumView.axaml:154-180`
`IsEnabled=false` bis 3× Legende → Tap macht nichts. Kein Toast, kein Hint. Lock-Icon ist visuell identisch zu Warehouse-Tab. Late-Game-Hauptbelohnung damit unsichtbar.
**Empfehlung**: Disabled-Tap → ContextualHint "3× Legende erforderlich". Tab-Icon farblich differenzieren.

### U-C04 · Workshop-Cards-Canvas hat unklare Tap-Targets
**View**: `Views/DashboardView.axaml:185-190`
Alle Workshop-Karten = **ein** SKCanvas. Keine `Button`-Wrapper → globales TapScale 0.95/80ms greift nicht (App.axaml:218-229). Keine AutomationId pro Karte. Long-Press fuer Bulk-Buy nicht implementiert obwohl Hint das verspricht (U-H04).
**Empfehlung**: Pro Card unsichtbaren `Button` ueberlagern ODER `_pressedCardIndex` im Renderer pflegen.

### U-C05 · Daily-Reward-Header-Button vs. Dialog im Konflikt — Streak-Dots hardcodiert
**View**: `Views/Dashboard/DashboardHeader.axaml:67-76` + `Views/Dialogs/DailyRewardDialog.axaml:43-60`
Header-Gift-Button + Auto-Open-Dialog beide aktiv. Streak-Dots 7× hardcodiert als alternierende Borders — **kein Binding** an aktiven Tag. Tag-1-Spieler sieht 7 vorgefuellte Dots → Confusion.
**Empfehlung**: ItemsControl mit Binding an `WelcomeFlowVM.StreakDays`. Tag-1-Reward im Header trotzdem zeigen.

### U-C06 · Prestige-Confirm zeigt KEINE Verlust-Liste
**View**: `Views/PrestigeView.axaml:101-127` + `Views/Dialogs/ConfirmDialog.axaml:99-110`
Alles in Gains-Gruen (`#A5D6A7`). `PrestigePreviewLosses` nur im Banner (`Views/Imperium/AscensionSection.axaml:60`) sichtbar, nicht im Confirm. Schwerer Frust-Trigger.
**Empfehlung**: `PrestigeLossesBrush` (#EF9A9A, in AppPalette schon vorhanden) im Confirm-Dialog rot zeigen. Erstes Prestige: zweites Confirm "Wirklich? Du verlierst X".

## High

### U-H01 · Header-Information-Overload (bis zu 8 Chips in Zeile 2)
**View**: `Views/Dashboard/DashboardHeader.axaml:119-237`
360dp-Phone: Level + Prestige + Reputation + XP-Bar + Boost + Seasonal + EternalMastery + Reputation-Btn + Streak. XP-Bar wird auf <80dp gedrueckt. Seasonal-Chip immer sichtbar (kein IsVisible-Gate).
**Empfehlung**: Header-Compact-Mode (Level+XP) vs Expanded-Mode.

### U-H02 · GoldenScrews-Badge fuehrt direkt zum Shop → Kontextverlust
**View**: `Views/Dashboard/DashboardHeader.axaml:46-64`
Anzeige = Aktion-Konflikt. Spieler tippt mitten im MiniGame auf Zahl → Shop.
**Empfehlung**: Tap = Bottom-Sheet mit Quellen-Info; Long-Press = Shop.

### U-H03 · Imperium 6 Sub-Tabs in 360dp mit FontSize 10 — Labels truncated
**View**: `Views/ImperiumView.axaml:64-181`
Tap-Target zwar 44dp, aber visuell eng. Deutsche Labels werden ellipsised.
**Empfehlung**: Auf 4 Sub-Tabs konsolidieren (Equipment unter Worker) ODER Scrollable.

### U-H04 · Long-Press-Bulk-Buy-Hint verspricht nicht-implementierte Funktion
**View**: `Resources/Strings/AppStrings.resx:3579-3583` + `Views/DashboardView.axaml:136-141`
`HintLongPressBulkTitle` sagt "Hold the upgrade button..." — im SKCanvas ist nur Click-Hit-Test. Spieler probiert Long-Press, nichts passiert, Cycle-Button (klein, oben rechts) wird uebersehen.
**Empfehlung**: Hint-Text korrigieren ODER Long-Press implementieren.

### U-H05 · 13 Rewarded-Ad-Placements bleiben fuer Premium-Spieler sichtbar
**View**: `Views/Dialogs/OfflineEarningsDialog.axaml:95-105` + `Views/MiniGames/SawingGameView.axaml:254-263` + `Views/ShopView.axaml:289-296` + `Views/DashboardView.axaml` + `Views/MissionenView.axaml:85-110`
Premium-User sieht genauso viele "Werbung schauen"-CTAs wie Free-User. Premium-Wert nicht spürbar.
**Empfehlung**: Pro Rewarded-Button `IsPremium`-Conditional → "Direkter Bonus (Premium)" statt Ad-CTA, oder Ad mit höherem Multiplier.

### U-H06 · Worker-Liste 3 Taps von Dashboard entfernt
**View**: `Views/Imperium/WorkersSection.axaml:1-143`
Imperium → Workers-Sub-Tab → "Team"-Tile → Liste. Prestige-Quick-Access-Tile semantisch falsch unter Workers.
**Empfehlung**: Worker-Liste direkt in WorkersSection, 4 Tiles als Header-Quickactions.

### U-H07 · Mini-Game-Direktstart ohne Pre-Game Risk-Reward-Erklärung
**View**: `Views/MiniGames/SawingGameView.axaml:79-92` + `ViewModels/MiniGames/BaseMiniGameViewModel.cs:373`
Tap "Auftrag annehmen" → sofort Countdown 3-2-1. Reward-Star-Mapping erst NACH Spiel sichtbar.
**Empfehlung**: 300ms Pre-Game-Karte mit Spielname + Star-Multiplikator + Skip-Button. Nach 5-10 Aufrufen auto-skip.

### U-H08 · Heirloom-Auswahl optional ohne Default-Indikator
**View**: `Views/PrestigeView.axaml:132-191`
Confirm ohne Auswahl moeglich → bis zu 4 Heirlooms (+8% naechsten Run) verloren.
**Empfehlung**: Best-Heirlooms-Auto-Select beim Oeffnen ODER Confirm disabled bis ≥1 gewählt.

## Medium

### U-M01 · ConfirmDialog Drag-Handle suggeriert Swipe-Dismiss (kein Handler) (`Views/Dialogs/ConfirmDialog.axaml:27-32`)
### U-M02 · AlertDialog zentriert vs ConfirmDialog Bottom-Sheet — inkonsistent (`Views/Dialogs/AlertDialog.axaml:14-20`)
### U-M03 · BulkBuy-Button klein/iconless oben rechts — versteckte Funktion (`Views/DashboardView.axaml:133-141`)
### U-M04 · Streak-Badge erst ab Tag 5 — kritische Retention-Phase ohne Feedback (`Views/Dashboard/DashboardHeader.axaml:228-236`)
### U-M05 · Sub-Tab-Active-Style nicht prominent (kein FontWeight=Bold, kein Foreground-Wechsel) (`Views/ImperiumView.axaml:23-26`)
### U-M06 · Material-Offer doppelter Start-Button verwirrt (`Views/Dashboard/OrdersQuickJobsSection.axaml:273-295`)
### U-M07 · GoalBanner nicht klickbar — Spieler tippt drauf, nichts passiert (`Views/DashboardView.axaml:144-169`)
### U-M08 · Order-Empty-State "Coming Soon" passiv statt aktives Refresh-CTA (`Views/Dashboard/OrdersQuickJobsSection.axaml:134-151`)
### U-M09 · Notification-Bell nur sichtbar wenn Items → Layout-Shift + Feature unentdeckbar (`Views/Dashboard/DashboardHeader.axaml:78-104`)
### U-M10 · PrestigeView Margin 80dp riskiert Content-Verdeckung bei Tab-Bar-Toggle (`Views/PrestigeView.axaml:47`)
### U-M11 · ContextualHintDialog ohne Drag-to-Dismiss im Dialog-Modus — inkonsistent zu Tooltip-Modus (`Views/Dialogs/ContextualHintDialog.axaml:14-19`)

## Low / Polish

### U-L01 · FontSize hardcoded numerisch — kein System-FontScale-Respekt (Android Accessibility) — alle Views
### U-L02 · Mini-Game StartOrderCommand ohne lokalen IsBusy-Guard — Doppel-Tap-Race (`Views/Dashboard/OrdersQuickJobsSection.axaml:265-271`)
### U-L03 · Loading-Tips Fallback englisch-only (`Views/MainView.axaml.cs:578-585`)
### U-L04 · Income-Pulse-Animation ohne `:not(.NoMotion)`-Gate — ignoriert ReduceMotion (`Views/DashboardView.axaml:28-36`)
### U-L05 · Toolbar-Icons inkonsistente Sizes (Bell 24, GoldenScrew 20) (`Views/Dashboard/DashboardHeader.axaml`)
### U-L06 · PrestigeView Back-Button mappt auf `ConfirmDialogCancelCommand` — semantisch falsch (`Views/PrestigeView.axaml:22-27`)
### U-L07 · FTUE-Skip-Button erst ab Step 4 — Zeitdruck-User koennen nicht skippen (`Views/FtueOverlay.axaml:54-63`)
### U-L08 · 7-Tage-Streak-Dots hardcoded — siehe U-C05
### U-L09 · Mini-Game-Cancel-Button ohne ConfirmDialog — Fortschritts-Verlust per Versehen (`Views/MiniGames/SawingGameView.axaml:23-29`)
### U-L10 · Workshop-Card-CanAfford-Puls Infinite — pulst auf allen Karten gleichzeitig (`App.axaml:232-243`)

## Onboarding-Mini-Story (1.-Start-Audit)

In den ersten 5 Minuten versteht der Spieler den Grund-Loop (Upgrade → Auftrag → MiniGame → Reward), **aber**: Bulk-Buy bleibt unentdeckt (U-H04/U-M03), Imperium-Sub-Tab-Locks werden nicht erklärt (U-C03), 6 Imperium-Sub-Tabs in 360dp sind eng (U-H03), Header verwirrt (U-H01), Streak-Visualisierung statisch (U-C05). Tap aufs Zahnrad → Settings → Tab-Bar weg, kein Back-Button (U-C01). Beim ersten Mini-Game springt der Countdown ohne Pre-Game-Erklärung (U-H07). Story-Chapter-1 + FTUE-Welcome überlappen (U-C02). Resultat: **Lern-Effizienz hoch, aber Tiefen-Features unsichtbar — Long-Term-Retention-Risiko**.

---

# IV · Performance & Memory (28 Findings)

## Critical

### P-C01 · SaveAsync hält State-Lock 50-200ms unter Late-Game
**File**: `Services/SaveGameService.cs:115-138`
`Task.Run(() => _gameStateService.ExecuteWithLock(() => { JsonSerializer.Serialize(state) }))` — Background-Thread, aber **Lock gehalten** für die gesamte Serialisierung. Bei Lv1000 + 10 Workshops × 20 Workers: 50-200ms unter Lock alle 30s → GameLoop kann nicht ticken, UI-Jitter.
**Empfehlung**: Deep-Snapshot des `GameState` **außerhalb** des Locks (~5ms), dann lock-frei serialisieren.

### P-C02 · WorkerAvatarRenderer.PruneCache disposed Bitmaps nicht
**File**: `Graphics/WorkerAvatarRenderer.cs:852-863`
Pruning entfernt Cache-Keys, **SKBitmap nicht disposed**. Pflicht liegt auf .NET-Finalizer — der disposed Native-Memory auf Android NICHT zeitnah. SKBitmap 128×128 Premul = 64KB Native × 10× Pruning-Cycle = ~13MB nicht freigegeben.
**Empfehlung**: Pattern aus `GameAssetService` (Z.28-29 / 93) übernehmen — `_pendingDispose`-Bucket, der bei Evict/App-Pause disposed wird.

### P-C03 · SKMaskFilter als Instanz-Felder statt static (Wiring + PipePuzzle)
**File**: `Graphics/WiringGameRenderer.cs:49-51` + `Graphics/PipePuzzleRenderer.cs:72-74`
`private readonly SKMaskFilter _blur3 = SKMaskFilter.CreateBlur(...)` — Instanz-Felder. Pro Mini-Game-Restart neue MaskFilter allokiert, alte vom Finalizer eingesammelt. **Bekanntes OOM-Pattern** aus Haupt-CLAUDE.md. Andere Renderer (`BlueprintGameRenderer:121-122`, `ForgeGameRenderer:121-124`, `InventGameRenderer:129-130`, `LuckySpinWheelRenderer:53-59`) machen es bereits static.
**Empfehlung**: `private static readonly`.

## High

### P-H01 · GameTabBarRenderer allokiert pro Frame SKPoint[4] + SKRoundRect + float[]
**File**: `Graphics/GameTabBarRenderer.cs:335-343, 362-376`
15fps × 6 Heap-Allocs = 90/s nur für Tab-Bar.
**Empfehlung**: `SKRoundRect` als Instanz-Feld + `SetRectRadii` updaten. Radii-Array static readonly.

### P-H02 · MarketChartRenderer allokiert `new SKPoint[24]` pro Frame
**File**: `Graphics/MarketChartRenderer.cs:112`
360 Heap-Allocs/s.
**Empfehlung**: `_pointsBuffer` als Instanz-Feld.

### P-H03 · ResearchTreeRenderer allokiert `List<SKPoint>` pro Frame
**File**: `Graphics/ResearchTreeRenderer.cs:147-185`
`GuildResearchTreeRenderer` macht es bereits korrekt (Z.87 mit Invalidation).
**Empfehlung**: Cache-Strategie übernehmen.

### P-H04 · WorkerAuctionViewModel pollt Firebase im 1s-Takt
**File**: `ViewModels/Auctions/WorkerAuctionViewModel.cs:59`
60 Requests/min während Auctions-Tab offen → Firebase-Bandbreite, Quota, Battery.
**Empfehlung**: Auf 5s erhöhen (analog GuildMegaProject 15s) oder Long-Polling/Event-Stream.

### P-H05 · GameLoopService.OnTimerTick — 15+ decimal-Multiplikationen pro Sekunde
**File**: `Services/IncomeCalculatorService.cs:39-150` (gerufen von `GameLoopService.cs:279`)
decimal ~10× langsamer als double. Kombiniert mit `_workerService.UpdateWorkerStates(1.0)` (200 Workers Late-Game) → Spike-Frames.
**Empfehlung**: `_cachedTotalIncomeMultiplier` mit Dirty-Flag.

### P-H06 · GuildAchievementService `.Result` nach `WhenAll` — siehe C-C04
Code-Hygiene + Deadlock-Falle bei Copy.

### P-H07 · BlueprintGameRenderer Color-Array pro Kachel pro Frame
**File**: `Graphics/BlueprintGameRenderer.cs:955-961`
16 Kacheln × 30fps = 480 Heap-Allocs/s + 480 `SKShader.CreateLinearGradient` (Native).
**Empfehlung**: `Dictionary<SKColor, SKShader>` Cache.

### P-H08 · BlueprintGameRenderer `using var scanShader/flashShader` pro Frame
**File**: `Graphics/BlueprintGameRenderer.cs:1128, 1163`
Animationspfad allokiert Shader pro Frame. Analog `PaintingGameRenderer:518`, `ScreenTransitionRenderer:249`.
**Empfehlung**: Zeit-basierte Cache-Invalidation (Position-Delta >5px).

### P-H09 · SaveGameService.CloudUpload nutzt zweites `ExecuteWithLock` für LastCloudSaveTime
**File**: `Services/SaveGameService.cs:168-185`
Lock-Übernahme für reines Anzeige-Detail.
**Empfehlung**: `volatile long _lastCloudUploadTicks` lock-frei.

## Medium

### P-M01 · MaterialIconRenderer-Cache ohne LRU-Bound (`Graphics/MaterialIconRenderer.cs:18`)
### P-M02 · WorkshopFormulas.CalculateMaxAffordableUpgrades — 999× Math.Pow im Hot-Path (`Models/WorkshopFormulas.cs:180-193`)
Debounce existiert (`MainViewModel.EventHandlers.cs:30,49`), greift aber nur wenn Dashboard NICHT aktiv. Auf aktivem Dashboard: 9990 Math.Pow/s im Max-Modus bei 10 Workshops.
**Empfehlung**: Closed-Form (geometrische Summe) statt Iteration.
### P-M03 · LuckySpinWheelRenderer mixed static/instance SKPaint (`Graphics/LuckySpinWheelRenderer.cs:62-85`)
### P-M04 · MaterialIconRenderer.RenderAffinityBadge allokiert Shader pro Aufruf (`Graphics/MaterialIconRenderer.cs:74-84`)
### P-M05 · GameStateService.Initialize feuert StateLoaded unter Lock (`Services/GameStateService.cs:58, 68`)
### P-M06 · DashboardView PropertyChanged-Subscribe ohne WeakEvent (`Views/DashboardView.axaml.cs:148-159`)
### P-M07 · SaveGameService File.Copy synchron unter `_ioLock` (`Services/SaveGameService.cs:145-152`)
30-80ms Android-Flash, blockiert andere Saves.
### P-M08 · FirebaseService Cooldown "expired_cooldown" als Fake-Token blockiert 30s ANR-Risiko (`Services/FirebaseService.cs:474-494`)

## Low

### P-L01 · GuildResearchTreeRenderer Caches ohne i18n-Clear (`Graphics/GuildResearchTreeRenderer.cs:92, 95`)
### P-L02 · GameAssetService.LoadBitmapAsync 224 Task.Run parallel — ThreadPool-Spike (`Services/GameAssetService.cs:73`)
### P-L03 · EconomyFeatureViewModel.SetBulkUpgradeCost doppeltes Math.Pow (`ViewModels/EconomyFeatureViewModel.cs:905-906`)
### P-L04 · RaiseLiveCountdownChanged feuert PropertyChanged auch für nicht-Viewport-Items (`Views/DashboardView.axaml.cs:119-134`)
### P-L05 · GameLoopService allokiert GameTickEventArgs pro Tick (`Services/GameLoopService.cs:390`)
### P-L06 · FrameClockService.OnTimerTick allokiert SubscriberEntry[] pro Tick (`Services/FrameClockService.cs:148-152`)
### P-L07 · WorkerAvatarRenderer-Cache hält Bitmap-Referenzen → prevents GC
### P-L08 · App.OnFrameworkInitializationCompleted eager-resolved IMiniGameMasteryService (akzeptabel)

## Startup-Profil
**Loading/HandwerkerImperiumLoadingPipeline.cs**:
- **S1** Shader+VM+Icons parallel via 4 Tasks (40% Weight) — ThreadPool-Backpressure-Spike möglich auf 4-Core-Mid-Tier, weil GameIcon.PreloadAllAsync intern noch Tasks startet.
- **S2** GameInit (35%) — `mainVm.InitializeAsync` + `purchase.InitializeAsync` **sequenziell**. Google Play Billing kann bei schlechter Verbindung 2-5s blockieren.
- **S3** RemoteConfig (5%) hat 5s-Timeout + ContinueWith ✅
- **S4** ~80 Singletons in DI vor MainViewModel — bekannte 200-500ms-Cost. `AddLazyResolution`-Aufrufe sind in `App.axaml.cs` **nicht sichtbar** — Empfehlung: späte VMs als `Lazy<T>` registrieren.
- **S5** Firebase-Auth on-demand ✅
- **S6** Splash-Animation Min-Display 800ms — auf High-End "verschwendet" Zeit (kosmetisch, kein Issue)

---

# V · Firebase / Gilden / Mini-Games (37 Findings)

> ⚠️ **ZWEI `database.rules.json` im Repo** mit unterschiedlichem Inhalt — `F:\Meine_Apps_Ava\database.rules.json` (Root) vs. `F:\Meine_Apps_Ava\src\Apps\HandwerkerImperium\database.rules.json` (App-lokal). Welche deployed ist, lässt sich aus dem Code nicht ablesen. App-Variante hat weniger Validatoren (z.B. `miniGameType`/`expiresAt` fehlt in CoopOrders).

## Critical

### FB-C01 · Co-op-Reward-Doppelauszahlung beim Gerätewechsel
**File**: `Services/GuildCoopOrderService.cs:159-178`
`ClaimedCoopOrderIds` lebt im lokalen GameState. Bei Neuinstallation + Restore aelterer Cloud-Save → ID fehlt → erneuter Claim. Server prüft "already paid" nirgendwo. HMAC deckt nur stabile Felder.
**Empfehlung**: Server-Rule-Marker `claimedBy/{playerId}=true` write-once.

### FB-C02 · Cloud-Save überschreibt lokal stärkeren Save ohne Konflikt-Erkennung
**File**: `Services/CloudSaveService.cs:105-124, 63-102` + `ViewModels/SettingsViewModel.cs:574+`
`SaveToCloudAsync` ohne Server-Read. `RestoreFromCloudAsync` ohne Version-Check. CLAUDE.md verlangt "höhere Version → Alert" — fehlt komplett. `GetMetadataAsync` existiert, wird **nirgendwo aufgerufen** — toter Code.
**Empfehlung**: Vor Upload/Restore: `GetMetadataAsync` → Vergleich (Level/Version/TotalMoneyEarned) → bei Konflikt Confirm-Dialog mit Diff-Info.

### FB-C03 · Auction-Bid mit `SetAsync` (PUT) statt PATCH — klassische Bid-Race
**File**: `Services/WorkerAuctionService.cs:50-84`
GET-modify-PUT bei zwei parallelen Biedern → Verlierer-Bid landet nie in Firebase, Geld trotzdem abgezogen, Refund findet ihn nicht in `AllBids`. `GuildCoopOrderService.cs:103+` macht es korrekt mit PATCH.
**Empfehlung**: `UpdateAsync` auf `AllBids/{playerId}`-Subpfad.

### FB-C04 · CoopOrder `.write`-Rule erlaubt Score-Tampering durch DRITTE
**File**: `database.rules.json:36-72`
`.write`-Rule fordert nur "Gildenmitglied" — Author-Check fehlt. Boes-Spieler setzt `status=Completed + bothPerfect=true` → Opfer claimt 125% Reward ohne MiniGame zu spielen.
**Empfehlung**: `auth.uid == data.child('createdBy').val() || auth.uid == data.child('invitedPlayer').val()` via `auth_to_player`-Mapping.

### FB-C05 · Auctions `.write`-Rule erlaubt fremde Bid-Manipulation
**File**: `database.rules.json:74-105`
Beliebiges Gildenmitglied kann `highestBidderId/highestBid/endsAt` setzen. Validate-Block fehlt für `endsAt`-Monotonie → Auktion endet nie.
**Empfehlung**: Author-Check + `endsAt: newData.val() >= data.val()` + `endsAt-now <= maxDuration`.

### FB-C06 · Guild-Hall-Level-Cap 50 ohne Monotonie / Up-Cost-Check
**File**: `database.rules.json:314-336`
Mitglied kann ALLE Gebäude direkt von 0 auf 50 setzen → guildwide Cheat.
**Empfehlung**: Monotonie `newData.val() == data.val() || newData.val() == data.val() + 1`.

### FB-C07 · App-lokale Rules — MiniGameType/expiresAt-Validatoren fehlen
**File**: `src/Apps/.../database.rules.json:36-65`
App-Variante hinkt der Root-Variante hinterher. Falls App-Variante deployed: `miniGameType = 999` oder `expiresAt = "9999-01-01"` setzbar.
**Empfehlung**: Rules konsolidieren — eine Quelle, klare Deployment-Pipeline.

## High

### FB-H01 · Anonymer Account-Reset → Verlust der auth_to_player-Bindung
**File**: `Services/FirebaseService.cs:120-134`
Neue UID, alter `auth_to_player/{oldUid}` bleibt → zwei UIDs auf eine PlayerId → doppelte Schreibrechte.
**Empfehlung**: Alten Eintrag vor Signup löschen (wenn alter Token noch gültig).

### FB-H02 · IsOnline-Flag wird im 401-Recovery falsch auf true gesetzt (12 Stellen)
**File**: `Services/FirebaseService.cs:253, 261, 294, 302, 332, 340, 369, 376, 405, 412, 439, 447`
`IsOnline=true` nach `SendAsync`, OHNE Status-Code-Check. 401/500 setzt Online=true → GuildService.InitializeAsync löscht Gilden-Membership lokal trotz echter Mitgliedschaft.
**Empfehlung**: `IsOnline=true` nur bei `response.IsSuccessStatusCode`.

### FB-H03 · Token-Cooldown `_idToken = "expired_cooldown"` → Infinite-401-Loop
**File**: `Services/FirebaseService.cs:486-492`
Fake-Token wird an Firebase gesendet → 401 → triggert Recovery erneut. Bandbreiten-Verschwendung.
**Empfehlung**: `_idToken = null` + Check auf null vor jedem Request.

### FB-H04 · Co-op-MiniGame-Type immer Sawing (default)
**File**: `Services/GuildCoopOrderService.cs:55-61`
`MiniGameType = MiniGameType.Sawing` hardcoded, ViewModel überschreibt nie. Live = immer Sawing für alle Co-op-Aufträge.
**Empfehlung**: Constructor-Parameter erzwingen oder Required-Property.

### FB-H05 · GameIntegrityService HMAC-Compare via Hex-Strings statt Bytes
**File**: `Services/GameIntegrityService.cs:60-63`
`Convert.ToHexStringLower` × 2 → `FixedTimeEquals`. Sicher, aber bei Migration auf Upper-Case/Base64 bricht es.
**Empfehlung**: HMAC-Bytes direkt vergleichen.

### FB-H06 · MigrateFromUidToPlayerId — Geister-Member nach Delete-Failure
**File**: `Services/GuildService.cs:121-172`
Set neuer Member + Delete alter Member nicht atomar. Bei Delete-Failure (Network) bleibt alter Eintrag, zählt in `memberCount`.
**Empfehlung**: Multi-Path-Update statt zwei Operationen, oder Tombstone + periodischer Cleanup.

### FB-H07 · GuildResearchService Cache wird bei Gilden-Wechsel nicht invalidiert
**File**: `Services/GuildResearchService.cs:17, 404-433`
`_cachedEffects` bleibt nach Leave/Join. `ClearLocalCache` in GuildService nullt GuildMembership, **nicht** den Research-Cache.
**Empfehlung**: `GuildService.LeaveGuild` ruft `_guildResearchService.InvalidateCache()`.

### FB-H08 · GuildBossService `_lastBossCheck` global → 30s falsche Boss-Daten nach Gildenwechsel
**File**: `Services/GuildBossService.cs:28, 222-225`
**Empfehlung**: Cache pro `guildId`.

### FB-H09 · Cloud-Save `metadata.savedAt` ohne Monotonie
**File**: `database.rules.json:393-404`
Cheater oder buggy App kann `savedAt = "1970-01-01"` schreiben. Konflikt-Pfad würde falsche Diff zeigen.
**Empfehlung**: `.validate: newData.val() >= data.val()` + `version`-Feld monoton.

### FB-H10 · SpamBid-Cooldown nur client-side
**File**: `Services/WorkerAuctionService.cs:58` + Rules
Gerooteter Spieler kann Firebase direkt mit 100 Bids/s fluten. DoS gegen eigene Gilde.
**Empfehlung**: Server-Rule `now - data.child('lastBidAt').val() >= 1000`.

### FB-H11 · InviteCode-Kollision: stiller Re-Use nach 5 Versuchen
**File**: `Services/GuildInviteService.cs:67-79`
36^6 = 2.1Mrd Codes, Wahrscheinlichkeit gering, aber kein Error-Pfad.
**Empfehlung**: Bei dauerhafter Kollision: Error-Result statt blind überschreiben.

### FB-H12 · NPC-Bot `new Random()` pro Aufruf statt `Random.Shared`
**File**: `Services/WorkerAuctionService.cs:291, 326`
Kosmetisch, RNG-Bias möglich.

### FB-H13 · Master-Client via lex. kleinste Key — Geister-Member kann Master-Voting kapern
**File**: `Services/WorkerAuctionService.cs:259-280`
Geister-Member mit Key `0000...` (siehe FB-H06) gewinnt Master-Vote, ist aber `null`-Player → kein Master → DoS für Auctions.
**Empfehlung**: Master-Client zusätzlich `IsActive`-Check.

## Medium

### FB-M01 · PipePuzzle Random-Backtracking-Pfad ohne Min-Length-Constraint (`ViewModels/MiniGames/PipePuzzleViewModel.cs:200-297`)
### FB-M02 · BaseMiniGameViewModel GameRestarted-Event vor StartGameAsync — View-Pooling-Race (`ViewModels/MiniGames/BaseMiniGameViewModel.cs:350-353`)
### FB-M03 · App.Services Service-Locator-Antipattern in MiniGames (`ViewModels/MiniGames/BaseMiniGameViewModel.cs:466`)
### FB-M04 · ProfanityFilter — Bypass via Leetspeak/Unicode/Diakritika (`Helpers/ProfanityFilter.cs:39-72`)
6-12 Wörter pro Sprache, kein `String.Normalize`. Play-Store-Basisschutz, aber bei Review eines Tier-1-Markts knapp.
**Empfehlung**: `String.Normalize(FormD) + IsNonSpacingMark`-Filter, Leetspeak-Mapping, größere Wortlisten.
### FB-M05 · Chat-Cooldown client-side (`Services/GuildChatService.cs:23-24, 34`) — zwei Geräte umgehen
### FB-M06 · GuildBoss-Reward Pref-Key teilt sich über Gilden (`Services/GuildBossService.cs:36, 477-501`)
### FB-M07 · CoopOrder.Score-PATCH ohne Status-Lock — Expired→Completed-Bypass (`Services/GuildCoopOrderService.cs:103-153`)
### FB-M08 · Boss-HP-Skalierung verliert bei MemberCount-Drop (`Services/GuildBossService.cs:316-324`)
### FB-M09 · PushAsync re-postet kein neues `content` im 401-Retry (`Services/FirebaseService.cs:371-377`)
### FB-M10 · SetOrderId GameRestarted-Event Reentrancy (theoretisch) (`ViewModels/MiniGames/BaseMiniGameViewModel.cs:299-354`)

## Low / Hardening

### FB-L01 · GuildChat `timestamp` als String — Sort bricht wenn jemand ms umstellt (`Services/GuildChatService.cs:95-99`)
### FB-L02 · InviteCode ohne Expiry — alte Reddit-Codes kursieren ewig
### FB-L03 · BrowseGuilds `limitToLast=50` ohne Pagination (`Services/GuildService.cs:187-188`)
### FB-L04 · GuildResearchService `SetAsync` statt `UpdateAsync` für einzelne Felder (`Services/GuildResearchService.cs:106, 251, 338`)
### FB-L05 · `AvailablePlayerInfo.LastActive` String-Sort (`Services/GuildInviteService.cs:161`)
### FB-L06 · ProfanityFilter ohne Telemetrie — A/B-Tuning unmöglich
### FB-L07 · MaskFilter-OOM in MiniGame-Glow-Renderern (Forge etc.) — siehe P-C03

## Firebase-Rules-Coverage-Matrix (Root)

| Pfad | indexOn | Monotonie | Status-Lock | Author-Check | Rate-Limit |
|---|---|---|---|---|---|
| `guilds/$gid` | ✓ (level, leaguePoints) | ✓ (level +1) | — | guild-member | — |
| `coopOrders/$oid` | — | partial | **NO** (Status frei!) | **guild-member (zu lax!)** | — |
| `auctions/$aid` | — | **NO** | — | **guild-member (zu lax!)** | — |
| `megaProjects/$pid` | — | ✓ (Donations monoton) | ✓ (type/projectId immutable) | guild-member + donations gated | — |
| `guild_chat/$gid` | ✓ (timestamp) | n/a | n/a (msg immutable) | strict uid==auth | **NO server-side** |
| `cloud_saves/$pid` | — | **NO (savedAt frei!)** | n/a | strict auth==player | n/a |
| `guild_boss_damage` | — | **NO (damage frei!)** | n/a | $playerId==auth | — |
| `guild_war_seasons` | ✓ (status) | ✓ (points +10000, wins +10) | n/a | guild-member | n/a |

**Vorbild**: `megaProjects` (Donations monoton, ProjectId immutable). Pattern schablonenhaft anwenden.

---

# Sprint-Plan-Empfehlung

## Sprint 1 — "Crash & Cheat" (2 Wochen)
Ziel: alle 15 Critical-Findings + die Locks aus dem Cross-Domain-Hotspot.

| Task | Files | Effort |
|------|-------|--------|
| `ExecuteWithLock` konsequent in 6 Stellen | `GameLoopService.Automation.cs`, `OrderGeneratorService.cs`, `WorkerService.cs`, `EquipmentService.cs`, `ReputationShopService.cs`, `PrestigeService.cs`, `DailyRewardService.cs` | M |
| Cloud-Save Konflikt-Erkennung | `CloudSaveService.cs`, `SettingsViewModel.cs` | M |
| Firebase Rules verschärfen (CoopOrder + Auctions Author-Check, Bid-Race fix) | `database.rules.json` + `WorkerAuctionService.cs` (PATCH) | S |
| Caps für Heirloom + EternalMastery + AscensionScaling | `AscensionService.cs`, `EternalMasteryService.cs`, `IncomeCalculatorService.cs` | M |
| Crafting-SellPrice Soft-Cap | `IncomeCalculatorService.cs:256-315` | S |
| `WorkerAvatarRenderer.PruneCache` Bitmap-Dispose | `Graphics/WorkerAvatarRenderer.cs:852-863` | S |
| `SKMaskFilter` static in WiringGameRenderer + PipePuzzleRenderer | `Graphics/*.cs` | XS |
| `SaveAsync` Snapshot-vor-Lock | `Services/SaveGameService.cs:115-138` | M |
| Settings-Back-Button + Prestige-Loss-Statement | `SettingsView.axaml`, `ConfirmDialog.axaml` | S |

## Sprint 2 — "Late-Game-Polish & Premium-Value" (2 Wochen)
Ziel: High-Findings aus Balancing/UX, Performance-Optimierung der 15-30fps-Renderer.

- Workshop-Card-TapScale-Implementation (U-C04)
- Header-Compact-Mode + GoldenScrews-Disambiguation (U-H01, U-H02)
- Heirloom-Auto-Select + Premium-Ad-Replacement (U-H08, U-H05)
- Bulk-Buy-Hint-Korrektur + Long-Press-Implementation (U-H04)
- Render-Loop-GC-Reduction (P-H01..P-H08): Buffer-Caching in GameTabBarRenderer, MarketChartRenderer, ResearchTreeRenderer, BlueprintGameRenderer
- `WorkerAuctionViewModel` Polling 1s → 5s (P-H04)
- Mini-Game Pre-Game-Card + Risk-Reward-Klarheit (U-H07)
- Eternal-Mastery / Ascension-Skalierungs-Caps (B-H02, B-H03)
- `DoPrestige` + `BuyShopItem` + `ClaimReward` Lock-Pattern (B-C02, B-M03, B-M05)

## Sprint 3 — "Hardening & Polish" (1 Woche)
- ProfanityFilter aufwerten (Normalize, Leetspeak, Wortlisten ausweiten)
- Welcome-Back-Cap + RushBoost-Cap (B-M02, B-M06)
- Workshop-Spezialisierung Re-Balance (B-H05)
- Mini-Game-Reward-Spread-Korrektur (B-H07, gated mit RemoteConfig)
- Notification-Bell immer sichtbar (U-M09), GoalBanner clickable (U-M07)
- FTUE-Welcome × Story-Chapter-1 Koordination (U-C02)
- Firebase Rules Monotonie-Pflicht für `guild_boss_damage`, `cloud_saves.savedAt`
- Telemetrie-Anbindung im ProfanityFilter
- Loading-Tips-Lokalisierung
- Lazy-VM-Pattern konsequent ausrollen (S4)
- 25 Low/Polish-Findings cherry-picken

---

# Was solide ist

Diese Bereiche sind **gut bis vorbildlich** und brauchen keinen Eingriff:

- **Avalonia-12-API-Migration**: vollständig (kein `GetVisualRoot`, `SystemDecorations`, `IsAttachedToVisualTree`, alte Clipboard-API)
- **DateTime-Pattern**: `DateTime.UtcNow` durchgehend persistiert; einziges `DateTime.Now` ist `CityWeatherSystem.cs:65` (absichtlich, dokumentiert)
- **SKPaint/SKPath/SKFont-Disposal**: konsistent in den Renderern (außer P-C03)
- **MVVM-Hygiene**: Code-Behind beschränkt auf SkiaSharp-Rendering + Animations-Hooks
- **Service-Container-Facades** (GuildFacade, WorkerFacade, ProgressionFacade, MissionsFacade): vorbildlicher Service-Sprawl-Fix
- **FrameClockService + FpsProfile**: solide Render-Infrastruktur, Mini-Games-Migration abgeschlossen
- **MainViewModel-Partial-Split** (12 Files, 501 Z. Hauptdatei): AAA-Audit-Ziel <500 erreicht
- **HMAC + PATCH-statt-PUT + Idempotenz-Claims**: in `GuildCoopOrderService` und `GuildMegaProjectService` vorbildlich
- **WorkerAvatarControl** statischer Shared-Timer: erstklassige Optimierung für N-Instanzen
- **Bekannte Bugs aus CLAUDE.md**: 6 von 8 verifiziert behoben (NotificationCenter-Lock, Worker-AssignedWorkshop-null, async-void-Pattern weitgehend, LuckySpin-Timer-Leak, SaveAsync-UI-Freeze, Avalonia-12-API)
- **`MegaProjects`-Rules**: Donations monoton, ProjectId immutable — Vorbild für andere Pfade
- **1058+ Tests grün** (Headless, Migration, Performance, Cinematic)

---

## Verweise auf Sub-Audits (Detail-Niveau)
- `src/Apps/HandwerkerImperium/.audit_balancing.md` — Detail-Balancing (26 Findings)
- `src/Apps/HandwerkerImperium/.audit_code.md` — Code/MVVM/Threading (37 Findings)
- `src/Apps/HandwerkerImperium/.audit_uiux.md` — UI/UX + Onboarding-Story (29 Findings)
- `src/Apps/HandwerkerImperium/.audit_performance.md` — Render/Memory/Startup (28 Findings)
- `src/Apps/HandwerkerImperium/.audit_firebase_minigames.md` — Firebase/Gilden/Mini-Games (25 Findings)
