# App-spezifische Ladebildschirme - Design

**Datum:** 02.03.2026
**Status:** Genehmigt

## Ziel

Jeden Ladebildschirm visuell individuell und passend zur jeweiligen App gestalten. Statt eines generischen Splash-Screens bekommt jede der 8 Apps einen eigenen thematischen Ladebildschirm mit individuellem Hintergrund, Mini-Szene, Partikeln und Progress-Bar-Stil.

## Architektur

### Hybrid-Ansatz: Shared Framework + App-spezifische Renderer

**Shared Framework** (MeineApps.UI):
- `SplashRendererBase` - abstrakte Basisklasse
  - `Update(float deltaTime)` - Animation aktualisieren
  - `Render(SKCanvas canvas, SKRect bounds)` - Zeichnen
  - Properties: `Progress`, `StatusText`, `AppName`, `AppVersion`
  - Smooth-Progress-Interpolation (Lerp 0.12f, bestehende Logik)
  - Gecachte SKPaint/SKFont Felder (kein per-frame GC)
  - `IDisposable` mit sauberem Cleanup
- `SkiaLoadingSplash` Control: Akzeptiert `SplashRendererBase` statt fest verdrahtetem `SplashScreenRenderer`
  - Neue Property: `Renderer` (SplashRendererBase)
  - Bestehender Lifecycle (OnAttachedToVisualTree, FadeOut, StopAndDispose) bleibt

**App-spezifische Renderer** (in jeweiliger App unter `Graphics/`):
- Jede App: `{App}SplashRenderer : SplashRendererBase`
- Komplette Kontrolle über Hintergrund, Szene, Partikel, Progress-Bar

### Bestehender Code

- `SplashScreenRenderer` (generisch) wird durch `SplashRendererBase` ersetzt
- `SplashParticle` struct bleibt als Basis-Partikel (kann von Apps wiederverwendet werden)
- `LoadingPipeline` pro App bleibt unverändert
- `SkiaLoadingSplash` Control wird minimal angepasst (Renderer-Property statt hartcodiert)
- HandwerkerImperium's `LoadingScreenRenderer` wird in `HandwerkerImperiumSplashRenderer` integriert

## Die 8 Ladebildschirme

### 1. RechnerPlus - "Die saubere Gleichung"

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Tiefes Midnight-Blau (#0A0E27 → #050816), mathematisches Punkt-Grid im Hintergrund |
| Szene | 4x4 Taschenrechner-Tasten-Matrix (0-9, +, -, x, /), Wellen-Animation: Tasten leuchten nacheinander auf (diagonal sweep). LCD-Display-Stil App-Name darüber mit Segment-Font-Optik |
| Partikel | 16 kleine leuchtende Zeichen (0-9, +, -, x, /, =, %) die langsam nach oben schweben, dezentes Glow |
| Progress | Minimalistischer, dünner Balken in kühlem Blau (#6366F1 → #818CF8) |
| App-Name | LCD/Segment-Display Stil, monochromes Grün oder Weiß |

### 2. ZeitManager - "Die tickende Uhr"

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Dunkles Indigo (#0D1B2A → #081420), konzentrische Kreise (Uhrzifferblatt-Ringe) mit dezenter Opacity |
| Szene | Analoge Uhr: Kreisförmiges Zifferblatt mit 12 Strich-Markierungen und 4 Zahlen (12/3/6/9), Sekundenzeiger tickt in Echtzeit (Tick-Bewegung, nicht smooth), Minutenzeiger bewegt sich langsam |
| Partikel | 12 kleine rotierende Zahnrad-Silhouetten (3-5 Zähne, verschiedene Größen) |
| Progress | Kreisförmiger Fortschrittsring um die Uhr herum (statt horizontalem Balken), Primary-Farbe des Themes |
| App-Name | Unter der Uhr, elegant, dezente Größe |

### 3. FinanzRechner - "Das wachsende Kapital"

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Tiefes Grün (#0A1F0A → #040D04), subtile Chart-Grid-Linien (horizontal, dezent) |
| Szene | Animierter Aktien-Chart: Bezier-Kurve die von links nach rechts wächst (synchron mit Progress), unter dem Chart gestapelte Münzen die nacheinander erscheinen (Scale-In, 4-6 Münzen) |
| Partikel | 16 goldene Münzen mit Euro-Prägung (Kreis + €-Symbol), aufsteigend mit leichter Sinus-Drift |
| Progress | Gold-grüner Gradient-Balken (#22C55E → #FFD700), Münz-Glow am Ende |
| App-Name | Über dem Chart, gold-akzentuiert |

### 4. FitnessRechner - "Der Herzschlag"

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Energetisches Dunkel-Cyan (#0A1A1F → #051015), pulsierende konzentrische Ringe (Ripple-Effekt, 1Hz) |
| Szene | EKG-Herzschlag-Linie: Horizontale Linie die rhythmisch pulsiert (realistisches P-QRS-T Wellen-Muster), wandert von links nach rechts, hinterlässt Fade-Trail. Großer pulsierender Herz-Kreis dahinter (Scale 0.95-1.05, 1Hz) |
| Partikel | 20 pulsierende Dots die im Herzrhythmus erscheinen (Burst bei jedem "Schlag") und kreisförmig nach außen wandern |
| Progress | Energetischer Cyan → Grün Gradient-Balken (#06B6D4 → #22C55E) |
| App-Name | Über der EKG-Linie, clean und energetisch |

### 5. HandwerkerRechner - "Das Maßband"

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Warmes Holz-Braun (#1C140E → #0D0A07), Holzmaserung-Linien (Sinus-Wellenlinien, wie ResearchBackgroundRenderer) |
| Szene | Gelbes Maßband das sich von links nach rechts entrollt - **synchron mit dem Loading-Progress!** Markierungen (cm-Striche) werden beim Entrollen sichtbar. Bleistift-Spitze folgt der Entrollung und zeichnet eine dünne Linie |
| Partikel | 12 Holzstaub/Sägespäne-Partikel (warm-braun, verschiedene Größen, langsam fallend mit Sinus-Drift) |
| Progress | Das Maßband SELBST ist der Fortschrittsbalken - kein separater Balken nötig. Gelb (#FFC107) mit cm-Markierungen |
| App-Name | Über dem Maßband, in warmer Holz-Typografie |

### 6. WorkTimePro - "Die Stechuhr"

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Professionelles Dunkelgrau (#141820 → #0A0D12), dezente horizontale Kalender-Linien |
| Szene | Stechuhr/Time-Clock: Rechteckiges Gehäuse mit Schlitz, periodisch (alle ~2s) gleitet eine Zeitkarte von oben ein, Stempel drückt mit Bump-Effekt (Scale 1.0 → 0.95 → 1.0), Tinte erscheint auf der Karte, Karte gleitet wieder raus |
| Partikel | 10 dezente Business-Partikel (kleine Quadrate/Rechtecke in Grau-Blau Tönen, langsam schwebend) |
| Progress | Professioneller Balken mit blauem Akzent (#3B82F6 → #60A5FA) |
| App-Name | Über der Stechuhr, professioneller Sans-Serif Stil |

### 7. HandwerkerImperium - "Die Schmiede" (Aufwertung des bestehenden)

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Dunkler Gradient (#1A1A2E → #0D0D1A) mit Vignette (bestehend), PLUS: dezente Feuer-Glut am unteren Rand |
| Szene | Bestehende Zahnräder + Funken, PLUS: Amboss-Silhouette unterhalb, periodischer Hammer-Schlag (alle ~1.5s) der beim Aufprall einen Funken-Burst erzeugt. Untertitel "Baue dein Imperium" (bestehend) |
| Partikel | Goldene Funken (bestehend, aufgewertet) + Feuer-Glut-Partikel (rot-orange, aufsteigend vom unteren Rand) |
| Progress | Craft-Orange → Gold Gradient-Balken (#EA580C → #FFD700) mit Funken-Glow am Ende |
| App-Name | Pulsierend (bestehend), über den Zahnrädern. Rotierende Tipps bleiben (bestehend) |

### 8. BomberBlast - "Die Bombe"

| Element | Beschreibung |
|---------|-------------|
| Hintergrund | Dunkles Feuer-Rot (#1A0808 → #0D0404), subtile Explosions-Vignette (heller Kern in der Mitte) |
| Szene | Cartoon-Bombe (schwarze Kugel mit weißem Highlight, kurze Lunte oben). Die Lunte brennt runter synchron mit Progress. Funken sprühen von der brennenden Lunte. Bei ~95% Progress: kurzer weißer Explosions-Flash (200ms) als Übergang zum Fade-Out |
| Partikel | 20 Feuer-Funken (gelb-orange-rot, aufsteigend und zur Seite, Schwerkraft), dezente Rauch-Wisps (grau, langsam aufsteigend) |
| Progress | Feuriger Gradient-Balken (#DC2626 → #F97316 → #FBBF24) mit Flammen-Effekt am Ende |
| App-Name | Über der Bombe, bold, leichter Feuer-Glow |

## Technische Details

### SplashRendererBase (abstrakt)

```csharp
public abstract class SplashRendererBase : IDisposable
{
    // Öffentliche Steuerung (wie bisher)
    public float Progress { get; set; }
    public string StatusText { get; set; } = "";
    public string AppName { get; set; } = "App";
    public string AppVersion { get; set; } = "";

    // Smooth-Interpolation
    protected float RenderedProgress;
    protected float Time;

    // Gecachte Basis-Paints
    protected readonly SKPaint StatusPaint = new() { IsAntialias = true };
    protected readonly SKFont StatusFont = new() { Size = 13f };

    public void Update(float deltaTime)
    {
        Time += deltaTime;
        // Smooth-Progress (Lerp 0.12f)
        var diff = Progress - RenderedProgress;
        if (Math.Abs(diff) > 0.001f)
            RenderedProgress += diff * 0.12f;
        else
            RenderedProgress = Progress;
        OnUpdate(deltaTime);
    }

    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        OnRender(canvas, bounds);
    }

    protected abstract void OnUpdate(float deltaTime);
    protected abstract void OnRender(SKCanvas canvas, SKRect bounds);

    // Helper: Zentrierten Text zeichnen
    protected void DrawCenteredText(SKCanvas canvas, string text, float y,
        SKFont font, SKPaint paint, float canvasWidth) { ... }

    // Helper: Status-Text zeichnen (unten)
    protected void DrawStatusText(SKCanvas canvas, SKRect bounds) { ... }

    public virtual void Dispose() { StatusPaint.Dispose(); StatusFont.Dispose(); }
}
```

### SkiaLoadingSplash Änderung

```csharp
// Neue Property statt hartcodiertem SplashScreenRenderer
public static readonly StyledProperty<SplashRendererBase?> RendererProperty = ...;

// In OnAttachedToVisualTree: _renderer = Renderer ?? new DefaultSplashRenderer();
```

### App-Integration (Beispiel)

```csharp
// In App.axaml.cs jeder App:
var splash = new SkiaLoadingSplash
{
    AppName = "BomberBlast",
    AppVersion = "v2.0.23",
    Renderer = new BomberBlastSplashRenderer() // NEU: App-spezifisch
};
```

### Performance-Richtlinien

- Alle SKPaint/SKFont/SKPath als Instanz-Felder (kein `new` pro Frame)
- Struct-basierte Partikel-Pools (kein GC-Druck)
- MaskFilter/Shader nur einmal erstellen, in Dispose() aufräumen
- ~60fps Render-Loop (bestehender 16ms DispatcherTimer)
- Jeder Renderer implementiert IDisposable korrekt

### Dateien pro App

| App | Neuer Renderer | Pfad |
|-----|---------------|------|
| RechnerPlus | `RechnerPlusSplashRenderer` | `Graphics/RechnerPlusSplashRenderer.cs` |
| ZeitManager | `ZeitManagerSplashRenderer` | `Graphics/ZeitManagerSplashRenderer.cs` |
| FinanzRechner | `FinanzRechnerSplashRenderer` | `Graphics/FinanzRechnerSplashRenderer.cs` |
| FitnessRechner | `FitnessRechnerSplashRenderer` | `Graphics/FitnessRechnerSplashRenderer.cs` |
| HandwerkerRechner | `HandwerkerRechnerSplashRenderer` | `Graphics/HandwerkerRechnerSplashRenderer.cs` |
| WorkTimePro | `WorkTimeProSplashRenderer` | `Graphics/WorkTimeProSplashRenderer.cs` |
| HandwerkerImperium | `HandwerkerImperiumSplashRenderer` | `Graphics/HandwerkerImperiumSplashRenderer.cs` |
| BomberBlast | `BomberBlastSplashRenderer` | `Graphics/BomberBlastSplashRenderer.cs` |

### Migration

1. `SplashScreenRenderer` → `SplashRendererBase` (abstrakt) + `DefaultSplashRenderer` (Fallback)
2. `SkiaLoadingSplash` bekommt `Renderer` Property
3. Pro App: Neuen Renderer erstellen, in App.axaml.cs setzen
4. HandwerkerImperium: Bestehenden `LoadingScreenRenderer` Code in neuen `HandwerkerImperiumSplashRenderer` überführen
5. Alten `SplashScreenRenderer` als `DefaultSplashRenderer` behalten (Fallback)
