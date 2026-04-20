---
name: Runde 2 MVVM-Audit 2026-04-17
description: Regression-Audit nach BingXBot Lazy-T, ZeitManager Snapshot, RechnerPlus WarmUp, FinanzRechner-Refactoring
type: project
---

**Gesamtbild**: Codebase ist sauber. Keine KRITISCHEN Funde.

Dokumentierte akzeptable Ausnahmen (nicht als Violations melden):
- SmartMeasure MainView.axaml.cs:33 `DataContext = vm.MapVm` — Mapsui GL-Crash-Schutz, in App-CLAUDE.md dokumentiert ("Lazy-Init per Code-Behind")
- SmartMeasure + GardenControl App.axaml.cs: `new MainView { DataContext = _mainVm }` — Window-Root Self-Binding, kein ViewLocator im Einsatz. OK.
- RechnerPlus MainViewModel.cs:77 `_expressionParser.Evaluate("1+1")` im Ctor — POCO-Warmup, synchron, dokumentiert.
- BingXBot: LazyDiService<T> als Transient via `services.AddTransient(typeof(Lazy<>), typeof(LazyDiService<>))` registriert. Kein AOT-Risiko weil die konkreten VMs einzeln als Singleton registriert sind (MakeGenericType zur DI-Laufzeit, nicht erst im ViewLocator).
- BingXBot MainView/MainViewMobile: `ContentControl Content="{Binding CurrentPageViewModel}"` — Lazy wird erst bei Navigation aufgelöst. IsXxxActive-Properties verwenden `IsValueCreated`-Check. Sidebar-Bindings nutzen nur IsXxxActive + Commands, KEIN direkter Sub-VM-Pfad. Korrekt.
- DashboardView.axaml:376 `$parent[UserControl].((vm:DashboardViewModel)DataContext).RemoveFromWatchlistCommand` mit `x:CompileBindings="False"` auf dem ItemsControl — explizit deaktiviert weil `{Binding}` im ItemTemplate auf String zeigt und nicht auf DashboardViewModel. Absichtlich, funktionierend.

Grauzonen:
- BingXBot MainView.axaml.cs:61 `dot.Fill = ...` per FindControl — sollte ein Binding auf ConnectionDotColor sein (ist bereits in AXAML-Code auf Zeile 270 korrekt gebunden via `{Binding ConnectionDotColor}`). Der Code-Behind-Handler ist damit redundant. Nicht kritisch — dort steht kein "ConnectionDot"-Ellipse mehr in der aktuellen MainView.axaml (der Handler findet null). Aufräum-Kandidat, kein Bug.
- FinanzRechner SettingsView OnRestoreFileRequested setzt `_vm.IsBackupInProgress = false` direkt aus der View — View schreibt auf VM-State. Grauzone, aber akzeptabel (keine Service-Locator-Nutzung, nur Dialog-Abbruch-Signalisierung).
