---
name: HandwerkerImperium Audit 2026-04-18
description: MVVM-Audit der Produktiv-App (v2.0.29) nach Phase 1-4 Refactoring. Sauber, ein MainWindow-Fix.
type: project
---

HandwerkerImperium v2.0.29, 55 Views/54 ViewModels, Produktion.

**Fact**: App ist nach den Review-Fixes vom 17.04.2026 (Phase 1-4) in einem vorbildlichen MVVM-Zustand. ViewLocator aktiv (App.axaml + `<Application.DataTemplates><core:ViewLocator /></Application.DataTemplates>`). Alle 54 Views haben `x:CompileBindings="True"` + `x:DataType`. Keine Service-Locator-Calls in Views (nur MainActivity.cs holt MainViewModel und AdService/PurchaseService fuer Factory-Verdrahtung - kein View-Ctor). Alle 54 ViewModels nutzen `ViewModelBase` + `[ObservableProperty]` / `[RelayCommand]`. BaseMiniGameViewModel eliminiert Duplikation in 10 MiniGame-VMs.

**Einziger Violation-Fund**: `MainWindow.axaml` fehlten `x:CompileBindings="True"` + `x:DataType="vm:MainViewModel"`. Code-Behind bindet sich explizit an `DataContext is MainViewModel` fuer Lifecycle-Events (Activated/Deactivated/Closing), Compiled Bindings fehlten nur auf der Window-Ebene. Direkt gefixt, Build OK.

**Why**: Das Gegenstueck `MainView.axaml` (UserControl unterhalb) hatte die Annotation bereits — nur das MainWindow war uebersehen. Keine Crashes, aber ReflectionBinding statt CompiledBinding als stiller Perf-Verlust.

**How to apply**: Bei Window+MainView-Kombinationen pruefen, ob beides `x:DataType` hat. Window setzt DataContext selbst nie (macht `App.axaml.cs` via `desktop.MainWindow.DataContext = mainVm`) — das ist OK und kein Code-Behind-Anti-Pattern.

**Dokumentierte Ausnahmen (bewusst korrekt)**:
- `App.axaml.cs:173-176` setzt `desktop.MainWindow.DataContext = mainVm` in Loading-Pipeline. Kein Anti-Pattern — App-Root muss DataContext initial zuweisen.
- `MainActivity.cs:113/128-129` holt MainViewModel + AdService + PurchaseService aus DI. Kein View-Ctor, sondern Android-Activity-Lifecycle. Korrekt.
- `OnWorkshopCardsPointerPressed` + `OnTabBarPointerPressed` + `OnCeremonyTapped` in Code-Behind — reine UI-Mechanik (SkiaSharp-HitTest + DPI-Skalierung), ruft `Command.Execute()` auf. OK da nicht ins VM wandern kann.
- `OnRenderTimerTick` / `OnCityRenderTick` in MainView/DashboardView — SkiaSharp-Render-Loop, delegiert an gecachte Renderer. OK.
- `<dialogs:XxxDialog DataContext="{Binding DialogVM}">` in MainView.axaml Zeilen 248-356 — gezielte DataContext-Umleitung im XAML (nicht Code-Behind). OK, da nur Sub-VM-Weiterleitung ohne Instanziierung.

**Phase-4-Migration (ViewLocator)**: 6 Thin-Wrapper-VMs in `ViewModels/Guild/` fuer ViewLocator-Konvention `ViewModels.Guild.X → Views.Guild.X`. Sub-VMs halten `Guild` Property auf Parent, Bindings mit `{Binding Guild.X}` Prefix. Pattern ist Vorbild fuer andere Apps.
