# ViewModels — Rechen- & Display-Logik

Alle ViewModels sind **Singleton** (in `App.axaml.cs` registriert) und werden vom
`MainViewModel` gehalten. Nur UI-Logik — wissenschaftliche Berechnungen delegieren immer an
`MeineApps.CalcLib`. Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Tab-Navigation (Rechner/Converter/Settings), Back-Press-Flow, FloatingText-Relay, `ExitHintRequested`. |
| `CalculatorViewModel.cs` | Felder, Props, Events, Lifecycle, History-Caches. |
| `CalculatorViewModel.Calculations.cs` | Eingabe, Berechnung, wissenschaftliche Funktionen. |
| `CalculatorViewModel.Display.cs` | `RawDisplay`, `FormatResult`, Preview, Klammern. |
| `CalculatorViewModel.History.cs` | Undo/Redo, History-Commands, Clipboard, Memory. |
| `ConverterViewModel.cs` | 11 Einheiten-Kategorien, Swap-Animation. |
| `SettingsViewModel.cs` | Zahlenformat, Dezimalstellen, Haptic-Toggle. |

## CalcLib-Integration

`CalculatorEngine` liefert `CalculationResult` mit Fehler-Semantik (kein Exception-Werfen).
**Nie** `Math.*` direkt aufrufen — immer über `_engine`, sonst kein Error-Handling/Formatting.

```csharp
var result = _engine.Factorial(n);
if (!result.IsSuccess) { ShowError(result.ErrorMessage); return; }
SetDisplayFromResult(result.Value);
```

## Display-Pipeline (KRITISCH)

```
Eingabe → Expression → ExpressionParser.Evaluate() → double
        → FormatResult() (Math.Round(.,10) gegen 0.30000000000000004, DecimalPlaces, Locale-Trenner)
RawDisplay (computed) = Display ohne Tausender-Trennzeichen, InvariantCulture
```

**`TryParseDisplay()` und alle Berechnungen IMMER auf `RawDisplay`** — `Display` enthält
Tausender-/Dezimaltrenner, `double.Parse()` schlägt damit auf manchen Locales lautlos fehl.

## Undo/Redo

`_undoList` = `LinkedList<CalculatorState>` (O(1) Overflow ohne Array-Umkopieren), `_redoStack`
= `Stack<CalculatorState>`. `SaveState()` **vor jeder** zustandsändernden Operation — auch vor
`Abs()`, `Negate()`, Memory-Operationen.

## History- & Memory-Persistenz

`IHistoryService` (CalcLib) speichert nicht selbst — das VM serialisiert per `IPreferencesService`
als JSON (`LoadHistoryFromPreferences()` beim Start, `SaveHistoryToPreferences()` nach jeder
Berechnung). M-Register überleben Neustart (Preferences); `MC` löscht auch aus Preferences.

## Back-Navigation (`MainViewModel.HandleBackPressed`)

1. History-Panel offen → schließen. 2. ClearHistory-Dialog offen → abbrechen. 3. Nicht auf
Rechner-Tab → `SelectedTabIndex = 0`. 4. Startseite → Double-Back-to-Exit via `BackPressHelper`.

## Converter-Kategorien (11)

Length, Mass, **Temperature (offset-basiert)**, Time, Volume, Area, Speed, Data, Energy,
Pressure, Angle. Leere Eingabe → `OutputValue = ""`.

## Domänen-Gotchas

- **Temperature offset-basiert:** `baseValue = value * ToBase + Offset`. Nur Celsius hat Offset 0.
  Fahrenheit `ToBase=5/9, Offset=-32*5/9`; Kelvin `ToBase=1, Offset=-273.15`.
- **km/h Präzision:** Faktor `1.0/3.6`, nicht `0.277778` — exakte Brüche statt gerundeter Dezimalwerte.
- **Tan-Validation:** `Math.Tan(π/2)` gibt großen Wert, kein `Infinity` → `Math.Abs(result) > 1e15`
  als undefiniert behandeln.
- **`MaxExpressionLength = 200`:** Eingabe-Buttons prüfen `Expression.Length` vor dem Anhängen.
- **Klammer-Validierung:** `")"` nur bei `CountOpenParentheses() > 0`; Smart-Parenthesis-Button
  entscheidet kontextabhängig `(` oder `)`.
