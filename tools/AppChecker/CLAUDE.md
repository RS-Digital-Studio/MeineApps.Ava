# AppChecker

Automatisches Pruef-Tool fuer alle 8 Avalonia-Apps. Validiert Projektstruktur, Android-Konfiguration, UI, Lokalisierung, Code-Qualitaet und Assets.

## Verwendung

```bash
# Alle 8 Apps pruefen
dotnet run --project tools/AppChecker

# Einzelne App pruefen
dotnet run --project tools/AppChecker RechnerPlus

# Interaktiver Modus (ohne Argument): Auswahl per Menue
```

## Exit Codes

| Code | Bedeutung |
|------|-----------|
| 0 | Kein Fehler, keine Warnungen |
| 1 | Warnungen vorhanden |
| 2 | Fehler vorhanden |

## 10 Check-Kategorien

1. **Projekt/Build** - 3 Projekte vorhanden, ApplicationId/Version, RuntimeIdentifiers, RootNamespace, AvaloniaResource
2. **Android** - AndroidManifest, Permissions, Icon, AdMob APPLICATION_ID, Mipmap-Verzeichnisse, AdMob Lifecycle
3. **Avalonia/UI** - MaterialIconStyles registriert, IThemeService Aufloesung, ILocalizationService
4. **Lokalisierung** - Resources/Strings Ordner, AppStrings.resx + 5 Sprachen, Designer.cs, Key-Vergleich
5. **Code Quality** - Keine Debug.WriteLine, keine ungenutzten Exception-Variablen, InvalidateSurface statt InvalidateVisual, DateTimeStyles.RoundtripKind
6. **Assets** - icon.png vorhanden, MainWindow Icon-Referenz
7. **DI-Registrierung** - ConfigureServices, alle Services korrekt registriert, Constructor-Parameter Cross-Check
8. **VM-Verdrahtung** - MainViewModel, Tab Properties, Commands, LanguageChanged, MessageRequested, Child-VM UpdateLocalizedTexts
9. **View-Bindings** - x:DataType, xmlns:vm, DynamicResource statt StaticResource, View↔ViewModel Paar-Check
10. **Navigation** - Tab-Buttons mit Commands, Tab-Count Cross-Check (VM vs View), Overlay, Ad-Spacer

## Bekannte Apps

| App | Package-ID | Ads |
|-----|-----------|-----|
| RechnerPlus | com.meineapps.rechnerplus | Nein |
| ZeitManager | com.meineapps.zeitmanager | Nein |
| FinanzRechner | com.meineapps.finanzrechner | Ja |
| FitnessRechner | com.meineapps.fitnessrechner | Ja |
| HandwerkerRechner | com.meineapps.handwerkerrechner | Ja |
| WorkTimePro | com.meineapps.worktimepro | Ja |
| HandwerkerImperium | com.meineapps.handwerkerimperium | Ja |
| BomberBlast | org.rsdigital.bomberblast | Ja |

## Ausgabeformat

```
= AppName =
  [Kategorie]
    [PASS] Pruefung bestanden
    [INFO] Information
    [WARN] Warnung
    [FAIL] Fehler gefunden

= Summary =
  PASS: 42  INFO: 8  WARN: 3  FAIL: 0
```

Farbcodierung: Gruen=PASS, Cyan=INFO, Gelb=WARN, Rot=FAIL

## Abhaengigkeiten

- .NET 10.0
- Keine externen NuGet-Packages (nur System.Text.RegularExpressions, System.Xml.Linq)

## Architektur

- `Program.cs` - Entry Point, CLI-Parsing, App-Definitionen
- `AppChecker.cs` - Hauptklasse mit allen 10 Check-Methoden
- Jede Check-Methode arbeitet dateibasiert (liest .csproj, .axaml, .cs, .resx Dateien)
