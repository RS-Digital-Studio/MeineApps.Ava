# BomberBlast — AAA-Quality-Audit (Solo-Dev / Code-Only)

**Version analysiert:** v2.0.56 (Produktion)
**Datum:** 2026-05-12
**Scope:** Ausschliesslich Verbesserungen, die du als Solo-Dev ohne externes Budget und ohne Server-Infrastruktur umsetzen kannst. Kein Composer, kein Artist, kein Voice-Actor, kein Pi-Server, kein Live-PvP.

---

## Kern-Befund

Die Codebase ist auf einem Indie-Level, das viele bezahlte Studios nicht erreichen. Engine-Architektur, Performance-Optimierung und Game-Feel sind bereits Pro-Niveau. Was zu AAA fehlt, ist primär **Content-Volumen, Live-Ops-Reife und UX-Konsolidierung** — alles Felder, die du ohne externe Hilfe weiter ausbauen kannst.

**Realistische Decke mit Code-only-Ansatz: ~7-7.5/10.** Das ist "Premium Indie" — eine Stufe ueber dem aktuellen Stand und deutlich ueber dem, was 90% der Mobile-Indies erreichen.

---

## Bewertung pro Achse (Code-only-Sicht)

| Achse | Status | Potenzial (Code-only) | Hebel |
|-------|--------|------------------------|-------|
| Engine-Architektur | 8.5/10 | 9.5/10 | Mode-Plugin-Migration abschliessen |
| Performance | 9/10 | 9.5/10 | FixedTimestep integrieren |
| Game Feel / Juice | 8.5/10 | 9.5/10 | Welt-Themed FX + Ultra-Combo-Flash |
| Audio (technisch) | 6/10 | 7.5/10 | Adaptive Layered Logic + LUFS-Mastering |
| Visuals (Engine) | 6/10 | 7.5/10 | Outline-Pass + Style-Lookup |
| Content-Volumen | 5/10 | 7/10 | Mehr Bosse/Enemies aus existierender Engine |
| Live-Ops | 6/10 | 9/10 | Remote Config + Funnel-Events + Push |
| UX/UI | 6/10 | 8.5/10 | Tab-Konsolidierung + Tutorial-Erweiterung |
| Architektur-Hygiene | 8/10 | 9/10 | God-VM aufloesen, Logging modernisieren |

---

## Tier 1 — Sofort (4 Wochen, hoechster ROI)

### 1. Firebase Remote Config integrieren

**Problem:** Du kannst nichts in Production tunen ohne App-Update. Preise, Drop-Rates, Event-Toggles, Difficulty-Werte — alles hardcoded.

**Loesung:**
- `Xamarin.Firebase.Config` Paket hinzufuegen
- `IRemoteConfigService` Interface + `FirebaseRemoteConfigService` + `NullRemoteConfigService` (Desktop-Fallback)
- Werte: `event_active_halloween`, `drop_rate_legendary_card`, `gem_pack_small_price`, `combo_slowmo_threshold`, `boss_telegraph_duration_ms`
- Default-Werte in lokaler JSON, Remote ueberschreibt nach `FetchAndActivateAsync`
- Cache 1h in Production, 5 Min in Debug

**Aufwand:** 1 Tag
**Impact:** Riesig. Schaltet alle weiteren Live-Ops-Verbesserungen frei.

---

### 2. Funnel-Event-Telemetrie

**Problem:** Du weisst nicht, wo Spieler abbrechen. Aktuelle Telemetrie deckt FPS + Memory ab — keine Game-Funnel-Events.

**Loesung:** 30 Events via `IAnalyticsService`:
- `level_start` (level_id, world_id, lives, deck_id)
- `level_complete` (level_id, time_ms, stars, deaths)
- `level_fail` (level_id, cause: enemy/bomb/time, attempt_count)
- `boss_encounter` (boss_type, phase)
- `boss_defeated` (boss_type, time_ms, damage_taken)
- `shop_opened` (entry_point)
- `purchase_flow_start` (sku, price, currency)
- `purchase_success` / `purchase_cancel` / `purchase_fail`
- `rewarded_ad_request` / `rewarded_ad_completed` (placement)
- `tutorial_step_complete` (step_id)
- `feature_unlocked` (feature_id, session_count)
- `daily_login` (consecutive_days)
- `combo_tier_reached` (tier, level_id)

**Aufwand:** 1 Sprint (3-5 Tage)
**Impact:** Datenbasis fuer alle weiteren Balancing-Entscheidungen.

---

### 3. Re-Engagement Push-Trigger

**Problem:** Push-Service ist verdrahtet, aber keine Auto-Trigger fuer inaktive Spieler.

**Loesung:** Lokale `NotificationScheduler` (kein Server noetig — Android `AlarmManager` + iOS local-notif-Schedule):
- **D1-no-return:** "Deine taegliche Belohnung wartet" (24h nach letztem Login, wenn DailyReward nicht abgeholt)
- **D3-no-return:** "Dein Battle-Pass laeuft in {days} Tagen ab" (nur wenn BP-Tier < Max)
- **D7-no-return:** "Wir haben dich vermisst! Hier sind 100 Gems" (one-shot, einmal pro Saison)
- Trigger werden beim App-Background gesetzt, beim Foreground gecancelt
- Respekt fuer Notification-Settings (DSGVO: Opt-in beim Onboarding)

**Aufwand:** 2-3 Tage
**Impact:** D1-Retention typischerweise +10-15% durch Push.

---

### 4. Bottom-Tab-Konsolidierung 15 -> 4

**Problem:** 15 sichtbare Tabs (Modi + Shop + GemShop + BP + League + Collection + Profile + Achievements + Daily + Weekly + Race + Dungeon + LuckySpin). Brawl Stars hat 4 Tabs.

**Loesung:** 4 Bottom-Tabs:
- **Home** — Story-Progression + Daily-Dashboard mit Karten (Daily-Challenge, Daily-Race, Lucky-Spin als Cards)
- **Spielen** — Quick / Survival / Dungeon / Boss-Rush / Master als Liste mit Hero-Image
- **Shop** — Coin-Shop / Gem-Shop / Battle-Pass als horizontale Sub-Tabs
- **Profil** — Achievements / Collection / League / Statistics / Settings als ListView

Side-Loops (Daily-Race, Lucky-Spin, Weekly) bleiben erreichbar, aber als **Karten im Home-Dashboard** statt als eigene Tabs. Mental-Load des Spielers sinkt drastisch.

**Aufwand:** 3-5 Tage (UI-Refactoring)
**Impact:** Spielbarkeit fuer Neulinge signifikant besser, Conversion-Rate steigt.

---

### 5. Tutorial auf 3 Tutorial-Levels

**Problem:** Aktuell 6 Tutorial-Schritte auf Level 1. Genre-Neulinge (nicht-Bomberman-Spieler) verstehen die Tiefe nicht.

**Loesung:** 3 geschuetzte Tutorial-Levels mit Force-Direction-Hints:
- **T1: Movement** — Nur DPad, keine Bomben. Sammle 5 Coins. Erklaere Tab-Bar.
- **T2: Bomb-Mechanik** — Bombe legen, Timing, Explosion-Reichweite. Zerstoere 3 Bricks, toete 1 Enemy.
- **T3: Power-Ups** — Pick-up von Fire+1, Speed+1, Bomb+1. Boss-Lite-Encounter mit telegraphed Attack.

Nach T3: Erster Story-Level mit deutlich reduzierter Schwierigkeit (Soft-Onboarding-Curve).

**Aufwand:** 3-4 Tage (Level-Design + Tutorial-Step-Erweiterung)
**Impact:** D0-D1-Conversion fuer Genre-Neulinge typisch +20-30%.

---

### 6. Welt-Themed Bomb-FX

**Problem:** Bombe in Welt 10 (Shadow Realm) hat selben orangen Explosion-Look wie Welt 1.

**Loesung:** Lookup-Table im `BombRenderer`:
```csharp
private static readonly Dictionary<int, BombFxTheme> WorldFx = new()
{
    [1] = new(Inner: 0xFFFF6B35, Outer: 0xFFFFD93D, Spark: 0xFFFFFFFF), // Default
    [2] = new(Inner: 0xFF8B4513, Outer: 0xFFA0522D, Spark: 0xFFDEB887), // Desert
    // ... 10 Welten
};
```

Jedes Bomb-Place-Event holt sich Theme per `_world.CurrentWorldId`. Same SkSL-Shader, andere Uniforms.

**Aufwand:** 1 Tag
**Impact:** Welt-Wechsel fuehlt sich endlich neu an, nicht nur ein anderer Hintergrund.

---

### 7. ULTRA-Combo Vollbild-Vignette-Flash

**Problem:** Ultra-Combo (x10+) hat Stinger + SlowMo + Subtitle, aber kein "Bildschirm-Bruellen". Bei Vampire Survivors / Risk of Rain ist das der ikonische Moment.

**Loesung:** `UltraComboFlash` Renderer:
- 1 SkPaint mit RadialGradient (Mitte transparent, Rand voll Farbe)
- 200ms Animation: Alpha 0 -> 1 in 80ms, dann 1 -> 0 in 120ms
- Farbe via Welt-Theme (in Shadow-Realm violett, in Desert orange)
- Trigger ueber `IGameJuiceEmitter.RequestUltraFlash()`

**Aufwand:** Halber Tag (~50 LOC)
**Impact:** Combo-System fuehlt sich endlich "fertig" an.

---

### 8. Adaptive-Icon-Layers + Monochrome-Icon

**Problem:** App-Icon ist single PNG. Android 8+ (alle modernen Geraete) erwarten Foreground+Background separat. Android 13+ Themed Icons brauchen Monochrome-Variante.

**Loesung:**
- `Resources/mipmap-anydpi-v26/ic_launcher.xml` — Adaptive Layers
- `mipmap-*/ic_launcher_foreground.png` (alle DPI-Buckets)
- `mipmap-*/ic_launcher_background.png` (oder XML-Color)
- `drawable/ic_launcher_monochrome.xml` — Vektor-only, single-color

Tool: Android Studio Image Asset Wizard oder dein eigener `StoreAssetGenerator` erweitert.

**Aufwand:** Halber Tag
**Impact:** App sieht auf modernen Geraeten endlich nicht mehr "alt" aus.

---

### 9. Microsoft.Extensions.Logging + Crashlytics-Sink

**Problem:** `AppLogger` ist 24-LOC `Trace.WriteLine`-Wrapper. Keine Levels, keine Scopes, keine File-Persistierung, keine Filterung.

**Loesung:**
- `ILogger<T>` ueberall per DI injizieren
- `LoggerFactory.Create` mit:
  - Android: `Crashlytics`-Sink (Custom-Sink, ~50 LOC)
  - Desktop: Console + RollingFile (`Serilog.Extensions.Logging` oder `Microsoft.Extensions.Logging.File`)
- LogLevels: Trace/Debug nur Debug-Build, Info+ in Production
- Scopes fuer GameSession (`using (_logger.BeginScope("game={id}", gameId))`)

**Aufwand:** 1-2 Tage (53 Services zum Migrieren)
**Impact:** Crash-Reports endlich mit Kontext. Bug-Reproduktion deutlich schneller.

---

### 10. MainViewModel reduzieren (1218 -> 600 LOC)

**Problem:** `MainViewModel.cs` ist God-VM. Navigation-Hub + Event-Bus + Wirings + Lazy-VM-Management in einer Klasse.

**Loesung:**
- `NavigationHub` (eigene Klasse, nur Routing-Logik + ActiveView-Enum)
- `GameEventBus` (Pub/Sub fuer FloatingText/Celebration/ExitHint/Message)
- `LazyVmRegistry` (verwaltet `EnsureXxxVm()`-Methoden, public Property-Bag)
- `MainViewModel` bleibt nur als Komposit + INavigable + Constructor-Injection
- Onboarding + Dashboard sind bereits extrahiert — gleichen Pattern fuer alle anderen Bereiche

**Aufwand:** 2-3 Tage
**Impact:** Weniger Bug-Risiko bei Aenderungen, neue Features ohne God-VM-Anfassen.

---

## Tier 2 — Mittelfristig (1-3 Monate, Code-only)

### 11. GameEngine Mode-Plugin-Migration abschliessen

**Problem:** `_isStoryMode`, `_isSurvivalMode`, `_isDungeonMode` etc. laufen parallel zu `_currentMode`. IGameMode-Pattern angelegt, aber Logik noch verstreut. 30+ Property-Aliasse fuer DungeonMode-State sind technische Schuld.

**Loesung:**
- Jeder Mode (`StoryMode`, `SurvivalMode`, `DungeonMode`, `BossRushMode`, `QuickPlayMode`, `DailyChallengeMode`, `DailyRaceMode`, `MasterMode`) wird volle `IGameMode`-Implementierung
- `IGameMode` Interface bekommt: `Initialize`, `OnLevelStart`, `OnEnemyKilled`, `OnBombExploded`, `OnPlayerHit`, `OnLevelComplete`, `OnGameOver`, `GetScoreModifier`
- GameEngine ruft nur `_currentMode.OnXxx()` auf, kein `if (_isStoryMode) ... else if (_isDungeon)` mehr
- Mode-spezifische State-Properties wandern in den Mode

**Aufwand:** 2-3 Wochen (Risiko: Regression-Tests muessen ueberall durchlaufen)
**Impact:** Neue Modi ohne Engine-Touch hinzufuegen, technische Schuld weg.

---

### 12. FixedTimestep + DeterministicRandom integrieren

**Problem:** Foundation steht (`FixedTimestepRunner.cs`, `DeterministicRandom.cs`, `ReplayCapture.cs`), aber nicht integriert. Replay-System und Anti-Cheat bleiben blockiert.

**Loesung:**
- GameEngine.Update mit Accumulator: `while (accumulator >= FixedDt) { Tick(FixedDt); accumulator -= FixedDt; }`
- Render-Interpolation: `float alpha = accumulator / FixedDt;` fuer Sprite-Position-Smoothing
- Alle `Random.Next` durch `_rng.Next` ersetzen (RngProvider zentral)
- Replay-Capture: 1 Byte pro Tick (Input-Bitmask) + Seed → vollstaendig deterministisch reproduzierbar
- Replay-Replay-Validation als Test: 100 zufaellige Replays werden 100x abgespielt und muessen identisches Ergebnis liefern

**Aufwand:** 2 Wochen
**Impact:** Replay-System (Best-of-Day-Replays als Spielanreiz), Anti-Cheat-Validierung fuer League moeglich.

---

### 13. Adaptive Music-Engine (mit existierenden Tracks)

**Problem:** Audio-Engine kann adaptive Layered Music, aber Tracks sind komplett (kein Stem-Splitting). Du kannst keinen externen Composer beauftragen.

**Loesung mit Bordmitteln:**
- **EQ-Sidechain auf Drum-Frequenzen:** Bei Combat lass Sub-Bass + Drum-Frequenzen (60-200 Hz) lauter, bei Erkundung leiser. Funktioniert via `AudioBus` EQ.
- **Layered Stinger:** Wenn Combat startet, layer ein Drum-Loop (kurzer Kenney-Loop) UEBER die Welt-Musik. Bei Combat-Ende: Drum-Loop fadet aus.
- **Tempo-Pitch-Shift:** Bei Last-Enemy/Last-10-Seconds: Welt-Musik wird via `SoundTouch` (NuGet) +1 Semitone und +5% BPM gepitcht. Spannungseffekt.
- **LUFS-Mastering-Pass:** `ffmpeg -i in.ogg -af loudnorm=I=-16:TP=-1:LRA=11 out.ogg` als Bash-Skript fuer alle Tracks. Loest Lautstaerke-Spruenge.

**Aufwand:** 3-5 Tage
**Impact:** Auch ohne Composer fuehlt sich Musik dynamischer an. LUFS-Pass loest hoerbares Welt-Wechsel-Problem.

---

### 14. Outline-Pass im Renderer

**Problem:** Inkonsistente Art-Styles (Vektor-Player + AI-WebP-Bosse + AI-WebP-Enemies) sehen aus wie drei verschiedene Spiele in einem Frame.

**Loesung:** SkSL-Shader `outline.sksl`:
- Sample Alpha bei 8 Pixel-Offsets um aktuelles Pixel
- Wenn aktuelles Pixel transparent UND mindestens 1 Nachbar opak → Outline-Color
- Outline-Color = `0xFF0A0A0F` (sehr dunkel), Outline-Breite konfigurierbar via Uniform

Anwendung: Per-Entity-Toggle `Entity.RenderOutline = true`. PowerUps + Bosse + Enemies + Player bekommen Outline → vereinheitlicht.

**Aufwand:** 1-2 Tage
**Impact:** Optische Konsistenz signifikant besser, ohne Assets neu zu zeichnen.

---

### 15. Mehr Bosse und Enemies aus existierender Engine

**Problem:** 5 Boss-Typen, 12 Enemy-Typen. Brawl Stars hat ~30 in einem Showdown-Event-Run.

**Loesung (Code-only, vorhandene Art-Assets recyceln):**
- **Elite-Varianten:** Jeder bestehende Enemy bekommt eine Elite-Version (1.5x HP, +20% Speed, lila Outline, 3x Coins). 12 -> 24 Enemy-Typen.
- **Boss-Varianten:** Jeder Boss-Typ bekommt Phase-2-Variante mit anderem Attack-Pattern (gleiche Sprite, andere Telegraph-Color). 5 -> 10 Bosse.
- **Mini-Bosse zwischen Welten:** L10/L20/L30 etc. — Mini-Boss mit reskinned-Enemy-Sprite und neuer AI. Statt 10 → 20 Encounter.
- **Boss-Modifier-System:** Boss bekommt 1 von 8 Modifiern (Shielded, Fast, Healing, Summoner, …). 5 Bosse x 8 Modifier = 40 Boss-Variationen.

**Aufwand:** 2-3 Wochen
**Impact:** Gefuehltes Content-Volumen verdoppelt, ohne neue Assets.

---

### 16. Mini-Story-Beats pro Welt

**Problem:** "Welt freischalten" ist keine Motivation. Wo ist das Why?

**Loesung (Code-only):**
- 1 Cutscene pro Welt-Start (Welt 1-10): 2 Saetze Text + Standbild aus existierenden Asset-Bitmaps + Stinger-SFX
- Cutscene-Engine = `CinematicSequencer` (existiert bereits)
- Texte lokalisiert in RESX (6 Sprachen)
- Beispiel: "Die Wueste flimmert. Ein alter Bombenleger ruht hier seit 100 Jahren. Wecke ihn nicht."
- Nach Welt-Boss: 1 Outro-Cutscene mit Cliffhanger zur naechsten Welt

**Aufwand:** 1 Woche (Schreiben + Lokalisierung + Integration)
**Impact:** Spieler bekommen Story-Kontext. Skip-Button respektiert Hardcore-Spieler.

---

### 17. "What's New"-Modal nach App-Update

**Problem:** Spieler sehen nicht, was du veroeffentlicht hast.

**Loesung:**
- `WhatsNewService` mit lokaler JSON `WhatsNew_2.0.57.json` (Title + 3-5 Bullets + Hero-Image-Path)
- Beim App-Start: Vergleiche `LastSeenVersion` Pref mit aktueller Version → wenn neuer, zeige Modal
- Modal hat "Spaeter" + "Verstanden" Buttons, deaktiviert durch Tap-Outside
- Auto-Version aus `Assembly.GetExecutingAssembly().GetName().Version` (loest auch das `AppVersion = "v2.0.56"`-Hardcoded-Problem)

**Aufwand:** 2 Tage
**Impact:** Spieler wissen, dass das Spiel lebt. Update-Conversion + Retention besser.

---

### 18. Player i-Frame Visualisierung

**Problem:** Player wird getroffen → harter Cut. AAA-Standard ist 200ms i-Frames mit Sprite-Blink + Vignette-Flash.

**Loesung:**
- `Player.IsInvincible = true` fuer 800ms nach Hit
- `Player.RenderAlpha` toggelt zwischen 0.3 und 1.0 alle 80ms (10 Blinks)
- Roter Vignette-Flash am Bildschirmrand (gleiche Mechanik wie Ultra-Combo-Flash, andere Farbe)
- Sound: "hurt.ogg" + leichtes Hit-Pause (80ms)
- Bei Re-Hit waehrend i-Frame: ignoriert (kein Insta-Death durch Doppel-Explosion)

**Aufwand:** 1 Tag
**Impact:** Spieler fuehlen sich respektiert, weniger frustrierende Death-Loops.

---

### 19. Anticipation-Frames fuer Big-Actions

**Problem:** Bomb-Place geht sofort. AAA-Pattern: 80ms "wind-up" wo Player-Sprite sich zusammenzieht, bevor Bomb-Place auslöst.

**Loesung:**
- Bomb-Place: 80ms Player-Squash-Animation (vertikal stauchen, horizontal strecken) -> dann Bomb spawnt
- Boss-Big-Attack: 120ms Anticipation (Boss-Sprite zieht sich zurueck) -> dann Attack
- Beide via existierende Easing-Library

**Aufwand:** Halber Tag
**Impact:** Spiel fuehlt sich "schwerer" und befriedigender an.

---

### 20. Feature-Unlock-Choreographie

**Problem:** Spieler sieht nicht, wann welcher Tab freigeschaltet wird.

**Loesung:**
- `IFeatureUnlockChoreographer` mit Events: `OnLevelComplete`, `OnAchievementUnlocked`
- Bei Trigger (z.B. L20 erreicht): Vollbild-Overlay "NEU: Dungeon Mode freigeschaltet!" mit grossem Icon + Stinger-SFX + 1500ms Auto-Close + "Jetzt entdecken"-CTA
- Feature-Unlock-Queue (wenn 2 Features gleichzeitig freischalten, hintereinander zeigen)
- Per Pref-Flag: jedes Feature-Unlock-Overlay wird nur einmal gezeigt

**Aufwand:** 2-3 Tage
**Impact:** Progressives Reveal statt Information-Overload am Tag 1.

---

## Tier 3 — Langfristig (3-6 Monate, Code-only)

### 21. Hero/Character-System

**Konzept:** 3-5 spielbare Charaktere mit unterschiedlichen Start-Stats. Mit existierender Player-Engine moeglich, nur Sprite-Variation + Stat-Sheets.

**Charaktere (Beispiel):**
- **Default-Bomber:** Stats: Speed 2, Fire 2, Bombs 1 (Status quo)
- **Speedy Sam:** Speed 3, Fire 1, Bombs 1, +5% Coin-Pickup
- **Brick-Boris:** Speed 1, Fire 3, Bombs 2, -1 Start-Heart, +10% Block-Drop
- **Twin-Tina:** Speed 2, Fire 1, Bombs 2, Bombs zuenden ein-zweimal nacheinander
- **Lucky-Lola:** Speed 2, Fire 2, Bombs 1, +20% PowerUp-Drop-Chance

Jeder Charakter hat eigene Combo-Quote + eigene Cosmetic-Slots. Unlock via Achievement oder Gem-Kauf.

**Aufwand:** 3-4 Wochen
**Impact:** Replayability + Monetization + Identifikations-Anker.

---

### 22. 2P-Local-Co-Op

**Konzept:** Foundation steht in `Core/Multiplayer/` (Player2, InputBuffer, GameStateSnapshot). Lokal-Splitscreen oder Shared-Screen-Co-Op.

**Modi:**
- **Splitscreen** (Desktop): Linker Player WASD+Space, rechter Player Pfeiltasten+Enter
- **Shared-Screen** (Mobile): Beide Spieler auf einem Geraet (P1 linke Bildschirm-Haelfte, P2 rechte), wenn beide ein Gamepad anschliessen
- **Co-Op-Modi:** Story-Co-Op (geteilte Hearts), Survival-Co-Op (geteiltes Score-Leaderboard), Dungeon-Co-Op
- **PvP-Modus:** Klassischer Bomberman-PvP, last-bomber-standing

**Aufwand:** 4-6 Wochen
**Impact:** Social-Loop ohne Server. Lokal-Multiplayer ist USP vs. den meisten Mobile-Games.

---

### 23. Clan-System (Asynchron, Firebase-basiert)

**Konzept:** Keine Live-Sync, keine Server-Infrastruktur. Nur Firebase-Realtime-Database wie League.

**Features:**
- Spieler erstellt Clan (10 Member max) oder tritt bei (via 6-stelliger Code)
- Clan-Wochenziel: "Sammelt 10.000 Coins gemeinsam" → Belohnung fuer alle Member
- Clan-Leaderboard (Top 10 globale Clans)
- Clan-Chat **asynchron** (alle 30s Pull, kein Realtime-Sync) — 50 letzte Messages, Profanity-Filter
- Clan-Helfen: Member kann Bomb-Card "spenden" (1x pro Tag)
- Profanity-Filter + Report-Button (existiert bereits aus League)

**Aufwand:** 4-6 Wochen
**Impact:** Retention-Multiplier (Spieler kommen wieder, weil Clan-Goal laeuft).

---

### 24. Wochentlicher Content-Drop-Pipeline

**Konzept:** Du allein als Solo-Dev kannst keinen woechentlichen Drop in Asset-Volumen schaffen. Aber du kannst die Pipeline + Tooling so bauen, dass jede Woche etwas Neues lebt.

**Loesung:**
- **Wochen-Modifier:** Jede Woche eine neue Mutator-Kombination (z.B. Woche 23: "Ice + Speed-Drain"). Existierende Mutatoren randomisiert.
- **Wochen-Layout:** 1 hand-designed Map pro Woche (du selbst, in 1-2h). 52 Maps/Jahr.
- **Wochen-Lucky-Spin-Reward:** 1 limitiertes Cosmetic pro Woche (recyceltes Frame mit neuer Farb-Variation, Code-generiert).
- **Wochen-Boss-Encounter:** Existierender Boss mit neuem Modifier-Set + reskinned Color.
- Alles ueber **Remote Config + JSON-Definitionen** auslieferbar, ohne App-Update.

**Aufwand:** Initial 2 Wochen (Pipeline), dann 2-4h/Woche
**Impact:** Spiel wirkt lebendig, Daily-Active-User-Rate steigt.

---

### 25. Splash-Crash-Recovery

**Problem:** Wenn `App.axaml.cs` crasht, sieht User Black-Screen. Spielt nicht mehr.

**Loesung:**
- `app_crash_count` Preference, inkrementiert vor jeder Initialisierung, decrementiert nach erfolgreichem Splash
- Bei `crash_count >= 3` → Reset-Dialog: "Es scheint ein Problem zu geben. Spielfortschritt zuruecksetzen?" mit "Nein, weiter versuchen" + "Ja, zuruecksetzen"
- Try/Catch um `InitializeAsync` mit Crashlytics-Report bei Exception
- Safe-Mode: Minimal-Initialisierung ohne optional Services (Firebase, Ads, Audio) → mindestens Settings + Account-Delete erreichbar

**Aufwand:** 2 Tage
**Impact:** Spiel ist nach Crash-Loop noch rettbar statt deinstalliert.

---

## Sofort-Backlog (Pre-Commit-Hygiene)

Diese Punkte sind klein genug fuer einen einzigen Sprint und kosten dich Stunden, nicht Tage:

- **Splash-Versions-String automatisieren:** `Assembly.GetExecutingAssembly().GetName().Version.ToString(3)` statt `"v2.0.56"` hardcoded
- **`AppLogger.cs` durch `ILogger<T>` ersetzen** (Tier 1 Punkt 9)
- **30+ DungeonMode-Property-Aliasse aufloesen** (Teil von Tier 2 Punkt 11)
- **`RemoteConfig` als `NullRemoteConfigService` Stub anlegen**, schon mal die Properties definieren — auch wenn Firebase-Integration spaeter kommt
- **Empty-State-Polish:** Achievements-Tab "0/66" sollte illustriertes Empty-State mit "Spiele dein erstes Level"-CTA haben
- **MaterialIconStyles-Registry-Check:** Stelle sicher, dass alle 6 Sprachen RESX-Komplett sind (du hast den Skill `localize-check` dafuer)

---

## Was bewusst NICHT in diesem Audit ist

Aufgrund deiner Vorgabe (Solo-Dev, kein externes Budget, kein Server):

- ~~Composer-Beauftragung~~ (-> stattdessen: Adaptive Music-Engine mit existierenden Tracks, Punkt 13)
- ~~Voice-Acting~~ (-> kein adaequater Code-only-Ersatz, akzeptiere "kein Voice")
- ~~Art-Direction-Audit durch Artist~~ (-> stattdessen: Outline-Pass-Shader, Punkt 14)
- ~~Hand-pixel-Sprite-Animation~~ (-> stattdessen: bestehende prozedurale Sin-Wippe behalten, Squash & Stretch ausbauen)
- ~~Live-PvP via Pi-Server~~ (-> stattdessen: 2P-Local-Co-Op, Punkt 22)
- ~~Server-side-Anti-Cheat~~ (-> stattdessen: Client-side Determinismus via FixedTimestep, Punkt 12)

---

## Priorisierte Roadmap

### Sprint 1 (1 Woche)
- Welt-Themed Bomb-FX (#6)
- Ultra-Combo-Flash (#7)
- Adaptive-Icon-Layers (#8)
- Splash-Version-Auto + Empty-State-Polish (Backlog)

### Sprint 2 (2 Wochen)
- Firebase Remote Config (#1)
- Funnel-Event-Telemetrie (#2)
- Re-Engagement-Push (#3)

### Sprint 3 (1-2 Wochen)
- Bottom-Tab-Konsolidierung (#4)
- Tutorial-Erweiterung (#5)
- Player i-Frame + Anticipation-Frames (#18, #19)

### Sprint 4 (2 Wochen)
- ILogger-Migration (#9)
- MainViewModel-Reduktion (#10)
- "What's New"-Modal (#17)
- Feature-Unlock-Choreographie (#20)

### Sprint 5-7 (4-6 Wochen)
- GameEngine Mode-Plugin-Migration (#11)
- FixedTimestep + DeterministicRandom (#12)
- Adaptive Music-Engine (#13)
- Outline-Pass (#14)

### Sprint 8-10 (6-8 Wochen)
- Mehr Bosse/Enemies (#15)
- Mini-Story-Beats (#16)
- Splash-Crash-Recovery (#25)

### Sprint 11+ (3-6 Monate)
- Hero/Character-System (#21)
- 2P-Local-Co-Op (#22)
- Clan-System (#23)
- Wochen-Content-Pipeline (#24)

---

## Erwartete Bewertung nach voller Roadmap-Umsetzung

| Achse | Heute | Nach Tier 1 | Nach Tier 2 | Nach Tier 3 |
|-------|-------|-------------|-------------|-------------|
| Engine-Architektur | 8.5 | 8.5 | 9.5 | 9.5 |
| Performance | 9 | 9 | 9.5 | 9.5 |
| Game Feel / Juice | 8.5 | 9 | 9.5 | 9.5 |
| Audio (technisch) | 6 | 6 | 7.5 | 7.5 |
| Visuals (Engine) | 6 | 6.5 | 7.5 | 8 |
| Content-Volumen | 5 | 5 | 6.5 | 8 |
| Live-Ops | 6 | 9 | 9 | 9.5 |
| UX/UI | 6 | 8 | 8.5 | 9 |
| Architektur-Hygiene | 8 | 8.5 | 9 | 9 |
| **Gesamt** | **7.0** | **7.7** | **8.4** | **8.8** |

**Ehrliche Decke ohne externes Budget: 8.5-8.8/10.** Das ist eine Stufe ueber "Premium Indie" und in Sichtweite von AAA-Mobile-Mid-Tier. Den Sprung auf 9.5+ (Brawl-Stars-Niveau) gibt es nicht ohne Audio/Art-Investment — und das ist auf deinem Wunsch ausgeklammert.

---

## Schluss-Empfehlung

**Starte mit Sprint 1 sofort.** Die 4 Punkte sind alle <1 Tag und liefern sichtbare Verbesserung in der naechsten Production-Version. Danach Sprint 2 (Remote Config + Telemetry) — das gibt dir Datenbasis, um Sprint 3 (Tab-Konsolidierung) datengestuetzt zu entscheiden statt aus dem Bauch.

Nach 4 Wochen Tier-1-Arbeit hast du das Spiel von 7.0 auf 7.7 geschoben — das ist die Stufe, die viele bezahlte Studios nie erreichen. Tier 2 + Tier 3 baust du dann mit ruhiger Hand ueber die naechsten 6-12 Monate.
