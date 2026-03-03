# App-spezifische Ladebildschirme - Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Jeden Ladebildschirm visuell individuell und passend zur App gestalten: 8 thematische Mini-Szenen mit eigenem Hintergrund, Partikeln und Progress-Bar.

**Architecture:** Abstrakte Basisklasse `SplashRendererBase` in MeineApps.UI als Shared Framework (Progress-Interpolation, Lifecycle, Dispose). Pro App ein konkreter Renderer in `Graphics/`. `SkiaLoadingSplash` bekommt eine `Renderer`-Property statt hartcodiertem `SplashScreenRenderer`.

**Tech Stack:** SkiaSharp 3.119.2, Avalonia 11.3, Struct-basierte Partikel-Pools, gecachte SKPaint/SKFont

**Design-Dokument:** `docs/plans/2026-03-02-app-specific-splash-screens-design.md`

---

## Task 1: SplashRendererBase erstellen (Shared Framework)

**Files:**
- Create: `src/UI/MeineApps.UI/SkiaSharp/SplashScreen/SplashRendererBase.cs`
- Modify: `src/UI/MeineApps.UI/SkiaSharp/SplashScreen/SplashScreenRenderer.cs` (erbt von Base)

**Step 1: SplashRendererBase erstellen**

Abstrakte Basisklasse mit gemeinsamer Logik:

```csharp
// src/UI/MeineApps.UI/SkiaSharp/SplashScreen/SplashRendererBase.cs
using SkiaSharp;

namespace MeineApps.UI.SkiaSharp.SplashScreen;

/// <summary>
/// Abstrakte Basis für app-spezifische Splash-Screen-Renderer.
/// Stellt Smooth-Progress-Interpolation, Time-Tracking und Helper-Methoden bereit.
/// Alle konkreten Renderer erben hiervon und implementieren OnUpdate/OnRender.
/// </summary>
public abstract class SplashRendererBase : IDisposable
{
    // --- Öffentliche Steuerung ---
    public float Progress { get; set; }
    public string StatusText { get; set; } = "";
    public string AppName { get; set; } = "App";
    public string AppVersion { get; set; } = "";

    // --- Render-State ---
    protected float RenderedProgress;
    protected float Time;
    protected bool IsInitialized;

    // --- Gecachte Basis-Paints ---
    protected readonly SKPaint StatusPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    protected readonly SKPaint VersionPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    protected readonly SKFont StatusFont = new() { Size = 13f };
    protected readonly SKFont VersionFont = new() { Size = 14f };

    protected readonly Random Rng = new();

    /// <summary>
    /// Aktualisiert Animationen (pro Frame, ~60fps).
    /// </summary>
    public void Update(float deltaTime)
    {
        Time += deltaTime;

        // Smooth-Interpolation zum Zielwert (EaseOut)
        var diff = Progress - RenderedProgress;
        if (Math.Abs(diff) > 0.001f)
            RenderedProgress += diff * 0.12f;
        else
            RenderedProgress = Progress;

        OnUpdate(deltaTime);
    }

    /// <summary>
    /// Rendert den kompletten Splash-Screen.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        OnRender(canvas, bounds);
    }

    /// <summary>App-spezifische Update-Logik (Partikel, Animationen).</summary>
    protected abstract void OnUpdate(float deltaTime);

    /// <summary>App-spezifische Render-Logik (Hintergrund, Szene, Progress).</summary>
    protected abstract void OnRender(SKCanvas canvas, SKRect bounds);

    // --- Helper-Methoden ---

    /// <summary>Zeichnet zentrierten Text.</summary>
    protected static void DrawCenteredText(SKCanvas canvas, string text, float y,
        SKFont font, SKPaint paint, float canvasWidth)
    {
        var textWidth = font.MeasureText(text);
        canvas.DrawText(text, (canvasWidth - textWidth) / 2f, y, font, paint);
    }

    /// <summary>Zeichnet den Status-Text zentriert unter dem Fortschrittsbalken.</summary>
    protected void DrawStatusText(SKCanvas canvas, float w, float y)
    {
        if (string.IsNullOrEmpty(StatusText)) return;
        StatusFont.Size = Math.Min(13f, w * 0.033f);
        StatusPaint.Color = new SKColor(0xBB, 0xBB, 0xBB);
        DrawCenteredText(canvas, StatusText, y, StatusFont, StatusPaint, w);
    }

    /// <summary>Zeichnet die Versionsnummer.</summary>
    protected void DrawVersion(SKCanvas canvas, float w, float y)
    {
        if (string.IsNullOrEmpty(AppVersion)) return;
        VersionFont.Size = Math.Min(14f, w * 0.035f);
        VersionPaint.Color = new SKColor(0x88, 0x88, 0x88);
        DrawCenteredText(canvas, AppVersion, y, VersionFont, VersionPaint, w);
    }

    /// <summary>Zeichnet einen Standard-Fortschrittsbalken mit Gradient und Glow.</summary>
    protected void DrawProgressBar(SKCanvas canvas, float w, float y,
        float barWidth, float barHeight, float barRadius,
        SKColor startColor, SKColor endColor, SKColor bgColor)
    {
        var progress = Math.Clamp(RenderedProgress, 0f, 1f);
        var barLeft = (w - barWidth) / 2f;

        // Hintergrund-Track
        using var bgPaint = new SKPaint { IsAntialias = true, Color = bgColor };
        canvas.DrawRoundRect(new SKRect(barLeft, y, barLeft + barWidth, y + barHeight), barRadius, barRadius, bgPaint);

        // Fortschritts-Fill
        if (progress > 0.005f)
        {
            var fillWidth = barWidth * progress;
            using var fillShader = SKShader.CreateLinearGradient(
                new SKPoint(barLeft, y), new SKPoint(barLeft + barWidth, y),
                new[] { startColor, endColor }, null, SKShaderTileMode.Clamp);
            using var fillPaint = new SKPaint { IsAntialias = true, Shader = fillShader };

            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(new SKRect(barLeft, y, barLeft + barWidth, y + barHeight), barRadius));
            canvas.DrawRoundRect(new SKRect(barLeft, y, barLeft + fillWidth, y + barHeight), barRadius, barRadius, fillPaint);
            canvas.Restore();
        }

        // Prozent-Text rechts
        var percentText = $"{(int)(progress * 100)}%";
        StatusFont.Size = Math.Min(13f, w * 0.033f);
        StatusPaint.Color = new SKColor(0xAA, 0xAA, 0xAA);
        canvas.DrawText(percentText, barLeft + barWidth + 10f, y + barHeight / 2f + StatusFont.Size * 0.35f, StatusFont, StatusPaint);
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        OnDispose();
        StatusPaint.Dispose();
        VersionPaint.Dispose();
        StatusFont.Dispose();
        VersionFont.Dispose();
    }

    /// <summary>App-spezifische Ressourcen freigeben.</summary>
    protected virtual void OnDispose() { }
}
```

**Step 2: SplashScreenRenderer auf SplashRendererBase umstellen**

`SplashScreenRenderer` erbt von `SplashRendererBase` und wird zu `DefaultSplashRenderer` (Fallback). Die bestehende Logik bleibt erhalten, nur die Basisklassen-Felder werden genutzt.

- `SplashScreenRenderer` umbenennen zu intern weiterhin `SplashScreenRenderer` (Kompatibilität), aber `: SplashRendererBase` erben
- Eigene `Progress`, `StatusText`, `AppName`, `AppVersion` Properties entfernen (kommen von Base)
- Eigene `_time`, `_renderedProgress` entfernen (kommen von Base als `Time`, `RenderedProgress`)
- `Update()` → `OnUpdate()` override
- `Render()` → `OnRender()` override
- `Dispose()` → `OnDispose()` override (nur eigene Ressourcen)

**Step 3: Build prüfen**

```bash
dotnet build src/UI/MeineApps.UI/MeineApps.UI.csproj
```

Expected: 0 Fehler

**Step 4: Commit**

```
feat(ui): SplashRendererBase als abstrakte Basis für app-spezifische Splash-Renderer
```

---

## Task 2: SkiaLoadingSplash Renderer-Property

**Files:**
- Modify: `src/UI/MeineApps.UI/Controls/SkiaLoadingSplash.axaml.cs`

**Step 1: Renderer-Property hinzufügen**

```csharp
// Neue StyledProperty
public static readonly StyledProperty<SplashRendererBase?> RendererProperty =
    AvaloniaProperty.Register<SkiaLoadingSplash, SplashRendererBase?>(nameof(Renderer));

public SplashRendererBase? Renderer
{
    get => GetValue(RendererProperty);
    set => SetValue(RendererProperty, value);
}
```

**Step 2: OnAttachedToVisualTree anpassen**

```csharp
// Renderer vom Property nehmen, oder Default erstellen
_renderer = Renderer ?? new SplashScreenRenderer();
_renderer.AppName = AppName;
_renderer.AppVersion = AppVersion;
_renderer.Progress = Progress;
_renderer.StatusText = StatusText;
```

Typ von `_renderer` von `SplashScreenRenderer?` auf `SplashRendererBase?` ändern.

**Step 3: Build prüfen**

```bash
dotnet build src/UI/MeineApps.UI/MeineApps.UI.csproj
```

**Step 4: Solution-weiten Build prüfen**

```bash
dotnet build F:/Meine_Apps_Ava/MeineApps.Ava.sln
```

Expected: 0 Fehler (alle 8 Apps nutzen SkiaLoadingSplash ohne Renderer-Property → Fallback auf SplashScreenRenderer)

**Step 5: Commit**

```
feat(ui): SkiaLoadingSplash Renderer-Property für app-spezifische Splash-Renderer
```

---

## Task 3: RechnerPlus Splash-Renderer ("Die saubere Gleichung")

**Files:**
- Create: `src/Apps/RechnerPlus/RechnerPlus.Shared/Graphics/RechnerPlusSplashRenderer.cs`
- Modify: `src/Apps/RechnerPlus/RechnerPlus.Shared/App.axaml.cs` (Renderer setzen)

**Visuelles Konzept:**
- Hintergrund: Tiefes Midnight-Blau (#0A0E27 → #050816), mathematisches Punkt-Grid
- Szene: 4x4 Taschenrechner-Tasten-Matrix, Wellen-Animation (Tasten leuchten diagonal auf), LCD-Stil App-Name
- Partikel: 16 schwebende Mathe-Zeichen (0-9, +, -, x, /)
- Progress: Minimalistischer blauer Balken (#6366F1 → #818CF8)

**Step 1: Renderer implementieren**

Neuen `RechnerPlusSplashRenderer : SplashRendererBase` erstellen mit:
- 4x4 Tasten-Array (Zeichen + animierter Glow-State pro Taste)
- Diagonale Sweep-Welle: Die "aktive" Taste wandert diagonal über die Matrix
- Partikel-Struct-Array (16 Zeichen, aufsteigend mit Fade)
- LCD-Display-artiger App-Name (Monospace-Font, leichter Glow)

**Step 2: App.axaml.cs - CreateSplash() anpassen**

```csharp
private static SkiaLoadingSplash CreateSplash()
{
    return new SkiaLoadingSplash
    {
        AppName = "RechnerPlus",
        AppVersion = "v2.0.6",
        Renderer = new RechnerPlusSplashRenderer()
    };
}
```

**Step 3: Build + visuell prüfen**

```bash
dotnet build src/Apps/RechnerPlus/RechnerPlus.Shared
dotnet run --project src/Apps/RechnerPlus/RechnerPlus.Desktop
```

Visuell prüfen: Tasten-Matrix sichtbar, Wellen-Animation läuft, Partikel schweben, Progress-Bar funktioniert.

**Step 4: Commit**

```
feat(rechnerplus): Individueller Splash-Renderer "Die saubere Gleichung"
```

---

## Task 4: ZeitManager Splash-Renderer ("Die tickende Uhr")

**Files:**
- Create: `src/Apps/ZeitManager/ZeitManager.Shared/Graphics/ZeitManagerSplashRenderer.cs`
- Modify: `src/Apps/ZeitManager/ZeitManager.Shared/App.axaml.cs`

**Visuelles Konzept:**
- Hintergrund: Dunkles Indigo (#0D1B2A → #081420), konzentrische Uhr-Ringe
- Szene: Analoge Uhr mit tickendem Sekundenzeiger (Echtzeit), Minutenzeiger
- Partikel: 12 rotierende Mini-Zahnräder
- Progress: Kreisförmiger Ring um die Uhr (statt horizontalem Balken!)

**Step 1: Renderer implementieren**

- Uhr-Zifferblatt (12 Strich-Markierungen, 4 Zahlen)
- Sekundenzeiger tickt in Echtzeit (Tick-Snap, nicht smooth)
- Minutenzeiger bewegt sich langsam
- Kreisförmiger Progress-Ring als Ersatz für linearen Balken
- Zahnrad-Partikel (3-5 Zähne, rotierend)

**Step 2: App.axaml.cs anpassen**

**Step 3: Build + visuell prüfen**

```bash
dotnet build src/Apps/ZeitManager/ZeitManager.Shared
dotnet run --project src/Apps/ZeitManager/ZeitManager.Desktop
```

**Step 4: Commit**

```
feat(zeitmanager): Individueller Splash-Renderer "Die tickende Uhr"
```

---

## Task 5: FinanzRechner Splash-Renderer ("Das wachsende Kapital")

**Files:**
- Create: `src/Apps/FinanzRechner/FinanzRechner.Shared/Graphics/FinanzRechnerSplashRenderer.cs`
- Modify: `src/Apps/FinanzRechner/FinanzRechner.Shared/App.axaml.cs`

**Visuelles Konzept:**
- Hintergrund: Tiefes Grün (#0A1F0A → #040D04), Chart-Grid-Linien
- Szene: Steigender Aktien-Chart (Bezier-Kurve, synchron mit Progress), gestapelte Münzen darunter
- Partikel: 16 goldene Euro-Münzen
- Progress: Gold-grüner Gradient (#22C55E → #FFD700)

**Step 1: Renderer implementieren**

- Bezier-Chart der mit Progress wächst (von links nach rechts)
- 4-6 Münzen die nacheinander erscheinen (Scale-In)
- Euro-Münzen-Partikel (Kreis + €-Prägung)
- Goldener Glow am Progress-Bar-Ende

**Step 2-4:** App.axaml.cs, Build, Commit

```
feat(finanzrechner): Individueller Splash-Renderer "Das wachsende Kapital"
```

---

## Task 6: FitnessRechner Splash-Renderer ("Der Herzschlag")

**Files:**
- Create: `src/Apps/FitnessRechner/FitnessRechner.Shared/Graphics/FitnessRechnerSplashRenderer.cs`
- Modify: `src/Apps/FitnessRechner/FitnessRechner.Shared/App.axaml.cs`

**Visuelles Konzept:**
- Hintergrund: Dunkel-Cyan (#0A1A1F → #051015), pulsierende Ripple-Ringe (1Hz)
- Szene: EKG-Herzschlag-Linie (P-QRS-T Welle), wandert horizontal, Fade-Trail. Großer pulsierender Kreis dahinter
- Partikel: 20 pulsierende Dots die im Herzrhythmus erscheinen
- Progress: Cyan→Grün Gradient (#06B6D4 → #22C55E)

**Step 1: Renderer implementieren**

- EKG-Wellenform als SKPath (P-Welle, QRS-Komplex, T-Welle)
- Horizontaler Sweep (wie EKG-Monitor)
- Pulsierende Ripple-Kreise im Hintergrund (1Hz)
- Burst-Partikel bei jedem "Herzschlag"

**Step 2-4:** App.axaml.cs, Build, Commit

```
feat(fitnessrechner): Individueller Splash-Renderer "Der Herzschlag"
```

---

## Task 7: HandwerkerRechner Splash-Renderer ("Das Maßband")

**Files:**
- Create: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/Graphics/HandwerkerRechnerSplashRenderer.cs`
- Modify: `src/Apps/HandwerkerRechner/HandwerkerRechner.Shared/App.axaml.cs`

**Visuelles Konzept:**
- Hintergrund: Warmes Holz-Braun (#1C140E → #0D0A07), Holzmaserung-Linien
- Szene: Gelbes Maßband entrollt sich synchron mit Progress! Bleistift-Spitze folgt
- Partikel: 12 Holzstaub/Sägespäne
- Progress: Das Maßband IST der Fortschrittsbalken (kein separater Balken!)

**Step 1: Renderer implementieren**

- Holzmaserung im Hintergrund (Sinus-Linien, warm-braune Töne)
- Gelbes Maßband (#FFC107) das sich entrollt (Breite = Progress * maxWidth)
- cm-Markierungen auf dem Band (Tick-Striche)
- Bleistift-Silhouette an der Bandspitze
- Kein separater Progress-Balken nötig - das Maßband zeigt den Fortschritt

**Step 2-4:** App.axaml.cs, Build, Commit

```
feat(handwerkerrechner): Individueller Splash-Renderer "Das Maßband"
```

---

## Task 8: WorkTimePro Splash-Renderer ("Die Stechuhr")

**Files:**
- Create: `src/Apps/WorkTimePro/WorkTimePro.Shared/Graphics/WorkTimeProSplashRenderer.cs`
- Modify: `src/Apps/WorkTimePro/WorkTimePro.Shared/App.axaml.cs`

**Visuelles Konzept:**
- Hintergrund: Professionelles Dunkelgrau (#141820 → #0A0D12), Kalender-Linien
- Szene: Stechuhr-Gehäuse, periodisch gleitet Zeitkarte ein, Stempel drückt mit Bump, Karte gleitet raus
- Partikel: 10 dezente Business-Partikel (kleine Quadrate/Rechtecke)
- Progress: Professioneller Balken (#3B82F6 → #60A5FA)

**Step 1: Renderer implementieren**

- Stechuhr-Gehäuse (Rechteck mit abgerundeten Ecken, metallisch)
- Schlitz oben für Karte
- Alle ~2s: Karte gleitet rein → Stempel-Bump → Tinte erscheint → Karte gleitet raus
- Dezente Business-Partikel

**Step 2-4:** App.axaml.cs, Build, Commit

```
feat(worktimepro): Individueller Splash-Renderer "Die Stechuhr"
```

---

## Task 9: HandwerkerImperium Splash-Renderer ("Die Schmiede" - Aufwertung)

**Files:**
- Create: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/HandwerkerImperiumSplashRenderer.cs`
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/App.axaml.cs`
- Reference: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/LoadingScreenRenderer.cs` (bestehender Code)

**Visuelles Konzept:**
- Bestehende Zahnräder + Funken übernehmen und aufwerten
- PLUS: Amboss-Silhouette, periodischer Hammer-Schlag mit Funken-Burst
- PLUS: Feuer-Glut am unteren Rand
- Rotierende Tipps behalten
- Progress: Craft-Orange → Gold (#EA580C → #FFD700)

**Step 1: Code aus LoadingScreenRenderer übernehmen**

Den bestehenden `LoadingScreenRenderer` Code (Zahnräder, Funken, Tipps) in neuen `HandwerkerImperiumSplashRenderer : SplashRendererBase` migrieren. Zusätzlich:
- Amboss-Silhouette unter den Zahnrädern
- Hammer-Schlag-Animation (alle ~1.5s)
- Funken-Burst bei Hammer-Aufprall
- Feuer-Glut-Partikel am unteren Rand
- Progress-Bar nutzt jetzt Pipeline-Progress (nicht mehr indeterminiert)

**Step 2: LoadingScreenRenderer.cs Referenzen entfernen**

Falls der alte `LoadingScreenRenderer` noch irgendwo verwendet wird, Referenzen aktualisieren. Alte Datei kann als Dead Code markiert oder gelöscht werden.

**Step 3-5:** App.axaml.cs, Build, Commit

```
feat(handwerkerimperium): Aufgewerteter Splash-Renderer "Die Schmiede"
```

---

## Task 10: BomberBlast Splash-Renderer ("Die Bombe")

**Files:**
- Create: `src/Apps/BomberBlast/BomberBlast.Shared/Graphics/BomberBlastSplashRenderer.cs`
- Modify: `src/Apps/BomberBlast/BomberBlast.Shared/App.axaml.cs`

**Visuelles Konzept:**
- Hintergrund: Dunkles Feuer-Rot (#1A0808 → #0D0404), Explosions-Vignette
- Szene: Cartoon-Bombe (schwarze Kugel + weiße Highlights), Lunte brennt runter synchron mit Progress, Funken sprühen. Bei ~95%: weißer Explosions-Flash (200ms)
- Partikel: 20 Feuer-Funken (gelb-orange-rot) + Rauch-Wisps
- Progress: Feuriger Gradient (#DC2626 → #F97316 → #FBBF24)

**Step 1: Renderer implementieren**

- Cartoon-Bombe (Kreis + Highlight + Lunte)
- Lunten-Länge = (1 - Progress) * maxLength
- Funken am brennenden Ende der Lunte
- Bei Progress >= 0.95: weißer Flash-Overlay (200ms)
- Feuer-Partikel (Schwerkraft, Fade-Out, warm-rot bis gelb)
- Rauch-Wisps (grau, langsam aufsteigend)

**Step 2-4:** App.axaml.cs, Build, Commit

```
feat(bomberblast): Individueller Splash-Renderer "Die Bombe"
```

---

## Task 11: Solution-Build + CLAUDE.md aktualisieren

**Files:**
- Modify: `src/UI/MeineApps.UI/CLAUDE.md` (SplashRendererBase dokumentieren)
- Modify: Haupt-`CLAUDE.md` oder relevante App-CLAUDEs

**Step 1: Solution-Build**

```bash
dotnet build F:/Meine_Apps_Ava/MeineApps.Ava.sln
```

Expected: 0 Fehler

**Step 2: AppChecker laufen lassen**

```bash
dotnet run --project tools/AppChecker
```

**Step 3: MeineApps.UI CLAUDE.md aktualisieren**

`SkiaLoadingSplash` Sektion um `Renderer`-Property und `SplashRendererBase` erweitern.

**Step 4: Commit**

```
docs: CLAUDE.md für app-spezifische Splash-Renderer aktualisiert
```

---

## Reihenfolge und Abhängigkeiten

```
Task 1 (SplashRendererBase) ──→ Task 2 (SkiaLoadingSplash Property)
                                      │
                                      ├──→ Task 3  (RechnerPlus)
                                      ├──→ Task 4  (ZeitManager)
                                      ├──→ Task 5  (FinanzRechner)
                                      ├──→ Task 6  (FitnessRechner)
                                      ├──→ Task 7  (HandwerkerRechner)
                                      ├──→ Task 8  (WorkTimePro)
                                      ├──→ Task 9  (HandwerkerImperium)
                                      └──→ Task 10 (BomberBlast)
                                              │
                                              └──→ Task 11 (Build + Docs)
```

Tasks 3-10 sind untereinander unabhängig und können parallelisiert werden.

## Performance-Checkliste (pro Renderer)

- [ ] Alle SKPaint/SKFont/SKPath als Instanz-Felder (kein `new` pro Frame)
- [ ] Struct-basierter Partikel-Pool (kein GC-Druck)
- [ ] MaskFilter/Shader nur einmal erstellen
- [ ] IDisposable via `OnDispose()` korrekt implementiert
- [ ] `canvas.LocalClipBounds` statt `e.Info.Width/Height`
- [ ] Keine `using var` für wiederverwendete Objekte im Render-Loop
