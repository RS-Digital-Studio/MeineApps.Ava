# Design: Verbesserungspaket FinanzRechner + FitnessRechner + HandwerkerRechner

**Datum:** 2026-02-28
**Scope:** ~35 Verbesserungen über 3 Apps + Shared Infrastructure
**Ansatz:** Shared-First, dann App-Sequential (HandwerkerRechner → FinanzRechner → FitnessRechner)

---

## Phase 0: Shared Infrastructure (MeineApps.UI)

### 0.1 EasingFunctions nach MeineApps.UI verschieben

`HandwerkerImperium.Shared/Graphics/EasingFunctions.cs` → `MeineApps.UI/SkiaSharp/EasingFunctions.cs`

Namespace: `MeineApps.UI.SkiaSharp`

HandwerkerImperium behält eine Weiterleitung (using alias) oder wird direkt auf den neuen Namespace umgestellt.

Funktionen: EaseOutCubic, EaseOutBack, EaseOutElastic, EaseOutBounce, Spring, EaseInOutQuint, Lerp, LerpClamped, SmoothStep, EaseInQuad, EaseOutQuad, PingPong.

### 0.2 AnimatedVisualizationBase

Neue abstrakte Basisklasse für SkiaSharp-Renderer die eine Einschwing-Animation beim ersten Render unterstützen.

```csharp
// MeineApps.UI/SkiaSharp/AnimatedVisualizationBase.cs
public abstract class AnimatedVisualizationBase
{
    private float _animationProgress = 0f;
    private bool _isAnimating;
    private DateTime _animationStart;

    public float AnimationDurationMs { get; set; } = 600f;
    public Func<float, float> EasingFunction { get; set; } = EasingFunctions.EaseOutCubic;
    public float AnimationProgress => _animationProgress;
    public bool IsAnimating => _isAnimating;

    public void StartAnimation() { ... }
    public void UpdateAnimation() { ... }

    // Unterklassen implementieren:
    protected abstract void OnRender(SKCanvas canvas, SKRect bounds, float progress);
}
```

**Nutzer:**
- HandwerkerRechner: Alle 13+ Visualisierungen (Fliesen legen sich, Stufen bauen sich auf, etc.)
- FinanzRechner: Donut-Segmente drehen rein, Balken wachsen hoch, Tachometer-Nadel
- FitnessRechner: BMI-Gauge, Kalorien-Ring, Wasser-Glas

---

## Phase 1: HandwerkerRechner

### 1.1 Beschreibungstexte auf Home-Cards

**Problem:** CalcTilesDescLabel etc. Properties sind im ViewModel vorhanden, aber nicht im AXAML angezeigt.

**Lösung:** Zweite TextBlock-Zeile unter dem Rechner-Namen in der Home-Card einfügen. FontSize=11, Muted-Farbe, MaxLines=2.

**Dateien:** `MainView.axaml` (Home-Tab DataTemplate)

### 1.2 CountUp-Animation auf Ergebnis-Hauptzahlen

**Problem:** Ergebnisse erscheinen schlagartig.

**Lösung:** CountUpBehavior (existiert in MeineApps.UI) auf die primären Ergebnis-TextBlocks in allen 11 Rechner-Views anbinden. TargetValue bindet an das berechnete Ergebnis, Format passend zum Rechner (F0 für Stückzahlen, F2 für m²/m³).

**Dateien:** Alle 11 Calculator-Views (TileView, WallpaperView, PaintView, etc.)

### 1.3 Live-Berechnung mit 300ms Debounce

**Problem:** Benutzer muss "Berechnen"-Button drücken. Konkurrenz-Apps rechnen live.

**Lösung:**
- Neuer `DebounceHelper` in MeineApps.UI (oder direkt in den VMs)
- PropertyChanged auf allen Input-Properties triggert Debounce-Timer
- Nach 300ms Inaktivität → CalculateCommand automatisch ausführen
- "Berechnen"-Button bleibt als explizite Alternative, "Reset" bleibt

**Architektur:** Jedes CalculatorViewModel bekommt:
```csharp
private readonly DebounceHelper _debounce = new(300);

partial void OnInputPropertyChanged() // für alle Input-Properties
{
    _debounce.Debounce(() => CalculateCommand.Execute(null));
}
```

**Dateien:** Alle 11 CalculatorViewModels, ggf. neuer DebounceHelper

### 1.4 SkiaSharp-Visualisierungen animiert

**Problem:** Alle 13 Renderer sind statisch - nach Berechnung erscheint die Visualisierung ohne Übergang.

**Lösung:** AnimatedVisualizationBase nutzen. Jeder Renderer bekommt `Render(canvas, bounds, progress)` wobei `progress` von 0→1 über 400-600ms läuft.

Beispiele:
- **StairsVisualization**: Stufen bauen sich von unten nach oben auf
- **TileVisualization**: Fliesen legen sich Reihe für Reihe
- **ConcreteVisualization**: Füllstand steigt
- **PaintVisualization**: Farbe füllt sich von unten
- **SolarVisualization**: Sonnenstrahlen erscheinen progressiv

**Dateien:** Alle Renderer in HandwerkerRechner.Shared/Graphics/

### 1.5 Ergebnis-Hauptzahl vergrößern

**Problem:** Primäres Ergebnis (z.B. "47 Fliesen") ist nur 16px Bold.

**Lösung:** Hero-Value Pattern:
- Hauptzahl: 28-32px, Bold, AccentBrush
- Sekundärwerte darunter: 14px, Muted
- Leichter Abstand zwischen Hero-Wert und Details

**Dateien:** Alle 11 Calculator-Views (Result-Section)

### 1.6 History-Tab als 4. Tab

**Problem:** Berechnungshistorie wird gespeichert aber nirgends angezeigt. Das Rewarded-Ad-Feature "extended_history" ist unsichtbar.

**Lösung:**
- Neuer 4. Tab "Verlauf" mit History-Icon
- HistoryView zeigt gespeicherte Berechnungen gruppiert nach Rechner-Typ
- Jeder Eintrag: Rechner-Icon + Typ + Datum + Hauptergebnis
- Tap auf Eintrag → lädt Berechnung in den Rechner (Inputs wiederhergestellt)
- Free: 5 Einträge, Extended (Ad): 30 Einträge

**Dateien:** Neues HistoryView.axaml, HistoryViewModel, MainView.axaml (Tab-Leiste), MainViewModel (Tab-Routing)

### 1.7 Wandflächen-Abzüge bei Farbe + Tapete

**Problem:** PaintCalculator nimmt direkte Fläche statt Raummaße. TapetenRechner hat keine Türen/Fenster-Abzüge.

**Lösung:**
- Optionale Abzugs-Sektion: "Abzüge (optional)" Expander
- Türen: Anzahl × Standard (0.8×2.0m) oder Custom-Maße
- Fenster: Anzahl × Standard (1.2×1.0m) oder Custom-Maße
- Berechnung: Wandfläche - Σ(Tür-/Fenster-Flächen)

**Dateien:** PaintCalculatorViewModel, WallpaperCalculatorViewModel, PaintView.axaml, WallpaperView.axaml

### 1.8 Materialkosten bei allen Rechnern

**Problem:** Nur Fliesen-Rechner hat Preisfeld.

**Lösung:** Optionales "Preis pro Einheit" Eingabefeld + "Geschätzte Kosten" im Ergebnis. Pro Rechner:
- Farbe: €/Liter → Gesamtkosten
- Tapete: €/Rolle → Gesamtkosten
- Laminat: €/m² → Gesamtkosten
- Beton: €/Sack → Gesamtkosten
- etc.

**Dateien:** Alle CalculatorViewModels + Views (Input + Result Sections)

### 1.9 Fugenmasse-Output im Fliesen-Rechner

**Problem:** Fugenmasse ergibt sich direkt aus Fliesen-Berechnung, wird aber nicht angezeigt.

**Lösung:** Zusätzliche Ergebnis-Zeile:
- Input: Fugenbreite (mm), Standard: 3mm
- Output: Fugenmasse in kg (Formel: Fläche × Fugenbreite × Faktor)

**Dateien:** TileCalculatorViewModel, TileView.axaml

### 1.10-1.14 Neue Rechner

Alle neuen Rechner folgen dem bestehenden Pattern:
- ViewModel mit ILocalizationService, Input-Properties, Calculate(), Reset()
- View mit farbigem Gradient-Header, ScrollContent, ActionBar
- SkiaSharp-Visualisierung (AnimatedVisualizationBase)
- RESX-Keys in 6 Sprachen
- Navigation in MainViewModel + Home-Card

| Rechner | Inputs | Outputs | Visualisierung |
|---------|--------|---------|----------------|
| **Putz** | Fläche, Schichtdicke (mm), Putztyp (Innen/Außen/Kalk/Gips) | kg Putz, Säcke, Kosten | Wandschnitt mit Putzschicht |
| **Estrich** | Fläche, Dicke (cm), Estrichtyp (Zement/Fließ/Anhydrit) | m³, kg, Säcke, Trocknungszeit | Bodenquerschnitt mit Schichten |
| **Dämmung** | Fläche, Ist-U-Wert, Soll-U-Wert, Dämmmaterial | Dämmdicke (cm), m², Kosten | Wandschnitt mit Dämmschicht |
| **Leitungsquerschnitt** | Strom (A), Länge (m), Spannung, Material (Cu/Al) | Mindest-Querschnitt (mm²), Spannungsabfall (%) | Kabel-Darstellung mit Querschnitt |
| **Fugenmasse** | Fläche, Fliesenmaße, Fugenbreite, Fugentiefe | kg Fugenmasse, Eimer, Kosten | Fliesenausschnitt mit markierten Fugen |

---

## Phase 2: FinanzRechner

### 2.1 Emoji-Icons in BudgetsView ersetzen

**Problem:** CategoryIcon nutzt Emojis - inkonsistent mit Material Icons im Rest der App.

**Lösung:** CategoryIcon von Emoji-String auf MaterialIconKind umstellen. Mapping:
- Food → Cart, Transport → Car, Housing → Home, Entertainment → Movie
- Shopping → Shopping, Health → Heart, Education → School, Bills → FileDocument, Other → DotsHorizontal
- Salary → Briefcase, Freelance → Laptop, Investment → TrendingUp, Gift → Gift, OtherIncome → CashPlus

**Dateien:** BudgetsView.axaml (DataTemplate), TransactionService oder Model (CategoryIcon Property)

### 2.2 CountUp-Animation auf Rechner-Ergebnissen

**Lösung:** Analog zu HandwerkerRechner - CountUpBehavior auf alle 6 Rechner-Ergebnis-TextBlocks.

**Dateien:** CompoundInterestView, SavingsPlanView, CreditView, AmortizationView, ReturnView, InflationView

### 2.3 Chart-Einschwing-Animationen

**Lösung:** AnimatedVisualizationBase für:
- DonutChartVisualization: Segmente drehen von 0° zum Endwinkel (800ms EaseOutCubic)
- StackedAreaVisualization: Y-Werte wachsen von 0 zum Endwert (600ms)
- AmortizationBarVisualization: Balken wachsen von unten (600ms, gestaffelt)
- BudgetGaugeVisualization: Nadel schwingt von 0 zum Wert (800ms EaseOutBack)

**Dateien:** Alle Visualization-Klassen in FinanzRechner.Shared/Graphics/

### 2.4 Budget-Tachometer Nadel animiert

**Lösung:** BudgetGaugeVisualization nutzt AnimatedVisualizationBase. Nadel-Winkel interpoliert mit EaseOutBack (leichtes Überschwingen) über 800ms.

**Dateien:** BudgetGaugeVisualization.cs

### 2.5-2.10 Neue Rechner

Alle folgen dem bestehenden Pattern (ViewModel, View, SkiaSharp-Chart, RESX, Navigation).

| Rechner | Inputs | Outputs | Chart-Typ |
|---------|--------|---------|-----------|
| **Netto-Brutto** | Brutto-Gehalt, Steuerklasse (1-6), Kirchensteuer (ja/nein), KV-Zusatzbeitrag | Netto, Lohnsteuer, Soli, KV, RV, AV, PV | Donut (Netto vs. Abzüge) |
| **Mehrwertsteuer** | Betrag, Richtung (Netto→Brutto / Brutto→Netto), Satz (19%/7%/Custom) | Netto, MwSt-Betrag, Brutto | Einfache Balken-Darstellung |
| **Mietrendite** | Kaufpreis, Nebenkosten (%), Monatskaltmiete, Hausgeld, Instandhaltung | Brutto-/Netto-Rendite (%), Cashflow/Monat | Donut (Einnahmen vs. Kosten) |
| **Kreditvergleich** | 2× (Kreditsumme, Zinssatz, Laufzeit, Sondertilgung) | Vergleichstabelle: Gesamtkosten, Monatsrate, Restschuld | StackedArea (beide Kredite übereinander) |
| **Break-Even** | Investition, monatliche Ersparnis, laufende Kosten | Amortisationszeit (Monate/Jahre), Gesamt-ROI | Linienchart (Investition vs. kumulative Ersparnis, Schnittpunkt markiert) |
| **Altersvorsorge** | Alter, Renteneintritt, Brutto, Sparrate, Rendite | Rentenlücke (€/Monat), Kapital bei Rente, Empfohlene Sparrate | StackedArea (gesetzliche Rente + private Vorsorge vs. Bedarf) |

### 2.11 Sparziele

**Neues Feature** - nicht ein Rechner, sondern ein Tracking-Feature.

**Model:**
```csharp
public class SavingsGoal
{
    public int Id { get; set; }
    public string Name { get; set; } // z.B. "Urlaub 2027"
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? Deadline { get; set; }
    public decimal MonthlyContribution { get; set; }
    public string IconKind { get; set; } // MaterialIconKind
    public string Color { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Features:**
- CRUD für Sparziele (Name, Betrag, Deadline, monatlicher Beitrag, Icon, Farbe)
- Fortschritts-Ring (SkiaGradientRing) pro Ziel
- Projektion: "Bei X €/Monat erreichst du dein Ziel am [Datum]"
- Dashboard-Widget: Top-3 Sparziele als Mini-Ringe auf dem Home-Tab
- Manuelle Einzahlungen oder automatisch aus Tracker-Transaktionen (Kategorie-Zuordnung)

**Dateien:** SavingsGoal.cs, SavingsGoalService.cs, SavingsGoalView.axaml, SavingsGoalViewModel.cs, HomeViewModel (Widget), RESX-Keys

---

## Phase 3: FitnessRechner

### 3.1 Kalorienfarbe in FoodLog korrigieren

**Problem:** `Foreground="{DynamicResource ErrorBrush}"` für Kalorien in TodayLog. Kalorien sind kein Fehler.

**Lösung:** `{DynamicResource TextSecondaryBrush}` oder `{DynamicResource WarningBrush}` (orange).

**Dateien:** FoodSearchView.axaml (TodayLog DataTemplate)

### 3.2 BMI-Gauge Zeiger-Animation

**Lösung:** SkiaGauge.cs (MeineApps.UI) hat bereits `NeedleAnimated` Property. Prüfen ob korrekt angebunden, ggf. EaseOutBack-Easing einbauen (600ms).

Falls FitnessRechner einen eigenen Gauge nutzt: AnimatedVisualizationBase-Integration.

**Dateien:** BmiGaugeVisualization.cs oder SkiaGauge-Integration

### 3.3 Kalorien-Ring Auffüll-Animation

**Lösung:** CalorieRingRenderer bekommt Animations-Support. `StartFillAnimation()` bei Datenwechsel → Ring füllt sich über 800ms von 0 zum aktuellen Wert.

**Dateien:** CalorieRingRenderer.cs, HomeView.axaml.cs (Trigger)

### 3.4 Wasser-Glas Wellen animieren

**Lösung:** SkiaWaterGlass.cs (MeineApps.UI) hat bereits `WaveEnabled` Property. Prüfen ob in FitnessRechner korrekt angebunden. Falls eigene Implementierung: Sinus-Wellen-Animation mit DispatcherTimer.

**Dateien:** WaterView.axaml, ggf. eigener WaterVisualization-Renderer

### 3.5 Food-Search Stagger-Fade

**Lösung:** StaggerFadeInBehavior (existiert in MeineApps.UI) auf Suchergebnis-Items anbinden. StaggerDelay=40, BaseDuration=200.

**Dateien:** FoodSearchView.axaml (Suchergebnis-DataTemplate)

### 3.6 Wasser-Portionsoptionen

**Problem:** Nur +250ml als Quick-Add.

**Lösung:** 4 Portions-Buttons statt einem:
- 200ml (Glas-Icon)
- 330ml (Flasche-Icon)
- 500ml (große Flasche-Icon)
- Custom (Eingabefeld)

**Dateien:** HomeView.axaml (Wasser-Quick-Add-Bereich), MainViewModel (neue Commands)

### 3.7 Mahlzeit-Templates

**Model:**
```csharp
public class MealTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } // z.B. "Typisches Frühstück"
    public MealType MealType { get; set; }
    public List<MealTemplateItem> Items { get; set; }
}
```

**Features:**
- "Als Template speichern" Button in der Tages-Übersicht
- Template-Auswahl in FoodSearch ("Meine Mahlzeiten" Tab/Chips)
- Template anwenden → alle Items auf einmal eintragen

**Dateien:** MealTemplate.cs, MealTemplateService.cs, FoodSearchView.axaml (Template-Bereich), FoodSearchViewModel

### 3.8 Körpermaße-Tracking

**Model:**
```csharp
public class BodyMeasurement
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal? Waist { get; set; } // cm
    public decimal? Hip { get; set; }
    public decimal? Chest { get; set; }
    public decimal? Biceps { get; set; }
    public decimal? Thigh { get; set; }
}
```

**Features:**
- Neuer Sub-Tab "Maße" im Progress-Tab
- Eingabe-Form für 5 Körpermaße
- Trend-Chart (Catmull-Rom Splines wie Gewichts-Chart)
- Taillien-Hüft-Verhältnis berechnen + anzeigen

**Dateien:** BodyMeasurement.cs, BodyMeasurementService.cs, ProgressViewModel (neuer Sub-Tab), ProgressView.axaml

### 3.9 Gewichts-Projektion

**Problem:** Kein "Wenn du so weiter machst, erreichst du dein Ziel am [Datum]".

**Lösung:**
- Lineare Regression über die letzten 14 Tage Gewichtsdaten
- Berechnung: Trend-Rate × Tage bis Zielgewicht
- Anzeige im Gewichts-Progress-Tab: "Prognose: [Datum]" oder "Bei aktuellem Tempo erreichst du dein Ziel in [X Wochen]"
- Strichlinie im Gewichts-Chart bis zum projizierten Zieldatum

**Dateien:** ProgressViewModel (Berechnungslogik), HealthTrendVisualization.cs (gestrichelte Projektionslinie)

### 3.10 Level-Up Full-Screen-Overlay

**Lösung:** RewardCeremonyRenderer-Pattern aus HandwerkerImperium adaptieren:
- Dunkler Backdrop, Scale-In Kreis mit Level-Nummer
- Titel "Level [N]!" + XP-Gain
- Confetti (SkiaCelebrationOverlay)
- 3s Dauer, Tap-to-Dismiss
- Trigger: LevelUp-Event im GamificationService

**Dateien:** Neuer LevelUpOverlay in HomeView oder als Shared-Overlay in MainView

### 3.11 Onboarding

**Lösung:** 3-4 Screen Onboarding beim ersten Start:
1. "Willkommen!" + Körpergröße eingeben
2. Aktuelles Gewicht eingeben
3. Ziel wählen (Abnehmen/Halten/Zunehmen) + Zielgewicht
4. Fertig → BMI berechnet, Kalorien-Ziel gesetzt, Wasser-Ziel gesetzt

**Dateien:** OnboardingView.axaml, OnboardingViewModel.cs, MainViewModel (First-Start-Check via Preferences)

---

## RESX-Aufwand

| App | Geschätzte neue Keys | × 6 Sprachen |
|-----|---------------------|--------------|
| HandwerkerRechner | ~120 (5 Rechner × ~20 Keys + UX) | 720 Einträge |
| FinanzRechner | ~150 (6 Rechner × ~20 Keys + Sparziele) | 900 Einträge |
| FitnessRechner | ~80 (Features + Onboarding) | 480 Einträge |
| **Gesamt** | **~350** | **~2100 Einträge** |

---

## Reihenfolge innerhalb jeder Phase

1. Models + Services (Datengrundlage)
2. ViewModels (Logik)
3. Views (UI)
4. SkiaSharp-Renderer (Visualisierungen)
5. RESX-Keys (Lokalisierung)
6. Navigation + DI-Registrierung
7. AppChecker laufen lassen
8. CLAUDE.md aktualisieren
