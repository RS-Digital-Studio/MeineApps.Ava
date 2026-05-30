# AI — Feind-KI & Pathfinding

Enthält die gesamte Feind-KI-Logik. Generische Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md). App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `EnemyAI.cs` | A\*-Pathfinding, BFS-Safe-Cell-Finder, Danger-Zone-Berechnung, Richtungsentscheidung |

---

## Architektur

`EnemyAI` ist eine Instanz-Klasse pro Feind. `GameEngine` ruft `Update(deltaTime)` im Game-Loop
auf. Alle Listen und Queues sind **object-pooled** — keine Heap-Allokation pro Frame.

### A\*-Pathfinding

- Gepoolte `PriorityQueue`, `HashSet`, `Dictionary` — werden pro Search-Aufruf recycled.
- `AStarBudgetPerFrame = 5` — Absicherung bei Extremfällen (Mass-Spawn). Feind fällt
  für 1 Frame auf Random-Movement zurück wenn Budget überschritten.
- **Spawn-Jitter**: `AIDecisionTimer = Random * AIDecisionInterval` im Enemy-Ctor → verteilt
  erste Pfadsuchen bei Mass-Spawn statt alle im selben Frame.

### BFS Safe-Cell Finder

Findet nach einer Explosion die nächste sichere Zelle. Gepoolte Queues.

### Danger-Zone

`PreCalculateDangerZone()` läuft **einmal pro Frame** in `GameEngine.Update`.
Kettenreaktions-Erkennung ist iterativ mit max-5-Durchgängen-Cap.

### Enemy Pin-Down Fix (`CanMoveInDirection`)

```csharp
// Two-Pass für Stuck-Feinde (von Bomben eingesperrt):
// Pass 1: Normal (Bomb-Cells blockiert)
// Pass 2 (Last-Resort, falls Count==0): allowBombCell=true
// Wände/Blöcke/PlatformGaps bleiben IMMER blockierend
```

### Boss-AI

Nutzt kein A\* — direkter Richtungs-Check wegen Multi-Cell-Bounding-Box.
Enrage halbiert den Decision-Timer. `OccupiesCell()` statt `GridX/GridY`.

---

## Performance-Gotchas

- Gepoolte Collections **nie** durch `new List<>()` im Update-Pfad ersetzen — jede
  Allokation auf dem Hot-Path kostet GC-Druck auf Mono-AOT-Android.
- `EnemyPositionIndex` (in `Core/Combat/`) ist der O(1)-Spatial-Lookup für diesen Ordner —
  Dirty-Flag verhindert unnötige Rebuilds.
