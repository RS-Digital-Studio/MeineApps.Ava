# Navigation — Routing & Tab-Steuerung

Feature-Module des MainViewModel-Kompositors. Verwalten `ActiveView` (Source-of-Truth),
26 Routen-Cases und Bottom-Tab-Synchronisation.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `INavigationCoordinator.cs` | Interface: `NavigateToRouteAsync(string)`, `NavigateTo(NavigationRequest)`, `HideAll()`, `ActiveView`-Property, `ActiveViewChanged`-Event |
| `NavigationCoordinator.cs` | Implementierung: 26 Routen-Cases, CloudSave-Init-Race-Guard (3s-Cap), `StateChanged`-Event |
| `IBottomTabController.cs` | Interface: 5 Sub-Tab-Bools, `IsBottomTabBarVisible`, 10 `SwitchToXxxTab()`-Methoden |
| `BottomTabController.cs` | Implementierung: bidirektionale `ActiveView ↔ BottomTab`-Sync via `IBottomTabHub` |
| `NavigationRouteParser.cs` | `Parse(route)` → `NavigationRequest`, `RequiresCloudSaveInit(baseRoute)` |

---

## Abhängigkeits-Richtung

```
ChildViewModelRegistry  (keine Modul-Deps)
        ↑
BottomTabController  → ChildViewModelRegistry
        ↑
NavigationCoordinator → ChildViewModelRegistry + BottomTabController
        ↑
LifecycleHub  → alle vier anderen
```

Die zwei Zirkel (`BottomTabController`↔`NavigationCoordinator`,
`NavigationCoordinator`↔`LifecycleHub`) werden über **lazy Provider-Lambdas** in der
DI-Factory aufgelöst (in `App.axaml.cs`).

---

## NavigationCoordinator

### Route-Parsing

`NavigationRouteParser.Parse(route)` trennt Base-Route von Query-String
(`"game?mode=bossrush"` → `GoGame { Mode = "bossrush" }`).

### CloudSave-Init-Race-Guard

`NavigateToRouteAsync` awaitet `LifecycleHub.CloudSaveInitTask` mit 3s-Cap bevor in
spielrelevante Ziele navigiert wird (Game/LevelSelect/Dungeon/DailyChallenge/
WeeklyChallenge/Deck/Collection). Verhindert Navigation-vor-Cloud-Pull-Race.

### State-Kommunikation

`ActiveViewChanged`-Event → `MainViewModel` ruft `OnPropertyChanged` für alle
betroffenen Forwarder-Properties.

---

## BottomTabController

5 Tabs (Home/Play/Shop/Profile + optional Liga). Tab-Sichtbarkeit via
`IsBottomTabBarVisible`. Sync: Wenn `ActiveView` sich ändert, wird der passende
Bottom-Tab automatisch aktualisiert — und umgekehrt.

---

## NavigationRequest-Pattern

```csharp
// Typsichere Navigation statt String-Konstanten:
NavigationRequested?.Invoke(new GoGame(Mode: "story", Floor: 0));
NavigationRequested?.Invoke(new GoBack());
NavigationRequested?.Invoke(new GoShop(Section: "upgrades"));
```

`INavigable`-Interface auf allen Child-VMs: `event Action<NavigationRequest>? NavigationRequested`.
`ChildViewModelRegistry.WireCommon` subscribed darauf und routet an `NavigationCoordinator.NavigateTo`.
