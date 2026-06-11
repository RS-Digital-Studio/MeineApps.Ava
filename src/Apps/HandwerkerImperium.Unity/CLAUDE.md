# CLAUDE.md — HandwerkerImperium.Unity

Unity-6-Neuentwicklung von HandwerkerImperium, **parallel** zur produktiven Avalonia-Version
(`../HandwerkerImperium/`). Diese Datei enthält **nur Unity-spezifische Conventions** dieses
Projekts. Generische Arbeitsweise → globale CLAUDE.md. Avalonia-Architektur/-Conventions →
Root-`CLAUDE.md` (gelten hier **nicht** — Unity hat einen eigenen Stack).

> **NEUAUSRICHTUNG (8.6.2026) — verbindliche Spiel-Design-Quelle ist jetzt [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md).**
> Die Unity-Version wird **voll neu als 3D-Walk-around-Idle-Tycoon** (Stil: My Perfect Hotel / My Mini Mart /
> Idle Office Tycoon) konzipiert — **Mechanik darf bewusst vom Avalonia-Original abweichen.** Die frühere
> „dasselbe Spiel, nur 3D-Präsentation"-Doktrin ist **abgelöst** (siehe §1). Gleiches Thema (Handwerk) + Personal
> (Meister Hans), aber genre-typische Schleife: Avatar läuft, sammelt Cash, stellt Arbeiter an, baut Werkstätten
> aus, saniert die Stadt. **Tech-Stack & Unity-Conventions in dieser Datei bleiben unverändert gültig.** Der
> alte 1:1-Domain-Port wurde **auf Nutzer-Entscheidung vollständig entfernt** (Clean-Slate) — `Domain/`
> enthält jetzt **ausschließlich** den neuen 3D-Idle-Core. Reaktivierung des Alt-Ports über git-Tag
> `hwi-unity-domain-port-pre-cleanslate`.
>
> **Stand:** Pre-MVP. **P0 komplett + headless verifiziert.** Der **gesamte Unity-freie Domain-Logik-Layer für
> P1–P4 ist gebaut + getestet** (clean-slate, kein Alt-Port-Rest). Alt-Port (Schicht 1-16 + ~143 Alt-Tests +
> 10 `*Formulas.cs`) ist gelöscht, via git-Tag `hwi-unity-domain-port-pre-cleanslate` reaktivierbar.
>
> **`Domain/`-Namespaces (alle pur, Unity-frei, NUnit-getestet):**
> - `Idle` (P0-Core: IdleBalancing, GreyboxSimState, IdleEconomyFormulas, GreyboxSimulation) · `Offline` (pure Staffel 0.80/0.35/0.15/0.05)
> - `Economy/IncomeSoftCap` (Log2 `T+log2(1+(M-T))`, validiert geborgen) · `Orders/OrderQueueFormulas` (Kunden-Queue + Eil-Auftrag)
> - `Restoration` (Wahrzeichen-Bauphasen) · `Progression` (Prestige `floor(sqrt(money/100k))`+×3/×12/×60, Mastery `1.15^N`, Meistergrad `1.5^R`, Perkboard, MasterTools 12/+74%)
> - `StarRating` (1-5★ Aggregat + Hysterese) · `Franchise/WorldTier` (4 Städte) · `MiniGames` (Tap-Boosts) · `Cosmetics`
> - `LiveOps` (DailyReward `GetScaledMoney`, DailyTask, Seasonal, RushEvent, RemoteConfig A/B, WhatsNew) · `Achievements`
> - `Social` (Referral, Leaderboard-HMAC, CrossPromo) · `Notifications` (Scheduling) · `Analytics` (Taxonomie) · `Common/StableHash` (FNV-1a)
> - `Save` (GameSave-Slices + **HMAC über ALLE economy-kritischen Felder** + Migrator + Sanitizer + CloudSave) · `Monetization` (Ads/Premium + Avalonia-Migration) · `Story` (Hans-Beats)
> - `Config/GameBalancing` (zentrale Tuning-Single-Source) · `Runtime` (**GameModel** = Idle+Meta+Sub-States, **GameModelMapping** ↔ Save, **GameSimulation**-Orchestrator: Tick/EffektivEinkommen/Stern/Prestige/Offline, **MetaProgression**)
>
> **Runtime-Wiring (verdrahtet + verifiziert):** Der Domain-`GameSimulation`-Orchestrator komponiert alle Formel-Sätze
> über EINEM `GameModel`; der Game-Layer ist dünne Verdrahtung: `GameBalancingConfig` (SO → IdleBalancing+GameBalancing),
> `RuntimeSave` (PlayerPrefs+HMAC, reparieren statt wipen), `RuntimeGameController` (Szenen-Root: Load/Offline/Tick/Autosave),
> `RuntimeHud` (IMGUI-Diagnose), `RuntimeSceneBuilder` (Editor-Menü `HandwerkerImperium/Runtime/Build Runtime Scene` →
> spielbare `Runtime.unity`). Generierte `.unity`/`.asset` sind lokal (`*.meta`-Gitignore-Policy); der Builder ist versioniert.
>
> **Physischer 3D-Loop (gekoppelt, Play-Mode-verifiziert):** Der P0-Genre-Loop (Avatar läuft/sammelt/trägt,
> Stationen produzieren sichtbar, Tresen verkauft, Hold-to-Pay-Pads, Bauzaun) arbeitet im gekoppelten Modus des
> `GreyboxGameController` (runtime-Ref) direkt auf `GameModel.Idle` — eine Wahrheit, EIN Ticker (`GameSimulation.Tick`),
> EIN HMAC-Save; `CounterView`-Verkäufe bedienen die Runtime-Kunden-Queue (`NotifyPhysicalSale`), Prestige-Rebind inklusive.
> `GameSceneBuilder` (Menü `…/Build Game Scene (3D)`) baut `Game.unity` lokal: **alle 10 Gewerke-Stationen**
> (GDD §6.1, Start nur Schreinerei, Plot-Kosten-Progression 500→140k via `StationBalance.UnlockCost` +
> `GreyboxSimulation.UnlockCostFor`) im 2×5-Hof-Layout mit den echten Pipeline-Modellen je Gewerk, Bauzaun +
> Hold-to-Pay je gesperrtem Plot (Gebäude erscheint erst nach Unlock — `StationView.unlockedVisual`).
> **Welt-Lesbarkeit + Leben:** `MakeSign`/`BillboardLabel` (Holzbrett + built-in-Font-3D-Text, Breite gedeckelt,
> dreht zur Kamera) für Gewerk-Namen, Plot-Preise, Pad-Beschriftung; `ToonBob` animiert die (noch) ungeriggten
> Figuren prozedural (Lauf-Hüpfen/Idle-Atmen — Brücke bis zur Auto-Rig-Stufe). Kamera: Genre-Framing ~40°,
> FOV 45, Look-Ahead; Builder setzt Start-Position UND -Rotation (sonst schaut die Edit-Game-View horizontal).
> **Szene-Builder-Gotchas:** Materialien über das
> Default-Material der aktiven Pipeline erzeugen (`Shader.Find` → magenta im Editor-Batch) und für Prefabs als
> Asset persistieren (in-memory-Material überlebt `SaveAsPrefabAsset` nicht); `Application.runInBackground` setzt
> der Controller (Editor/Desktop tickt ohne Fokus, sonst steht der Play-Mode bei frameCount≈2).
>
> **Live-Ops im Runtime aktiv:** Über die Formel-Sätze hinaus sind die Live-Systeme im `RuntimeGameController`
> verdrahtet und periodisch ausgewertet: Master-Tool-Auto-Sammlung, Achievement-Gutschrift, Meister-Hans-Story-Beats,
> Endgame-Renommee-Akkumulation, Rush-Event (2×), Saison-Erkennung, Free-Cash-Pad (Monetarisierung), Tagesaufgaben
> (3/Tag → Gems, UTC-Reset, persistiert + HMAC-signiert). Der headless-baubare Logik-Layer ist damit komplett.
>
> **Welt-Bestand (Assets + Verdrahtung):** 33 Pipeline-GLBs in `Art/Models/` — 10 Gewerke-Gebäude, 10 Trag-Waren
> (1,2k Tris, stationsspezifischer Stapel + Carry via `AvatarController.stationWarePrefabs`), 3 Wahrzeichen-Paare
> (Ruine/saniert, `LandmarkCatalog` + `LandmarkView`-Hold-to-Pay-Sanierung mit sichtbarem Modell-Swap), Avatar +
> 3 Kunden + Worker. Details/Re-Import/Sichtungs-Lehren → `Assets/_Project/Art/Models/README.md`.
>
> **Verifikation:** netstandard2.1/C#9-Compat-Compile (0 Fehler/0 Warnungen) + echtes Unity 6000.4.8f1 **173 NUnit /
> 0 Fehler** (159 Domain + 14 Game, via unity-mcp-Reflection-Runner). Zusätzlich **2 adversariale Mehr-Agenten-Reviews**
> gegen die .md-Specs → alle gefundenen Bugs (Anti-Cheat-Lücken, Determinismus, Overflow, Off-by-one) fix-forward behoben.
> **HMAC-Mapping (CLAUDE.md §7-Tuple → neue Slices):** `Gems` ≙ GoldenScrews, kein `PlayerLevel` (stattdessen `Mastery.Level`
> signiert) — bewusste Schema-Neuausrichtung.
>
> **Offen (externe/gated Schichten):** Premium-UI (UI Toolkit) statt Diagnose-HUD, volle 3D-Scene (Avatar/Stationen/
> Cinematics) + 3D-Assets (ComfyUI), Firebase-Backend/Push/Ad-IAP-SDK, Beta/Store/KPIs/Performance/Cutover. **Spiel-Design** folgt
> dem GDD ([3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)); ARCHITECTURE/DESIGN/ROADMAP-Mechanik = Referenz,
> Infra/Tech (Scenes, Save, Netz, Pipeline) = Soll.

---

## 1. Grundsatz (unverhandelbar)

Die Unity-Version ist ein **eigenständiges, genre-typisches Spiel**: ein **3D-Walk-around-Idle-Tycoon**
(Stil: My Perfect Hotel / My Mini Mart / Idle Office Tycoon). **Gleiches Thema** (Handwerk) und **Personal**
(Meister Hans) wie das Avalonia-Original — aber die **Spiel-Mechanik darf bewusst abweichen** (Avatar läuft &
sammelt, Arbeiter-Automatisierung, Plot-/Distrikt-Ausbau, Stadt-Wiederaufbau, ein Prestige = neue Stadt).

> Die alte Doktrin „dasselbe Spiel, jede Abweichung ist ein Bug" gilt **nicht mehr**. Verbindliche
> Spiel-Design-Quelle ist **[3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)**.

Werte-/Referenz-Quellen (Status unter der Neuausrichtung):
- [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md) — **verbindlicher GDD** (Loop, Systeme, Monetarisierung, Roadmap).
- [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) — Original-Werte/Formeln: **Referenz** für wiederverwendete Formeln (Income/Offline/Auto-Produktion); nicht mehr global verbindlich.
- [DESIGN.md](DESIGN.md) / [PLAN_ABGLEICH_ORIGINAL.md](PLAN_ABGLEICH_ORIGINAL.md) — Original-Sim als **Themen-/Reaktivierungs-Referenz**, nicht als Soll.

**Balancing-Disziplin bleibt:** wo der GDD Original-Formeln wiederverwendet, gelten deren Werte (nicht neu
erfinden); neue Genre-Mechaniken werden gegen Idle-Arcade-KPIs getunt (§14 GDD), nicht freihändig gesetzt.

Migration: Closed Beta unter eigener App-ID (`com.meineapps.handwerkerimperium2.beta`).
Avalonia-Original bleibt produktiv. Cutover erst nach erfolgreicher Beta.

---

## 2. Tech-Stack (Pflicht-Wahl)

| Komponente | Wahl | Warum diese und keine andere |
|------------|------|------------------------------|
| Unity | 6000.4.8f1 (LTS) | Gleiche Version wie ArcaneKingdom — Engine-Patches geteilt |
| C# | **C# 9 / netstandard2.1** (empirisch verifiziert, NICHT C# 12) | Unity 6000.4.8f1-Default. VERBOTEN: file-scoped Namespaces, Collection-Expressions `[…]`, `Random.Shared`, generisches `Enum.GetValues<T>()`, `init`/positional records (kein `IsExternalInit`), Range/Index `[..n]`. ERLAUBT: block-Namespaces, `new[]{}`, pattern matching, switch-expressions, `get;set;`. Details → Memory `[[unity-domain-port]]` |
| Scripting Backend | IL2CPP (Release), Mono (Editor) | AOT für Mobile |
| Render-Pipeline | URP 17.0.4 | 2D + 3D, Mobile-optimiert |
| DI | VContainer 1.16.9 | AOT-kompatibel mit IL2CPP (NICHT Zenject) |
| Async | UniTask 2.5.10 | GC-frei (NICHT `Task<T>`) |
| JSON | Newtonsoft.Json 3.2.2 | Kompatibel zum Avalonia-Save-Format |
| Lokalisierung | Unity Localization 1.5.11 | String-Tables (NICHT RESX) |
| Asset-Loading | Addressables 2.9.1 | NICHT `Resources.Load` (außer Bootstrap) |
| 3D-Asset-Import | **glTFast 6.12.0** (`com.unity.cloud.gltfast`, Apache-2.0) | GLB der ComfyUI-Pipeline → URP-Lit mit vollem PBR (Albedo+Metallic/Roughness) automatisch. NICHT FBX/OBJ (verlieren Metallic/Roughness). `Packages/manifest.json` ist git-ignored → Dependency hier dokumentiert. Asset-Quelle/Re-Import → `Assets/_Project/Art/Models/README.md` |
| Audio | Unity AudioMixer | plattformneutral (war Avalonia-Schmerzpunkt) |
| Animation | Animator + DOTween + Timeline | NICHT CSS-Hacks (war Avalonia-Schmerzpunkt) |
| Camera | Cinemachine 3.x | Orbit + Pan + Impulse-Shake (Unity-6-Default `com.unity.cinemachine`, API-inkompatibel zu 2.10) |
| UI | UI Toolkit (statische Screens) + uGUI (animierte) | gemischt nach Bedarf |
| Text | TextMesh Pro | Typografie/Rich-Text (NICHT uGUI Text) |
| Input | New Input System 1.19.0 | Touch-Gesten nativ (NICHT Legacy Input) |
| Test | Unity Test Framework 1.5.1 (NUnit) | Domain → EditMode, Game → PlayMode |

**Verboten (jeweils mit Pflicht-Alternative):**
- `Task.Run` für Game-Logik → `UniTask.RunOnThreadPool`
- `MonoBehaviour` im Domain-Layer → Domain bleibt Unity-frei
- `GameObject.Find` / `FindObjectOfType` → DI via VContainer
- Statische Singletons (`Xxx.Instance`) → DI-Singleton
- `Resources.Load` außerhalb Bootstrap → Addressables
- `Coroutine`/`WaitForSeconds` für Spiellogik → UniTask + GameClock
- `string`-Asset-Pfade → `AssetReference` / Typed-Reference
- Hardcoded Spiel-Werte → `BalancingConfig` (ScriptableObject)
- `DateTime.Now` → `DateTime.UtcNow` (Timezone-Bugs)

---

## 3. Assembly-Hierarchie (asmdef — Reihenfolge ist Pflicht)

Sieben Assemblies, keine zirkulären Refs (Vorbild: ArcaneKingdom `Unity/Assets/_Project/Scripts/`):

```
Core
 └── Domain   (KEIN UnityEngine, KEIN Game/UI — pure C#, NUnit-testbar)
      └── Game (UnityEngine + Domain)
           └── UI (Game + Domain)
                └── Bootstrap (UI + Game + Domain)

Editor: standalone, refs Domain + Game (Editor-only)
Tests:  refs Domain (NUnit, EditMode)
```

Geplanter Pfad: `Unity/Assets/_Project/Scripts/{Bootstrap,Core,Domain,Game,UI,Editor,Tests}/`,
asmdef-Name `HandwerkerImperium.{Layer}` (Tests: `HandwerkerImperium.Domain.Tests`).
Resources **nur** für Bootstrap-Scene. Vollständige Ordner-/Scene-Struktur → [ARCHITECTURE.md](ARCHITECTURE.md).

**Coverage-Ziele:** Domain ≥ 80 % (Pflicht je neuer Domain-Klasse), Game ≥ 50 %, UI optional.

---

## 4. Namespaces & Code-Style (Unity-spezifisch)

**Pattern:** `HandwerkerImperium.{Layer}.{Feature}` (kein `HWI`-Prefix). Beispiele:
`HandwerkerImperium.Domain.Workshops`, `HandwerkerImperium.Game.Services`,
`HandwerkerImperium.UI.Screens`, `HandwerkerImperium.Bootstrap`. **Block-Namespaces** (`namespace X { }`)
— file-scoped sind C# 10 und brechen in Unity (C# 9).

Erlaubte moderne C#-9-Features: Pattern Matching, switch-expressions, target-typed `new()`, relationale
Patterns. **NICHT verfügbar** (C# 10+/.NET 5+, brechen in Unity 6000.4.8f1): Collection-Expressions,
`init`/positional records, Required Members, file-scoped Namespaces. Stattdessen `new[]{}`/`new List<>{}`,
`get;set;` + Ctor, block-Namespaces. Vollständige Grenze + Verifikations-Harness → Memory `[[unity-domain-port]]`.

**Naming-Abweichungen ggü. Avalonia** (Rest wie Root):
- `View` = UI-Toolkit-Screen, `Panel` = uGUI-Screen, `Behaviour`/`Component` = MonoBehaviour
- `Controller` = Feature-Orchestrator (Scene-Scope), `Definition`/`Config` = ScriptableObject
- Konstanten `UPPER_SNAKE`, private Felder `_camelCase`

**Sprache:** Kommentare/Domain-UI-Begriffe Deutsch (echte Umlaute, UTF-8). Identifier + **Logging**
Englisch (Crashlytics-Lesbarkeit). Lokalisierungs-Keys Englisch, Values je Sprache.

**Async-Konvention:** `UniTask` statt `Task`, Suffix `Async`, `CancellationToken` als **letzter**
Parameter, `Result<T>` statt Exceptions in der Game-Loop.

---

## 5. DI (VContainer)

Constructor Injection per Primary Constructor. Verboten: `ServiceLocator.Resolve<T>()`,
`Container.Resolve<T>()` (beides nur in Bootstrap), statische `Instance`-Properties,
Property Injection (außer optionale Deps). MonoBehaviours können nicht via Ctor injiziert
werden → `[Inject]`-Field oder Method-Injection.

**Lifetimes:** Domain-Calculators / Game-Services / Platform-Services / Coordinators → Singleton.
Transient-Modal-ViewModels → Transient. Scene-Controller → Scoped (Scene-Scope).

**Container-Facades gegen Service-Sprawl** (Original hat ~91 Services): Aus Avalonia übernommen
sind `IGuildFacade` (9 Gilden-Services) und `IMissionsFacade` (5 Services). NEUE
Unity-Strukturentscheidungen sind `IWorkerFacade` und `IProgressionFacade` (Prestige/Rebirth/
Ascension/EternalMastery) — im Avalonia-Original existieren diese beiden nicht. Bündeln
zusammengehörige Services hinter einem Interface.

---

## 6. MVVM-Light (Unity-Variante)

Avalonia-MVVM gilt hier **nicht** — Unity nutzt ein eigenes, Unity-freies Light-Pattern:

```
View (UXML/Prefab) → ViewBinder (MonoBehaviour) → ViewModel (POCO, Unity-frei)
```

- **ViewModel:** Unity-frei, Constructor Injection, `ObservableProperty<T>` (eigenes Lib) statt
  `INotifyPropertyChanged`, Async-Commands via UniTask, NUnit-testbar ohne Editor.
- **ViewBinder:** MonoBehaviour, holt UI-Refs in `Awake()`, subscribt VM in `OnEnable()`,
  **unsubscribt in `OnDisable()`** (`CompositeDisposable`). Niemals Domain-Logik im Binder.

Konkrete Code-Skelette → [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 7. Spielmechanik-Regeln (kritisch, aus Avalonia übernommen)

- **Service-Caches resetten:** Jeder Service mit Cache **muss** sich auf `StateLoadedEvent`
  **und** `PrestigeCompletedEvent` subscriben und dort die Caches leeren (Avalonia-Gotcha #1 —
  stale Werte nach Prestige/Load).
- **Save-Schema:** Single-Source-of-Truth `SaveMigrator.CurrentSchemaVersion`. Unter der Neuausrichtung
  **neues, schlankeres Schema** mit Genre-Slices (Town, Stations, Workers, Restoration, Franchise, Cosmetics,
  Economy, …) statt Avalonia-v7-1:1. **Migrierbarkeit + HMAC-Signatur-Pattern bleiben.** Slice-Definition →
  [3D_IDLE_GAME_PLAN.md §12](3D_IDLE_GAME_PLAN.md); Migrations-Infra-Mechanik → [ARCHITECTURE.md](ARCHITECTURE.md).
- **Anti-Cheat (HMAC):** Lokaler Haupt-Save → gerätegebundene Signatur über GameState-Kernwerte
  (`PlayerLevel|PrestigeCount|Money:F2|GoldenScrews|TotalOrders`), Verifikation lokal via
  `FixedTimeEquals`. Bei ungültiger Signatur **reparieren statt ablehnen** (`SanitizeState`
  klemmt auf Caps, kein Wipe). Echte **Ablehnung** nur server-seitig für Online-Werte
  (Co-op-Score, Auktions-Gebot, Boss-Damage, Mega-Projekt) per atomarem Firebase-PATCH +
  `validate`-Rules. Details → ARCHITECTURE § 16.
- **Firebase-Pfade 1:1 wie Avalonia.** Neue Pfade: Eintrag in `Server/DatabaseRules/
  database.rules.json`, `.indexOn` für `orderBy`-Queries, mit Stubs testen.
- **PlayerId:** stabile UUID (NICHT Firebase-UID), beim ersten Login generiert, dauerhaft an
  Google-Account gebunden.
- **DateTime:** Persistenz `DateTime.UtcNow.ToString("O")`, Parse mit
  `DateTimeStyles.RoundtripKind`, Server-Timestamp via Firebase `{".sv":"timestamp"}`.

---

## 8. Unity-Gotchas (Tech-spezifisch)

| Problem | Lösung |
|---------|--------|
| MonoBehaviour nicht ctor-injizierbar | `[Inject]`-Field oder Method-Injection |
| `Awake()` vs `OnEnable()` Race | DI komplett in `Awake`, UI-Subs in `OnEnable` |
| `OnDestroy()` bei App-Quit nicht garantiert | Save in `OnApplicationPause(true)` |
| Addressables-Memory-Leak | nach Scene-Unload immer `Addressables.Release(handle)` |
| IL2CPP-Stripping entfernt Reflection | `[Preserve]` bzw. `link.xml` |
| VContainer + IL2CPP Generics | `RuntimeInitializeOnLoadMethod`-Pre-Reservation |
| Cinemachine-Shake stoppt nicht | Impulse-Source-Cleanup |
| DOTween auf zerstörtem GameObject | `tween.SetLink(gameObject)` |
| TextMeshPro CJK-Font fehlt | Dynamic-SDF-Font / Font-Asset je Sprache |
| Android Back-Button | `KeyCode.Escape` → Double-Back-to-Exit |
| Notch / Safe-Area | `Screen.safeArea` lesen, Layout anpassen |
| AAB > 150 MB | Addressables-Remote-Catalog (Phase 2), Texture-Compression |

Mono-JIT-Assertion-Crash (Avalonia-Android-Thema) betrifft IL2CPP-Builds **nicht** — Mono ist
hier Editor-only. UI-Animationen: DOTween statt Coroutine; kein `Time.deltaTime` (DOTween-eigenes
Time-Scaling); kein hardcoded `Animator.Play("State")` → typsicheres Wrapper.

---

## 9. Git & Build

- **Branches:** `unity-main` (Hauptbranch, parallel zum produktiven `master`),
  `unity-feature/xxx`, `unity-bugfix/xxx`. `master` bleibt Avalonia-produktiv.
- **Commit-Prefix:** `Unity-HWI:` (unterscheidet Unity- von Avalonia-Commits), Deutsch.
- **Build:** über Unity-Editor / Cloud Build (NICHT `dotnet build` — Unity ist nicht in der
  `.sln`). Geplante Editor-Menüs (Setup-Wizard, DataImporter, BalancingDashboard, SaveEditor,
  Cheats nur `DEV_BUILD`, BuildScripts) → [ARCHITECTURE.md](ARCHITECTURE.md) / [SETUP.md](SETUP.md).

---

## 10. Plandokumente

| Datei | Inhalt | Status (Neuausrichtung) |
|-------|--------|-------------------------|
| **[3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)** | **Verbindlicher GDD** der 3D-Idle-Neuausrichtung (Loop, Systeme, Story, Monetarisierung, Roadmap) | **SOLL — Spiel-Design** |
| **[PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)** | Langzeit-Progression & Balancing: **max. 3 Prestige**, Meisterschaft, Endgame-Meistergrade, Monate-Pacing | **SOLL — Progression** |
| **[P0_GREYBOX_PROTOTYP.md](P0_GREYBOX_PROTOTYP.md)** | Buildbare Spec des ersten Fun-Check-Prototyps (Go/No-Go) | **SOLL — nächster Schritt** |
| [P1_VERTICAL_SLICE.md](P1_VERTICAL_SLICE.md) | Vertical Slice: 1 volle Stadt bis erstes Prestige + Kern-Monetarisierung | SOLL — Phase P1 |
| [P2_CONTENT.md](P2_CONTENT.md) | Content: 4 Städte, Sanierung, Master-Tools, Meisterschaft/Perkboard, Cosmetics, Live-Ops, 6 Sprachen | SOLL — Phase P2 |
| [P3_SOCIAL_BETA.md](P3_SOCIAL_BETA.md) | Telemetrie, Remote-Live-Ops, Push, Leaderboards, Closed Beta | SOLL — Phase P3 |
| [P4_POLISH_CUTOVER.md](P4_POLISH_CUTOVER.md) | KPI-Balancing, Performance, Store, Cutover-Entscheidungsrahmen | SOLL — Phase P4 |
| [PLAN.md](PLAN.md) | Vision, Strategie, MVP-Definition (Original-1:1-Richtung) | Reframt — Banner verweist auf GDD |
| [DESIGN.md](DESIGN.md) | Original-Sim-GDD (alle Mechaniken/Werte) | Referenz/Reaktivierung, nicht Soll |
| [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) | Echte Werte/Formeln aus dem Avalonia-Code | Referenz für wiederverwendete Formeln |
| [PLAN_ABGLEICH_ORIGINAL.md](PLAN_ABGLEICH_ORIGINAL.md) | System-für-System-Soll-Ist-Abgleich | Referenz, nicht Soll |
| [DOMAIN_3D_PLAN.md](DOMAIN_3D_PLAN.md) | Domain-Port-Roadmap + alter Mechanik-1:1-3D-Plan | Port-Teil gültig, Mechanik-1:1-Teil abgelöst |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Tech-Details (Scenes, Save-Slices, Editor-Tools, Netz) | Infra=Soll; Mechanik-Bezüge=Referenz |
| [ROADMAP.md](ROADMAP.md) | Beta-gestufter Wochenplan | An GDD-Phasen (§14) anzugleichen |
| [ASSETS_AI.md](ASSETS_AI.md) | KI-Asset-Pipeline (3D-Meshes/PBR/Audio/Voice) | Gültig + neuer Bedarf (Avatar/NPC/Stadt, GDD §13) |
| [SETUP.md](SETUP.md) | Unity-/Firebase-/Pipeline-Setup | Gültig |
| [README.md](README.md) | Schnelleinstieg | Reframt auf 3D-Idle |

- Domain-Referenz: [Avalonia-Version](../HandwerkerImperium/CLAUDE.md)
- Unity-Architektur-Referenz (echter Code): [ArcaneKingdom](../ArcaneKingdom/CLAUDE.md)
