# MeineApps.UI - Shared UI Components

## Zweck
Wiederverwendbare UI-Komponenten für alle Avalonia Apps:
- Cards (verschiedene Varianten)
- EmptyStateView
- FloatingActionButton (FAB)
- WheelPicker (Drum-Style Swipe-Zahlen-Picker)
- SplashOverlay (App-Start Animation)
- FloatingTextOverlay (Game Juice: Floating Text Animation)
- CelebrationOverlay (Game Juice: Confetti Partikel-Effekt)
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
│   ├── FloatingTextOverlay.cs      # Floating text animation (Game Juice)
│   └── CelebrationOverlay.cs       # Confetti particle effect (Game Juice)
└── Styles/
    ├── ButtonStyles.axaml
    ├── TextStyles.axaml
    └── InputStyles.axaml
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

- 1.5s Animation, 80px Aufwaertsbewegung, CubicEaseOut
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
- 2.5s Animation mit Schwerkraft, sin-Schwankung und Rotation
- Fade-Out in letzten 30% der Animation
- ~60fps via DispatcherTimer
