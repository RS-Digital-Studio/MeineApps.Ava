# HandwerkerRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Handwerker-App mit 9 Rechnern (4 Free Floor + 5 Premium), Projektverwaltung und Einheiten-Umrechnung.

**Version:** 2.0.0 | **Package-ID:** com.meineapps.handwerkerrechner | **Status:** Geschlossener Test

## Features

### 9 Rechner in 2 Kategorien

**Free Floor Calculators (4):**
1. TileCalculator - Fliesenbedarf (Raum, Verschnitt, Fugenmasse)
2. WallpaperCalculator - Tapetenrollen (Wandhoehe, Muster-Rapport)
3. PaintCalculator - Farbbedarf (Anstriche, Deckfaehigkeit)
4. FlooringCalculator - Laminat/Parkett (Raumform, Verschnitt)

**Premium Calculators (5):**
5. DrywallCalculator - Trockenbau (Platten, Profile, Schrauben)
6. ElectricalCalculator - Elektrik (Kabel, Kosten, Ohm'sches Gesetz)
7. MetalCalculator - Metall (Gewicht, Gewindegroesse, Bohrung)
8. GardenCalculator - Garten (Erde, Mulch, Pflaster, Rasen)
9. RoofSolarCalculator - Dach+Solar (Dachflaeche, Solarpanel, Amortisation)

### Weitere Features
- **Projektverwaltung**: CRUD mit JSON-Persistenz + SemaphoreSlim
- **Einheiten-Umrechnung**: Laenge, Flaeche, Volumen, Gewicht (Metrisch/Imperial)
- **Material-Liste PDF Export**: PdfSharpCore-basiert (A4, Header, Inputs, Results, Footer)

## App-spezifische Services

- **ProjectService**: JSON-Persistenz (Project Model), DateTime.UtcNow
- **CalculationHistoryService**: MaxItemsPerCalculator 30 (10 free / 30 extended)
- **UnitConverterService**: Laenge, Flaeche, Volumen, Gewicht
- **IMaterialExportService / MaterialExportService**: PdfSharpCore A4 Export
- **IPremiumAccessService / PremiumAccessService**: 30-Min temporaerer Zugang zu Premium-Rechnern, 24h Extended History

## Premium & Ads

### Ad-Placements (Rewarded)
1. **premium_access**: 30 Minuten Zugang zu 5 Premium-Rechnern (HomeView)
2. **extended_history**: 24h-Zugang zu 30 statt 10 History-Eintraegen (HomeView)
3. **material_pdf**: Material-Liste PDF Export (alle 9 Calculator Views)
4. **project_export**: Projekt-Export als PDF (ProjectsView)

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- **Vorteile**: Keine Ads, permanenter Premium-Rechner-Zugang, unbegrenzte History, direkter PDF-Export

## Besondere Architektur

### Calculator Overlay via DataTemplates
- `MainViewModel`: `CurrentPage` + `CurrentCalculatorVm` Properties
- `MainView`: DataTemplates fuer automatische View-Zuordnung per VM-Typ (9 VMs)
- Tab-Wechsel: `SelectHomeTab/SelectProjectsTab/SelectSettingsTab` setzen `CurrentPage=null`

### Projekt-Navigation
- ProjectsVM.NavigationRequested → MainVM → `CurrentPage = route` (inkl. projectId Query-Parameter)
- WireCalculatorEvents: Per switch/case (kein gemeinsames Interface)

### Floor vs Premium VMs
- Beide erben direkt von `ObservableObject` (nicht von CalculatorViewModelBase)
- CalculatorViewModelBase existiert als Abstract Base Class, wird aber NICHT verwendet

### Game Juice
- **FloatingText**: "Projekt wurde gespeichert!" nach ConfirmSaveProject
- **Celebration**: Confetti bei erfolgreichem Save

## Changelog (Highlights)

- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay, Export-Buttons (9 Calculator + Projects), Extended-History
- **07.02.2026**: 4 Rewarded Ad Features, Design-Redesign, AppChecker Fixes
- **06.02.2026**: Vollstaendige Lokalisierung, Deep Code Review (36x Debug.WriteLine entfernt)
