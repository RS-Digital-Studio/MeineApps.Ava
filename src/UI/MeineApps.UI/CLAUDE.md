# MeineApps.UI — Shared UI Component Library

Wiederverwendbare UI-Bausteine für alle 12 Avalonia-Apps: Custom Controls, Behaviors,
SkiaSharp-Visualisierungen, SkSL-GPU-Shader und das Loading-Pipeline-Framework. Stack, Build und
Paket-Versionen → Haupt-CLAUDE.md (`Directory.Packages.props`).

**Abhängigkeits-Grenze:** `MeineApps.UI` referenziert nur `MeineApps.Core.Ava` (für
`ThemeColors.axaml`, Converter) — NIE `MeineApps.Core.Premium.Ava` (Premium hängt von UI ab,
nicht umgekehrt). Build: `dotnet build src/UI/MeineApps.UI/MeineApps.UI.csproj`.

---

## Komponenten-Übersicht

### Controls (Avalonia)

| Datei | Klasse | Zweck |
|-------|--------|-------|
| `Card.axaml` | Styles | Card, Interactive, Outlined, Success/Warning/Error/Info |
| `ModernCardStyles.axaml` | Styles | StatsCard (Hover-Lift -2px), SettingsCard (-1px), EmptyPulse, SectionTitle |
| `FloatingActionButton.axaml` | Styles | FAB, FAB Mini, FAB Extended |
| `ButtonStyles.axaml` | Styles | Primary, Secondary, Outlined, Text, Icon, Sizes, Success, Danger |
| `TextStyles.axaml` | Styles | DisplayLarge, HeadlineMedium, TitleLarge, BodyMedium, Caption, Primary, Muted, Success, Error |
| `InputStyles.axaml` | Styles | TextBox, Filled TextBox, ComboBox, NumericUpDown |
| `EmptyStateView.axaml` | Custom Control | Icon + Titel + Untertitel + optionaler Action-Button |
| `WheelPicker.axaml` | Custom Control | Drum-Style Swipe-Zahlen-Picker (5 sichtbare Items, Wrap-Around) |
| `CircularProgress.cs` | Custom Control | Kreisförmiger Fortschrittsring (code-basiert, PenLineCap.Round) |
| `SplashOverlay.axaml` | Custom Control | App-Icon + Ladebar + Status-Text (echtes Preloading via Callback) |
| `SkiaLoadingSplash.axaml` | Custom Control | Vollbild-Splash mit appspezifischem SplashRendererBase |
| `FloatingTextOverlay.cs` | Custom Control | Schwebender Text (Game Juice, Canvas-basiert) |
| `SkiaCelebrationOverlay.cs` | Custom Control | Confetti-System (SkiaSharp, Glow, Sternformen, Blitz-Flash) |
| `CelebrationOverlay.cs` | Legacy | Border-basiertes Confetti — nicht mehr verwenden |
| `LottieAnimationView.cs` | Custom Control | Lottie-Wrapper mit OneShot-Modus + AnimationCompleted Event |
| `SvgIcon.cs` | ContentControl | SVG-Path-Icon, lädt StreamGeometry aus `Assets/Icons/AppIcons.axaml` per `Icon_{Kind}`-Key (`Kind`-Property, ViewBox-Auto-Scale) |
| `TooltipBubble.cs` | Custom Control | Onboarding-Tooltip (Tap-to-Dismiss, FadeIn/FadeOut via Transitions) |
| `AnimatedNumberText.cs` | Custom Control | TextBlock mit CubicEaseOut-Interpolation bei Wertänderung |
| `NotificationBadge.cs` | Custom Control | Runder Badge-Punkt (Count=0 unsichtbar, -1 = Punkt, >0 = Zahl + Bounce) |
| `SkeletonLoader.cs` | Custom Control | Shimmer-Platzhalter-Rechtecke während Ladevorgang |
| `HeatmapCalendar.cs` | Custom Control | GitHub-Style Aktivitäts-Heatmap (7×N Wochen, Level 0–4) |

### Behaviors (Xaml.Behaviors)

| Datei | Zweck |
|-------|-------|
| `TapScaleBehavior.cs` | Scale-Down (PressedScale=0.92) auf PointerPressed/Released |
| `FadeInBehavior.cs` | Fade-In + optionales Slide-from-Bottom (CubicEaseOut) |
| `StaggerFadeInBehavior.cs` | Gestaffelter Fade-In für Listen (Index-Auto-Erkennung) |
| `CountUpBehavior.cs` | Zählt TextBlock von 0 auf Zielwert (Format, Suffix, CultureName, UseSignedPrefix) |
| `SwipeToRevealBehavior.cs` | Swipe-to-Reveal (Delete-Layer, Spring-Back, ScrollViewer-kompatibel) |
| `LongPressBehavior.cs` | Long-Press (`DurationMs`, `Command`, `CommandParameter`); unterdrückt nachfolgenden Click bei Trigger |

### SkiaSharp Controls & Helpers

| Datei | Zweck |
|-------|-------|
| `SkiaThemeHelper.cs` | Konvertiert Avalonia-Theme-Farben zu `SKColor`, gecacht, einmalig `RefreshColors()` beim Start |
| `SkiaParticleSystem.cs` | Struct-basiertes Partikelsystem (kein GC), Presets: Confetti, Sparkle, WaterDrop, Glow, Coin, Firework |
| `DonutChartVisualization.cs` | Donut-Chart (Gradient-Segmente, innerer Schatten, Glow, 3D-Highlight, Legende) |
| `LinearProgressVisualization.cs` | Gradient-Fortschrittsbalken (Überschreitungs-Shimmer, Glow, optionaler Prozentwert) |
| `SkiaGradientRing.cs` | Gradient-Fortschrittsring (Glow, Tick-Marks, IsPulsing) |
| `SkiaGauge.cs` | Halbkreis-Tachometer (Farbzonen, animierter Zeiger) |
| `SkiaWaterGlass.cs` | Animiertes Wasserglas (Wellen, Tropfen, Glas-Glanz) |
| `SkiaBlueprintCanvas.cs` | Helper für technische Zeichnungen (Raster, Maßlinien, Winkel-Arcs, Schraffuren) |
| `SkiaChartTooltip.cs` | Tooltip-System für interaktive Charts (Pfeil, Auto-Positionierung, Highlight-Dot) |
| `InteractiveChartBase.cs` | Abstrakte Basis für Touch-Charts (Drag, Tooltip-Timeout 2s, DPI-Skalierung) |
| `EasingFunctions.cs` | Mathematische Easing-Funktionen (CubicEaseOut, SineEaseInOut etc.) |
| `AnimatedVisualizationBase.cs` | Basis für animierte Renderer (Einschwing-Animation, `StartAnimation()` + `AnimationProgress`) |

### SkSL GPU-Shader (`SkiaSharp/Shaders/`)

| Datei | Zweck |
|-------|-------|
| `SkiaShimmerEffect.cs` | Wandernder Glanzstreifen (Gold-Shimmer, Premium-Shimmer, Overlay) |
| `SkiaGlowEffect.cs` | Pulsierender Glow (EdgeGlow, RadialGlow, Success/Warning/Premium-Presets) |
| `SkiaWaveEffect.cs` | Animierte Wellen (WaterFill, BackgroundWaves) |
| `SkiaFireEffect.cs` | Feuer/Flammen (DrawFlames, DrawEmbers, DrawForgeFlame, DrawCampfire) |
| `SkiaHeatShimmerEffect.cs` | Hitze-Verzerrung (DrawHeatShimmer, DrawHeatHaze, DrawForgeHeat, DrawSoftHeat) |
| `SkiaElectricArcEffect.cs` | Elektrische Lichtbögen (DrawArc, DrawEnergyPulse, DrawLightning, DrawTeslaCoil) |
| `ShaderPreloader.cs` | Kompiliert alle 12 Shader vorab (Startup), gibt Dauer zurück |

### Splash-Screen-System (`SkiaSharp/SplashScreen/`)

| Datei | Zweck |
|-------|-------|
| `SplashRendererBase.cs` | Abstrakte Basis (Progress-Lerp, DrawCenteredText, DrawProgressBar, IDisposable) |
| `SplashScreenRenderer.cs` | Default-Renderer (Gradient-BG, 24 Glow-Partikel, pulsierender App-Name) |
| `SplashParticle.cs` | Struct für Partikel-Pool (Fixed-Size, kein GC-Druck) |

### Loading-Pipeline (`Loading/`)

| Datei | Zweck |
|-------|-------|
| `ILoadingPipeline.cs` | Interface: Steps, ProgressChanged, ExecuteAsync |
| `LoadingStep.cs` | Datenmodell: Name, DisplayName, Weight, ExecuteAsync |
| `LoadingPipelineBase.cs` | Sequentielle Ausführung, gewichteter Fortschritt, Fehler-Toleranz |

---

## SvgIcon — geteilte Icon-Library

Die verbindliche Icon-Strategie (drei zugelassene Quellen, Verbot von Unicode-Symbolen als
UI-Text) steht in der Haupt-CLAUDE.md → "Icon-Strategie". Hier nur das Library-Spezifische:

`SvgIcon` ist die erste Wahl für einfache geteilte Glyphen (Chevron, Arrow, Check, Star, Crown,
Plus/Minus, …). Es lädt eine `StreamGeometry` aus `Assets/Icons/AppIcons.axaml` über den
`Icon_{Kind}`-Key und skaliert auf die 24×24-ViewBox:

```axaml
<ui:SvgIcon Kind="ChevronDown" Width="16" Height="16" Foreground="..." />
<!-- xmlns:ui="using:MeineApps.UI.Controls" -->
```

**Neue Glyphe ergänzen:** Path-Data manuell (24×24-ViewBox) oder eine CC0-Vorlage
(Material/Tabler/Phosphor) als `<StreamGeometry x:Key="Icon_{Name}">` in `AppIcons.axaml`
eintragen — nie pro App duplizieren. App-eigene Icon-Klassen (`BomberBlast.Icons.GameIcon`,
`RebornSaga.Icons.SagaIcon`) bleiben app-lokal, weil ihre Pfade auf den jeweiligen Visual-Stil
abgestimmt sind.

## Architektur-Patterns

### Library in App registrieren

Jede App registriert die Styles in `App.axaml`:

```axaml
<Application.Styles>
    <StyleInclude Source="avares://MeineApps.UI/Controls/Card.axaml" />
    <StyleInclude Source="avares://MeineApps.UI/Styles/ButtonStyles.axaml" />
    <StyleInclude Source="avares://MeineApps.UI/Styles/TextStyles.axaml" />
    <StyleInclude Source="avares://MeineApps.UI/Styles/InputStyles.axaml" />
    <StyleInclude Source="avares://MeineApps.UI/Styles/ModernCardStyles.axaml" />
</Application.Styles>
```

Namespace in jeder View, die Controls verwendet:

```axaml
xmlns:controls="using:MeineApps.UI.Controls"
xmlns:behaviors="using:MeineApps.UI.Behaviors"
xmlns:i="using:Avalonia.Xaml.Interactivity"
```

### DynamicResource-Keys (von dieser Library erwartet)

Alle Komponenten ziehen ihre Farben per `DynamicResource` aus
`MeineApps.Core.Ava/Themes/ThemeColors.axaml` (keine hardcodierten Farben → Root Anti-Patterns).
Eine App, die diese Controls einbindet, muss diese Keys definiert haben:

| Key | Verwendung |
|-----|-----------|
| `PrimaryBrush` | Aktive Elemente, Fortschrittsringe, Buttons |
| `CardBrush` | Card-Hintergründe, Donut-Chart-Mitte |
| `BackgroundBrush` | App-Hintergrund, Splash-BG |
| `SurfaceBrush` | Panel-Hintergründe |
| `TextPrimaryBrush` | Haupttext, SectionTitle |
| `TextMutedBrush` | Sekundärtext, Captions |
| `BorderSubtleBrush` | Track-Farbe für Fortschrittsringe |

### Property-basiert, keine VM-Bindings

Die Controls dieser Library binden NIE auf externe ViewModels — sie sind rein Property-basiert
(StyledProperties/DirectProperties) und damit in jeder App ohne DataContext-Annahme nutzbar.
Compiled Bindings auf den konsumierenden Views sind global Pflicht (→ Haupt-CLAUDE.md).

### SkiaThemeHelper in App.axaml.cs initialisieren

Einmalig nach Theme-Registrierung aufrufen, damit alle SkiaSharp-Controls sofort die richtigen Farben haben:

```csharp
// In App.axaml.cs, nach Styles-Registrierung:
SkiaThemeHelper.RefreshColors();
```

### Loading-Pipeline: Standardmuster

```csharp
public class MyAppLoadingPipeline : LoadingPipelineBase
{
    public MyAppLoadingPipeline(IServiceProvider services)
    {
        AddStep(new LoadingStep {
            Name = "Shader", DisplayName = "Initialisierung...", Weight = 40,
            ExecuteAsync = () => Task.Run(() => ShaderPreloader.PreloadAll())
        });
        AddStep(new LoadingStep {
            Name = "ViewModel", DisplayName = "Daten werden geladen...", Weight = 30,
            ExecuteAsync = () => { services.GetRequiredService<MainViewModel>(); return Task.CompletedTask; }
        });
    }
}
```

8 App-Implementierungen: `RechnerPlusLoadingPipeline`, `ZeitManagerLoadingPipeline`, `FinanzRechnerLoadingPipeline`, `FitnessRechnerLoadingPipeline`, `HandwerkerRechnerLoadingPipeline`, `WorkTimeProLoadingPipeline`, `HandwerkerImperiumLoadingPipeline`, `BomberBlastLoadingPipeline`.

### App-spezifischer Splash-Renderer

```csharp
// In App.axaml.cs:
var splash = new SkiaLoadingSplash
{
    AppName = "MyApp",
    AppVersion = "v2.0.x",
    Renderer = new MyAppSplashRenderer()  // erbt von SplashRendererBase
};
// FadeOut nach Pipeline-Ende:
splash.FadeOut(); // 200ms Pause + 300ms Opacity → IsVisible=false + Dispose
```

8 App-spezifische Renderer: `RechnerPlusSplashRenderer`, `ZeitManagerSplashRenderer`, `FinanzRechnerSplashRenderer`, `FitnessRechnerSplashRenderer`, `HandwerkerRechnerSplashRenderer`, `WorkTimeProSplashRenderer`, `HandwerkerImperiumSplashRenderer`, `BomberBlastSplashRenderer`. Jeweils in `{App}.Shared/Graphics/`.

---

## Gotchas

### StatsCard / SettingsCard — wann NICHT verwenden

Hover-Lift-Styles funktionieren nur auf Elemente die nicht per Template instanziiert werden. **NICHT verwenden** in:
- `DataTemplate` / `ItemTemplate` (Transition-Objekte werden pro Item angelegt → Performance)
- Dialog-Overlays
- Header-Banner

### TapScaleBehavior — RenderTransform-Pflicht

`TransformOperationsTransition` auf `RenderTransform` crasht auf manchen Android-GPU-Treibern wenn kein initialer Wert gesetzt ist. Gilt auch für den durch TapScaleBehavior ausgelösten ScaleTransform-Übergang:

```axaml
<!-- IMMER RenderTransform + RenderTransformOrigin setzen wenn Transition verwendet wird -->
<Button RenderTransform="scale(1)" RenderTransformOrigin="50%,50%">
```

### DonutChart — 100%-Segment

Wenn ein Segment 360° einnimmt, erzeugt SkiaSharp `ArcTo` einen leeren Path (Start = Ende). Bei `sweepAngle >= 359°` intern in zwei 180°-Hälften aufteilen. Diese Logik ist in `DonutChartVisualization.cs` bereits implementiert.

### ShaderPreloader — Timing

`ShaderPreloader.PreloadAll()` auf dem Background-Thread aufrufen (via `Task.Run`). Das erste Rendern eines Shaders braucht 50–200ms auf Android — ohne Preload entsteht sichtbarer Jank beim ersten Auftauchen des Controls.

### SKMaskFilter — kein Leak

`paint.MaskFilter = SKMaskFilter.CreateBlur(...)` ohne vorheriges Dispose des alten Filters leckt nativen Speicher. In allen SkiaSharp-Controls dieser Library sind gecachte statische Filter oder explizite `Dispose()`-Aufrufe vor Neuzuweisung implementiert. Bei eigenen Erweiterungen dieses Pattern beibehalten.

### SkeletonLoader — IsVisible statt Remove

`SkeletonLoader` startet seinen internen `DispatcherTimer` beim Attached-to-Tree. Statt das Control zu entfernen lieber `IsVisible="False"` setzen — der Timer wird intern gestoppt wenn nicht sichtbar.

### HeatmapCalendar — DateTime-Keys immer UTC

`Data`-Dictionary erwartet `DateTime.Date`-Werte (ohne Uhrzeit). Lokale Mitternacht und UTC-Mitternacht können auf verschiedene Tage fallen. Für konsistentes Tages-Tracking immer `DateTime.UtcNow.Date` als Key verwenden.

### AnimatedNumberText vs. CountUpBehavior

`AnimatedNumberText` ist ein eigenständiges Control (erbt von `TextBlock`) — gut für statische Views wo kein Behavior-XML-Overhead gewünscht ist. `CountUpBehavior` ist flexibler (Format, Suffix, Signed-Prefix, CultureName) und für bestehende `TextBlock`-Elemente gedacht. Nicht beide auf demselben Element verwenden.

### SKCanvasView — `InvalidateSurface()` statt `InvalidateVisual()`

`InvalidateVisual()` triggert die Avalonia-Layout-Pipeline, nicht den SkiaSharp-Repaint. SKCanvasView updatet erst nach `InvalidateSurface()`.

### SKCanvasView — leer nach `IsVisible`-Toggle

`InvalidateSurface()` auf einer unsichtbaren Canvas wird ignoriert. Nach Sichtbar-Werden erneut Daten setzen oder `Calculate()` aufrufen, damit der nächste `PropertyChanged` → `InvalidateSurface()`-Trigger feuert.

### SKCanvasView — Render-Loop stirbt nach `StartRenderLoop()`

`StartRenderLoop()` darf NICHT `StopRenderLoop()` aufrufen — `StopRenderLoop()` setzt `_gameCanvas = null` und die Timer-Lambda captured immer null. In `StartRenderLoop()` nur `_renderTimer?.Stop()` aufrufen, nicht die Canvas-Referenz nullen.

### SKCanvasView — Game-Loop startet nicht (Countdown stuck)

ContentControl + ViewLocator setzt DataContext verzögert → `InvalidateCanvasRequested` hat beim ersten `StartGameLoop()` keinen Subscriber → Render-Timer startet nie. Fix: 3-stufige VM-Subscription via `TrySubscribeToViewModel()` — (1) OnDataContextChanged, (2) OnLoaded als Backup, (3) OnPaintSurface Safety-Net startet Timer nach.

### Custom Control unsichtbar (`PathIcon`-Ableitung)

Abgeleitete Controls (z.B. `GameIcon : PathIcon`) haben kein eigenes `ControlTheme` → Avalonia findet kein Template → Control rendert nichts. Fix: `protected override Type StyleKeyOverride => typeof(PathIcon);` in der Klasse überschreiben. Gilt für ALLE von `TemplatedControl` abgeleiteten Custom Controls.

### TapScaleBehavior — `InvalidCastException` bei `RunAsync(ScaleTransform)`

`animation.RunAsync(ScaleTransform)` crasht in `TransformAnimator.Apply`. Statt der Animation-API einen DispatcherTimer-basierten Tween verwenden — siehe `TapScaleBehavior.cs`.

---

## Verwendungs-Schnipsel

### Card-Varianten

```axaml
<Border Classes="Card StatsCard">          <!-- Standard-Inhaltskarte -->
<Border Classes="Card SettingsCard">       <!-- Einstellungs-Section -->
<Border Classes="Card Interactive">        <!-- Klickbare Karte -->
<Border Classes="Card Success/Warning/Error/Info">
```

### Behaviors

```axaml
<Button RenderTransform="scale(1)" RenderTransformOrigin="50%,50%">
    <i:Interaction.Behaviors>
        <behaviors:TapScaleBehavior PressedScale="0.92" />
    </i:Interaction.Behaviors>
</Button>

<Border>
    <i:Interaction.Behaviors>
        <behaviors:FadeInBehavior Duration="250" SlideFromBottom="True" SlideDistance="16" />
    </i:Interaction.Behaviors>
</Border>

<TextBlock>
    <i:Interaction.Behaviors>
        <behaviors:CountUpBehavior TargetValue="{Binding Balance}"
            Format="N2" Suffix=" €" CultureName="de-DE" UseSignedPrefix="True" />
    </i:Interaction.Behaviors>
</TextBlock>
```

### Game Juice

```axaml
<controls:FloatingTextOverlay x:Name="FloatingTextCanvas"
    Grid.RowSpan="99" ZIndex="15" IsHitTestVisible="False" />
<controls:SkiaCelebrationOverlay x:Name="CelebrationCanvas"
    Grid.RowSpan="99" ZIndex="16" IsHitTestVisible="False" />
```

```csharp
FloatingTextCanvas.ShowFloatingText("+100 Punkte", x, y, Color.Parse("#22C55E"), fontSize: 16);
CelebrationCanvas.ShowConfetti();   // 80 Partikel, 2s
CelebrationCanvas.ShowSparkle();    // 30 Partikel, subtil
```

### GPU-Shader (im PaintSurface-Handler)

```csharp
// Shimmer auf Premium-Elementen
SkiaShimmerEffect.DrawGoldShimmer(canvas, bounds, time);

// Glow auf aktiven Timern
SkiaGlowEffect.DrawEdgeGlow(canvas, bounds, time, glowColor);

// Feuer in Schmiede-Werkstätten
SkiaFireEffect.DrawForgeFlame(canvas, bounds, time);
SkiaHeatShimmerEffect.DrawForgeHeat(canvas, bounds, time);
```

---

## Verweise

- Design-Tokens (Spacing, Radius, Farben): `src/Libraries/MeineApps.Core.Ava/Themes/ThemeColors.axaml`
- App-Farbpaletten (Primary-Farbe pro App): Haupt-CLAUDE.md → "App-Portfolio → App-Farbpaletten"
- Avalonia-/MVVM-Framework-Fallstricke: `src/Libraries/MeineApps.Core.Ava/CLAUDE.md`
- App-spezifische Splash-Renderer: jeweils `src/Apps/{App}/{App}.Shared/Graphics/{App}SplashRenderer.cs`
