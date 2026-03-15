---
name: new-view
description: Erstellt eine neue View + ViewModel nach Projekt-Conventions (AXAML + CS + VM + DI + Navigation).
user-invocable: true
allowed-tools: Read, Write, Edit, Grep, Glob
argument-hint: "<AppName> <ViewName>"
---

# Neue View erstellen

Erstelle eine neue View mit ViewModel fuer die App `$ARGUMENTS` nach den Projekt-Conventions.

## Argumente parsen

- Erstes Wort: App-Name (z.B. HandwerkerImperium)
- Rest: View-Name (z.B. AchievementView)
- Falls ohne "View"-Suffix: automatisch anhaengen

## Vorgehen

### 1. Bestehende Patterns analysieren
- Lese eine bestehende View in `src/Apps/{App}/{App}.Shared/Views/` als Vorlage
- Lese das dazugehoerige ViewModel in `ViewModels/`
- Pruefe `App.axaml.cs` fuer DI-Registrierung
- Pruefe `MainViewModel.cs` fuer Navigation-Verdrahtung

### 2. ViewModel erstellen
- Datei: `src/Apps/{App}/{App}.Shared/ViewModels/{ViewName}Model.cs`
- Erbt von `ObservableObject`
- `[ObservableProperty]` fuer Properties
- `[RelayCommand]` fuer Commands
- `NavigationRequested` Event falls Sub-Navigation noetig
- `UpdateLocalizedTexts()` Methode
- Constructor Injection fuer Services
- Deutsche Kommentare

### 3. View erstellen (AXAML)
- Datei: `src/Apps/{App}/{App}.Shared/Views/{ViewName}.axaml`
- `x:CompileBindings="True"` und `x:DataType="vm:{ViewName}Model"`
- `DynamicResource` fuer alle Farben (keine hardcodierten Werte)
- Material.Icons xmlns einbinden
- ScrollViewer mit Bottom-Margin 60dp falls scrollbar

### 4. View Code-Behind erstellen
- Datei: `src/Apps/{App}/{App}.Shared/Views/{ViewName}.axaml.cs`
- Minimal: nur InitializeComponent()
- DataContext wird per DI gesetzt, NICHT im Code-Behind

### 5. DI-Registrierung
- ViewModel in `App.axaml.cs` registrieren (Transient oder Singleton je nach Bedarf)
- View NICHT registrieren (wird per DataTemplate/ViewLocator aufgeloest)

### 6. Navigation verdrahten
- In `MainViewModel.cs`: NavigationRequested-Event des neuen VMs verdrahten
- Route-String definieren

### 7. Zusammenfassung
- Erstellte Dateien auflisten
- Fehlende RESX-Keys identifizieren
- Naechste Schritte vorschlagen
