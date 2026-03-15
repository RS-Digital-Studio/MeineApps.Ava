---
name: new-service
description: Erstellt einen neuen Service nach Projekt-Conventions (Interface + Implementation + DI-Registrierung).
user-invocable: true
allowed-tools: Read, Write, Edit, Grep, Glob
argument-hint: "<AppName> <ServiceName>"
---

# Neuen Service erstellen

Erstelle einen neuen Service fuer die App `$ARGUMENTS` nach den Projekt-Conventions.

## Argumente parsen

- Erstes Wort: App-Name (z.B. HandwerkerImperium)
- Rest: Service-Name (z.B. AchievementService)
- Falls Service-Name ohne "Service"-Suffix: automatisch anhaengen

## Vorgehen

### 1. Bestehende Patterns analysieren
- Lese `src/Apps/{App}/{App}.Shared/Services/` fuer bestehende Services
- Lese `src/Apps/{App}/{App}.Shared/App.axaml.cs` fuer DI-Registrierung
- Identifiziere das Pattern: Interface-Prefix `I`, Singleton-Registrierung, Constructor Injection

### 2. Interface erstellen
- Datei: `src/Apps/{App}/{App}.Shared/Services/Interfaces/I{ServiceName}.cs`
- Namespace: `{App}.Shared.Services`
- Oeffentliche Methoden als async Task wo sinnvoll
- Deutsche XML-Kommentare

### 3. Implementation erstellen
- Datei: `src/Apps/{App}/{App}.Shared/Services/{ServiceName}.cs`
- Namespace: `{App}.Shared.Services`
- Implementiert das Interface
- Constructor Injection fuer Abhaengigkeiten
- SemaphoreSlim fuer Thread-Safety bei async-Methoden
- Deutsche Kommentare

### 4. DI-Registrierung
- In `App.axaml.cs` als Singleton registrieren:
  ```csharp
  services.AddSingleton<I{ServiceName}, {ServiceName}>();
  ```
- Alphabetisch einsortieren

### 5. Zusammenfassung
- Erstellte Dateien auflisten
- Naechste Schritte vorschlagen (ViewModel-Integration, RESX-Keys)
