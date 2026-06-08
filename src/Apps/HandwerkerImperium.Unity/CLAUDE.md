# CLAUDE.md — HandwerkerImperium.Unity

Unity-6-Neuentwicklung von HandwerkerImperium, **parallel** zur produktiven Avalonia-Version
(`../HandwerkerImperium/`). Diese Datei enthält **nur Unity-spezifische Conventions** dieses
Projekts. Generische Arbeitsweise → globale CLAUDE.md. Avalonia-Architektur/-Conventions →
Root-`CLAUDE.md` (gelten hier **nicht** — Unity hat einen eigenen Stack).

> **Stand:** Pre-MVP. Der **komplette Domain-Layer (Schicht 1-16) ist 1:1 aus dem Avalonia-Original portiert**
> und liegt unter `Unity/Assets/_Project/Scripts/Domain/` (Economy, Orders, Crafting, Progression, Research,
> Reputation, Buildings, Guild, Events, LiveOps, Settings, Statistics, Boosts, Cosmetics, Onboarding,
> Notifications, Warehouse, **State/GameState** v7) + EditMode-Tests unter `…/Tests/Domain/`. Jede Schicht
> 3-fach verifiziert (netstandard2.1/C#9-Compat-Compile + Werte-Run gegen Original + ggf. Quelltext-Diff),
> der GameState-Root zusätzlich per **v7-JSON-Save-Roundtrip** (Newtonsoft). Roadmap + Hazards + 3D-Plan →
> [DOMAIN_3D_PLAN.md](DOMAIN_3D_PLAN.md). **Offen:** Service-Formel-Extrakte (`*Formulas.cs`), dann Game-/UI-/
> Bootstrap-Layer (3D-Präsentation). Bewusst in der Präsentations-/Netzwerk-Schicht: Firebase-DTOs, Guild-
> Display-DTOs, ContextualHint/FtueStep-Definitionen, alle Lokalisierungs-/Icon-/Farb-Anteile. Alles in
> ARCHITECTURE/DESIGN/ROADMAP über die Präsentation/Infra Beschriebene ist weiterhin **Soll**, nicht Ist.

---

## 1. Grundsatz (unverhandelbar)

Die Unity-Version ist **dasselbe Spiel** wie das produktive Avalonia-Original — gleiche
Mechaniken, Formeln, Balancing-Werte. "Besser/3D" betrifft **ausschließlich die Präsentation**
(Grafik, 3D, Hub, Cinematics, Audio, Input, UI-Tech). JEDE mechanische oder Balancing-Abweichung
ist ein Fehler.

Verbindliche Werte-Referenzen, im Zweifel gelten diese (niemals Werte erfinden):
- [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) — echte Werte/Formeln aus dem Avalonia-Code
- [DESIGN.md](DESIGN.md) — abgeglichenes GDD
- [PLAN_ABGLEICH_ORIGINAL.md](PLAN_ABGLEICH_ORIGINAL.md) — System-für-System-Soll-Ist-Abgleich

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
- **Save-Schema:** Single-Source-of-Truth `SaveMigrator.CurrentSchemaVersion` (geplant v8 =
  Avalonia-v7 + Unity-Slices). Save ist in modular migrierbare **Slices** unterteilt
  (GameState, Workshops, Workers, Orders, Research, Prestige, … + `UnitySpecificSlice`).
  Vollständige Slice-Liste & Persistenz-Trigger → [ARCHITECTURE.md](ARCHITECTURE.md).
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

| Datei | Inhalt |
|-------|--------|
| [PLAN.md](PLAN.md) | Vision, Strategie, MVP-Definition |
| [DESIGN.md](DESIGN.md) | Game Design Document (alle Mechaniken/Werte) |
| [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) | Echte Werte/Formeln aus dem Avalonia-Code |
| [PLAN_ABGLEICH_ORIGINAL.md](PLAN_ABGLEICH_ORIGINAL.md) | System-für-System-Soll-Ist-Abgleich |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Tech-Details (Scenes, Save-Slices, Editor-Tools, Netz) |
| [ROADMAP.md](ROADMAP.md) | Beta-gestufter Wochenplan |
| [ASSETS_AI.md](ASSETS_AI.md) | KI-Asset-Pipeline (3D-Meshes/PBR/Audio/Voice) |
| [SETUP.md](SETUP.md) | Unity-/Firebase-/Pipeline-Setup |
| [README.md](README.md) | Schnelleinstieg |

- Domain-Referenz: [Avalonia-Version](../HandwerkerImperium/CLAUDE.md)
- Unity-Architektur-Referenz (echter Code): [ArcaneKingdom](../ArcaneKingdom/CLAUDE.md)
