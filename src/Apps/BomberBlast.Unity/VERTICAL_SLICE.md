# BomberBlast 3D — Vertical-Slice: Sektor 1 + Granite Warden

> Erster spielbarer Durchstich (Phase 1). Ziel: **ein aktiv gespieltes Bomberman-Erlebnis in 3D** auf
> dem Gerät — Sektor 1 (L1–L10) mit dem **Granite Warden** als Sektor-Boss, deterministischer Sim,
> Placeholder-3D. Baut auf [SETUP.md](SETUP.md) (Phase 0) auf. Richtung → [PLAN.md](PLAN.md),
> Design → [DESIGN.md](DESIGN.md), Tech → [ARCHITECTURE.md](ARCHITECTURE.md), Reuse → [PARITY.md](PARITY.md).
> **Stand v0.5 — reiner Single-Player, kein Idle/AFK/Multiplayer.**

---

## 1. Ziel & Scope

**Slice-Ziel (Definition of Done):** Spieler startet die App → Sektor-1-Levelauswahl → spielt **L1**
aktiv (bewegen, Bomben legen, Blöcke sprengen, Gegner legen, PowerUps sammeln, Combos), schließt mit
**Sterne-Wertung** ab → progressiert bis **L10**, besiegt den **Granite Warden** → Victory-Cinematic →
verdiente **Coins** landen im Persistenz-Save. 60/30 FPS auf High-/Low-End, deterministische Sim.

### In Scope
- 15×10-Grid, Bomben-Lege-/Ketten-Logik, zerstörbare Blöcke, Exit.
- 4 PowerUps des frühen Spiels: **BombUp, Fire, Speed** (+ Discovery-Overlay).
- 2–3 Gegner-Typen aus Sektor 1: **Ballom, Onil** (+ optional Doll).
- **Granite Warden** (Boss-Archetyp StoneGolem): Telegraph→Attack→Cooldown, Enrage 50 %, Multi-Cell, BlockRegen.
- **Combo-System**, Score, Sterne (1–3), Lives, Timer, HUD.
- Input (Touch-Joystick + Bomb-Button), Cinemachine-Top-Down, Placeholder-3D (Primitives + URP-Materialien).
- Deterministische Sim (`IRngProvider` + `FixedTimestepRunner`), `ReplayCapture`-Hook, Coins-Persistenz.

### Out of Scope (später)
Shop/Upgrades, Karten/Deck, Helden-Auswahl, weitere Sektoren/Gegner/Bosse, Master-Mode/Reborn,
Anomaly-Dives, Grid-Rankings, Battle-Pass, Cloud-Save, finale 3D-Art/VFX, Audio-Politur.
**Niemals:** Multiplayer, Idle/AFK, Offline-Income.

---

## 2. Architektur des Slice: Sim ⟂ View

| Schicht | Asmdef | Verantwortung |
|---------|--------|---------------|
| **Sim** (Unity-frei) | `BomberBlast.Domain` | Spielzustand + Regeln, 60 Hz Fixed-Step, deterministisch, **testbar ohne Unity** |
| **Daten/Math** | `BomberBlast.Core` | Enums, Grid-Typen, `IRngProvider`, `DeterministicRandom`, `BalancingConfig`-DTOs |
| **View/Treiber** | `BomberBlast.Game` | MonoBehaviours: liest Sim-State, rendert Placeholder-3D, treibt den Sim-Tick |
| **UI** | `BomberBlast.UI` | HUD-Binder (Timer/Score/Combo/Lives), Levelauswahl, Result-Screen |
| **Composition** | `BomberBlast.Bootstrap` | VContainer: verdrahtet Sim + Services |

**Tick-Fluss (pro Fixed-Step, 60 Hz):**
```
GameLoopDriver (MonoBehaviour, FixedUpdate)
  → sammelt Input (InputService) → InputCommand
  → GameSimulation.Tick(dt_fixed, input)   // reine Domain-Logik
       ├─ Spieler bewegen (Pre-Turn-Buffering)
       ├─ Bomben-Timer, Ketten-Explosionen, Blöcke zerstören, Drops
       ├─ Gegner-AI (A*/BFS über IRngProvider)
       ├─ Boss-State-Machine (Granite Warden)
       ├─ Combo-Fenster, Score, Lives/Death
       └─ Win/Lose-Check
  → SimSnapshot (read-only) → ViewRenderer aktualisiert Transforms/VFX
  → ReplayCapture.RecordTick(input)   // Daily-Race/Replay-Verifikation
```
**Regeln:** Sim nutzt **nie** `Time.deltaTime`, `UnityEngine.Random` oder Transforms. View liest nur,
schreibt nie in den Sim-State. (CLAUDE.md Anti-Patterns.)

---

## 3. Domain-Code-Port (PORT-1:1 zuerst — risikoarm)

> Reihenfolge aus [PARITY.md §1](PARITY.md). Diese sind reines C# aus dem Original → kopieren + Namespace +
> Tests. Kein Unity-API.

| # | Datei (Original) | Ziel | Notiz |
|---|------------------|------|-------|
| 1 | `DeterministicRandom` (xoshiro256+) | `Core` | bit-stabil; `GetState/SetState` für Replay |
| 2 | `IRngProvider` (+ Sim-/Visual-Impl) | `Core` | Sim-RNG vs. `[Key("visual")]`-RNG |
| 3 | `ReplayCapture` | `Core` | 1 Byte/Tick, Schema-V1 |
| 4 | `FixedTimestepRunner` | `Core` | 60-Hz-Akkumulator (Referenz; echte Integration = Slice-Arbeit) |
| 5 | `GameStateSnapshot` (FNV-1a-Hash) | `Core` | Replay-Hash für Determinismus-Test |
| 6 | `CellType`, `Direction`, `EnemyType`, `PowerUpType` (Enums) | `Core` | Werte/Reihenfolge exakt halten |
| 7 | `GameGrid` (15×10) | `Domain` | Zell-Zugriff, Block/Wall/Exit |
| 8 | `ComboSystem` | `Domain` | 2-s-Fenster, ×2…×10+, Boni |
| 9 | `LevelLayoutGenerator` (12 Layouts) | `Domain` | Pure prozedural, seed-getrieben (xoshiro!) |
| 10 | `AStar` + `EnemyAI` (Ballom/Onil) | `Domain` | Pathfinding über `IRngProvider` |
| 11 | Boss-Logik aus `GameEngine.Level` (Granite Warden) | `Domain` | extrahieren als `BossController` |

> **Wichtig:** Beim Port alle Gameplay-`System.Random`/`UnityEngine.Random` → `IRngProvider` umstellen
> (Determinismus-Integration, [PARITY §7](PARITY.md)). Das ist Neu-Arbeit, kein reiner Copy-Paste.

---

## 4. Neue Slice-Klassen (je Asmdef)

**Core**
- `BalancingConfig` (POCO-Mirror des ScriptableObject), `SektorDef`, `LevelDef`, `EnemySpawn`.

**Domain (Sim)**
- `GameSimulation` — Aggregat-Root: hält `GameGrid`, `PlayerState`, `List<BombState>`, `List<EnemyState>`,
  `BossState?`, `ComboSystem`, `ScoreState`. Methode `Tick(Fixed dt, InputCommand input)`.
- `PlayerState` (Pos, Dir, MaxBombs, FireRange, SpeedLevel, Lives, Buffs, i-Frames), `BombState`,
  `EnemyState`, `BossState` (Phase, HP, AttackTimer, Cells), `ExplosionResolver` (Ketten, iterativ max 5).
- `InputCommand` (record: MoveDir, PlaceBomb, Detonate).
- `SimSnapshot` (read-only struct/record für die View).
- `BossController` (Granite Warden State-Machine).

**Game (View/Treiber)**
- `GameLoopDriver` (MonoBehaviour, FixedUpdate → Sim.Tick; Update → View-Interpolation).
- `BoardRenderer` (instanziert Tile-/Block-Meshes, Pooling), `EntityViewPool` (Bomber/Bomb/Enemy/Boss-Prefabs),
  `ExplosionVfx` (Placeholder-Partikel), `CameraRig` (Cinemachine Top-Down + Impulse für Shake).
- `InputService` (Input System → `InputCommand`; Touch-Joystick + Buttons).

**UI**
- `BattleHudViewModel` (POCO, R3-ReactiveProperties: Time/Score/Combo/Lives) + `BattleHudBinder`.
- `LevelSelectViewModel`/`Binder` (Sektor 1, L1–L10, Sterne), `ResultViewModel`/`Binder` (Sterne + Coins).

**Bootstrap**
- `RootLifetimeScope` (Core-Services), `GameLifetimeScope` (Scoped: `GameSimulation`, `GameLoopDriver`).

---

## 5. Granite Warden (Boss-Slice)

> Archetyp StoneGolem. Mechanik 1:1 aus Original, neu eingekleidet. Werte aus [Balancing-Workbook](prep/BalancingConfig.xlsx).

- **HP:** 4–6 (Slice: 5). **Multi-Cell:** belegt 2×2-BoundingBox (`OccupiesCell`).
- **Angriffs-Zyklus:** Telegraph (2 s) → Attack (1.5 s) → Cooldown (12–18 s). Bei **Enrage (≤50 % HP)**:
  Decision-Timer halbiert, Phase 1→2.
- **Kern-Angriff BlockRegen:** stellt periodisch zerstörte Blöcke in der Arena wieder her (Druck-Mechanik).
- **Anticipation:** letzte 120 ms vor Big-Attack Boss-Mesh auf 0.85× skalieren (Telegraph-Lesbarkeit).
- **Schaden am Boss:** nur durch eigene Explosionen auf belegten Zellen; i-Frame-Fenster nach Treffer.
- **Sieg:** HP=0 → Victory-Sequenz (Cinemachine-Zoom + Slow-Mo), Coins + 1–3 Sterne nach Restzeit/Combo.

---

## 6. Determinismus & Tests

- **Sim deterministisch:** alle Random über `IRngProvider`; Tick mit festem `dt`. Gleicher Seed + gleiche
  Input-Sequenz ⇒ identischer `GameStateSnapshot`-Hash.
- **CI-Gate (EditMode):** `DeterminismTest` — Replay-Corpus (aufgezeichnete L1-/L10-Runs) re-simulieren,
  End-Hash muss matchen. Failure blockt Merge.
- **Unit-Tests (Domain, ohne Unity):** `ComboSystem` (Fenster/Boni), `ExplosionResolver` (Ketten),
  `LevelLayoutGenerator` (seed-stabil), `EnemyAI` (Pfad-Determinismus), `BossController` (Phasen/Enrage),
  Score/Sterne-Berechnung.
- **PlayMode-Smoke:** Boot→Game lädt, 1 Tick läuft ohne Exception, HUD bindet.

---

## 7. Task-Backlog (umsetzbare Tickets)

**A — Fundament (nach Phase-0-Setup)**
1. Port `DeterministicRandom` + `IRngProvider` (+ Visual-RNG) → `Core` + Tests.
2. Port `ReplayCapture`, `FixedTimestepRunner`, `GameStateSnapshot` → `Core` + Tests.
3. Port Enums (`CellType/Direction/EnemyType/PowerUpType`) → `Core`.
4. `BalancingConfig`-ScriptableObject + Importer (liest `prep/seed/*.json`).

**B — Sim-Kern (Domain)**
5. `GameGrid` (15×10) + `GameSimulation`-Gerüst + `InputCommand`/`SimSnapshot`.
6. Spieler-Bewegung (Pre-Turn-Buffering, SpeedLevel) + Lives/Death/i-Frames.
7. Bomben legen + Timer + `ExplosionResolver` (Ketten, Blöcke zerstören, Exit).
8. PowerUp-Drops (BombUp/Fire/Speed) + Aufnahme-Effekte.
9. Port `LevelLayoutGenerator` + Sektor-1-Layout-Pool (seed-stabil).
10. Port `AStar` + `EnemyAI`; Ballom + Onil spawn/move/kill.
11. `ComboSystem` + Score + Sterne-Berechnung.
12. `BossController` Granite Warden (Telegraph/Attack/Cooldown/Enrage/BlockRegen/Multi-Cell).
13. Win/Lose-Logik + Level-Übergang L1→L10.

**C — View/Treiber (Game)**
14. `GameLoopDriver` (FixedUpdate→Tick; Render-Interpolation in Update).
15. `BoardRenderer` (Tile-/Block-Mesh-Pool) + `CameraRig` (Cinemachine Top-Down).
16. `EntityViewPool` (Bomber/Bomb/Enemy/Boss Placeholder-Prefabs) + `ExplosionVfx`.
17. `InputService` (Input System: Touch-Joystick + Bomb/Detonator) → `InputCommand`.
18. Game-Juice-Minimal: Hit-Pause, Screen-Shake (Impulse), Floating-Score-Text.

**D — UI**
19. `LevelSelect` (Sektor 1, L1–L10, Sterne-Anzeige, Gating).
20. `BattleHud` (Time/Score/Combo/Lives, NeonJoystick-Layout).
21. `Result`-Screen (Sterne, Coins, Retry/Next) + Coins-Persistenz (PlayerPrefs/JSON).
22. Discovery-Overlay (Erst-PowerUp), Victory-Sequenz (Warden).

**E — Determinismus & QA**
23. Determinismus-Replay-Corpus (L1 + L10) + `DeterminismTest` (CI-Gate).
24. Domain-Unit-Tests (Combo/Explosion/Layout/AI/Boss/Score).
25. Min-Spec-Performance-Pass (Galaxy A50): 30 FPS halten, Object-Pooling prüfen.
26. PlayMode-Smoke + Boot→Game→Result-Durchlauf auf Gerät.

---

## 8. Acceptance-Criteria (Slice „fertig")

- [ ] L1 aktiv spielbar: bewegen, Bombe legen, Block sprengen, Gegner legen, PowerUp aufnehmen, Combo auslösen.
- [ ] L1–L10 durchspielbar; L10 = Granite Warden besiegbar (mit Enrage-Phase).
- [ ] Sterne-Wertung + Coins werden vergeben und **persistiert** (Neustart behält Coins/Sterne).
- [ ] 60 FPS High-End, 30 FPS Galaxy-A50, keine GC-Spikes im Hot-Path.
- [ ] Determinismus-Replay-Test grün (CI).
- [ ] 0 Compiler-Warnungen; `Domain` ohne `UnityEngine`-Referenz (CI-Gate).
- [ ] Kein Multiplayer-/Idle-/Offline-Code im Slice.

---

## 9. Code-Skelette (Referenz)

**IRngProvider + Sim-Tick-Treiber:**
```csharp
// Core (Unity-frei)
public interface IRngProvider { int NextInt(int min, int max); float NextFloat(); 
    ulong GetState(); void SetState(ulong s); }

// Game (Treiber) — Sim-Tick im FixedUpdate, deterministisch
public class GameLoopDriver : MonoBehaviour
{
    private GameSimulation _sim; private IInputService _input; private IViewRenderer _view;
    [Inject] public void Construct(GameSimulation sim, IInputService input, IViewRenderer view)
        { _sim = sim; _input = input; _view = view; }

    private void FixedUpdate()
    {
        var cmd = _input.PollCommand();             // InputCommand (MoveDir, PlaceBomb, Detonate)
        _sim.Tick(FixedDt.Step, cmd);               // reine Domain-Logik, 1/60 s
    }
    private void Update() => _view.Render(_sim.Snapshot()); // read-only Interpolation
}
```

**Sim-Aggregat (Domain, testbar ohne Unity):**
```csharp
public sealed class GameSimulation
{
    private readonly IRngProvider _rng; private readonly BalancingConfig _cfg;
    public GameGrid Grid { get; } public PlayerState Player { get; } /* … */
    public GameSimulation(IRngProvider rng, BalancingConfig cfg, LevelDef level) { /* init via rng */ }

    public void Tick(Fixed dt, InputCommand input)
    {
        MovePlayer(dt, input);            // Pre-Turn-Buffering
        UpdateBombs(dt);                  // Timer → ExplosionResolver (Ketten, Blöcke, Drops)
        UpdateEnemies(dt);                // A*/BFS über _rng
        _boss?.Tick(dt, this);            // Granite Warden State-Machine
        _combo.Tick(dt);                  // 2-s-Fenster
        CheckWinLose();
    }
    public SimSnapshot Snapshot() => /* read-only Kopie für die View */;
}
```

**HUD-Binder (View ↔ ViewModel):**
```csharp
public class BattleHudBinder : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _score, _combo, _time;
    [Inject] private BattleHudViewModel _vm;
    private void Start() {
        _vm.Score.Subscribe(v => _score.text = v.ToString("N0")).AddTo(this);
        _vm.Combo.Subscribe(c => _combo.text = c > 1 ? $"x{c}" : "").AddTo(this);
        _vm.TimeLeft.Subscribe(t => _time.text = $"{t:0}").AddTo(this);
    }
}
```

---

## Änderungslog

| Datum | Version | Änderung |
|-------|---------|----------|
| 2026-06-08 | v0.5 | Initialer Vertical-Slice-Plan (Sektor 1 + Granite Warden), Single-Player, deterministisch. |

> **Nächster Schritt nach Slice:** Phase 2 (Meta-Progression — Shop/Karten/Achievements/Cloud-Save) → [PLAN.md §11](PLAN.md).
