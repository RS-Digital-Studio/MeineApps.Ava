---
name: MVVM-Fullsweep aller 12 Apps 2026-04-18
description: Strikter MVVM-Audit aller 12 Avalonia-Apps, 0 KRITISCHE Findings, globales AvaloniaUseCompiledBindingsByDefault aktiv
type: project
---

**Gesamtbild**: Alle 12 Apps sauber. Keine KRITISCHEN Findings (Service-Locator, DataContext-Code-Behind, View-Typen in VMs, manuelle ICommands).

**Globales Compiled-Bindings-Flag**: `Directory.Build.props:22` setzt `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`. Damit nutzen ALLE Views mit `x:DataType` automatisch Compiled Bindings. Das fehlende `x:CompileBindings="True"`-Attribut auf Einzelviews ist KEIN Finding (nur 0 von ca. 200 Views kritisch betroffen).

**Views ohne x:DataType** (4 Stück): Ausschliesslich Window-Wrapper (BomberBlast/RebornSaga/WorkTimePro/ZeitManager MainWindow.axaml) die ihre `<views:MainView />` als Child hosten — korrekt, kein Handlungsbedarf.

**Verified Patterns**:
- `App.Services.GetRequiredService` / `ServiceLocator.Get` in Views: 0 Treffer (grep **/Views/*.axaml.cs + **/Views/**/*.cs)
- `DataContext = new...` oder `DataContext = App.Services...` in Views: 0 Treffer
- `Avalonia.Controls` / `Avalonia.Markup` in ViewModels: 0 Treffer
- Manuelle `public event PropertyChangedEventHandler`: 0 Treffer (alle ObservableObject)
- `class : ICommand`: 0 Treffer (alle RelayCommand)
- Click=-Handler in AXAML: 0 Treffer
- `_Click`-Handler in Code-Behind: 0 Treffer
- DB-Zugriffe (`_database.` / `.SaveAsync` / `.InsertAsync`) in Views: 0 Treffer
- `NavigationRequested`-Event in VMs: 265 Treffer in 75 Dateien (Pattern durchgängig)

**Einzige Grauzone**: SmartMeasure MainView.axaml.cs:33 `mapContainer.Child = new MapView { DataContext = vm.MapVm }` — bereits in App-CLAUDE.md + memory dokumentiert (Mapsui-GL-Crash-Schutz, Lazy-Init). OK.

**App-spezifische Besonderheiten**:
- BingXBot: ViewLocator-Pattern aktiv seit 15.04.2026, Lazy<T>-DI für Mobile-Shell
- HandwerkerImperium: Phase 4 ViewLocator-Migration für Guild-Sub-Views (6 Thin-Wrapper-VMs in ViewModels.Guild)
- BomberBlast: Klassisches MainView-Stack-Pattern mit 17 Sub-Borders + Class.Active (bewusst kein ViewLocator)
- SmartMeasure + GardenControl: App.axaml.cs macht `new MainView { DataContext = _mainVm }` (Window-Root Self-Binding, OK)
- RechnerPlus: ExpressionParser-Warmup im MainViewModel-Ctor dokumentiert

**How to apply**: Bei zukünftigen Audits nicht auf fehlendes `x:CompileBindings="True"` reagieren, solange `AvaloniaUseCompiledBindingsByDefault=true` in Directory.Build.props steht. Nur auf fehlendes `x:DataType` achten (Pflicht für Compiled Binding).
