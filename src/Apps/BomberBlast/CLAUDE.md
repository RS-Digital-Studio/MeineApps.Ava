# BomberBlast — Bomberman-Klon (SkiaSharp)

Vollständige 2D-Spiel-Engine im Bomberman-Stil. Landscape-only auf Android, Grid 15×10,
zwei Visual-Styles (Classic HD + Neon/Cyberpunk). SkiaSharp-Rendering, eigenes Icon-System,
A\*-AI, Roguelike-Dungeon, Liga-System, Battle-Pass, Cloud-Save.

| Aspekt | Wert |
|--------|------|
| Package-ID | `org.rsdigital.bomberblast` |
| Premium-Modell | 1,99 EUR `remove_ads` (Rewarded statt Banner) |
| App-Palette | Orange #FF6B35 (Neon-Arcade), Dark-only |
| Mandat | Code-only / CC0 / prozedural — keine externen Audio-/Art-Pipelines, keine Voice-Talents, kein Telemetrie-Backend |

Generische Build-Befehle, Conventions (MVVM, DI, Naming, DateTime, Localization, Navigation,
Icon-Strategie) und Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md).
**Das Detail-Wissen liegt in den granularen Unterordner-Dateien (Doku-Karte unten) — diese
App-Root-Datei beschreibt nur den Gesamtüberblick und app-weite Querschnitte.**

---

## Doku-Karte — wo das Detail liegt

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Factories, Loading | `App.axaml.cs`, 37 Services, 25 VMs, Crash-Recovery, Namespaces | [BomberBlast.Shared](BomberBlast.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Factories, Gamepad, Immersive, FCM, Firebase | [BomberBlast.Android](BomberBlast.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs` | [BomberBlast.Desktop](BomberBlast.Desktop/CLAUDE.md) |
| GameEngine, Modes, Combat, Audio, Replay/MP-Foundation | GameEngine (5 Partials), IGameMode-Plugin, ComboSystem, Determinismus, Audio-Bus | [Shared/Core](BomberBlast.Shared/Core/CLAUDE.md) |
| Render-Pipeline + atmosphärische Systeme | GameRenderer (10 Partials), Bloom, FoW, ScreenShake, Cinematic, SkiaSharp-Caching | [Shared/Graphics](BomberBlast.Shared/Graphics/CLAUDE.md) |
| AI & Pathfinding | A\*-Pathfinding, BFS Safe-Cell, Danger-Zone, Boss-AI | [Shared/AI](BomberBlast.Shared/AI/CLAUDE.md) |
| Input-System | InputManager, NeonJoystick, Keyboard, Gamepad, KonamiCode | [Shared/Input](BomberBlast.Shared/Input/CLAUDE.md) |
| Icon-System | 159 Neon-Arcade-Icons (160 Enum-Member inkl. None), PathIcon-Ableitung, Skia-Renderer | [Shared/Icons](BomberBlast.Shared/Icons/CLAUDE.md) |
| Services (37) | Economy, Firebase, Live-Ops, Logging, DialogPresenter, Anti-Cheat-Timer, PersistenceHealth | [Shared/Services](BomberBlast.Shared/Services/CLAUDE.md) |
| ViewModels (25) | Compositor, Feature-Module, ChildViewModelRegistry, LifecycleHub | [Shared/ViewModels](BomberBlast.Shared/ViewModels/CLAUDE.md) |
| Views (30 + Components) | AXAML-Views, Compiled Bindings, GameView-Subscription, Overlays | [Shared/Views](BomberBlast.Shared/Views/CLAUDE.md) |
| Navigation-Module | NavigationCoordinator, BottomTabController, NavigationRouteParser | [Shared/Navigation](BomberBlast.Shared/Navigation/CLAUDE.md) |
| Domain-Modelle | Entities, Grid, Levels, Dungeon, Cards, BattlePass, Cosmetics, CloudSave, SchemaMigrator | [Shared/Models](BomberBlast.Shared/Models/CLAUDE.md) |
| Startup-Pipeline | `BomberBlastLoadingPipeline`, `LoadingTips` | [Shared/Loading](BomberBlast.Shared/Loading/CLAUDE.md) |
| Controls | GameButtonCanvas, AchievementIcon, MenuBackground | [Shared/Controls](BomberBlast.Shared/Controls/CLAUDE.md) |
| Converter | ActiveViewEquals, BoolToOpacity, StringToGameIconKind | [Shared/Converters](BomberBlast.Shared/Converters/CLAUDE.md) |
| DI-Extensions | `LazyServiceExtensions` (Zirkel-Auflösung) | [Shared/Extensions](BomberBlast.Shared/Extensions/CLAUDE.md) |

Die Doku ist eine Pyramide: Diese Datei → Projekt-Roots (`BomberBlast.Shared/CLAUDE.md` u.a.)
→ Ordner-Dateien. Domänen-Details/Gotchas eines Bereichs stehen **dort**, nicht hier.

---

## Gameplay-Überblick (Balancing-Werte → Memory `balancing.md`)

App-weite Mechanik-Landkarte. Konkrete Implementierung jeweils in der verlinkten Bereichs-Doku.

| System | Kern | Doku |
|--------|------|------|
| Welten/Level | 10 Welten × 10 Level (L1-L100), jedes 10. Level Boss. Layout-Rotation + Mutatoren ab Welt 6 | Models, Core |
| PowerUps | 12 Typen, Level-basierte Freischaltung, Skull/Curse + Cure | Models (`PowerUpType`) |
| Gegner | 12 Typen (8 Basis + Tanker/Ghost/Splitter/Mimic), Elite-Modifier (1.2× Speed, 2× HP) | Models, AI |
| Bosse | 5 Typen, Multi-Cell, Enrage bei 50%, Duo-Encounter L90/L100, `BossModifier`-Enum | Models, AI, Graphics |
| Spielmodi | Story, Master (Reborn nach L100), QuickPlay, Survival, Dungeon, BossRush, DailyChallenge, DailyRace — alle via `IGameMode` | Core (`Modes/`) |
| Dungeon | Roguelike: Floor 1-10+, 16 Buffs, 5 Synergien, Node-Map 10×3, eigener Persistenz-Pfad | Core, Models, Services |
| Karten/Deck | 14 Bomben-Typen (3 Shop + 10 Karten + Default), 4+1 Slots, Crafting, Drop-Gewichtung | Services (`ICardService`), Models |
| Combo | Kill-Fenster 2s, ×2-×10+ Score-Bonus, Slow-Mo bei MEGA/ULTRA | Core (`Combat/ComboSystem`) |
| Economy | Coins (Level-Score-basiert) + Gems (nur Gameplay), 12 Shop-Upgrades (9 Stat + 3 Bomb-Unlocks), Overflow-Guard | Services (`ICoinService`/`IGemService`/`IShopService`) |
| Liga | 5 Tiers (Bronze-Diamant) + Sub-Tiers, 14-Tage-Saisons, Firebase + NPC-Backfill, Daily-Race | Services, Models (`League/`) |
| Live-Ops | Battle-Pass (30 Tier, Theme-Rotation), Event-Calendar, RotatingDeals, LuckySpin (Pity 25), Weekly-Content | Services |
| Cosmetics | 98 Definitionen: 32 Trails + 33 Frames + 33 Victories | Models (`Cosmetics/`), Graphics |
| Heroes | 5 spielbare Heroes mit Stat-Multiplikatoren (Engine-Integration deferred) | Models, Services (`IHeroService`) |

**Game-Juice** (Neon-Arcade-Stil): Iris-Wipe, Slow-Motion, Hit-Pause, Squash/Stretch,
Trauma-ScreenShake, UltraComboFlash, Cinematic-Sequencer, Konfetti, saisonale Partikel,
First-Win-Cinematic. Implementierung → Graphics + Core. Bei `ReducedEffects`/niedrigem
Hardware-Tier werden Flash/Bloom/Partikel gedrosselt (Photosensitivity + Performance).

---

## App-weite Querschnitte

Diese Patterns betreffen mehrere Bereiche gleichzeitig und sind daher hier verankert
(Detail-Implementierung in der jeweiligen Bereichs-Doku).

### Hardware-Tier + Performance-Adaption

`IHardwareProfileService` mit `HardwareTier`-Enum (Low/Medium/High/Ultra), Auto-Detection
(ProcessorCount + GC-Heuristik), User-Override, Battery-Save-Toggle (Tier −1), Thermal-Hook,
`OnMemoryTrimRequested` (Level ≥ 40 → Tier −1 + Bloom aus).

| Tier | ParticleSystem-Cap | Bloom | AudioSpatial-Reverb |
|------|-------------------|-------|---------------------|
| Low | 300 | aus | aus |
| Medium | 800 | aus | aus |
| High | 1200 | aus | an |
| Ultra | 1500 | an (außer Battery/Thermal) | an |

Orthogonal dazu: **adaptives Frame-Skipping** der atmosphärischen Systeme (Render-Pipeline,
siehe Graphics-Doku). Tier-Service → [Services](BomberBlast.Shared/Services/CLAUDE.md).

### Anti-Cheat-Hybridtimer (Date-Manipulation-Schutz)

`RewardedAdCooldownTracker`-Strategie (monotoner `Environment.TickCount64` **plus** persistierte
`DateTime.UtcNow`, OR-verknüpft) gegen "Datum vorstellen → claim → zurückstellen → re-claim".
Genutzt von `CoinService` (Daily-Bonus 20 h), `RetentionService` (Comeback ~3 Tage),
`BattlePassService` (XP-Boost 24 h). Bei Process-Reboot (TickCount64 springt zurück) zählt das
UTC-Datum allein — Reboot ist legitime neue Session. Detail → Services-Doku.

### Cloud-Save + Schema-Migration

Local-First, 35 Persistenz-Keys, Pull bei Start, Push-Debounce 5 s. Schema-Migration liegt in
`CloudSaveSchemaMigrator` (migriert das gesamte `CloudSaveData`, `CurrentSchemaVersion = 3`),
läuft VOR `ApplyCloudData`. `PersistenceHealth` (Static) sammelt Parse-Corruption →
`CloudSaveService` erzwingt Pull statt Push bei `WasCorruptionDetected` (Data-Loss-Schutz).
Konflikt-Resolution: TotalStars → Wealth → Cards → Keys.Count → Timestamp → Cloud-Default.
Detail (Keys, `ChooseBest`, `BuildCloudSaveData`) → Models + Services.

### DSGVO-Compliance

- **Art. 17** (`IAccountDeletionService`): Cascade Local → Firebase-Liga → CloudSave, Best-Effort
  bei Netzfehler, lokale Daten werden IMMER gelöscht.
- **Art. 20** (`IDataExportService`): JSON + Human-Readable-Export aller Spieler-Daten.
- Consent-Toggles über `IPrivacyCenter`/`IAccessibilityService` (AnalyticsConsent persistiert,
  kein aktives Backend — Toggle wird bei künftigem Provider ausgewertet).

### Firebase-Stack

Aktiv: Cloud Messaging (FCM) + Remote Config + Realtime Database (Liga/Daily-Race/Clans/CloudSave).
**Crashlytics + Analytics ausgebaut** — keine Crash-/Funnel-Telemetrie (FirebaseInitProvider
crasht ohne Crashlytics-Gradle-Plugin; Single-App-Nutzung rechtfertigt den Analytics-Aufwand
nicht). REST-Sicherheit (Bearer-Header, `ServerValue.TIMESTAMP`, Rate-Limits) und die konkreten
Services → Services + Android-Doku. Security-Rules → `database.rules.bomberblast.json` (Verweise unten).

### Crash-Recovery + Splash-Version

`OnFrameworkInitializationCompleted` inkrementiert den Crash-Counter VOR der Init;
`RunLoadingAsync` setzt ihn nach Pipeline-Erfolg auf 0. Der Counter liegt in einer eigenen
Mini-Datei (`CrashCounter` → `crashcount.txt` im App-Daten-Ordner), NICHT in den Preferences —
so wird vor dem DI-Build kein voller `PreferencesService`-Load (komplette `preferences.json`)
nur für einen int erzwungen. Bei ≥ 3 Crashes: Safe-Mode (optionale Services per Try/Catch
übersprungen, App startet garantiert). `App.ResetCrashRecoveryCounter()` für Settings.
Optionale Services (Push/RemoteConfig) werden erst NACH dem ersten Frame in `RunLoadingAsync`
(`InitializeOptionalServices`, off-UI-Thread via `Task.Run`) initialisiert — nicht mehr vor dem
ersten Frame im Startup-kritischen Pfad. Splash-Version aus
`typeof(App).Assembly.GetName().Version` — Source-of-Truth ist
`<Version>` in `BomberBlast.Shared.csproj`, muss mit `ApplicationDisplayVersion` in
`BomberBlast.Android.csproj` synchron bleiben. Detail → Shared-Composition-Root-Doku.

---

## Icon-Strategie (Abweichung von der Haupt-Convention)

BomberBlast nutzt **kein** Material.Icons, sondern ein eigenes Neon-Arcade `GameIcon`-System
(159 Icons). Das ist als app-spezifische Ausnahme in der Haupt-CLAUDE.md (Icon-Strategie,
Punkt 3) zugelassen. **AppChecker-False-Positive**: der Material.Icons-Check schlägt für
BomberBlast immer an — bewusste Konvention, kein Bug. Detail → [Icons-Doku](BomberBlast.Shared/Icons/CLAUDE.md).

---

## What's-New-Release-Workflow (Pflicht)

`WhatsNewService.BuildReleases()` enthält EINEN offenen Eintrag für die aktuell in Entwicklung
befindliche nächste Version. Bei JEDER funktionalen Änderung zwischen zwei Releases einen
weiteren Bullet ergänzen (kumulativ, nicht selektiv):

1. Zwischen Releases: pro Feature/Fix einen Bullet (Lokalisierungs-Key + Default-Text) zum
   offenen Eintrag hinzufügen.
2. Beim Release-Trigger: aktuellen Eintrag finalisieren (bleibt in `BuildReleases()`), danach
   neuen leeren Eintrag für die Folge-Version anhängen.
3. Nach dem Release: neuen Eintrag sukzessive füllen bis zum nächsten Release.

**RESX-Pflicht**: Jeder referenzierte Key (Titel + Bullets) MUSS in allen 6 RESX existieren.
`BuildReleases()` nutzt den `L(key, default)`-Helper — `GetString` liefert bei Miss den
Key-NAMEN (nie null), `?? default` wäre toter Code. `GetEntries()` filtert leere-Bullet-Einträge.
Detail → Services-Doku (`IWhatsNewService`).

---

## Build & Test

```bash
dotnet build  src/Apps/BomberBlast/BomberBlast.Shared      # net10.0, häufigster Check
dotnet run    --project src/Apps/BomberBlast/BomberBlast.Desktop
dotnet build  src/Apps/BomberBlast/BomberBlast.Android     # net10.0-android
dotnet publish src/Apps/BomberBlast/BomberBlast.Android -c Release   # Release-AAB (nur auf Anfrage)

dotnet run    --project tools/AppChecker BomberBlast
dotnet test   tests/BomberBlast.Tests/

# Firebase RTDB-Rules deployen (Repo-Root, Projekt bomberblast-league):
npx firebase deploy --only database --project bomberblast-league --config firebase.bomberblast.json
```

**Build-Hygiene**: 0 Warnungen in Shared + Android. SkiaSharp 3.x Text-Rendering nutzt gepoolte
`SKFont` + `canvas.DrawText(text, x, y, SKTextAlign, SKFont, SKPaint)` — kein deprecated
`SKPaint.TextSize/TextAlign/FakeBoldText`.

---

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md): Build-System, Conventions, Architektur, Keystore, DI-Pattern.
- Ad-/Banner-/IAP-Patterns → [MeineApps.Core.Premium.Ava/CLAUDE.md](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).
- SkiaSharp-Grundlagen/Gotchas (DPI, MaskFilter-Leak, Render-Loop) → [MeineApps.UI/CLAUDE.md](../../UI/MeineApps.UI/CLAUDE.md).
- Avalonia-/MVVM-Framework-Fallstricke (Value-Precedence, verschachtelte `x:DataType`) → [MeineApps.Core.Ava/CLAUDE.md](../../Libraries/MeineApps.Core.Ava/CLAUDE.md).
- `database.rules.bomberblast.json` + `firebase.bomberblast.json` (Repo-Root): Firebase-RTDB-Security-Rules
  (Liga + Daily-Race + Reports + Clans). Firebase-CLI verlangt `firebase.json` + Rules-Datei im selben
  Verzeichnis → beide im Repo-Root. Rules **kommentarfrei** halten (`"//"` wird als Pfad interpretiert,
  Deploy bricht ab); `daily_race`-Rule muss VOR der `$tier`-Wildcard stehen.
- Datierte Erkenntnisse / Balancing-Werte / Build-Historie → Memory (`balancing.md`,
  `gotchas.md`, `lessons-learned.md`), Git, `Releases/BomberBlast/CHANGELOG`.
