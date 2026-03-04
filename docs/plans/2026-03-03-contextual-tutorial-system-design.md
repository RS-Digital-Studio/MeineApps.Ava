# Design: Kontextuelles Tutorial-System für HandwerkerImperium

**Datum:** 2026-03-03
**Status:** Genehmigt

## Problemstellung

Das bestehende 8-Schritte-Tutorial erscheint komplett beim Spielstart als lineare Dialog-Kette.
- Am Anfang zu viele Meldungen auf einmal (alle 8 Dialoge nacheinander)
- Danach zu wenige Hilfestellungen (keine kontextuellen Erklärungen)
- Features werden erklärt bevor der Spieler sie gesehen hat
- Kein visuelles Highlighting (TargetElement war deklariert aber nie implementiert)

## Lösung: Kontextuelles Hinweis-System

### Kernprinzip
Jeder Hinweis erscheint genau dann, wenn der Spieler das Feature zum ersten Mal erreicht oder benutzt.
Statt 8 Dialoge am Stück → 1 Willkommens-Dialog + ~17 kontextuelle Tooltip-Bubbles.

### 1. Willkommens-Dialog (einmalig)

Nur 1 zentrierter Dialog beim allerersten Start:
> "Willkommen bei HandwerkerImperium! Baue dein eigenes Handwerker-Imperium auf. Wir zeigen dir alles Schritt für Schritt, wenn du es brauchst."

### 2. Kontextuelle Tooltip-Bubbles

| # | Hint-ID | Trigger | Ziel-Element |
|---|---------|---------|-------------|
| 1 | `welcome` | Allererster Start | Zentriert (Dialog) |
| 2 | `first_workshop` | Nach Welcome dismissed | Workshop-Card (Schreiner) |
| 3 | `workshop_detail` | Erste Werkstatt-Detail geöffnet | Upgrade-Button |
| 4 | `first_order` | Nach erstem Upgrade (oder 30s) | Auftrags-Bereich |
| 5 | `first_minigame` | Erstes MiniGame | (bestehende MiniGame-Tutorials) |
| 6 | `order_completed` | Erster Auftrag abgeschlossen | Geld-Anzeige |
| 7 | `worker_unlock` | Level 3 erreicht | Worker-Bereich |
| 8 | `shop_hint` | Level 5 | Shop-Tab |
| 9 | `research_hint` | Level 8 | Forschungs-Button |
| 10 | `building_hint` | Erstes Gebäude | Gebäude-Karte |
| 11 | `daily_challenge` | Erster Missionen-Tab-Besuch | Challenge-Bereich |
| 12 | `quick_jobs` | Level 10 | Quick-Jobs-Tab |
| 13 | `prestige_hint` | Prestige verfügbar | Prestige-Button |
| 14 | `guild_hint` | Level 15 | Gilden-Tab |
| 15 | `crafting_hint` | Crafting freigeschaltet | Crafting-Button |
| 16 | `battle_pass` | Battle Pass verfügbar | Battle-Pass-Button |
| 17 | `lucky_spin` | Tag 2 | Glücksrad-Button |
| 18 | `automation` | Auto-Collect freigeschaltet | Auto-Collect-Toggle |

### 3. Tooltip-Bubble Design

- Sprechblase mit Pfeil zum Ziel-Element
- Craft-Orange Rand, abgerundete Ecken, dezenter Shadow
- "Verstanden"-Button zum Dismissen (manuell, kein Auto-Hide)
- Slide-In-Animation (Opacity + TranslateY)
- Bleibt bis Spieler tippt

### 4. Technische Architektur

**Neuer `ContextualHintService`** ersetzt `TutorialService`:
- `IContextualHintService`: TryShowHint(hintId), DismissHint(), ResetAllHints()
- `ContextualHintService`: Prüft SeenHints-Set, feuert HintChanged-Event
- `ContextualHint` Model: HintId, TitleKey, TextKey, Position (Above/Below)

**GameState-Erweiterung:**
- `HashSet<string> SeenHints` (ersetzt TutorialCompleted/TutorialStep)

**UI: `TooltipBubbleControl`** (UserControl):
- Positioniert sich relativ zum Ziel-Element
- Pfeil zeigt zum Element (oben/unten)
- In MainView eingebettet, per Binding gesteuert

### 5. MiniGame-Tutorials

Bleiben unverändert (bereits kontextuell).
Einzige Verbesserung: Spezifischere Texte für Planing/TileLaying/Measuring.

### 6. Settings: "Tutorial zurücksetzen"

Button setzt SeenHints + SeenMiniGameTutorials zurück.

### 7. Entfernt

- TutorialService, TutorialStep.cs, TutorialDialog.axaml
- Tutorial-Code in MainViewModel.Dialogs.cs und Init.cs
- GameState.TutorialCompleted, GameState.TutorialStep
- 16 alte Tutorial RESX-Keys
