# MeineApps.CalcLib — Calculator Engine Library

Pure-C# Calculator-Engine für alle Rechner-Apps. Tokenizer + Parser (Shunting Yard) + Evaluator,
ohne UI-/Plattform-Abhängigkeiten, vollständig testbar.

## Zielframework

- .NET 10.0
- C# 14
- Keine externen Abhängigkeiten (Pure C#, `System.Math` only)

## Build

```bash
dotnet build src/Libraries/MeineApps.CalcLib/MeineApps.CalcLib.csproj
```

## Zweck

- **CalculatorEngine** — Core Math Operations (Basic / Extended / Scientific Mode)
- **ExpressionParser** — Infix → Postfix via Shunting Yard mit Operator-Präzedenz
- **CalculationResult** — Result-Wrapper mit Error-Handling
- **HistoryService** — In-Memory-Historie pro Calculator

## Struktur

```
MeineApps.CalcLib/
├── CalculatorEngine.cs           # Core math operations (sin/cos/log/x^y/...)
├── ExpressionParser.cs           # Infix → Postfix mit Operator-Präzedenz
├── CalculationResult.cs          # Result mit Error-Handling
├── CalculationHistoryEntry.cs
├── IHistoryService.cs
└── HistoryService.cs
```

## Features

| Modus | Operationen |
|-------|-------------|
| Basic | `+`, `-`, `×`, `÷`, `%`, `√`, `x²`, `1/x` |
| Extended | `x^y`, `ⁿ√x`, `n!`, Klammern, Memory (`M+`, `M-`, `MR`, `MC`, `MS`) |
| Scientific | `sin`/`cos`/`tan`, `sinh`/`cosh`/`tanh`, `log`/`ln`, `π`/`e`, Deg/Rad |

## Verwendung

```csharp
var engine = new CalculatorEngine();
var parser = new ExpressionParser(engine);
var result = parser.Evaluate("2+3×4");  // 14 (korrekte Präzedenz)
```

## Konsumenten

| App | Verwendung |
|-----|------------|
| RechnerPlus | CalculatorEngine + ExpressionParser (Standard-Rechner) |
| HandwerkerRechner, FinanzRechner, FitnessRechner | ExpressionParser für Eingabe-Felder (optional) |

## Technische Hinweise

- Thread-safe (keine Shared State zwischen Aufrufen)
- Für Unit-Tests geeignet (keine Avalonia-/Android-Abhängigkeit)
- **Potenz rechtsassoziativ:** `2^3^2 = 2^9 = 512` (nicht `(2^3)^2 = 64`)

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — Projekt-Übersicht, App-Status
- [MeineApps.Core.Ava/CLAUDE.md](../MeineApps.Core.Ava/CLAUDE.md) — `ICalculationHistoryService` (persistierte History)
