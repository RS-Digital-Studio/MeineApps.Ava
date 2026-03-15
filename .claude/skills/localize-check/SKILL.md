---
name: localize-check
description: Prueft RESX-Vollstaendigkeit fuer eine App - fehlende Keys, leere Werte, Placeholder-Konsistenz ueber alle 6 Sprachen.
user-invocable: true
allowed-tools: Read, Grep, Glob, Bash
argument-hint: "<AppName>"
---

# Lokalisierungs-Check

Pruefe die RESX-Vollstaendigkeit fuer die App `$ARGUMENTS`.

## Vorgehen

### 1. RESX-Dateien finden
- Pfad: `src/Apps/{App}/{App}.Shared/Resources/Strings/`
- Erwartete Dateien: AppStrings.resx (EN), .de.resx, .es.resx, .fr.resx, .it.resx, .pt.resx

### 2. Keys vergleichen
- Basis: AppStrings.resx (EN) als Referenz
- Fuer jede Sprache pruefen:
  - Fehlende Keys (in EN vorhanden, in Sprache nicht)
  - Zusaetzliche Keys (in Sprache vorhanden, in EN nicht)
  - Leere Values (Key vorhanden aber Value leer)

### 3. Placeholder-Konsistenz
- Suche nach `{0}`, `{1}`, etc. in allen Sprachen
- Jeder Key muss in ALLEN 6 Sprachen die gleichen Placeholder haben
- Fehlende Placeholder = Crash bei string.Format!

### 4. Hardcodierte Strings finden
- Durchsuche AXAML-Dateien nach `Text="..."`, `Content="..."`, `Header="..."`
- Durchsuche C#-Dateien nach String-Literalen die dem User angezeigt werden
- Ignoriere: Pfade, Log-Messages, technische Strings

### 5. Ausgabe

```
## Lokalisierungs-Report: {App}

### RESX-Vollstaendigkeit
| Sprache | Keys | Fehlend | Leer | Status |
|---------|------|---------|------|--------|
| EN      | X    | -       | X    | OK/WARN |
| DE      | X    | X       | X    | OK/WARN |
| ...     |      |         |      |         |

### Placeholder-Probleme
- {Key}: EN hat {0},{1} aber FR hat nur {0}

### Hardcodierte Strings
- {Datei}:{Zeile}: "{String}"

### Zusammenfassung
- X fehlende Keys, Y leere Values, Z Placeholder-Probleme
```
