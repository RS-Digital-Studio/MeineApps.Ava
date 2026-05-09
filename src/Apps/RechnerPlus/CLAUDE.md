# RechnerPlus

> Build-Befehle, Conventions, Troubleshooting → [Haupt-CLAUDE.md](../../../CLAUDE.md)

Scientific Calculator mit Unit Converter — werbefrei, kostenlos, kein IAP.

**Version:** 2.0.7 (VersionCode 31) | **Package:** com.meineapps.rechnerplus | **Status:** Geschlossener Test

---

## Architektur

### Datei-Struktur

```
RechnerPlus.Shared/
├── App.axaml.cs                          # DI-Root, Loading-Pipeline, HapticServiceFactory
├── Loading/
│   └── RechnerPlusLoadingPipeline.cs     # Sequentieller Start (CalcLib-Warm-Up, History-Load)
├── ViewModels/
│   ├── MainViewModel.cs                  # Tab-Navigation, Back-Press, FloatingText-Relay
│   ├── CalculatorViewModel.cs            # Felder, Props, Events, Lifecycle, History-Caches
│   ├── CalculatorViewModel.Calculations.cs # Eingabe, Berechnung, wissenschaftliche Funktionen
│   ├── CalculatorViewModel.Display.cs    # RawDisplay, FormatResult, Preview, Klammern
│   ├── CalculatorViewModel.History.cs    # Undo/Redo, History-Commands, Clipboard, Memory
│   ├── ConverterViewModel.cs             # 11 Einheiten-Kategorien, Swap-Animation
│   └── SettingsViewModel.cs              # Zahlenformat, Dezimalstellen, Haptic-Toggle
├── Views/
│   ├── MainView.axaml(.cs)               # Tab-Layout, Bottom-Sheet-Gesten, FloatingText
│   ├── CalculatorView.axaml(.cs)         # VFD-Timer, Burst-Timer, Keyboard, Landscape-Layout
│   ├── ConverterView.axaml(.cs)          # Swap-Animation
│   └── SettingsView.axaml(.cs)
├── Graphics/
│   ├── VfdDisplayVisualization.cs        # 7-Segment VFD-Röhren-Simulation (SkiaSharp)
│   ├── ResultBurstVisualization.cs       # Lichtring + 8 Partikel-Strahlen bei "="
│   ├── FunctionGraphVisualization.cs     # Mini-Funktionsgraph mit Glow + Gradient
│   ├── CalculatorBackgroundRenderer.cs   # "Digital Circuit Board"-Hintergrund (5fps)
│   └── RechnerPlusSplashRenderer.cs      # Splash: Tasten-Matrix + LCD-Display
└── Controls/
    └── ExpressionHighlightControl.cs     # Syntax-Highlighting (Zahlen/Operatoren/Klammern)
```

### DI-Registrierung (alle Singleton)

```csharp
// Core
IPreferencesService → PreferencesService("RechnerPlus")
ILocalizationService → LocalizationService(AppStrings.ResourceManager, ...)

// CalcLib (externe Library)
CalculatorEngine → Singleton
ExpressionParser  → Singleton
IHistoryService   → HistoryService (Singleton)

// Platform
IHapticService → HapticServiceFactory?.Invoke(sp) ?? NoOpHapticService  // Android setzt Factory

// ViewModels
CalculatorViewModel, ConverterViewModel, SettingsViewModel, MainViewModel → alle Singleton
```

### Loading-Pipeline

```
1. CalcLib-Warm-Up (ExpressionParser.Evaluate("1+1") im MainViewModel-Ctor)
2. History-Persistenz laden (IHistoryService aus IPreferencesService JSON)
3. Memory-Persistenz laden (M-Register aus Preferences)
4. Mindestens 800ms Splash-Anzeige (damit Sweep-Wellen-Animation abläuft)
```

---

## Feature-Patterns

### CalcLib-Integration

`MeineApps.CalcLib` liefert `CalculatorEngine` + `ExpressionParser` + `IHistoryService`.
Das ViewModel delegiert wissenschaftliche Funktionen immer an `_engine` — nie selbst berechnen,
weil `CalculatorEngine` `CalculationResult` mit Fehler-Semantik zurückgibt (kein Exception-Werfen).

```csharp
// Richtig: CalculationResult mit Fehler-Semantik
var result = _engine.Factorial(n);
if (!result.IsSuccess) { ShowError(result.ErrorMessage); return; }
SetDisplayFromResult(result.Value);

// Falsch: direkt Math.* aufrufen (kein Error-Handling, kein Formatting)
display = Math.Sin(value).ToString();
```

### Display-Pipeline (KRITISCH)

```
Eingabe → Expression (string) → ExpressionParser.Evaluate() → double
         → FormatResult() → Display-String
              └─ Math.Round(value, 10)  ← verhindert 0.30000000000000004
              └─ DecimalPlaces-Setting
              └─ locale-abhängige Tausender-/Dezimaltrenner

RawDisplay (computed) = Display ohne Tausender-Trennzeichen, InvariantCulture
TryParseDisplay()     = IMMER auf RawDisplay operieren, nie auf Display
```

### Temperature-Konvertierung (Offset-Formel)

Fahrenheit und Kelvin sind **offset-basiert**, nicht nur faktor-basiert.
Nur Celsius hat Offset 0 (Referenz-Einheit).

```csharp
baseValue = value * ToBase + Offset;
// Fahrenheit: ToBase = 5/9,  Offset = -32 * 5/9
// Kelvin:     ToBase = 1.0,  Offset = -273.15
// Celsius:    ToBase = 1.0,  Offset = 0 (Referenz)
```

### History-Persistenz

`IHistoryService` aus CalcLib speichert die Einträge **nicht selbst**. Das ViewModel
serialisiert per `IPreferencesService` als JSON. Beim Start `LoadHistoryFromPreferences()`,
nach jeder Berechnung `SaveHistoryToPreferences()`.

### Undo/Redo

Stack-basiert: `_undoList` als `LinkedList<CalculatorState>` (O(1) Overflow ohne Array-Umkopieren),
`_redoStack` als `Stack<CalculatorState>`. `SaveState()` IMMER vor jeder zustandsändernden
Operation aufrufen — auch vor `Abs()`, `Negate()` und Memory-Operationen.

### Landscape-Layout (CalculatorView.axaml.cs)

```
Portrait  → RowDefinitions: Auto,Auto,Auto,Auto,Auto,*
Landscape → RowDefinitions: Auto,Auto,Auto,Auto,*
            Spalte 0 (40%): Display + FunctionGraph + ModeSelector + Scientific + Memory
            Spalte 1 (60%): BasicGrid (RowSpan=5)
```

`_autoSwitchedToScientific`-Flag verhindert, dass manueller Scientific-Modus beim
Zurückdrehen überschrieben wird. Zurück zu Basic nur wenn auto-gewechselt wurde.

### Back-Navigation (HandleBackPressed)

```
1. History-Panel offen         → schließen, return true
2. ClearHistory-Dialog offen   → abbrechen, return true
3. Nicht auf Rechner-Tab       → SelectedTabIndex = 0, return true
4. Startseite                  → Double-Back-to-Exit via BackPressHelper
```

---

## SkiaSharp-Visualisierungen

### VFD-Display (`VfdDisplayVisualization.cs`)

Retro-Tech-Charakter von RechnerPlus. Ersetzt TextBlock durch echte 7-Segment-Simulation:
- Cyan-Grün (#00FFB0) normal, Rot (#FF4444) bei Fehler
- Ghost-Segmente (alle 7 dezent sichtbar) wie bei echten VFD-Röhren
- `_glowPaint.MaskFilter` dauerhaft im Field-Initializer gesetzt (kein Leak)
- `_dotSegmentPaint` für Punkte ohne Glow (separater Paint)
- Subtiles Flicker (±3%, ~7Hz sin-basiert), Hintergrund #0A0A0A
- Timer: DispatcherTimer 33ms, startet in `OnAttachedToVisualTree`

### Result-Burst (`ResultBurstVisualization.cs`)

Expandierender Lichtring + 8 Partikel-Strahlen. Ausgelöst durch `CalculationCompleted`-Event.
Cubic ease-out, 500ms, transparentes Overlay über Display-Bereich.

### FunctionGraph (`FunctionGraphVisualization.cs`)

Mini-Graph (140px Portrait / 100px Landscape) für sin, cos, tan, sqrt, log, ln, x², 1/x.
- `xStep`/`yStep` einmal in `Render()` berechnen, an `DrawGrid()` + `DrawLabels()` übergeben
- Asymptoten-Handling: NaN/Infinity/große Werte → Pfad-Unterbrechung (kein DrawLine)
- Teilt den VFD-Timer (33ms), `_vfdAnimTime` als `animTime`-Parameter

### Animierter Hintergrund (`CalculatorBackgroundRenderer.cs`)

"Digital Circuit Board" — 4 Layer: 3-Farben-Gradient (#302A56→#221E40→#2C1850),
Dot-Grid (32px Spacing, 2px/s Drift), 15 Math-Partikel (Indigo Alpha 8%), radiale Vignette.
~5fps (DispatcherTimer 200ms). Gecachte Paints, Shader-Cache nur bei Größenänderung.

### ExpressionHighlightControl

Brushes (`_cachedPrimary`, `_cachedText`, `_cachedMuted`) gecacht, invalidiert bei
`ActualThemeVariantChanged`. Zahlen: TextPrimary, Operatoren: Primary+Bold, Klammern: Muted.

---

## UI-Patterns

### Button-Grid-Layout (Basic Mode 4×5)

```
C  | ()  | %  | ÷
7    8     9    ×
4    5     6    −
1    2     3    +
±    0     .    =
```

### Scientific-Panel (4×5)

```
Row 0: sin/cos/tan/log/ln  (INV-Varianten: sin⁻¹/cos⁻¹/tan⁻¹/10ˣ/eˣ)
Row 1: ( ) x^y 1/x Ans
Row 2: π e x² √x x!
Row 3: |x|
```

### Display-Card Header (5 Spalten)

Memory-Indikator (M) | Expression | Share-Icon | Copy-Icon | Backspace-Icon

### Keyboard-Mapping (Desktop)

| Taste | Funktion |
|-------|----------|
| Shift+8 | × |
| Shift+9 / Shift+0 | ( / ) |
| OemPlus (ohne Shift) | = |
| OemComma/OemPeriod | Dezimalpunkt |
| Ctrl+C/V/S/Z/Y | Kopieren/Einfügen/Teilen/Undo/Redo |

### Clipboard-Pattern (Event-basiert)

`ClipboardCopyRequested` / `ClipboardPasteRequested` vom VM → View nutzt
`TopLevel.GetTopLevel(this)?.Clipboard`. Sauberes Abmelden bei DataContext-Wechsel nötig.

---

## Gotchas

### RawDisplay vs. Display

`TryParseDisplay()` und alle Berechnungen IMMER auf `RawDisplay` operieren.
`Display` enthält Tausender-Trennzeichen und locale-spezifische Dezimaltrenner —
`double.Parse()` schlägt damit auf manchen Locales lautlos fehl.

### VFD-Timer und FunctionGraph teilen denselben Timer

Der DispatcherTimer 33ms in `CalculatorView.axaml.cs` wird von VFD-Flicker
und FunctionGraph-Glow-Pulsierung gemeinsam genutzt (`_vfdAnimTime` als animTime).
Separaten Timer für FunctionGraph erstellen würde doppelte CPU-Last verursachen.

### Landscape-Auto-Switch Guard

`_autoSwitchedToScientific` nicht vergessen wenn neue Modus-Wechsel-Pfade eingebaut werden.
Ohne Flag würde manueller Scientific-Modus beim Drehen zurückgesetzt.

### MaxExpressionLength = 200

Verhindert Memory-Probleme bei langen verschachtelten Ausdrücken. Eingabe-Buttons
prüfen `Expression.Length >= MaxExpressionLength` vor dem Anhängen.

### Klammer-Validierung

")" wird nur eingefügt wenn `CountOpenParentheses() > 0`. Smart-Parenthesis-Button
`( )` entscheidet automatisch (kontextabhängig) ob "(" oder ")".

### km/h Präzision

Speed-Faktor ist `1.0 / 3.6` (nicht 0.277778). Bei Einheiten-Faktoren immer
exakte Brüche bevorzugen statt gerundete Dezimalwerte.

### Tan-Validation

`Math.Tan()` gibt bei π/2 einen sehr großen Wert zurück, kein `Infinity`.
Explizite Prüfung: `Math.Abs(result) > 1e15` → als undefiniert behandeln.

### Memory-Persistenz

M-Register überleben App-Neustart (in `IPreferencesService`). ToolTip zeigt aktuellen
Wert. `MC` löscht auch aus Preferences. Memory-Row: Opacity 0.4→1 via DoubleTransition
wenn Memory aktiv (XAML KeyFrame nur für Opacity, KEIN RenderTransform wegen Crash-Risiko).

---

## Converter-Kategorien (11)

| Nr | Kategorie | Einheiten (Beispiele) |
|----|-----------|----------------------|
| 1 | Length | m, km, mi, ft, in, cm, mm, yd, nmi, µm |
| 2 | Mass | kg, g, lb, oz, mg, t, st |
| 3 | Temperature | C, F, K (offset-basiert) |
| 4 | Time | s, min, h, d, wk |
| 5 | Volume | L, mL, gal, qt, pt, fl oz, cup, tbsp, tsp |
| 6 | Area | m², km², ha, ft², ac |
| 7 | Speed | m/s, km/h, mph, kn |
| 8 | Data | B, KB, MB, GB, TB, bit |
| 9 | Energy | J, kJ, cal, kcal, Wh, kWh, BTU |
| 10 | Pressure | Pa, kPa, bar, atm, psi, mmHg |
| 11 | Angle | °, rad, gon, tr, ′, ″ |

Leere Eingabe → `OutputValue = ""` (kein Fehlertext). Swap-Button rotiert 180° per Animation.

---

## Build / Test / Deploy

```bash
# Shared bauen
dotnet build "F:\Meine_Apps_Ava\src\Apps\RechnerPlus\RechnerPlus.Shared"

# Desktop starten (Entwicklung)
dotnet run --project "F:\Meine_Apps_Ava\src\Apps\RechnerPlus\RechnerPlus.Desktop"

# AppChecker
dotnet run --project "F:\Meine_Apps_Ava\tools\AppChecker" RechnerPlus

# Android AAB (nur auf explizite Anfrage)
dotnet publish "F:\Meine_Apps_Ava\src\Apps\RechnerPlus\RechnerPlus.Android" -c Release
```

---

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Troubleshooting | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Calculator Engine, ExpressionParser, IHistoryService | `src/Libraries/MeineApps.CalcLib/` |
| Theme-Tokens, IPreferencesService, BackPressHelper | `src/Libraries/MeineApps.Core.Ava/` |
| IHapticService, FloatingTextOverlay, SkiaLoadingSplash | `src/UI/MeineApps.UI/` |
