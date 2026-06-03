# Views — AXAML + UI-Logik

AXAML-Views mit Code-Behind (ausschließlich UI-Logik). Compiled Bindings Pflicht
(`x:CompileBindings="True"` + `x:DataType`). Generische UI-Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md), geteilte Controls → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Tab-Layout, Verlauf-Swipe-Gesten, animierter Hintergrund (`CalculatorBackgroundRenderer`), Onboarding-Flow, FloatingText-Overlay. |
| `CalculatorView.axaml(.cs)` | VFD-/Burst-Animations-Steuerung, Keyboard-Mapping, Landscape-Layout, Swipe-to-Backspace, Funktionsgraph-Ein-/Ausblendung, Error-Shake, Copy-Feedback. |
| `ConverterView.axaml(.cs)` | Swap-Rotation-Animation, Clipboard-Copy-Event. |
| `SettingsView.axaml(.cs)` | Einstellungen (kein eigener Code-Behind-Logik). |

## UI-Patterns

- **Basic-Grid 4×5** (`C () % ÷ / 7 8 9 × / … / ± 0 . =`), **Scientific-Panel 4×5**
  (sin/cos/tan/log/ln + INV-Varianten, `( ) x^y 1/x Ans`, `π e x² √x x!`, `|x|`).
- **Display-Card Header (5 Spalten):** Memory-Indikator | Expression | Share | Copy | Backspace.
- **Keyboard-Mapping (Desktop):** `Shift+8`=×, `Shift+9/0`=`(`/`)`, `OemPlus`=`=`,
  `OemComma/Period`=Dezimalpunkt, `Ctrl+C/V/S/Z/Y`=Kopieren/Einfügen/Teilen/Undo/Redo.
- **Clipboard (Event-basiert):** `ClipboardCopyRequested`/`ClipboardPasteRequested` vom VM →
  View nutzt `TopLevel.GetTopLevel(this)?.Clipboard`. Sauberes Abmelden bei DataContext-Wechsel.

## Landscape-Layout (`CalculatorView.axaml.cs`)

```
Portrait  → RowDefinitions: Auto,Auto,Auto,Auto,Auto,*
Landscape → Spalte 0 (40%): Display + FunctionGraph + ModeSelector + Scientific + Memory
            Spalte 1 (60%): BasicGrid (RowSpan=5)
```

`_autoSwitchedToScientific`-Flag verhindert, dass manueller Scientific-Modus beim Zurückdrehen
überschrieben wird — bei neuen Modus-Wechsel-Pfaden beibehalten.

## Verlauf-Swipe (`MainView.axaml.cs`)

Swipe-up öffnet, Swipe-down schließt den Verlaufs-Bottom-Sheet — aber nur wenn Calculator-Tab
aktiv ist. Implementiert über Tunnel-Routing (`PointerPressedEvent`/`PointerReleasedEvent`)
damit Gesten auch über Buttons registriert werden. Cooldown (`HistoryToggleCooldownMs = 500 ms`)
verhindert versehentliches Sofort-Wiederholen.

## Onboarding (`MainView.axaml.cs`)

Startet nach `Splash.PreloadCompleted`, 500 ms verzögert. Drei Schritte (Display → Button-Grid →
Mode-Selector), jeder als `OnboardingTooltip` an unterschiedlicher vertikaler Position.
`Dismissed`-Event wird pro Schritt frisch an- und abgemeldet um Mehrfachauslösungen zu vermeiden.
`_vm.IsOnboardingCompleted` wird abgefragt bevor der Flow beginnt; am Ende `MarkOnboardingCompleted()`.

## Hintergrund-Render-Loop (`MainView.axaml.cs`)

`CalculatorBackgroundRenderer` läuft via `DispatcherTimer` mit ~5 fps (200 ms-Intervall).
Timer wird in `OnAttachedToVisualTree` gestartet und in `OnDetachedFromVisualTree` mit
`Dispose()` des Renderers gestoppt — kein Memory Leak.
