---
name: BomberBlast MVVM-Audit 2026-04-17
description: Befund-Snapshot v2.0.28 - Architektur-Besonderheiten und Grenzfaelle die NICHT als Violations zaehlen
type: project
---

BomberBlast nutzt klassisches Single-MainView-Pattern (nicht ViewLocator wie BingXBot).

**Why:** Singleton-VMs werden alle eager im DI registriert (App.axaml.cs Zeilen 276-299). MainView stackt 17 Sub-View-Borders mit `IsXxxActive`-Bool-Properties + CSS-`Active`-Klasse. KEIN ViewLocator/ContentControl-Pattern - DataContext der Sub-Views wird per AXAML-Binding `DataContext="{Binding XxxVm}"` gesetzt.

**How to apply:**
- Keine `ViewLocator.cs` erwarten - die App hat bewusst keine
- Mehrere VMs gleichzeitig in Memory ist by design (alle Singleton, kein Lazy-Load)
- MainView Code-Behind 207 Zeilen ist OK: nur SkiaSharp-Effekte (FloatingText, Confetti, Splash-Preload) + CSS-Klassen-Toggle
- GameView Code-Behind 256 Zeilen ist OK: Render-Timer + 3-stufige VM-Subscription (DataContextChanged + Loaded + OnPaintSurface-Safety) - bewusst so wegen Singleton-VM-Re-Subscribe-Problem
- `Avalonia.Media.Color` in VMs (HighScores, League, LevelSelect, Shop) = Datentyp-Import, KEINE View-Abhaengigkeit. OK.
- `Avalonia.Threading.Dispatcher` in GameViewModel/GameOverViewModel = UI-Thread-Marshalling, OK
- `Avalonia.Input.Key` in GameViewModel = Keyboard-Input wird vom Code-Behind weitergeleitet, Key-Enum wandert zwingend mit. OK.

Snapshot: 24 Views, 27 VMs (inkl. INavigable, IGameJuiceEmitter, NavigationRequest), 0 Service-Locator, 0 DataContext-Code-Behind-Setzungen, 23/24 AXAML mit CompileBindings (MainWindow.axaml ist nur Window-Wrapper ohne Bindings - korrekt). Build clean.
