# Graphics — VitalOS Medical Design System

App-eigene SkiaSharp-Visualisierungen. Konzept: „Apple Watch Health trifft Sci-Fi Medical
Console". XAML nur für native Form-Controls — alle visuellen Elemente via SkiaSharp.
Nutzt `SkiaThemeHelper` + Helpers aus [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) → dort dokumentiert.

---

## Dateien

| Datei | Typ | Zweck |
|-------|-----|-------|
| `MedicalColors.cs` | Static | Farb-Konstanten, EKG-Daten (24 Punkte), Timing-Konstanten (72 BPM = 1.2 Beats/s), Hintergrund-Gradient |
| `MedicalBackgroundRenderer.cs` | Instance | 5-Layer Hintergrund: Teal-Gradient, Grid, EKG-Linie, schwebende Partikel, Vignette |
| `MedicalTabBarRenderer.cs` | Instance | Holografische Tab-Bar (64dp, 4 Tabs, Cyan-Glow auf aktivem Tab) |
| `MedicalCardRenderer.cs` | Static | Universeller Card-Hintergrund: Surface `#D90F1D32` + HUD-Brackets + Akzent-Linie |
| `VitalSignsHeroRenderer.cs` | Instance | Dashboard Vital Signs Monitor (300dp, 4 Quadranten, EKG-Ring, Center-Score) |
| `QuickActionButtonRenderer.cs` | Static | Holografische Quick-Action Buttons: +kg (lila), +250ml (grün), +kcal (orange); 3s Puls-Animation, Press-Effekt Scale 0.95 |
| `StreakCardRenderer.cs` | Static | Medical Streak-Anzeige: pulsierendes Herz, Mini-EKG |
| `ChallengeCardRenderer.cs` | Static | Medical Challenge-Card: Indigo-Gradient, Scan-Line Progress |
| `LevelProgressRenderer.cs` | Static | Medical XP/Level-Bar: Cyan-Badge + Gradient + Scan-Line |
| `CalculatorHeaderRenderer.cs` | Static | Header für alle 5 Rechner: Feature-Gradient + Grid + EKG, holografischer Back-Button |
| `BmiGaugeRenderer.cs` | Static | BMI-Gauge: Medical Grid + Nadel-Glow + Scan-Line |
| `BodyFatRenderer.cs` | Static | Körperfett-Grafik: Cyan-Kontur + Scan-Linie + Prozent-Ring Glow |
| `CalorieRingRenderer.cs` | Static | Kalorien-Ringe: Medical Grid + 72BPM Glow + Data-Stream Partikel |
| `HealthTrendVisualization.cs` | Static | Catmull-Rom Spline mit Gradient-Fill, Target-Zones, Milestones |
| `WeeklyCaloriesBarVisualization.cs` | Static | Gradient-Balken mit Target-Linie |
| `FitnessRechnerSplashRenderer.cs` | Splash | EKG-Herzschlag-Splash. Erbt von `SplashRendererBase` |

---

## Farbpalette (`MedicalColors`)

| Farbe | Hex | Zweck |
|-------|-----|-------|
| Primär | `#06B6D4` Cyan | Akzent, EKG, Glow |
| Sekundär | `#14B8A6` Teal, `#3B82F6` Electric Blue | Hintergrund-Verläufe |
| Hintergrund | `#142832` → `#0A1824` | Teal Deep / Teal Dark |
| Surface | `#1E3844` | Cards |
| Card-Surface | `#D90F1D32` | Universeller Card-Hintergrund |
| Card-Border | `#1A06B6D4` (Standard), `#4D06B6D4` (holografisch) | Cyan-Glow |
| Weight | `#8B5CF6` Lila | Feature-Farbe |
| BMI | `#3B82F6` Blau | Feature-Farbe |
| Wasser | `#22C55E` Grün | Feature-Farbe |
| Kalorien | `#F59E0B` Amber | Feature-Farbe |

---

## EKG-Konfiguration

24-Punkt-Array in `MedicalColors.EkgData`: P-Welle + QRS-Komplex + T-Welle + Baseline.
Herzschlag: 72 BPM (1.2 Beats/Sekunde). Wird in `MedicalBackgroundRenderer` (floating EKG),
`VitalSignsHeroRenderer` (animierter Ring), `StreakCardRenderer` (Mini-EKG) und
`CalculatorHeaderRenderer` (statisch) verwendet.

---

## Render-Loop

| Pfad | Loop |
|------|------|
| `MainView` | DispatcherTimer 33ms (~30 FPS) → `_backgroundRenderer.Render(...)` + `_tabBarRenderer.Render(...)` |
| `HomeView` | `OnRenderTick(float time)` von `MainView` aufgerufen → invalidiert VitalSigns, QuickButtons, LevelProgress, ChallengeCard, StreakCard |
| Calculator-Views | Kein Render-Loop — `time = 0f` → statischer Snapshot |

---

## Static vs. Instance Renderer

- **Instance-Renderer** (`MedicalBackgroundRenderer`, `MedicalTabBarRenderer`,
  `VitalSignsHeroRenderer`): Halten eigenen Animationszustand (Partikelkoordinaten, EKG-Phase,
  Zeit-Akkumulator). Ein Renderer pro View-Instanz.
- **Static-Renderer** (alle anderen): Zustandslos — bekommen alle nötigen Werte als Parameter.
  Kein Leak-Risiko, kein Lifetime-Management.

---

## Gotchas

- **`MedicalCardRenderer` universell:** Wird von allen 5 Calculator-Views, ProgressView-Cards und
  HomeView-Cards aufgerufen. Änderungen wirken sich auf die gesamte App aus.
- **`HealthTrendVisualization` Catmull-Rom:** Benötigt mindestens 4 Datenpunkte für glatte
  Kurven. Bei < 4 Punkten auf lineare Interpolation fallback.
- **`CalculatorHeaderRenderer` Feature-Farbe als Parameter:** Jeder Rechner übergibt seine
  Feature-Farbe (`#8B5CF6` BMI, `#F59E0B` Kalorien, ...) — kein hardcoded Cyan.
