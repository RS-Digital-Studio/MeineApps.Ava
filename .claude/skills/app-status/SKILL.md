---
name: app-status
description: Zeigt den aktuellen Status einer App - Version, letzte Commits, offene TODOs, Build-Status, RESX-Stand.
user-invocable: true
allowed-tools: Read, Grep, Glob, Bash
argument-hint: "<AppName>"
---

# App-Status

Zeige den aktuellen Status der App `$ARGUMENTS`.

## Vorgehen

### 1. Version ermitteln
- Lese `ApplicationDisplayVersion` und `ApplicationVersion` aus der Android .csproj
- Lese `<Version>` aus der Shared .csproj
- Vergleiche mit Haupt-CLAUDE.md Status-Tabelle

### 2. Letzte Aenderungen
- `git log --oneline -10 -- src/Apps/{App}/` fuer die letzten 10 Commits
- `git diff --stat HEAD -- src/Apps/{App}/` fuer uncommittete Aenderungen

### 3. Code-Metriken
- Anzahl .cs-Dateien und .axaml-Dateien
- Anzahl Services, ViewModels, Views
- Groesste Dateien (nach Zeilen)

### 4. Offene TODOs
- Suche nach `// TODO`, `// FIXME`, `// HACK` in der App

### 5. RESX-Kurzcheck
- Anzahl Keys in EN-RESX
- Schnellcheck ob alle 6 Sprachen gleich viele Keys haben

### 6. Build-Status
- `dotnet build src/Apps/{App}/{App}.Shared --verbosity quiet`
- Nur Fehler und relevante Warnungen zeigen

### 7. Ausgabe

```
## Status: {App} v{Version} (VersionCode {X})

### Aenderungen seit letztem Commit
{git diff --stat oder "Keine uncommitteten Aenderungen"}

### Letzte 10 Commits
{git log}

### Code-Metriken
- {X} .cs Dateien | {X} .axaml Dateien
- {X} Services | {X} ViewModels | {X} Views
- Groesste Datei: {Name} ({X} Zeilen)

### Offene TODOs: {X}
{Liste}

### RESX: {X} Keys, alle 6 Sprachen {vollstaendig/unvollstaendig}

### Build: {OK/FEHLER}
```
