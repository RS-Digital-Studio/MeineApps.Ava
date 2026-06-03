# Audio — WAV-Generator & Sound-Definitionen

Plattformneutraler Audio-Hilfscode, der von `AudioService` (Desktop) und `AndroidAudioService`
gemeinsam genutzt wird. Kein Playback-Code hier — nur Datenerzeugung und Definitionen.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `WavGenerator.cs` | Generiert PCM WAV-Bytes in-memory (Sinuston, 44100 Hz, 16-bit mono, ~50ms Fade-Out). `MemoryStream` mit vorallozierter Kapazität; `ToArray()` am Ende ist der einzige Alloc. |
| `SoundDefinitions.cs` | 6 eingebaute Töne (`default`, `alert_high`, `alert_low`, `chime`, `bell`, `digital`) mit Frequenz/Dauer-Mapping via `GetToneParams(soundId)`. |
| `TimeFormatHelper.cs` | `HH:MM:SS.cs`-Formatierung (Stunden optional). Plattformneutral, auch in ViewModel-Strings genutzt. |
| `HashHelper.cs` | Deterministischer String-Hash (Djb2-Variante, `hash = hash * 31 + c`). Gleicher Algorithmus wie `AndroidNotificationService.StableHash`. |

## WavGenerator — Aufbau

`GenerateWav(frequency, durationMs)` gibt `byte[]` zurück:
- RIFF-Header → fmt-SubChunk (PCM, Mono, 44100 Hz, 16-bit) → data-SubChunk.
- Fade-Out: letzte ~50ms (min(samples/10, sampleRate/20)) werden linear gedämpft.
- Amplitude 0.5 um Clipping zu vermeiden.

## HashHelper — Warum nicht GetHashCode()

`string.GetHashCode()` ist **nicht deterministisch** (ändert sich zwischen App-Neustarts
wegen .NET-Randomization). `AndroidNotificationService` und `AlarmActivity` verwenden stabile
IDs für Notification-IDs, die über Neustarts hinweg konsistent bleiben müssen.
`HashHelper.StableHash()` nutzt den Algorithmus `hash = 17; foreach (char c) hash = hash * 31 + c`
mit `Math.Abs` am Ende. `AndroidNotificationService.StableHash` implementiert denselben
Algorithmus — beide müssen synchron gehalten werden, da Notification-IDs beim Neustart der
App dieselben Werte haben müssen wie beim Erstellen.

## SoundItem.Uri — nullable

`SoundItem.Uri` ist nullable: `null` bedeutet eingebauter Ton (Wiedergabe via `WavGenerator`),
ein String-Wert ist eine `content://`-URI (Android) oder Dateipfad (Desktop).
`AudioService.PlayAsync(soundId)` prüft zuerst `SoundDefinitions.GetToneParams` — nur wenn der
Sound-ID in den eingebauten Sounds enthalten ist, wird WAV generiert. Unbekannte IDs → "default".
