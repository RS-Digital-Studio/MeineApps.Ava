# AI — Feind-KI & Pathfinding

Enthält die gesamte Feind-KI-Logik. Generische Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md). App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `EnemyAI.cs` | Verhaltensentscheidung, Danger-Zone-Berechnung, Richtungsentscheidung |
| `PathFinding/AStar.cs` | A\*-Pathfinding + BFS-Safe-Cell-Suche (eine Klasse) |

---

## Architektur

`EnemyAI` ist eine **Singleton-Instanz für alle Feinde** (nicht pro Feind). `GameEngine` ruft
`SetActiveEnemyCount(n)` + `PreCalculateDangerZone(bombs)` einmal pro Frame auf, dann
`Update(enemy, player, deltaTime)` je Feind. Alle internen Collections sind **object-pooled**
— keine Heap-Allokation pro Frame.

### A\*-Pathfinding (`PathFinding/AStar.cs`)

- `AStar.FindPath(...)` gibt die Pfadlänge zurück. Ergebnis liegt in `ResultPath`
  (`IReadOnlyList<(int x, int y)>`), gültig bis zum nächsten `FindPath()`-Aufruf.
- Gepoolte `PriorityQueue`, `HashSet`, `Dictionary`, `List` — werden pro Aufruf per `.Clear()`
  recycled, keine `new`-Allokation auf dem Hot-Path.
- **Dynamisches Budget** (`AStarBudgetMin = 5`, `AStarBudgetMax = 12`): Das Pro-Frame-Cap
  skaliert mit der aktiven Gegnerzahl (`5 + activeEnemyCount / 4`, geclampt). Notwendig weil
  Mass-Spawn (Survival-Modus, Splitter-Gegner) sonst alle Feinde gleichzeitig auf
  Random-Movement zurückfallen lässt.
- `TryAcquireAStarSlot()` reserviert einen Slot; wer keinen erhält, fällt für **diesen Frame**
  auf Cached-Path bzw. Random-Movement zurück (nächster Frame: Decision-Timer läuft normal weiter).
- **Spawn-Jitter**: `AIDecisionTimer = Random * AIDecisionInterval` im Enemy-Ctor → verteilt
  erste Pfadsuchen bei Mass-Spawn statt alle im selben Frame.

### BFS Safe-Cell (`AStar.FindSafeCell`)

`FindSafeCell(startX, startY, dangerZone, canPassWalls)` ist eine Methode derselben `AStar`-Klasse,
keine eigene Klasse. Gepoolte BFS-Queue + Visited-Set. Wird von `TryEvade` aufgerufen wenn alle
direkten Nachbarzellen unsicher sind. Suchabbruch bei Distanz > 10.

### Danger-Zone

`PreCalculateDangerZone(bombs)` läuft **einmal pro Frame** in `GameEngine.Update`.
Kettenreaktions-Erkennung ist iterativ mit max-3-Durchgängen-Cap (terminiert in der Praxis
sofort, da alle aktiven Bomben bereits in einem Durchgang erfasst werden — das Cap ist ein
Sicherheitsnetz für künftige Mechaniken).

### Enemy Pin-Down Fix (`CanMoveInDirection`)

```csharp
// Two-Pass für Stuck-Feinde (von Bomben eingesperrt):
// Pass 1 (normal):   Bomb-Cells blockierend
// Pass 2 (Last-Resort, falls Count == 0): allowBombCell = true
// Wände/Blöcke/PlatformGaps bleiben in BEIDEN Pässen blockierend
```

Der Gegner darf als Notfall auf eine Bomb-Cell laufen (und dadurch sterben) — das ist
bewusste Bomberman-Mechanik (Spieler treibt Gegner in Bomben). Ohne diesen Pass friert der
Gegner in Korridoren permanent ein.

### Boss-AI

**Lebt nicht hier.** `EnemyAI.Update` überspringt Bosse explizit (der `GameEngine`-Update-Loop
macht für Bosse `continue` vor dem `_enemyAI.Update`-Aufruf). Die vollständige Boss-AI mit
Duo-Ausweichen, Slowdown-Cells und Summoner liegt in `GameEngine.Level.cs` (`UpdateBossAI`).

---

## Performance-Gotchas

- Gepoolte Collections **nie** durch `new List<>()` im Update-Pfad ersetzen — jede
  Allokation auf dem Hot-Path kostet GC-Druck auf Mono-AOT-Android.
- `ResultPath` ist nur bis zum nächsten `FindPath()`-Aufruf gültig. Aufrufer muss Daten
  sofort konsumieren oder in die enemy-eigene Queue kopieren (`enemy.CopyPathFrom(result)`).
