# Code Review Memory

## ViewLocator Pattern (ViewModel-First Migration)
- ViewLocator nutzt simple String-Replace: `.ViewModels.` -> `.Views.` und `ViewModel` -> `View`
- Funktioniert NUR wenn ViewModels und Views parallele Subfolder-Strukturen haben
- HandwerkerImperium MiniGames haben Namespace-Mismatch: VMs in `ViewModels/` (flat), Views in `Views/MiniGames/` (subfolder)
- HandwerkerRechner hat korrekte parallele Struktur: `ViewModels.Floor/Premium` <-> `Views.Floor/Premium`
- FinanzRechner hat korrekte parallele Struktur: `ViewModels.Calculators` <-> `Views.Calculators`
- Lokale DataTemplates in MainView.axaml haben Vorrang vor globalem ViewLocator in App.axaml
- Apps die lokale DataTemplates nutzen: HandwerkerRechner (alle 16 Calculator-VMs), FitnessRechner (5 Calculator-VMs + BarcodeScanner)
- Apps die NUR den ViewLocator nutzen: HandwerkerImperium, WorkTimePro, BomberBlast, RechnerPlus, ZeitManager

## Sonderfälle bei ViewModel-First
- HomeView/TodayView/DashboardView etc. die den MainViewModel als DataContext teilen: `DataContext="{Binding}"` direkt binden, NICHT via ContentControl
- Guild Sub-Views: Eigener DataContext per `DataContext="{Binding GuildViewModel}"`, gehen NICHT durch ViewLocator
- AchievementsView in FitnessRechner: Bindet an MainViewModel, hat KEIN eigenes ViewModel
- $parent[views:MainView] Bindings in WorkTimePro funktionieren korrekt da ContentControl-Children im Visual Tree der MainView liegen

## Data Models die ObservableObject bleiben
- CategoryDisplayItem, BlueprintStep, RoomSlot, RoomCard, InventPart, WorkshopDisplayModel, InspectionCell, RoofTile, PaintCell, PipeTile, Wire, ShiftDayItem
- Korrekt: Diese sind keine ViewModels sondern Daten-Objekte, der ViewLocator soll sie NICHT matchen

## Projekt-Konventionen
- Alle 8 Apps: 3-Projekt-Struktur (Shared/Android/Desktop)
- MainView.axaml ist die Root-View in allen Apps
- Tab-Navigation: Border.TabContent + .Active CSS-Klassen
- Calculator-Overlay: ContentControl Content="{Binding CurrentCalculatorVm}"
