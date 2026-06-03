# Extensions — DI-Hilfsmethoden

Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `LazyServiceExtensions.cs` | `AddLazyResolution()` — registriert `Lazy<T>` als Open-Generic im DI-Container, damit Services mit Zirkel-Abhängigkeiten `Lazy<T>` per Constructor-Injection erhalten können |

---

## Warum das nötig ist

BomberBlast hat zwei Zirkel im Service-Graph die nicht durch einfache Registrierungsreihenfolge
auflösbar sind:

- `BottomTabController` ↔ `NavigationCoordinator`
- `NavigationCoordinator` ↔ `LifecycleHub`

`AddLazyResolution()` registriert einen Resolver der `Lazy<T>` für beliebige `T` bereitstellt.
Das Lambda in der DI-Factory läuft erst zur Laufzeit — zu diesem Zeitpunkt sind alle
Registrierungen abgeschlossen und der Zirkel existiert nicht mehr.

```csharp
// Registrierung in App.axaml.cs:
services.AddLazyResolution();

// Verwendung im Ctor (beliebiger Service):
public class CardService(
    IPreferencesService preferences,
    Lazy<IAchievementService> achievementService,   // aufgelöst erst bei erstem .Value-Zugriff
    Lazy<IWeeklyChallengeService> weeklyService,
    Lazy<IDailyMissionService> dailyMissionService)
```
