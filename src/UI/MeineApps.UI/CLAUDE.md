# MeineApps.UI - Shared UI Components

## Zweck
Wiederverwendbare UI-Komponenten für alle Avalonia Apps:
- Cards (verschiedene Varianten)
- ModernCardStyles (StatsCard Hover-Lift, SettingsCard, EmptyPulse, SectionTitle)
- EmptyStateView
- FloatingActionButton (FAB)
- WheelPicker (Drum-Style Swipe-Zahlen-Picker)
- CircularProgress (Kreisförmiger Fortschrittsring)
- SplashOverlay (App-Start Animation)
- FloatingTextOverlay (Game Juice: Floating Text Animation)
- CelebrationOverlay (Game Juice: Confetti Partikel-Effekt)
- TapScaleBehavior (Micro-Animation: Scale-Down bei Tap)
- FadeInBehavior (Fade-In + optionales Slide-from-Bottom)
- Button Styles
- Text Styles
- Input Styles

## Struktur

```
MeineApps.UI/
├── Controls/
│   ├── Card.axaml              # Card Styles
│   ├── FloatingActionButton.axaml
│   ├── EmptyStateView.axaml
│   ├── EmptyStateView.axaml.cs
│   ├── WheelPicker.axaml           # Drum-style swipe number picker
│   ├── WheelPicker.axaml.cs
│   ├── SplashOverlay.axaml         # App startup splash with icon + loading bar
│   ├── SplashOverlay.axaml.cs
│   ├── CircularProgress.cs          # Kreisförmiger Fortschrittsring
│   ├── FloatingTextOverlay.cs      # Floating text animation (Game Juice)
│   ├── CelebrationOverlay.cs       # Confetti particle effect (Game Juice)
│   └── TooltipBubble.cs            # Onboarding tooltip with tap-to-dismiss
├── Behaviors/
│   ├── TapScaleBehavior.cs         # Scale-Down Micro-Animation bei Tap
│   ├── FadeInBehavior.cs           # Fade-In + Slide-from-Bottom Animation
│   ├── StaggerFadeInBehavior.cs    # Gestaffelter Fade-In fuer Listen
│   ├── CountUpBehavior.cs          # Animiertes Hochzaehlen fuer TextBlocks
│   └── SwipeToRevealBehavior.cs    # Swipe-to-Reveal (Delete-Aktion)
└── Styles/
    ├── ButtonStyles.axaml
    ├── TextStyles.axaml
    ├── InputStyles.axaml
    └── ModernCardStyles.axaml      # Hover-Lift Styles (StatsCard, SettingsCard, EmptyPulse, SectionTitle)
```

## Cards

```axaml
<!-- Basic Card -->
<Border Classes="Card">
  <TextBlock Text="Content" />
</Border>

<!-- Interactive Card (hover effects) -->
<Border Classes="Card Interactive">
  <TextBlock Text="Clickable" />
</Border>

<!-- Outlined Card (no shadow) -->
<Border Classes="Card Outlined">
  <TextBlock Text="Bordered" />
</Border>

<!-- Semantic Cards -->
<Border Classes="Card Success" />
<Border Classes="Card Warning" />
<Border Classes="Card Error" />
<Border Classes="Card Info" />
```

## ModernCardStyles (Hover-Lift + Animationen)

Gemeinsame Styles für alle 8 Apps. Importiert via `<StyleInclude Source="avares://MeineApps.UI/Styles/ModernCardStyles.axaml" />` in jeder App.axaml.

### StatsCard (Hover-Lift -2px)

Für Statistik-, Dashboard- und Content-Cards. Hebt sich bei Hover um 2px an.

```axaml
<!-- Standalone StatsCard -->
<Border Classes="StatsCard">
  <TextBlock Text="Statistik-Card mit Hover-Lift" />
</Border>

<!-- Kombination mit Card-Basis-Style (häufigster Fall) -->
<Border Classes="Card StatsCard">
  <TextBlock Text="Card + Hover-Lift" />
</Border>

<!-- Kombination mit Card Elevated -->
<Border Classes="Card Elevated StatsCard">
  <TextBlock Text="Elevated Card + Hover-Lift" />
</Border>
```

- Background: CardBrush, CornerRadius: 16, Padding: 16, Margin: 0,0,0,12
- Hover: translateY(-2px) mit 200ms Transition
- **NICHT verwenden in**: DataTemplates/ItemTemplates, Dialog-Overlays, Header-Banner

### SettingsCard (Hover-Lift -1px)

Für Settings-Sections. Subtilerer Hover-Effekt als StatsCard.

```axaml
<Border Classes="SettingsCard">
  <StackPanel Spacing="12">
    <TextBlock Text="Einstellungs-Kategorie" FontWeight="Bold" />
    <!-- Settings-Inhalt -->
  </StackPanel>
</Border>
```

- Gleiche Basis-Properties wie StatsCard
- Hover: translateY(-1px) mit 200ms Transition (subtiler)

### EmptyPulse (Pulse-Animation)

Pulsierende Opacity-Animation für Empty-State-Icons.

```axaml
<Border Classes="EmptyPulse" Width="64" Height="64" CornerRadius="32">
  <mi:MaterialIcon Kind="InboxOutline" Width="32" Height="32" />
</Border>
```

- Opacity: 1.0 → 0.5, 2s Dauer, INFINITE, Alternate, CubicEaseInOut

### SectionTitle

Konsistenter Section-Header-Text.

```axaml
<TextBlock Classes="SectionTitle" Text="Übersicht" />
```

- FontSize: 16, FontWeight: Bold, Foreground: TextPrimaryBrush

## WheelPicker

```axaml
xmlns:controls="using:MeineApps.UI.Controls"

<!-- Hours picker (0-23) -->
<controls:WheelPicker Value="{Binding Hours}" Minimum="0" Maximum="23" FormatString="D2" />

<!-- Minutes picker (0-59) -->
<controls:WheelPicker Value="{Binding Minutes}" Minimum="0" Maximum="59" FormatString="D2" />
```

- Drum-style swipe number picker (5 visible items)
- Swipe up/down or mouse wheel to change value
- Wraps around at min/max boundaries
- Center item highlighted with PrimaryBrush
- Properties: Value (TwoWay), Minimum, Maximum, FormatString

## Buttons

```axaml
<!-- Primary (filled) -->
<Button Classes="Primary" Content="Save" />

<!-- Secondary -->
<Button Classes="Secondary" Content="Cancel" />

<!-- Outlined -->
<Button Classes="Outlined" Content="Details" />

<!-- Text (no background) -->
<Button Classes="Text" Content="Learn more" />

<!-- Sizes -->
<Button Classes="Primary Small" Content="Small" />
<Button Classes="Primary Large" Content="Large" />

<!-- Icon Button -->
<Button Classes="Icon">
  <mi:MaterialIcon Kind="Delete" />
</Button>

<!-- Semantic -->
<Button Classes="Success" Content="Confirm" />
<Button Classes="Danger" Content="Delete" />
```

## FAB

```axaml
<!-- Standard FAB -->
<Button Classes="FAB">
  <mi:MaterialIcon Kind="Plus" Width="24" Height="24" />
</Button>

<!-- Mini FAB -->
<Button Classes="FAB Mini">
  <mi:MaterialIcon Kind="Plus" Width="20" Height="20" />
</Button>

<!-- Extended FAB -->
<Button Classes="FAB Extended">
  <StackPanel Orientation="Horizontal" Spacing="8">
    <mi:MaterialIcon Kind="Plus" Width="20" Height="20" />
    <TextBlock Text="Add Item" />
  </StackPanel>
</Button>
```

## EmptyStateView

```axaml
<controls:EmptyStateView
    Icon="InboxOutline"
    Title="No items yet"
    Subtitle="Add your first item to get started"
    ActionText="Add Item"
    ActionCommand="{Binding AddCommand}" />
```

## CircularProgress

Kreisförmiger Fortschrittsanzeiger (Ring). Zeichnet von 12-Uhr-Position im Uhrzeigersinn.

```axaml
xmlns:controls="using:MeineApps.UI.Controls"

<controls:CircularProgress Width="64" Height="64"
    Value="{Binding ProgressFraction}"
    StrokeWidth="5"
    StrokeBrush="{DynamicResource TimerAccentBrush}"
    TrackBrush="{DynamicResource BorderSubtleBrush}" />
```

- Value: 0.0 (leer) bis 1.0 (voll)
- StrokeWidth: Ringbreite in Pixel (Default: 8)
- StrokeBrush: Farbe des Fortschritts-Rings
- TrackBrush: Farbe des Hintergrund-Rings
- PenLineCap.Round für abgerundete Enden
- Rein code-basiert (Custom Control mit Render Override)

## SplashOverlay

```axaml
xmlns:splash="using:MeineApps.UI.Controls"

<!-- In MainWindow.axaml -->
<Panel>
  <views:MainView />
  <splash:SplashOverlay AppName="MyApp"
                        IconSource="avares://MyApp.Shared/Assets/icon.png" />
</Panel>
```

- Shows app icon (120x120, rounded corners) + app name + animated loading bar
- Auto-fades out after 1.5s, hides after 2s
- Properties: AppName (string), IconSource (IImage)
- Uses BackgroundBrush, TextPrimaryBrush, SurfaceBrush, PrimaryBrush from theme

## Text Styles

```axaml
<!-- Typography -->
<TextBlock Classes="DisplayLarge" Text="48px Bold" />
<TextBlock Classes="HeadlineMedium" Text="28px SemiBold" />
<TextBlock Classes="TitleLarge" Text="22px SemiBold" />
<TextBlock Classes="BodyMedium" Text="14px Regular" />
<TextBlock Classes="Caption" Text="11px Muted" />

<!-- Colors -->
<TextBlock Classes="Primary" Text="Primary Color" />
<TextBlock Classes="Muted" Text="Muted Text" />
<TextBlock Classes="Success" Text="Success" />
<TextBlock Classes="Error" Text="Error" />
```

## Inputs

```axaml
<!-- TextBox -->
<TextBox Watermark="Enter text" />

<!-- Filled TextBox -->
<TextBox Classes="Filled" Watermark="Filled style" />

<!-- ComboBox, NumericUpDown etc. inherit styles automatically -->
```

## FloatingTextOverlay (Game Juice)

Canvas-basiertes Control fuer animierten Floating-Text (schwebt nach oben, fadet aus).

```axaml
xmlns:controls="using:MeineApps.UI.Controls"

<controls:FloatingTextOverlay x:Name="FloatingTextCanvas"
                              Grid.RowSpan="99" ZIndex="15"
                              IsHitTestVisible="False" />
```

```csharp
// Im Code-Behind:
FloatingTextCanvas.ShowFloatingText("Gespeichert!", x, y, Color.Parse("#22C55E"), fontSize: 16);
```

- 1.2s Animation, 80px Aufwaertsbewegung, CubicEaseOut
- Fade-Out: 100% bis 30%, dann linear auf 0%
- IsHitTestVisible=false, ClipToBounds=true
- Kann mehrfach gleichzeitig aufgerufen werden (jeder Aufruf erstellt neuen TextBlock)

## CelebrationOverlay (Game Juice)

Canvas-basiertes Confetti-Partikel-System mit Border-Controls (kein SkiaSharp noetig).

```axaml
<controls:CelebrationOverlay x:Name="CelebrationCanvas"
                              Grid.RowSpan="99" ZIndex="16"
                              IsHitTestVisible="False" />
```

```csharp
// Im Code-Behind:
CelebrationCanvas.ShowConfetti();
```

- 50 Border-Partikel-Pool (keine GC-Allokationen pro Animation)
- 5 Farben: Gold, Amber, Rot, Gruen, Blau
- 1.5s Animation mit Schwerkraft, sin-Schwankung und Rotation
- Fade-Out in letzten 30% der Animation
- ~60fps via DispatcherTimer

## TapScaleBehavior

Micro-Animation: Skaliert Control beim Tippen herunter und beim Loslassen wieder hoch.

```axaml
xmlns:behaviors="using:MeineApps.UI.Behaviors"
xmlns:i="using:Avalonia.Xaml.Interactivity"

<Button>
  <i:Interaction.Behaviors>
    <behaviors:TapScaleBehavior PressedScale="0.92" />
  </i:Interaction.Behaviors>
</Button>
```

- PressedScale: Skalierungsfaktor bei gedrücktem Zustand (Default: 0.92)
- Nutzt ScaleTransform auf PointerPressed/Released

## FadeInBehavior

Fade-In Animation mit optionalem Slide-from-Bottom Effekt.

```axaml
<Border>
  <i:Interaction.Behaviors>
    <behaviors:FadeInBehavior Duration="250" SlideFromBottom="True" SlideDistance="16" />
  </i:Interaction.Behaviors>
</Border>
```

- Duration: Animationsdauer in ms (Default: 300)
- SlideFromBottom: Ob von unten reinsliden (Default: false)
- SlideDistance: Slide-Distanz in px (Default: 20)
- CubicEaseOut Easing

## StaggerFadeInBehavior

Automatischer gestaffelter Fade-In fuer Listen-Items. Erkennt den Index im uebergeordneten Panel.

```axaml
<Border>
  <i:Interaction.Behaviors>
    <behaviors:StaggerFadeInBehavior StaggerDelay="50" BaseDuration="300" />
  </i:Interaction.Behaviors>
</Border>
```

- StaggerDelay: Verzoegerung pro Element in ms (Default: 50)
- BaseDuration: Animationsdauer in ms (Default: 300)
- FixedIndex: Fester Index statt Auto-Erkennung (-1 = automatisch)
- Fade-In + Slide-Up (15px), CubicEaseOut

## CountUpBehavior

Zaehlt einen TextBlock-Wert von 0 zum Zielwert hoch (animierte Zahl).

```axaml
<TextBlock>
  <i:Interaction.Behaviors>
    <behaviors:CountUpBehavior TargetValue="{Binding MyValue}" Format="F1" Suffix=" kg" Duration="500" />
  </i:Interaction.Behaviors>
</TextBlock>
```

- TargetValue: Zielwert (double, Binding-faehig)
- Format: Zahlenformat (Default: "F1")
- Suffix: Text nach der Zahl (Default: "")
- Duration: Animationsdauer in ms (Default: 500)
- 30 Frames, CubicEaseOut Interpolation

## SwipeToRevealBehavior

Swipe-to-Reveal Behavior: Verschiebt ein Control nach links um eine Aktion dahinter freizulegen.

```axaml
<Panel>
  <!-- Delete-Layer (dahinter) -->
  <Border Background="#EF4444" HorizontalAlignment="Right" Width="80">
    <Button Command="{Binding DeleteCommand}">
      <mi:MaterialIcon Kind="Delete" Foreground="White" />
    </Button>
  </Border>
  <!-- Content-Layer (verschiebbar) -->
  <Border Background="{DynamicResource CardBrush}">
    <i:Interaction.Behaviors>
      <behaviors:SwipeToRevealBehavior SwipeThreshold="80" RevealWidth="80" />
    </i:Interaction.Behaviors>
    <!-- Inhalt -->
  </Border>
</Panel>
```

- SwipeThreshold: Ab welcher Distanz eingerastet wird (Default: 80)
- RevealWidth: Breite des freigelegten Bereichs (Default: 80)
- Nur horizontale Swipes (vertikale werden ignoriert, ScrollViewer-kompatibel)
- Spring-Back-Animation (CubicEaseOut, 10 Frames)
- Snap-to-Open/Close bei Loslassen
- Parent-Border braucht `ClipToBounds="True"`

## TooltipBubble (Onboarding)

Abgerundete Tooltip-Blase mit Tap-to-Dismiss fuer Onboarding-Flows.

```axaml
xmlns:controls="using:MeineApps.UI.Controls"

<controls:TooltipBubble x:Name="OnboardingTooltip"
                         HorizontalAlignment="Center"
                         VerticalAlignment="Top"
                         Margin="32,120,32,0">
  <controls:TooltipBubble.Transitions>
    <Transitions>
      <DoubleTransition Property="Opacity" Duration="0:0:0.3" Easing="CubicEaseOut"/>
      <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.3" Easing="CubicEaseOut"/>
    </Transitions>
  </controls:TooltipBubble.Transitions>
</controls:TooltipBubble>
```

```csharp
// Im Code-Behind:
OnboardingTooltip.Text = "Wische nach links zum Loeschen";
OnboardingTooltip.Show();
OnboardingTooltip.Dismissed += (_, _) => { /* naechster Tooltip */ };
```

- Properties: Text (string), Arrow (Top/Bottom)
- Show(): FadeIn + Scale 0.9→1.0 (300ms via Transitions)
- Hide(): FadeOut + Scale 1.0→0.9, dann Dismissed Event
- Tap-to-Dismiss: PointerPressed ruft Hide() auf
- Background: PrimaryBrush aus Theme (Fallback #6366F1)
- CornerRadius 12, Padding 16/12, MaxWidth 260, weisser Text

---

## SkiaSharp Controls & Helpers

Wiederverwendbare SkiaSharp-basierte Controls und Hilfsklassen für visuell anspruchsvolle Darstellungen.

### SkiaThemeHelper.cs

Statische Klasse die Avalonia-Theme-Farben (DynamicResource) zu `SKColor` konvertiert. Cached die Farben für performanten Zugriff in SkiaSharp Paint-Operationen.

- `RefreshColors()` bei Theme-Wechsel aufrufen (z.B. im ThemeChanged-Handler)
- Stellt alle Theme-Farben als `SKColor`-Properties bereit (Primary, Background, Surface, Text etc.)
- Vermeidet wiederholtes Parsen von Avalonia-Brushes in jedem PaintSurface-Call

### SkiaParticleSystem.cs

Struct-basiertes Partikelsystem für performante Partikel-Effekte ohne GC-Druck.

- `SkiaParticle` (struct): Position, Velocity, Farbe, Größe, Lifetime, Rotation
- `SkiaParticleManager`: Verwaltet Partikel-Array, Update/Draw-Loop
- `SkiaParticlePresets`: Vordefinierte Effekte:
  - **Confetti** - Bunte Rechteck-Partikel mit Schwerkraft und Rotation
  - **Sparkle** - Kleine leuchtende Funken
  - **WaterDrop** - Tropfen-Effekt mit Schwerkraft
  - **Glow** - Leuchtende Partikel mit Fade-Out
  - **Coin** - Münz-Partikel (für Belohnungen/Idle-Games)
  - **Firework** - Explosionsartige Partikel-Emission

### SkiaBlueprintCanvas.cs

Statische Helper-Klasse für technische Zeichnungen auf SKCanvas. Ideal für Bau-/Handwerker-Visualisierungen.

- **Raster**: Zeichnet Hintergrund-Gitter (konfigurierbare Abstände/Farben)
- **Maßlinien**: Bemaßungs-Pfeile mit Text (horizontal/vertikal)
- **Winkel-Arcs**: Winkelbögen mit Grad-Beschriftung
- **Schraffuren**: Diagonale Linien für Flächen-Markierungen
- **Auto-Skalierung**: Berechnet Scale/Offset damit Zeichnung in Canvas passt

### DonutChartVisualization.cs

Wiederverwendbarer Donut-Chart-Renderer für alle Apps. Segmente mit Farben, InnerRadius konfigurierbar, Labels, optionale Legende.

```csharp
// Segment-Definition
var segments = new DonutChartVisualization.Segment[]
{
    new() { Value = 60, Color = SKColors.Green, Label = "Arbeit", ValueText = "60%" },
    new() { Value = 30, Color = SKColors.Orange, Label = "Pause", ValueText = "30%" },
    new() { Value = 10, Color = SKColors.Red, Label = "Sonstiges", ValueText = "10%" }
};

// Im PaintSurface-Handler:
DonutChartVisualization.Render(canvas, bounds, segments,
    innerRadiusFraction: 0.55f, showLabels: true, showLegend: true);
```

- **Segment** struct: `Value`, `Color`, `Label`, `ValueText`
- **innerRadiusFraction**: 0.0 (Pie) bis 0.9 (dünner Ring)
- **centerText / centerSubText**: Optionaler Text in der Donut-Mitte
- **showLabels**: Prozent-Labels auf Segmenten (bei genug Platz)
- **showLegend**: Farbige Legende unter dem Chart
- Arc-Path-Segmente mit Gradient, Gaps, Inner-Glow

### SkiaGradientRing.cs (Avalonia Control)

Gradient-Fortschrittsring mit Glow-Effekt, Tick-Marks und Partikeln. Erbt von `Control`, rendert via `SKCanvasView`.

```axaml
xmlns:controls="using:MeineApps.UI.Controls"

<controls:SkiaGradientRing Width="200" Height="200"
    Value="{Binding Progress}"
    StartColor="#6366F1"
    EndColor="#22D3EE"
    GlowEnabled="True"
    ShowTickMarks="True"
    IsPulsing="{Binding IsActive}" />
```

- **Value**: Fortschrittswert 0.0–1.0
- **StartColor / EndColor**: Gradient-Farben des Rings
- **GlowEnabled**: Äußerer Glow-Effekt am Fortschritts-Ende
- **ShowTickMarks**: Tick-Markierungen auf dem Ring
- **IsPulsing**: Pulsier-Animation bei aktivem Zustand

### SkiaGauge.cs (Avalonia Control)

Halbkreis-Tachometer mit konfigurierbaren Farbzonen und animiertem Zeiger.

```axaml
<controls:SkiaGauge Width="240" Height="140"
    Value="{Binding CurrentValue}"
    Minimum="0" Maximum="100"
    Zones="{Binding GaugeZones}"
    NeedleAnimated="True"
    Unit="km/h" />
```

- **Value**: Aktueller Wert (animiert zum Ziel wenn NeedleAnimated=True)
- **Minimum / Maximum**: Wertebereich
- **Zones**: Liste von Farbzonen (z.B. Grün 0–60, Gelb 60–80, Rot 80–100)
- **NeedleAnimated**: Zeiger-Animation beim Wertwechsel
- **Unit**: Einheits-Text unter dem Wert

### SkiaWaterGlass.cs (Avalonia Control)

Animiertes Wasserglas mit Wellen-Animation, Tropfen-Effekt und Glas-Glanz.

```axaml
<controls:SkiaWaterGlass Width="120" Height="200"
    FillPercent="{Binding WaterLevel}"
    WaveEnabled="True"
    WaterColor="#3B82F6"
    ShowDrops="True" />
```

- **FillPercent**: Füllstand 0.0–1.0
- **WaveEnabled**: Animierte Wellen-Oberfläche
- **WaterColor**: Farbe des Wassers
- **ShowDrops**: Tropfen-Partikel die ins Glas fallen
