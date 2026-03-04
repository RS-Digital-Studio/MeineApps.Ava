# FitnessRechner "VitalOS" Design-Upgrade - Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** FitnessRechner von einer funktionalen Utility-App zu einem visuell beeindruckenden "VitalOS" Medical Premium Dashboard upgraden - mit Full SkiaSharp Immersion (animierter Hintergrund, SkiaSharp Tab-Bar, Vital Signs Hero, holografische Cards).

**Architecture:** Instance-basierte SkiaSharp-Renderer (wie HandwerkerImperium GameBackgroundRenderer) mit 20fps Render-Loop via DispatcherTimer. Struct-basierte Partikel-Pools (0 GC). Gecachte SKPaint/SKPath/SKShader. MainView.axaml wird um SKCanvasViews erweitert (Background + TabBar). HomeView bekommt VitalSignsHero als SKCanvasView. Bestehende XAML-Interaktion (Inputs, ScrollViewer) bleibt erhalten, nur visuelle Hintergründe werden SkiaSharp.

**Tech Stack:** SkiaSharp 3.119.2, Avalonia.Labs.Controls (SKCanvasView), DispatcherTimer, Struct-Pools

**Design-Dokument:** `docs/plans/2026-03-04-fitnessrechner-vitalos-design.md`

---

## Referenz: Projekt-Patterns

**IMMER einhalten:**
- `canvas.LocalClipBounds` statt `e.Info.Width/Height` (DPI!)
- `InvalidateSurface()` statt `InvalidateVisual()`
- SKPaint als readonly Instanzfelder, NICHT `using var` im Render-Loop
- SKPath mit `.Reset()` wiederverwenden, NICHT `new SKPath()` pro Frame
- SKShader cachen mit Bounds-Check (nur neu erstellen wenn Größe sich ändert)
- Partikel als `struct` Array mit `ref var p = ref _particles[i]`
- Alle Renderer implementieren `IDisposable` (Paints, Paths, Shaders, MaskFilters)
- `xmlns:skia="using:Avalonia.Labs.Controls"` für SKCanvasView im XAML
- Touch: `e.GetPosition(canvas)` → DPI-Skalierung → SkiaSharp-Koordinaten

**Farbkonstanten (wiederverwenden in allen Renderern):**
```csharp
// In jedem Renderer oder als shared static class
private static readonly SKColor ColorCyan = new(0x06, 0xB6, 0xD4);
private static readonly SKColor ColorTeal = new(0x14, 0xB8, 0xA6);
private static readonly SKColor ColorNavyDeep = new(0x0A, 0x1A, 0x2E);
private static readonly SKColor ColorNavyDark = new(0x05, 0x10, 0x20);
private static readonly SKColor ColorSurface = new(0x0F, 0x1D, 0x32);
private static readonly SKColor ColorGrid = new(0x0E, 0x74, 0x90);
private static readonly SKColor ColorWeightPurple = new(0x8B, 0x5C, 0xF6);
private static readonly SKColor ColorBmiBlue = new(0x3B, 0x82, 0xF6);
private static readonly SKColor ColorWaterGreen = new(0x22, 0xC5, 0x5E);
private static readonly SKColor ColorCalorieAmber = new(0xF5, 0x9E, 0x0B);
private static readonly SKColor ColorCriticalRed = new(0xEF, 0x44, 0x44);
private static readonly SKColor ColorTextPrimary = new(0xE2, 0xE8, 0xF0);
private static readonly SKColor ColorTextMuted = new(0x64, 0x74, 0x8B);
```

---

## Task 1: MedicalColors - Shared Farbkonstanten

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/MedicalColors.cs`

**Zweck:** Zentrale Farbkonstanten für alle VitalOS-Renderer. Verhindert Duplikation.

**Step 1: Erstelle MedicalColors.cs**

```csharp
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Zentrale Farbkonstanten für das VitalOS Medical-Premium Design.
/// Alle Renderer referenzieren diese Klasse statt eigene Farben zu definieren.
/// </summary>
public static class MedicalColors
{
    // Primär-Palette
    public static readonly SKColor Cyan = new(0x06, 0xB6, 0xD4);
    public static readonly SKColor CyanBright = new(0x22, 0xD3, 0xEE);
    public static readonly SKColor Teal = new(0x14, 0xB8, 0xA6);
    public static readonly SKColor ElectricBlue = new(0x3B, 0x82, 0xF6);

    // Hintergrund
    public static readonly SKColor NavyDeep = new(0x0A, 0x1A, 0x2E);
    public static readonly SKColor NavyDark = new(0x05, 0x10, 0x20);
    public static readonly SKColor NavyDarkest = new(0x03, 0x08, 0x10);
    public static readonly SKColor Surface = new(0x0F, 0x1D, 0x32);
    public static readonly SKColor SurfaceLight = new(0x1A, 0x2A, 0x4A);
    public static readonly SKColor TabBarBg = new(0x0D, 0x1B, 0x2A);

    // Grid
    public static readonly SKColor Grid = new(0x0E, 0x74, 0x90);

    // Feature-Farben (Quadranten)
    public static readonly SKColor WeightPurple = new(0x8B, 0x5C, 0xF6);
    public static readonly SKColor WeightPurpleDark = new(0x7C, 0x3A, 0xED);
    public static readonly SKColor BmiBlue = new(0x3B, 0x82, 0xF6);
    public static readonly SKColor BmiBlueLight = new(0x60, 0xA5, 0xFA);
    public static readonly SKColor WaterGreen = new(0x22, 0xC5, 0x5E);
    public static readonly SKColor WaterGreenDark = new(0x16, 0xA3, 0x4A);
    public static readonly SKColor CalorieAmber = new(0xF5, 0x9E, 0x0B);
    public static readonly SKColor CalorieAmberDark = new(0xD9, 0x77, 0x06);
    public static readonly SKColor CriticalRed = new(0xEF, 0x44, 0x44);

    // Text
    public static readonly SKColor TextPrimary = new(0xE2, 0xE8, 0xF0);
    public static readonly SKColor TextMuted = new(0x64, 0x74, 0x8B);
    public static readonly SKColor TextDimmed = new(0x47, 0x55, 0x69);

    // Herzschlag-Timing (72 BPM)
    public const float BeatsPerSecond = 1.2f;
    public const float BeatPeriod = 1f / BeatsPerSecond; // ~0.833s

    // EKG-Wellenform (24 Punkte, identisch zum Splash-Screen)
    public static readonly float[] EkgWave =
    {
        0f, 0f, 0.05f, 0.08f, 0.05f, 0f,              // P-Welle
        0f, -0.08f, 0.45f, -0.15f, 0f, 0f,             // QRS-Komplex
        0f, 0f, 0.03f, 0.06f, 0.08f, 0.06f, 0.03f, 0f, // T-Welle
        0f, 0f, 0f, 0f                                   // Baseline
    };
}
```

**Step 2: Build prüfen**

```bash
dotnet build src/Apps/FitnessRechner/FitnessRechner.Shared
```

**Step 3: Commit**

```bash
git add src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/MedicalColors.cs
git commit -m "feat(FitnessRechner): VitalOS MedicalColors - zentrale Farbkonstanten"
```

---

## Task 2: MedicalBackgroundRenderer - Animierter Hintergrund

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/MedicalBackgroundRenderer.cs`

**Zweck:** Immer sichtbarer animierter Hintergrund mit Medical Grid, EKG-Trace, Vital-Partikel und Vignette. Wie GameBackgroundRenderer in HandwerkerImperium.

**Step 1: Erstelle MedicalBackgroundRenderer.cs**

Architektur:
- Instance-basiert (nicht static) weil er State hält (Partikel, Render-Time, Shader-Cache)
- `IDisposable` für SKPaint/SKPath/SKShader Cleanup
- `Update(float deltaTime)` für Partikel + EKG-Animation
- `Render(SKCanvas canvas, SKRect bounds, float time)` für Zeichnung
- 5 Layer: Gradient → Grid → EKG-Trace → Partikel → Vignette

Partikel-Typen (Struct-Pool, 60 max):
```csharp
private enum VitalParticleType : byte { Cross, Heart, WaterDrop, Scale, Pulse }

private struct VitalParticle
{
    public float X, Y, VelocityX, VelocityY;
    public float Alpha, Size, Life, MaxLife, Phase;
    public VitalParticleType Type;
    public bool Active;
}
```

EKG-Trace:
- Nutzt `MedicalColors.EkgWave` (24 Punkte)
- Sweep von links nach rechts (synchron zu 72 BPM)
- Trail-Effekt: Links vom Sweep verblasst, rechts unsichtbar
- Glow-Punkt am Sweep-Ende (verstärkt bei QRS-Spike)

Grid:
- Feine Linien alle 40px (Cyan 8% Opacity)
- Dickere Linien alle 200px (Cyan 12% Opacity)
- Statisch (keine Animation nötig)

Vignette:
- Radialer Gradient: Mitte transparent → Ecken 60% schwarz
- Shader gecacht mit Bounds-Check

**Key Implementation Details:**
- Beat-Timer: `_beatTimer += deltaTime; if (_beatTimer >= BeatPeriod) { _beatTimer -= BeatPeriod; _beatGlow = 1f; EmitBeatParticles(3); }`
- Partikel spawnen nur bei Beat (3 pro Beat, nach oben steigend)
- Partikel-Formen: DrawCross (2 Linien), DrawHeart (Bezier), DrawWaterDrop (Tropfen-Form), DrawScale (Waagen-Form), DrawPulse (kleiner Kreis mit Glow)
- Alle Partikel: 15-25% max Alpha, langsam aufsteigend (VelocityY = -10 bis -20px/s)

**Step 2: Build prüfen**

```bash
dotnet build src/Apps/FitnessRechner/FitnessRechner.Shared
```

**Step 3: Commit**

```bash
git add src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/MedicalBackgroundRenderer.cs
git commit -m "feat(FitnessRechner): MedicalBackgroundRenderer - EKG, Grid, Partikel, Vignette"
```

---

## Task 3: MedicalTabBarRenderer - Holografische Tab-Bar

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/MedicalTabBarRenderer.cs`

**Zweck:** SkiaSharp Tab-Bar (64dp) mit Glas-Panel, Cyan-Glow, Tab-Icons, Active-Underline. Wie GameTabBarRenderer in HandwerkerImperium.

**Step 1: Erstelle MedicalTabBarRenderer.cs**

Architektur:
- Instance-basiert, `IDisposable`
- `Render(SKCanvas canvas, SKRect bounds, MedicalTabBarState state)` - pro Frame
- `HitTest(SKRect bounds, float skiaX, float skiaY) → int` - für Touch-Handling
- State-Struct: `ActiveTab`, `Labels[]`, `Time`, `TabSwitchTime`

Tab-Bar Design:
- Hintergrund: `TabBarBg` (#0D1B2A) bei 90% Opacity
- Obere Kante: 1px Gradient-Line (Cyan → Transparent → Cyan)
- 4 Tabs: HeartPulse, ChartLine, FoodApple, Cog (als SkiaSharp-Pfade gezeichnet)
- Aktiver Tab: Cyan Underline (3px, CornerRadius 1.5) + Glow (MaskFilter.Blur 4px) + Icon volle Helligkeit + Label
- Inaktive Tabs: Icon 40% Opacity, kein Label
- Touch-Feedback: Flash-Alpha beim Tap (schneller Fade 200ms)
- Tab-Switch: Bounce-Animation auf aktivem Icon (EaseOutBack, 200ms)

Icons als SKPath (prozedurale Vektor-Icons):
- HeartPulse: Herz + EKG-Linie durch die Mitte
- ChartLine: 3 Datenpunkte + Trendlinie
- FoodApple: Apfel-Silhouette
- Cog: 6-Zahn Zahnrad

Separator-Lines: Vertikale Linien zwischen Tabs (Cyan 5% Opacity, 60% der Höhe)

**Step 2: Build prüfen**

**Step 3: Commit**

```bash
git add src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/MedicalTabBarRenderer.cs
git commit -m "feat(FitnessRechner): MedicalTabBarRenderer - holografische Tab-Bar mit Glow"
```

---

## Task 4: MainView.axaml - SkiaSharp Background + Tab-Bar Integration

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/MainView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/MainView.axaml.cs`

**Zweck:** Background-Canvas und TabBar-Canvas in die MainView einbauen, Render-Loop starten, XAML Tab-Bar durch SkiaSharp ersetzen.

**Step 1: MainView.axaml modifizieren**

Änderungen:
- `xmlns:skia="using:Avalonia.Labs.Controls"` hinzufügen
- Background-Canvas als erstes Element (Grid.RowSpan="3", IsHitTestVisible="False")
- XAML Tab-Bar Border (Grid.Row="2") komplett entfernen
- SkiaSharp TabBar-Canvas als Grid.Row="2" hinzufügen (Height="64", PointerPressed Event)
- Grid RowDefinitions bleiben: `*,Auto,Auto` → `*,Auto,64`

Neue AXAML-Struktur (vereinfacht):
```xaml
<Grid RowDefinitions="*,Auto,64">
    <!-- SkiaSharp Background (hinter allem) -->
    <skia:SKCanvasView x:Name="BackgroundCanvas"
                        Grid.RowSpan="3"
                        IsHitTestVisible="False"
                        PaintSurface="OnBackgroundPaintSurface" />

    <!-- Content Area (Grid.Row="0") - bestehender Code bleibt -->
    <Panel Grid.Row="0"> ... </Panel>

    <!-- Ad Banner Spacer (Grid.Row="1") - bleibt -->
    <Border Grid.Row="1" ... />

    <!-- SkiaSharp Tab-Bar (Grid.Row="2") - ersetzt XAML Border -->
    <skia:SKCanvasView x:Name="TabBarCanvas"
                        Grid.Row="2"
                        Height="64"
                        PaintSurface="OnTabBarPaintSurface"
                        PointerPressed="OnTabBarPointerPressed" />

    <!-- Game Juice Overlays - bleiben (höchster ZIndex) -->
    <controls:FloatingTextOverlay ... />
    <controls:SkiaCelebrationOverlay ... />
</Grid>
```

**Step 2: MainView.axaml.cs modifizieren**

Neue Felder:
```csharp
private readonly MedicalBackgroundRenderer _backgroundRenderer = new();
private readonly MedicalTabBarRenderer _tabBarRenderer = new();
private DispatcherTimer? _renderTimer;
private float _renderTime;
private float _lastTabSwitchTime;
private SKRect _lastBackgroundBounds;
private SKRect _lastTabBarBounds;
```

Neue Methoden:
- `StartRenderTimer()` - 50ms DispatcherTimer (20fps)
- `OnRenderTimerTick()` - Update + InvalidateSurface für Background + TabBar
- `OnBackgroundPaintSurface()` - `_backgroundRenderer.Render(canvas, bounds, _renderTime)`
- `OnTabBarPaintSurface()` - State zusammenstellen + `_tabBarRenderer.Render(canvas, bounds, state)`
- `OnTabBarPointerPressed()` - DPI-Skalierung + HitTest + Tab-Wechsel-Command
- `GetActiveTabIndex()` - VM Properties → int (0-3)
- `ExecuteTabCommand(int index)` - VM Commands aufrufen

Lifecycle:
- `OnDataContextChanged`: Render-Timer starten (nach VM verfügbar)
- Dispose/Unloaded: Timer stoppen, Renderer disposen

**Step 3: Build prüfen**

**Step 4: Visuell verifizieren (Desktop)**

```bash
dotnet run --project src/Apps/FitnessRechner/FitnessRechner.Desktop
```

Erwartet: Animierter Navy-Hintergrund mit EKG-Trace, Grid-Lines und schwebenden Partikeln. Tab-Bar unten mit Cyan-Glow.

**Step 5: Commit**

```bash
git add src/Apps/FitnessRechner/FitnessRechner.Shared/Views/MainView.axaml
git add src/Apps/FitnessRechner/FitnessRechner.Shared/Views/MainView.axaml.cs
git commit -m "feat(FitnessRechner): MainView - SkiaSharp Background + TabBar Integration"
```

---

## Task 5: MedicalCardRenderer - Universeller Card-Hintergrund

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/MedicalCardRenderer.cs`

**Zweck:** Static Render-Methode die den holografischen Card-Hintergrund zeichnet. Wird von allen Dashboard-Cards und Views verwendet.

**Step 1: Erstelle MedicalCardRenderer.cs**

```csharp
public static class MedicalCardRenderer
{
    /// <summary>
    /// Zeichnet den holografischen Medical-Card-Hintergrund.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Card-Bounds</param>
    /// <param name="accentColor">Optionale Feature-Farbe für linken Akzent-Rand (null = kein Akzent)</param>
    /// <param name="cornerRadius">Corner-Radius (Standard 12)</param>
    public static void RenderCardBackground(SKCanvas canvas, SKRect bounds,
        SKColor? accentColor = null, float cornerRadius = 12f)
    {
        // 1. Hintergrund: Surface bei 85% Opacity, gerundet
        // 2. Obere Kante: 1px Gradient (Cyan → Transparent → Cyan)
        // 3. Ecken-Akzente: L-förmige Linien (HUD-Bracketing)
        // 4. Optional: Feature-Farbe am linken Rand (2px, 60% Höhe)
    }
}
```

Design-Details:
- Hintergrund: `MedicalColors.Surface` bei Alpha 217 (85%), RoundRect
- Obere Kante: LinearGradient (links: Cyan 40% → Mitte: Transparent → rechts: Cyan 40%), 1px StrokeWidth
- HUD-Bracketing: 4 L-förmige Linien in jeder Ecke (10px lang, Cyan 30%), StrokeWidth 1px
- Accent: Optionaler vertikaler Strich links (2px breit, 60% Höhe, Feature-Farbe bei 60% Alpha)

**WICHTIG:** Static Class mit lokalen Paint-Objekten (per `using var`) da NICHT im Render-Loop aufgerufen (nur bei Card-Erstellung/Resize). Alternativ: SKPaint als static readonly wenn Performance kritisch.

**Step 2: Build + Commit**

---

## Task 6: VitalSignsHeroRenderer - Dashboard Hero-Element

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/VitalSignsHeroRenderer.cs`

**Zweck:** Das Herzstück - kreisförmiger Vital Signs Monitor (280x280dp) mit 4 Quadranten, EKG-Ring, Tages-Score im Zentrum.

**Step 1: Erstelle VitalSignsHeroRenderer.cs**

Architektur:
- Instance-basiert, `IDisposable` (hat Animation-State)
- `Update(float deltaTime)` - EKG-Ring Animation, Center-Pulse, Data-Stream Partikel
- `Render(SKCanvas canvas, SKRect bounds, VitalSignsState state)` - Zeichnung
- `HitTest(SKRect bounds, float x, float y) → VitalQuadrant` - Tap-Erkennung

State-Struct:
```csharp
public struct VitalSignsState
{
    // Werte
    public float Weight;        // kg
    public float Bmi;           // BMI Wert
    public float WaterMl;       // ml heute
    public float WaterGoalMl;   // ml Ziel
    public float Calories;      // kcal heute
    public float CalorieGoal;   // kcal Ziel
    public int DailyScore;      // 0-100

    // Trends
    public int WeightTrend;     // -1, 0, +1 (↓, →, ↑)
    public string BmiCategory;  // "Normal", "Übergewicht" etc.

    // Animation
    public float Time;
    public bool HasData;
}

public enum VitalQuadrant { None, Weight, Bmi, Water, Calories, Center }
```

Render-Layer:
1. **Äußerer EKG-Ring**: EKG-Wellenform die kreisförmig um den Monitor läuft
   - `SKPath.AddArc()` für einzelne Segmente
   - Sweep synchron zum 72 BPM Beat
   - Trail-Effekt (Alpha Gradient im Uhrzeigersinn)
   - Glow am Sweep-Punkt

2. **4 Quadranten** (NW/NE/SW/SE):
   - Jeder Quadrant: Feature-Farbe bei 15% Alpha als Hintergrund-Segment
   - Icon (klein, 16dp) + Wert (groß, Bold) + Einheit/Info (klein, Muted)
   - Fortschritts-Arc für Wasser + Kalorien (innerer Ring-Segment)
   - Trend-Pfeil für Gewicht

3. **Kreuz-Trenner**: Horizontale + vertikale Linie durch Zentrum (Cyan 10%)

4. **Zentrum**: Score-Zahl (28pt, Bold, TextPrimary) mit pulsierendem Cyan-Ring
   - Pulse: Scale 1.0 → 1.03 im 72 BPM Rhythmus
   - Glow: MaskFilter.Blur, Alpha pulsierend

5. **Data-Stream Partikel** (optional, 8 max): Kleine Punkte die von Quadranten zum Zentrum fließen

HitTest:
```csharp
public VitalQuadrant HitTest(SKRect bounds, float x, float y)
{
    float cx = bounds.MidX;
    float cy = bounds.MidY;
    float dx = x - cx;
    float dy = y - cy;
    float dist = MathF.Sqrt(dx * dx + dy * dy);
    float radius = MathF.Min(bounds.Width, bounds.Height) / 2f;

    if (dist > radius) return VitalQuadrant.None;
    if (dist < radius * 0.25f) return VitalQuadrant.Center;

    // Quadrant per Winkel
    float angle = MathF.Atan2(dy, dx);
    if (angle < -MathF.PI / 2f) return VitalQuadrant.Weight;   // NW (oben-links)
    if (angle < 0) return VitalQuadrant.Bmi;                     // NE (oben-rechts)
    if (angle < MathF.PI / 2f) return VitalQuadrant.Calories;    // SE (unten-rechts)
    return VitalQuadrant.Water;                                    // SW (unten-links)
}
```

**Step 2: Build + Commit**

---

## Task 7: HomeView.axaml - VitalSignsHero Integration

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/HomeView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/HomeView.axaml.cs`

**Zweck:** VitalSignsHero ersetzt den Hero-Header + das 2x2 Dashboard-Grid. Die restlichen Cards (Challenge, Streak, etc.) bekommen MedicalCardRenderer-Hintergründe.

**Step 1: HomeView.axaml modifizieren**

Änderungen:
- `xmlns:skia="using:Avalonia.Labs.Controls"` hinzufügen
- Hero-Header Border (Green/Teal Gradient) entfernen
- Daily Score Ring Card entfernen
- XP/Level Bar Card vorerst beibehalten (wird in Task 11 ersetzt)
- Today Dashboard Card (2x2 Grid) entfernen
- Empty-State Border bleibt (wird nur bei keinen Daten gezeigt)
- NEU: VitalSignsHero SKCanvasView (280dp Height, zentriert)

```xaml
<!-- VitalOS Hero (ersetzt Header + Dashboard Grid) -->
<skia:SKCanvasView x:Name="VitalSignsCanvas"
                    Height="280" Margin="16,12"
                    IsVisible="{Binding HasDashboardData}"
                    PaintSurface="OnVitalSignsPaintSurface"
                    PointerPressed="OnVitalSignsPointerPressed" />
```

**Step 2: HomeView.axaml.cs modifizieren**

- VitalSignsHeroRenderer Instanz
- `OnVitalSignsPaintSurface()` - State aus ViewModel zusammenstellen + Render
- `OnVitalSignsPointerPressed()` - HitTest → Rechner öffnen oder Center-Action
- VitalSignsCanvas in den Render-Loop der MainView einbinden (per public Methode oder Event)

**WICHTIG:** HomeView hat keinen eigenen Render-Loop. Die MainView's Timer ruft `InvalidateSurface()` auf dem HomeView's VitalSignsCanvas auf. Dafür:
- HomeView exponiert `VitalSignsCanvas` als public Property
- MainView greift darauf zu wenn HomeTab aktiv ist

Alternativ (einfacher): HomeView registriert sich beim ViewModel für einen Timer-Tick Event.

**Step 3: Build + visuelle Verifikation + Commit**

---

## Task 8: QuickActionButtonRenderer - Premium Buttons

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/QuickActionButtonRenderer.cs`

**Zweck:** SkiaSharp-gerenderte Quick-Action Buttons (+kg, +250ml, +kcal) mit holografischem Rand und Pulse-Effekt.

**Step 1: Erstelle QuickActionButtonRenderer.cs**

Static Class mit Render-Methode:
```csharp
public static class QuickActionButtonRenderer
{
    public static void Render(SKCanvas canvas, SKRect bounds,
        string label, SKColor featureColor, float time, bool isPressed)
    {
        // 1. Hintergrund: Feature-Farbe bei 25% Alpha, RoundRect (CornerRadius 12)
        // 2. Rand: Feature-Farbe bei 60% Alpha, 1.5px, pulsierend (Opacity 40-80%, 3s Zyklus)
        // 3. Icon-Bereich: Links, Feature-Farbe bei 40% Alpha, Kreis
        // 4. Label: Weiß, 14pt, Bold, mittig
        // 5. Press-Effekt: Scale 0.95 (Canvas.Scale) + Feature-Farbe Flash (200ms)
    }
}
```

**Step 2: HomeView.axaml anpassen**

Die 3 XAML Gradient-Buttons durch 3 SKCanvasViews ersetzen:
```xaml
<Grid ColumnDefinitions="*,*,*" ColumnSpacing="10">
    <skia:SKCanvasView x:Name="BtnWeight" Height="48"
                        PaintSurface="OnQuickWeightPaint"
                        PointerPressed="OnQuickWeightPressed" />
    <skia:SKCanvasView x:Name="BtnWater" Height="48"
                        PaintSurface="OnQuickWaterPaint"
                        PointerPressed="OnQuickWaterPressed" />
    <skia:SKCanvasView x:Name="BtnCalories" Height="48"
                        PaintSurface="OnQuickCaloriesPaint"
                        PointerPressed="OnQuickCaloriesPressed" />
</Grid>
```

**Step 3: Build + Commit**

---

## Task 9: StreakCardRenderer + ChallengeCardRenderer + LevelProgressRenderer

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/StreakCardRenderer.cs`
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/ChallengeCardRenderer.cs`
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/LevelProgressRenderer.cs`

**Zweck:** Dashboard-Cards als SkiaSharp-Renderer im Medical-Design.

### StreakCardRenderer (Static)

```csharp
public static class StreakCardRenderer
{
    public static void Render(SKCanvas canvas, SKRect bounds,
        int currentStreak, int bestStreak, bool hasStreak, float time)
    {
        // 1. MedicalCardRenderer.RenderCardBackground() als Basis
        // 2. Links: Pulsierendes Herz-Icon (Beat-synchron, Amber→Rot Gradient)
        // 3. Mitte: "VITAL STREAK" (10pt, TextMuted) + Streak-Zahl (20pt, Bold)
        // 4. Rechts: Mini-EKG Trace (3 Zyklen, Amber, niedrige Amplitude)
        // 5. Best-Streak Badge oben rechts (Surface-Hintergrund, kleine Schrift)
    }
}
```

### ChallengeCardRenderer (Static)

```csharp
public static class ChallengeCardRenderer
{
    public static void Render(SKCanvas canvas, SKRect bounds,
        string title, float progress, int xpReward, bool isCompleted, float time)
    {
        // 1. Hintergrund: Indigo→Lila Gradient (#6366F1 → #8B5CF6) bei 85% Opacity
        // 2. Links: Challenge-Icon im holografischen Kreis (Cyan-Glow)
        // 3. Mitte: "DAILY MISSION" + Titel + Progress-Bar
        // 4. Progress-Bar: Medical-Style (Navy-Hintergrund, Cyan-Füllung, Scan-Line-Sweep)
        // 5. Rechts: XP-Badge mit Glow
        // 6. Completed: Grüner Checkmark-Overlay + Success-Glow
    }
}
```

### LevelProgressRenderer (Static)

Ersetzt die bestehende LinearProgressVisualization für XP/Level:
```csharp
public static class LevelProgressRenderer
{
    public static void Render(SKCanvas canvas, SKRect bounds,
        int level, float xpProgress, string xpText, float time)
    {
        // 1. Links: Level-Badge (holografischer Kreis, Cyan-Rand, Level-Zahl innen)
        // 2. Mitte: Progress-Bar (Navy-Hintergrund, Cyan→Teal Gradient-Füllung)
        //    - Scan-Line die über die Füllung gleitet (heller Streifen, 3s Zyklus)
        //    - Glow am Ende der Füllung
        // 3. Rechts: XP-Text (11pt, TextMuted)
    }
}
```

**Step 2: HomeView.axaml anpassen**

Streak-Card, Challenge-Card und XP-Bar durch SKCanvasViews ersetzen:
```xaml
<skia:SKCanvasView x:Name="LevelCanvas" Height="34" ... />
<skia:SKCanvasView x:Name="ChallengeCanvas" Height="72" ... />
<skia:SKCanvasView x:Name="StreakCanvas" Height="68" ... />
```

**Step 3: Build + Commit**

---

## Task 10: CalculatorHeaderRenderer - Medical Calculator Headers

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/CalculatorHeaderRenderer.cs`

**Zweck:** Medical-Style Header für alle 5 Rechner-Views. Ersetzt die farbigen Border-Header.

**Step 1: Erstelle CalculatorHeaderRenderer.cs**

```csharp
public static class CalculatorHeaderRenderer
{
    public static void Render(SKCanvas canvas, SKRect bounds,
        string title, SKColor featureColor, float time)
    {
        // 1. Hintergrund: Feature-Farbe Gradient (Feature → Feature Dark) + Medical Grid Overlay
        //    - Grid: Feine Linien (Feature-Farbe 15%), alle 30px
        // 2. Mini-EKG: Feature-farbige EKG-Welle (kleine Amplitude, über ganze Breite)
        //    - Sweep synchron zu 72 BPM
        //    - Nur 1 Zyklus, subtil
        // 3. Title: Weiß, 18pt, Bold, mittig
        //    - Subtiler Glow (MaskFilter.Blur 2px, weiß 30%)
        // 4. Back-Button Bereich: Links, holografischer Kreis (20dp)
        //    - Chevron-Left Icon (Pfad)
        //    - Cyan-Rand, Surface-Hintergrund
        // 5. Untere Kante: CornerRadius 0,0,24,24 (wie bestehend)
    }
}
```

**Step 2: Alle 5 Calculator Views modifizieren**

Bestehende Header-Borders in BmiView, CaloriesView, WaterView, BodyFatView, IdealWeightView:
- Aktuelles Pattern: `<Border CornerRadius="0,0,24,24" Padding="20" Background="{DynamicResource AccentBrush}">`
- Neues Pattern: SKCanvasView mit CalculatorHeaderRenderer + PointerPressed für Back-Button

```xaml
<skia:SKCanvasView x:Name="HeaderCanvas" Height="80"
                    PaintSurface="OnHeaderPaintSurface"
                    PointerPressed="OnHeaderPointerPressed" />
```

**Step 3: Build + Commit**

---

## Task 11: Bestehende Renderer Upgrades (BmiGauge, CalorieRing, BodyFat)

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/BmiGaugeRenderer.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/CalorieRingRenderer.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/BodyFatRenderer.cs`

**Zweck:** Medical-Ästhetik auf bestehende Renderer anwenden.

### BmiGaugeRenderer Upgrades:
- Medical Grid im Gauge-Hintergrund (feine Cyan-Linien, 8% Opacity)
- Holographic Glow auf der Nadel (MaskFilter.Blur, Feature-Farbe)
- Scan-Line-Sweep über den Gauge (horizontaler heller Streifen, 5s Zyklus)
- Zonen-Labels mit Medical-Font (TextMuted statt Standard)
- Neuer `time` Parameter für Animationen

### CalorieRingRenderer Upgrades:
- Pulsierender Glow auf äußerem Ring (72 BPM synchron, MaskFilter.Blur)
- Data-Stream Partikel zwischen den Ringen (4-6 kleine Punkte die kreisförmig fließen)
- Medical Grid im Hintergrund
- Neuer `time` Parameter

### BodyFatRenderer Upgrades:
- Holographische Cyan-Kontur um Körper-Silhouette (statt einfache Linien)
- Scan-Linie die vertikal über den Körper fährt (3s Zyklus, Cyan, subtil)
- Prozent-Ring mit Glow (wie bestehend, aber mit MaskFilter.Blur)
- Neuer `time` Parameter

**WICHTIG:** Alle 3 Renderer sind aktuell static classes ohne `time` Parameter. Die Signatur muss erweitert werden:
```csharp
// Alt:
public static void Render(SKCanvas canvas, SKRect bounds, float bmiValue, bool hasResult)
// Neu:
public static void Render(SKCanvas canvas, SKRect bounds, float bmiValue, bool hasResult, float time = 0f)
```

Default-Wert `0f` für Abwärtskompatibilität (bestehende Aufrufe ohne time funktionieren weiter, aber ohne Animation).

**Step: Build + Commit**

---

## Task 12: FoodSearchView + ProgressView Medical Upgrades

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/FoodSearchView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/ProgressView.axaml`

**Zweck:** Medical-Styling auf die restlichen Views anwenden.

### FoodSearchView Änderungen:
- Search-Bar Background: `MedicalColors.Surface` statt Standard
- Food-Item Cards: MedicalCardRenderer-Hintergrund via SKCanvasView (oder XAML-Annäherung mit Background/BorderBrush)
- Barcode-Button: Cyan-Glow Border
- Quick-Add Panel: Holografischer Rand (Cyan-Border 1px + Glow)
- **Pragmatischer Ansatz:** Da FoodSearch viele ItemsControl/DataTemplates hat, ist XAML-Styling hier sinnvoller als SkiaSharp. Stattdessen die Card/Border Styles anpassen:
  - Background → `#D90F1D32` (Surface 85%)
  - BorderBrush → Cyan bei 30% für aktive Elemente
  - Ecken-Akzente per XAML Border-Tricks

### ProgressView Änderungen:
- HealthTrendVisualization: Medical Grid als Hintergrund-Layer hinzufügen
  - Modifiziere die bestehende `HealthTrendVisualization.cs` in `src/UI/MeineApps.UI/`
  - ODER: Nur für FitnessRechner eine Wrapper-Methode die Grid vorher zeichnet
- Sub-Tab Navigation: Cyan Underline statt Primary (per DynamicResource Override)
- Chart-Bereich: Dunklerer Hintergrund (Navy statt Standard Surface)

**ACHTUNG:** HealthTrendVisualization ist in der Shared UI Library. Änderungen dort betreffen alle Apps. Besser: FitnessRechner-spezifischen Wrapper erstellen der Medical Grid vorher zeichnet.

**Step: Build + Commit**

---

## Task 13: AchievementsView Medical Upgrade

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/AchievementsView.axaml`

**Zweck:** Achievements Fullscreen-Overlay mit Medical-Styling.

Änderungen:
- Hintergrund: Navy Deep statt Standard BackgroundBrush
- Achievement-Cards: MedicalCardRenderer-Style (XAML-Annäherung)
  - Unlocked: Feature-Gradient mit Cyan-Glow Border
  - Locked: Surface-Hintergrund mit TextMuted Icon + ProgressBar in Cyan
- Header: Medical-Style mit Cyan-Akzenten
- Level-Badge: Holografischer Kreis

**Step: Build + Commit**

---

## Task 14: SettingsView Medical Upgrade

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/SettingsView.axaml`

**Zweck:** Settings-Seite mit Medical-Styling.

Änderungen:
- Theme-Selection Cards: Medical-Style Borders
- Toggle-Switches: Cyan-Akzent statt Standard Primary
- Section Headers: Cyan-Farbe, Medical-Font-Stil
- Hintergrund: Transparent (Background-Renderer scheint durch)

**Step: Build + Commit**

---

## Task 15: HomeView Render-Loop + Badge/Heatmap/Wochenvergleich Cards

**Files:**
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/HomeView.axaml`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/Views/HomeView.axaml.cs`

**Zweck:** Alle animierten Canvas-Elemente in der HomeView korrekt an den Render-Loop anbinden. Verbleibende Cards (Badges, Heatmap, Wochenvergleich, Evening Summary) mit Medical-Card-Style versehen.

Änderungen:
- Badge-Reihe: XAML-Borders mit Medical-Gradient statt Standard
- Heatmap: SKCanvasView mit Medical Grid Hintergrund
- Wochenvergleich: Medical-Card-Style
- Abend-Zusammenfassung: Medical-Card mit Cyan-Akzent

Render-Loop Integration:
- MainView's Timer invalidiert HomeView's animierte Canvases (VitalSigns, QuickButtons, Streak, Challenge, Level)
- Nur wenn HomeTab aktiv ist (Performance)

**Step: Build + Commit**

---

## Task 16: Finaler Polish + Build-Verifikation

**Files:**
- Modify: `src/Apps/FitnessRechner/CLAUDE.md` (App-Dokumentation aktualisieren)

**Step 1: Gesamte Solution bauen**
```bash
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln
```

**Step 2: Desktop visuell testen**
```bash
dotnet run --project src/Apps/FitnessRechner/FitnessRechner.Desktop
```

Checkliste:
- [ ] Animierter Hintergrund sichtbar (EKG-Trace, Grid, Partikel)
- [ ] Tab-Bar funktioniert (Touch, aktiver Tab, Cyan-Glow)
- [ ] VitalSignsHero zeigt Werte (4 Quadranten, Center-Score, EKG-Ring)
- [ ] Quick-Action Buttons funktionieren (+kg, +250ml, +kcal)
- [ ] Streak-Card im Medical-Design
- [ ] Challenge-Card im Medical-Design
- [ ] XP/Level-Bar im Medical-Design
- [ ] Calculator-Headers mit Medical-Grid + Mini-EKG
- [ ] BmiGauge mit Glow + Scan-Line
- [ ] CalorieRing mit Pulse + Partikel
- [ ] BodyFat mit Hologramm-Kontur + Scan-Line
- [ ] FoodSearch mit Medical-Card-Style
- [ ] ProgressView Charts mit Medical Grid
- [ ] Achievements mit Medical-Design
- [ ] Settings mit Cyan-Akzenten
- [ ] Performance: Smooth 20fps, kein Stutter
- [ ] Keine Build-Warnungen

**Step 3: AppChecker laufen lassen**
```bash
dotnet run --project tools/AppChecker FitnessRechner
```

**Step 4: CLAUDE.md aktualisieren**

FitnessRechner CLAUDE.md um VitalOS-Design-Informationen erweitern:
- Neue Renderer-Dateien dokumentieren
- MedicalColors referenzieren
- Render-Loop Architektur dokumentieren
- 72 BPM Herzschlag-Synchronisation dokumentieren

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(FitnessRechner): VitalOS Design-Upgrade komplett - Medical Premium Dashboard"
```

---

## Zusammenfassung: Neue Dateien

| # | Datei | Typ | Zweck |
|---|-------|-----|-------|
| 1 | `Graphics/MedicalColors.cs` | Static | Farben, EKG-Daten, Timing-Konstanten |
| 2 | `Graphics/MedicalBackgroundRenderer.cs` | Instance | Animierter Hintergrund (5 Layer) |
| 3 | `Graphics/MedicalTabBarRenderer.cs` | Instance | Holografische Tab-Bar |
| 4 | `Graphics/MedicalCardRenderer.cs` | Static | Universeller Card-Hintergrund |
| 5 | `Graphics/VitalSignsHeroRenderer.cs` | Instance | Dashboard Vital Signs Monitor |
| 6 | `Graphics/QuickActionButtonRenderer.cs` | Static | Premium Quick-Action Buttons |
| 7 | `Graphics/StreakCardRenderer.cs` | Static | Medical Streak-Anzeige |
| 8 | `Graphics/ChallengeCardRenderer.cs` | Static | Medical Challenge-Card |
| 9 | `Graphics/LevelProgressRenderer.cs` | Static | Medical XP/Level-Bar |
| 10 | `Graphics/CalculatorHeaderRenderer.cs` | Static | Medical Calculator Header |

## Modifizierte Dateien

| Datei | Änderung |
|-------|----------|
| `Views/MainView.axaml` | +SkiaSharp Background + TabBar, -XAML Tab-Bar |
| `Views/MainView.axaml.cs` | +Render-Loop, +Renderer-Instanzen, +Touch-Handling |
| `Views/HomeView.axaml` | +VitalSignsHero, +SkiaSharp Cards, -XAML Dashboard |
| `Views/HomeView.axaml.cs` | +Paint-Handler, +HitTest, +Render-Loop-Anbindung |
| `Views/FoodSearchView.axaml` | Medical-Style XAML Cards |
| `Views/ProgressView.axaml` | Medical Grid + Cyan-Akzente |
| `Views/AchievementsView.axaml` | Medical-Style Cards |
| `Views/SettingsView.axaml` | Cyan-Akzente |
| `Views/Calculators/*.axaml` | SkiaSharp Header (alle 5) |
| `Graphics/BmiGaugeRenderer.cs` | +Glow, +Grid, +Scan-Line, +time |
| `Graphics/CalorieRingRenderer.cs` | +Pulse, +Partikel, +time |
| `Graphics/BodyFatRenderer.cs` | +Hologramm, +Scan-Line, +time |
| `CLAUDE.md` | VitalOS Design-Dokumentation |
