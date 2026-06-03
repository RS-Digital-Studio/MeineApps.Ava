# MeineApps.CalcLib — Calculator Engine Library

Pure-C# Calculator-Engine für Rechner-Apps. Tokenizer + Parser (Shunting Yard) + Evaluator
+ Unit Converter, ohne UI-/Plattform-Abhängigkeiten, vollständig testbar.

Framework: .NET 10.0 · C# 14 · Keine externen Abhängigkeiten (Pure C#, `System.Math` only)

## Struktur

```
MeineApps.CalcLib/
├── CalculatorEngine.cs           # Stateless math operations (Basic / Extended / Scientific)
├── ExpressionParser.cs           # Infix → Postfix via Shunting Yard + Tokenizer
├── CalculationResult.cs          # readonly record struct: Value / IsError / ErrorMessage
├── CalculationHistoryEntry.cs    # Record: Expression / Result / ResultValue / Timestamp
├── IHistoryService.cs            # Interface: History / AddEntry / LoadEntries / Clear / DeleteEntry / HistoryChanged
├── HistoryService.cs             # In-Memory-Implementation (Session, keine Persistenz)
├── PersistentHistoryService.cs   # JSON-Persistenz in appDataDirectory/calculator_history.json (max 100 Einträge)
└── UnitConverter.cs              # Statische Konvertierung: 8 Kategorien, static Convert(value, from, to)
```

## API-Überblick

### CalculatorEngine

Stateless, alle Methoden geben `double` oder `CalculationResult` zurück (bei Fehler-Möglichkeit):

| Gruppe | Methoden |
|--------|----------|
| Basic | `Add`, `Subtract`, `Multiply`, `Divide`, `Negate` |
| Extended | `Percentage`, `SquareRoot`, `Square`, `Cube`, `CubeRoot`, `Reciprocal`, `NthRoot`, `Power`, `Factorial`, `Abs`, `Mod` |
| Scientific | `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, `Atan`, `Sinh`, `Cosh`, `Tanh`, `Log`, `Ln`, `Exp`, `Exp10` |
| Konstanten | `Pi`, `E` (Properties) |
| Hilfsmethoden | `DegreesToRadians`, `RadiansToDegrees` |

Memory-Funktionen (`M+`, `M-`, `MR`, `MC`, `MS`) sind **nicht** in der Engine — sie liegen im
aufrufenden ViewModel (z.B. `CalculatorViewModel` in RechnerPlus).

### ExpressionParser

```csharp
var engine = new CalculatorEngine();
var parser = new ExpressionParser(engine);
var result = parser.Evaluate("2+3×4");  // 14 (korrekte Präzedenz)
```

Unterstützte Operatoren: `+`, `−`/`-`, `×`/`*`, `÷`/`/`, `^`, `mod`. Klammern, implizite
Multiplikation (`5(3+2)` → `5×(3+2)`), unäre Minus-Verarbeitung, Scientific-Notation in Zahlen
(`1.5E+10`).

**Potenz rechtsassoziativ:** `2^3^2 = 2^(3^2) = 512` (nicht `(2^3)^2 = 64`).

### UnitConverter

```csharp
var units = UnitConverter.GetUnitsForCategory(UnitCategory.Length);
var result = UnitConverter.Convert(100, units[0], units[2]);  // mm → m
```

Kategorien: `Length`, `Weight`, `Temperature`, `Area`, `Volume`, `Speed`, `Time`, `Currency`.
Temperatur nutzt Offset-Logik (Celsius/Fahrenheit/Kelvin). Währungskurse sind statische
Platzhalter — für Live-Kurse API-Integration im Konsumenten nötig.

### IHistoryService / HistoryService / PersistentHistoryService

`HistoryService` — rein In-Memory, geeignet für Apps ohne Datei-Persistenz.
`PersistentHistoryService(appDataDirectory)` — JSON in `calculator_history.json`, max 100 Einträge,
thread-safe via `lock`. Beide implementieren `IHistoryService`.

## Konsumenten

| App | Verwendung |
|-----|------------|
| RechnerPlus | `CalculatorEngine` + `ExpressionParser` + `IHistoryService` + `UnitConverter` |

## Technische Hinweise

- Thread-safe: `CalculatorEngine` und `ExpressionParser` haben keinen Shared State.
- `PersistentHistoryService` ist thread-safe via `lock` (Dateizugriff).
- Für Unit-Tests geeignet — keine Avalonia-/Android-Abhängigkeit. Tests liegen in `tests/`.

## Build

```bash
dotnet build src/Libraries/MeineApps.CalcLib/MeineApps.CalcLib.csproj
```

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — Workspace-Architektur, App-Status
- [MeineApps.Core.Ava/CLAUDE.md](../MeineApps.Core.Ava/CLAUDE.md) — `ICalculationHistoryService` (persistierte History mit Core.Ava-Integration)
