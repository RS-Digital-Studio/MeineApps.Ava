# Overlays — Transparente Spielüberlagerungen

10 Overlays erben von `RebornSaga.Engine.Scene` und werden vom `SceneManager` als transparente
Ebenen über der aktiven Szene gerendert.
Overlay-Verwaltung → [Engine/CLAUDE.md](../Engine/CLAUDE.md).

## Dateien

| Datei | Overlay | ConsumesInput | Beschreibung |
|-------|---------|---------------|-------------|
| `PauseOverlay.cs` | `PauseOverlay` | true | 7 Buttons (Fortsetzen, Laden, Einstellungen, Hauptmenü, …), Press-Feedback. |
| `StatusWindowOverlay.cs` | `StatusWindowOverlay` | true | Solo Leveling Stil: Stats, HP/MP, Glitch-Einblendung via `StatusWindowRenderer`. |
| `SystemMessageOverlay.cs` | `SystemMessageOverlay` | true | ARIA-Nachrichten, auto-dismiss 3–5 s. |
| `LevelUpOverlay.cs` | `LevelUpOverlay` | true | Fanfare, Stats hochzählen, +3 Punkte verteilen. |
| `FateChangedOverlay.cs` | `FateChangedOverlay` | true | Glitch-Effekt, "Das Schicksal hat sich verändert…" |
| `GameOverOverlay.cs` | `GameOverOverlay` | true | Revive via Rewarded Ad oder Speicherpunkt laden. |
| `ChapterUnlockOverlay.cs` | `ChapterUnlockOverlay` | true | Gold-Kosten, Freischalten oder Video ansehen. |
| `BacklogOverlay.cs` | `BacklogOverlay` | true | Scrollbare Dialog-Historie (max 200 Einträge), thread-safe via `lock`. |
| `EffectFeedbackOverlay.cs` | `EffectFeedbackOverlay` | **false** | Floating-Texte (Karma/Affinität/EXP/Gold), auto-dismiss 2,5 s. Input wird durchgereicht. |
| `TutorialOverlay.cs` | `TutorialOverlay` | true | Highlight + ARIA-Textbox, blockiert `BattleScene`-Input. |

## Wichtige Patterns

**`BacklogOverlay` — Thread-Safety:**
`lock` auf der Entries-Liste bei `Add`, `Clear` und `Render`. Dialog-Texte können von
`StoryEngine` auf beliebigem Thread geschrieben werden, während der Render-Loop auf dem UI-Thread läuft.

**`EffectFeedbackOverlay` — ConsumesInput=false:**
Einziges Overlay das Input durchreicht. Floating-Texte fliegen über der aktiven Szene,
ohne Tap-Events zu blockieren — der Spieler kann während des Einblendens weiter tippen.

**`GameOverOverlay` — Rewarded Ad:**
Ruft `IRewardedAdService.ShowAdAsync("revive")`. Nach erfolgreichem Ad: Spieler wird mit
vollen HP wiederbelebt, Overlay verschwindet. Bei Misserfolg oder Ablehnung: Speicherpunkt laden.
