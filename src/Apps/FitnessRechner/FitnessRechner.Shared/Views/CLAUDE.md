# Views — AXAML-Views & UI-Patterns

Alle Views folgen ViewModel-First (DataContext per ViewLocator, `x:CompileBindings="True"`,
`x:DataType` auf View-Root). Generische MVVM-/View-Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Haupt-Container: SkiaSharp-Background + Tab-Bar (DispatcherTimer 33ms), ContentControl für Tab-Content, Ad-Spacer (56dp), Render-Loop-Delegation an HomeView |
| `HomeView.axaml(.cs)` | Dashboard: VitalSignsHero-Canvas, QuickAction-Buttons, StreakCard, ChallengeCard, LevelProgressBar — alle via SkiaSharp-Renderer |
| `ProgressView.axaml(.cs)` | 5 Sub-Tabs (Weight, BMI, BodyFat, Water/Calories, Aktivitäten) + Tracking-Einträge + Charts |
| `FoodSearchView.axaml(.cs)` | Suchfeld + Ergebnisliste (lokal + API), Quick-Add-Panel (Orange Gradient), Favoriten, Rezepte |
| `SettingsView.axaml(.cs)` | Benutzerprofil-Form, Erinnerungsschalter, Haptic/Sound-Toggle |
| `AchievementsView.axaml(.cs)` | Achievement-Grid (5 Kategorien), Fortschrittsanzeige |
| `ActivityView.axaml(.cs)` | Aktivitäts-Auswahl (30 Aktivitäten in 4 Kategorien), Dauer-Eingabe, History |
| `FastingView.axaml(.cs)` | Intervallfasten-Timer, Preset-Auswahl (16:8/18:6/20:4/Custom), History |
| `BarcodeScannerView.axaml(.cs)` | Scanner-Ergebnis-View (nicht die Camera-View — die ist `BarcodeScannerActivity` in Android-Projekt). Desktop: manuelle Texteingabe |
| `MainWindow.axaml(.cs)` | Desktop-only: Fenster-Container ohne eigene Logik |
| `Calculators/BmiView.axaml(.cs)` | BMI-Rechner: `CalculatorHeader`-Canvas + Eingabe-Form + `BmiGauge`-Canvas |
| `Calculators/CaloriesView.axaml(.cs)` | Kalorien-Rechner: `CalculatorHeader` + Mifflin-St-Jeor-Formular + `CalorieRing`-Canvas |
| `Calculators/WaterView.axaml(.cs)` | Wasserbedarf-Rechner: `CalculatorHeader` + Eingaben + Ergebnis-Anzeige |
| `Calculators/IdealWeightView.axaml(.cs)` | Idealgewicht: `CalculatorHeader` + Broca/Creff/BMI-Range |
| `Calculators/BodyFatView.axaml(.cs)` | Körperfett: `CalculatorHeader` + Maßangaben + `BodyFat`-Canvas |

---

## Render-Loop-Pattern

```
MainView (DispatcherTimer 33ms, ~30 FPS)
  ├─ _backgroundRenderer.Render(canvas, bounds, time)   → MedicalBackgroundRenderer
  ├─ _tabBarRenderer.Render(canvas, bounds, time)        → MedicalTabBarRenderer
  └─ _homeView?.OnRenderTick(time)                       → HomeView invalidiert VitalSigns, QuickButtons, Level, Challenge, Streak
```

Calculator-Views haben **keinen Render-Loop** — `time = 0f` → statischer Snapshot.
Animationen (Scan-Line, Glow) sind im Renderer vorbereitet, werden aber nicht getaktet.

---

## Touch-HitTest-Pattern (SkiaSharp-Views)

```csharp
// DPI-korrekte Koordinaten-Umrechnung:
var dpiScale = lastBounds.Width / canvas.Bounds.Width;
var skPoint = e.GetPosition(canvas) * dpiScale;
```

Spezialfälle:
- `VitalSignsHero`: `Math.Atan2` → Quadrant-Erkennung
- `TabBar`: `bounds.Width / 4` → Tab-Index
- `CalculatorHeader`: `IsBackButtonHit()` mit Radius-Toleranz

---

## Ad-Banner-Spacer

`MainView` hat `RowDefinitions="*,Auto,Auto"`:
- Row 0: Tab-Content
- Row 1: Ad-Spacer `56dp` (Adaptive Banner kann bis 60dp+ hoch sein)
- Row 2: Tab-Bar (`MedicalTabBarRenderer`, 64dp via SkiaSharp)

**ScrollViewer-Inhalte** in Sub-Views brauchen mindestens `Margin Bottom="60"` auf dem
direkten Kind-Element, damit Content nicht hinter dem Banner verschwindet.

---

## Medical-Styling (XAML-Cards)

Surface-Farbe: `#D90F1D32`. Cyan-Border Standard: `#1A06B6D4`. Holografisch: `#4D06B6D4`.

Alle interaktiven SkiaSharp-Canvas-Elemente (Tab-Bar, QuickAction-Buttons, CalculatorHeader)
implementieren `PointerPressed`/`PointerReleased` für Press-Feedback (Scale 0.95 oder Glow-Puls).
