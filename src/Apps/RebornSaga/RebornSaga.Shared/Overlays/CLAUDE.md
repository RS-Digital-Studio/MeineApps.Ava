# Overlays — Transparente Spielüberlagerungen

10 Overlays erben von `RebornSaga.Engine.Scene` und werden vom `SceneManager` als transparente
Ebenen über der aktiven Szene gerendert.
Overlay-Verwaltung → [Engine/CLAUDE.md](../Engine/CLAUDE.md).

## Dateien

| Datei | Overlay | ConsumesInput | Beschreibung |
|-------|---------|---------------|-------------|
| `PauseOverlay.cs` | `PauseOverlay` | true | 7 Buttons (Fortsetzen, Speichern, Status, Inventar, Kodex, Einstellungen, Hauptmenü), Press-Feedback. |
| `StatusWindowOverlay.cs` | `StatusWindowOverlay` | true | Solo Leveling Stil: Stats, HP/MP, Einblend-Animation via `StatusWindowRenderer`. |
| `SystemMessageOverlay.cs` | `SystemMessageOverlay` | true | ARIA-Nachrichten, auto-dismiss (konfigurierbar, Default 4 s). |
| `LevelUpOverlay.cs` | `LevelUpOverlay` | true | Partikel-Fanfare, +3 Punkte verteilen, `StatsConfirmed`-Event nach Bestätigung. |
| `FateChangedOverlay.cs` | `FateChangedOverlay` | true | Glitch-Effekt, "Das Schicksal hat sich verändert…", auto-dismiss 2,5 s. |
| `GameOverOverlay.cs` | `GameOverOverlay` | true | Roter Fade-In, fallende Partikel; `ReviveRequested`- und `LoadSaveRequested`-Events (Aufrufseite verdrahtet Rewarded Ad). |
| `ChapterUnlockOverlay.cs` | `ChapterUnlockOverlay` | true | Gold-Kosten, Freischalten oder Video ansehen. |
| `BacklogOverlay.cs` | `BacklogOverlay` | true | Scrollbare Dialog-Historie (max 200 Einträge), thread-safe via `lock`. |
| `EffectFeedbackOverlay.cs` | `EffectFeedbackOverlay` | **false** | Floating-Texte (Karma/Affinität/EXP/Gold), auto-dismiss 2,5 s. Input wird durchgereicht. |
| `TutorialOverlay.cs` | `TutorialOverlay` | true | Highlight + ARIA-Textbox, blockiert Szenen-Input. |

## Wichtige Patterns

**`BacklogOverlay` — Thread-Safety:**
`_entries` ist statisch (überlebt Szenen-Wechsel). `lock (_entriesLock)` schützt `AddEntry`,
`Clear` und den kurzen Snapshot-Aufruf in `Render`. Dialog-Texte können von `StoryEngine`
auf beliebigem Thread geschrieben werden, während der Render-Loop auf dem UI-Thread läuft.

**`EffectFeedbackOverlay` — ConsumesInput=false:**
Einziges Overlay, das Input durchreicht. Floating-Texte fliegen über der aktiven Szene,
ohne Tap-Events zu blockieren — der Spieler kann während des Einblendens weiter tippen.

**`GameOverOverlay` — Event-Delegation statt direkter Ad-Aufruf:**
Das Overlay feuert `ReviveRequested` bzw. `LoadSaveRequested`. Die Aufrufseite (Szene oder
ViewModel) verdrahtet die Events mit `IRewardedAdService`. Kein Ad-Aufruf direkt im Overlay.

**`PauseOverlay` — Speichern mit Busy-Guard:**
`OnSaveRequested` ist ein `Func<Task>?`-Callback, den die aufrufende Szene setzt. Ein
`_isSaving`-Flag verhindert doppelte Aufrufe. Kein direkter Service-Zugriff im Overlay.
