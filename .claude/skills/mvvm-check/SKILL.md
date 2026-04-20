---
name: mvvm-check
description: Schneller MVVM-Audit fuer eine App - findet Code-Behind-Service-Locator, fehlende Compiled Bindings, DataContext-Zuweisungen im Code-Behind.
user-invocable: true
allowed-tools: Read, Grep, Glob, Bash
argument-hint: "<AppName>"
---

# MVVM-Check

Fuehre einen schnellen, Grep-basierten MVVM-Audit fuer die App `$ARGUMENTS` aus. Ziel: unter 30 Sekunden. Fuer Pre-Commit.

## Vorgehen

### 1. App-Pfad validieren
- App-Name muss unter `src/Apps/` existieren
- Wenn kein Argument: alle Apps nacheinander pruefen

### 2. Kritische Anti-Patterns (Crash-Risiko)

```bash
# View-Side-DI (BingXBot v1.1.4 Crash-Pattern)
grep -rn "App.Services.GetRequiredService" src/Apps/{App}/{App}.Shared/Views/

# DataContext im Code-Behind zuweisen
grep -rn "DataContext = " src/Apps/{App}/{App}.Shared/Views/

# Direkte ViewModel-Instanzierung in Views
grep -rn "= new .*ViewModel" src/Apps/{App}/{App}.Shared/Views/
```

Jeder Treffer ist KRITISCH.

### 3. Compiled Bindings (Performance + Compile-Time-Safety)

```bash
# Views OHNE x:CompileBindings
for f in src/Apps/{App}/{App}.Shared/Views/*.axaml; do
  grep -L "x:CompileBindings" "$f"
done

# Views OHNE x:DataType
for f in src/Apps/{App}/{App}.Shared/Views/*.axaml; do
  grep -L "x:DataType" "$f"
done
```

Jeder Treffer ist HOCH.

### 4. ViewModel-Hygiene

```bash
# Manuell implementiertes INPC (sollte [ObservableProperty] sein)
grep -rn "public event PropertyChangedEventHandler" src/Apps/{App}/{App}.Shared/ViewModels/

# Manuell implementierte ICommand (sollte [RelayCommand] sein)
grep -rn ": ICommand" src/Apps/{App}/{App}.Shared/ViewModels/

# View-Typen in ViewModels (Layer-Verletzung)
grep -rn "using Avalonia.Controls" src/Apps/{App}/{App}.Shared/ViewModels/
```

Jeder Treffer ist MITTEL.

### 5. Service-Locator-Erkennung

```bash
# App.Services in Views oder ViewModels (sollte Constructor Injection sein)
grep -rn "App.Services\." src/Apps/{App}/{App}.Shared/ViewModels/
grep -rn "App.Services\." src/Apps/{App}/{App}.Shared/Views/
```

### 6. IAppPaths-Check (Server-Apps / Android)

```bash
# Direkter UserProfile-Zugriff (Android-Crash-Pattern)
grep -rn "Environment.SpecialFolder.UserProfile" src/Apps/{App}/
```

Jeder Treffer in Services ist HOCH (IAppPaths-Abstraktion nutzen).

### 7. Ausgabe

```
## MVVM-Check: {App}

### KRITISCH ({N})
- {Datei:Zeile}: App.Services.GetRequiredService im View-Ctor
- {Datei:Zeile}: DataContext = new ... im Code-Behind

### HOCH ({N})
- {Datei:Zeile}: Fehlendes x:CompileBindings
- {Datei:Zeile}: Fehlendes x:DataType

### MITTEL ({N})
- {Datei:Zeile}: Manuell implementiertes INPC
- {Datei:Zeile}: View-Typ in ViewModel

### Zusammenfassung
- {X} KRITISCH | {Y} HOCH | {Z} MITTEL
- Commit-ready: {JA/NEIN}
- Fuer automatische Fixes: mvvm-auditor starten
```

## Abgrenzung

- Fuer komplette MVVM-Analyse MIT automatischen Fixes -> Agent `mvvm-auditor`
- Fuer allgemeine Code-Review -> Agent `code-review`
- Fuer UI-Styling -> Agent `ui`
