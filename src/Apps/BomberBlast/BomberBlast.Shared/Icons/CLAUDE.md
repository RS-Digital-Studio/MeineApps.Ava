# Icons — Neon-Arcade Icon-System

Eigenes Icon-System mit 159 Icons (160 Enum-Member inkl. None) im Neon-Arcade-Stil. Ersetzt `Material.Icons` vollständig —
kein Material-Icons-Namespace in BomberBlast. Generische Conventions →
[Haupt-CLAUDE.md](../../../../../CLAUDE.md). App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `GameIcon.cs` | `PathIcon`-Ableitung. **`StyleKeyOverride => typeof(PathIcon)` ist Pflicht** — sonst findet Avalonia kein Template und das Control ist leer. |
| `GameIconKind.cs` | Enum aller Icons (160 Member inkl. None = 159 Icons) |
| `GameIconPaths.cs` | SVG-Pfade im Neon-Arcade-Stil (nur M/L/H/V/Z — keine Kurven) |
| `GameIconRenderer.cs` | SkiaSharp-Renderer für Icons auf `SKCanvas`. Gecachte `SKPath` pro Kind. |

---

## Design-Sprache

Oktagone (8 Seiten, flach), scharfe Kanten, Arcade-Ästhetik.
Keine organischen Kurven, keine Schatten, kein Anti-Aliasing im Source-SVG.

## XAML-Nutzung

```axaml
xmlns:icons="using:BomberBlast.Icons"

<icons:GameIcon Kind="Bomb" Width="24" Height="24" />
<icons:GameIcon Kind="{Binding IconKind}" Foreground="{DynamicResource AccentColor}" />
```

## Skia-Nutzung (SkiaSharp-Canvas)

```csharp
// Gecachte SKPath → einmal rendern, beliebig oft wiederverwenden
GameIconRenderer.Render(canvas, GameIconKind.Star, x, y, size, paint);
```

---

## AppChecker-False-Positive

Der AppChecker meldet für BomberBlast immer einen Material.Icons-Fehler, weil er das
GameIcon-System nicht kennt. Das ist kein Bug — bewusste Konvention. Ignorieren.
