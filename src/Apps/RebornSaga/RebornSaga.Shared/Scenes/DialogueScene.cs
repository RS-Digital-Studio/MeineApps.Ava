namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Services;
using RebornSaga.Engine;
using RebornSaga.Engine.Transitions;
using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Overlays;
using RebornSaga.Rendering.Backgrounds;
using RebornSaga.Rendering.Characters;
using RebornSaga.Rendering.Effects;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Dialog-Szene: Hintergrund + Charakter-Portraits + Typewriter-Text + Choices.
/// Kern der Visual Novel Gameplay-Loop.
/// </summary>
public class DialogueScene : Scene
{
    private readonly StoryEngine _storyEngine;
    private readonly IPreferencesService _preferences;
    private readonly SpriteCache _spriteCache;
    private float _time;
    private readonly TypewriterRenderer _typewriter = new();

    // Manga-Panel-Modus ("off", "dual", "triple")
    private string _mangaPanelMode = "off";

    // Glitch-Effekt (ARIA-Szenen)
    private readonly GlitchEffect _glitchEffect = new();

    // Kamera-Effekte (Zoom + Shake)
    private float _cameraZoom = 1f;
    private float _targetZoom = 1f;
    private float _cameraShakeTimer;
    private float _cameraShakeIntensity;

    // Aktueller Dialog-State
    private string _backgroundKey = "village";
    private readonly List<DialogueSpeaker> _activeSpeakers = new();
    private string _currentSpeakerName = "";
    private SKColor _currentSpeakerColor = UIRenderer.TextPrimary;
    private bool _isAutoMode;

    // Choices (wenn vorhanden, werden nach Text-Ende angezeigt)
    private bool _showChoices;
    private string[] _choiceLabels = Array.Empty<string>();
    private string?[] _choiceTags = Array.Empty<string?>();
    private SKRect[] _choiceRects = Array.Empty<SKRect>();
    private int _hoveredChoice = -1;
    private int _pressedChoice = -1;
    private HashSet<int>? _disabledChoices;
    private bool _choiceRectsValid; // Verhindert per-Frame Array-Allokation

    // Callback wenn eine Wahl getroffen wird
    public event Action<int>? ChoiceMade;

    // Callback wenn der Dialog weiter soll (nächster Node)
    public event Action? AdvanceRequested;

    // Auto-Mode Timer
    private float _autoTimer;
    private const float AutoDelay = 1.5f; // Sekunden nach Text-Ende

    // UI-Buttons (Skip, Auto, Backlog)
    private SKRect _skipButtonRect;
    private SKRect _autoButtonRect;
    private SKRect _logButtonRect;

    public DialogueScene(StoryEngine storyEngine, IPreferencesService preferences, SpriteCache spriteCache)
    {
        _storyEngine = storyEngine;
        _preferences = preferences;
        _spriteCache = spriteCache;
    }

    public override void OnEnter()
    {
        _time = 0;

        // Effekt-Feedback verdrahten (Karma, Affinität, EXP, Gold sichtbar machen)
        _storyEngine.EffectsApplied += OnEffectsApplied;

        // FateChanged-Overlay verdrahten
        _storyEngine.FateChangeTriggered += OnFateChangeTriggered;

        // Aktuellen Knoten aus StoryEngine präsentieren (falls vorhanden)
        if (_storyEngine.CurrentNode != null)
            PresentCurrentNode();
    }

    public override void OnExit()
    {
        _storyEngine.EffectsApplied -= OnEffectsApplied;
        _storyEngine.FateChangeTriggered -= OnFateChangeTriggered;
    }

    /// <summary>
    /// Zeigt Effekt-Benachrichtigungen (Karma, Affinität, EXP, Gold) als Floating-Overlay.
    /// </summary>
    private void OnEffectsApplied(StoryEffects effects)
    {
        // Nur anzeigen wenn relevante Effekte vorhanden (nicht für reine Flags/FateChanged)
        if (effects.Karma == 0 && effects.Exp == 0 && effects.Gold == 0
            && effects.Affinity == null && effects.AddItems == null)
            return;

        var overlay = SceneManager.ShowOverlay<EffectFeedbackOverlay>();
        overlay.SetEffects(effects);
    }

    /// <summary>
    /// Zeigt das FateChanged-Overlay bei Schicksals-Wendepunkten.
    /// </summary>
    private void OnFateChangeTriggered(string fateKey)
    {
        SceneManager.ShowOverlay<FateChangedOverlay>();
    }

    /// <summary>
    /// Setzt den Hintergrund der Szene.
    /// </summary>
    public void SetBackground(string key) { _backgroundKey = key; BackgroundCompositor.SetScene(key); }

    /// <summary>
    /// Zeigt eine Dialogzeile an (Sprecher + Text + optional Portrait).
    /// </summary>
    public void ShowDialogue(string speakerName, string text, SKColor nameColor,
        CharacterDefinition? portrait = null, string position = "center",
        Emotion emotion = Emotion.Neutral, Pose pose = Pose.Standing,
        float typewriterSpeed = 30f)
    {
        _currentSpeakerName = speakerName;
        _currentSpeakerColor = nameColor;

        // Benutzer-Einstellung anwenden wenn kein spezieller Speed gesetzt (30f = Default)
        if (typewriterSpeed == 30f)
        {
            var userSpeed = (TypewriterSpeed)_preferences.Get("settings_text_speed", 1);
            _typewriter.SetSpeed(userSpeed);
        }
        else
        {
            _typewriter.SetSpeed(typewriterSpeed);
        }

        _typewriter.SetText(text);
        _showChoices = false;
        _autoTimer = 0;

        // Portrait setzen
        if (portrait != null)
        {
            // Alle bisherigen Sprecher als inaktiv markieren
            foreach (var s in _activeSpeakers)
                s.IsActive = false;

            // Prüfen ob dieser Charakter schon angezeigt wird
            var existing = _activeSpeakers.Find(s => s.Definition.Id == portrait.Id);
            if (existing != null)
            {
                existing.IsActive = true;
                existing.Emotion = emotion;
                existing.Pose = pose;
                existing.Position = position;
            }
            else
            {
                _activeSpeakers.Add(new DialogueSpeaker
                {
                    Definition = portrait,
                    Position = position,
                    Emotion = emotion,
                    Pose = pose,
                    IsActive = true
                });

                // Max 2 Portraits gleichzeitig
                while (_activeSpeakers.Count > 2)
                    _activeSpeakers.RemoveAt(0);
            }
        }

        // Backlog-Eintrag hinzufügen
        BacklogOverlay.AddEntry(speakerName, text, nameColor);
    }

    /// <summary>
    /// Zeigt Choice-Buttons an (nach aktuellem Text).
    /// </summary>
    public void ShowChoices(string[] labels, string?[]? tags = null, HashSet<int>? disabled = null)
    {
        _choiceLabels = labels;
        _choiceTags = tags ?? new string?[labels.Length];
        _disabledChoices = disabled;
        _showChoices = true;
        _choiceRectsValid = false;
    }

    /// <summary>
    /// Entfernt alle Charakter-Portraits.
    /// </summary>
    public void ClearPortraits() => _activeSpeakers.Clear();

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
        _typewriter.Update(deltaTime);
        _glitchEffect.Update(deltaTime);

        // Kamera-Zoom interpolieren
        _cameraZoom += (_targetZoom - _cameraZoom) * deltaTime * 5f;
        if (_cameraShakeTimer > 0) _cameraShakeTimer -= deltaTime;

        // Auto-Mode: nach Text-Ende automatisch weiter
        if (_isAutoMode && _typewriter.IsComplete && !_showChoices)
        {
            _autoTimer += deltaTime;
            if (_autoTimer >= AutoDelay)
            {
                _autoTimer = 0;
                AdvanceRequested?.Invoke();
                AdvanceStory();
            }
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Kamera-Effekte (Zoom + Shake) — umklammert gesamtes Rendering
        canvas.Save();
        if (Math.Abs(_cameraZoom - 1f) > 0.001f)
        {
            canvas.Translate(bounds.MidX, bounds.MidY);
            canvas.Scale(_cameraZoom);
            canvas.Translate(-bounds.MidX, -bounds.MidY);
        }
        if (_cameraShakeTimer > 0)
        {
            var intensity = _cameraShakeIntensity * (_cameraShakeTimer / 0.3f);
            var offsetX = (float)(Random.Shared.NextDouble() * 2 - 1) * intensity;
            var offsetY = (float)(Random.Shared.NextDouble() * 2 - 1) * intensity;
            canvas.Translate(offsetX, offsetY);
        }

        // Manga-Panel-Modus: Dual-Panel mit Sprite-Rendering
        if (_mangaPanelMode == "dual" && _activeSpeakers.Count >= 2)
        {
            MangaPanelRenderer.RenderDualPanel(canvas, bounds,
                (c, r) => RenderSpeakerInPanel(c, r, _activeSpeakers[0]),
                (c, r) => RenderSpeakerInPanel(c, r, _activeSpeakers[1]));
        }
        else
        {
            // Standard-Rendering (kein Manga-Panel)

            // Hintergrund (hinter Charakteren)
            BackgroundCompositor.SetScene(_backgroundKey);
            BackgroundCompositor.RenderBack(canvas, bounds, _time);

            // Ambient-Licht-Tönung (umklammert Charaktere)
            BackgroundCompositor.BeginLighting(canvas);

            // Charakter-Portraits
            foreach (var speaker in _activeSpeakers)
            {
                CharacterRenderer.DrawPortrait(canvas, bounds, speaker.Definition,
                    speaker.Pose, speaker.Emotion, speaker.Position, _time, speaker.IsActive);
            }

            BackgroundCompositor.EndLighting(canvas);

            // Vordergrund + Partikel (über Charakteren)
            BackgroundCompositor.RenderFront(canvas, bounds, _time);
        }

        // Choice-Buttons (über der Dialogbox, nur wenn Text fertig)
        if (_showChoices && _typewriter.IsComplete)
        {
            // Rects nur neu berechnen wenn nötig (nicht per Frame)
            if (!_choiceRectsValid)
            {
                _choiceRects = ChoiceButtonRenderer.CalculateRects(bounds, _choiceLabels.Length, _choiceLabels);
                _choiceRectsValid = true;
            }
            ChoiceButtonRenderer.Render(canvas, _choiceRects, _choiceLabels,
                _choiceTags, _hoveredChoice, _pressedChoice, _disabledChoices, _time);
        }

        // Dialogbox
        DialogBoxRenderer.Render(canvas, bounds, _currentSpeakerName,
            _typewriter.VisibleText, _currentSpeakerColor,
            _typewriter.IsComplete && !_showChoices, _time);

        // UI-Buttons (oben rechts: Skip, Auto, Log)
        RenderUIButtons(canvas, bounds);

        // Glitch-Effekt als Post-Processing (über allem)
        _glitchEffect.Render(canvas, bounds);

        // Kamera-Restore
        canvas.Restore();
    }

    private void RenderUIButtons(SKCanvas canvas, SKRect bounds)
    {
        var btnW = bounds.Width * 0.12f;
        var btnH = bounds.Height * 0.055f;
        var btnY = bounds.Height * 0.01f;
        var btnSpacing = btnW + 5;

        _logButtonRect = new SKRect(
            bounds.Right - btnSpacing * 3, btnY,
            bounds.Right - btnSpacing * 2 - 5, btnY + btnH);

        _autoButtonRect = new SKRect(
            bounds.Right - btnSpacing * 2, btnY,
            bounds.Right - btnSpacing - 5, btnY + btnH);

        _skipButtonRect = new SKRect(
            bounds.Right - btnSpacing, btnY,
            bounds.Right - 5, btnY + btnH);

        UIRenderer.DrawButton(canvas, _logButtonRect, "Log", false, false, UIRenderer.TextMuted);
        UIRenderer.DrawButton(canvas, _autoButtonRect, _isAutoMode ? "Auto: An" : "Auto",
            false, false, _isAutoMode ? UIRenderer.Success : UIRenderer.TextMuted);
        UIRenderer.DrawButton(canvas, _skipButtonRect, "Skip", false, false, UIRenderer.TextMuted);
    }

    // ── Manga-Panel Hilfsmethoden ──

    /// <summary>
    /// Rendert einen Sprecher innerhalb eines Manga-Panels per SpriteCharacterRenderer.
    /// </summary>
    private void RenderSpeakerInPanel(SKCanvas canvas, SKRect rect, DialogueSpeaker speaker)
    {
        var cx = rect.MidX;
        var cy = rect.MidY;
        var scale = rect.Height / 1216f;
        SpriteCharacterRenderer.Draw(canvas, speaker.Definition.Id, speaker.Pose, speaker.Emotion,
            cx, cy, scale, _time, _spriteCache);
    }

    // ── Kamera-Effekte ──

    private void ZoomToSpeaker(float zoom) => _targetZoom = zoom;
    private void ZoomReset() => _targetZoom = 1f;
    private void ShakeCamera(float intensity, float duration)
    {
        _cameraShakeTimer = duration;
        _cameraShakeIntensity = intensity;
    }

    // ── StoryEngine-Integration ──

    /// <summary>
    /// Zeigt den aktuellen Story-Knoten an (Dialog-Zeile, Choices, Hintergrund).
    /// </summary>
    private void PresentCurrentNode()
    {
        var node = _storyEngine.CurrentNode;
        if (node == null) return;

        // Manga-Panel-Modus vom Knoten übernehmen
        _mangaPanelMode = node.MangaPanel ?? "off";

        // Hintergrund setzen (wenn vom Knoten definiert)
        if (!string.IsNullOrEmpty(node.BackgroundKey))
        {
            SetBackground(node.BackgroundKey);
        }

        // Alle Text-basierten Knotentypen mit Sprechern → Sprecher-Zeile anzeigen
        var isTextNode = node.Type is NodeType.Dialogue or NodeType.SystemMessage
            or NodeType.BondScene or NodeType.Cutscene or NodeType.FateChange;

        if (isTextNode && node.Speakers != null && node.Speakers.Count > 0)
        {
            PresentSpeakerLine(node.Speakers[_storyEngine.CurrentLineIndex]);
        }
        // Knotentyp: Choice → Optionen anzeigen
        else if (node.Type == NodeType.Choice && node.Options != null)
        {
            // Erst die letzte Sprecher-Zeile anzeigen (falls vorhanden, als Kontexttext)
            if (node.Speakers != null && node.Speakers.Count > 0)
                PresentSpeakerLine(node.Speakers[0]);

            // Choices vorbereiten
            var labels = node.Options.Select(o => _storyEngine.GetLocalizedText(o.TextKey)).ToArray();
            var tags = node.Options.Select(o => o.Tag).ToArray();
            ShowChoices(labels, tags);
        }
        // Andere Knotentypen → Szenenwechsel
        else
        {
            HandleNodeTypeChange();
        }
    }

    /// <summary>
    /// Zeigt eine einzelne Sprecher-Zeile an (Name, Text, Portrait).
    /// </summary>
    private void PresentSpeakerLine(SpeakerLine line)
    {
        var text = _storyEngine.GetLocalizedText(line.TextKey);
        var charDef = CharacterDefinitions.GetById(line.Character);
        var emotion = Enum.TryParse<Emotion>(line.Emotion, true, out var em) ? em : Emotion.Neutral;
        var pose = Enum.TryParse<Pose>(line.Pose, true, out var p) ? p : Pose.Standing;

        // Sprecher-Name: Character-ID groß geschrieben oder aus Definition
        var speakerName = charDef?.Id ?? line.Character;
        if (!string.IsNullOrEmpty(speakerName))
            speakerName = char.ToUpper(speakerName[0]) + speakerName[1..];

        // Sprecher-Farbe je nach Charakter
        var nameColor = charDef != null ? UIRenderer.Primary : UIRenderer.TextPrimary;

        ShowDialogue(speakerName, text, nameColor, charDef,
            line.Position ?? "center", emotion, pose, line.TypewriterSpeed);

        // Glitch-Effekt bei ARIA-Sprecher
        if (line.Character?.Equals("ARIA", StringComparison.OrdinalIgnoreCase) == true)
        {
            _glitchEffect.Start(0.7f, 0.8f);
        }

        // Kamera-Zoom bei emotionalen Momenten
        if (emotion is Emotion.Angry or Emotion.Surprised)
        {
            ZoomToSpeaker(1.05f);
            ShakeCamera(3f, 0.3f);
        }
        else
        {
            ZoomReset();
        }
    }

    /// <summary>
    /// Geht zur nächsten Zeile/Knoten im StoryEngine weiter.
    /// </summary>
    private void AdvanceStory()
    {
        // Noch Zeilen im aktuellen Knoten?
        if (_storyEngine.AdvanceToNextLine())
        {
            PresentCurrentNode();
            return;
        }

        // Keine weiteren Zeilen → nächsten Knoten laden
        if (_storyEngine.AdvanceToNext())
        {
            HandleNodeTypeChange();
            return;
        }

        // Kein nächster Knoten → Kapitel-Ende
        AdvanceToNextChapter();
    }

    /// <summary>
    /// Wechselt zum nächsten Kapitel, oder zur Overworld (Arc-Kapitel), oder zum Titel (letztes Kapitel).
    /// </summary>
    private async void AdvanceToNextChapter()
    {
        var currentId = _storyEngine.CurrentChapter?.Id;
        if (currentId == null)
        {
            SceneManager.ChangeScene<TitleScene>(new FadeTransition());
            return;
        }

        var nextId = StoryEngine.GetNextChapterId(currentId);

        if (nextId == null)
        {
            // Letztes Kapitel (k10) → Titel
            SceneManager.ChangeScene<TitleScene>(new FadeTransition());
            return;
        }

        try
        {
            await _storyEngine.LoadChapterAsync(nextId);

            // Arc-Kapitel (k1+) → Overworld-Map anzeigen
            if (nextId.StartsWith("k"))
            {
                SceneManager.ChangeScene<OverworldScene>(new FadeTransition());
            }
            else
            {
                // Prolog-Kapitel (p2, p3) → direkt in DialogueScene weiter
                PresentCurrentNode();
            }
        }
        catch
        {
            // Nächstes Kapitel nicht verfügbar → Titel
            SceneManager.ChangeScene<TitleScene>(new FadeTransition());
        }
    }

    /// <summary>
    /// Prüft den Knotentyp und wechselt bei Bedarf die Szene.
    /// </summary>
    private void HandleNodeTypeChange()
    {
        var node = _storyEngine.CurrentNode;
        if (node == null) return;

        switch (node.Type)
        {
            case NodeType.Dialogue:
            case NodeType.BondScene:
            case NodeType.Cutscene:
            case NodeType.FateChange:
            case NodeType.SystemMessage:
                // Bleibt in der DialogueScene
                PresentCurrentNode();
                break;

            case NodeType.Choice:
                // Choices anzeigen
                PresentCurrentNode();
                break;

            case NodeType.Battle:
                // Zum Kampf wechseln (BattleScene)
                SceneManager.ChangeScene<BattleScene>(new FadeTransition());
                break;

            case NodeType.Shop:
                SceneManager.PushScene<ShopScene>();
                break;

            case NodeType.Overworld:
                SceneManager.ChangeScene<OverworldScene>(new FadeTransition());
                break;

            case NodeType.ChapterEnd:
                AdvanceToNextChapter();
                break;

            case NodeType.ClassSelect:
                // Klassenwahl-Szene anzeigen (z.B. in K1)
                SceneManager.ChangeScene<ClassSelectScene>(new FadeTransition());
                break;

            default:
                // Unbekannter Typ → weiter
                if (_storyEngine.AdvanceToNext())
                    HandleNodeTypeChange();
                else
                    SceneManager.ChangeScene<TitleScene>(new FadeTransition());
                break;
        }
    }

    public override void HandlePointerDown(SKPoint position)
    {
        if (_showChoices && _typewriter.IsComplete)
        {
            for (int i = 0; i < _choiceRects.Length; i++)
            {
                if (UIRenderer.HitTest(_choiceRects[i], position))
                {
                    _pressedChoice = i;
                    return;
                }
            }
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _hoveredChoice = -1;
        if (_showChoices && _typewriter.IsComplete)
        {
            for (int i = 0; i < _choiceRects.Length; i++)
            {
                if (UIRenderer.HitTest(_choiceRects[i], position))
                {
                    _hoveredChoice = i;
                    return;
                }
            }
        }
    }

    public override void HandlePointerUp(SKPoint position)
    {
        _pressedChoice = -1;
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        switch (action)
        {
            case InputAction.Tap:
                // UI-Buttons prüfen
                if (UIRenderer.HitTest(_skipButtonRect, position))
                {
                    _typewriter.ShowAll();
                    return;
                }
                if (UIRenderer.HitTest(_autoButtonRect, position))
                {
                    _isAutoMode = !_isAutoMode;
                    return;
                }
                if (UIRenderer.HitTest(_logButtonRect, position))
                {
                    SceneManager.ShowOverlay<BacklogOverlay>();
                    return;
                }

                // Choice-Buttons
                if (_showChoices && _typewriter.IsComplete)
                {
                    for (int i = 0; i < _choiceRects.Length; i++)
                    {
                        if (UIRenderer.HitTest(_choiceRects[i], position) &&
                            !(_disabledChoices?.Contains(i) ?? false))
                        {
                            _showChoices = false;
                            ChoiceMade?.Invoke(i);
                            _storyEngine.MakeChoice(i);
                            HandleNodeTypeChange();
                            return;
                        }
                    }
                }

                // Text noch nicht fertig → alles anzeigen
                if (!_typewriter.IsComplete)
                {
                    _typewriter.ShowAll();
                    return;
                }

                // Text fertig, keine Choices → weiter
                if (!_showChoices)
                {
                    AdvanceRequested?.Invoke();
                    AdvanceStory();
                }
                break;

            case InputAction.Back:
                SceneManager.ShowOverlay<PauseOverlay>();
                break;
        }
    }
}

/// <summary>
/// Ein aktiver Sprecher/Portrait in der Dialog-Szene.
/// </summary>
public class DialogueSpeaker
{
    public CharacterDefinition Definition { get; set; } = null!;
    public string Position { get; set; } = "center";
    public Emotion Emotion { get; set; } = Emotion.Neutral;
    public Pose Pose { get; set; } = Pose.Standing;
    public bool IsActive { get; set; } = true;
}
