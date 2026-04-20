---
name: Kein ViewLocator in SmartMeasure
description: SmartMeasure setzt DataContext im AXAML per SubView-Pattern, nicht per ViewLocator
type: project
---

SmartMeasure hat KEINEN ViewLocator (anders als BingXBot seit 15.04.2026).

Pattern:
1. `App.axaml.cs:45/52`: `new MainView { DataContext = _mainVm }` — einmalig fuer Window-Root, OK
2. `MainView.axaml`: Child-Views mit `DataContext="{Binding SurveyVm}"` etc. — SubView-Pattern
3. `MainView.axaml.cs`: Nur MapView wird lazy erstellt wegen Mapsui GL-Crash (dokumentiert in CLAUDE.md)

Das ist legitim fuer diese App-Groesse (7 Tabs, feste Hierarchie). ViewLocator waere erst sinnvoll bei Mobile/Desktop-Shell-Split wie BingXBot (Dual-Shell).

**Why:** Simpler Aufbau, keine doppelten Mobile-Views noetig. User hat nicht explizit ViewLocator gefordert.

**How to apply:** Beim Audit NICHT das Fehlen eines ViewLocators als Verstoss melden. Nur wenn Service-Locator im View-Ctor oder `new XxxViewModel()` im Code-Behind auftaucht.
