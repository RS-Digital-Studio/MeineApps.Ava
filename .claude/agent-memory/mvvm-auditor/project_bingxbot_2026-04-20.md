---
name: BingXBot MVVM-Audit 2026-04-20
description: Nach MVVM-Sanierung 15.04.2026 + Mobile-Shell-Refactor auf ViewLocator: 0 Findings, Referenz-App
type: project
---

Regression-Audit nach v1.1.4 Android-Crash (Service-Locator in View-Ctor) und v1.2.x Mobile-Shell-Refactor.

**Stand v1.2.5/6** (20.04.2026): Vollstaendig sauber. Architektur-Baseline fuer alle Avalonia-Apps.

Schluessel-Aspekte:
- App.axaml hat `<local:ViewLocator />` als `<Application.DataTemplates>`.
- App.axaml.cs setzt `singleView.MainView = new ContentControl { Content = mainVm }` (Android) bzw. `desktop.MainWindow = new MainWindow { DataContext = mainVm, Content = mainVm }` (Desktop). Der Desktop-Fall mit `DataContext=mainVm` im MainWindow-Root ist der erlaubte Composition-Root (kein View-Ctor).
- `App.IsMobileShell`-Flag steuert ViewLocator-Fallback auf `XyzViewMobile`.
- Alle 18 View-Code-Behinds (9 Desktop + 9 Mobile) nur `InitializeComponent()` + optional `DataContextChanged`-Pattern fuer VM-Event-Subscriptions (DashboardView, LogView, MainViewMobile mit Scrim-Tap-Handler).
- MainViewModel ist saubere Composition-Root: `DashboardViewModel` eager + 7 `Lazy<T>` Child-VMs per Constructor Injection.
- Alle Views haben `x:CompileBindings="True"` + `x:DataType="vm:XxxViewModel"`. MainWindow hat `x:DataType`, `x:CompileBindings` defaultet via `AvaloniaUseCompiledBindingsByDefault=true` (global). Eine einzige `x:CompileBindings="False"`-Ausnahme: `DashboardView.axaml:362` (WatchlistSymbols ItemsControl, bewusst fuer heterogene Symbol-Typen).
- Keine Click-Handler im Code-Behind. Keine manuell implementierten ICommand-Klassen.
- Keine PropertyChanged-Handler manuell (ObservableObject via CommunityToolkit.Mvvm.ComponentModel).
- Navigation per `CurrentPageViewModel`-Swap im MainViewModel — Event-basiertes `NavigationRequested` wird hier bewusst nicht verwendet, weil MainView/MainViewMobile ein einzelnes ContentControl haben und Navigation-Commands direkt am MainViewModel haengen.
- `App.axaml.cs` registriert alle VMs als Singleton (kompatibel mit Lazy<T>-Pattern) + `LazyDiService<>` als Transient-Wrapper.

**False-Positives die NICHT zu melden waren**:
- `DataContext = mainVm` in App.axaml.cs:84 — Desktop-MainWindow-Composition-Root, kein View-Ctor.
- `DataContextChanged`-Subscribes in DashboardView/LogView/MainViewMobile — dokumentiertes Pattern fuer Views die VM-Events konsumieren, sauberes Ab/Anmelden in `OnDetached`/`Unsubscribe`.

**Regression-Resistenz**: Das v1.1.4-Crash-Pattern (`App.Services.GetRequiredService<T>()` im View-Ctor) ist aktuell in 0 Views vorhanden und wurde per MainViewMobile-Refactor (8 parallele Views → 1 ContentControl) grundsaetzlich eliminiert.
