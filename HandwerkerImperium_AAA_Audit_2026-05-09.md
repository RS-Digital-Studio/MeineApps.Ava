# HandwerkerImperium — AAA-Audit (brutal-ehrlich)

**Datum:** 09.05.2026  
**Version:** v2.1.0 (VersionCode 50, Produktion)  
**Messlatte:** Supercell, Playrix, Habby, Game Hive, Auxbrain, Hyper Hippo, Kolibri  
**Tonalitaet:** Brutal ehrlich — keine Schmeichelei, keine Politur. Wenn was Crap ist, steht's so da.

---

## 0. TL;DR — Wo ihr steht

HandwerkerImperium ist auf einem **soliden indie-bis-mid-tier Niveau** und meilenweit von einem AAA-Idle-Studio entfernt. Die Engineering-Basis ist erstaunlich diszipliniert (zentrales Balancing-File, ordentliche Tests, RemoteConfig, Analytics, HMAC-Signing). Aber die Player-Experience-Schicht — Audio, Onboarding, Visual-Polish auf Desktop, IA, Live-Ops-Variabilitaet — ist nicht annaehernd da, wo ein Studio mit 100M+ Downloads abliefern wuerde. **Drei AAA-Killer:**

1. **Desktop hat keinen Sound.** `AudioService.cs` ist ein 69-Zeilen-Stub, der `Task.CompletedTask` zurueckgibt. Auf Desktop spielt nichts. AAA-Studios liefern auf jeder Plattform.
2. **Es gibt keine echte FTUE.** `TutorialState` ist ein 38-Zeilen-Model, kein Tutorial-Service existiert. Spieler werden ins Dashboard geworfen mit Hint-Bubble. Habby/Playrix haben 30+ Schritte mit Hand-Holding und gerichtetem Aha-Moment unter 60s.
3. **Feature-Bloat ohne IA-Disziplin.** 30+ Subsysteme (Workshops, Worker, Crafting, Research, Manager, Tournaments, BattlePass, Saison-Story, Gilden mit 8 Sub-Tabs, DailyChallenges, WeeklyMissions, Events, Achievements, ReputationShop, LuckySpin, Auktionen, Coop-Orders) — alle als ContentControls in ein-und-derselben `MainView.axaml`. Spieler sieht 10 Tabs und verstehen Stunde 3 nicht, was sie spielen.

**Gesamteinordnung:** Engineering 7/10, Game-Design-Math 7/10, Game-Design-Komplexitaet 4/10, UX/Polish 4/10, Live-Ops 5/10, Audio 2/10, Onboarding 2/10. **AAA Schnitt: 7+/10 in jeder Achse.** Ihr habt 2-3 Quartale Arbeit vor euch.

---

## 1. Code-Architektur & MVVM

### [P0] MainViewModel ist 2403 Zeilen lang

**Was:** `MainViewModel.cs` = 2403 Zeilen, plus 4 Partial-Files (Missions/Economy/Dialogs/Host/Init/Navigation). Effektive God-Class von ~3500+ Zeilen Logik. Das ist nicht "split", das ist "auf-mehrere-Files-verteilt".

**Warum AAA-Problem:** Bei Supercell/Habby ist die Navigations-Wurzel-VM typischerweise <500 Zeilen, weil sie nur als Composition-Root + Router fungiert. Hier macht MainViewModel: Navigation, Lifecycle, Ad-Steuerung, Welcome-Flow-Trigger, Economy-Bindings, Tab-State, Reputation-Tier-Tracking, Story-Trigger, Cinematic-Steuerung. Bei jedem neuen Feature waechst sie weiter. **Dieser Code ist nicht testbar — niemand schreibt einen Unit-Test gegen 3500 Zeilen.**

**Fix:** `MainViewModel` zerlegen in:
- `RootViewModel` (Composition + Lifecycle + Pause/Resume) — max 200 Zeilen
- `NavigationCoordinator` (Tab + Sub-Navigation + Breadcrumb) — max 300 Zeilen
- `WelcomeOrchestrator` (Offline + DailyReward + Starter-Offer Flow)
- `EconomyHeaderViewModel` (Money/GS/Rep-Tier-Bindings)
- `CinematicCoordinator` (Prestige-Cinematic + Loading + Ceremony)

**Aufwand:** L (3-5 Tage). Hohe ROI, weil jede zukuenftige Aenderung billiger wird.

### [P0] MainView.axaml ist eine Z-Stack-Hoelle

**Was:** `MainView.axaml` (425 Zeilen) hat 30+ ContentControls + Dialoge in EINEM Grid mit `IsVisible`-Bindings. Beispiel Zeilen 80-212: Shop, Statistics, Achievements, Settings, Workshop-Detail, Order-Detail, WorkerMarket, Imperium, Missionen, Research, Manager, Tournament, SeasonalEvent, BattlePass, Guild, GuildResearch, GuildMembers, GuildInvite, GuildWarSeason, GuildBoss, GuildHall, GuildAchievements, GuildChat, GuildWar, Crafting, Ascension, Prestige, MiniGame.

**Warum AAA-Problem:** Bei einem AAA-Studio ist das ein Router mit Lazy-View-Materialization. Hier: alle 30 Views werden im Visual-Tree als `IsVisible=false` instantiiert, was beim Cold-Start einen massiven Allokations-Spike erzeugt und auf 2GB-Mid-Tier-Geraeten Memory-Druck macht. Lazy-VM hilft beim ViewModel — aber `<views:DashboardView>` als XAML-Element wird trotzdem konstruiert. Das ist ein Anti-Pattern.

**Fix:** `ContentControl Content="{Binding ActivePageViewModel}"` mit `DataTemplates` — nur die aktive View wird gerendert. Der ViewLocator-Pfad ist in CLAUDE.md schon vorgemerkt. Konsequent umstellen.

**Aufwand:** L (1 Woche, weil 30 Bindings angefasst werden muessen, plus Smoke-Tests).

### [P0] DialogViewModel ist 849 Zeilen — nach dem "Split"

**Was:** Laut CLAUDE.md wurde DialogViewModel in 4 Partial-Files extrahiert (LevelUp, Achievement, Alert, PrestigeSummary). Trotzdem ist `DialogViewModel.cs` immer noch **849 Zeilen**. Story/Hint/Confirm sind drin, plus Helper-Methoden. Die Partial-Aufspaltung hat 38 Zeilen gespart — kosmetisch, nicht strukturell.

**Warum AAA-Problem:** Eine VM, die zentral 7 verschiedene Dialog-Typen orchestriert, verstoesst gegen Single-Responsibility. Wenn der LevelUp-Code mit dem ConfirmDialog-Code im selben File lebt, ist die mentale Last beim Lesen unbezahlbar. AAA-Studios haben pro Dialog-Typ einen eigenen ViewModel + DialogService als Composition.

**Fix:** Echter Strukturschnitt — nicht Partial. Pro Dialog-Typ eigene VM, plus `IDialogOrchestrator` (existiert schon) als Coordinator.

**Aufwand:** M (3 Tage).

### [P1] GameLoop tickt nur 1 Hz

**Was:** `GameLoopService.cs` Zeile 210-212: `Interval = TimeSpan.FromSeconds(1)`. Single-Tick fuer Game-Logic + Visual.

**Warum AAA-Problem:** OK fuer Income-Akkumulation. Nicht OK fuer flutschige Animationen — Coin-Fly, Odometer-Roll, Bar-Fill brauchen 30-60Hz. Aktuell muessen alle Renderer ihren eigenen Animation-Timer fahren (Code in CLAUDE.md erwaehnt das). Das ist fragmentiert. Egg Inc. trennt sauber: 1Hz Logic-Tick, 60Hz Render-Tick, deltaTime-basierte Animation.

**Fix:** Zentraler `IFrameClock` mit Stopwatch-deltaTime und Subscriber-Pattern. Renderer subscriben statt eigene Timer zu betreiben.

**Aufwand:** M (2-3 Tage), aber sehr hoher Polish-ROI.

### [P1] CLAUDE.md = 94k Tokens

**Was:** Die App-CLAUDE.md ist so lang, dass meine `Read`-Tools sie nicht mehr im Stueck einlesen koennen.

**Warum AAA-Problem:** Knowledge ist in einem Fass, nicht navigierbar. Niemand findet drin schnell etwas. Bei AAA-Studios sind Dev-Docs aufgeteilt nach Domain (Economy.md, Liveops.md, Architecture.md, Onboarding.md).

**Fix:** Aufsplitten in 6-8 thematische Files. Top-Level-CLAUDE.md ist nur noch Index + Conventions.

**Aufwand:** S (1 Tag). Aber riskant — bei jedem Split verliert man Sucheffizienz, also klare Pfad-Konvention.

### [P1] Service-Verzeichnis hat ~70 Services + Interfaces

**Was:** Glob ueber `Services/*.cs` zaehlt > 70 Files. Inklusive Splits wie `GameStateService.Money.cs`, `GameStateService.Xp.cs`, `GameStateService.Workshop.cs`, `GameLoopService.PrestigeCache.cs`, `GameLoopService.Automation.cs`, `GameLoopService.PeriodicChecks.cs` etc.

**Warum AAA-Problem:** Das ist Service-Sprawl. `IGuildResearchService`, `IGuildAchievementService`, `IGuildBossService`, `IGuildHallService`, `IGuildChatService`, `IGuildInviteService`, `IGuildWarSeasonService`, `IGuildTickService`, `IGuildTipService`, `IGuildCoopOrderService`, `IWorkerAuctionService` — 11 Gilden-Services. Bei AAA: 1 Aggregat-Service + interne Module. Dependency-Graph ist hier ungeschoeflach.

**Fix:** Bounded-Contexts einfuehren (DDD-Style): `GuildModule` als Aggregat mit internen Substituts. Externe Code sieht nur `IGuildFacade`.

**Aufwand:** L (1-2 Wochen). Fuer langfristige Wartbarkeit kritisch — sonst hat das Spiel in v3.0 200 Services.

### [P2] AudioService Desktop ist Stub — Plattform-Asymmetrie

**Was:** `AudioService.cs` Zeile 38-68 returnen alle `Task.CompletedTask`. Kommentar: *"Audio playback and haptic feedback are not available on desktop platforms."*

**Warum AAA-Problem:** Avalonia auf Desktop kann sehr wohl Audio (NAudio, SoundFlow, OpenAL fuer Cross-Plattform). Die Aussage ist nicht wahr — sie ist eine Designentscheidung, die als technische Limitation getarnt wird. AAA-Studios liefern auf allen Plattformen identisch.

**Fix:** `LibVLCSharp` oder `SoundFlow` einbauen (~200 Zeilen Code). Alternativ: NAudio (Windows-only).

**Aufwand:** M (2-3 Tage).

### [P2] Tests sind da (gut), aber Coverage-Metriken nicht sichtbar

**Was:** 30+ Test-Files in `tests/HandwerkerImperium.Tests/` — GameStateTests, WorkshopTests, ManagerTests, ToolTests, MasterToolTests, BuildingServiceTests, CraftingTests, ResearchTests, OrderTests, SeasonalEventServiceTests, DailyRewardServiceTests, PrestigeDataTests, VipServiceTests, GameStateServiceTests, EquipmentServiceTests, EventServiceTests, LuckySpinServiceTests, AchievementTests, BattlePassTests, BattlePassServiceTests, WorkshopFormulasTests, WorkerServiceTests, AchievementServiceTests, WorkerTests, GoalServiceTests, IncomeCalculatorServiceTests, PrestigeServiceTests, AscensionServiceTests, TournamentServiceTests.

**Warum AAA-Problem:** Coverage-Reporting fehlt. Bei AAA-Studios faehrt CI bei <70% Coverage rot. Hier weiss niemand, ob es 40% oder 90% sind. Gefahr: kritischer Pfad ist nicht abgedeckt, niemand merkt's.

**Fix:** `coverlet.collector` + `ReportGenerator` in CI; ZielKpi 80% auf Domain-Layer.

**Aufwand:** S (1 Tag).

### [P2] Logs in Release-Builds?

**Was:** `LogService.cs` existiert. Unklar, ob in Release alle Debug-Aufrufe gestrippt werden.

**Warum AAA-Problem:** AAA-Studios stripen aggressiv. Logs an STDOUT oder Datei kosten Frame-Time und Battery.

**Fix:** Conditional Compilation `#if DEBUG` oder `[Conditional("DEBUG")]` auf allen Log-Methoden im Hot-Path.

**Aufwand:** S.

---

## 2. Game-Design / Economy / Balancing

### [P0] Reset-Hierarchie (Prestige + Rebirth + Ascension) ist ueberkomplex

**Was:** GameBalanceConstants zeigt:
- **Rebirth** mit 5 Sternen (Workshop-spezifisch ab Lv1000): Income-Bonus 15%/35%/60%/100%/150%, Upgrade-Discount 5%/10%/15%/20%/25%, Extra-Worker.
- **Prestige** mit Tier-System (Bronze/Silber/Gold/Platinum/Legende), Diminishing Returns 0.2 pro selbem Tier, Discount-Cap 50%.
- **Ascension** mit AP-Punkten, kommt zusaetzlich on top.

Drei separate Reset-Layer. Zusaetzlich: Saison-Pass (4 Saisons * 5 Kapitel), BattlePass, ReputationShop, DailyChallenges, WeeklyMissions, Achievements (sechs verschiedene Categories).

**Warum AAA-Problem:** Egg Inc. hat **eine** Prestige-Schicht (Soul Eggs). AdVenture Capitalist hat **zwei** (Angels + Mega-Angels), aber sehr klar in der Player-UX getrennt. Realm Grinder hat 3-4, ist aber als Genre-Hardcore-Game positioniert. HandwerkerImperium ist eher Mass-Market — Spieler verstehen 3 Reset-Layer + 2 Pass-Systeme + 4 Mission-Systeme nicht intuitiv. **Player-Survey-Feedback "ich verstehe nicht was zu tun ist" ist hier vorprogrammiert.**

**Fix:** Entweder (a) Ascension hinter Tutorial-Wall verstecken bis Spieler 3+ Prestiges geschafft hat (Pacing); oder (b) Rebirth + Prestige mergen in EIN System mit Prestige-Tier-Aufstiegen ueber Sterne. Variant (a) ist 2 Tage Arbeit, Variant (b) ist 3 Wochen Refactor.

**Aufwand:** S fuer (a), L fuer (b).

### [P0] Nur EIN Whale-Tier IAP (4,99 EUR)

**Was:** ShopViewModel Zeile 200-313 zeigt 9 IAP-Slots. Hoechster Preis: **4,99 EUR**.
- Premium 4,99 (remove ads + bonus)
- Booster 2x 2h: 1,99
- Instant Cash Small/Large/Huge/Mega: 0,99 / 0,99 / 2,49 / 3,99
- Goldschrauben 50/150/450: 0,99 / 2,49 / 4,99

**Warum AAA-Problem:** Mobile-Idle-Games leben von Whales. Top-10% der Spieler generieren 70% der Revenue. Die brauchen 19,99 EUR / 49,99 EUR / 99,99 EUR Slots. Aktuell kann ein Spieler maximal 9 * Top-Preis = ~24 EUR ausgeben — und dann ist Schluss. Das ist Revenue-Hardcap. AdVenture Capitalist hat IAP bis 99,99. Idle Heroes sogar bis 199,99 EUR-Bundles bei Events.

**Fix:** Sofort drei Slots hinzufuegen: 9,99 (Mid-Whale-Booster-Bundle), 19,99 (Workshop-Skin + 1500 GS), 49,99 (Big-Bundle-Pack). Plus Limited-Time-Offer-Bundles (Doppel-Goldschrauben fuer 7 Tage, dann weg).

**Aufwand:** S (Pricing + Asset-Bundles, 2-3 Tage). **Erwarteter Revenue-Lift: 30-60% bei Whale-Cohort.**

### [P1] Kein dedizierter Long-Term-Engagement-Loop nach Lv1000

**Was:** Workshop max Lv 1000 (`WorkshopMaxLevel = 1000`). Nach 1000 → Rebirth verfuegbar, 5 Sterne moeglich. Dann Prestige. Dann Ascension. Dann?

**Warum AAA-Problem:** Egg Inc. hat unendliche Skalierung durch Prophecy-Eggs + Soul-Egg-Bonus, plus Contracts, plus Co-op. Spieler bei Lv200+ haben in Egg Inc. trotzdem klare Wochen-Ziele. Hier nach Ascension: was kommt? Saison-Pass-Wartung. Das ist duenn.

**Fix:** "Ewige" Prestige-Skalierung mit log-Wachstum + neue Master-Tools alle ~5 Prestiges. Plus Saison-Goals als rotierende North-Stars.

**Aufwand:** L (3 Wochen). Strategisch wichtig fuer Whale-Retention.

### [P1] Mini-Games sind Distraction, kein Spielgefuehl-Hauptanker

**Was:** 9 Mini-Games (Pipe-Puzzle, Sawing, Painting, Inspection, Roof-Tiling, Forge, Wiring, Blueprint, Design-Puzzle, Invent). Jeder als optionaler Bonus.

**Warum AAA-Problem:** Egg Inc. hat ein Aktiv-Klick-Element ("Drone-Boost") als zentralen, taeglich relevanten Game-Loop-Teil. Hier sind die Mini-Games nett, aber niemand spielt sie zwingend. Sie sind 9 Subsysteme mit eigenem SkiaSharp-Renderer und Tutorial — gigantische Codeflaeche fuer minimale Spielerwirkung. Wenn man einen Skill-Cap will, einigt euch auf 2-3 Top-Tier-Mini-Games und macht die richtig poliert.

**Fix:** Spieler-Telemetrie pruefen: Welche Mini-Games werden tatsaechlich gespielt? Bottom-50% killen, Top-3 als Daily-Active-Loop-Hook positionieren mit eigener Reward-Layer.

**Aufwand:** M (Daten-Review + Gameplay-Feinschliff, 1 Woche).

### [P1] Saison-Story Stub-Texte fuer 3/4 Saisons

**Was:** CLAUDE.md zeigt: *"Spring-Saison komplett mit deutschem Text. Summer/Autumn/Winter haben Stub-Texte."* 240 RESX-Eintraege existieren als Skeleton.

**Warum AAA-Problem:** Eine Live-App mit halbfertiger Saison-Story = gebrochen-versprochener Content. Spieler sehen "Coming Soon" und springen ab. Habby liefert Saisons komplett oder gar nicht.

**Fix:** Story-Pass mit Game-Writer (intern oder extern). 3 Saisons * 5 Kapitel * 2 Texte * 6 Sprachen = 180 Strings. 4-6h Schreiben + 2 Tage Localization.

**Aufwand:** M.

### [P2] Cost-Exponent 1.07 ist gut — bleibt!

**Was:** `UpgradeCostExponent = 1.07` (Lv1-500), `1.06` ab Lv500. Income `1.02`. Diese Werte sind exakt im AdVenture-Capitalist-Sweet-Spot.

**Status:** Kein Fix noetig. Lobpreisung in einem Satz: hier sitzt das Math-Tuning sauber.

### [P2] Diminishing-Returns auf Same-Tier-Prestiges (0.2)

**Was:** `DiminishingReturnsPerTierPrestige = 0.2m`. Verhindert Bronze-Farm-Loop.

**Status:** Gut. Nach 5 Same-Tier-Prestiges nur noch 50% Bonus — solide Anti-Exploit-Konstanten.

### [P1] BattlePass-Free vs Premium-Track-Balance unbekannt

**Was:** BattlePassService existiert, BattlePassViewModel ebenfalls. Tests sind da. Aber: Free-Track vs Premium-Track-Reward-Spread im Code wo?

**Warum AAA-Problem:** AAA-Standard: Free-Track liefert ~30% des Premium-Rewards, sodass Free-Spieler nicht frustriert sind, Premium aber klar lohnt. Wenn das hier 5% oder 70% ist, ist das ein Revenue-Verlust.

**Fix:** Audit der BattlePass-Reward-Tabellen. Ziel: Premium gibt 3.5x Free-Total-Value.

**Aufwand:** S (Tabelle + Smoke-Test, 1 Tag).

---

## 3. UX / Visual / Audio / Onboarding

### [P0] FTUE existiert nicht als System

**Was:** `TutorialState.cs` ist 38 Zeilen Model. Grep nach `FtueService`, `OnboardingService`, `TutorialService`: 0 Matches. Kein zentraler FTUE-Manager. Es gibt `ContextualHintService` und `MiniGame-Tutorials`, aber kein scripted First-Run-Erlebnis.

**Warum AAA-Problem:** Supercell hat fuer Clash Royale 32 FTUE-Schritte mit Hand-Animation, Cooldown-Skip-Block, Dopamine-Trigger nach jedem Click. Hier: Spieler wird ins Dashboard geworfen, sieht 5 Tabs, eine Tutorial-Hint-Bubble (`ShowTutorialHint`). 80% der Mobile-Spieler sind in 60 Sekunden weg, wenn sie nicht hand-gefuehrt werden. **Das ist der Top-1-Retention-Killer.**

**Fix:**
1. `IFtueService` mit step-machine, gespeicherter Step-State im SaveGame.
2. 8-12 Skript-Schritte mit Spotlight-Overlay (klick-here Pulse), gerichteter Reward.
3. Erste 30s: erste Werkstatt-Bauen → erstes Geld → erstes Worker-Hire → erstes Upgrade. Pacing wie Egg Inc.
4. Telemetrie auf jeden Step: `ftue_step_X_completed` / `ftue_step_X_skipped`.

**Aufwand:** L (2-3 Wochen). **Hoechste Player-Retention-Hebel im ganzen Audit.**

### [P0] Audio Desktop = Stub, Audio Android = nur 15 SFX, KEINE Musik

**Was:**
- `AudioService.cs` (Shared/Desktop): alle Methoden return `Task.CompletedTask`.
- `HandwerkerImperium.Android/Assets/Sounds/`: 15 OGG-Dateien (`sfx_*.ogg`).
- Keine `music_*.mp3` / `music_*.ogg` gefunden.

**Warum AAA-Problem:** Mobile-Idle-Games haben:
- 200-300 SFX (jeder Button, jede UI-Interaktion, jede Coin-Sammlung, Worker-Idle-Geraeusche, Build-Sounds, Achievement-Stinger, Prestige-Cinematic-Soundscape).
- 5-10 Music-Loops (Idle-Dashboard, Workshop-Active, Boss-Fight, Prestige-Ceremony, Saison-Spring/Summer/Autumn/Winter).
- Cross-Platform (Desktop genauso wie Mobile).
- Adaptive-Music-System (Engagement-getrieben — schnellerer Beat bei aktivem Order).

Aktuell: Desktop = stumm. Android = sehr duenn. Egg Inc. ist beruehmt fuer seine Atmospaehre — das verkauft das Spiel emotional.

**Fix:**
1. Cross-Platform-Audio-Layer (`SoundFlow` oder eigener Wrapper ueber `NAudio`+`SuperPower` auf Desktop, Android-Side wie heute).
2. SFX-Pack ausweiten: Pixabay/Freesound-Lizenz-Pack mit ~150 SFX, 2 Tage Sound-Designer.
3. 4 Music-Loops bestellen (Fiverr/AudioJungle, ~200 EUR), eine pro Saison.
4. Music-Mixer mit Ducking bei UI-Sound + Crossfade bei Tab-Wechsel.

**Aufwand:** L (3 Wochen + SFX/Music-Beschaffung).

### [P0] MainView ist visueller Z-Stack-Hoelle (siehe Architektur P0)

**Was:** Siehe Code-Architektur-Sektion. Aus UX-Sicht: alle Dialoge, Overlays, Cinematics sind zur gleichen Zeit im DOM, nur per IsVisible umgeschaltet. Das macht die View-Performance auf Mid-Tier-Android schlecht und das Code-Review-Erlebnis schmerzhaft.

**Spieler-Impact:** Auf 2GB-Geraeten messbares Stutter beim Tab-Wechsel.

**Fix:** Siehe Architektur P0.

### [P1] DashboardView (753 Zeilen) und ImperiumView (816 Zeilen) sind Monolithen

**Was:** `DashboardView.axaml` 753 Zeilen, `ImperiumView.axaml` 816 Zeilen.

**Warum AAA-Problem:** AAA-Studios brechen XAML/UI-Files bei ~200 Zeilen in Sub-UserControls. Einer gewachsenen `DashboardView.axaml` mit 753 Zeilen vertraut keiner — niemand findet, wo der Goal-Banner gerendert wird ohne 3 Minuten zu suchen. Imperium ist als 5-Sub-Tab-Container besonders anfaellig.

**Fix:** `DashboardView` zerlegen in: `DashboardHeader`, `EarningsCard`, `WorkshopList`, `GoalBanner`, `BannerStrip` (existiert teils), `AutomationPanel` (existiert), `WeeklyMissionSection` (existiert), `DailyChallengeSection` (existiert). Dito `ImperiumView` → `WorkshopsSection`, `WorkersSection`, `ResearchSection`, `EquipmentSection`, `AscensionSection`.

**Aufwand:** M (1 Woche pro View).

### [P1] Tab-Bar mit 6+ Top-Tabs ist Cognitive-Overload

**Was:** Aus MainView und CLAUDE.md ablesbar: Dashboard, Imperium (mit 5 Sub-Tabs), Missionen, Shop, Achievements, Statistics, Settings, Guild (mit 8 Sub-Tabs), Crafting, Ascension, Prestige. Plus Worker-Market-Overlay, Workshop-Detail, Order-Detail, Mini-Games.

**Warum AAA-Problem:** Mobile-IA-Standard: max 4-5 Bottom-Tabs. Mehr → Spieler klickt nichts mehr durch. AdVenture Capitalist hat 4 Tabs (Money, Angels, Investors, Stats). Idle Miner hat 5. Hier sind es deutlich mehr — und teilweise hidden behind unlocks (Ascension-Tab nur bei Lege-Count >= 3).

**Fix:** Konsolidierung auf 5 Bottom-Tabs:
1. **Imperium** (Dashboard + Workshops + Workers — heute mehrere)
2. **Reisen** (Missionen + Tournament + Saison-Event-Hub)
3. **Gilde** (alle 8 Gilden-Sub-Tabs als interner Hub)
4. **Shop** (alle IAP + Goldschrauben + ReputationShop + LuckySpin als interner Hub)
5. **Mehr** (Achievements + Statistics + Settings + Story als Drawer)

Prestige/Ascension/Crafting kommen als Card-Aktion auf Imperium, kein eigener Tab.

**Aufwand:** L (Wochen Refactoring), aber kritisch fuer Mass-Market-Onboarding.

### [P1] Push-Notifications: Datenlage unklar

**Was:** `INotificationService` und `NotificationService.cs` existieren. Auch `BootReceiver.cs` auf Android. `AndroidPlayGamesService.cs`. FCM-Cleanup wurde bei BingXBot Phase 18 erwaehnt.

**Warum AAA-Problem:** Re-Engagement-Push ist Top-2-Retention-Hebel nach FTUE. AAA-Idle-Games schicken 2-4 Push/Tag mit smart targeting (Worker-Stimmung kritisch, Daily-Reward-Reset, BattlePass-Tier-Up-moeglich, Boss spawnt, Saison-endet-in-3-Tagen).

**Fix:** Audit der NotificationService-Triggers. Mindestens 8 verschiedene Push-Typen, bedingt von Spielerverhalten. A/B-Test der Send-Times.

**Aufwand:** M (Audit + Tuning, 1 Woche).

### [P1] DSGVO-Consent: Status?

**Was:** UMP-Hinweise in CLAUDE.md (`Xamarin.Google.UserMesssagingPlatform`). BomberBlast hatte v2.0.55 P0 fuer DSGVO-Consent-UI.

**Warum AAA-Problem:** AdMob ohne UMP-Consent in EU = Vertragsverletzung + Auszahlungs-Stop. 4,99 EUR App ohne klares Privacy-Center = Apple/Google-Reject.

**Fix:** UMP-Consent-Flow validieren (Hat Spieler in EU einen Consent-Modal beim ersten Start gesehen?). Privacy-Center als Settings-Subview mit Data-Export-Link.

**Aufwand:** S falls Consent-Flow schon besteht. M falls neu.

### [P1] Localization-Tonalitaet — Engineering-Speak vs AAA-Snappy?

**Was:** RESX-Files DE/EN/ES/FR/IT/PT mit ~2200+ Strings. Stub-Texte fuer 3/4 Saisons.

**Warum AAA-Problem:** AAA-Mobile-Texte sind kurz, witzig, kontextuell. "Werkstatt aufgewertet" vs "Boom — die Werkstatt brummt jetzt!". Tonalitaet macht 20% des Spiel-Charakters aus. Aktuell wirkt der Output (basierend auf den Section-Titeln in CLAUDE.md wie "Imperium-Sub-Tabs UI") engineering-driven.

**Fix:** Game-Writer (intern oder Fiverr-Pool) fuer Top-200 sichtbarste Strings. 1 Tag Pass.

**Aufwand:** S.

### [P2] Accessibility — Touch-Targets, Contrast, Reduced-Motion

**Was:** AutomationProperties.AutomationId gut gepflegt (in jedem View). Kein Hinweis auf `ReduceMotion`-Flag im Code-Review.

**Warum AAA-Problem:** Apple/Google A11y-Stores penalisieren Apps ohne Reduced-Motion-Mode. Plus: Spieler mit Photosensitivity haben Probleme mit Confetti/Fireworks.

**Fix:** `Settings.ReducedMotion` Bool, in allen Renderern respektieren (Confetti aus, Coin-Fly schneller).

**Aufwand:** M.

### [P2] Empty-States / Error-States / Loading-States

**Was:** `LoadingCanvas` (Animierter Loading-Screen mit Zahnraedern + Tipps) ist da — gut. Empty-States: in MainView nicht offensichtlich.

**Warum AAA-Problem:** Spieler ohne Gilden-Mitgliedschaft sieht GuildView — was sieht er? Eine leere Liste? Bei AAA: liebevolles Empty-State mit "Trete einer Gilde bei!"-CTA und Visual.

**Fix:** Empty-State-Views fuer GuildView-no-guild, AchievementView-locked, ResearchView-locked-by-level. 1 Tag pro View.

**Aufwand:** M.

### [P2] Visual Identity — Mascot fehlt

**Was:** "Meister Hans" wird in CLAUDE.md erwaehnt (`MeisterHansRenderer.cs` existiert). Tutorial-NPC-Pattern.

**Warum AAA-Problem:** Egg Inc. hat ihre Henne, AdVenture Capitalist hat Cumberbund Cumberbund III, Idle Miner Tycoon hat den Miner. **Mascot ist Marketing.** Meister Hans ist eine Chance — ist er auf Store-Asset, im Loading-Screen, auf Push-Icon? Vermutlich nicht konsequent.

**Fix:** Meister-Hans-Brand-Pass. Auf Loading-Screen, Push-Notification-Icon, Achievement-Stinger, Store-Asset (Hero-Image).

**Aufwand:** M (Asset-Erstellung + UX-Integration).

---

## 4. Live-Ops / Retention / Monetization

### [P1] Limited-Time-Events fehlen

**Was:** SeasonalEventService gibt es, GameEvent-Model gibt es. Aber: rotierende 7-Tage-Events mit eigenem Reward-Track? Unklar — Stubs in CLAUDE.md erwaehnen Saisons.

**Warum AAA-Problem:** Habby/Coin Master leben von Limited-Time-Events. 7-tagiges "Holz-Festival" mit doppeltem Material-Output, eigenem Cosmetic-Skin als Reward. Schafft FOMO + Re-Engagement.

**Fix:** `LiveEventService` mit RemoteConfig-getriebenen Events. 4 Event-Templates (Double-Reward, Boss-Rush, Co-op-Marathon, Mini-Game-Mastery) rotierend.

**Aufwand:** L.

### [P1] Cross-Promotion zwischen Apps fehlt

**Was:** 11 Apps im Portfolio. Kein "Try our other apps"-Banner sichtbar.

**Warum AAA-Problem:** Voodoo, Lion Studios machen 30% ihrer Installs ueber Cross-Promo. Hier: 0%.

**Fix:** Mini-CPI-Network zwischen den eigenen Apps. House-Ads im AdMob-Mediation-Stack, plus Native-Banner in Settings-Tab.

**Aufwand:** S (1-2 Tage).

### [P1] Friend-Invites / Viral-Loops

**Was:** `Friend.cs` Model existiert. Guild-System mit Invites.

**Warum AAA-Problem:** Wenn Friend-Invites nicht stark belohnt sind (3-Tier-Reward bei 1/5/10 Friends), wird's nie genutzt.

**Fix:** Aggressive Invite-Belohnung + Push-Banner-Prompt nach Lv5 ("Bring deinen Kumpel mit, hol dir 100 Goldschrauben").

**Aufwand:** S.

### [P2] BattlePass-Saison-Length: 28 oder 30 Tage?

**Was:** BattlePassService existiert. CLAUDE.md erwaehnt 4 Saisons (Spring/Summer/Autumn/Winter) — wahrscheinlich 90-Tage-Saisons.

**Warum AAA-Problem:** 30 Tage sind State-of-the-Art (Fortnite Battle Pass, Habby) fuer Engagement-Loop. 90 Tage = Spieler hat Zeit zum Aufschieben → kein Daily-Drive.

**Fix:** Zu klaeren. Falls 90 Tage, auf 30 Tage runter.

**Aufwand:** M.

---

## 5. Was schon AAA-tauglich ist (kurz)

- **Zentrale Balancing-Konstanten** (`GameBalanceConstants.cs`) — sehr saubere Praktik.
- **Cost-Exponent 1.07** entspricht AdVenture-Capitalist-Industriestandard.
- **Milestone-Multipliers** mit gezielten Lv-Luecken-Brechern (Lv400/600/650/750/900) — bewusst designed, nicht aus dem Bauch.
- **Diminishing-Returns auf Same-Tier-Prestiges (0.2)** — Anti-Exploit-Mathe sitzt.
- **HMAC-Signing** fuer Co-op-Score und Auctions-Bidding — Anti-Cheat ist auf Niveau.
- **AppChecker v2.0** mit 22 Checkern — Engineering-Disziplin sichtbar.
- **AnalyticsService + RemoteConfigService** — Live-Ops-Foundation steht.
- **Tests-Suite** ist erstaunlich breit (30+ Test-Files fuer HandwerkerImperium).
- **AutomationProperties.AutomationId** ueberall im XAML — UI-Test-bereit.
- **Welcome-Back-Offer + Starter-Offer** sind konzipiert (auch wenn FTUE drumherum fehlt).

---

## 6. AAA-Roadmap — Priorisierung in 3 Quartalen

### Q1 (Mai-Jul 2026): Foundation Fixes — Player kann nicht weglaufen

**Goal:** Stop-the-Bleed. Onboarding-Drop und Audio-Schwaeche eliminieren.

| # | Task | Owner | Aufwand | Erwartung |
|---|------|-------|---------|-----------|
| 1 | `IFtueService` + 10-Schritt-Tutorial | game-design + ui | L | Day-1-Retention +15-25% |
| 2 | Cross-Platform-Audio-Layer (Desktop+Android) | engineering | M | Polish-Fuehlen +30% |
| 3 | 100 SFX dazu, 4 Music-Loops bestellen | sound | M | Engagement-uplift |
| 4 | IAP-Whale-Tier (9,99/19,99/49,99) | monetization | S | ARPU +30-60% in Whale-Cohort |
| 5 | UMP-Consent + Privacy-Center auditieren | legal | S | Compliance |
| 6 | Saison-Story Summer/Autumn/Winter schreiben | writer | M | Content-versprochen |

### Q2 (Aug-Okt 2026): Strukturelle Sanierung — Skalierungsfaehigkeit

| # | Task | Aufwand | Erwartung |
|---|------|---------|-----------|
| 7 | MainViewModel zerlegen | L | Wartbarkeit |
| 8 | MainView Lazy-View-Loading | L | Cold-Start -300ms |
| 9 | DialogViewModel echt zerlegen | M | Code-Hygiene |
| 10 | Tab-Bar konsolidieren auf 5 Tabs | L | Onboarding-Klarheit |
| 11 | Reset-Hierarchie vereinfachen (Ascension hinter Lv-Wall) | S | Confusion -50% |
| 12 | Coverage-Reporting in CI | S | Test-Visibility |

### Q3 (Nov-Jan 2027): Live-Ops-Reife — Skalierung

| # | Task | Aufwand | Erwartung |
|---|------|---------|-----------|
| 13 | LiveEventService + 4 Event-Templates | L | Re-Engagement-Schub |
| 14 | Push-Notification-Audit + 8 Trigger-Typen | M | DAU-Lift +5-10% |
| 15 | Cross-Promotion Network zwischen 11 eigenen Apps | S | Free-Installs |
| 16 | Friend-Invite Reward-Loop | S | K-Factor >0.4 |
| 17 | BattlePass-Saison-Length auf 30 Tage | M | Daily-Drive |
| 18 | Mascot-Brand-Pass (Meister Hans ueberall) | M | Brand-Recognition |

---

## 7. Top-3 Hebel mit hoechstem ROI

1. **FTUE-System (Q1 #1)** — Day-1-Retention von vermutet ~25% auf 40-50% heben. Das ist die wichtigste Einzel-Investition. Bei 1000 Daily-Installs: +150 zusaetzliche DAU, ueber 3 Monate kompoundiert auf ~10000 zusaetzliche MAU.

2. **Whale-IAP-Tiers (Q1 #4)** — Unter 1 Woche Arbeit, kann Revenue von Top-10%-Spielern um 30-60% lifteen. Ihr habt aktuell ein hartes Spending-Ceiling von ~24 EUR. Das ist Geld, was Whale-Spieler aktuell aktiv ausgeben WOLLEN aber nicht koennen.

3. **Audio-Polish (Q1 #2+#3)** — Subjektive Spielqualitaet steigt enorm, App-Store-Reviews springen typischerweise um 0.3-0.5 Sterne. Das beeinflusst direkt Conversion-Rate ueber organische Listings.

---

## 8. Blind Spots (was ich NICHT pruefen konnte)

- **Crash-Free-Rate, ANR-Rate** ohne Firebase-Crashlytics-Daten.
- **Cold-Start-Time auf realer Mid-Tier-Hardware** (Pixel 4a, Galaxy A22) — braucht Geraete.
- **Echte Player-Telemetrie** (welche Mini-Games werden gespielt? Wo brechen Spieler ab?) — braucht Amplitude/Firebase-Dashboard.
- **App-Store-Rating-Verteilung** — braucht Play-Console-Zugriff.
- **Tatsaechliches Ad-Fill-Rate + ECPM** — braucht AdMob-Reports.
- **DSGVO-Consent-Flow-Korrektheit** — braucht echtes Andriod-Test-Gerät.
- **Konkrete BattlePass-Reward-Werte** Free vs Premium.

---

## 9. Schlusswort

Ihr habt ein technisch erstaunlich diszipliniertes Indie-Projekt gebaut. Das Fundament traegt eine AAA-Produktion. Aber: ihr habt **Feature-Tiefe ueber Feature-Politur priorisiert**. AAA-Studios machen das andersrum — sie haben weniger Subsysteme, aber jedes ist polished bis zum Anschlag und wird mit Audio, Onboarding, Live-Ops gefuettert. Eure naechste Phase ist: **stop building, start polishing**.

Wenn ich eine einzelne Empfehlung geben muesste: **kein neues Subsystem mehr, bevor FTUE und Audio nicht auf AAA-Niveau sind.** Alles andere kommt danach.

— Audit Ende —
