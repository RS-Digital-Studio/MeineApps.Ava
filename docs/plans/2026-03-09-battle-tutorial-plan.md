# Kampf-Tutorial Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Geführtes 5-Phasen Tutorial im ersten Kampf (P1, E005) mit ARIA-Erklärungen und Button-Highlighting.

**Architecture:** Tutorial-Modus direkt in BattleScene. `_isTutorialBattle` Flag steuert welche Aktions-Buttons aktiv sind. TutorialOverlay (existiert bereits) zeigt ARIA-Textboxen mit Highlight-Rects. Pro Phase wird genau eine Aktion freigeschaltet, nach Abschluss nächste Phase.

**Tech Stack:** SkiaSharp (Rendering), TutorialService + TutorialOverlay (existierend), RESX-Lokalisierung (6 Sprachen), SceneManager Overlay-System.

---

### Task 1: RESX-Keys für Tutorial-Texte (6 Sprachen)

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.resx` (EN)
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.de.resx` (DE)
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.es.resx` (ES)
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.fr.resx` (FR)
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.it.resx` (IT)
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Resources/Strings/AppStrings.pt.resx` (PT)

**Step 1:** Füge folgende 6 Keys in alle 6 RESX-Dateien ein:

| Key | DE | EN |
|-----|----|----|
| `tutorial_battle_intro` | Dein erster Kampf! Ich werde dich Schritt für Schritt durch die Grundlagen führen. | Your first battle! I'll guide you step by step. |
| `tutorial_battle_attack` | Tippe auf Angriff, um den Gegner zu treffen. | Tap Attack to hit the enemy. |
| `tutorial_battle_skill` | Der Gegner schlägt zurück! Nutze einen Skill für mehr Schaden. Skills kosten MP. | The enemy strikes back! Use a Skill for more damage. Skills cost MP. |
| `tutorial_battle_item` | Du bist schwer verwundet! Öffne Items und nutze einen Heiltrank. | You're badly wounded! Open Items and use a healing potion. |
| `tutorial_battle_dodge` | Manchmal ist Ausweichen die beste Wahl! Tippe auf Ausweichen. | Sometimes dodging is the best option! Tap Dodge. |
| `tutorial_battle_finish` | Gut gemacht! Jetzt besiege den Gegner! | Well done! Now defeat the enemy! |

**Step 2:** Übersetze für ES, FR, IT, PT (sinngemäß).

**Step 3:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 2: Starter-Item in Prolog-Story einbauen

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Data/Chapters/chapter_p1.json`

**Step 1:** Finde den Node VOR dem Battle-Node (E005 Kampf). Füge dort `"effects": { "addItems": ["C001"] }` hinzu, damit der Spieler 1x Heal Potion Small erhält bevor er kämpft.

**Step 2:** Verifiziere dass der `addItems`-Effekt in StoryEngine.ApplyEffects() korrekt verarbeitet wird (bereits implementiert laut CLAUDE.md).

**Step 3:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 3: Tutorial-State in BattleScene

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs`

**Step 1:** Füge Felder oben in der Klasse hinzu (nach den bestehenden Feldern):

```csharp
// Tutorial-System (geführter Prolog-Kampf)
private bool _isTutorialBattle;
private int _tutorialStep;       // 0=Intro, 1=Attack, 2=Skill, 3=Item, 4=Dodge, 5=Free
private bool _tutorialWaitForOverlay; // true wenn TutorialOverlay angezeigt wird
private string _tutorialAriaTitle = "";
```

**Step 2:** In `Setup()`, nach den bestehenden Initialisierungen, Tutorial erkennen:

```csharp
// Tutorial-Kampf erkennen (Prolog-Gegner + noch nicht gesehen)
var tutorialService = App.Services.GetService<TutorialService>();
_isTutorialBattle = enemy.IsProlog && (tutorialService?.ShouldShow("FirstBattle") ?? false);
_tutorialStep = 0;
_tutorialWaitForOverlay = false;
_tutorialAriaTitle = _localization.GetString("SystemName") ?? "ARIA";
```

**Step 3:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 4: Tutorial-Phasen-Steuerung

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs`

**Step 1:** Neue Methode `ShowTutorialHint()` die das TutorialOverlay zeigt:

```csharp
private void ShowTutorialHint(string messageKey, SKRect? highlightRect = null)
{
    _tutorialWaitForOverlay = true;
    var message = _localization.GetString(messageKey) ?? messageKey;
    var overlay = SceneManager.CreateOverlay<TutorialOverlay>();
    overlay.SetHint("battle_tutorial", _tutorialAriaTitle, message, highlightRect);
    overlay.Dismissed += OnTutorialDismissed;
    SceneManager.ShowOverlay(overlay);
}

private void OnTutorialDismissed()
{
    _tutorialWaitForOverlay = false;
}
```

**Step 2:** Neue Methode `AdvanceTutorialStep()` die nach jeder Aktion prüft ob Tutorial weitergeht:

```csharp
private void AdvanceTutorialStep()
{
    if (!_isTutorialBattle) return;
    _tutorialStep++;
}
```

**Step 3:** In `TransitionTo()` (oder dort wo `BattlePhase.PlayerTurn` gesetzt wird): Tutorial-Hints zeigen.

Finde die Stelle wo `_phase = BattlePhase.PlayerTurn` gesetzt wird und füge hinzu:

```csharp
// Tutorial-Hint zeigen wenn Tutorial aktiv
if (_isTutorialBattle && !_tutorialWaitForOverlay && _phase == BattlePhase.PlayerTurn)
{
    switch (_tutorialStep)
    {
        case 0: ShowTutorialHint("tutorial_battle_intro"); break;
        case 1: ShowTutorialHint("tutorial_battle_attack", _actionRects[0]); break;
        case 2: ShowTutorialHint("tutorial_battle_skill", _actionRects[2]); break;
        case 3: ShowTutorialHint("tutorial_battle_item", _actionRects[3]); break;
        case 4: ShowTutorialHint("tutorial_battle_dodge", _actionRects[1]); break;
        case 5: ShowTutorialHint("tutorial_battle_finish"); break;
    }
}
```

**Step 4:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 5: Aktions-Buttons einschränken (Tutorial-Modus)

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs`

**Step 1:** Neue Methode `IsTutorialActionEnabled(int actionIndex)`:

```csharp
/// <summary>
/// Prüft ob eine Aktion im Tutorial erlaubt ist.
/// 0=Angriff, 1=Ausweichen, 2=Skill, 3=Item
/// </summary>
private bool IsTutorialActionEnabled(int actionIndex)
{
    if (!_isTutorialBattle || _tutorialStep >= 6) return true;
    return _tutorialStep switch
    {
        1 => actionIndex == 0,  // Nur Angriff
        2 => actionIndex == 2,  // Nur Skill
        3 => actionIndex == 3,  // Nur Item
        4 => actionIndex == 1,  // Nur Ausweichen
        _ => true               // Intro/Free: alles
    };
}
```

**Step 2:** In `RenderActionButtons()`: Disabled-Buttons ausgegraut zeichnen.

Ändere die for-Schleife:

```csharp
for (int i = 0; i < 4; i++)
{
    var col = i % 2;
    var row = i / 2;
    var x = startX + col * (btnW + spacing);
    var y = startY + row * (btnH + spacing);
    _actionRects[i] = new SKRect(x, y, x + btnW, y + btnH);

    var enabled = IsTutorialActionEnabled(i);
    var isHovered = i == _hoveredAction && enabled;
    UIRenderer.DrawButton(canvas, _actionRects[i], _actionLabels[i],
        isHovered, !enabled, _actionColors[i]);
}
```

Hinweis: `UIRenderer.DrawButton` hat bereits einen `disabled`-Parameter (3. bool).

**Step 3:** In `HandleAction()`: Blockiere Aktionen die im Tutorial nicht erlaubt sind.

Am Anfang von `HandleAction()`:
```csharp
if (_isTutorialBattle && !IsTutorialActionEnabled(actionIndex))
    return;
```

**Step 4:** In `HandleAction()`: Nach erfolgreicher Aktion Tutorial weiterschalten.

Am Ende jedes case-Blocks (0=Attack, 1=Dodge, 2=Skill, 3=Item) rufe `AdvanceTutorialStep()` auf.

**Step 5:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 6: Gegner-Schaden Override für Tutorial Phase 3

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes\BattleScene.cs`

**Step 1:** Finde die Stelle wo der Gegner-Schaden am Spieler berechnet wird (in der EnemyAttack-Phase).

**Step 2:** Füge Tutorial-Override hinzu: In Phase 3 (Item-Tutorial) soll der Gegner genug Schaden machen, dass der Spieler auf ~30% HP fällt:

```csharp
// Tutorial-Override: In Schritt 3 (Item-Erklärung) Spieler auf ~30% HP bringen
if (_isTutorialBattle && _tutorialStep == 3)
{
    var targetHp = (int)(_player.MaxHp * 0.3f);
    if (_player.Hp > targetHp)
        damage = _player.Hp - targetHp;
}
```

**Step 3:** Sicherheit: In Tutorial-Schritt < 3 soll der Gegner-Schaden niedrig sein (max 10% HP), damit der Spieler nicht stirbt bevor das Item-Tutorial kommt.

**Step 4:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 7: Tutorial-Abschluss + Dodge-Override

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Scenes/BattleScene.cs`

**Step 1:** In der Dodge-Phase: Wenn Tutorial aktiv und Step 4, erzwinge erfolgreichen Dodge:

```csharp
// Tutorial: Dodge ist im Tutorial immer erfolgreich
if (_isTutorialBattle && _tutorialStep == 4)
    _dodgeSuccessful = true;
```

**Step 2:** Bei Victory: Tutorial als gesehen markieren:

```csharp
if (_isTutorialBattle)
{
    var tutorialService = App.Services.GetService<TutorialService>();
    tutorialService?.MarkSeen("FirstBattle");
}
```

**Step 3:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 8: TutorialOverlay Dismissed-Event prüfen/ergänzen

**Files:**
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Overlays/TutorialOverlay.cs` (falls nötig)

**Step 1:** Prüfe ob TutorialOverlay ein `Dismissed`-Event hat. Wenn nicht, füge eins hinzu:

```csharp
public event Action? Dismissed;
```

**Step 2:** Im HandleInput (Tap nach 500ms Gate): Trigger `Dismissed?.Invoke()` und `SceneManager.HideOverlay()`.

**Step 3:** Prüfe ob `SceneManager.CreateOverlay<T>()` existiert oder ob TutorialOverlay per DI erstellt werden muss.

**Step 4:** Baue: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

---

### Task 9: Build + Visueller Test

**Step 1:** Vollständiger Build: `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`

**Step 2:** Desktop-Test: `dotnet run --project src/Apps/RebornSaga/RebornSaga.Desktop`
- Neues Spiel starten → P1 durchspielen bis zum E005-Kampf
- Prüfen: Tutorial-Overlays erscheinen, Buttons werden korrekt eingeschränkt
- Prüfen: Heal Potion im Inventar vorhanden
- Prüfen: Dodge funktioniert im Tutorial

**Step 3:** CLAUDE.md aktualisieren mit Tutorial-System-Beschreibung.
