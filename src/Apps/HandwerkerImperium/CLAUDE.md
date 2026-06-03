# HandwerkerImperium — Idle-Game

Handwerker-Imperium aufbauen: Werkstätten kaufen und upgraden, Arbeiter einstellen, Aufträge
abarbeiten, forschen, Gilden beitreten. Idle-Einkommen läuft passiv weiter, Mini-Games bringen
aktive Belohnungen.

| Aspekt | Wert |
|--------|------|
| Package-ID | com.meineapps.handwerkerimperium |
| Ads | Rewarded (13 Placements, kein Banner-Banner — Ad-Spacer-Row in MainView) |
| Premium | 4,99 EUR Lifetime ("Imperium-Pass") |
| Plattform | Android (primär), Desktop (Test) — `net10.0` Shared, `net10.0-android` Host |

Diese Datei beschreibt das **Spiel als Ganzes** (Game-Design, Monetarisierung, ordnerübergreifende
Konzepte) und verweist für jede Implementierung in die Unterordner-Doku. Generische Conventions
(MVVM, DI, DateTime, Naming, Build) → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Dispose-Reihenfolge, Platform-Factories | `App.axaml.cs`, ~70 Services, ~50 VMs, Loading-Pipeline | [Shared](HandwerkerImperium.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Factory-Wiring, AdMob, In-App Review | [Android](HandwerkerImperium.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs`, DesktopAudioService | [Desktop](HandwerkerImperium.Desktop/CLAUDE.md) |
| Services (Game-Loop, Coordinatoren, Gilden, Live-Ops, Facaden, Service-Gotchas) | Service-Hierarchie, IUiEffectBus, IFrameClock | [Shared/Services](HandwerkerImperium.Shared/Services/CLAUDE.md) |
| ViewModels (MainVM 13 Partials, DialogVM, Feature-/Guild-/MiniGame-VMs) | Composition, Host-Pattern, IsBusy, INavigable | [Shared/ViewModels](HandwerkerImperium.Shared/ViewModels/CLAUDE.md) |
| Views (5 Tabs, Sub-Tabs, Dialoge, MiniGames) | MainView-Layout, Lazy-Loading, Code-Behind-Regeln, View-Gotchas | [Shared/Views](HandwerkerImperium.Shared/Views/CLAUDE.md) |
| Models (GameState V7, GameBalanceConstants, Domain-Entities, SaveGame-Migration) | Persistenz-Versionen, Konfig-Kataloge | [Shared/Models](HandwerkerImperium.Shared/Models/CLAUDE.md) |
| Graphics (~55 SkiaSharp-Renderer, GameJuiceEngine, FpsProfile) | IDisposable-Klassifizierung, Caching, Scroll-Perf, Cinematic | [Shared/Graphics](HandwerkerImperium.Shared/Graphics/CLAUDE.md) |
| Icons (224 Bitmap-Icons, GameIcon-Control) | PathIcon-Ableitung, GameAssetService | [Shared/Icons](HandwerkerImperium.Shared/Icons/CLAUDE.md) |
| Helpers, Loading, Controls, Converters | RunHandlerSafely, ProfanityFilter, EmptyStateCard, WorkerAvatarControl | jeweilige Unterordner-CLAUDE.md |

**Single-Source-of-Truth-Anker** (alles andere leitet ab):
- **Alle Balancing-Zahlen** → `Models/GameBalanceConstants.cs` (NIE hardcoded außerhalb). Aktuelle
  Werte tabellarisch → Memory `balancing.md`.
- **Level-Gates** (Feature-/Tab-/Automation-Unlocks) → `Models/LevelThresholds.cs`.
- **Workshop-Farben** → `WorkshopTypeExtensions.GetColorHex()`. Alle Renderer + `WorkshopColorConverter` leiten davon ab.
- **Telemetrie-Namen** → `Models/AnalyticsEvents.cs`.

---

## Game-Design-Spezifikation

Die Mechanik-Identitäten leben hier; konkrete Zahlen → `GameBalanceConstants` / `balancing.md`;
Entity-Strukturen → [Shared/Models](HandwerkerImperium.Shared/Models/CLAUDE.md).

### 5-Tab-Navigation

| Tab | View | Inhalt |
|-----|------|--------|
| Werkstatt | DashboardView | City-Szene, Workshop-Karten, Automation-Panel, Quick-Jobs |
| Imperium | ImperiumView | Sub-Tabs (s.u.): Workshops, Lager, Worker, Forschung, Equipment, Ascension |
| Missionen | MissionenView | Heute (Daily, Quick-Jobs, Glücksrad) + Wettbewerbe (Weekly, Turnier, BattlePass) |
| Gilde | GuildView | 5-Tab-Hub (Übersicht/Kampf/Forschung/Chat/Mitglieder) |
| Shop | ShopView | IAP, Goldschrauben-Pakete, Ausrüstungs-Shop |

**Imperium-Sub-Tabs** (`ImperiumSubTab`-Enum): Workshops / Warehouse / Workers / Research /
Equipment / Ascension. Warehouse gesperrt bis Spielerlevel 50, Ascension bis `LegendeCount >= 3`
— beide IMMER sichtbar (Lock-Icon-Overlay statt Ausblenden, Layout-Stabilität).

### Game Loop (1s-Takt, GameLoopService)

Pro Sekunde: Idle-Einkommen (`IncomeCalculatorService`), Kosten, Worker-States (Mood-Decay,
Fatigue, Training, Kündigung bei Mood<20 — online + 24h-offline konsistent), AutoSave alle 30s
auf Background-Thread. Periodische Checks laufen tick-versetzt (Offsets gegen Frame-Spikes):
Automation alle 5, Lieferant alle 10, Live-Auftrag alle 25, QuickJob-/Mission-Rotation alle 60,
Manager/MasterTool alle 120, Auto-Produktion T1 alle 180 / höhere Tiers alle 360, Event-/Saison-/
BattlePass alle 300, Gilden-Tick jeden Tick (Sub-Service-Offsets → [Services](HandwerkerImperium.Shared/Services/CLAUDE.md)).

### Werkstätten (10 Typen)

Carpenter, Plumber, Electrician, Painter, Roofer, Contractor, Architect, GeneralContractor,
MasterSmith, InnovationLab. Spezial: MasterSmith 60s Auto-Produktion + passive Crafting-Materialien,
InnovationLab 120s + verdoppelt Research-Speed.

- **Spezialisierung** (ab Lv50, erste Wahl gratis, Re-Spec 20 GS): Efficiency (+30% Einkommen, −1 Slot),
  Quality (+15% Kosten, +20% Worker-Effizienz), Economy (−5% Einkommen, −25% Kosten).
- **Rebirth** (0–5 Sterne, permanent über Prestige+Ascension): Einkommens-Bonus +15%…+150%,
  Upgrade-Rabatt bis −25%, Extra-Worker bis +3.

### Arbeiter (10 Tiers F…Legendary)

EffectiveEfficiency = `BaseEff × XpBonus × MoodFactor × FatigueFactor × (1+Spec+Equip) × Personality × Talent`.
`HiringCost` wird persistiert (Marktpreise bleiben nach Neustart korrekt). S-Tier+ erst nach Research
`mgmt_10`; `mgmt_04` erhöht Pool 5→8. Aura-Bonus (S-Tier+, 5–20%) gedeckelt bei 50% (`MaxAuraBonus`).
3 Training-Typen (Efficiency/Endurance/Morale), Auto-Rest bei 100% Fatigue. Praktikanten (gratis,
F-Tier, max 2, Promotion nach 24h Spielzeit). Legende-Prestige sichert Top 3 Worker pro Workshop.

### Aufträge (OrderType)

Quick (immer, 0.6x) / Standard (1.0x) / Large (Lv10+, 1.8x) / Cooperation (Lv15+, ≥2 Workshops, 2.5x) /
Weekly (Lv20+, 3.0x, 7-Tage-Deadline) / MaterialOrder (Lv50+, kein MiniGame, Items liefern, 4h).
Stammkunden 20% Chance (1.1–1.5x). Live-Aufträge: `ExpiresAt` 45–180s, VIP (5%): 3x Reward/2.5x XP,
pausierbar (5-Min-Cap). Bis zu 3 parallele Aufträge (`ParallelOrdersByWorkshop`).
**Risk/Reward** pro Auftrag: Safe (0.75x, leichter) / Standard (1.0x) / Risk (2.0x, schwerer, Miss = 0 + −10 Rep).
**Material-Offer**: ab Lv30 optionales Material-Angebot (35% Chance) für Reward-/XP-Bonus; Materialien
werden atomar in `ReservedInventory` reserviert, bei HardFail trotzdem konsumiert ("echtes Risiko").

### Prestige (7 Tiers) → Ascension → Eternal Mastery

- **PP** = `floor(sqrt(CurrentRunMoney / 100_000))`. Tier-Boni Bronze +20%…Legende +800%,
  Diminishing Returns `×1/(1+0.1×tierCount)`, Cap 20x.
- **Bewahrung** steigt mit Tier (Bronze: Achievements/Premium/Settings … Legende: + Manager + beste Worker).
- **Herausforderungen** (max 3, additiv): Spartaner +45%, OhneForschung +30%, Inflationszeit +25%,
  SoloMeister +50%, Sprint +35%, KeinNetz +20%. `ChallengeConstraintService` durchsetzt die Constraints.
- **Bonus-PP** (nach Multiplikator): Perfect Ratings, volle Research-Branch, Gebäude-Level, Level über Tier-Min.
- **Meilensteine** (kumulativ, überleben Ascension) + wiederholbarer Wochen-Meilenstein (alle 7 Prestiges +5 GS).
- **Erbstücke**: Tier-4-Items überleben Prestige (max 3, +2%/Run); bei Ascension permanent (+0.5% forever).
- **Ascension** (Meta-Prestige nach 3× Legende): 6 Perks × MaxLevel 3 (54 AP), Vollreset inkl. Prestige-Daten.
- **Eternal Mastery**: permanenter Income-Bonus, skaliert mit jedem Prestige (+0.5% linear + 5er-/10er-Stufen),
  Soft-Cap, kein Reset bei Ascension — sichert Late-Game-Progression post-Lv1000.

**Reset-Pacing**: Ascension-Tab immer sichtbar, aber `IsEnabled` erst bei `LegendeCount >= 3`.
Foreshadowing-Hint nach 1. Prestige (`ContextualHints.AscensionPath`), Action-Hint nach 3× Legende.

### Reputation (0–100, Start 50)

Beeinflusst Auftragsbelohnungen (0.7x–1.5x). Tier-System (Beginner/CityKnown/RegionStar/IndustryLegend)
mit Hysterese (3-Punkte-Buffer gegen UI-Flackern, `CurrentTier` persistiert). Quellen: Auftrags-Rating,
Showroom-Gebäude, langsamer Decay >50. Reputation-Shop (ab Score ≥60, 5 Items). Tier-Up = Celebration.

### Events, Lieferant, Crafting & Warehouse

- **Events** (8 Typen, z.B. TaxAudit/WorkerStrike/HighDemand): Intervall skaliert mit Prestige-Tier.
  **Feierabend-Rush** 2h 2x (1x/Tag gratis). **Saisonale Events** (4/Jahr, SP-Währung, Event-Shop).
- **Lieferant**: Lieferung alle 2–5 Min (Geld/GS/XP/Mood/Speed; ab Lv50 25% Chance Material-Lieferung).
- **Crafting**: 30 Rezepte (10 WS × 3 Tiers, Unlock Lv50/150/300). Cross-Workshop-Inputs ab Lv100.
  Tier-4-Manufaktur am GeneralContractor ab WS-Lv500 (villa/skyscraper/imperium_hq, Heirloom-fähig).
- **Warehouse** (`IWarehouseService`): 20 Slots Start, Stack-Limit 50, Max 200 Slots. `Available =
  CraftingInventory − ReservedInventory`. Auto-Sell bei Stack-Overflow zum Marktpreis sonst Workshop-Pause.
  Stack-Limit wird VOR Job-Start und VOR Collect geprüft (kein Material-Burn).
- **Material-Markt** (`IMarketService`, ab Research `logi_05`): deterministischer Tagespreis
  (Seed `PlayerGuid ^ utcDay ^ productId`, ±50% Sinus-Welle), Event-Modulatoren, 5% Spread gegen Arbitrage.
- **Worker-Material-Affinität** (Wood/Metal/Stone/Art/Tech): bis +20% Crafting-Speed wenn Worker-Affinität
  zum Output-Produkt passt (anteilig pro matchendem Worker).

### Forschung

45 Nodes (`ResearchService`) in Branches inkl. `ResearchBranch.Logistics` (12 Lager-/Markt-/Crafting-Nodes).
**Gilden-Forschung**: 18 kollaborative Forschungen in 6 Kategorien (Infrastruktur/Wirtschaft/Wissen/
Logistik/Arbeitsmarkt/Meisterschaft), permanente Gildenboni.

### Meisterwerkzeuge (12 Artefakte)

5 Seltenheiten, permanente Einkommens-Boni (gesamt +74%), Freischalt-Bedingungen (Workshop-Level,
Auftrags-/MiniGame-Zahlen, Prestige-Tiers). Prüfung alle 2 Min im GameLoop → `MasterToolUnlocked`-Event.

### Auto-Produktion

Alle 10 Workshops produzieren passiv Tier-1 Items ab WS-Lv50 (Standard 180s/Worker, InnovationLab 120s,
MasterSmith 60s). Verkaufspreis skaliert logarithmisch mit Workshop-Level (kein Soft-Cap).

---

## Mini-Games (10, SkiaSharp)

Alle erben `BaseMiniGameViewModel`, dedizierte Renderer (Header/Result/Countdown/Buttons bleiben XAML).
Direktstart ohne Start-Button. Rating: Perfect=100% / Good=75% / Ok=50% / Miss=0%. Auto-Complete:
Timing-Spiele ab 30 Perfects (Premium 15), Puzzle/Memory ab 20 (Premium 10).

| MiniGame | Renderer | Besonderheit |
|----------|----------|-------------|
| Sawing | SawingGameRenderer | Bezier-Maserung, Sägemehl-Partikel, 4 Sub-Typen |
| Pipe Puzzle | PipePuzzleRenderer | BFS Wasser-Durchfluss |
| Wiring | WiringGameRenderer | SKPathMeasure-Pulse, Sicherungskasten |
| Painting | PaintingGameRenderer | Combo-Badge (Fire-Icon ab Combo ≥3) |
| Blueprint | BlueprintGameRenderer | Blaupausen-Grid, Memorisierungs-Scan |
| RoofTiling | RoofTilingRenderer | 3D-Ziegel, Platzierungs-Funken |
| DesignPuzzle | DesignPuzzleRenderer | Architektenplan |
| Inspection | InspectionGameRenderer | 16 Vektor-Icons (8 gut/8 defekt), pulsierende Lupe |
| ForgeGame | ForgeGameRenderer | Amboss, Temperatur-Zonen, Hammer |
| InventGame | InventGameRenderer | Circuit-Pulse |

Renderer-Caching + IDisposable-Klassifizierung → [Shared/Graphics](HandwerkerImperium.Shared/Graphics/CLAUDE.md).

---

## Monetarisierung

### Premium "Imperium-Pass" (4,99 EUR Lifetime)

+50% Einkommen (`IncomeCalculatorService.CalculateGrossIncome` + `CalculateCraftingSellMultiplier`),
+100% Goldschrauben (Gameplay-Quellen, NICHT IAP), +50% Offline-Einkommen, keine Werbung,
Auto-ClaimDaily, ×2 Rewarded-Belohnungen, Markt-Heatmap, Auto-Verkaufs-Regeln, +1 Erbstück-Slot (3→4),
2× Lucky-Spin/Tag, MiniGame-Auto-Complete früher. Shop zeigt `PremiumIncomeComparison` (Einkommen
ohne/mit Premium — psychologisch stärkster Kaufgrund). Bestehende `IsPremium`-Spieler bekommen den Pass automatisch.

### Rewarded Ads (13 Placements)

golden_screws (10 GS, 4h-Cooldown), shop_reward (3h), score_double, market_refresh, workshop_speedup
(2h Brutto), workshop_unlock (30% Rabatt), worker_hire_bonus (+1 Slot, max 3/WS), research_speedup
(−50% Restzeit, ab 30min), daily_challenge_retry, achievement_boost (+20%, ab TargetValue>5),
offline_double, rush_boost (1h), lucky_spin (1x/Tag nach Gratis-Spin).

### Goldschrauben & Whale-IAP

GS-Quellen: Mini-Games (3–10), Daily Challenges (~12), Achievements (5–50), Rewarded (10),
IAP (50/150/450), Daily Login (1–25), Meilensteine. `AddGoldenScrews(amount, fromPurchase)`
verdoppelt bei Premium nur Gameplay-Quellen (IAP-Käufe NICHT). Whale-Bundles über das 4,99-Ceiling:
`bundle_mid` (9,99 €), `bundle_big` (19,99 €), `bundle_mega` (49,99 €, inkl. Premium) — VIP-Tracking
via `RecordVipPurchase()`.

Ad-/Billing-Plattform-Details → [Premium-Library](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).

---

## Gilden-Multiplayer (Firebase Realtime Database)

**PlayerId** (GUID) ist die stabile Spieler-Identität — überlebt Firebase-Account- und Geräte-Wechsel.
Alle DB-Pfade nutzen `PlayerId`, NICHT `Uid`. Identitäts-Auflösung: Preferences `player_id` →
`GameState.PlayerGuid` → neue GUID. `/auth_to_player/{uid}` → PlayerId-Mapping nach jedem Token-Refresh
(Security Rules autorisieren darüber).

Service-Inventar (CRUD, Forschung, Krieg, Boss, Hall, Co-op-Aufträge, Auktionen, Mega-Projekte) +
Cross-Client-Konsistenz-Patterns (geteilter HMAC-Salt, atomares `IncrementAsync`, Master-Client-Pattern,
PATCH-statt-PUT) → [Shared/Services](HandwerkerImperium.Shared/Services/CLAUDE.md).

**Mega-Projekte**: wochenlange Material-Spenden-Pipeline (Cathedral / Headquarters) mit permanenten
Gildenboni (Crafting-Speed, Auto-Verkaufs-Preis, Lager-Slots) für alle Mitglieder. Single-active je Gilde
(`guilds/{guildId}/megaProjects/active`), Sunset nach 30 Tagen, `ClaimedGuildProjectIds` gegen Doppel-Belohnung.
UI: `GuildBuildSiteView` (Route `guild_build_site`, erreichbar über Combat-Tab).

### Firebase-Security-Rules-Patterns

Alle Pfade müssen in `database.rules.json` (Repo-Root) stehen — fehlender Eintrag → Firebase liefert
`null` ohne Error (kein Exception-Log).

```json
"progress":  { ".validate": "newData.isNumber() && newData.val() >= data.val()" },
"completed": { ".validate": "!data.exists() || (data.val() == false && newData.val() == true)" }
```

Server-Timestamp gegen Manipulation: C#-Sentinel `{ ".sv": "timestamp" }`, Firebase löst serverseitig auf.
Rate-Limit auf Rules-Ebene: `"(now - data.child('updatedMs').val()) >= 60000"`. `orderBy`-Queries
brauchen `.indexOn` für das Feld; `|| !data.exists()` zur Write-Rule für Neuanlage.

Deployment: `npx firebase-tools deploy --only database --project handwerkerimperium-487917`.

---

## Live-Ops, Retention & FTUE

| System | Service | Kern |
|--------|---------|------|
| Live-Events | `LiveEventService` | RemoteConfig-getrieben, 4 Templates (DoubleReward/BossRush/CoopMarathon/MiniGameMastery), 3-Tier-Reward (25/75/200 GS). Game-Code hängt `AddScore` ein |
| Push-Notifications | `AndroidNotificationService` | 8 Trigger (ResearchComplete, DeliveryReminder, RushAvailable, DailyReward, WorkerMoodCritical, OfflineEarningsCapped, BattlePassExpiring, LiveOrderAvailable). Meister-Hans-Persona-Präfix |
| Referral | `ReferralService` | 6-stelliger Code, 3-Tier-Reward (50/200/500 GS), +5% Income ab Tier 10, server-seitiges Anti-Cheat |
| BattlePass | `BattlePassService` | 30-Tier-Saison (30 Tage), Free/Premium-Track |
| Cross-Promotion | `CrossPromoService` | House-Ads, Tagesrotation `DayOfYear % AppCount`, eigene App gefiltert. UI: `CrossPromoCard` in SettingsView |
| FTUE | `FtueService` | 10-Step State-Machine, persistiert in `GameState.Tutorial.Ftue`, Telemetrie-Hooks. `FtueProgressTracker` verdrahtet Fortschritt mit Game-Events |
| What's-New | `WhatsNewService` | Versionierter Feature-Dialog (`s_releases`-Array, RESX-Keys), `lastSeen` vor Render (Crash-Sicherheit) |

**What's-New Release-Workflow (Pflicht):** `s_releases` enthält genau EINEN offenen Eintrag für die
nächste Version. Bei JEDER funktionalen Änderung kumulativ erweitern: neuen RESX-Key (6 Sprachen) anlegen
und ins `FeatureKeys`-Array des offenen Eintrags einfügen. Beim Release-Trigger: Eintrag finalisieren
(bleibt im Array), neuen leeren Eintrag für die Folge-Version anhängen. So sehen Spieler beim Update
immer die vollständige Liste seit ihrer installierten Version.

### Telemetrie

`IAnalyticsService.TrackEvent(name, props)` ist die einzige Schnittstelle. Services injizieren
`IAnalyticsService? analytics = null` (optional, damit Tests/DI ohne Mock laufen). Event-Namen +
Property-Keys leben in `Models/AnalyticsEvents.cs` (z.B. `material_crafted`, `material_sold`,
`warehouse_full_pause`, `material_market_trade`, `guild_mega_project_donation`, `heirloom_chosen`,
`order_accepted_with_material`).

---

## Audio (Cross-Platform)

- **Android**: `AndroidAudioService` (SoundPool für SFX, MediaPlayer für Music + Crossfade, AudioFocus-Listener).
- **Desktop**: `DesktopAudioService` (Windows NAudio+NAudio.Vorbis; Linux/macOS ffplay-Fallback),
  in `Program.cs` als `App.AudioServiceFactory` registriert.
- **Assets**: 82 SFX + 4 Music-Loops in `Assets/Sounds|Music/*.ogg`. Android linkt via `<AndroidAsset>`.
- `MusicTrack`-Enum: `IdleWorkshop`, `BossOrTournament`, `Celebration`. Crossfade default 800 ms.
- **Generator**: `tools/SoundForge/generate_audio.py` (Python + FFMPEG, algorithmische Synthese).

---

## SaveGame & State

`GameState.CurrentStateVersion = 7` (const) — Cloud-Save mit höherer Version triggert Alert statt Download.
Migration V1→V7 in `SaveGameService` (V7 kürzt überlaufende Stacks + zahlt BaseValue als Geld aus →
kein Wert-Verlust). Versions-Tabelle + Entity-Details → [Shared/Models](HandwerkerImperium.Shared/Models/CLAUDE.md).

**Daily-Challenge-Tracking**: `MiniGameResultRecorded`-Event auf `IGameStateService` →
`DailyChallengeService` subscribt (Score-Mapping Perfect=100%…Miss=0%).

---

## App-weite Gotchas

Bereichsspezifische Gotchas leben in der jeweiligen Unterordner-Doku (Service-Caches/Firebase →
Services · SKCanvasView/DPI → Graphics · Bindings/IsHitTestVisible → Views · sqlite/AssignedWorkshop →
Models). Hier nur das, was wirklich app-weit greift:

| Problem | Ursache | Lösung |
|---------|---------|--------|
| 5 Dialoge am ersten Start überfluten neue Spieler | Daily Reward→Story→Welcome→FirstWorkshop→AcceptOrder kaskadieren | Daily Reward Tag 1 still einsammeln; Welcome-Hint überspringen wenn Story Ch.1 gezeigt wurde |
| Multi-Task-Order: nur erster Task spielbar | MiniGame-View stoppt 30fps-Timer bei `IsResultShown`; bei gleicher MiniGame-Art bleibt `ActivePage` konstant → kein Neustart | `BaseMiniGameViewModel.GameRestarted`-Event in `SetOrderId()`; alle 10 Views rufen `StartRenderLoop()`; `ContinueCommand` mit Reentrancy-Guard |
| MainView-RenderTimer läuft bei App-Pause | Battery-Drain im Hintergrund | `MainViewModel.PauseStateChanged`-Event → MainView stoppt/startet alle Canvases |
| `async void` Event-Handler crashen Prozess | Timer-Ticks als `async void` ohne try/catch | `RunHandlerSafely` (Helpers/AsyncExtensions) bzw. `OnGameTimerTickAsync` + Wrapper |

---

## Build & Test

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared        # Kompilier-Check
dotnet run   --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop   # Entwicklung/Test
dotnet publish src/Apps/HandwerkerImperium/HandwerkerImperium.Android -c Release # Release-AAB
dotnet test  tests/HandwerkerImperium.Tests                               # Unit-/Headless-Tests
dotnet run   --project tools/AppChecker HandwerkerImperium                # AppChecker
```

Tests u.a.: SaveGameMigration, PerformanceBenchmark, PrestigeCinematicRenderer, DailyBundleService,
CjkFontResolver, HeadlessSmoke (Avalonia.Headless + XUnit), EternalMasteryService.
CI: `.github/workflows/ci.yml` (Build + Test + Firebase-Rules-Lint).

---

## Verweise

- Generische Conventions (MVVM, DateTime, DI, Naming, Build-Config) → [Haupt-CLAUDE.md](../../../CLAUDE.md)
- Firebase Security Rules: `database.rules.json` (Repo-Root)
- Balancing-Werte: Memory `balancing.md`
- Asset-/Icon-Generierung: `F:\AI\ComfyUI_workflows\handwerkerimperium\`
