# HandwerkerImperium (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Idle-Game: Baue dein Handwerker-Imperium auf, stelle Mitarbeiter ein, kaufe Werkzeuge, erforsche Upgrades, schalte neue Workshop-Typen frei. Verdiene Geld durch automatische Auftraege oder spiele Mini-Games.

**Version:** 2.0.14 (VersionCode 22) | **Package-ID:** com.meineapps.handwerkerimperium | **Status:** Geschlossener Test

## Haupt-Features

- **10 Workshop-Typen** (Schreiner, Klempner, Elektriker, Maler, Dachdecker, Bauunternehmer, Architekt, Generalunternehmer, Meisterschmiede, Innovationslabor)
- **10 Mini-Games** (Sawing, Pipe Puzzle, Wiring, Painting, RoofTiling, Blueprint, DesignPuzzle, Inspection, ForgeGame, InventGame)
- **Worker-System** mit 10 Tiers (F/E/D/C/B/A/S/SS/SSS/Legendary), Avatare, Training, Ausruestung
- **Goldschrauben-Economy** (Premium-Waehrung fuer Boosts/Unlock)
- **Research Tree** (45 Upgrades in 3 Branches: Tools, Management, Marketing)
- **7 Gebaeude** (Canteen, Storage, Office, Showroom, TrainingCenter, VehicleFleet, WorkshopExtension)
- **Daily Challenges** (3/Tag) + **Weekly Missions** (5/Woche, 50 Goldschrauben Komplett-Bonus)
- **Daily Login Rewards** (30-Tage-Zyklus) + **Streak-Rettung** (5 Goldschrauben)
- **Achievements** (94 Erfolge) + **Milestone-Celebrations** (Spieler-Level + Workshop-Level)
- **Prestige-System** (7 Stufen Bronze-Legende, progressive Bewahrung, Hard-Cap bei 200x)
- **Events** (8 zufaellige + saisonaler Multiplikator, Intervall skaliert mit Prestige)
- **Auftragstypen** (Standard/Large 1.8x/Weekly 4x/Cooperation 2.5x) + **Stammkunden** (bis 1.5x Bonus)
- **Bulk Buy** (x1/x10/x100/Max) + **Hold-to-Upgrade** (schnelles Hochleveln)
- **Naechstes-Ziel-System** (GoalService: dynamischer Gold-Banner, 4 Prioritaeten)
- **Offline-Earnings** (100% erste 2h, 50% bis 6h, 25% danach)
- **Feierabend-Rush** (2h 2x-Boost, 1x taeglich gratis, danach 10 Goldschrauben)
- **Meisterwerkzeuge** (12 Artefakte, 5 Seltenheiten, passive Einkommens-Boni)
- **Lieferant-System** (Variable Rewards alle 2-5 Min: Geld, Schrauben, XP, Mood, Speed)
- **Prestige-Shop** (22 Items in 4 Kategorien) + **Prestige-Pass** (2,99 EUR IAP, +50% Prestige-Punkte)
- **Story-System** (25 Kapitel von NPC "Meister Hans" mit SkiaSharp-Portrait)
- **Kontextuelles Tutorial** (17 Tooltip-Bubbles + Welcome-Dialog, ContextualHintService, SeenHints-Tracking)
- **In-App Review** (Level 20/50/100, erstes Prestige, 50 Auftraege)
- **Benachrichtigungen** (4 Typen, AlarmManager + BroadcastReceiver, BootReceiver, 6 Sprachen)
- **Google Play Games** (Leaderboards, kein Cloud-Save im NuGet v121.0.0.2)
- **Audio + Haptik** (15 Sounds via SoundPool, 7 Vibrations-Muster, Hintergrundmusik)
- **Vorarbeiter-System** (14 Manager, Lv.1-5, Workshop-Boni)
- **Turniere** (Woechentlich, 9 simulierte Gegner, 3x/Tag gratis)
- **Battle Pass** (30 Tiers, Free/Premium Track, 30-Tage-Saisons)
- **Saisonale Events** (4/Jahr mit Saisonwaehrung und Event-Shop)
- **Gilden/Innungen** (Firebase Realtime Database, Wochenziele, 18 Forschungen mit Timer+Auto-Completion, Einladungs-Inbox mit Accept/Decline)
- **Crafting-System** (13 Rezepte in 3 Tiers, Inventar + Verkauf)
- **Automatisierung** (Auto-Collect Lv15+, Auto-Accept Lv25+, Auto-Assign Lv50+, Auto-ClaimDaily Premium)
- **Welcome Back Angebote** + **Gluecksrad** (taeglich gratis)
- **Ausruestungs-System** (4 Typen x 4 Seltenheiten fuer Arbeiter)
- **Isometrische Weltkarte** (Full-Screen 2.5D SkiaSharp, 8x8 Diamond-Grid, Kamera, Radial-Menue, Partikel, Tag/Nacht, Wetter)

## Premium & Ads

### Premium-Modell
- **Preis**: 4,99 EUR (Lifetime)
- **Vorteile**: +50% Einkommen, +100% Goldschrauben aus Mini-Games, keine Werbung

### Rewarded (9 Placements)
1. `golden_screws` - 5 Goldschrauben (Dashboard)
2. `score_double` - Mini-Game Score verdoppeln
3. `market_refresh` - Arbeitermarkt-Pool neu wuerfeln
4. `workshop_speedup` - 2h Produktionsertrag sofort
5. `workshop_unlock` - Workshop ohne Level freischalten
6. `worker_hire_bonus` - +1 Worker-Slot persistent
7. `research_speedup` - Forschung sofort fertig gratis
8. `daily_challenge_retry` - Challenge-Fortschritt zuruecksetzen
9. `achievement_boost` - Achievement Progress +20%

## Architektur-Besonderheiten

### Dispose / Memory Leak Prevention

`App.DisposeServices()` gibt alle IDisposable-Singletons frei (IGameLoopService, GameJuiceEngine, MainViewModel, IFirebaseService).
- **Desktop**: `desktop.ShutdownRequested += (_, _) => DisposeServices();`
- **Android**: `MainActivity.OnDestroy()` ruft `App.DisposeServices()` als ERSTE Zeile auf (vor AdMob-Dispose)
- Pattern identisch mit BomberBlast

### MainViewModel Partial-Class-Split

MainViewModel ist in 6 partielle Dateien aufgeteilt:

| Datei | Inhalt |
|-------|--------|
| `MainViewModel.cs` | Felder, Constructor, ~120 ObservableProperties, Event-Handler, GameTick, Dispose |
| `MainViewModel.Navigation.cs` | Tab-Auswahl, NavigateTo-Commands, HandleBackPressed, MiniGame-Navigation |
| `MainViewModel.Dialogs.cs` | Alert/Confirm, Prestige-Bestaetigung, Story-Dialog, Kontextuelle Hints |
| `MainViewModel.Economy.cs` | Workshop-Kauf/Upgrade, Auftraege, Rush, Lieferant, BulkBuy, Hold-to-Upgrade |
| `MainViewModel.Missions.cs` | Weekly Missions, Welcome-Back, Lucky Spin, Streak-Rettung, Quick Jobs, Daily Challenges, Meisterwerkzeuge |
| `MainViewModel.Init.cs` | InitializeAsync, Cloud-Save, Offline-Earnings, Daily Reward |

**Konventionen**: Jede Datei hat eigene `using`-Direktiven + `namespace HandwerkerImperium.ViewModels;` + `public partial class MainViewModel`. Event-Handler fuer BuildingsViewModel.FloatingTextRequested als benanntes Delegate-Feld mit korrektem Unsubscribe in Dispose().

### Dialog-UserControls (Views/Dialogs/)

MainView-Dialoge in eigenstaendige UserControls extrahiert (reduziert MainView.axaml um ~650 Zeilen):
`OfflineEarningsDialog`, `DailyRewardDialog`, `WelcomeBackOfferDialog`, `AchievementDialog`, `ContextualHintDialog` (Tooltip-Bubble/Dialog, ersetzt TutorialDialog), `StoryDialog` (Hans-Blinzel-Animation via StoryDialogControl.UpdateHansAnimation()), `AlertDialog`, `ConfirmDialog`, `WorkerProfileDialog`.
Alle erben `DataContext="{Binding}"` vom MainViewModel. Backdrop-Dismiss im Code-Behind wo noetig.

### 5-Tab Navigation

| Tab | Index | View | Inhalt |
|-----|-------|------|--------|
| Werkstatt | 0 | IsometricWorldView | 2.5D-Weltkarte, Radial-Menue, Partikel, Tag/Nacht, Wetter |
| Imperium | 1 | ImperiumView | Prestige-Banner, Gebaeude, Crafting+Research, Workers/Manager/MasterTools |
| Missionen | 2 | MissionenView | Daily Challenges, Weekly Missions, Turnier/BattlePass/SaisonEvent/Erfolge, Gluecksrad |
| Gilde | 3 | GuildView | Guild-Hub, Research/Members/Invite Sub-Seiten |
| Shop | 4 | ShopView | IAP, Goldschrauben-Pakete, Ausruestungs-Shop |

**Tab-Bar Badges**: Tab 0 (HasPendingDelivery+CanActivateRush), Tab 1 (HasWorkerWarning), Tab 2 (ClaimableMissionsCount+HasFreeSpin). SkiaSharp-Tab-Bar (GameTabBarRenderer, 64dp).

### Dashboard-UserControls (Views/Dashboard/)

`DailyChallengeSection`, `WeeklyMissionSection`, `BannerStrip` - erben DataContext vom Parent (MainViewModel). PaintSurface-Handler nutzen `IProgressProvider`-Interface.

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

Navigation via `NavigationRequested` Events. Sub-VM-Events werden an GuildViewModel propagiert. Zurueck-Navigation (".." oder Android-Back) fuehrt zum Guild-Hub.

### Isometrische Weltkarte (Graphics/IsometricWorld/)

Full-Screen 2.5D-Weltkarte als zentraler Game-Hub (Tab 0). Komplett SkiaSharp-basiert, 20fps Render-Loop.

**8 Dateien:**

| Datei | Zweck |
|-------|-------|
| `IsoGridHelper.cs` | IsoToScreen/ScreenToIso, TileWidth=96f, TileHeight=48f, 8x8 Grid, Painter's Algorithm |
| `IsoCameraSystem.cs` | Pan mit Inertia, PinchZoom (0.5x-2.0x), FocusOnBuilding (animiert), Bounds-Clamping |
| `IsoTerrainRenderer.cs` | 4 Gruentoene, Weg-Farben nach WorldTier, wind-animiertes Gras, Dekorationen |
| `IsoBuildingRenderer.cs` | 10 Workshop-Typen (25+ Draw-Calls), 7 Support-Gebaeude, Level-Rahmen. **0 GC/Frame**: 18 static readonly SKPath mit Rewind() |
| `IsoParticleManager.cs` | 300-Partikel Struct-Pool (0 GC), 10 ParticleTypes, LCG Pseudo-Random, lock-basiert |
| `IsoRadialMenu.cs` | 4 Aktionen (Upgrade/Workers/MiniGame/Info), EaseOutBack, 6 static readonly SKPaint, HitTest |
| `IsometricWorldRenderer.cs` | 9-Stage Render-Pipeline, Shader-Cache, GridCell Struct, HitTest |
| `EasingFunctions.cs` | EaseOutCubic/Back/Elastic/Bounce, Spring, Lerp, SmoothStep, PingPong |

**Grid-Layout (8x8 Diamond):**
- Workshop-Positionen: (2,2)/(4,2)/(6,2)/(1,4)/(3,4)/(5,4)/(7,4)/(2,6)/(4,6)/(6,6)
- Building-Positionen: (0,1)/(1,1)/(3,1)/(5,1)/(7,1)/(0,3)/(7,6)

**Render-Pipeline (9 Stufen):** Sky-Gradient → Camera-Transform → Terrain → Buildings (Painter's Algorithm) → Partikel → Camera-Reset → Wetter-Overlay → Radial-Menue → HUD

**Touch-Handling:**
- Pan: Drag mit >10px Schwelle, Camera.Pan(dx, dy) mit Inertia
- Tap: <10px + <300ms → HitTest → Workshop (Radial-Menue), Building (Imperium-Tab), RadialMenu-Aktion (Upgrade bleibt offen). Screen-Space HitTest: Klick innerhalb Menue-Backdrop ohne Button-Treffer → ignoriert
- Desktop-Mausrad-Zoom: ScrollZoomFactor=1.1f, zoomt Richtung Mausposition

**Integration mit MainViewModel:**
- `UpgradeWorkshopSilent(type)`, `NavigateToWorkshopFromCity(type)`, `SelectWorkerMarketTabCommand`, `SelectBuildingsTabCommand`
- `FloatingTextRequested` Event (money=gruen, xp/golden_screws=gold, level=craft-orange)
- `GetGameStateForRendering()` fuer Renderer

### Game Loop

- **GameLoopService** (1s-Takt via DispatcherTimer) → Idle-Einkommen, Kosten, Worker-States, Research-Timer, Event-Check
- **AutoSave** alle 30 Sekunden → GameState → JSON via SaveGameService
- **Research-/Gebaeude-Effekte** werden pro Tick angewendet

### Workshop-Typen

Enum: `Carpenter`, `Plumber`, `Electrician`, `Painter`, `Roofer`, `Contractor`, `Architect`, `GeneralContractor`, `MasterSmith`, `InnovationLab`
Jeder Typ hat: `BaseIncomeMultiplier`, `UnlockLevel`, `UnlockCost`, `RequiredPrestige`
**Spezial-Effekte**: MasterSmith produziert passiv Crafting-Materialien, InnovationLab verdoppelt Research-Geschwindigkeit

### Worker-System

10 Tiers: `F` (0.4x), `E` (0.65x), `D` (1.0x), `C` (1.5x), `B` (2.25x), `A` (3.35x), `S` (4.9x), `SS` (7.25x), `SSS` (11.25x), `Legendary` (17.5x)
Loehne ~1.8x pro Tier (5-900 EUR/h). ROI sinkt ~15%/Tier.
`HireWorker()` → individuelle Marktpreise: Tier-Basis * Level * Talent (0.7-1.3x) * Persoenlichkeit * Spezialisierung * Effizienz-Position. A+ Tiers kosten zusaetzlich Goldschrauben.
Tier-Farben: F=Grau, E=Gruen, D=Teal, C=DarkOrange, B=Amber, A=Rot, S=Gold, SS=Lila, SSS=Cyan, Legendary=Rainbow
**3 Training-Typen**: Efficiency (XP→Level→+Effizienz), Endurance (senkt Fatigue, max -50%), Morale (senkt MoodDecay, max -50%)
**Worker-Avatare**: WorkerAvatarRenderer (Pixel-Art), Worker.IsFemale deterministisch, RarityFrameRenderer (Tier→Rarity), Idle-Animationen (Atem+Blinzeln ab 56dp)

### Goldschrauben-Quellen

1. Mini-Games (3-10), 2. Daily Challenges (20), 3. Achievements (5-50), 4. Rewarded Ad (5), 5. IAP (100/500/2000), 6. Daily Login (1-25), 7. Spieler-Meilensteine (3-200), 8. Workshop-Meilensteine (2-50)

### Research Tree

45 Upgrades in 3 Branches a 15 Level: Tools (Effizienz + MiniGame-Zone), Management (Loehne + Worker-Slots), Marketing (Belohnungen + Order-Slots)
Kosten: 500 bis 1B. Dauer: 10min bis 72h (Echtzeit).
**UI**: 2D-Baum-Layout mit 6 SKCanvasViews (Header, ActiveResearch, Tabs, BranchBanner, Tree, Celebration). 20fps Render-Loop.
**Renderer**: ResearchTreeRenderer, ResearchIconRenderer (12 Icons), ResearchActiveRenderer, ResearchBranchBannerRenderer, ResearchTabRenderer, ResearchCelebrationRenderer, ResearchLabRenderer.

### Mini-Games (alle SkiaSharp-basiert)

Alle 10 Mini-Games nutzen dedizierte SkiaSharp-Renderer. Header, Result-Display, Countdown und Buttons bleiben XAML. Jeder Renderer hat `Render()` + `HitTest()`, View hat 20fps Render-Loop, Touch via `PointerPressed` + DPI-Skalierung.
**Tutorial-System**: Erstes Spielen zeigt Overlay (Tracking via `GameState.SeenMiniGameTutorials`).
**Belohnungsanzeige**: NUR bei letzter Aufgabe als Gesamt-Belohnung. Berechnung: `order.FinalReward * GetOrderRewardMultiplier(order)` (inkl. Research, Gebäude, Reputation, Events, Stammkunden). PaintingGame zusätzlich `* comboMult`. Rewarded-Ad setzt `order.IsScoreDoubled = true`, PaintingGame setzt `order.ComboMultiplier`. `CompleteActiveOrder()` wendet beides bei Auszahlung an.
**Ergebnis-Animation**: Zwischen-Runden sofort, letzte Runde staggered (100ms Delay, 250ms Duration).
**Dashboard-Belohnung**: Bindet an `EstimatedReward`/`EstimatedXp` (inkl. Difficulty + OrderType, mit "~"-Präfix).

| MiniGame | Renderer | Besonderheit |
|----------|----------|-------------|
| Sawing | SawingGameRenderer | Holzbrett mit Bezier-Maserung, Schnitt-Animation, Saegemehl+Splitter-Partikel |
| Pipe Puzzle | PipePuzzleRenderer | Metall-Rohre, progressive Wasser-Durchfluss-Animation (BFS), Blasen+Splash |
| Wiring | WiringGameRenderer | Sicherungskasten, Bezier-Kabel, elektrische Pulse (SKPathMeasure) |
| Painting | PaintingGameRenderer | Putzwand, Pinselstrich-Textur, Farbspritzer, Combo-Badge |
| Blueprint | BlueprintGameRenderer | Blaupausen-Grid, Circuit-Verbindungen, Memorisierungs-Scan-Linie |
| RoofTiling | RoofTilingRenderer | Holz-Dachstuhl, 3D-Ziegel, Platzierungs-Funken |
| DesignPuzzle | DesignPuzzleRenderer | Architektenplan, Tuer-Oeffnungen, Grundriss-Glow |
| Inspection | InspectionGameRenderer | Beton-Baustelle, pulsierende Lupe, 16 Vektor-Icons (8 gut+8 defekt) |
| ForgeGame | ForgeGameRenderer | Amboss+Esse, Temperatur-Zonen, Hammer-Schlag-Animation |
| InventGame | InventGameRenderer | Violettes Puzzle, 12 Bauteil-Icons, Circuit-Pulse entlang Verbindungen |

Alle Renderer: Struct-basierte Partikel (kein GC), 20fps Render-Loop.

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| `GameLoopService` | 1s-Takt: Einkommen, Kosten, Worker-States, AutoSave (30s) |
| `GameStateService` | Zentraler State mit Thread-Safety (lock), GetOrderRewardMultiplier() |
| `SaveGameService` | JSON-Persistenz (Load/Save/Import/Export/Reset) |
| `WorkerService` | Mood, Fatigue, Training, Ruhe, Kuendigung, ReinstateWorker |
| `PrestigeService` | 7-Tier Prestige + Shop-Effekte + progressive Bewahrung |
| `ResearchService` | 45 Research-Nodes, Timer, Effekt-Berechnung |
| `EventService` | 8 Event-Typen + saisonaler Multiplikator |
| `DailyChallengeService` | 3 Challenges/Tag (00:00 Reset) |
| `DailyRewardService` | 30-Tage Login-Zyklus |
| `QuickJobService` | Schnelle MiniGame-Jobs (Rotation 8-15min, Limit 20-40/Tag) |
| `StoryService` | 25 Kapitel von Meister Hans, fortschrittsbasiert |
| `AchievementService` | 94 Erfolge + Goldschrauben-Rewards, PrestigeCompleted-Event |
| `OfflineProgressService` | Offline-Einnahmen (Staffelung 100%/50%/25%) |
| `GoalService` | Dynamisches Naechstes-Ziel-System (4 Prioritaeten, Cache mit Dirty-Flag) |
| `OrderGeneratorService` | 4 OrderTypes, Stammkunden-Zuweisung, Reputation beeinflusst Qualitaet |
| `ReviewService` | In-App Review (14-Tage Cooldown, 5 Trigger) |
| `AudioService` | SoundPool (15 Sounds), Vibrator (7 Muster), MediaPlayer (Musik). Factory-Pattern |
| `NotificationService` | 4 Typen, AlarmManager+BroadcastReceiver, BootReceiver, 6 Sprachen |
| `PlayGamesService` | Leaderboards, kein Cloud-Save (NuGet-Limitation). Factory-Pattern |
| `ManagerService` | 14 Vorarbeiter: Unlock/Upgrade (Lv.1-5), Workshop-Boni |
| `TournamentService` | Woechentliche MiniGame-Turniere, 9 simulierte Gegner |
| `BattlePassService` | 30-Tier Battle Pass, Free/Premium, 30-Tage-Saisons |
| `SeasonalEventService` | 4 Events/Jahr (Mär/Jun/Sep/Dez, 1.-14.), SP-Waehrung (5+Bonus pro Auftrag), Event-Shop (4 Items/Saison), IDisposable (OrderCompleted-Subscription) |
| `GuildService` | Firebase REST API, Gilden-CRUD, Wochenziele, Einladungen, GetMaxMembers() |
| `GuildResearchService` | 18 Gilden-Forschungen (6 Kategorien), Timer, Effekt-Cache, SemaphoreSlim |
| `GuildWarSeasonService` | Gilden-Krieg Saison-System (Matchmaking, Scoring, Ligen) |
| `GuildHallService` | Interaktives Gilden-HQ mit 10 Gebäuden (Level 1-5), Upgrade-Timer, Effekt-Cache |
| `GuildBossService` | Kooperative Gilden-Bosse (6 Typen), Schaden-Tracking, Spawn/Despawn, Belohnungen |
| `GuildTipService` | Kontextuelle Gilden-Tipps (Preferences-basiert, 24h Cooldown) |
| `GuildAchievementService` | 30 Gilden-Achievements (10 Typen x 3 Tiers), Firebase-Tracking |
| `FirebaseService` | Anonymous Auth, Token-Refresh (55min), CRUD, 5s Timeout, SemaphoreSlim |
| `CraftingService` | 13 Rezepte in 3 Tiers, Produktionsketten, Echtzeit-Timer |
| `WeeklyMissionService` | 5 Wochenmissionen, Montag-Reset, 50 Goldschrauben Bonus |
| `WelcomeBackService` | Angebote nach 24h+ Abwesenheit, Starter-Paket (einmalig) |
| `LuckySpinService` | Taeglicher Gratis-Spin, 8 Preiskategorien (gewichtet) |
| `EquipmentService` | 4 Typen x 4 Seltenheiten, Drop nach MiniGames, Shop-Rotation |

## Game Juice (kompakte Uebersicht)

| Feature | Beschreibung |
|---------|-------------|
| Workshop Cards | Farbiges Border + WorkshopCardRenderer (10 thematische Szenen, 48dp) |
| Worker Avatars | Pixel-Art (6 Hauttoene/Haare/Kleidung, Tier-Farbe+Sterne, Mood, RarityFrame) |
| Meister Hans Portrait | 4 Stimmungen, Idle-Bobbing, Blinzel-Animation, 120x120 |
| Golden Screw Icon | Gold-Shimmer CSS-Animation (scale+rotate) |
| Level-Up | XP-Bar Puls, CelebrationOverlay + Sound bei Meilensteinen |
| Income FloatingText | Gruener Text, +100px, 1.5s |
| TapScale-Effekt | Globale CSS-Styles: scale(0.95) bei :pressed, 80ms CubicEaseOut |
| Tab-Bar CraftTextures | CraftTextures.DrawWoodGrain() mit Holz-Maserung |
| Combo Badge | Gold-Badge mit Fire-Icon bei Combo >= 3 (PaintingGame) |
| Bottom Sheets | CSS translateY(800→0px), CubicEaseOut |
| Hold-to-Upgrade | DispatcherTimer 120ms, stilles Upgrade, Zusammenfassung am Ende |
| Tab-Wechsel | FadeIn 150ms CubicEaseOut |
| Workshop-Szenen | WorkshopSceneRenderer: 10 ikonische Szenen mit Level-Visuals (Glow Lv50+, Sterne Lv250+, Premium-Aura Lv500+, Shimmer Lv1000) |
| Workshop-Interieur | WorkshopInteriorRenderer: Gradient+Boden+Vignette+Wand-Details, Ambient-Partikel |
| MiniGame Result | Staggered Stars, Rating-Farbe, Border-Pulse, MiniGameEffectHelper |
| MiniGame Countdown | Pulsierendes 3-2-1-GO! Overlay |
| Muenz-Partikel | Goldene Coin-Partikel im City-Header via AnimationManager |
| Money-Display Flash | Opacity-Flash 400ms bei Geld-Einnahmen |
| Confetti | AddLevelUpConfetti bei Level-Up und Goldschrauben-Events |
| Offline-Earnings Burst | FloatingText "money" → Muenz-Partikel + Money-Flash |
| GameJuiceEngine | Zentrale Effekt-Engine: ScreenShake, RadialBurst, CoinsFlyToWallet, SparkleEffect etc. Struct-Pool (max 200) |
| OdometerRenderer | Animierte Geld-Anzeige mit rollenden Ziffern, Suffix-Crossfade, Gold-Flash |
| CoinFlyAnimation | 8-16 Muenzen auf Bezier-Kurven, Euro-Praegung, HUD-Pulse bei Ankunft |
| SkiaShimmerEffect | GPU-Shimmer auf Goldschrauben-Bereich (permanent wenn > 0) |
| City 2.5D | CityRenderer: 5-Layer Parallax, isometrische Gebaeude, Tag/Nacht, 8 Welt-Stufen |
| City Buildings | CityBuildingShapes: Workshop-Farben, Dach-Details, Fenster-Blinken, Level-Rahmen, Mini-Arbeiter |
| City Weather | CityWeatherSystem: Regen+Regenbogen, Sonne+Shimmer, Blaetter, Schnee (80 Struct-Pool) |
| City Progression | CityProgressionHelper: Strassen-Upgrade, Dekorationen (Buesche/Baeume/Laternen/Baenke/Brunnen) |
| City Touch | Tap auf Workshop → RadialBurst + TapLabel + Navigation. Tap auf Gebaeude → Imperium-Tab |
| City Details | Workshop-Mini-Icons, Spotlight+Fahnen, Lieferwagen+Fussgaenger, Fenster-Glow, Laternen-Lichtkegel, Wolken-Schatten, Sonnenauf-/Untergang, Boden-Wetter |
| Reward-Zeremonie | Full-Screen Overlay: Scale-In, Confetti (120), Feuerwerk, 5 CeremonyTypes, 4s Tap-to-Dismiss |
| Loading-Screen | Zahnraeder, Funken-Partikel, Gradient-Fortschrittsbalken, rotierende Tipps |
| Splash-Screen | "Die Schmiede": Zahnraeder, Amboss, Hammer-Animation, Glut-Partikel |
| Gluecksrad | LuckySpinWheelRenderer: 8 Segmente, Nieten-Rand, SkiaSharp-Icons, Spin-Animation ~60fps |
| Iso-Weltkarte | 2.5D 8x8 Diamond-Grid, 10 Workshop-Gebaeude, Kamera, Radial-Menue, Partikel, Tag/Nacht |
| Gilden-Forschungsbaum | 18 Items, Bezier-Verbindungen, Flow-Partikel, GuildHallHeader (Steinmauer, Fackeln, Emblem), Node-Namen+Kosten/Effekt-Labels, Lock-Badges, Drop-Shadow, Inner-Highlight |
| Research-Labor | ResearchLabRenderer: Werkstatt-Szene, Zahnraeder, Dampf, Gluehbirne |
| Research-Baum | 2D Top-Heroes-Style, Branch-Farben, Flow-Partikel, Branch-Banner, Celebration-Confetti |
| Forschungs-Hintergrund | ResearchBackgroundRenderer: Nussholz, Holzmaserung, Zahnrad-Wasserzeichen, Vignette |

## Farbkonsistenz (Craft-Palette)

- **Buttons**: Immer Craft-Orange/Braun via App.axaml Style-Overrides (keine `{DynamicResource PrimaryBrush}`)
- **Workshop-Farben**: Carpenter=#A0522D, Plumber=#0E7490, Electrician=#F97316, Painter=#EC4899, Roofer=#DC2626, Contractor=#EA580C, Architect=#78716C, GeneralContractor=#FFD700, MasterSmith=#D4A373, InnovationLab=#6A5ACD
- **Tier-Farben**: F=Grau, E=Gruen, D=#0E7490, C=#B45309, B=Amber, A=Rot, S=Gold
- **Branch-Farben**: Tools=#EA580C, Management=#92400E, Marketing=#65A30D
- **Feature-Farben** (App.axaml): Tournament=#DC2626, SeasonalEvent=#059669, BattlePass=#7C3AED, MasterSmith=#B91C1C, InnovationLab=#6D28D9
- **Overlay-Farben**: DialogOverlay=#AA000000, RewardOverlay=#CC000000
- **Hardcodierte Farben**: Alle in ~30 Views durch CraftXxxBrush ersetzt. Ausnahme: Alpha-Kanal + GradientStop bleiben hardcodiert

## IsBusy-Pattern

`private bool _isBusy` + try/finally Guard in GuildVM, SettingsVM, ShopVM, WorkerMarketVM fuer alle async-Methoden.

## Daily Challenge Tracking

- `MiniGameResultRecorded` Event auf `IGameStateService` → `DailyChallengeService` subscribt
- Score-Mapping: Perfect=100%, Good=75%, Ok=50%, Miss=0%

## Reputation-System

- **CustomerReputation** (0-100, Start 50): Beeinflusst Auftragsbelohnungen (0.7x-1.5x)
- **AddRating()** bei Auftragsabschluss (MiniGame-Rating → 1-5 Sterne)
- **Showroom-Gebaeude**: Passive Reputation-Steigerung (0.5-2.5/Tag)
- **DecayReputation()**: Langsamer Abbau >50 (1/Tag)
- **ExtraOrderSlots**: >=70 → +1, >=90 → +2
- **OrderQualityBonus**: <30 → -10%, >=80 → +20%

## Auftragstypen (OrderType)

| Typ | Freischaltung | Belohnung | Besonderheit |
|-----|---------------|-----------|-------------|
| Standard | Immer | 1.0x | Basis |
| Large | WS-Level 10+ | 1.8x | Mehr Aufgaben |
| Cooperation | WS-Level 15+, >=2 Workshops | 2.5x | Gemischte Aufgaben |
| Weekly | WS-Level 20+ | 4.0x | 7-Tage-Deadline |

- **Stammkunden**: 20% Chance, BonusMultiplier 1.1-1.5x, max 20
- **Abgelaufene Orders**: GameLoop prueft alle 60 Ticks

## Event-Mechanik

- **AffectedWorkshop**: HighDemand/MaterialShortage betreffen zufaelligen Workshop-Typ
- **MarketRestriction**: WorkerStrike → nur Tier C und niedriger
- **Intervall-Skalierung**: Kein Prestige 8h/30%, Bronze 6h/35%, Silver 4h/40%, Gold+ 3h/50%
- **TaxAudit**: 10% Steuer auf Brutto (dauerhaft waehrend Event)
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
| Logistik | guild_logistics_1/2/3 | 75M-3B | +1 Auftragsslot, +15% Order-Qualitaet, +20% Auftragsbelohnungen |
| Arbeitsmarkt | guild_workforce_1/2/3 | 150M-5B | +1 Worker-Slot, +25% Training-Speed, -20% Ermuedung/Stimmung |
| Meisterschaft | guild_mastery_1/2 | 500M/7.5B | +20% Forschungs-Speed, +10% Prestige-Punkte |

**Gesamtkosten**: ~37,4 Mrd. EUR | **Linear pro Kategorie**

### Firebase-Datenstruktur

`/guild_research/{guildId}/{researchId}` → `{ progress: long, completed: bool, completedAt: string?, researchStartedAt: string? }`
`/guild_invites/{guildId}/{recipientUserId}` → `{ senderId, senderName, guildId, guildName, sentAt }`

### Effekt-Integration (14 Effekt-Typen)

Effekte ueber `GuildMembership`-Properties gecacht:
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
- `Models/GuildHall.cs`: Hauptquartier (GuildBuildingState, GuildBuildingCost, GuildBuildingDefinition mit 10 Gebaeuden, GuildBuildingDisplay, GuildHallEffects)
- `Models/GuildAchievement.cs`: Achievements (GuildAchievementState, GuildAchievementDefinition mit 30 Achievements = 10 Typen x 3 Tiers, GuildAchievementDisplay)
- `Models/Guild.cs`: GuildMembership +14 Research-Properties + ApplyResearchEffects() + 6 Hall-Properties + ApplyHallEffects() + guildHallLevel + leagueId
- `Models/Firebase/FirebaseGuildData.cs`: +maxMembers, leagueId, leaguePoints, hallLevel, description
- `Models/Firebase/FirebaseGuildMember.cs`: +lastActiveAt, weeklyWarScore, totalWarScore
- `Models/Firebase/GuildWar.cs`: +guildALevel, guildBLevel, phase, phaseEndsAt

#### Services & Views
- `Services/GuildService.cs`: Gilden-CRUD, Wochenziele, Einladungen (SendInvite, AcceptInvite, DeclineInvite), GetMaxMembers(). Research-Logik nach GuildResearchService extrahiert
- `Services/GuildResearchService.cs`: Extrahierte Research-Logik (IGuildResearchService). GetGuildResearchAsync(), ContributeToResearchAsync(), CheckResearchCompletionAsync(), GetCachedEffects(), RefreshResearchCacheAsync(). SemaphoreSlim Thread-Safety
- `Services/GuildWarSeasonService.cs`: Saison-basierter Gilden-Krieg (Matchmaking, Scoring, Ligen-Auf/Abstieg, Bonus-Missionen)
- `Services/GuildHallService.cs`: 10 Gebäude mit Upgrade-Timer (1-12h), Kosten (GS+Gildengeld), Effekt-Cache auf GuildMembership
- `Services/GuildBossService.cs`: 6 Boss-Typen, Schadensbeitrag (Crafting/Orders/MiniGames/Donations), Spawn/Despawn-Logik, Belohnungen
- `Services/GuildTipService.cs`: Kontextuelle Tipps (Preferences-basiert, 24h Cooldown, IsBusy-Guard)
- `Services/GuildAchievementService.cs`: 30 Achievements (10 Typen x 3 Tiers), Firebase-State-Tracking, Fortschrittsberechnung
- `ViewModels/GuildViewModel.cs`: Research + Timer auto-completion, ContributeDialog, Einladungs-Inbox, nutzt IGuildResearchService
- `Views/Guild/GuildResearchView.axaml(.cs)`: 3 Renderer, 20fps, DPI-skalierter HitTest, ToList-Cache
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

### GameLoop-Integration (neue Gilden-Services)

4 neue Services im GameLoopService (1s-Takt) mit gestaffelten Offsets:
- `GuildBossService.CheckBossStatusAsync()` + `SpawnBossIfNeededAsync()` alle 60s (Offset 20)
- `GuildHallService.CheckUpgradeCompletionAsync()` alle 60s (Offset 40)
- `GuildAchievementService.CheckAllAchievementsAsync()` alle 300s (Offset 200)
- `GuildWarSeasonService.CheckPhaseTransitionAsync()` + `CheckSeasonEndAsync()` alle 300s (Offset 260)

## Feierabend-Rush

- 2h 2x-Boost, 1x/Tag gratis, danach 10 Goldschrauben
- Stackt mit SpeedBoost (bis 4x), Prestige-Shop "Rush-Verstaerker" erhoeht auf 3x
- GameState: `RushBoostEndTime`, `LastFreeRushUsed`, `IsRushBoostActive`, `IsFreeRushAvailable`

## Meisterwerkzeuge (12 Artefakte)

5 Seltenheiten (Common/Uncommon/Rare/Epic/Legendary), permanente Einkommens-Boni (+2% bis +15%, gesamt +74%).
Pruefung alle 2 Minuten im GameLoop. `MasterToolUnlocked` Event → FloatingText + Celebration.

| ID | Seltenheit | Bonus | Bedingung |
|----|-----------|-------|-----------|
| mt_golden_hammer | Common | +2% | Workshop Lv.25 |
| mt_diamond_saw | Common | +2% | Workshop Lv.50 |
| mt_titanium_pliers | Common | +3% | 50 Auftraege |
| mt_brass_level | Common | +3% | 100 Minispiele |
| mt_silver_wrench | Uncommon | +5% | Workshop Lv.100 |
| mt_jade_brush | Uncommon | +5% | 25 Perfect Ratings |
| mt_crystal_chisel | Uncommon | +5% | Bronze Prestige |
| mt_obsidian_drill | Rare | +7% | Workshop Lv.250 |
| mt_ruby_blade | Rare | +7% | Silver Prestige |
| mt_emerald_toolbox | Epic | +10% | Workshop Lv.500 |
| mt_dragon_anvil | Epic | +10% | Gold Prestige |
| mt_master_crown | Legendary | +15% | Alle 11 Tools |

## Lieferant-System

- Zufaellige Lieferungen alle **2-5 Minuten** (Prestige-Bonus reduziert Intervall)
- 5 Typen: Geld (35%), Goldschrauben (20%), XP (20%), Mood-Boost (15%), Speed-Boost (10%)
- 2 Minuten Abholzeit, sonst verfaellt
- GameState: `NextDeliveryTime`, `PendingDelivery`, `TotalDeliveriesClaimed`

## SKPath/SKFont-Caching

6 Renderer nutzen gecachte Instanz-Felder statt `using var` pro Frame (GC-Reduktion bei 60fps):

| Renderer | Gecachte Felder |
|----------|----------------|
| InventGameRenderer | `_cachedPath` |
| BlueprintGameRenderer | `_cachedPath` |
| SawingGameRenderer | `_cachedPath` |
| InspectionGameRenderer | `_cachedPath` |
| WiringGameRenderer | 8 SKPaint + 3 MaskFilter + `_cachedPath` + `_cachedFont` |
| DesignPuzzleRenderer | 7 SKPaint + `_cachedFont` |
| PipePuzzleRenderer | `_cachedPath` |
| RewardCeremonyRenderer | `_iconPath` |

**WorkerAvatarRenderer**: Statische wiederverwendbare Paints (s_fillNoAA, s_fillAA, s_strokeNoAA) + s_cachedPath. Kein IDisposable (static readonly Felder leben bis Prozessende). **GameCardRenderer** und **ResearchIconRenderer** sind statische Klassen.

## IDisposable auf allen Renderern

Alle SkiaSharp-Renderer mit Instanz-Feldern (SKPaint, SKFont, SKPath, SKShader, SKMaskFilter) implementieren `IDisposable` mit `_disposed`-Guard. Statische Felder werden NICHT disposed.

| Renderer | Disposed Ressourcen |
|----------|---------------------|
| CityWeatherSystem | 3 SKPaint + 1 SKPath |
| CoinFlyAnimation | 4 SKPaint |
| ScreenTransitionRenderer | 3 SKPaint |
| OdometerRenderer | 5 SKPaint + 3 SKFont |
| ResearchTabRenderer | 1 SKFont + 1 SKPath |
| ResearchCelebrationRenderer | 2 SKFont |
| ResearchActiveRenderer | 4 SKFont |
| ResearchBranchBannerRenderer | 2 SKFont + 1 SKPath |
| GameBackgroundRenderer | 6 SKPaint + 1 SKShader |
| GameJuiceEngine | 6 SKPaint + 1 SKFont + 1 SKPath |
| GameTabBarRenderer | 5 SKPaint + 2 MaskFilter + 7 SKPath |
| CityRenderer | 12 SKPaint + 2 SKFont + 3 SKPath + CityWeatherSystem |
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
| ForgeGameRenderer | 10 SKPaint |
| PipePuzzleRenderer | 6 SKPaint + 3 SKMaskFilter + 1 SKPath |
| SawingGameRenderer | 10 SKPaint + 1 SKPath |
| BlueprintGameRenderer | 1 SKPath + 21 SKPaint (Instanz) + ~40 static readonly + 2 static MaskFilter |
| InventGameRenderer | 23 SKPaint + 1 SKPath |
| WiringGameRenderer | 8 SKPaint + 3 SKMaskFilter + 1 SKPath + 1 SKFont |
| DesignPuzzleRenderer | 7 SKPaint + 1 SKFont |
| WorkshopSceneRenderer | 8 SKPaint |
| WorkshopInteriorRenderer | 10 SKPaint |
| PaintingGameRenderer | 13 SKPaint |
| InspectionGameRenderer | 8 SKPaint + 1 SKPath (_fillNoAA, _fillAA, _fillAA2, _fillAA3, _strokeNoAA, _strokeAA, _strokeAA2, _strokeAA3, _cachedPath) |
| RoofTilingRenderer | 5 SKPaint (_fillPaint, _strokePaint, _iconPaint, _fillPaintAA, _borderPaint) |
| LuckySpinWheelRenderer | 11 SKPaint (_shadowPaint, _glintPaint, _segFillPaint, _segGlowPaint, _hubFillPaint, _pointerFillPaint, _iconShaderPaint, _iconBorderPaint, _iconFillPaint, _iconStrokePaint, _iconTextPaint) |
