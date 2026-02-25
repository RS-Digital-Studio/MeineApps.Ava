---
name: build-check
description: Baut die Solution und fuehrt den AppChecker aus. Optionaler Parameter fuer eine einzelne App.
user-invocable: true
allowed-tools: Bash, Read, Grep, Glob
argument-hint: "[AppName]"
---

# Build + AppChecker

Fuehre folgende Schritte aus:

1. **Solution bauen:**
   ```
   dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln --verbosity quiet
   ```
   Zeige nur Fehler und Warnungen (nicht die bekannten CA1422/CA1416 Android-Warnungen).

2. **AppChecker ausfuehren:**
   - Wenn ein App-Name angegeben wurde (`$ARGUMENTS`): `dotnet run --project tools/AppChecker $ARGUMENTS`
   - Wenn kein Argument: `dotnet run --project tools/AppChecker`

3. **Ergebnis zusammenfassen:**
   - Build-Status (Fehler/Warnungen)
   - AppChecker-Ergebnisse (kritische Probleme hervorheben)
   - Wenn Fehler gefunden: Vorschlaege zur Behebung machen
