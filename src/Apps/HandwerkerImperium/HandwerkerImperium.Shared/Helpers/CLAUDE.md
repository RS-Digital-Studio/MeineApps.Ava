# Helpers — Querschnitts-Utilities

Plattformunabhängige Hilfsklassen die in mehreren Schichten genutzt werden.
Kein Avalonia-spezifischer Code (Ausnahme: `AnimationHelper` und `MiniGameEffectHelper`
nutzen Avalonia-`Animation`-API), aber keine SkiaSharp- oder Android-Abhängigkeiten.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AsyncExtensions.cs` | `SafeFireAndForget`, `FireAndForget` (Task + ValueTask), `RunHandlerSafely(Func<Task>)` — verhindert `async void`-Crashes. `ILogService? Logger` als internes statisches Property (wird in `App.axaml.cs` nach DI-Build gesetzt) |
| `PageNavigationHelper.cs` | Statische Navigation-Logik: `MainTabs`-Set, `GetPropertyNameFor`, `IsTabToTabTransition`, `ManageStack` — extrahiert aus `MainViewModel` |
| `CappedNavigationStack.cs` | O(1)-Ringbuffer für den Back-Navigation-Stack (Push/Pop/TryPeek/Clear, API-kompatibel zu `Stack<ActivePage>`) |
| `WorkshopCardHitTester.cs` | Hit-Testing für das 2×4-Workshop-Grid: Avalonia-dp → Skia-Koordinaten, Card-Index, Upgrade-Button-Treffer. Konstanten: `TapDistanceThreshold=15dp`, `TapMaxDurationMs=400ms`, `ScrollOffsetThreshold=2dp` |
| `ProfanityFilter.cs` | Chat + Namen-Filterung (DE/EN/ES/FR/IT/PT). Unicode-NFKD + Strip + Lowercase → deckt Leetspeak + Zero-Width-Tricks. Play-Store-konform |
| `FrameClockRenderLoop.cs` | `IDisposable`-Wrapper um `IFrameClock`-Subscriptions. Start/Stop/SetInterval idempotent. Lazy-Init via `App.Services` (View-Layer-Konvention) |
| `AnimationHelper.cs` | Avalonia-`Animation`-basierte UI-Übergänge: `FadeInAsync`, `FadeOutAsync`, `ShowDialogAsync`, `HideDialogAsync`, `BounceAsync`, `PulseAsync`, `SlideInFromBottomAsync`, `ShakeHorizontalAsync`, `ScaleUpDownAsync`, `CountdownPulseAsync` |
| `MoneyFormatter.cs` | Geldformatierung mit Suffix K/M/B/T/Qa/Qi/Sx/Sp/Oc (bis 10^27 Octillion, negatives Vorzeichen, Unicode-Minus U+2212). Varianten: `Format`, `FormatPerSecond`, `FormatPerHour`, `FormatCompact`, `FormatNumber` (ohne €) |
| `TimeSkipHelper.cs` | Berechnet übersprungene Minuten für Zeitbeschleunigung (Rewarded Ads / Premium): bis 10 Min linear, darüber 70% Effizienz |
| `FirebaseKeyValidator.cs` | Prüft Firebase-Pfad-Keys auf verbotene Zeichen (`. $ # [ ] /`) — geteilt zwischen `GuildService` und `GuildInviteService` |
| `StableHash.cs` | Deterministischer 32-Bit-Hash (Polynom `hash = 17 * 31 + c`) — `string.GetHashCode()` ist pro Prozess randomisiert und taugt nicht für persistierte Seeds (z.B. tagesbasierte Marktpreise) |
| `UtcDateTimeJsonConverter.cs` | `JsonConverter<DateTime>` der immer UTC + ISO 8601 "O" schreibt und beim Lesen `Unspecified` als UTC interpretiert, `Local` konvertiert. Alle persistierten `DateTime`-Properties nutzen diesen Converter |
| `MiniGameEffectHelper.cs` | Avalonia-Animation-Utilities für Mini-Game-Ergebnis-Screens: `ShowStarsStaggeredAsync`, `GetRatingBrush`, `PulseResultBorderAsync`, `AnimateCountdownAsync`, `AnimateRewardTextAsync`, `FlashZoneAsync` |

---

## AsyncExtensions — Kritisch

`async void`-Event-Handler crashen den Prozess bei unbehandelten Exceptions (kein
awaitable Rückgabewert → Exception geht verloren und killt den App-Process auf Mono).

```csharp
// FALSCH — async void crasht bei Exception:
_gameLoop.OnTick += async () => { await DoWorkAsync(); };

// RICHTIG — RunHandlerSafely fängt alle Exceptions:
_gameLoop.OnTick += async () => await AsyncExtensions.RunHandlerSafely(DoWorkAsync);

// Für Fire-and-Forget (kein await möglich):
SomeTask().SafeFireAndForget();
SomeTask().FireAndForget(ex => HandleError(ex));
```

`AsyncExtensions.Logger` (intern, nullable) wird in `App.axaml.cs` nach DI-Build gesetzt.
Vor DI-Aufbau fällt `SafeFireAndForget` auf `Console.WriteLine` zurück, `RunHandlerSafely`
auf `Debug.WriteLine`.

## UtcDateTimeJsonConverter — Pflicht für Persistenz

Alle `DateTime`-Properties in Modellen die persistiert werden MÜSSEN diesen Converter
verwenden. Ohne ihn konvertiert `JsonSerializer` UTC-Zeiten in Lokalzeit beim Deserialisieren
→ Daily-/Weekly-Resets feuern bis zu einen Kalendertag versetzt oder werden übersprungen.

```csharp
[JsonConverter(typeof(UtcDateTimeJsonConverter))]
public DateTime LastPlayedAt { get; set; }
```

## CappedNavigationStack — O(1)-Ringbuffer

Ersetzt den früheren `Stack<ActivePage>` mit O(n)-Rebuild bei Cap-Überschreitung.
Älteste Einträge werden bei vollem Buffer still verworfen (LIFO mit FIFO-Drop).
`TryPeek` erlaubt Inspektion ohne Pop.

## MoneyFormatter — Skala bis Octillion

`decimal max ≈ 7.9 × 10^28` — reicht für alle Prestige-Stufen. Negatives Vorzeichen
wird als Unicode-Minus (U+2212) ausgegeben. `FormatNumber` gibt Werte ohne €-Suffix aus
(für Scores, XP, nicht-monetäre Werte).
