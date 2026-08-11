# BomberBlast 3D — Feel-Prototyp / Vertical-Slice: Sektor 1 + Granite Warden

> **Erster spielbarer Durchstich (Phase 1) — und ein hartes GATE.** Ziel: das **volumetrische 3D-Spielgefühl
> beweisen** — freie Bewegung, Physik-Bomben, volumetrische Explosionen, ebenenübergreifende Ketten und
> zerstörbare Arena in einem **vertikalen Sektor 1 (L1–L10)** mit dem **Granite Warden** als Boss. Macht es
> Spaß und ist es lesbar? → erst dann Content-Breite (Phase 2). Baut auf [SETUP.md](SETUP.md) (Phase 0) auf.
> Richtung → [PLAN.md](PLAN.md), Design → [DESIGN.md](DESIGN.md), Tech → [ARCHITECTURE.md](ARCHITECTURE.md),
> Reuse → [PARITY.md](PARITY.md).
> **Stand v0.6 — volumetrisches 3D-Action, reiner Single-Player, kein Idle/AFK/Multiplayer, kein flaches Grid.**

---

## 1. Ziel & Scope

**Slice-Ziel (Definition of Done):** Spieler startet die App → Sektor-1-Levelauswahl → spielt **L1** aktiv:
**bewegt sich frei** durch eine **mehrstöckige Arena** (laufen/dashen/springen/Kante), **legt/wirft/rollt
Physik-Bomben**, löst eine **volumetrische Explosion + 3D-Kettenreaktion** aus, **sprengt zerstörbare
Module** (inkl. Durchbruch in tiefere Ebene), sammelt PowerUps, baut Combos → schließt mit **Sterne-Wertung**
ab → progressiert bis **L10**, besiegt den **Granite Warden** in einer vertikalen Arena → Victory-Cinematic
→ verdiente **Coins** landen im Persistenz-Save. **60/30 FPS** auf High-/Low-End, **lesbar** auf Min-Spec.

> **Gate-Frage (entscheidet über Phase 2):** Fühlt sich das volumetrische Sprengen *besser* an als flaches
> Grid-Bomberman — und bleibt es auf dem Galaxy A50 **lesbar und performant**? Nur dann Content-Breite.

### In Scope
- **Mehrstöckige Arena** (2–3 Ebenen) mit Rampe/Lift, zerstörbaren Modulen, einem Void/Hazard-Rand.
- **Freie Bewegung:** Laufen, **Dash/Roll** (i-Frames), **Sprung/Doppelsprung**, Ledge-Grab; Fall zwischen Ebenen.
- **Physik-Bombe:** legen + **werfen/lobben** + **rollen**, fällt zwischen Ebenen; **eine** Blast-Form
  (**Sphere/Dome**) im Slice + **3D-Kettenreaktion**. Detonator-Button nur bei `HasDetonator`.
- 4 PowerUps des frühen Spiels: **BombUp, Fire, Speed, Detonator** (+ Discovery-Overlay).
- 2–3 Gegner-Typen aus Sektor 1: **Ballom, Onil** (+ optional Doll) auf **NavMesh** (inkl. Ebenenwechsel).
- **Granite Warden** (Boss-Archetyp StoneGolem) in 3D: Telegraph→Attack→Cooldown, Enrage 50 %, raumgreifend,
  BlockRegen → in 3D als **Modul-Regen / Arena-Wiederaufbau**.
- **Combo-System** (inkl. **Air-/Drop-Kill**-Bonus im Ansatz), Score, Sterne (1–3), Lives, Timer, HUD.
- **3D-Lesbarkeit (Pflicht):** Blast-Preview-Volumen, Through-Wall-Outlines, Höhen-/Schatten-Indikatoren.
- Input (Touch: Bewegung + Sprung/Dash + Bombe legen/werfen), dynamische Cinemachine-Kamera + Tilt-Fallback.
- **Basis-SFX:** Explosion, PowerUp-Pickup, Tod, Level-Win/Fail (Platzhalter-Sounds reichen).
- **Seed-deterministische Arena-Generierung** (`IRngProvider`), Coins-Persistenz.

### Out of Scope (später)
Alle weiteren Blast-Formen, Shop/Upgrades, Karten/Deck, Helden-Auswahl, weitere Sektoren/Gegner/Bosse,
Master-Mode/Reborn, Anomaly-Dives, Grid-Rankings, Battle-Pass, Cloud-Save, Style-Rang-Politur, finale
3D-Art/VFX, Audio-Politur, voll-zerstörbare Architektur (im Slice nur ausgewählte Module).
**Niemals:** Multiplayer, Idle/AFK, Offline-Income, flaches Grid als Sim-Kern, bit-exakter Replay-Resim.

---

## 2. Architektur des Slice: Sim ⟂ View

| Schicht | Asmdef | Verantwortung |
|---------|--------|---------------|
| **Sim** (Unity-frei wo möglich) | `BomberBlast.Domain` | Spielregeln, Bomben-/Ketten-/Combo-/Boss-Logik, Score; testbar |
| **Daten/Math** | `BomberBlast.Core` | Enums, `IRngProvider`, `DeterministicRandom`, `BalancingConfig`-DTOs |
| **View/Treiber/Physik** | `BomberBlast.Game` | MonoBehaviours: freie Bewegung, **Unity-Physics** (Bomben/Trümmer), NavMesh-AI, Rendering, Treiber |
| **UI** | `BomberBlast.UI` | HUD-Binder (Timer/Score/Combo/Lives), Levelauswahl, Result-Screen |
| **Composition** | `BomberBlast.Bootstrap` | VContainer: verdrahtet Sim + Services |

> **v0.6 — 2-Schichten-Modell ([GDD §12.3](3D_REINVENTION_PLAN.md)):** **Schicht A (autoritativ,
> deterministisch)** = Regel-/Zustandslogik auf abstrahierter **Occupancy-Repräsentation** (Bomben-Timer,
> Ketten-/Blast-Auflösung, Combo/Score, Boss-Phasen, Win/Lose, Drops) in `Domain`, fixed-step über
> `FixedTimestepRunner`, `IRngProvider`, **Fixed-Point** → **State-Hash reproduzierbar** (Replay-Verifikation).
> **Schicht B (kosmetisch, nicht-autoritativ)** = PhysX (Bomben-/Trümmer-Physik, freie Bewegung visuell,
> Partikel) in `Game` — beeinflusst Schicht A **nie**, darf non-deterministisch sein. Im Slice schon **sauber
> getrennt aufsetzen** — das ist die wichtigste Architektur-Entscheidung.

**Tick-Fluss (pro Frame):**
```
GameLoopDriver (MonoBehaviour, Update)
  → FixedTimestepRunner @ 60 Hz (Akkumulator, Clamp 5 Steps/Frame)
      → sammelt Input (InputService) → InputCommand (Move, Jump, Dash, PlaceBomb, ThrowBomb, Detonate)
      → GameSimulation.Tick(dt, input):
           ├─ Spieler-Intent (Bewegung/Jump/Dash an PlayerController/Physics weitergeben)
           ├─ Bomben-Timer, Ketten-Explosionen (3D-Radius/-Volumen, ebenenübergreifend), Drops
           ├─ Modul-Zerstörung anstoßen (Game zerlegt Chunk + Trümmer-Physik)
           ├─ Gegner-AI-Entscheidung (Ziel/Flee) → NavMeshAgent im Game bewegt
           ├─ Boss-State-Machine (Granite Warden)
           ├─ Combo-Fenster (+ Air-/Drop-Kill-Klassifikation), Score, Lives/Death
           └─ Win/Lose-Check
  → PhysicsStep (Unity, fester Substep) für Bomben/Trümmer/Bewegung
  → ViewRenderer/Outlines/Preview aktualisieren
  → ReplayCapture.RecordTick(input)   // Schicht-A-Inputs → Replay-Hash-Verifikation (Determinismus-Test)
```
**Regeln:** Generierungs-Random **immer** über `IRngProvider` (seed-reproduzierbar). Visual-Random
(`[Key("visual")]`) für Partikel/Shake. Regel-Logik nutzt **nie** `UnityEngine.Random`.

---

## 3. Domain-/Fundament-Port (zuerst — risikoarm)

> Reihenfolge aus [PARITY.md §1](PARITY.md). Reines C# aus dem Original → kopieren + Namespace + Tests.

| # | Datei (Original) | Ziel | Notiz |
|---|------------------|------|-------|
| 1 | `DeterministicRandom` (xoshiro256+) | `Core` | seed-stabil; `GetState/SetState` für reproduzierbare Generierung |
| 2 | `IRngProvider` (+ Sim-/Visual-Impl) | `Core` | Generierungs-RNG vs. `[Key("visual")]`-RNG |
| 3 | `FixedTimestepRunner` | `Core` | 60-Hz-Treiber der **autoritativen Schicht A** (Akkumulator, Clamp 5/Frame); Schicht A rechnet Fixed-Point auf Occupancy |
| 3b | `ReplayCapture` + `GameStateSnapshot` (FNV-1a) | `Core` | Replay-Hash **über Schicht A** (Determinismus-Test); PhysX/Debris nicht im Hash |
| 4 | `ComboSystem` | `Domain` | 2-s-Fenster, ×2…×10+ — **erweitern** um Air-/Drop-Kill-Klassifikation |
| 5 | `CellType`/`Direction`/`EnemyType`/`PowerUpType` (Enums) | `Core` | als Hazard-/Typ-Vokabular; Werte erhalten |

> **NEU (Kern der Phase 1, kein Port):** freie Bewegung, Physik-Bomben, volumetrische Blast-Auflösung,
> 3D-Arena-Generator, Chunk-Destruktion, NavMesh-AI, Boss in 3D. Siehe §4/§5.

---

## 4. Neue Slice-Klassen (je Asmdef)

**Core**
- `BalancingConfig` (POCO-Mirror des ScriptableObject), `SektorDef`, `LevelDef`, `ArenaSpec`, `EnemySpawn`,
  `BlastShape`-Enum (Slice: nur `SphereDome`).

**Domain (Regel-/Zustandslogik, testbar)**
- `GameSimulation` — Aggregat-Root: `PlayerState`, `List<BombState>`, `List<EnemyState>`, `BossState?`,
  `ComboSystem`, `ScoreState`, `ArenaState` (Module/Ebenen/Belegung). Methode `Tick(dt, InputCommand)`.
- `BombResolver` — Timer + **3D-Ketten-Auflösung** (Radius/Volumen über Ebenen, iterativ mit Cap).
- `ComboSystem`-Erweiterung (Air-/Drop-Kill), `ScoreState`, Drop-Roll (`IRngProvider`).
- `InputCommand` (record: MoveDir, Jump, Dash, PlaceBomb, ThrowBomb, Detonate).
- `BossController` (Granite Warden State-Machine: Phasen/Enrage/Modul-Regen).

**Game (View/Treiber/Physik)**
- `GameLoopDriver` (MonoBehaviour, `Update` → `FixedTimestepRunner` → `Sim.Tick`; danach Physik/Interpolation).
- `PlayerController` (CharacterController/Rigidbody: Lauf/Dash/Sprung/Ledge, Coyote-Time-Buffer aus Original retten).
- `BombPhysics` (Rigidbody: legen/werfen/rollen/Fall), `BlastVfx` (volumetrisches Sphere/Dome-VFX + Preview).
- `ArenaBuilder` (3D-Arena-Generator aus `ArenaSpec`, seed-det.) + `DestructibleModule` (Chunk + Trümmer-Pool).
- `EnemyAgent` (NavMeshAgent + Off-Mesh-Links für Rampe/Lift/Fall), `CameraRig` (Cinemachine + Impulse + Tilt-Fallback).
- `InputService` (Input System → `InputCommand`; Touch: Stick + Jump/Dash/Bomb/Throw), `ReadabilityFx`
  (Through-Wall-Outline, Höhen-Schatten, Blast-Preview-Volumen).

**UI**
- `BattleHUDViewModel` (POCO, R3: Time/Score/Combo/Lives) + `BattleHUDBinder`.
- `LevelSelectViewModel`/`Binder` (Sektor 1, L1–L10, Sterne), `ResultViewModel`/`Binder` (Sterne + Coins).

**Bootstrap**
- `RootLifetimeScope` (Core-Services), `GameLifetimeScope` (Scoped: `GameSimulation`, `GameLoopDriver`).

---

## 5. Granite Warden (Boss-Slice, 3D)

> Archetyp StoneGolem. Mechanik-Anker aus dem Original, **neu in 3D inszeniert**. Werte aus
> [Balancing-Workbook](prep/BalancingConfig.xlsx), im Prototyp tunen.

- **HP:** 4–6 (Slice: 5). **Raumgreifend:** belegt mehrere Felder (3D-Bounding-Volumen).
- **Angriffs-Zyklus:** Telegraph (2 s, **volumetrischer Boden-/Raum-Marker**) → Attack (1.5 s) →
  Cooldown (12–18 s). Bei **Enrage (≤50 % HP)**: Decision-Timer halbiert, Phase 1→2.
- **Kern-Angriff (BlockRegen → 3D):** baut **zerstörte Arena-Module periodisch wieder auf** (Druck-Mechanik,
  nimmt dem Spieler Deckung/Wege).
- **Vertikalität:** nutzt 2 Ebenen (z.B. wirft Spieler per Schockwelle eine Ebene tiefer) — Lesbarkeits-Test.
- **Schaden am Boss:** nur durch eigene Explosionen im belegten Volumen; i-Frame-Fenster nach Treffer.
- **Sieg:** HP=0 → Victory-Sequenz (Cinemachine-Zoom + Slow-Mo), Coins + 1–3 Sterne nach Restzeit/Combo/Style.

---

## 6. Determinismus & Tests (v0.6)

- **2-Schichten ([GDD §12.3](3D_REINVENTION_PLAN.md)):** **Schicht A** (autoritative Regel-/Occupancy-Sim)
  ist deterministisch → **State-Hash reproduzierbar** (Replay-Verifikation); **Schicht B** (PhysX-Debris) ist
  kosmetisch und zählt **nicht** mit. Generierung seed-deterministisch (gleiche Seed ⇒ gleiche Arena).
- **CI-Gate (EditMode):** (1) **Replay-Determinismus über Schicht A** (Input-Replay → identischer State-Hash)
  + (2) **Seed-Reproduzierbarkeit** (`ArenaGenerator`/Drop-Roll bei festem Seed identisch). Failure blockt Merge.
- **Unit-Tests (Domain, ohne Unity):** `ComboSystem` (Fenster/Boni + Air-/Drop-Klassifikation),
  `BombResolver` (3D-Ketten-Logik gegen ein abstraktes Arena-Modell), Score/Sterne-Berechnung,
  `BossController` (Phasen/Enrage).
- **PlayMode-Smoke:** Boot→Game lädt, Spieler bewegt sich/legt Bombe, 1 Explosion + Kette ohne Exception, HUD bindet.
- **Min-Spec-Pass (Galaxy A50):** 30 FPS mit Physik + Trümmer + Gegnern; Lesbarkeit bestätigt.

---

## 7. Task-Backlog (umsetzbare Tickets)

**A — Fundament (nach Phase-0-Setup)**
1. Port `DeterministicRandom` + `IRngProvider` (+ Visual-RNG) → `Core` + Tests.
2. Port `FixedTimestepRunner` → `Core`; als Regel-Treiber verdrahten.
3. Port Enums (`CellType/Direction/EnemyType/PowerUpType`) → `Core`.
4. `BalancingConfig`-ScriptableObject + Importer (liest `prep/seed/*.json`); `BlastShape`/`ArenaSpec`-DTOs.

**B — 3D-Bewegung & Kamera (Feel zuerst)**
5. `PlayerController`: Laufen + **Dash/Roll** (i-Frames) + **Sprung/Doppelsprung** + Ledge-Grab; Coyote-Time-Buffer.
6. `CameraRig`: dynamische Cinemachine-Action-Kamera + **Top-Down-Tilt-Fallback**; Smart-Framing.
7. `InputService` (Touch: Stick + Jump/Dash/Bombe-legen/-werfen; Detonator nur bei `HasDetonator`).

**C — Bomben, Blast, Arena (Kern-Spaß)**
8. `ArenaBuilder` (seed-det. mehrstöckige Arena aus `ArenaSpec`) + `DestructibleModule` (Chunk + Trümmer-Pool).
9. `BombPhysics` (legen/werfen/rollen/Fall) + `BombResolver` (Timer + **3D-Kettenreaktion**, ebenenübergreifend).
10. `BlastVfx` **Sphere/Dome** + **Blast-Preview-Volumen**; Modul-Zerstörung + Drops (BombUp/Fire/Speed/Detonator).
11. `ReadabilityFx`: Through-Wall-Outline (Spieler/scharfe Bomben/Gegner-in-Gefahr), Höhen-/Schatten-Indikator.

**D — Gegner, Boss, Combo**
12. `EnemyAgent` (NavMesh + Off-Mesh-Links) Ballom + Onil: spawn/move/kill, Bomben-Gefahr meiden.
13. `ComboSystem`-Erweiterung (Air-/Drop-Kill) + Score + Sterne-Berechnung.
14. `BossController` Granite Warden 3D (Telegraph/Attack/Cooldown/Enrage/Modul-Regen/Vertikalität).
15. Win/Lose-Logik + Level-Übergang L1→L10; Game-Juice-Minimal (Hit-Pause, Screen-Shake-Impulse, Floating-Text, SFX).

**E — UI**
16. `LevelSelect` (Sektor 1, L1–L10, Sterne, Gating).
17. `BattleHUD` (Time/Score/Combo/Lives) + `Result`-Screen (Sterne, Coins, Retry/Next) + Coins-Persistenz.
18. Discovery-Overlay (Erst-PowerUp), Victory-Sequenz (Warden).

**F — Determinismus & QA**
19. Seed-Reproduzierbarkeit-Test (ArenaGenerator/Drops) als CI-Gate.
20. Domain-Unit-Tests (Combo/BombResolver/Score/Boss).
21. **Min-Spec-Feel-/Lesbarkeits-/Performance-Pass (Galaxy A50)** — Gate-Entscheidung dokumentieren.
22. PlayMode-Smoke + Boot→Game→Result-Durchlauf auf Gerät.

---

## 8. Acceptance-Criteria (Slice „fertig" = Gate-Entscheidung)

- [ ] L1 aktiv spielbar: **frei bewegen** (laufen/dashen/springen/Kante), Bombe **legen + werfen/rollen**,
      Modul sprengen, **3D-Kettenreaktion** auslösen, Gegner besiegen, PowerUp aufnehmen, Combo auslösen.
- [ ] **Vertikalität funktioniert:** mind. 2 Ebenen, Fall zwischen Ebenen, **ebenenübergreifende Kette** sichtbar.
- [ ] Alle 4 Slice-PowerUps droppen und wirken (BombUp/Fire/Speed/Detonator); Detonator-Button nur bei `HasDetonator`.
- [ ] L1–L10 durchspielbar; L10 = Granite Warden in 3D besiegbar (mit Enrage + Modul-Regen).
- [ ] Sterne-Wertung + Coins werden vergeben und **persistiert** (Neustart behält Coins/Sterne).
- [ ] **Blast-Preview-Volumen** korrekt (zeigt getroffenes Volumen inkl. Ketten/Fall), **Through-Wall-Outlines**
      und **Höhen-/Schatten-Indikatoren** machen Vertikalität & Bombenreichweite lesbar.
- [ ] **Lesbarkeit auf Galaxy A50 bestätigt** (Kamera/Outlines/Höhe) — sonst konservativere Kamera/Vertikalität.
- [ ] **60 FPS High-End, 30 FPS Galaxy-A50** mit Physik + Trümmer + Gegnern; keine GC-Spikes im Hot-Path; Pooling greift.
- [ ] **Determinismus-Test grün (CI):** Replay-Hash über Schicht A + Seed-Reproduzierbarkeit. Schicht B
      (PhysX/Debris) ist sauber getrennt und beeinflusst Schicht A nachweislich nicht.
- [ ] 0 Compiler-Warnungen; Domain-Regel-Logik ohne Spiel-`UnityEngine.Random`.
- [ ] Kein Multiplayer-/Idle-/Offline-/Flat-Grid-Code im Slice.
- [ ] **GATE dokumentiert:** „Macht das volumetrische Sprengen mehr Spaß und ist lesbar?" — Ja/Nein + Begründung.

---

## 9. Code-Skelette (Referenz)

**IRngProvider (Generierung) + Regel-Treiber:**
```csharp
// Core (Unity-frei) — xoshiro256+ (256 Bit Zustand). C#9/netstandard2.1: readonly struct statt record struct.
public readonly struct RngState
{
    public readonly ulong S0, S1, S2, S3;
    public RngState(ulong s0, ulong s1, ulong s2, ulong s3) { S0 = s0; S1 = s1; S2 = s2; S3 = s3; }
}
public interface IRngProvider { int NextInt(int min, int max); float NextFloat();
    RngState GetState(); void SetState(RngState s); }

// Game (Treiber) — FixedTimestepRunner treibt die AUTORITATIVE Schicht A @ 60 Hz (Occupancy, Fixed-Point,
// deterministisch → Replay-Hash). Danach läuft die kosmetische PhysX-Schicht B + Render-Interpolation
// separat (nicht-autoritativ, ARCHITECTURE §13 / GDD §12.3).
public class GameLoopDriver : MonoBehaviour
{
    private GameSimulation _sim; private IInputService _input; private IViewRenderer _view;
    private FixedTimestepRunner _runner;
    [Inject] public void Construct(GameSimulation sim, IInputService input, IViewRenderer view)
        { _sim = sim; _input = input; _view = view;
          _runner = new FixedTimestepRunner(hz: 60, maxStepsPerFrame: 5, RuleStep); }

    private void RuleStep(float dt)
    {
        var cmd = _input.PollCommand();   // Move, Jump, Dash, PlaceBomb, ThrowBomb, Detonate
        _sim.Tick(dt, cmd);               // Regel-/Zustandslogik (Bomben/Ketten/Combo/Boss/Score)
    }

    private void Update()
    {
        _runner.Advance(Time.deltaTime);  // Akkumulator → 0..5 Regel-Steps
        _view.Render(_sim);               // read-only: Transforms/Outlines/Preview aktualisieren
    }
    // Physik (Bomben/Trümmer/Bewegung) läuft im Unity-Physics-Substep separat.
}
```

**Sim-Aggregat (Regel-Logik, testbar):**
```csharp
public sealed class GameSimulation
{
    private readonly IRngProvider _rng; private readonly BalancingConfig _cfg;
    public PlayerState Player { get; } public ArenaState Arena { get; } /* Bombs, Enemies, Boss, Combo, Score */
    public GameSimulation(IRngProvider rng, BalancingConfig cfg, LevelDef level) { /* seed-det. Arena via rng */ }

    public void Tick(float dt, InputCommand input)
    {
        ApplyPlayerIntent(input);     // an PlayerController/Physics (Bewegung/Jump/Dash) weiterreichen
        UpdateBombs(dt);              // Timer → BombResolver: 3D-Kette (Volumen, ebenenübergreifend), Drops
        ResolveDestruction();         // zerstörte Module an Game (Chunk + Trümmer) melden
        UpdateEnemiesDecision(dt);    // Ziel/Flee; NavMeshAgent bewegt im Game
        _boss?.Tick(dt, this);        // Granite Warden
        _combo.Tick(dt, killEvents);  // inkl. Air-/Drop-Kill-Klassifikation
        CheckWinLose();
    }
}
```

---

## Änderungslog

| Datum | Version | Änderung |
|-------|---------|----------|
| 2026-06-08 | v0.5 | Initialer Vertical-Slice-Plan (Sektor 1 + Granite Warden), flaches Grid, Fixed-Point-Determinismus. |
| 2026-06-14 | **v0.6** | **Retarget auf Feel-Prototyp (GDD [3D_REINVENTION_PLAN.md](3D_REINVENTION_PLAN.md)): volumetrisches 3D — freie Bewegung (Dash/Sprung/Ledge), Physik-Bomben (legen/werfen/rollen/Fall), volumetrische Sphere/Dome-Blast + ebenenübergreifende 3D-Kette, zerstörbare Module, mehrstöckige Arena, Granite Warden in 3D, NavMesh-AI. Determinismus = 2-Schichten (autoritative Occupancy-Sim mit Replay-Hash + kosmetische PhysX-Schicht); CI = Replay-Hash über Schicht A + Seed-Reproduzierbarkeit. Slice ist hartes Gate für Phase 2.** |

> **Nächster Schritt nach bestandenem Gate:** Phase 2 (Combat-Breite — alle Blast-Formen/Bomben/Gegner/Bosse
> — + Meta-Progression) → [PLAN.md §11](PLAN.md).
