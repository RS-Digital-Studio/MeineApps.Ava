# Themes — App-Farbpalette

| Datei | Zweck |
|-------|-------|
| `AppPalette.axaml` | RechnerPlus-Primärfarbe **Indigo #7C7FF7** (Retro-Tech Calculator). Statisch via `StyleInclude` in `App.axaml` geladen. |

Kein dynamischer Theme-Wechsel. Alle `DynamicResource`-Keys sind studio-weit identisch;
Design-Tokens (Spacing, Radius, Fonts) kommen aus
[`MeineApps.Core.Ava/Themes/ThemeColors.axaml`](../../../../Libraries/MeineApps.Core.Ava/CLAUDE.md).
