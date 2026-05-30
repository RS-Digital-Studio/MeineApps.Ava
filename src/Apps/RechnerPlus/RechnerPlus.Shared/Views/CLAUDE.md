# Views — AXAML + UI-Logik

AXAML-Views mit Code-Behind (ausschließlich UI-Logik). Compiled Bindings Pflicht
(`x:CompileBindings="True"` + `x:DataType`). Generische UI-Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md), geteilte Controls → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Tab-Layout, Bottom-Sheet-Gesten, FloatingText-Overlay. |
| `CalculatorView.axaml(.cs)` | VFD-/Burst-Timer, Keyboard, Landscape-Layout. |
| `ConverterView.axaml(.cs)` | Swap-Animation. |
| `SettingsView.axaml(.cs)` | Einstellungen. |

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
