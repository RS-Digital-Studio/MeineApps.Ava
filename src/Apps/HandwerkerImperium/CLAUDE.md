# HandwerkerImperium (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Idle-Game: Baue dein Handwerker-Imperium auf, stelle Mitarbeiter ein, kaufe Werkzeuge, erforsche Upgrades, schalte neue Workshop-Typen frei. Verdiene Geld durch automatische Auftraege oder spiele Mini-Games.

**Version:** 2.0.22 (VersionCode 30) | **Package-ID:** com.meineapps.handwerkerimperium | **Status:** Produktion

## Icon-System (Bitmap-Icons, AI + Programmatisch)

Kein Material.Icons.Avalonia. Alle 224 Icons sind WebP-Bitmaps (128x128) in `Assets/visuals/icons/`.

- **Hybrid-Generierung**: ~200 Objekt-Icons AI-generiert (ComfyUI DreamShaper XL, Cartoon-Stil), ~22 abstrakte UI-Icons programmatisch (PIL, geometrische Formen)
- **GameIconKind.cs**: Enum mit 224 Werten in Kategorien (Navigation, Status, Stars, Combat, Economy, Workers, Tools, Buildings, etc.)
- **GameIcon** (`Icons/GameIcon.cs`): Custom Control (erbt von `TemplatedControl`, hat `Foreground`). Render: Bitmap-Alpha als OpacityMask, Foreground als Füllfarbe
- **GameIconRenderer** (`Icons/GameIconRenderer.cs`): SkiaSharp-Renderer für SKCanvas. Bitmap + SKColorFilter.CreateBlendMode(color, SrcIn) für Tinting
- **GameIconPaths.cs**: Leerer Stub (gibt null zurück). Keine SVG-Pfade mehr
- **Preloading**: `GameIcon.PreloadAllAsync()` in Loading-Pipeline (Step 1 parallel mit Shader+ViewModel+Purchases)
- **Pfad-Konvertierung**: PascalCase → snake_case → `icons/{name}.webp` (z.B. ArrowDown → icons/arrow_down.webp)
- **Tinting**: AXAML nutzt `Foreground="{StaticResource CraftGoldBrush}"`, SkiaSharp nutzt `paint.Color`
- **StringToGameIconKindConverter**: Konvertiert String-Iconnamen zu Enum-Werten in XAML-Bindings
- **Generierungs-Script**: `F:\AI\ComfyUI_workflows\handwerkerimperium\generate_icons_test.py`

## Haupt-Features

- **10 Workshop-Typen** (Schreiner, Klempner, Elektriker, Maler, Dachdecker, Bauunternehmer, Architekt, Generalunternehmer, Meisterschmiede, Innovationslabor)
- **10 Mini-Games** (Sawing, Pipe Puzzle, Wiring, Painting, RoofTiling, Blueprint, DesignPuzzle, Inspection, ForgeGame, InventGame)
- **Worker-System** mit 10 Tiers (F/E/D/C/B/A/S/SS/SSS/Legendary), Avatare, Training, Ausruestung
- **Goldschrauben-Economy** (Premium-Waehrung fuer Boosts/Unlock)
- **Research Tree** (45 Upgrades in 3 Branches: Tools, Management, Marketing)
- **7 Gebaeude** (Canteen, Storage, Office, Showroom, TrainingCenter, VehicleFleet, WorkshopExtension)
- **Daily Challenges** (3/Tag) + **Weekly Missions** (5/Woche, 50 Goldschrauben Komplett-Bonus)
- **Daily Login Rewards** (30-Tage-Zyklus) + **Streak-Rettung** (3 Goldschrauben)
- **Achievements** (94 Erfolge) + **Milestone-Celebrations** (Spieler-Level + Workshop-Level)
- **Prestige-System** (7 Stufen Bronze-Legende, verschärfte Bewahrung, Soft-Cap 5x, Tier-skalierendes Startgeld, wiederholbares Shop-Item, permanenter Prestige-Pass, Diminishing Returns auf Multiplikator)
- **Events** (8 zufaellige + saisonaler Multiplikator, Intervall skaliert mit Prestige)
- **Auftragstypen** (Standard/Large 1.8x/Weekly 3.0x/Cooperation 2.5x) + **Stammkunden** (bis 1.5x Bonus)
- **Bulk Buy** (x1/x10/x100/Max) + **Hold-to-Upgrade** (schnelles Hochleveln)
- **Naechstes-Ziel-System** (GoalService: dynamischer Gold-Banner, 5 Prioritäten inkl. Worker-Tier-Ziel)
- **Offline-Earnings** (100% erste 2h, 40% bis 4h, 25% bis 8h, 10% danach)
- **Feierabend-Rush** (2h 2x-Boost, 1x taeglich gratis, danach 10 Goldschrauben)
- **Meisterwerkzeuge** (12 Artefakte, 5 Seltenheiten, passive Einkommens-Boni)
- **Lieferant-System** (Variable Rewards alle 2-5 Min: Geld, Schrauben, XP, Mood, Speed)
- **Prestige-Shop** (23 Items in 4 Kategorien, 1 wiederholbar) + **Prestige-Pass** (2,99 EUR IAP, +50% Prestige-Punkte, permanent nach Kauf). Shop-Effekte: OfflineHoursBonus (GameState.MaxOfflineHours), CraftingSpeedBonus (CraftingService.StartCrafting), ExtraQuickJobLimit (QuickJobService.GetMaxQuickJobsPerDay), UpgradeDiscount (GameLoopService-Cache), Income/Rush/Delivery (GameLoopService-Cache), CostReduction/MoodDecay/XP (PrestigeService-Getter), GoldenScrews (GameStateService.AddGoldenScrews), StartMoney/StartWorkerTier (PrestigeService.ResetProgress)
- **Story-System** (25 Kapitel von NPC "Meister Hans" mit SkiaSharp-Portrait)
- **Kontextuelles Tutorial** (17 Tooltip-Bubbles + Welcome-Dialog, ContextualHintService, SeenHints-Tracking)
- **In-App Review** (Level 20/50/100, erstes Prestige, 50 Auftraege)
- **Benachrichtigungen** (4 Typen, AlarmManager + BroadcastReceiver, BootReceiver, 6 Sprachen)
- **Google Play Games** (Leaderboards, kein Cloud-Save im NuGet v121.0.0.2)
- **Audio + Haptik** (15 Sounds via SoundPool, 7 Vibrations-Muster, Hintergrundmusik)
- **Vorarbeiter-System** (14 Manager, Lv.1-5, Workshop-Boni)
- **Turniere** (Woechentlich, 9 simulierte Gegner, 3x/Tag gratis)
- **Battle Pass** (30 Tiers, Free/Premium Track, 30-Tage-Saisons, Premium: 10 GS/3 Tiers + 50 GS Capstone)
- **Saisonale Events** (4/Jahr mit Saisonwaehrung und Event-Shop)
- **Gilden/Innungen** (Firebase Realtime Database, Wochenziele, 18 Forschungen mit Timer+Auto-Completion, Einladungs-Inbox mit Accept/Decline)
- **Crafting-System** (13 Rezepte in 3 Tiers, Inventar + Verkauf)
- **Automatisierung** (Auto-Collect Lv15+, Auto-Accept Lv25+, Auto-Assign Lv20+, Auto-ClaimDaily Premium) — eigenes Dashboard-Panel (nicht in Settings)
- **Welcome Back Angebote** + **Gluecksrad** (taeglich gratis)
- **Ausruestungs-System** (4 Typen x 4 Seltenheiten fuer Arbeiter, Equip/Unequip im Worker-Profil, Inventar-Browser)
- **Grafik-Einstellungen** (Low/Medium/High, GraphicsQuality in GameState, steuert Wetter-Effekte etc.)
- **MiniGame-Direktstart** (Auto-Start nach Tutorial-Check + 3-2-1-Countdown, kein Start-Button)
- **MiniGame Auto-Complete** (ab 30 Perfect-Ratings "Auto-Ergebnis" mit Good-Rating, Premium ab 15, PerfectRatingCounts in GameState)
- **Gilden-Browser** (offene Gilden suchen+beitreten ohne Einladung, Firebase REST-Abfrage, Browse-UI in GuildView)
- **Soft-Cap-Transparenz** (IsSoftCapActive + SoftCapReductionPercent im GameState, UI-Indikator im Dashboard)

### Prestige-System (Details)

**PP-Formel**: `floor(sqrt(CurrentRunMoney / 100_000))` - nur Geld aus dem aktuellen Durchlauf zählt (nicht kumulativ). `CurrentRunMoney` wird bei jedem Prestige auf 0 zurückgesetzt.

**Multiplikator mit Diminishing Returns**: Bonus pro Prestige sinkt mit Anzahl bereits durchgeführter Prestiges desselben Tiers. Formel: `baseBonus * 1/(1 + 0.1 * tierCount)`. Erster Prestige voller Bonus, 10. nur noch 50%. Cap bei 50x (statt 200x).

**Tier-Multiplikator-Boni (Basis)**: Bronze +20%, Silver +25%, Gold +50%, Platin +100%, Diamant +200%, Meister +400%, Legende +800%.

**Verschärfte Erhaltung (eine Stufe höher als original)**:

| Tier | Erhaltung |
|------|-----------|
| Bronze/Silver | Nur Basis (Achievements, Premium, Settings, PrestigeData, Tutorial) |
| Gold+ | + Research bleibt |
| Platin+ | + Prestige-Shop Items bleiben |
| Diamant+ | + MasterTools bleiben |
| Meister+ | + Gebäude (Level→1) + Equipment |
| Legende | + Manager (Level→1) + beste Worker |

### Neuer-Spieler-Einstieg

- **Startgeld**: 1.000 EUR (statt 250)
- **Start-Werkstatt**: Schreinerei mit 2 Arbeitern (statt 1)
- Workshop-Karten zeigen werkstatt-spezifische Icons (GameIconRenderer) auf Upgrade-Button und dimmed auf Locked-Karten

## Premium & Ads

### Premium-Modell
- **Preis**: 4,99 EUR (Lifetime)
- **Vorteile**: +50% Einkommen, +100% Goldschrauben aus Mini-Games, keine Werbung
- **Shop Live-Vergleich**: `PremiumIncomeComparison` Property im ShopVM zeigt Nicht-Premium-Spielern "Dein Einkommen: X/s -> Mit Premium: Y/s"
- **Starter-Offer**: Einmaliges Angebot ab Level 10, 24h-Countdown, Properties `StarterOfferShown`/`StarterOfferTimestamp` in GameState

### Rewarded (9 Placements)
1. `golden_screws` - 10 Goldschrauben (Dashboard, BAL-3: von 5 erhöht)
2. `score_double` - Mini-Game Score verdoppeln
3. `market_refresh` - Arbeitermarkt-Pool neu wuerfeln
4. `workshop_speedup` - 30min Produktionsertrag sofort (BAL-5: von 2h reduziert)
5. `workshop_unlock` - Workshop ohne Level freischalten
6. `worker_hire_bonus` - +1 Worker-Slot persistent
7. `research_speedup` - Forschungszeit -50% (BAL-4: statt Sofortfertigstellung) + zeitbasierte GS-Sofortfertigstellung (5 GS/h, min 5, max 50)
8. `daily_challenge_retry` - Challenge-Fortschritt zuruecksetzen
9. `achievement_boost` - Achievement Progress +20%

## Architektur-Besonderheiten

### Dispose / Memory Leak Prevention

`App.DisposeServices()` gibt alle IDisposable-Singletons frei (IGameLoopService, GameJuiceEngine, MainViewModel, IFirebaseService).
- **Desktop**: `desktop.ShutdownRequested += (_, _) => DisposeServices();`
- **Android**: `MainActivity.OnDestroy()` ruft `App.DisposeServices()` als ERSTE Zeile auf (vor AdMob-Dispose)
- Pattern identisch mit BomberBlast

### MainViewModel Partial-Class-Split

MainViewModel ist in 6 partielle Dateien aufgeteilt (~5.150 Zeilen, 165 ObservableProperties):

| Datei | Inhalt |
|-------|--------|
| `MainViewModel.cs` | Felder, Constructor, ~165 ObservableProperties, Event-Handler, GameTick, Dispose |
| `MainViewModel.Navigation.cs` | Tab-Auswahl, NavigateTo-Commands, HandleBackPressed, MiniGame-Navigation |
| `MainViewModel.Dialogs.cs` | Weiterleitungsmethoden an DialogVM, Prestige-Durchfuehrungslogik |
| `MainViewModel.Economy.cs` | Workshop-Kauf/Upgrade, Auftraege, Rush, Lieferant, BulkBuy, Hold-to-Upgrade |
| `MainViewModel.Missions.cs` | Weekly Missions, Welcome-Back, Lucky Spin, Streak-Rettung, Quick Jobs, Daily Challenges, Meisterwerkzeuge |
| `MainViewModel.Init.cs` | InitializeAsync, Cloud-Save, Offline-Earnings, Daily Reward |

### DialogViewModel (extrahiert aus MainViewModel)

`DialogViewModel.cs` (785 Zeilen, 45 ObservableProperties) enthaelt alle Dialog-bezogenen Properties und Methoden:
- **Alert-Dialog**: ShowAlertDialog(), DismissAlertDialog
- **Confirm-Dialog**: ShowConfirmDialog() mit TaskCompletionSource, ConfirmDialogAccept/Cancel
- **Story-Dialog**: CheckForNewStoryChapter(), ShowStoryDialog(), DismissStoryDialog (Meister Hans NPC)
- **Achievement-Dialog**: AchievementName/Description, DismissAchievementDialog
- **LevelUp-Dialog**: IsLevelUpPulsing, DismissLevelUpDialog
- **Hint-Dialog**: OnHintChanged(), DismissHint (kontextuelle Tooltips/Dialoge)
- **Prestige-Summary**: ShowPrestigeSummary(), DismissPrestigeSummary, GoToShop
- **Prestige-Tier-Auswahl**: ShowPrestigeConfirmationDialogAsync(), SelectPrestigeTier, UpdatePrestigeDialogContent
- **IsAnyDialogVisible**: Aggregierte Property fuer alle Dialog-Sichtbarkeiten

**Kommunikation mit MainViewModel** via Events:
- `DeferredDialogCheckRequested` → MainViewModel.CheckDeferredDialogs()
- `PrestigeSummaryGoToShopRequested` → MainViewModel.SelectBuildingsTab()
- `FloatingTextRequested` → MainViewModel.FloatingTextRequested Event

MainViewModel erstellt DialogVM im Constructor und verdrahtet Events. Dialog-Views in MainView.axaml binden per `DataContext="{Binding DialogVM}"`.

**Konventionen**: Jede Datei hat eigene `using`-Direktiven + `namespace HandwerkerImperium.ViewModels;` + `public partial class MainViewModel`. Event-Handler fuer BuildingsViewModel.FloatingTextRequested als benanntes Delegate-Feld mit korrektem Unsubscribe in Dispose().

### Dialog-UserControls (Views/Dialogs/)

MainView-Dialoge in eigenstaendige UserControls extrahiert (reduziert MainView.axaml um ~650 Zeilen):
`OfflineEarningsDialog`, `DailyRewardDialog`, `WelcomeBackOfferDialog`, `AchievementDialog`, `ContextualHintDialog` (Tooltip-Bubble/Dialog, ersetzt TutorialDialog), `StoryDialog` (Hans-Blinzel-Animation via StoryDialogControl.UpdateHansAnimation()), `AlertDialog`, `ConfirmDialog`, `WorkerProfileDialog`.
Die Dialog-Controls `AchievementDialog`, `ContextualHintDialog`, `StoryDialog`, `AlertDialog`, `ConfirmDialog`, `PrestigeSummaryDialog` binden per `DataContext="{Binding DialogVM}"` an DialogViewModel (x:DataType="vm:DialogViewModel"). `OfflineEarningsDialog`, `DailyRewardDialog`, `WelcomeBackOfferDialog`, `WorkerProfileDialog` erben weiterhin `DataContext="{Binding}"` vom MainViewModel. Backdrop-Dismiss im Code-Behind wo noetig.

### 5-Tab Navigation

| Tab | Index | View | Inhalt |
|-----|-------|------|--------|
| Werkstatt | 0 | DashboardView | City-Szene (CityRenderer), Workshop-Karten, Automation-Panel, Quick-Jobs |
| Imperium | 1 | ImperiumView | Gebaeude, Crafting+Research, Workers/Manager/MasterTools, Prestige (am Ende) |
| Missionen | 2 | MissionenView | Daily Challenges, Weekly Missions, Turnier/BattlePass/SaisonEvent/Erfolge, Gluecksrad |
| Gilde | 3 | GuildView | Guild-Hub, Research/Members/Invite Sub-Seiten |
| Shop | 4 | ShopView | IAP, Goldschrauben-Pakete, Ausruestungs-Shop |

**Tab-Bar Badges**: Tab 0 (HasPendingDelivery+CanActivateRush), Tab 1 (HasWorkerWarning), Tab 2 (ClaimableMissionsCount+HasFreeSpin). SkiaSharp-Tab-Bar (GameTabBarRenderer, 64dp).

### Dashboard-Header (UI-Entschlackung Phase 1)

- **Zeile 1**: Level-Badge + Gold/Schrauben + Settings-Gear (kreisförmiger Button mit `#30000000` Background)
- **Zeile 2**: Nur Level+XP (immer), Reputation (bedingt: `ShowReputationBadge` bei <50 oder >=80), Streak (bedingt: `ShowStreakBadge` bei >=5 Tagen)
- **Statistics-Zugang**: Über SettingsView (Link-Button mit ChevronRight, Route `../statistics`)
- **Workshop-Canvas**: Dynamische Höhe via `WorkshopCanvasHeight` (2 Spalten, ~160dp/Reihe) statt fixe 800dp

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
| `ShowTournamentSection` | 50 | Missionen Turnier-Button (Dead Zone Lv40-80) |
| `ShowSeasonalEventSection` | 60 | Missionen Saison-Event-Button |
| `ShowBattlePassSection` | 70 | Missionen Battle-Pass-Button |

Zusätzlich existieren Tab-Level-Gates (`TabUnlockLevels`): Werkstatt=1, Shop=3, Imperium=5, Missionen=8, Gilde=15.

**Alle Level-Schwellenwerte sind in `Models/LevelThresholds.cs` zentralisiert** (Feature-Unlocks, Automation, Tabs, Hints, Reputation, Daten-Caps). Keine Magic Numbers in Services/ViewModels - immer `LevelThresholds.*` verwenden. GameLoopService nutzt `GameStateService.IsAuto*Unlocked` Properties statt eigener Level-Checks.

### Dashboard-UserControls (Views/Dashboard/)

`DailyChallengeSection`, `WeeklyMissionSection`, `BannerStrip`, `AutomationPanel` (Lv15+ sichtbar, 3 Toggles: AutoCollect/AutoAccept/AutoClaimDaily) - erben DataContext vom Parent (MainViewModel). PaintSurface-Handler nutzen `IProgressProvider`-Interface.

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

Navigation via `NavigationRequested` Events. Sub-VM-Events werden an GuildViewModel propagiert. Zurueck-Navigation (".." oder Android-Back) fuehrt zum Guild-Hub. Routes: `guild_chat` (startet Chat-Polling), `guild_war` (lädt War-Status).

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
`HireWorker()` → individuelle Marktpreise: Tier-Basis * Level * Talent (0.7-1.3x) * Persönlichkeit * Spezialisierung * Effizienz-Position. A+ Tiers kosten zusätzlich Goldschrauben.
**HiringCost wird persistiert** (`[JsonPropertyName("hiringCost")]`) → Marktpreise bleiben nach App-Neustart korrekt.
Tier-Farben: F=#9E9E9E(Grau), E=#4CAF50(Grün), D=#2196F3(Blau), C=#9C27B0(Lila), B=#FFC107(Gold), A=#F44336(Rot), S=#FF9800(Orange), SS=#E040FB(Pink), SSS=#7C4DFF(DeepPurple), Legendary=#FFD700(Gold)
**S-Tier+ Freischaltung**: Research `mgmt_10` (UnlocksSTierWorkers) muss erforscht sein → WorkerService liest ResearchEffects und übergibt `hasSTierResearch` an `GeneratePool()`. Ebenso `mgmt_04` (UnlocksHeadhunter) → Pool-Größe 5→8.
**3 Training-Typen**: Efficiency (XP→Level→+Effizienz), Endurance (senkt Fatigue, max -50%), Morale (senkt MoodDecay, max -50%)
**Training Auto-Rest**: Bei 100% Fatigue wird Training automatisch beendet und Ruhe gestartet (identisch mit Arbeits-Modus).
**Worker-Avatare**: WorkerAvatarRenderer (Pixel-Art), Worker.IsFemale deterministisch, RarityFrameRenderer (Tier→Rarity), Idle-Animationen (Atem+Blinzeln ab 56dp)

### Goldschrauben-Quellen

1. Mini-Games (3-10), 2. Daily Challenges (~12, BAL-9: von ~19 reduziert), 3. Achievements (5-50), 4. Rewarded Ad (10, BAL-3), 5. IAP (50/150/450), 6. Daily Login (1-25), 7. Spieler-Meilensteine (3-200), 8. Workshop-Meilensteine (2-50)

**Premium +100% GS**: `AddGoldenScrews(amount, fromPurchase)` verdoppelt Gameplay-Quellen für Premium-Spieler. IAP-Käufe (`fromPurchase: true`) werden nicht verdoppelt. Prestige-Shop-Bonus stackt additiv.

### Research Tree

45 Upgrades in 3 Branches a 15 Level: Tools (Effizienz + MiniGame-Zone), Management (Loehne + Worker-Slots), Marketing (Belohnungen + Order-Slots)
Kosten: 500 bis 1B. Dauer: 10min bis 72h (Echtzeit).
**UI**: 2D-Baum-Layout mit 6 SKCanvasViews (Header, ActiveResearch, Tabs, BranchBanner, Tree, Celebration). 30fps Render-Loop.
**Renderer**: ResearchTreeRenderer, ResearchIconRenderer (12 Icons), ResearchActiveRenderer, ResearchBranchBannerRenderer, ResearchTabRenderer, ResearchCelebrationRenderer, ResearchLabRenderer.

### Mini-Games (alle SkiaSharp-basiert)

Alle 10 Mini-Games nutzen dedizierte SkiaSharp-Renderer. Header, Result-Display, Countdown und Buttons bleiben XAML. Jeder Renderer hat `Render()` + `HitTest()`, View hat 30fps Render-Loop, Touch via `PointerPressed` + DPI-Skalierung.
**Tutorial-System**: Erstes Spielen zeigt Overlay (Tracking via `GameState.SeenMiniGameTutorials`).
**Direktstart**: Alle MiniGames starten automatisch nach Tutorial-Check (kein Start-Button). Start-Buttons sind per `<Panel IsVisible="False">` versteckt. Bei Tutorial-Dismiss wird `StartGameAsync()` aufgerufen.
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

Alle Renderer: Struct-basierte Partikel (kein GC), 30fps Render-Loop.

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| `LogService` | Zentraler Logging-Service (Debug-Output). ILogService injiziert in GuildService, GuildWarSeasonService, FirebaseService |
| `GameLoopService` | 1s-Takt: Einkommen, Kosten, Worker-States, AutoSave (30s) |
| `GameStateService` | Zentraler State mit Thread-Safety (lock), GetOrderRewardMultiplier(), AddXp() (aus GameState verschoben) |
| `SaveGameService` | JSON-Persistenz (Load/Save/Import/Export/Reset), MigrateFromV1() (aus GameState verschoben) |
| `WorkerService` | Mood, Fatigue, Training, Ruhe, Kündigung, ReinstateWorker, Research-basierte Markt-Generierung (S-Tier+Headhunter) |
| `PrestigeService` | 7-Tier Prestige + Shop-Effekte + verschärfte Bewahrung + Diminishing Returns (Cap 50x) |
| `ResearchService` | 45 Research-Nodes, Timer, Effekt-Berechnung |
| `EventService` | 8 Event-Typen + saisonaler Multiplikator |
| `DailyChallengeService` | 3 Challenges/Tag (00:00 Reset) |
| `DailyRewardService` | 30-Tage Login-Zyklus |
| `QuickJobService` | Schnelle MiniGame-Jobs (Rotation 8-15min, Limit 20-40/Tag) |
| `StoryService` | 25 Kapitel von Meister Hans, fortschrittsbasiert |
| `AchievementService` | 94 Erfolge + Goldschrauben-Rewards, PrestigeCompleted-Event |
| `OfflineProgressService` | Offline-Einnahmen (Staffelung 100%/40%/25%/10%, Events+Boosts anteilig) + Worker-State-Simulation (2-Phasen: Aktivität→Rest-Recovery, dynamische Fatigue, Training-Fortschritt+Kosten, Arbeits-XP, Level-Ups, alle GameLoop-Modifikatoren) |
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
| `GuildService` | Firebase REST API, Gilden-CRUD, Wochenziele, Einladungen, GetMaxMembers(), CountAndSyncMemberCountAsync (Race-Condition-frei) |
| `GuildResearchService` | 18 Gilden-Forschungen (6 Kategorien), Timer, Effekt-Cache, SemaphoreSlim |
| `GuildWarSeasonService` | Einziger Gilden-Krieg-Service (Saison-System, Matchmaking, Scoring, Ligen, Bonus-Missionen). Legacy GuildWarService entfernt |
| `GuildHallService` | Interaktives Gilden-HQ mit 10 Gebäuden (Level 1-5), Upgrade-Timer, Effekt-Cache |
| `GuildBossService` | Kooperative Gilden-Bosse (6 Typen), Schaden-Tracking, Spawn/Despawn, Belohnungen |
| `GuildTipService` | Kontextuelle Gilden-Tipps (Preferences-basiert, 24h Cooldown) |
| `GuildAchievementService` | 30 Gilden-Achievements (10 Typen x 3 Tiers), Firebase-Tracking |
| `FirebaseService` | Anonymous Auth, Token-Refresh (55min, Retry bei Netzwerkfehler), CRUD, 5s Timeout, SemaphoreSlim. PlayerId-GUID (stabile Spieler-Identität, überlebt Account-Wechsel), auth_to_player Mapping via SyncAuthToPlayerMappingAsync() |
| `GameAssetService` | LRU-Cache 50MB, WebP→SKBitmap + animierte WebP Multi-Frame, PlatformAssetLoader. Statischer `GameAssetService.Current` Zugriff für Views (kein Service-Locator) |
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
| City Weather | CityWeatherSystem: Regen+Regenbogen, Sonne+Shimmer, Blaetter, Schnee (80 Struct-Pool, canvas.ClipRect auf City-Bounds, nur bei GraphicsQuality >= Medium) |
| City Progression | CityProgressionHelper: Strassen-Upgrade, Dekorationen (Buesche/Baeume/Laternen/Baenke/Brunnen) |
| City Touch | Tap auf Workshop → RadialBurst + TapLabel + Navigation. Tap auf Gebaeude → Imperium-Tab |
| City Details | Workshop-Mini-Icons, Spotlight+Fahnen, Lieferwagen+Fussgaenger, Fenster-Glow, Laternen-Lichtkegel, Wolken-Schatten, Sonnenauf-/Untergang, Boden-Wetter |
| Reward-Zeremonie | Full-Screen Overlay: Scale-In, Confetti (120), Feuerwerk, 5 CeremonyTypes, 4s Tap-to-Dismiss |
| Loading-Screen | Zahnraeder, Funken-Partikel, Gradient-Fortschrittsbalken, rotierende Tipps |
| Splash-Screen | "Die Schmiede": Zahnraeder, Amboss, Hammer-Animation, Glut-Partikel |
| Gluecksrad | LuckySpinWheelRenderer: 8 Segmente, Nieten-Rand, SkiaSharp-Icons, Spin-Animation ~60fps. Segment-Reihenfolge (0-7): MoneySmall, MoneyMedium, MoneyLarge, XpBoost, GoldenScrews, SpeedBoost, ToolUpgrade, Jackpot. Winkelberechnung: `360 - segmentCenter` (Rad dreht im Uhrzeigersinn, Zeiger oben) |
| Workshop-Icons | WorkshopGameCardRenderer: Werkstatt-spezifische GameIconRenderer-Icons auf Upgrade-Button + dimmed auf Locked-Karten |
| Grafik-Qualität | GraphicsQuality (Low/Medium/High) steuert Wetter, Partikel etc. in SettingsView |
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
- **Overlay-Farben**: DialogOverlay=#AA000000 → `DialogOverlayBrush`, RewardOverlay=#CC000000 → `RewardOverlayBrush`
- **Semantische Farben**: `SuccessBrush` (#22C55E), `ErrorBrush` (#EF4444), `WarningBrush` (#F59E0B) - alle in AppPalette.axaml definiert
- **Kontrast-Farbe**: `PrimaryContrastBrush` (#FFFFFF) statt `Foreground="White"` in allen Views
- **Hardcodierte Farben**: Alle in ~35 Views durch DynamicResource/StaticResource ersetzt. Ausnahme: Alpha-Kanal-Hintergruende (#20D97706 etc.), GradientStops, Opacity-Varianten (#FFFFFFCC) und SkiaSharp-Code bleiben hardcodiert

## Visual Upgrade (Phase 0-3, deployt)

AI-generierte Stylized-Cartoon-Hintergründe via ComfyUI + DreamShaper XL / Juggernaut-X. Hybrid-Rendering: AI-Hintergrund (1x DrawBitmap) + prozedurale Overlays.

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
- **Offen:** Android-Benchmark, alte prozedurale Hintergründe entfernen (erst nach Verifikation auf Gerät)

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
| Weekly | WS-Level 20+ | 3.0x | 7-Tage-Deadline (BAL-14: von 2.5 auf 3.0, eigene Identität vs. Cooperation) |

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
- Stackt mit SpeedBoost (bis 4x), Prestige-Shop "Rush-Verstaerker" erhoeht auf 3x
- GameState: `RushBoostEndTime`, `LastFreeRushUsed`, `IsRushBoostActive`, `IsFreeRushAvailable`

## Meisterwerkzeuge (12 Artefakte)

5 Seltenheiten (Common/Uncommon/Rare/Epic/Legendary), permanente Einkommens-Boni (+2% bis +15%, gesamt +74%).
Pruefung alle 2 Minuten im GameLoop. `MasterToolUnlocked` Event → FloatingText + Celebration.

| ID | Seltenheit | Bonus | Bedingung |
|----|-----------|-------|-----------|
| mt_golden_hammer | Common | +2% | Workshop Lv.75 |
| mt_diamond_saw | Common | +2% | Workshop Lv.150 |
| mt_titanium_pliers | Common | +3% | 150 Auftraege |
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

**SKFont-Migration (neue SkiaSharp-API)**: Alle Renderer verwenden `canvas.DrawText(text, x, y, align, font, paint)` statt deprecated `paint.TextSize`/`paint.TextAlign`/`paint.FakeBoldText`/`canvas.DrawText(text, x, y, paint)`. Font-Felder: static readonly bei fester Größe (WorkshopGameCardRenderer: 8 Fonts, RewardCeremonyRenderer: 2 Fonts, GameCardRenderer: 2 Fonts), Instanz-Felder bei dynamischer Größe (PrestigeRoadmapRenderer: 1 Font, LuckySpinWheelRenderer: 1 Font, ForgeGameRenderer: 1 Font). `SKFont.MeasureText()` nutzt `out SKRect` statt `ref SKRect`.

**WorkerAvatarControl**: Gemeinsamer statischer Timer (`s_sharedTimer`) für alle Instanzen statt pro-Instanz Timer. Statische `s_bitmapPaint` + `s_blinkPaint` (keine Allokation pro Frame). WeakReference-Liste für Auto-Cleanup.

## Scroll-Performance-Optimierungen

| Optimierung | Effekt |
|-------------|--------|
| MainView BackgroundCanvas ~5fps statt 25fps | -80% Background-Draw-Calls (Partikel unter Content unsichtbar bei 25fps) |
| Dashboard City-Canvas ~6fps während Scroll | -70% Draw-Calls während Scroll. WorkshopCards pausieren komplett |
| WorkerAvatarControl Shared Timer | 1 Timer statt N (bei 8 Avataren: 20 statt 160 Ticks/s) |
| GameTick Tab-Awareness | PropertyChanged nur für sichtbare Tabs (spart ~20 Notifications/s) |
| BoxShadow→Opacity Animationen | GPU-beschleunigt statt CPU-Blur auf Android |
| LINQ→For-Schleifen | Kein Enumerator+Closure-GC in OnMoneyChanged, RefreshFeatureStatusTexts, Workshop-Lookups |
| MiniGame Views: Gecachte Render-Arrays | WiringGameView/PaintingGameView: .Select().ToArray() → gecachte Arrays mit For-Schleife (0 Allokation/Frame) |
| MiniGame Views: SKColor.Parse-Cache | InspectionGameView/RoofTilingGameView: Dictionary-Cache fuer Hex→SKColor/uint (0 String-Parsing/Frame) |
| PaintingGameView: Farb-Cache | SelectedColor nur bei Aenderung neu geparst statt pro Frame |
| MiniGame Shader-Cache | ForgeGame (6), Wiring (3), WorkshopInterior (2), Sawing (1), CraftTextures (1): Bounds-basierter Cache statt pro-Frame-Erstellung. Spart ~13 Shader/Frame = ~390 Shader/s |

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
| ForgeGameRenderer | 10 SKPaint + 1 SKFont + 6 SKShader (gecacht) |
| PipePuzzleRenderer | 6 SKPaint + 3 SKMaskFilter + 1 SKPath |
| SawingGameRenderer | 10 SKPaint + 1 SKPath + 1 SKMaskFilter + 1 SKShader (gecacht) |
| BlueprintGameRenderer | 1 SKPath + 21 SKPaint (Instanz) + 3 SKFont + ~40 static readonly + 2 static MaskFilter |
| InventGameRenderer | 23 SKPaint + 1 SKPath |
| WiringGameRenderer | 8 SKPaint + 3 SKMaskFilter + 1 SKPath + 1 SKFont + 3 SKShader (gecacht) |
| DesignPuzzleRenderer | 7 SKPaint + 1 SKFont |
| WorkshopSceneRenderer | 8 SKPaint |
| WorkshopInteriorRenderer | 10 SKPaint + 2 SKShader (gecacht) |
| PaintingGameRenderer | 13 SKPaint |
| InspectionGameRenderer | 8 SKPaint + 1 SKPath (_fillNoAA, _fillAA, _fillAA2, _fillAA3, _strokeNoAA, _strokeAA, _strokeAA2, _strokeAA3, _cachedPath) |
| RoofTilingRenderer | 5 SKPaint (_fillPaint, _strokePaint, _iconPaint, _fillPaintAA, _borderPaint) |
| LuckySpinWheelRenderer | 11 SKPaint + 1 SKFont (_shadowPaint, _glintPaint, _segFillPaint, _segGlowPaint, _hubFillPaint, _pointerFillPaint, _iconShaderPaint, _iconBorderPaint, _iconFillPaint, _iconStrokePaint, _iconTextPaint, _iconTextFont) |
| PrestigeRoadmapRenderer | 5 SKPaint + 1 SKFont + 1 SKMaskFilter |
| RewardCeremonyRenderer | 1 SKPath (_iconPath) |
| ResearchTreeRenderer | 3 SKFont + 2 SKPath |

**Kein IDisposable nötig** (nur static readonly Felder): `FireworksRenderer`, `LoadingScreenRenderer`, `WorkerAvatarRenderer`, `GameCardRenderer`, `ResearchIconRenderer`.
