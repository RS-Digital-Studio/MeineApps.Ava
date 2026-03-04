# MeineApps.CalcLib - Calculator Library

## Zweck
Shared Library für Rechner-Apps:
- CalculatorEngine (Basic/Extended/Scientific Mode)
- ExpressionParser (Shunting Yard Algorithmus)
- CalculationResult, HistoryService

## Struktur
```
MeineApps.CalcLib/
├── CalculatorEngine.cs       # Core math operations
├── ExpressionParser.cs       # Infix → Postfix mit Operator-Präzedenz
├── CalculationResult.cs      # Result mit Error-Handling
├── CalculationHistoryEntry.cs
├── IHistoryService.cs
└── HistoryService.cs
```

## Features
- **Basic:** +, -, ×, ÷, %, √, x², 1/x
- **Extended:** x^y, ⁿ√x, n!, Klammern, Memory (M+/M-/MR/MC/MS)
- **Scientific:** sin/cos/tan, sinh/cosh/tanh, log/ln, π/e, Deg/Rad

## Verwendung
```csharp
var engine = new CalculatorEngine();
var parser = new ExpressionParser(engine);
var result = parser.Evaluate("2+3×4");  // 14 (korrekte Präzedenz)
```

## Apps
| App | CalcLib |
|-----|---------|
| RechnerPlus | ✅ |

## Technische Hinweise
- Target: `net10.0` (keine MAUI-Abhängigkeit)
- Pure C#, thread-safe, für Unit Tests geeignet
- Potenz rechtsassoziativ: 2^3^2 = 512
