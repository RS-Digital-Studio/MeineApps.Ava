# RechnerPlus Avalonia

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Uebersicht

Scientific Calculator mit Unit Converter - werbefrei, kostenlos.

**Version:** 2.0.0
**Package:** com.meineapps.rechnerplus
**Werbung:** Keine
**Status:** Im geschlossenen Test

## App-spezifische Features

### Calculator
- Basic + Scientific Mode (Trigonometrie, Logarithmen, Potenzen)
- Memory-Funktionen (M+, M-, MR, MC)
- Berechnungsverlauf (Bottom-Sheet, Swipe-Up/Down, max 100 Eintraege)
- Keyboard-Support (Desktop): Ziffern, Operatoren, Enter, Backspace, Escape, Shift+8 fuer Multiplikation
- Floating Text Overlay (Game Juice): Ergebnis schwebt nach oben bei Berechnung

### Converter
8 Kategorien mit offset-basierter Temperature-Konvertierung:
1. Length (m, km, mi, ft, in, cm, mm, yd)
2. Mass (kg, g, lb, oz, mg, t)
3. Temperature (C, F, K) - offset-basiert
4. Time (s, min, h, d, wk)
5. Volume (L, mL, gal, qt, pt, fl oz, cup)
6. Area (m2, km2, ha, ft2, in2, ac, yd2)
7. Speed (m/s, km/h, mph, kn)
8. Data (B, KB, MB, GB, TB)

## Besondere Implementierungen

### Temperature-Konvertierung (Offset-Fix)
```csharp
// Offset-basierte Formel (nicht nur Faktor-basiert)
baseValue = value * ToBase + Offset
// Celsius: ToBase=1, Offset=0 (Referenz)
// Fahrenheit: ToBase=5/9, Offset=-32*5/9
// Kelvin: ToBase=1, Offset=-273.15
```

### History-Integration (persistent)
- `IHistoryService` (aus MeineApps.CalcLib) als Singleton
- **Persistenz**: Verlauf wird per IPreferencesService als JSON gespeichert und beim Start geladen
- CalculatorViewModel: `IsHistoryVisible`, `HistoryEntries`, Show/Hide/Clear/SelectHistoryEntry Commands
- MainView: Bottom-Sheet Overlay mit Backdrop, Slide-Animation (TransformOperationsTransition)
- Swipe-Gesten in MainView.axaml.cs: Up=ShowHistory, Down=HideHistory (nur im Calculator-Tab)
- "Verlauf löschen"-Button im History-Panel (Grid.Row="3")

### Floating Text (Game Juice)
- CalculatorViewModel feuert `FloatingTextRequested` Event nach Calculate()
- MainView.axaml.cs: `OnFloatingText` ruft `FloatingTextOverlay.ShowFloatingText` auf
- Farbe: Indigo (#6366F1), FontSize 14, Position 30%/30% des Canvas

### Keyboard (CalculatorView.axaml.cs)
- KeyDown-Handler auf UserControl (Focusable=true)
- Mappings: Shift+8 = Multiplikation, OemPlus ohne Shift = Equals, OemComma/OemPeriod = Dezimalpunkt

## App-spezifische Abhaengigkeiten

- **MeineApps.CalcLib** - Calculator Engine + ExpressionParser + IHistoryService

## Wichtige Fixes

- **Konsekutive Operatoren (11.02.2026)**: "5 + × 3" ersetzte Operator korrekt statt "0" einzufügen
- **Operator nach Klammer (11.02.2026)**: "(5+3) × 2" fügt keinen "0" mehr zwischen ")" und "×" ein
- **= nach Klammer (11.02.2026)**: "(5+3) =" evaluiert korrekt ohne "0" anzuhängen
- **= nach Operator (11.02.2026)**: "5 + =" entfernt trailing Operator statt "0" als Operand zu nutzen
- **Verlauf-Persistenz (11.02.2026)**: History wird per JSON in IPreferencesService gespeichert
- **FormatResult lokalisiert**: "Error" durch `_localization.GetString("Error")` ersetzt
- **SelectHistoryEntry**: ClearError() hinzugefuegt - HasError-Flag wird beim History-Eintrag zurueckgesetzt
- **Tan() Validation**: Math.Tan()-Ergebnis > 1e15 wird als undefiniert erkannt
- **km/h Precision**: Speed-Faktor von 0.277778 auf `1.0 / 3.6` (exakter)
