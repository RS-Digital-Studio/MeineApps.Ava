---
name: BomberBlast MVVM-Audit 2026-04-18
description: Regression-Check gegenüber 17.04. - unverändert clean, LevelGenerator-Extraktion+PersistenceHealth berührt kein MVVM
type: project
---

Zweiter Audit in 24h (nach GameEngine-Extraktionen v2.0.30+). Befund: weiterhin clean.

**Warum nochmal checken:** Nach GameEngine-Teil-Extraktionen (LevelGenerator, SpecialExplosionEffects, PersistenceHealth) — alle in Core/, berühren keine Views/VMs.

**Bestätigt unverändert:**
- 0 View-Side Service-Locator (nur Android MainActivity.cs:123-124 = Lifecycle-Injection, OK)
- 0 DataContext-Setzungen in *.axaml.cs
- 23/24 AXAML mit CompileBindings+DataType (MainWindow = reiner Window-Wrapper, 13 Zeilen, korrekte Ausnahme)
- 25 VMs mit NavigationRequested-Event
- 1 Code-Behind Event-Handler: DungeonView.MapCanvas_PointerPressed (reine Touch-Koordinaten-Umrechnung → VM-Call, OK)
- 7 DataTemplate-Bindings mit $parent[ItemsControl].((vm:XxxViewModel)DataContext).Command-Syntax = korrekte Compiled-Binding mit Cast (BattlePass, Shop, Dungeon)

**How to apply:**
- BomberBlast bleibt die MVVM-Referenz-Implementierung unter den Avalonia-Apps
- Keine ViewLocator-Migration geplant (Singleton-VM-Pattern funktioniert und ist dokumentiert)
- Zukünftige Audits können sich auf neue VMs/Views konzentrieren; bestehende sind stabil
