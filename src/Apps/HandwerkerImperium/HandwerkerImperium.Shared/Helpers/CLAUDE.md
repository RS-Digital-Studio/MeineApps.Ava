# Helpers — Querschnitts-Utilities

Plattformunabhängige Hilfsklassen die in mehreren Schichten genutzt werden.
Kein Avalonia-spezifischer Code — reine C#-Utilities.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AsyncExtensions.cs` | `RunHandlerSafely(Func<Task>)` — ersetzt `async void`-Event-Handler überall in der App. `ILogService Logger` als statisches Property (wird in `App.axaml.cs` nach DI-Build gesetzt) |
| `PageNavigationHelper.cs` | `CappedNavigationStack` — O(1)-Ringbuffer für Back-Navigation-Stack |
| `CappedNavigationStack.cs` | Ringbuffer-Implementierung für den Navigation-Stack |
| `WorkshopCardHitTester.cs` | Koordinaten-Mapping für Workshop-Card-Touches auf der SkiaSharp-Canvas (relativer Tap → Card-Index) |
| `ProfanityFilter.cs` | Chat + Namen-Filterung (DE/EN/ES/FR/IT/PT). Unicode-NFKD + Strip + Lowercase → deckt Leetspeak + Zero-Width-Tricks. Play-Store-konform |
| `FrameClockRenderLoop.cs` | Helper um `IFrameClock`-basierte Render-Loops in Views sauber anzubinden (Subscribe/Unsubscribe-Pattern) |
| `AnimationHelper.cs` | Einfache Tween-Utilities (Lerp, Clamp01, SmoothStep) |
| `MoneyFormatter.cs` | Geld-Formatierung mit Suffix (K/M/B/T) und Locale-Trenner |
| `TimeSkipHelper.cs` | Berechnet Offline-Einnahmen-Zeitdifferenz mit UTC-Konvertierung |
| `FirebaseKeyValidator.cs` | Validiert Firebase-Key-Zeichensätze (keine `./[]#$`) |
| `StableHash.cs` | Deterministischer 32-Bit-Hash (FNV-1a) für Firebase-HMAC-Basis und Seed-Berechnungen |
| `UtcDateTimeJsonConverter.cs` | `JsonConverter<DateTime>` der IMMER UTC + ISO 8601 "O" Format verwendet. Alle DateTime-Properties in persistierten Models nutzen diesen Converter |
| `MiniGameEffectHelper.cs` | Shared Effekt-Berechnungen für MiniGame-Renderer (Easing, Rating-Score-Mapping) |

---

## AsyncExtensions — Kritisch

`async void`-Event-Handler crashen den Prozess bei unbehandelten Exceptions (kein
awaitable Rückgabewert → Exception geht verloren und killt den App-Process auf Mono).

```csharp
// FALSCH — async void crasht bei Exception:
_gameLoop.OnTick += async () => { await DoWorkAsync(); };

// RICHTIG — RunHandlerSafely fängt alle Exceptions:
_gameLoop.OnTick += () => RunHandlerSafely(DoWorkAsync);
```

`RunHandlerSafely` loggt Exceptions via `AsyncExtensions.Logger` (gesetzt in `App.axaml.cs`).

## UtcDateTimeJsonConverter — Pflicht für Persistenz

Alle `DateTime`-Properties in Modellen die persistiert werden MÜSSEN diesen Converter
verwenden. Ohne ihn konvertiert `JsonSerializer` UTC-Zeiten in Lokalzeit beim Deserialisieren
→ Offline-Timer sind 1-2h falsch bei Zeitzonen-Wechsel.

```csharp
[JsonConverter(typeof(UtcDateTimeJsonConverter))]
public DateTime LastPlayedAt { get; set; }
```
