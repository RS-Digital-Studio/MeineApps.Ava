# Kampf-Tutorial Design (P1: Shadow Scout)

## Übersicht

Geführtes 5-Phasen Tutorial im ersten Kampf (P1, E005 Shadow Scout). Integration direkt in BattleScene als Tutorial-Modus (kein separater Scene-Typ). TutorialOverlay zeigt ARIA-Erklärungen mit Button-Highlighting.

## Aktivierung

- `_isTutorialBattle = true` wenn `enemy.isProlog && TutorialService.ShouldShow("FirstBattle")`
- Nach Abschluss: `TutorialService.MarkSeen("FirstBattle")`
- Tutorial-Modus deaktiviert freie Aktionswahl — nur der aktuelle Tutorial-Schritt ist wählbar

## Voraussetzungen (Story-Setup)

- Spieler startet P1 mit **1x Heal Potion Small (C001)** im Inventar (über Story-Effect `addItems`)
- Erster Skill der Klasse ist freigeschaltet (SkillService gibt Tier-1 Skills ab Level 1)
- E005: Level 1, 15 HP, 4 ATK, 1 DEF (schwach genug für Tutorial)

## Tutorial-Phasen (TutorialStep Enum)

### Phase 0: Intro
- TutorialOverlay: "Dein erster Kampf! ARIA wird dich durch die Grundlagen führen."
- Tap → weiter zu Phase 1

### Phase 1: Angriff
- Nur "Angriff"-Button aktiv (Skill, Item, Ausweichen ausgegraut)
- TutorialOverlay Highlight auf Angriff-Button
- ARIA: "Tippe auf **Angriff**, um den Gegner zu treffen."
- Nach Angriff: Gegner verliert ~4-5 HP, Phase → 2

### Phase 2: Gegner-Angriff + Skill-Erklärung
- Gegner greift automatisch an (normaler Schaden ~3-4 HP)
- TutorialOverlay nach Gegner-Angriff: "Der Gegner schlägt zurück! Nutze jetzt einen **Skill** für mehr Schaden."
- Nur "Skill"-Button aktiv
- Highlight auf Skill-Button → nach Tap: Skill-Auswahl öffnet sich
- Erster Skill vorausgewählt (Highlight), andere deaktiviert
- Nach Skill-Angriff: Gegner verliert mehr HP, Phase → 3

### Phase 3: Schwerer Gegner-Angriff + Item-Heilung
- Gegner macht Tutorial-Override-Schaden: Spieler auf ~30% HP bringen
- TutorialOverlay: "Du bist schwer verwundet! Öffne **Items** und nutze einen Heiltrank."
- Nur "Item"-Button aktiv
- Highlight auf Item-Button → Item-Liste zeigt Heal Potion
- Nach Item-Nutzung: HP restauriert, Phase → 4

### Phase 4: Ausweichen
- TutorialOverlay: "Manchmal ist es klüger auszuweichen! Tippe auf **Ausweichen**."
- Nur "Ausweichen"-Button aktiv
- Gegner-Angriff wird ausgewichen (Dodge-Animation)
- Phase → 5

### Phase 5: Finaler Angriff (frei)
- TutorialOverlay: "Gut gemacht! Jetzt besiege den Gegner mit einem Skill!"
- Alle Buttons aktiv (Tutorial-Restriktion aufgehoben)
- Kampf läuft normal weiter bis Victory
- Bei Victory: TutorialService.MarkSeen("FirstBattle")

## Technische Umsetzung

### BattleScene Erweiterungen
- `_isTutorialBattle: bool` — Tutorial-Modus aktiv
- `_tutorialStep: int` (0-5) — Aktueller Tutorial-Schritt
- `_tutorialOverlayShown: bool` — Ob gerade ein Overlay angezeigt wird
- `CheckTutorialStep()` — Wird nach jeder Aktion aufgerufen, steuert nächsten Schritt
- `GetEnabledActions()` — Gibt zurück welche Buttons im aktuellen Schritt aktiv sind
- `RenderActionButtons()` — Ausgegraut-Rendering für deaktivierte Buttons

### TutorialOverlay Integration
- `SceneManager.ShowOverlay<TutorialOverlay>()` mit Highlight-Rect des aktiven Buttons
- Nach Tap auf Overlay: `SceneManager.HideOverlay()`, Tutorial-Schritt aktiviert den Button
- Button-Rects werden in `RenderActionButtons()` berechnet und gecacht

### Enemy Damage Override
- In Phase 3: `_tutorialDamageOverride` setzt Gegner-Schaden so dass Spieler auf ~30% HP fällt
- Berechnung: `player.Hp * 0.7f` als Override-Schaden

### RESX-Keys (6 Sprachen)
- `tutorial_battle_intro` — "Dein erster Kampf! ARIA führt dich..."
- `tutorial_battle_attack` — "Tippe auf Angriff..."
- `tutorial_battle_skill` — "Nutze einen Skill für mehr Schaden..."
- `tutorial_battle_item` — "Du bist verwundet! Nutze einen Heiltrank..."
- `tutorial_battle_dodge` — "Manchmal ist Ausweichen klüger..."
- `tutorial_battle_finish` — "Gut gemacht! Besiege den Gegner!"

## Nicht im Scope
- Tutorial für Bosse (P2/P3 Bosse sind Story-driven, kein Tutorial)
- Tutorial für Shop/Inventar (eigenes Tutorial bei erstem Shop-Besuch)
- Tutorial-Reset-Option (nur via TutorialService.ResetAll Debug)
