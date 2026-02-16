# RechnerPlus Avalonia

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Übersicht

Scientific Calculator mit Unit Converter - werbefrei, kostenlos.

**Version:** 2.0.0
**Package:** com.meineapps.rechnerplus
**Werbung:** Keine
**Status:** Im geschlossenen Test

## App-spezifische Features

### Calculator
- Basic + Scientific Mode (Trigonometrie, Logarithmen, Potenzen)
- **INV-Button (2nd Function)**: Toggle im Mode-Selector, sin→sin⁻¹, cos→cos⁻¹, tan→tan⁻¹, log→10ˣ, ln→eˣ
- Memory-Funktionen (M+, M-, MR, MC, MS) mit ToolTip-Anzeige des Werts, **persistent** (überlebt App-Neustart)
- Berechnungsverlauf (Bottom-Sheet, Swipe-Up/Down, max 100 Einträge)
- **Einzelne History-Einträge löschen** (X-Button pro Eintrag)
- **Bestätigungsdialog** beim Löschen des gesamten Verlaufs
- **ANS-Taste**: Letztes Ergebnis einfügen, implizite Multiplikation nach ")"
- **Share-Button**: Teilen von Expression+Ergebnis (Ctrl+S, Share Intent auf Android)
- **Undo/Redo**: Ctrl+Z/Ctrl+Y, Stack-basiert (max 50 Zustände), SaveState vor jeder zustandsändernden Operation
- **Zahlenformat konfigurierbar**: US (1,234.56) oder EU (1.234,56) in Settings wählbar
- Keyboard-Support (Desktop): Ziffern, Operatoren, Enter, Backspace, Escape, Shift+8/9/0, Ctrl+Z/Y
- Floating Text Overlay (Game Juice): Ergebnis schwebt nach oben bei Berechnung
- **Live-Preview**: Zeigt Zwischenergebnis grau unter dem Display bei jeder Eingabe
- **Operator-Highlight**: Aktiver Operator (÷×−+) wird visuell hervorgehoben
- **Swipe-to-Backspace**: Horizontaler Swipe nach links auf Display = Backspace
- **Landscape = Scientific**: Automatisch Scientific Mode im Querformat, 2-Spalten-Layout (Display+Scientific links, BasicGrid rechts)
- **Copy-Button im Display**: ContentCopy-Icon neben Backspace
- Wiederholtes "=" wiederholt letzte Operation (z.B. 5+3=== → 8, 11, 14)
- Implizite Multiplikation nach Klammern: (5+3)2 → (5+3) × 2 (sowohl im ViewModel als auch im ExpressionParser)
- Kontextuelles Prozent: 100+10% = 110 (bei ×/÷: nur /100)
- Auto-Close offener Klammern bei "="
- Smart-Parenthesis-Button "( )" wählt automatisch ( oder ) je nach Kontext
- **Klammer-Validierung**: ")" wird ignoriert wenn keine offene Klammer existiert
- **Haptic Feedback (Android)**: Tick/Click/HeavyClick bei Button-Aktionen, **abschaltbar** in Settings (IHapticService.IsEnabled)
- **Double-Back-to-Exit (Android)**: Zurücktaste navigiert intern (History→Tab→Rechner), erst 2x schnell drücken schließt App. Logik komplett im MainViewModel (`HandleBackPressed()` + `ExitHintRequested` Event), MainActivity ruft nur VM auf
- **Tausender-Trennzeichen**: Display zeigt `1,000,000` statt `1000000` (RawDisplay ohne Kommas für Berechnungen)
- **Responsive Schriftgröße**: DisplayFontSize passt sich an Zahlenlänge an (52→42→34→26→20)
- **Startup-Modus persistent**: Basic/Scientific-Wahl wird gespeichert (nicht bei Auto-Landscape)
- **Button-Press-Animation**: scale(0.92) mit TransformOperationsTransition (80ms) + BrushTransition (150ms)
- **Dezimalstellen-Einstellung**: Auto oder 0-10 feste Stellen (in Settings konfigurierbar)
- **Floating-Point-Rounding**: Math.Round(value, 10) verhindert `0.30000000000000004`
- **Expression Syntax-Highlighting**: ExpressionHighlightControl (Zahlen: TextPrimary, Operatoren: Primary+Bold, Klammern: Muted)
- **Ergebnis-Animation**: CalculationCompleted Event → Display Fade+Scale (0.3→1, 0.96→1) + Equals-Button Weiß-Flash (100ms)
- **Display-Gradient**: Theme-spezifischer Gradient-Hintergrund (DisplayGradientBrush)
- **Equals-Gradient**: Theme-spezifischer Gradient (EqualsGradientBrush) statt einfarbig
- **Operator-Glow**: Aktiver Operator mit PrimaryBrush-Background + Border-Highlight
- **Digit-Buttons**: Fast-transparente DigitButtonBrush (Glassmorphism-Effekt)
- **Scientific-Panel Slide**: Opacity+MaxHeight Animation statt IsVisible (smooth Ein-/Ausblenden)
- **Memory-Row Animation**: Opacity 0.4→1 wenn Memory aktiv (DoubleTransition)
- **Mini-History**: Letzte 2 Berechnungen als Chips unter Display (tappbar)
- **Gruppierter Verlauf**: Einträge nach Heute/Gestern/Älter gruppiert
- **Onboarding**: 3 Tooltips beim ersten Start (Swipe-Delete, Swipe-History, Scientific-Mode)
- **Converter Swap-Animation**: Swap-Button rotiert 180° bei jedem Klick

### Converter
11 Kategorien mit offset-basierter Temperature-Konvertierung:
1. Length (m, km, mi, ft, in, cm, mm, yd, nmi, µm)
2. Mass (kg, g, lb, oz, mg, t, st)
3. Temperature (C, F, K) - offset-basiert
4. Time (s, min, h, d, wk)
5. Volume (L, mL, gal, qt, pt, fl oz, cup, tbsp, tsp)
6. Area (m², km², ha, ft², ac)
7. Speed (m/s, km/h, mph, kn)
8. Data (B, KB, MB, GB, TB, bit)
9. Energy (J, kJ, cal, kcal, Wh, kWh, BTU)
10. Pressure (Pa, kPa, bar, atm, psi, mmHg)
11. Angle (°, rad, gon, tr, ′, ″)
- **Copy-Button** neben Result-Anzeige

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
- CalculatorViewModel: `IsHistoryVisible`, `HistoryEntries`, Show/Hide/Clear/Delete/SelectHistoryEntry Commands
- MainView: Bottom-Sheet Overlay mit Backdrop, Slide-Animation (TransformOperationsTransition)
- Swipe-Gesten in MainView.axaml.cs: Up=ShowHistory, Down=HideHistory (nur im Calculator-Tab)
- **Gruppierung**: HistoryGroup record (Heute/Gestern/Älter), GroupedHistory Property
- **Mini-History**: RecentHistory (letzte 2 Einträge) als Chips unter CalculatorView
- "Verlauf löschen"-Button mit Bestätigungsdialog (ShowClearHistoryConfirm)

### Floating Text (Game Juice)
- CalculatorViewModel feuert `FloatingTextRequested` Event nach Calculate()
- MainView.axaml.cs: `OnFloatingText` ruft `FloatingTextOverlay.ShowFloatingText` auf
- Farbe: Indigo (#6366F1), FontSize 14, Position 30%/30% des Canvas

### Clipboard (Event-basiert)
- `ClipboardCopyRequested` / `ClipboardPasteRequested` vom VM
- View nutzt `TopLevel.GetTopLevel(this)?.Clipboard` (Avalonia-API)
- Ctrl+C / Ctrl+V via KeyDown-Handler
- Sauberes Abmelden bei DataContext-Wechsel (kein Memory Leak)

### Keyboard (CalculatorView.axaml.cs)
- KeyDown-Handler auf UserControl (Focusable=true)
- Mappings: Shift+8 = Multiplikation, Shift+9 = (, Shift+0 = ), OemPlus ohne Shift = Equals, OemComma/OemPeriod = Dezimalpunkt
- Ctrl+C = Kopieren, Ctrl+V = Einfügen, Ctrl+S = Teilen, Ctrl+Z = Undo, Ctrl+Y = Redo

### Haptic Feedback
- `IHapticService` Interface in `Services/IHapticService.cs`
- `NoOpHapticService` für Desktop (kein Feedback)
- `AndroidHapticService` in MainActivity.cs (VibrationEffect.EffectTick/Click/HeavyClick)
- Factory-Pattern: `App.HapticServiceFactory` wird von Android gesetzt

### Live-Preview
- `PreviewResult` Property, `UpdatePreview()` bei jeder Eingabe
- Versucht Expression+Display auszuwerten, zeigt Zwischenergebnis grau an
- Offene Klammern automatisch schließen, trailing Operatoren entfernen für Preview
- Nur angezeigt wenn sich der Wert vom Display unterscheidet

### Landscape-Layout (CalculatorView.axaml.cs, 13.02.2026)
- `OnSizeChanged` prueft Width > Height
- Automatischer Wechsel zu Scientific Mode mit `_autoSwitchedToScientific` Flag
- Zurueck zu Basic nur wenn automatisch gewechselt wurde (nicht manuell)
- **2-Spalten-Layout**: Spalte 0 (40%): Display+ModeSelector+ScientificPanel+Memory | Spalte 1 (60%): BasicGrid (RowSpan=4)
- RowDefinitions: `Auto,Auto,Auto,*` - letzte Zeile * fuer BasicGrid-Gesamthoehe
- Memory-Row: `VerticalAlignment.Bottom` im Landscape (zurueckgesetzt auf Stretch in Portrait)
- Kompaktere Landscape-Styles: CalcButton MinHeight 36, Function MinHeight 32, Memory MinHeight 28
- ModeSelector-Buttons: FontSize 11, Padding 8,3 im Landscape
- Scientific: ColumnSpacing/RowSpacing 4 statt 6

### Code-Qualität
- `TryParseDisplay()`: Zentrale Hilfsmethode, nutzt `RawDisplay` (ohne Tausender-Trennzeichen)
- `SetDisplayFromResult(double/CalculationResult)`: Zentrale Methode für Display+Error-Handling
- `RawDisplay`: Computed Property - entfernt Tausender-Trennzeichen und normalisiert Dezimaltrenner auf InvariantCulture
- `FormatResult()`: Math.Round(10) → Dezimalstellen-Setting → locale-abhängige Tausender-/Dezimaltrenner
- `RefreshNumberFormat()`: Aktualisiert Zahlenformat aus Preferences (aufgerufen beim Tab-Wechsel)
- Undo/Redo: `_undoStack`/`_redoStack` (Stack<CalculatorState>), SaveState() vor zustandsändernden Operationen
- `DisplayFontSize`: Automatisch angepasst bei Display-Änderungen (42/34/28/22/18)
- Wissenschaftliche Funktionen delegieren an `_engine` (besseres Error-Handling via CalculationResult)
- `CalculatorEngine.Factorial()` gibt `CalculationResult` zurück (statt double)
- `ExpressionParser.ProcessUnaryMinus()`: Konsekutive Minus-Zeichen (--5=5, ---5=-5)
- Alle ViewModels: IDisposable mit sauberem Event-Unsubscribe

### UI-Layout (12.02.2026)
- **Display-Card**: Expression + Copy-Icon + Backspace-Icon (oben rechts) + Memory-Indikator mit ToolTip (oben links) + Live-Preview
- **Mode-Selector**: Basic | Scientific | INV | RAD/DEG
- **Basic Mode Grid (4×5)**: `C | () | % | ÷` / `789×` / `456−` / `123+` / `± 0 . =`
- **Scientific Panel (3×5)**: Row 0: sin/cos/tan/log/ln (INV-abhängig), Row 1: ( ) x^y 1/x Ans, Row 2: π e x² √x x!
- **Memory Row (5)**: MC MR M+ M- MS
- CE entfernt (redundant mit C, nur noch per Delete-Taste)

### Expression-Schutz
- MaxExpressionLength = 200 Zeichen (verhindert Memory-Probleme)
- Klammer-Validierung: ")" nur wenn offene Klammern existieren

### DI-Registrierung (alle Singleton)
- CalculatorVM, ConverterVM, SettingsVM, MainViewModel → Singleton
- CalculatorEngine, ExpressionParser, IHistoryService → Singleton
- IHapticService → Singleton (Factory-Pattern für Android)

### ConverterVM Dispose
- IDisposable: Unsubscribe von LanguageChanged im Dispose()
- Erweiterte Einheiten: NauticalMile, Micrometer (Length), Stone (Mass), Tablespoon, Teaspoon (Volume), 6 Angle-Einheiten

### Display-Card Header (5 Spalten)
- Memory-Indikator (M) | Expression | Share-Icon | Copy-Icon | Backspace-Icon

## SkiaSharp-Visualisierungen (16.02.2026)

### Graphics-Ordner: `RechnerPlus.Shared/Graphics/`

| Datei | Beschreibung |
|-------|-------------|
| `VfdDisplayVisualization.cs` | 7-Segment VFD (Vacuum Fluorescent Display) mit Glow, Ghost-Segmenten, Flicker-Effekt |
| `ResultBurstVisualization.cs` | Expandierender Lichtring + 8 Partikel-Strahlen bei "="-Berechnung |

### VFD-Display
- Ersetzt das TextBlock-basierte Display durch SkiaSharp-gerenderte 7-Segment-Ziffern
- Cyan-Grün (#00FFB0) im Normalzustand, Rot (#FF4444) bei Fehler
- Ghost-Segmente (alle 7 Segmente dezent sichtbar) wie bei echten VFD-Röhren
- Glow-Effekt (SKMaskFilter.CreateBlur) auf aktiven Segmenten
- Subtiles Flicker (±3%, ~7Hz sin-basiert) simuliert echte Röhren-Schwankung
- Hintergrund: Fast-schwarz (#0A0A0A)
- Segment-Map: 0-9, Minus, E, r, Space; Punkt/Komma als leuchtender Dot
- Rechtsbündige Darstellung, Tausender-Trennzeichen als Dezimalpunkte

### Result-Burst
- Wird bei `CalculationCompleted` Event ausgelöst
- Expandierender Ring mit Glow + 8 gleichmäßig verteilte Partikel
- Cubic ease-out Easing, 500ms Dauer
- Transparentes Overlay über dem gesamten Display-Bereich

### Integration (CalculatorView)
- `xmlns:skia="using:Avalonia.Labs.Controls"` im AXAML
- Display-Bereich in Panel gewrappt (für Burst-Overlay)
- VFD-Canvas: Border mit `#0A0A0A` Hintergrund, 56px Höhe, CornerRadius 8
- Original-TextBlock unsichtbar (Opacity=0, für Layout-Referenz)
- VFD-Flicker-Timer: DispatcherTimer 33ms, startet bei OnAttachedToVisualTree
- Burst-Timer: DispatcherTimer 33ms, startet bei CalculationCompleted, stoppt nach 500ms
- PropertyChanged-Handler invalidiert VFD bei Display/HasError-Änderung

## App-spezifische Abhängigkeiten

- **MeineApps.CalcLib** - Calculator Engine + ExpressionParser + IHistoryService

## Wichtige Fixes

- **Konsekutive Operatoren (11.02.2026)**: "5 + × 3" ersetzte Operator korrekt statt "0" einzufügen
- **Operator nach Klammer (11.02.2026)**: "(5+3) × 2" fügt keinen "0" mehr zwischen ")" und "×" ein
- **= nach Klammer (11.02.2026)**: "(5+3) =" evaluiert korrekt ohne "0" anzuhängen
- **= nach Operator (11.02.2026)**: "5 + =" entfernt trailing Operator statt "0" als Operand zu nutzen
- **Verlauf-Persistenz (11.02.2026)**: History wird per JSON in IPreferencesService gespeichert
- **FormatResult lokalisiert**: "Error" durch `_localization.GetString("Error")` ersetzt
- **SelectHistoryEntry**: ClearError() hinzugefügt - HasError-Flag wird beim History-Eintrag zurückgesetzt
- **Tan() Validation**: Math.Tan()-Ergebnis > 1e15 wird als undefiniert erkannt (implementiert in CalculatorViewModel)
- **km/h Precision**: Speed-Faktor von 0.277778 auf `1.0 / 3.6` (exakter)
- **Lokalisierung (11.02.2026)**: 84 fehlende Akzente/Umlaute in ES/FR/IT/PT/DE resx korrigiert
- **Process.Start Android-Fix (11.02.2026)**: UriLauncher statt Process.Start (PlatformNotSupportedException auf Android)
- **Clipboard (11.02.2026)**: Copy/Paste via Event-Pattern + TopLevel.Clipboard API
- **ConverterVM Dispose (11.02.2026)**: IDisposable für LanguageChanged Unsubscribe
- **Expression-Altlast nach Fehler (12.02.2026)**: ShowError() leert jetzt Expression und setzt _isNewCalculation=true
- **Klammer-Bugs (12.02.2026)**: "))" fügte "0" ein → Fix: bei _isNewCalculation nur ")" ohne Display-Wert
- **Implizite Multiplikation (12.02.2026)**: Zahl/Dezimalpunkt/"(" nach ")" fügt automatisch "×" ein
- **Kontextuelles Prozent (12.02.2026)**: Bei +/−: Prozent vom Basiswert (100+10%=110)
- **Wiederholtes = (12.02.2026)**: _lastOperator/_lastOperand speichern letzte Operation
- **Auto-Close Klammern (12.02.2026)**: Offene Klammern werden bei "=" automatisch geschlossen
- **Power konsistent (12.02.2026)**: Power() delegiert an InputOperator("^")
- **UI-Redesign (12.02.2026)**: Layout wie Google/Samsung Calculator
- **Umfassendes Refactoring (12.02.2026)**: 8 Bugs, 3 Code-Qualitätsprobleme, 9 UX-Features, 2 mittlere Features
- **Runde 2 (12.02.2026)**: Floating-Point-Rounding, Memory-Persistenz, Parser Doppel-Minus, Factorial→CalculationResult, Tausender-Trennzeichen, Responsive FontSize, Startup-Modus persistent, Button-Animation, Energy+Pressure Converter, Dezimalstellen-Setting
- **Runde 3 (12.02.2026)**: 6 Bugs (Negate-Formatierung, Backspace-SciNotation, Factorial-Negativ, Leere-Klammern, SwapUnits-Doppelconvert, Swipe-Timing), ANS-Taste, Share-Button, Undo/Redo (Ctrl+Z/Y), Zahlenformat US/EU, Winkel-Konverter (11. Kategorie), History-Expression-Copy, erweiterte Einheiten (Seemeile, Mikrometer, Stone, Esslöffel, Teelöffel), Lokalisierung 13 Keys (6 Sprachen)
- **Runde 4 (12.02.2026)**: 20 Fixes nach Tiefenanalyse mit Google/Samsung/Apple/CalcKit/Microsoft Calculator Vergleich:
  - **6 kritische Bugs**: SelectHistoryEntry nutzt ResultValue (Locale-sicher), RefreshNumberFormat mit Parse-Validierung, Undo/Redo speichert _lastResult, Tan()-Validierung > 1e15, Converter akzeptiert EU-Komma, Parser implizite Multiplikation `(5+3)(2+1)`
  - **9 mittlere Bugs**: Backspace auf "0" setzt _isNewCalculation=true, EPSILON 1e-15 (statt 1e-10), Percent/InputDecimal UpdatePreview, InputDigit strip Tausender beim Append, Swipe nur auf Calculator-Tab, Parser Infinity-Check, Factorial Overflow-Check, Unbekannte Tokens als Fehler, ShareDisplay kontextabhängig
  - **5 UX**: Haptic-Toggle in Settings (IHapticService.IsEnabled), Equals-Button farbig hervorgehoben, Lokalisierung 2 Keys (6 Sprachen)
- **UI/UX Komplett-Upgrade (13.02.2026)**: 6-Phasen-Modernisierung:
  - Phase 1: Neue Theme-Ressourcen (EqualsGradientBrush, DisplayGradientBrush, OperatorGlowShadow, DigitButtonBrush, DigitButtonHoverBrush) in allen 4 Themes
  - Phase 2: Button-Grid (MinHeight 56, CornerRadius 16, Spacing 8, BrushTransition), Display-Gradient, ExpressionHighlightControl, größere Schrift (52/42/34/26/20), CalculationCompleted-Animation
  - Phase 3: Operator-Glow (PrimaryBrush+Border), Scientific-Panel Slide (Opacity+MaxHeight statt IsVisible), Memory-Row Opacity-Animation
  - Phase 4: Mini-History Chips, gruppierter Verlauf (Heute/Gestern/Älter), Empty-State Puls-Animation, 6 neue RESX-Keys (6 Sprachen)
  - Phase 5: Converter Swap-Rotation (180° pro Klick), Equals Weiß-Flash, Result FontSize 32
  - Phase 6: TooltipBubble Control (MeineApps.UI), Onboarding-Flow (3 Tooltips, Preference onboarding_shown_v2), 6 neue RESX-Keys (6 Sprachen)
