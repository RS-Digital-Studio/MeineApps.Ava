# BomberBlast — AAA-Quality-Audit (Solo-Dev / Code-Only)

**Version analysiert:** v2.0.57 (Produktion)
**Datum:** 2026-05-14
**Scope:** Ausschließlich Verbesserungen, die als Solo-Dev ohne externes Budget und ohne Server-Infrastruktur umsetzbar sind. Kein Composer, kein Artist, kein Voice-Actor, kein Pi-Server, kein Live-PvP.
**Verifikation:** Jede Behauptung wurde gegen den Stand v2.0.57 abgeglichen. Bereits umgesetzte Punkte sind im Abschnitt „Erledigt in v2.0.56–v2.0.57" aufgeführt und nicht mehr Teil der offenen Punkte.

---

## Erledigt in v2.0.56–v2.0.57 (nicht mehr zu tun)

Diese Punkte aus früheren Audit-Fassungen sind in v2.0.56 / v2.0.57 vollständig oder im Wesentlichen umgesetzt — nur als Referenz, damit Claude Code sie nicht erneut anfasst:

| Bereich | Status | Beleg |
|---------|--------|-------|
| Welt-Themed Bomb-FX | erledigt v2.0.56 | `GameRenderer` + `_gameStyleService.GetWorldTheme()` |
| ULTRA-Combo Vollbild-Vignette-Flash | erledigt v2.0.56 | `UltraComboFlash.cs`, Trigger ab `ComboSystem.ULTRA_THRESHOLD=10` |
| Player i-Frame + Damage-Flash | erledigt v2.0.56 | `Player.IsInvincible`, `InvincibilityTimer`, `HasSpawnProtection` |
| Anticipation-Frames Bomb-Place + Boss-Attack | erledigt v2.0.56 | `Player.TriggerBombPlaceAnticipation()`, `_bombSquashTimer` 80ms |
| Feature-Unlock-Choreographie | erledigt v2.0.56 | `FeatureUnlockChoreographer.cs` mit Analytics-Call-Site |
| „What's New"-Service + Version-Auto-Lookup | erledigt v2.0.56 | `WhatsNewService.cs`, `Assembly.GetName().Version` |
| Splash-Crash-Recovery | erledigt v2.0.56 | `App.axaml.cs` mit Crash-Counter + Safe-Mode-Switch bei ≥ 3 Crashes |
| Adaptive-Icon-Layers + Monochrome-Icon | erledigt v2.0.56 | `BomberBlast.Android/Resources/mipmap-anydpi-v26/appicon.xml` |
| IGameEventBus (Pub/Sub-Fundament) | erledigt v2.0.56 | `IGameEventBus.cs` + `GameEventBus.cs` |
| Mode-Bool-Flags entkoppeln | erledigt v2.0.56 | `_isStoryMode` etc. sind Computed Properties auf `_currentMode` |
| IAnalyticsService Event-Definitionen | erledigt v2.0.56 | 40+ Event-Konstanten, Call-Sites in GameEngine/MainMenu/Choreographer |
| Tutorial-Engine (Foundation) | erledigt v2.0.56 | `TutorialService.cs` mit 6 Schritten in 3 Phasen |
| D1-Daily-Reminder-Push | erledigt v2.0.56 | WhatsNew-Bullet „Daily reminder notifications" |
| Boss-Modifier-Enum + RollForWorld | erledigt v2.0.56 | `BossModifier.cs` mit 8 Modifiern, Spawn-Roll je Welt |
| CinematicSequencer | erledigt v2.0.56 | `BomberBlast.Shared/Graphics/CinematicSequencer.cs` |
| FixedTimestep-/Replay-/RNG-Foundation | erledigt v2.0.56 | `Core/FixedTimestepRunner.cs`, `Core/DeterministicRandom.cs`, `Core/ReplayCapture.cs` |
| **Boss-Modifier-Effekte alle 8** | **erledigt v2.0.57** | `BossEnemy.ApplyModifierEffects` (Healing/Summoner/Shielded/Burning), `MoveBoss` (Fast), Update (Berserk/Frenzy), `GameEngine.Collision` (Reflective 30%-Chance, Burning-Trail-Damage am Spieler) |
| **Elite-Enemy-Varianten** | **erledigt v2.0.57** | `Enemy.IsElite` ctor-Param, 1.2x Speed / 2x HP / 3x Points multiplikativ; `LevelGenerator` 8% Roll ab Welt 3 |
| **Hero/Character-Stats** | **erledigt v2.0.57** | `GameEngine.ApplyHeroStats()` aus 5 Mode-Starts (Story/Survival/Dungeon/BossRush/QuickPlay). Player.MaxBombs/FireRange/SpeedLevel/Lives vom aktiven Hero |
| **Summoner-Minion-Spawn** | **erledigt v2.0.57** | `GameEngine.Level.SpawnSummonerMinion`, `boss.TryConsumeSummonRequest` jeden Frame |
| **Outline-Pass im Renderer** | **erledigt v2.0.57** | `OutlineRenderHelper.RenderWithOutline`. Enemy + Player setzen `RenderOutline = true` im ctor — Boss erbt von Enemy |
| **Cosmetic-Volumen 98 Items** | **erledigt v2.0.57** | 32 Trails + 33 Frames + 33 Victories über Welt-Themes + BP-Saisons |
| Re-Engagement D1/D3/D7 | erledigt v2.0.57 | `IReEngagementScheduler.ScheduleAll/CancelAll` via MainActivity-OnPause/Resume |
| AppLogger NonFatal-Sink | erledigt v2.0.57 | `AppLogger.LogError(msg, ex)` → `ITelemetryService.LogNonFatal` |

---

## Kern-Befund (Stand v2.0.57)

Die Codebase ist auf einem Indie-Level, das viele bezahlte Studios nicht erreichen. Engine-Architektur, Performance-Optimierung und Game-Feel sind bereits Pro-Niveau. v2.0.57 hat die Content-Verkabelung (Boss-Modifier-Effekte, Elite-Enemies, Hero-Stats, Outline-Pass) komplettiert und die Re-Engagement-Push-Trigger D3/D7 aktiviert. Was zu AAA fehlt, ist primär **Live-Ops-Reife (Firebase Remote Config), UX-Konsolidierung (Tab-Konsolidierung, Tutorial-Splits), Architektur-Hygiene (MainViewModel-Reduktion, ILogger-Migration) und Engine-Reife (FixedTimestep-Integration, Mode-Hooks-Verkabelung)** — alles Felder, die ohne externe Hilfe weiter ausbaubar sind.

**Realistische Decke mit Code-only-Ansatz: ~7.5–8/10.** Eine Stufe über „Premium Indie".

---

## Bewertung pro Achse (Code-only-Sicht, v2.0.57)

| Achse | Status v2.0.57 | Potenzial (Code-only) | Hebel |
|-------|----------------|------------------------|-------|
| Engine-Architektur | 8.5/10 | 9.5/10 | NavigationHub + LazyVmRegistry, Mode-Hook-Verkabelung |
| Performance | 9/10 | 9.5/10 | FixedTimestep integrieren |
| Game Feel / Juice | 9/10 | 9.5/10 | Audio-Layering (Tempo-Pitch), Phase-2-Attack-Varianten |
| Audio (technisch) | 6.5/10 | 7.5/10 | EQ-Sidechain + Layered Stinger + LUFS-Mastering |
| Visuals (Engine) | 7/10 | 7.5/10 | Outline-Pass ist da — bleibt nur Konsistenz-Audit über alle Sprites |
| Content-Volumen | 6/10 | 7/10 | Mini-Bosse zwischen Welten + Phase-2-Pattern |
| Live-Ops | 7/10 | 9/10 | Firebase Remote Config + Funnel-Lücken + D3/D7 |
| UX/UI | 6/10 | 8.5/10 | Tab-Konsolidierung + 3 Tutorial-Levels |
| Architektur-Hygiene | 8/10 | 9/10 | God-VM auflösen, Logging modernisieren |

---

## Offene Punkte (Stand v2.0.57)

### Tier 1 — Live-Ops + UX-Konsolidierung

#### 1. Firebase Remote Config integrieren

**Status:** `IRemoteConfigService` + `DefaultsRemoteConfigService` aus embedded JSON aktiv. `Xamarin.Firebase.Config` fehlt in `Directory.Packages.props`. `FirebaseRemoteConfigService` als Android-Override fehlt.

**Lösung:**
- `Xamarin.Firebase.Config` Paket in `Directory.Packages.props` ergänzen
- `AndroidFirebaseRemoteConfigService.cs` in Premium-Library `Android/`-Ordner via Linked-File-Pattern
- App.axaml.cs `RemoteConfigServiceFactory` mit MainActivity-Setup
- `FetchAndActivateAsync` mit Cache 1h Production / 5min Debug
- Override-Pattern: Firebase überschreibt Default-Werte aus JSON via `SetOverride(key, value)`

**Impact:** Schaltet alle weiteren Live-Ops-Verbesserungen frei (z.B. Boss-Modifier-Chance je Welt ohne Update tunen, Drop-Rate-Adjustments).

---

#### 2. Funnel-Lücken-Audit der Analytics-Calls

**Status:** 40+ Event-Konstanten in `IAnalyticsService` definiert, Call-Sites in `GameEngine.Level.cs`, `GameEngine.Collision.cs`, `MainMenuViewModel.cs`, `FeatureUnlockChoreographer.cs`, `RewardedAdAnalyticsExtensions.cs`. Vollständigkeit aber nicht durchgängig verifiziert.

**Lösung:** Pro Event prüfen, ob er an allen Game-States feuert:
- `LevelStart` / `LevelComplete` / `LevelFailed`: in jedem Mode (Story, Survival, Dungeon, BossRush, QuickPlay, Daily, Master) verdrahtet?
- `BossEncounter` / `BossDefeated`: feuert auch bei Mini-Bossen und Boss-Phase-2?
- `PurchaseFlowStart/Success/Cancel/Fail`: alle Branches in `AndroidPurchaseService` abgedeckt?
- `RewardedAdRequest` / `RewardedAdCompleted`: für jede der 28 Placement-IDs?
- `TutorialStepComplete`: für alle 6 Schritte (später für T1/T2/T3 aus #5)
- `ComboTierReached`: feuert genau einmal pro Combo-Stufe (kein Spam pro Frame)?

**Impact:** Datenbasis für Balancing und Conversion-Analyse wird belastbar.

---

#### 3. Tutorial auf 3 separate Tutorial-Levels

**Status:** `TutorialService` mit 6 Steps in 3 Phasen (Movement → Bombs → PowerUps) auf Level 1. `TutorialPhase`-Enum + `PhaseChanged`-Event vorhanden.

**Problem:** 6 Schritte in einem Level überfordern Genre-Neulinge.

**Lösung:** 3 geschützte Tutorial-Levels — die 3 Phasen werden zu eigenständigen Mini-Leveln:
- **T1: Movement** — Nur DPad, keine Bomben. Sammle 5 Coins. Erkläre Tab-Bar.
- **T2: Bomb-Mechanik** — Bombe legen, Timing, Explosion-Reichweite. Zerstöre 3 Bricks, töte 1 Enemy.
- **T3: Power-Ups** — Pick-up von Fire+1, Speed+1, Bomb+1. Boss-Lite-Encounter mit telegraphed Attack.

Persistenz: `tutorial_t1_done`, `tutorial_t2_done`, `tutorial_t3_done` Preferences. Nach T3 erster Story-Level. `TutorialStepComplete`-Event pro Schritt prüfen.

**Impact:** D0–D1-Conversion für Genre-Neulinge spürbar besser.

---

#### 4. Bottom-Tab-Konsolidierung 19 → 4

**Status:** `ActiveView`-Enum hat 19 Werte. `IBottomTabHub` Foundation existiert (`IBottomTabHub.cs`), aber MainMenuView (974 LOC) wurde noch nicht refaktoriert.

**Lösung:** 4 sichtbare Bottom-Tabs:
- **Home** — Story-Progression + Daily-Dashboard mit Karten (Daily-Challenge, Daily-Race, Lucky-Spin)
- **Spielen** — Quick / Survival / Dungeon / Boss-Rush / Master als Liste mit Hero-Image
- **Shop** — Coin-Shop / Gem-Shop / Battle-Pass als horizontale Sub-Tabs
- **Profil** — Achievements / Collection / League / Statistics / Settings als ListView

`ActiveView`-Enum bleibt für die interne Navigation, nur die sichtbare Tab-Bar wird reduziert.

**Impact:** Spielbarkeit für Neulinge signifikant besser, Conversion-Rate steigt.

---

### Tier 2 — Architektur-Hygiene + Engine-Reife

#### 5. Microsoft.Extensions.Logging + Crashlytics-Sink

**Status:** `AppLogger.LogError(msg, ex)` leitet bereits an `ITelemetryService.LogNonFatal(ex, ctx)` weiter, `BeginScope` mit AsyncLocal-Stack vorhanden. Vollmigration der 53 Services auf `ILogger<T>` per DI fehlt aber.

**Lösung:**
- `ILogger<T>` überall per DI injizieren
- `LoggerFactory.Create` mit:
  - Android: `Crashlytics`-Sink (Custom-Sink, ~50 LOC)
  - Desktop: Console + RollingFile (`Microsoft.Extensions.Logging.File`)
- LogLevels: Trace/Debug nur Debug-Build, Info+ in Production
- `AppLogger` bleibt als Fassade über `ILogger`, damit existierende Call-Sites schrittweise migrieren

**Impact:** Crash-Reports mit Kontext, Bug-Reproduktion deutlich schneller.

---

#### 6. MainViewModel reduzieren (1302 → ~600 LOC)

**Status:** `MainViewModel.cs` 1302 LOC. `GameEventBus` existiert. Onboarding + Dashboard sind Partial-Class-Splits, aber nicht als eigene VMs extrahiert. `NavigationHub` und `LazyVmRegistry` fehlen.

**Lösung:**
- `NavigationHub` (eigene Klasse, nur Routing-Logik + `ActiveView`-Enum-Übergänge)
- `LazyVmRegistry` (verwaltet `EnsureXxxVm()`-Methoden, public Property-Bag)
- `DashboardViewModel` + `OnboardingViewModel` als echte Klassen aus den Partial-Splits extrahieren
- `MainViewModel` bleibt nur als Komposit + INavigable + Constructor-Injection für NavigationHub/EventBus/LazyVmRegistry

**Impact:** Weniger Bug-Risiko bei Änderungen, neue Features ohne God-VM-Anfassen.

---

#### 7. GameEngine Mode-State-Properties verlagern

**Status:** Mode-Bool-Flags sind bereits Computed Properties auf `_currentMode`. DungeonMode-Aliasse via `DungeonModeState`-Property delegieren. **Restschuld:** `IGameMode.UpdateLogic` und `OnLevelComplete`-Hooks werden NICHT aus dem Engine-Loop gerufen — Bool-Flag-Branches sind live.

**Lösung:**
- `_currentMode?.UpdateLogic(deltaTime, ctx)` in `GameEngine.Update`
- `_currentMode?.OnLevelComplete(ctx)` in `GameEngine.CompleteLevel`
- `_currentMode?.OnGameOver(ctx)` in `GameEngine.GameOver`
- Jeder Mode bekommt eigene State-Felder (analog DungeonMode-Pattern)
- GameEngine-Branches `if (_isStoryMode) ...` durch `_currentMode.OnXxx()`-Dispatch ablösen

**Impact:** Neue Modi ohne Engine-Touch, technische Schuld weg.

---

#### 8. FixedTimestep + DeterministicRandom in den Haupt-Tick integrieren

**Status:** `FixedTimestepRunner`, `DeterministicRandom`, `ReplayCapture` als Klassen vorhanden. `IRngProvider` DI-registriert mit `DeterministicRngProvider`. **GameEngine.Update nutzt sie noch nicht für den Haupt-Tick.**

**Lösung:**
- `GameEngine.Update` mit Accumulator-Pattern: `while (accumulator >= FixedDt) { Tick(FixedDt); accumulator -= FixedDt; }`
- Render-Interpolation: `float alpha = accumulator / FixedDt;` für Sprite-Position-Smoothing
- Alle engine-internen `Random.Shared` durch `_rng.Next` ersetzen (LevelGenerator/EnemyAI haben noch viele Direct-Calls)
- Replay-Capture: 1 Byte pro Tick (Input-Bitmask) + Seed → vollständig deterministisch reproduzierbar
- Replay-Validation als Test: 100 zufällige Replays werden 100 × abgespielt und müssen identisches Ergebnis liefern

**Impact:** Replay-System (Best-of-Day-Replays als Spielanreiz), Anti-Cheat-Validierung für League.

---

### Tier 3 — Content + Erweiterungen

#### 9. Boss-Phase-2 Attack-Pattern-Variation

**Status:** `BossEnemy.CurrentPhase = 2` wird beim Enrage gesetzt. Aber Attack-Patterns sind identisch zu  (keine Variation in Telegraph-Farbe, AttackTargetCells-Muster).

**Lösung pro Boss-Typ:**
- StoneGolem:  wirft 2 Blöcke statt 1
- IceDragon:  friert 2 Reihen (eine vor, eine nach Spieler-Position)
- FireDemon:  Lava auf 3/4 statt halben Boden
- ShadowMaster:  Teleport + 2 Schattenklone statt 1
- FinalBoss:  rotiert alle 4 Angriffe in halber Zeit

**Impact:** Bosse fühlen sich nach Enrage wirklich anders an, nicht nur schneller.

---

#### 10. Mini-Bosse zwischen Welten (L5/L15/L25/...)

**Status:** Aktuell Boss nur alle 10 Level (L10, L20, ..., L100). Dazwischen Standard-Level.

**Lösung:**
- Mid-World Mini-Boss bei L5, L15, ..., L95
- Reskinned bestehende BossEnemy mit 50% HP + neuer Modifier-Roll
- Halbierte Boss-Points
- `LevelLayoutGenerator.IsMiniBossLevel(n)` mit Modulo-5-und-nicht-10-Check
- `LevelGenerator.SpawnBossAtPosition` mit `miniBoss: true`-Param

**Impact:** Gefühltes Content-Volumen verdoppelt — 10 Boss-Encounter → 20 pro Story.

---

#### 11. WhatsNew + WorldStory UI-Modals bauen

**Status:**
- `IWhatsNewService` + `WhatsNewViewModel` fertig — **View fehlt**
- `IWorldStoryService` mit 10 Intros + 9 Outros + RESX in 6 Sprachen — **Renderer/Modal fehlt**

**Lösung:**
- `WhatsNewView.axaml` (Avalonia UserControl, Compiled Bindings, x:DataType=WhatsNewViewModel) in MainView eingebettet via Overlay-Aggregat
- `WorldStoryView.axaml` mit Skip-Button + Standbild + 2 Sätze Text + Stinger-SFX-Trigger
- GameEngine.Level.cs triggert Intro vor WorldAnnouncement, Outro nach Boss-Kill
- `IsAnyOverlayOpen`-Aggregat um WhatsNew + WorldStory erweitern (Hit-Test)

**Impact:** WhatsNew zeigt Patch-Notes nach Update. WorldStory gibt Story-Kontext pro Welt.

---

#### 12. Card-Crafting UI

**Status:** Service-API für Crafting (5C+2.000C→1R, 5R+8.000C→1E, 5E+25.000C→1L) im `CardService` als Coin-Sink dokumentiert, UI fehlt.

**Lösung:**
- `CraftingView.axaml` als Sub-View in DeckView mit Slot-Vorschau, Cost-Anzeige, Recipe-Buttons
- RESX-Keys in 6 Sprachen
- Bestätigungs-Dialog vor Crafting (Coin-Konsum + Karten-Konsum)

**Impact:** Coin-Sink für Endgame, Common-/Rare-Cards bekommen Wert.

---

#### 13. Hero/Character-Trait-Effekte verkabeln

**Status:** `IHeroService` + 5 Heroes + `ApplyHeroStats()` für Start-Stats aktiv. Spezial-Traits (DoubleDetonation/LuckyDrops/DemolitionExpert/QuickPocket) wirken noch nicht im Gameplay.

**Lösung:**
- `Trait == DoubleDetonation`: Bomb explodiert + spawnt nach 0.5s Sekundär-Explosion am selben Ort
- `Trait == LuckyDrops`: Drop-Roll in `BlockDestroyed` × `PowerUpDropMultiplier`
- `Trait == DemolitionExpert`: Block-Drop-Chance + `BlockDropChanceBonus` (additiv)
- `Trait == QuickPocket`: CoinService.AddCoins × `CoinPickupMultiplier`, kein Speed-Penalty bei Curse.Slow

**Impact:** Heroes bekommen mechanische Differenzierung, nicht nur Start-Stats.

---

#### 14. Clan-System (Firebase scharfschalten)

**Status:** `NullClanService` aktiver Default. `FirebaseClanService` existiert teilweise, ist aber nicht als Default registriert. Domain-Models (ClanData, ClanMember, ClanChatMessage) komplett.

**Lösung:**
- App.axaml.cs `ClanServiceFactory` auf `FirebaseClanService` umstellen (Android-only, Desktop bleibt Null)
- Profanity-Filter aus `LeagueService` recyceln
- `database.rules.json` um `/clans/{clanId}` mit Rate-Limit + Member-Cap
- Multi-Path-Updates für Member-Beitritte (atomar)
- Asynchroner Pull (30s) wie geplant

**Impact:** Retention-Multiplier durch Clan-Goals.

---

### Tier 4 — Audio + Premium-Erweiterungen

#### 15. Adaptive Music komplett (SoundTouch + LUFS + Layered Stinger)

**Status:** `AudioBusMixer.Boost(bus, multiplier, duration)` Pfad da. Music-Boost-Trigger bei Last-Enemy-Drama und ULTRA-Combo. **Fehlt:** EQ-Sidechain auf Drum-Frequenzen, Tempo-Pitch-Shift bei Last-10s, LUFS-Mastering.

**Lösung mit Bordmitteln:**
- **EQ-Sidechain auf Drum-Frequenzen** (60–200 Hz): Bei Combat lauter, bei Erkundung leiser. Über `AudioBusMixer`-EQ ergänzen.
- **SoundTouch.Net** NuGet: Bei Last-Enemy / Last-10-Seconds +1 Semitone und +5% BPM.
- **Layered Stinger:** Combat-Start layer ein Drum-Loop (Kenney CC0) über die Welt-Musik. Bei Combat-Ende fadet Drum-Loop aus.
- **LUFS-Mastering-Pass:** `ffmpeg -i in.ogg -af loudnorm=I=-16:TP=-1:LRA=11 out.ogg` als Bash-Skript für alle Tracks.

**Impact:** Musik dynamischer. LUFS-Pass löst hörbares Welt-Wechsel-Problem.

---

#### 16. BattlePassPlus + VIP-Subscription-IAP scharfschalten

**Status:** `IBattlePassPlusService` + `IVipSubscriptionService` Code-Foundation. IAP-Console-Setup (Product-IDs `battle_pass_plus_season`, `vip_monthly`) + Server-Validation deferred.

**Lösung:**
- Google Play Console: SKUs anlegen
- BillingClient-Wiring in AndroidPurchaseService um Subscription-Branch erweitern (Subscriptions API v3)
- Server-Validation deferred (kein eigener Server) — nutze stattdessen Google Play Receipt-Verification offline (`InAppPurchase.purchaseState`)

**Impact:** Premium-Revenue-Stream zusätzlich zu remove_ads.

---

#### 17. 2P-Co-Op (echte Engine-Integration)

**Status:** Engine-Foundation komplett (`Player2`, `UpdatePlayer2Movement`, `MultiplayerSpawnPositions`, `MultiplayerSessionService`). **Fehlt:** UI/Joystick für Spieler 2 (Split-Screen-Control oder Dual-Stick), Co-Op-Camera-Zoom, GameOver-Bedingung „beide tot", Co-Op-Score-Aggregat.

**Lösung:**
- Avalonia-View mit 2 NeonJoystick-Instanzen (oben rechts für P2)
- Bomb-Button-Layout an Touch-Verteilung anpassen (P1 unten links, P2 unten rechts)
- `GameViewModel.CoopActive` Property + UI-Toggle
- 2P-Score-HUD im rechten Panel

**Impact:** Couch-Co-Op = großer Marketing-Hook für Friends-Mode.

---

## Was bewusst NICHT in diesem Audit ist

Aufgrund der Vorgabe (Solo-Dev, kein externes Budget, kein Server):

- ~~Composer-Beauftragung~~ → Adaptive Music-Engine mit existierenden Tracks (#15)
- ~~Voice-Acting~~ → kein adäquater Code-only-Ersatz, akzeptiere „kein Voice"
- ~~Art-Direction-Audit durch Artist~~ → Outline-Pass-Shader (erledigt v2.0.57)
- ~~Hand-pixel-Sprite-Animation~~ → prozedurale Squash & Stretch (erledigt)
- ~~Server-side-Anti-Cheat~~ → Client-side Determinismus via FixedTimestep (#8)

---

## Abarbeitungs-Reihenfolge (v2.0.58 → v2.0.62)

**Block A — Content + UI-Polish (Release-Kandidat v2.0.58)**
1. Boss-Phase-2 Attack-Pattern-Variation (#9)
2. WhatsNew + WorldStory UI-Modals (#11)
3. Mini-Bosse zwischen Welten (#10)
4. Card-Crafting UI (#12)
5. Hero-Trait-Effekte verkabeln (#13)
6. Outline-Pass Konsistenz-Check (alle Sprites)

**Block B — Engine-Reife (Release-Kandidat v2.0.59)**
7. FixedTimestep + DeterministicRandom-Migration (#8)
8. IGameMode-Hooks aus Engine-Loop (#7)
9. ILogger-Pilot-Migration 10 Services (#5)

**Block C — Live-Ops (Release-Kandidat v2.0.60)**
10. Firebase Remote Config (#1)
11. Funnel-Lücken-Audit (#2)
12. 3 separate Tutorial-Levels (#3)

**Block D — UX-Refactor (Release-Kandidat v2.0.61)**
13. Bottom-Tab-Konsolidierung (#4)
14. MainViewModel-Reduktion (#6)

**Block E — Audio + Premium (Release-Kandidat v2.0.62)**
15. Adaptive Music komplett (#15)
16. BattlePassPlus + VIP-IAP (#16)
17. Clan-System Firebase scharfschalten (#14)

**Block F — Major-Feature (separate Release-Reihe)**
18. 2P-Co-Op Engine-Integration (#17)

---

## Erwartete Bewertung nach voller Umsetzung

| Achse | v2.0.57 heute | Nach Block A+B | Nach Block C+D | Nach Block E+F |
|-------|---------------|----------------|----------------|----------------|
| Engine-Architektur | 8.5 | 9.5 | 9.5 | 9.5 |
| Performance | 9 | 9.5 | 9.5 | 9.5 |
| Game Feel / Juice | 9 | 9.5 | 9.5 | 9.5 |
| Audio (technisch) | 6.5 | 6.5 | 6.5 | 7.5 |
| Visuals (Engine) | 7 | 7.5 | 7.5 | 8 |
| Content-Volumen | 6 | 8 | 8 | 8.5 |
| Live-Ops | 7 | 7 | 9 | 9.5 |
| UX/UI | 6 | 7 | 9 | 9 |
| Architektur-Hygiene | 8 | 9 | 9.5 | 9.5 |
| **Gesamt** | **7.4** | **8.2** | **8.7** | **9.0** |

**Ehrliche Decke ohne externes Budget: 8.8–9.0/10.** In Sichtweite von AAA-Mobile-Mid-Tier. Der Sprung auf 9.5+ (Brawl-Stars-Niveau) gibt es nicht ohne Audio-/Art-Investment.

---

## Definition of Done (pro Block)

Bevor ein Block als „erledigt" markiert und ein Release rausgeht, muss zwingend gelten:

- Build grün auf Solution-Ebene (`dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln`)
- AppChecker grün für BomberBlast (`dotnet run --project tools/AppChecker BomberBlast`)
- MVVM-Check sauber (kein Code-Behind außer Generated, Compiled-Bindings, keine View→VM-Direct-Refs)
- RESX-Vollständigkeit über alle 6 Sprachen für neue Strings (`/skill localize-check`)
- Manuelle Smoke-Tests auf Android-Gerät: Startup, Tutorial, Story-Level 1, Bomb-Place, Shop öffnen, Settings
- Crashlytics-Counter unverändert nach Smoke-Tests
- Telemetry-/Analytics-Events feuern wie spezifiziert (über Logcat verifiziert)
- Version-Bump in `BomberBlast.Shared.csproj` (`<Version>`) und Android-Manifest (`versionCode` / `versionName`)
- `WhatsNewService.GetEntries()` um den neuen `switch`-Branch für die neue Version erweitert
- Changelog-Eintrag und Social-Posts (`/skill changelog`)
- Geschlossener-Test-Upload nur, wenn alle obigen Punkte abgehakt
