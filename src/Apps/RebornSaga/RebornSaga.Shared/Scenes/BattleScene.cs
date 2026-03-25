namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Localization;
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

/// <summary>
/// Kampf-Phasen: Steuerung des gesamten Kampfablaufs.
/// </summary>
public enum BattlePhase
{
    Intro,          // Gegner-Name + Level einblenden
    PlayerTurn,     // Spieler wählt Aktion
    SkillSelect,    // Skill-Auswahl (Untermenü)
    ItemSelect,     // Item-Auswahl (Untermenü)
    PlayerAttack,   // Angriffs-Animation
    PlayerSkillAttack, // Skill-Angriffs-Animation
    PlayerDodge,    // Ausweich-Animation
    EnemyTurn,      // Gegner greift an
    EnemyAttack,    // Gegner-Angriffs-Animation
    Victory,        // Sieg (EXP + Gold + Drops)
    Defeat,         // Niederlage
    BossPhaseChange, // Boss wechselt Phase (Mini-Cutscene)
    Done            // Kampf abgeschlossen, keine weitere Interaktion
}

/// <summary>
/// Floating-Damage-Number: Schwebt nach oben, Fade-Out.
/// </summary>
public struct FloatingNumber
{
    public float X, Y;
    public float VelocityY;
    public float Life;
    public string Text;
    public SKColor Color;
    public float FontSize;
    public bool IsActive;
}

/// <summary>
/// Vollständige Kampf-Szene: Intro, Aktionswahl, Animationen, HP-Balken,
/// Floating Damage Numbers, Combo-Counter, Boss-Phasen-Wechsel.
/// </summary>
public class BattleScene : Scene, IDisposable
{
    private readonly BattleEngine _battleEngine;
    private readonly SkillService _skillService;
    private readonly InventoryService _inventoryService;
    private readonly ProgressionService _progressionService;
    private readonly GoldService _goldService;
    private Player _player = null!;
    private Enemy _enemy = null!;

    // Kampf-Zustand
    private BattlePhase _phase;
    private float _phaseTimer;
    private float _time;
    private int _enemyHp;
    private int _enemyMaxHp;
    private int _currentBossPhase;
    private string _cachedEnemySpriteKey = ""; // Gecacht, nur bei Boss-Phasenwechsel aktualisiert
    private int _comboCount;
    private bool _dodgeSuccessful;

    // Skill-Auswahl
    private List<PlayerSkill> _availableSkills = new();
    private readonly SKRect[] _skillRects = new SKRect[6]; // Max 6 Skills anzeigen
    private SKRect _skillBackRect;
    private PlayerSkill? _selectedSkill;

    // Item-Auswahl
    private List<(Item item, int count)> _availableItems = new();
    private readonly SKRect[] _itemRects = new SKRect[6]; // Max 6 Items anzeigen
    private SKRect _itemBackRect;

    // Gecachte Strings (nur bei Änderung aktualisiert, vermeidet per-Frame Allokation)
    private string _cachedEnemyName = "";
    private string _cachedEnemyDisplayName = ""; // Ohne Level-Suffix (für Intro)
    private string _cachedEnemyIntroSub = "";    // "Lv.X" oder "Lv.X - Element"
    private string _cachedEnemyElemText = "";
    private string _cachedPlayerName = "";
    private string _cachedPlayerHp = "";
    private string _cachedPlayerMp = "";
    private string _cachedComboText = "";
    private int _lastPlayerHp, _lastPlayerMaxHp, _lastPlayerMp, _lastPlayerMaxMp;

    // Gecachtes Element (vermeidet per-Frame String-Parsing)
    private Element? _cachedEnemyElement;

    // Letzte Bounds (für Floating Numbers und Partikel)
    private SKRect _lastBounds;

    // Animationen
    private float _shakeIntensity;
    private float _playerFlashTimer;
    private float _enemyFlashTimer;
    private float _introProgress; // 0-1

    // Angriffs-Animation (Task 3.10): Spieler bewegt sich zum Gegner und zurück
    private const float AttackAnimDuration = 0.4f;
    private const float SlashDuration = 0.15f;
    private float _attackAnimTimer;
    private float _attackOffsetX;
    private float _slashTimer;
    private bool _showSlashEffect;

    // Dodge-Ghosting-Effekt (Task 3.11): Semi-transparente Nachbilder
    private const float DodgeDuration = 0.4f;
    private float _dodgeTimer;
    private float _dodgeOffsetX;

    // SplashArt für Ultimate-Skills (Task 3.12)
    private readonly SplashArtRenderer _splashArt = new();

    // Floating Damage Numbers (fester Pool)
    private readonly FloatingNumber[] _floatingNumbers = new FloatingNumber[16];

    // Partikel
    private readonly ParticleSystem _particles = new(300);

    // UI-Rects für Touch
    private readonly SKRect[] _actionRects = new SKRect[4];
    private int _hoveredAction = -1;
    private bool _actionsEnabled;

    // Tutorial-System (geführter Prolog-Kampf)
    private bool _isTutorialBattle;
    private int _tutorialStep;       // 0=Intro, 1=Attack, 2=Skill, 3=Item, 4=Dodge, 5=Free
    private bool _tutorialOverlayActive; // true während TutorialOverlay sichtbar
    private string _tutorialAriaTitle = "";

    // Temporärer ATK-Buff (wird am Kampfende zurückgesetzt)
    private int _tempAtkBonus;

    // GameOver-Overlay (Feld statt lokal, verhindert Event-Subscription-Stacking)
    private GameOverOverlay? _gameOverOverlay;

    // Ergebnis-Daten
    private int _earnedExp;
    private int _earnedGold;
    private readonly List<string> _earnedDrops = new();
    private readonly List<string> _earnedDropNames = new(); // Lokalisierte Display-Namen für Drops

    // Events
    public event Action<int, int, List<string>>? BattleWon; // (exp, gold, drops)
    public event Action? BattleLost;

    // Gecachte Paints (statisch, kein per-Frame-Allokation)
    private static readonly SKPaint _bgOverlayPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _hpBarBgPaint = new() { IsAntialias = true, Color = new SKColor(0x15, 0x18, 0x22, 200) };
    private static readonly SKPaint _hpBarFillPaint = new() { IsAntialias = true };
    private static readonly SKPaint _hpBarBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKPaint _comboPaint = new() { IsAntialias = true };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKFont _nameFont = new() { LinearMetrics = true };
    private static readonly SKFont _labelFont = new() { LinearMetrics = true };
    private static readonly SKFont _dmgFont = new() { LinearMetrics = true };
    private static readonly SKFont _comboFont = new() { LinearMetrics = true };
    private static readonly SKMaskFilter _glowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
    private static readonly SKPaint _ghostLayerPaint = new() { IsAntialias = false };

    // Aktions-Labels (lokalisiert)
    private readonly string[] _actionLabels = new string[4];
    private static readonly SKColor[] _actionColors =
    {
        new(0xE7, 0x4C, 0x3C), // Rot (Angriff)
        new(0x2E, 0xCC, 0x71), // Grün (Ausweichen)
        new(0x9B, 0x59, 0xB6), // Lila (Skill)
        new(0xF3, 0x9C, 0x12)  // Gold (Item)
    };

    private readonly StoryEngine _storyEngine;
    private readonly TutorialService? _tutorialService;
    private readonly IAudioService? _audioService;
    private readonly SpriteCache? _spriteCache;
    private readonly ILocalizationService _localization;

    // Gecachte lokalisierte UI-Strings
    private string _victoryText = "VICTORY!";
    private string _defeatText = "Fallen...";
    private string _tapToContinueText = "Tap to continue";
    private string _chooseSkillText = "Choose skill";
    private string _chooseItemText = "Choose item";
    private string _noSkillsText = "No skills available";
    private string _noItemsText = "No items available";
    private string _bossText = "BOSS";
    private string _notEnoughMpText = "Not enough MP!";
    private string _dodgedText = "Dodged!";
    private string _skillEvolutionText = "Skill Evolution!";
    private string _backText = "Back";
    private string _bossPhaseFormat = "Phase {0}";
    private string _comboLabel = "COMBO";

    public BattleScene(BattleEngine battleEngine, SkillService skillService,
        InventoryService inventoryService, ProgressionService progressionService,
        GoldService goldService, StoryEngine storyEngine,
        ILocalizationService localization,
        TutorialService? tutorialService = null,
        IAudioService? audioService = null, SpriteCache? spriteCache = null)
    {
        _battleEngine = battleEngine;
        _skillService = skillService;
        _inventoryService = inventoryService;
        _progressionService = progressionService;
        _goldService = goldService;
        _storyEngine = storyEngine;
        _localization = localization;
        _tutorialService = tutorialService;
        _audioService = audioService;
        _spriteCache = spriteCache;
        UpdateLocalizedTexts();
    }

    /// <summary>Aktualisiert alle gecachten lokalisierten Strings.</summary>
    private void UpdateLocalizedTexts()
    {
        _actionLabels[0] = _localization.GetString("Attack") ?? "Attack";
        _actionLabels[1] = _localization.GetString("Dodge") ?? "Dodge";
        _actionLabels[2] = _localization.GetString("Skill") ?? "Skill";
        _actionLabels[3] = _localization.GetString("Item") ?? "Item";
        _victoryText = _localization.GetString("Victory") ?? "VICTORY!";
        _defeatText = _localization.GetString("Defeat") ?? "Fallen...";
        _tapToContinueText = _localization.GetString("TapToContinue") ?? "Tap to continue";
        _chooseSkillText = _localization.GetString("ChooseSkill") ?? "Choose skill";
        _chooseItemText = _localization.GetString("ChooseItem") ?? "Choose item";
        _noSkillsText = _localization.GetString("NoSkillsAvailable") ?? "No skills available";
        _noItemsText = _localization.GetString("NoItemsAvailable") ?? "No items available";
        _bossText = _localization.GetString("Boss") ?? "BOSS";
        _comboLabel = _localization.GetString("Combo") ?? "COMBO";
        _notEnoughMpText = _localization.GetString("NotEnoughMp") ?? "Not enough MP!";
        _dodgedText = _localization.GetString("Dodged") ?? "Dodged!";
        _skillEvolutionText = _localization.GetString("SkillEvolution") ?? "Skill Evolution!";
        _backText = _localization.GetString("Back") ?? "Back";
        _bossPhaseFormat = _localization.GetString("BossPhase") ?? "Phase {0}";
    }

    /// <summary>Initialisiert den Kampf mit Spieler und Gegner.</summary>
    public void Setup(Player player, Enemy enemy)
    {
        _player = player;
        _enemy = enemy;
        _enemyHp = enemy.Hp;
        _enemyMaxHp = enemy.Hp;
        _currentBossPhase = 1;
        _comboCount = 0;
        _dodgeSuccessful = false;
        _phase = BattlePhase.Intro;
        _phaseTimer = 0;
        _introProgress = 0;
        _actionsEnabled = false;
        _shakeIntensity = 0;
        _playerFlashTimer = 0;
        _enemyFlashTimer = 0;
        _attackAnimTimer = 0;
        _attackOffsetX = 0;
        _slashTimer = 0;
        _showSlashEffect = false;
        _dodgeTimer = 0;
        _dodgeOffsetX = 0;
        _earnedExp = 0;
        _earnedGold = 0;
        _earnedDrops.Clear();
        _earnedDropNames.Clear();
        _particles.Clear();
        _tempAtkBonus = 0;
        _gameOverOverlay = null; // Altes Overlay-Referenz zurücksetzen bei neuem Kampf

        // Sprite-Key cachen (wird nur bei Boss-Phasenwechsel aktualisiert)
        UpdateEnemySpriteKey();

        // Gecachte Strings + Element einmal berechnen
        var enemyDisplayName = _localization.GetString(enemy.NameKey) ?? enemy.NameKey;
        _cachedEnemyDisplayName = enemyDisplayName;
        _cachedEnemyName = $"{enemyDisplayName} Lv.{enemy.Level}";
        _cachedEnemyElement = enemy.Element;
        _cachedEnemyElemText = _cachedEnemyElement.HasValue ? $"[{_cachedEnemyElement.Value}]" : "";
        _cachedEnemyIntroSub = enemy.Element.HasValue
            ? $"Lv.{enemy.Level} - {enemy.Element.Value}"
            : $"Lv.{enemy.Level}";
        _cachedPlayerName = $"{player.Name} Lv.{player.Level}";
        _cachedComboText = "";
        _lastPlayerHp = -1; // Erzwingt Update beim ersten Render
        _lastPlayerMaxHp = -1;
        _lastPlayerMp = -1;
        _lastPlayerMaxMp = -1;

        // Floating Numbers zurücksetzen
        for (int i = 0; i < _floatingNumbers.Length; i++)
            _floatingNumbers[i].IsActive = false;

        // Tutorial-Kampf erkennen (Malachar-Boss in P2 + noch nicht gesehen)
        _isTutorialBattle = enemy.Id == "B001" && (_tutorialService?.ShouldShow("FirstBattle") ?? false);
        _tutorialStep = 0;
        _tutorialOverlayActive = false;
        _tutorialAriaTitle = "ARIA";
    }

    /// <summary>Aktualisiert gecachte HP/MP-Strings nur bei Änderung.</summary>
    private void UpdateCachedStrings()
    {
        if (_player.Hp != _lastPlayerHp || _player.MaxHp != _lastPlayerMaxHp)
        {
            _cachedPlayerHp = $"{_player.Hp}/{_player.MaxHp}";
            _lastPlayerHp = _player.Hp;
            _lastPlayerMaxHp = _player.MaxHp;
        }
        if (_player.Mp != _lastPlayerMp || _player.MaxMp != _lastPlayerMaxMp)
        {
            _cachedPlayerMp = $"MP {_player.Mp}/{_player.MaxMp}";
            _lastPlayerMp = _player.Mp;
            _lastPlayerMaxMp = _player.MaxMp;
        }
    }

    public override void OnEnter()
    {
        _time = 0;

        // Hintergrund-Szene einmalig setzen (nicht pro Frame in Render)
        BackgroundCompositor.SetScene("battlefield");

        // Auto-Setup wenn kein manueller Setup()-Aufruf erfolgt ist
        if (_player is null)
            AutoSetupFromStoryEngine();
    }

    /// <summary>
    /// Holt Player und Enemy automatisch aus StoryEngine + EmbeddedResource.
    /// Fallback wenn Setup() nicht explizit aufgerufen wurde.
    /// </summary>
    private void AutoSetupFromStoryEngine()
    {
        var player = _storyEngine.GetPlayer();
        if (player == null) return;

        var enemyId = _storyEngine.CurrentNode?.EnemyId;
        var enemy = !string.IsNullOrEmpty(enemyId)
            ? EnemyLoader.GetById(enemyId)
            : EnemyLoader.GetById("E001"); // Fallback: Shadow Wolf

        if (enemy != null)
            Setup(player, enemy);
    }

    public override void Update(float deltaTime)
    {
        if (_player is null) return;
        _time += deltaTime;
        _phaseTimer += deltaTime;
        _particles.Update(deltaTime);

        // Tutorial: Overlay-Dismissal erkennen (Step 0 → 1 Übergang)
        if (_isTutorialBattle && _tutorialOverlayActive && SceneManager.Overlays.Count == 0)
        {
            _tutorialOverlayActive = false;
            if (_tutorialStep == 0)
            {
                _tutorialStep = 1;
                ShowTutorialHint("tutorial_battle_attack", _actionRects[0]);
            }
        }

        // Screen-Shake abklingen
        _shakeIntensity *= MathF.Pow(0.01f, deltaTime);
        if (_shakeIntensity < 0.5f) _shakeIntensity = 0;

        // Flash-Timer abklingen
        _playerFlashTimer = MathF.Max(0, _playerFlashTimer - deltaTime);
        _enemyFlashTimer = MathF.Max(0, _enemyFlashTimer - deltaTime);

        // Angriffs-Animation (Translate + Slash)
        if (_attackAnimTimer > 0)
        {
            _attackAnimTimer -= deltaTime;
            var half = AttackAnimDuration * 0.5f;
            if (_attackAnimTimer > half)
            {
                // Hinbewegung: 0 → 30px
                var t = 1f - (_attackAnimTimer - half) / half;
                _attackOffsetX = 30f * t;
            }
            else
            {
                // Rückbewegung: 30px → 0
                var t = _attackAnimTimer / half;
                _attackOffsetX = 30f * t;
            }

            // Slash-Effekt am Umkehrpunkt auslösen
            if (_attackAnimTimer <= half && !_showSlashEffect)
            {
                _slashTimer = SlashDuration;
                _showSlashEffect = true;
            }
        }
        else
        {
            _attackOffsetX = 0;
        }

        if (_slashTimer > 0)
        {
            _slashTimer -= deltaTime;
            _showSlashEffect = _slashTimer > 0;
        }

        // Dodge-Ghosting-Animation
        if (_dodgeTimer > 0)
        {
            _dodgeTimer -= deltaTime;
            var half = DodgeDuration * 0.5f;
            if (_dodgeTimer > half)
            {
                // Ausweichen nach rechts
                var t = 1f - (_dodgeTimer - half) / half;
                _dodgeOffsetX = 40f * t;
            }
            else
            {
                // Zurück zur Ausgangsposition
                var t = _dodgeTimer / half;
                _dodgeOffsetX = 40f * t;
            }
        }
        else
        {
            _dodgeOffsetX = 0;
        }

        // SplashArt-Effekt aktualisieren
        _splashArt.Update(deltaTime);

        // Floating Numbers aktualisieren
        UpdateFloatingNumbers(deltaTime);

        // Phasen-Logik
        switch (_phase)
        {
            case BattlePhase.Intro:
                _introProgress = Math.Min(1f, _phaseTimer / 1.5f);
                if (_phaseTimer >= 2f)
                    TransitionTo(BattlePhase.PlayerTurn);
                break;

            case BattlePhase.SkillSelect:
            case BattlePhase.ItemSelect:
                // Warten auf Auswahl (Input-gesteuert)
                break;

            case BattlePhase.PlayerAttack:
                if (_phaseTimer >= 0.8f)
                {
                    _phase = BattlePhase.EnemyTurn; // Sofort wechseln, verhindert Re-Entry
                    ExecutePlayerAttack();
                }
                break;

            case BattlePhase.PlayerSkillAttack:
                if (_phaseTimer >= 1.0f)
                {
                    _phase = BattlePhase.EnemyTurn; // Sofort wechseln, verhindert Re-Entry
                    ExecuteSkillAttack();
                }
                break;

            case BattlePhase.PlayerDodge:
                if (_phaseTimer >= 0.5f)
                    TransitionTo(_dodgeSuccessful ? BattlePhase.PlayerTurn : BattlePhase.EnemyTurn);
                break;

            case BattlePhase.EnemyTurn:
                if (_phaseTimer >= 0.5f)
                    TransitionTo(BattlePhase.EnemyAttack);
                break;

            case BattlePhase.EnemyAttack:
                if (_phaseTimer >= 0.8f)
                {
                    _phase = BattlePhase.PlayerTurn; // Sofort wechseln, verhindert Re-Entry
                    ExecuteEnemyAttack();
                }
                break;

            case BattlePhase.BossPhaseChange:
                if (_phaseTimer >= 2.5f)
                    TransitionTo(BattlePhase.PlayerTurn);
                break;

            case BattlePhase.Victory:
            case BattlePhase.Defeat:
                // Warten auf Tap
                break;
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        _lastBounds = bounds;

        // Hintergrund zeichnen (SetScene erfolgt einmalig in OnEnter)
        BackgroundCompositor.RenderBack(canvas, bounds, _time);

        // Guard: Setup() wurde noch nicht aufgerufen — nur Hintergrund zeigen
        if (_player is null) return;

        UpdateCachedStrings();

        // Screen-Shake anwenden
        if (_shakeIntensity > 0)
        {
            var shakeX = (MathF.Sin(_time * 40f) * _shakeIntensity);
            var shakeY = (MathF.Cos(_time * 35f) * _shakeIntensity * 0.7f);
            canvas.Save();
            canvas.Translate(shakeX, shakeY);
        }

        // Ambient-Licht-Tönung (umklammert Kampf-Grafiken, try/finally gegen Canvas-Korruption)
        BackgroundCompositor.BeginLighting(canvas);
        try
        {
            // Gegner (obere Hälfte)
            RenderEnemy(canvas, bounds);

            // Slash-Effekt am Gegner (3 diagonale Linien mit Alpha-Fade)
            if (_showSlashEffect)
                RenderSlashEffect(canvas, bounds);

            // Dodge-Ghosting: Semi-transparente Nachbilder des Spieler-HUD
            if (_dodgeTimer > 0)
            {
                var ghostAlpha = (byte)(60 * (_dodgeTimer / DodgeDuration));
                for (int g = 2; g >= 1; g--)
                {
                    canvas.Save();
                    canvas.Translate(_dodgeOffsetX * (g * 0.4f), 0);
                    _ghostLayerPaint.Color = SKColors.White.WithAlpha((byte)(ghostAlpha / g));
                    canvas.SaveLayer(_ghostLayerPaint);
                    RenderPlayerHud(canvas, bounds);
                    canvas.Restore(); // SaveLayer
                    canvas.Restore(); // Translate
                }
            }

            // Spieler-Info (untere Hälfte) mit Angriffs-/Dodge-Offset
            if (_attackOffsetX != 0 || _dodgeOffsetX != 0)
            {
                canvas.Save();
                canvas.Translate(_attackOffsetX + _dodgeOffsetX, 0);
                RenderPlayerHud(canvas, bounds);
                canvas.Restore();
            }
            else
            {
                RenderPlayerHud(canvas, bounds);
            }

            // Floating Damage Numbers
            RenderFloatingNumbers(canvas, bounds);

            // Combo-Counter
            if (_comboCount >= 3)
                RenderComboCounter(canvas, bounds);

            // Partikel
            _particles.Render(canvas);
        }
        finally
        {
            BackgroundCompositor.EndLighting(canvas);
        }

        // Vordergrund + atmosphärische Partikel (über Kampf-Grafiken)
        BackgroundCompositor.RenderFront(canvas, bounds, _time);

        // Phasen-spezifisches UI
        switch (_phase)
        {
            case BattlePhase.Intro:
                RenderIntro(canvas, bounds);
                break;
            case BattlePhase.PlayerTurn:
                RenderActionButtons(canvas, bounds);
                break;
            case BattlePhase.SkillSelect:
                RenderSkillSelect(canvas, bounds);
                break;
            case BattlePhase.ItemSelect:
                RenderItemSelect(canvas, bounds);
                break;
            case BattlePhase.Victory:
                RenderVictory(canvas, bounds);
                break;
            case BattlePhase.Defeat:
                RenderDefeat(canvas, bounds);
                break;
            case BattlePhase.BossPhaseChange:
                RenderBossPhaseChange(canvas, bounds);
                break;
        }

        if (_shakeIntensity > 0)
            canvas.Restore();

        // SplashArt-Overlay für Ultimate-Skills (über allem, nach Shake-Restore)
        _splashArt.Render(canvas, bounds);
    }

    // --- Render-Methoden ---

    private void RenderEnemy(SKCanvas canvas, SKRect bounds)
    {
        var margin = bounds.Width * 0.04f;

        // Gegner-Name + Level (gecachter String) — ganz oben
        var nameSize = bounds.Width * 0.04f;
        var nameY = bounds.Top + margin + nameSize;
        _nameFont.Size = nameSize;
        _textPaint.Color = UIRenderer.TextPrimary;
        canvas.DrawText(_cachedEnemyName, bounds.MidX, nameY, SKTextAlign.Center, _nameFont, _textPaint);

        // Element-Anzeige (wenn vorhanden, gecachter String)
        var elemH = 0f;
        if (_cachedEnemyElement.HasValue)
        {
            var elemColor = GetElementColor(_cachedEnemyElement.Value);
            _labelFont.Size = nameSize * 0.7f;
            _textPaint.Color = elemColor;
            elemH = nameSize * 1.0f;
            canvas.DrawText(_cachedEnemyElemText, bounds.MidX, nameY + elemH,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Gegner-HP-Balken
        var hpBarW = bounds.Width * 0.6f;
        var hpBarH = bounds.Height * 0.02f;
        var hpBarX = bounds.MidX - hpBarW / 2;
        var hpBarY = nameY + elemH + nameSize * 0.6f;
        DrawHpBar(canvas, hpBarX, hpBarY, hpBarW, hpBarH, _enemyHp, _enemyMaxHp, UIRenderer.Danger);

        // Gegner-Sprite: Füllt Raum von HP-Bar-Ende bis Spieler-HUD-Beginn (75%)
        var spriteTop = hpBarY + hpBarH + margin * 0.5f;
        var spriteBottom = bounds.Height * 0.72f;  // Vor dem Spieler-HUD aufhören
        var spriteH = spriteBottom - spriteTop;

        if (spriteH > 0)
        {
            // Seitenverhältnis des AI-Sprites beibehalten (Portrait ~0.68:1)
            // Breite maximal 85% des Screens
            var spriteW = MathF.Min(spriteH * 0.68f, bounds.Width * 0.85f);
            // Wenn Breite begrenzt, Höhe anpassen
            if (spriteW < spriteH * 0.68f)
                spriteH = spriteW / 0.68f;

            var spriteRect = new SKRect(
                bounds.MidX - spriteW * 0.5f, spriteTop,
                bounds.MidX + spriteW * 0.5f, spriteTop + spriteH);

            // Flash-Effekt bei Treffer (mit Overflow-Schutz)
            if (_enemyFlashTimer > 0)
            {
                var flashAlpha = (byte)Math.Min(255, _enemyFlashTimer * 5f * 255);
                _bgOverlayPaint.Color = SKColors.White.WithAlpha(flashAlpha);
                using var flashRect = new SKRoundRect(spriteRect, 8f);
                canvas.DrawRoundRect(flashRect, _bgOverlayPaint);
            }

            DrawEnemySprite(canvas, spriteRect, _time);
        }
    }

    private void DrawEnemySprite(SKCanvas canvas, SKRect rect, float time)
    {
        // AI-Sprite laden (gecacht über SpriteCache)
        var sprite = _spriteCache?.GetEnemySprite(_cachedEnemySpriteKey);

        if (sprite != null)
        {
            // AI-Sprite mit korrektem Seitenverhältnis zeichnen (Contain-Fit)
            var srcRect = new SKRect(0, 0, sprite.Width, sprite.Height);
            var srcAspect = (float)sprite.Width / sprite.Height;
            var dstAspect = rect.Width / rect.Height;
            SKRect destRect;
            if (srcAspect > dstAspect)
            {
                // Sprite breiter als Ziel: an Breite anpassen
                var h = rect.Width / srcAspect;
                var yOff = (rect.Height - h) / 2f;
                destRect = new SKRect(rect.Left, rect.Top + yOff, rect.Right, rect.Top + yOff + h);
            }
            else
            {
                // Sprite höher als Ziel: an Höhe anpassen
                var w = rect.Height * srcAspect;
                var xOff = (rect.Width - w) / 2f;
                destRect = new SKRect(rect.Left + xOff, rect.Top, rect.Left + xOff + w, rect.Bottom);
            }
            canvas.DrawBitmap(sprite, srcRect, destRect);

            // Treffer-Flash (weißer Overlay bei Schaden)
            if (_enemyFlashTimer > 0f)
            {
                var flashAlpha = (byte)Math.Min(255, 180 * _enemyFlashTimer);
                _bgOverlayPaint.Color = SKColors.White.WithAlpha(flashAlpha);
                canvas.DrawRect(rect, _bgOverlayPaint);
            }

            // Enraged-Overlay (Boss ab Phase 4+): roter Schimmer
            if (_currentBossPhase >= 4)
            {
                _bgOverlayPaint.Color = new SKColor(0xDC, 0x26, 0x26, 40);
                canvas.DrawRect(rect, _bgOverlayPaint);
            }
        }
        else
        {
            // Fallback: Prozeduraler Gegner (bis AI-Assets vorhanden)
            DrawProceduralEnemy(canvas, rect, time);
        }
    }

    /// <summary>
    /// Berechnet und cached den Sprite-Key für den Gegner.
    /// Dateiname: {Id}_{nameKey_suffix}.webp, z.B. "E005_shadow_scout", "B001_malachar_p2"
    /// Wird bei Setup und Boss-Phasenwechsel aufgerufen (nicht per Frame).
    /// </summary>
    private void UpdateEnemySpriteKey()
    {
        // NameKey-Suffix extrahieren: "enemy_shadow_scout" → "shadow_scout", "boss_malachar" → "malachar"
        var nameSuffix = _enemy.NameKey;
        if (nameSuffix.StartsWith("enemy_"))
            nameSuffix = nameSuffix[6..];
        else if (nameSuffix.StartsWith("boss_"))
            nameSuffix = nameSuffix[5..];

        var baseKey = $"{_enemy.Id}_{nameSuffix}";

        // Boss: Phasen-basiertes Sprite (Phases > 1 = Boss, _p2/_p3 Suffix)
        _cachedEnemySpriteKey = (_enemy.Phases > 1 && _currentBossPhase > 1)
            ? $"{baseKey}_p{_currentBossPhase}"
            : baseKey;
    }

    /// <summary>
    /// Altes prozedurales System als Fallback (dunkle Silhouette mit Element-Farbe).
    /// </summary>
    private void DrawProceduralEnemy(SKCanvas canvas, SKRect rect, float time)
    {
        var color = _enemy.Element.HasValue
            ? GetElementColor(_enemy.Element.Value)
            : UIRenderer.Danger;

        // Körper (ovale Silhouette)
        _bgOverlayPaint.Color = new SKColor(0x15, 0x15, 0x20, 220);
        canvas.DrawOval(rect.MidX, rect.MidY, rect.Width * 0.5f, rect.Height * 0.5f, _bgOverlayPaint);

        // Inneres Leuchten (pulsierend)
        var glowSize = 0.35f + MathF.Sin(time * 2f) * 0.05f;
        _bgOverlayPaint.Color = color.WithAlpha(80);
        canvas.DrawOval(rect.MidX, rect.MidY, rect.Width * glowSize, rect.Height * glowSize, _bgOverlayPaint);

        // Augen (zwei leuchtende Punkte)
        var eyeY = rect.MidY - rect.Height * 0.1f;
        var eyeSpacing = rect.Width * 0.12f;
        _bgOverlayPaint.Color = color.WithAlpha(240);
        canvas.DrawCircle(rect.MidX - eyeSpacing, eyeY, rect.Width * 0.04f, _bgOverlayPaint);
        canvas.DrawCircle(rect.MidX + eyeSpacing, eyeY, rect.Width * 0.04f, _bgOverlayPaint);

        // Boss-Indikator: Krone/Hörner
        if (_enemy.Phases > 1)
        {
            _glowPaint.Color = color.WithAlpha(150);
            _glowPaint.MaskFilter = _glowBlur;
            var hornY = rect.Top + rect.Height * 0.15f;
            canvas.DrawLine(rect.MidX - eyeSpacing, hornY, rect.MidX - eyeSpacing * 1.5f, hornY - rect.Height * 0.15f, _glowPaint);
            canvas.DrawLine(rect.MidX + eyeSpacing, hornY, rect.MidX + eyeSpacing * 1.5f, hornY - rect.Height * 0.15f, _glowPaint);
            _glowPaint.MaskFilter = null;
        }
    }

    /// <summary>Slash-Effekt: 3 diagonale Linien am Gegner mit Alpha-Fade.</summary>
    private void RenderSlashEffect(SKCanvas canvas, SKRect bounds)
    {
        var alpha = (byte)(255 * (_slashTimer / SlashDuration));
        var cx = bounds.MidX;
        var cy = bounds.Height * 0.40f;  // Mitte des Gegner-Sprite-Bereichs
        var len = bounds.Width * 0.15f;

        _glowPaint.Color = SKColors.White.WithAlpha(alpha);
        _glowPaint.StrokeWidth = 3f;
        _glowPaint.MaskFilter = _glowBlur;

        // 3 diagonale Slash-Linien (versetzt)
        for (int i = -1; i <= 1; i++)
        {
            var offsetX = i * len * 0.25f;
            canvas.DrawLine(
                cx + offsetX - len * 0.5f, cy - len * 0.6f,
                cx + offsetX + len * 0.5f, cy + len * 0.6f,
                _glowPaint);
        }

        _glowPaint.MaskFilter = null;
        _glowPaint.StrokeWidth = 2f; // Originalwert zurücksetzen
    }

    private void RenderPlayerHud(SKCanvas canvas, SKRect bounds)
    {
        var hudY = bounds.Height * 0.75f;
        var hudMargin = bounds.Width * 0.05f;
        var hudW = bounds.Width - 2 * hudMargin;

        // Spieler-Name + Level (gecachter String)
        _labelFont.Size = bounds.Width * 0.035f;
        _textPaint.Color = UIRenderer.PrimaryGlow;
        canvas.DrawText(_cachedPlayerName, hudMargin, hudY,
            SKTextAlign.Left, _labelFont, _textPaint);

        // HP-Balken
        var barH = bounds.Height * 0.02f;
        var barY = hudY + barH * 0.8f;
        DrawHpBar(canvas, hudMargin, barY, hudW * 0.65f, barH, _player.Hp, _player.MaxHp, UIRenderer.Success);

        // HP-Text (gecachter String)
        _labelFont.Size = barH * 1.2f;
        _textPaint.Color = UIRenderer.TextPrimary;
        canvas.DrawText(_cachedPlayerHp, hudMargin + hudW * 0.67f, barY + barH * 0.8f,
            SKTextAlign.Left, _labelFont, _textPaint);

        // MP-Balken
        var mpBarY = barY + barH * 2f;
        DrawHpBar(canvas, hudMargin, mpBarY, hudW * 0.65f, barH * 0.7f,
            _player.Mp, _player.MaxMp, new SKColor(0x58, 0xA6, 0xFF));

        // MP-Text (gecachter String)
        _labelFont.Size = barH;
        _textPaint.Color = UIRenderer.TextSecondary;
        canvas.DrawText(_cachedPlayerMp, hudMargin + hudW * 0.67f, mpBarY + barH * 0.6f,
            SKTextAlign.Left, _labelFont, _textPaint);

        // Flash bei Spieler-Treffer (mit Overflow-Schutz)
        if (_playerFlashTimer > 0)
        {
            var flashAlpha = (byte)Math.Min(255, _playerFlashTimer * 3f * 80);
            _bgOverlayPaint.Color = UIRenderer.Danger.WithAlpha(flashAlpha);
            canvas.DrawRect(0, hudY - 20, bounds.Width, bounds.Height - hudY + 20, _bgOverlayPaint);
        }
    }

    private void DrawHpBar(SKCanvas canvas, float x, float y, float w, float h,
        int current, int max, SKColor color)
    {
        // Hintergrund
        canvas.DrawRect(x, y, w, h, _hpBarBgPaint);

        // Füllung
        var ratio = max > 0 ? Math.Clamp((float)current / max, 0f, 1f) : 0f;
        _hpBarFillPaint.Color = color;
        canvas.DrawRect(x, y, w * ratio, h, _hpBarFillPaint);

        // Kritische HP: Pulsierendes Rot
        if (ratio < 0.25f && ratio > 0)
        {
            var pulseAlpha = (byte)(100 + MathF.Sin(_time * 5f) * 80);
            _hpBarFillPaint.Color = UIRenderer.Danger.WithAlpha(pulseAlpha);
            canvas.DrawRect(x, y, w * ratio, h, _hpBarFillPaint);
        }

        // Rand
        _hpBarBorderPaint.Color = color.WithAlpha(80);
        canvas.DrawRect(x, y, w, h, _hpBarBorderPaint);
    }

    private void RenderActionButtons(SKCanvas canvas, SKRect bounds)
    {
        _actionsEnabled = true;
        var btnW = bounds.Width * 0.42f;
        var btnH = bounds.Height * 0.055f;
        var spacing = bounds.Height * 0.01f;
        var startX = bounds.MidX - btnW - spacing / 2;
        var startY = bounds.Height * 0.87f;

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
                isHovered, false, _actionColors[i], disabled: !enabled);
        }
    }

    private void RenderIntro(SKCanvas canvas, SKRect bounds)
    {
        // Dunkles Overlay das langsam transparenter wird
        var overlayAlpha = (byte)((1f - _introProgress) * 180);
        _bgOverlayPaint.Color = SKColors.Black.WithAlpha(overlayAlpha);
        canvas.DrawRect(bounds, _bgOverlayPaint);

        // Gegner-Name einblenden (Slide von rechts)
        var slideOffset = (1f - Math.Min(1f, _introProgress * 2f)) * bounds.Width * 0.3f;
        var introY = bounds.Height * 0.4f;

        _nameFont.Size = bounds.Width * 0.06f;
        _textPaint.Color = UIRenderer.Danger.WithAlpha((byte)(_introProgress * 255));
        canvas.DrawText(_cachedEnemyDisplayName, bounds.MidX + slideOffset, introY,
            SKTextAlign.Center, _nameFont, _textPaint);

        // Level + Element (gecacht in Setup)
        _labelFont.Size = bounds.Width * 0.035f;
        _textPaint.Color = UIRenderer.TextSecondary.WithAlpha((byte)(_introProgress * 200));
        canvas.DrawText(_cachedEnemyIntroSub, bounds.MidX + slideOffset * 0.5f, introY + _nameFont.Size * 1.5f,
            SKTextAlign.Center, _labelFont, _textPaint);

        // Boss-Warnung
        if (_enemy.Phases > 1 && _introProgress > 0.5f)
        {
            var bossAlpha = (byte)((_introProgress - 0.5f) * 2f * 255);
            _nameFont.Size = bounds.Width * 0.045f;
            _textPaint.Color = UIRenderer.Accent.WithAlpha(bossAlpha);
            canvas.DrawText(_bossText, bounds.MidX, introY + _nameFont.Size * 3.5f,
                SKTextAlign.Center, _nameFont, _textPaint);
        }
    }

    private void RenderVictory(SKCanvas canvas, SKRect bounds)
    {
        // Dunkles Overlay
        _bgOverlayPaint.Color = new SKColor(0x00, 0x00, 0x00, 160);
        canvas.DrawRect(bounds, _bgOverlayPaint);

        var centerY = bounds.Height * 0.3f;

        // Sieg-Text (lokalisiert)
        UIRenderer.DrawTextWithShadow(canvas, _victoryText, bounds.MidX, centerY,
            bounds.Width * 0.08f, UIRenderer.Accent);

        // EXP + Gold
        var infoY = centerY + bounds.Height * 0.1f;
        _labelFont.Size = bounds.Width * 0.04f;

        _textPaint.Color = UIRenderer.PrimaryGlow;
        canvas.DrawText($"+{_earnedExp} EXP", bounds.MidX, infoY,
            SKTextAlign.Center, _labelFont, _textPaint);

        _textPaint.Color = UIRenderer.Accent;
        canvas.DrawText($"+{_earnedGold} Gold", bounds.MidX, infoY + _labelFont.Size * 1.8f,
            SKTextAlign.Center, _labelFont, _textPaint);

        // Drops (lokalisierte Namen statt roher ItemIds)
        if (_earnedDropNames.Count > 0)
        {
            var dropY = infoY + _labelFont.Size * 3.6f;
            _textPaint.Color = UIRenderer.Success;
            _labelFont.Size = bounds.Width * 0.035f;
            foreach (var dropName in _earnedDropNames)
            {
                canvas.DrawText($"+ {dropName}", bounds.MidX, dropY,
                    SKTextAlign.Center, _labelFont, _textPaint);
                dropY += _labelFont.Size * 1.5f;
            }
        }

        // Tippen zum Fortfahren (lokalisiert)
        if (_phaseTimer > 1.5f)
        {
            var tapAlpha = (byte)(128 + MathF.Sin(_time * 3f) * 80);
            _labelFont.Size = bounds.Width * 0.03f;
            _textPaint.Color = UIRenderer.TextMuted.WithAlpha(tapAlpha);
            canvas.DrawText(_tapToContinueText, bounds.MidX, bounds.Height * 0.85f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
    }

    private void RenderDefeat(SKCanvas canvas, SKRect bounds)
    {
        // Rotes Overlay
        var fadeIn = Math.Min(1f, _phaseTimer / 1f);
        _bgOverlayPaint.Color = new SKColor(0x20, 0x00, 0x00, (byte)(fadeIn * 200));
        canvas.DrawRect(bounds, _bgOverlayPaint);

        var centerY = bounds.Height * 0.35f;

        UIRenderer.DrawTextWithShadow(canvas, _defeatText, bounds.MidX, centerY,
            bounds.Width * 0.07f, UIRenderer.Danger);

        if (_phaseTimer > 1.5f)
        {
            var tapAlpha = (byte)(128 + MathF.Sin(_time * 3f) * 80);
            _labelFont.Size = bounds.Width * 0.03f;
            _textPaint.Color = UIRenderer.TextMuted.WithAlpha(tapAlpha);
            canvas.DrawText(_tapToContinueText, bounds.MidX, bounds.Height * 0.85f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
    }

    private void RenderBossPhaseChange(SKCanvas canvas, SKRect bounds)
    {
        // Dramatische Verdunkelung + Flash
        var progress = _phaseTimer / 2.5f;
        byte overlayAlpha;

        if (progress < 0.3f)
            overlayAlpha = (byte)(progress / 0.3f * 200);
        else if (progress < 0.5f)
            overlayAlpha = 200;
        else
            overlayAlpha = (byte)((1f - progress) * 2f * 200);

        _bgOverlayPaint.Color = SKColors.Black.WithAlpha(overlayAlpha);
        canvas.DrawRect(bounds, _bgOverlayPaint);

        // "Phase X" Text mit Glitch-Effekt
        if (progress > 0.2f && progress < 0.8f)
        {
            var textAlpha = (byte)(Math.Min(1f, (progress - 0.2f) * 5f) * 255);
            var glitchOffset = MathF.Sin(_time * 30f) * (1f - progress) * 10f;

            _nameFont.Size = bounds.Width * 0.06f;
            _textPaint.Color = UIRenderer.Danger.WithAlpha(textAlpha);
            canvas.DrawText(string.Format(_bossPhaseFormat, _currentBossPhase), bounds.MidX + glitchOffset, bounds.MidY,
                SKTextAlign.Center, _nameFont, _textPaint);

            // Rote Glow-Linien
            _glowPaint.Color = UIRenderer.Danger.WithAlpha((byte)(textAlpha / 2));
            _glowPaint.MaskFilter = _glowBlur;
            canvas.DrawLine(bounds.Left, bounds.MidY, bounds.Right, bounds.MidY, _glowPaint);
            _glowPaint.MaskFilter = null;
        }
    }

    private void RenderComboCounter(SKCanvas canvas, SKRect bounds)
    {
        var comboX = bounds.Right - bounds.Width * 0.12f;
        var comboY = bounds.Height * 0.5f;

        // Pulsierender Glow
        var pulse = 1f + MathF.Sin(_time * 4f) * 0.1f;
        _comboFont.Size = bounds.Width * 0.06f * pulse;
        _comboPaint.Color = UIRenderer.Accent;
        canvas.DrawText(_cachedComboText, comboX, comboY,
            SKTextAlign.Center, _comboFont, _comboPaint);

        _labelFont.Size = bounds.Width * 0.025f;
        _comboPaint.Color = UIRenderer.Accent.WithAlpha(180);
        canvas.DrawText(_comboLabel, comboX, comboY + _comboFont.Size * 0.8f,
            SKTextAlign.Center, _labelFont, _comboPaint);
    }

    // --- Skill-/Item-Auswahl ---

    private void RenderSkillSelect(SKCanvas canvas, SKRect bounds)
    {
        // Halbtransparentes Panel über den Aktions-Buttons
        var panelW = bounds.Width * 0.9f;
        var panelH = bounds.Height * 0.35f;
        var panelRect = new SKRect(
            bounds.MidX - panelW / 2, bounds.Height - panelH - 8f,
            bounds.MidX + panelW / 2, bounds.Height - 8f);

        _bgOverlayPaint.Color = UIRenderer.PanelBg.WithAlpha(230);
        using var rr = new SKRoundRect(panelRect, 8f);
        canvas.DrawRoundRect(rr, _bgOverlayPaint);

        // Titel
        _labelFont.Size = bounds.Width * 0.035f;
        _textPaint.Color = new SKColor(0x9B, 0x59, 0xB6); // Lila für Skills
        canvas.DrawText(_chooseSkillText, panelRect.MidX, panelRect.Top + 22f,
            SKTextAlign.Center, _labelFont, _textPaint);

        // Skills auflisten
        var itemH = panelH * 0.13f;
        var startY = panelRect.Top + 35f;
        var pad = panelW * 0.04f;

        for (int i = 0; i < Math.Min(_availableSkills.Count, 6); i++)
        {
            var skill = _availableSkills[i];
            var y = startY + i * (itemH + 4f);
            _skillRects[i] = new SKRect(panelRect.Left + pad, y, panelRect.Right - pad, y + itemH);

            // Hintergrund (grau wenn nicht genug MP)
            var canUse = _player.Mp >= skill.Definition.MpCost;
            _bgOverlayPaint.Color = canUse
                ? new SKColor(0x9B, 0x59, 0xB6, 40)
                : new SKColor(0x40, 0x40, 0x40, 40);
            using var itemRr = new SKRoundRect(_skillRects[i], 4f);
            canvas.DrawRoundRect(itemRr, _bgOverlayPaint);

            // Skill-Name
            _labelFont.Size = itemH * 0.45f;
            _textPaint.Color = canUse ? UIRenderer.TextPrimary : UIRenderer.TextMuted;
            var skillName = _localization.GetString(skill.Definition.NameKey) ?? skill.Definition.NameKey;
            canvas.DrawText(skillName, _skillRects[i].Left + 10f,
                _skillRects[i].MidY + itemH * 0.12f, SKTextAlign.Left, _labelFont, _textPaint);

            // MP-Kosten rechts
            _textPaint.Color = canUse ? new SKColor(0x58, 0xA6, 0xFF) : UIRenderer.TextMuted;
            canvas.DrawText($"{skill.Definition.MpCost} MP", _skillRects[i].Right - 10f,
                _skillRects[i].MidY + itemH * 0.12f, SKTextAlign.Right, _labelFont, _textPaint);

            // Element-Anzeige (wenn vorhanden)
            if (skill.Definition.Element.HasValue)
            {
                var elemColor = GetElementColor(skill.Definition.Element.Value);
                _labelFont.Size = itemH * 0.35f;
                _textPaint.Color = elemColor.WithAlpha(180);
                canvas.DrawText($"[{skill.Definition.Element.Value}]", _skillRects[i].MidX,
                    _skillRects[i].MidY + itemH * 0.12f, SKTextAlign.Center, _labelFont, _textPaint);
            }
        }

        // Zurück-Button
        var backH = itemH * 0.9f;
        var backY = panelRect.Bottom - backH - 8f;
        _skillBackRect = new SKRect(panelRect.MidX - 60f, backY, panelRect.MidX + 60f, backY + backH);
        UIRenderer.DrawButton(canvas, _skillBackRect, _backText, false, false, UIRenderer.TextMuted);

        // Hinweis wenn keine Skills
        if (_availableSkills.Count == 0)
        {
            _labelFont.Size = bounds.Width * 0.03f;
            _textPaint.Color = UIRenderer.TextMuted;
            canvas.DrawText(_noSkillsText, panelRect.MidX, panelRect.MidY,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
    }

    private void RenderItemSelect(SKCanvas canvas, SKRect bounds)
    {
        // Halbtransparentes Panel
        var panelW = bounds.Width * 0.9f;
        var panelH = bounds.Height * 0.35f;
        var panelRect = new SKRect(
            bounds.MidX - panelW / 2, bounds.Height - panelH - 8f,
            bounds.MidX + panelW / 2, bounds.Height - 8f);

        _bgOverlayPaint.Color = UIRenderer.PanelBg.WithAlpha(230);
        using var rr = new SKRoundRect(panelRect, 8f);
        canvas.DrawRoundRect(rr, _bgOverlayPaint);

        // Titel
        _labelFont.Size = bounds.Width * 0.035f;
        _textPaint.Color = UIRenderer.Accent; // Gold für Items
        canvas.DrawText(_chooseItemText, panelRect.MidX, panelRect.Top + 22f,
            SKTextAlign.Center, _labelFont, _textPaint);

        // Items auflisten
        var itemH = panelH * 0.13f;
        var startY = panelRect.Top + 35f;
        var pad = panelW * 0.04f;

        for (int i = 0; i < Math.Min(_availableItems.Count, 6); i++)
        {
            var (item, count) = _availableItems[i];
            var y = startY + i * (itemH + 4f);
            _itemRects[i] = new SKRect(panelRect.Left + pad, y, panelRect.Right - pad, y + itemH);

            // Hintergrund
            _bgOverlayPaint.Color = new SKColor(0xF3, 0x9C, 0x12, 30);
            using var itemRr = new SKRoundRect(_itemRects[i], 4f);
            canvas.DrawRoundRect(itemRr, _bgOverlayPaint);

            // Item-Name
            _labelFont.Size = itemH * 0.45f;
            _textPaint.Color = UIRenderer.TextPrimary;
            var itemName = _localization.GetString(item.NameKey) ?? item.NameKey;
            canvas.DrawText(itemName, _itemRects[i].Left + 10f,
                _itemRects[i].MidY + itemH * 0.12f, SKTextAlign.Left, _labelFont, _textPaint);

            // Anzahl rechts
            _textPaint.Color = UIRenderer.Accent;
            canvas.DrawText($"x{count}", _itemRects[i].Right - 10f,
                _itemRects[i].MidY + itemH * 0.12f, SKTextAlign.Right, _labelFont, _textPaint);

            // Effekt-Info in der Mitte
            var effectText = GetItemEffectText(item);
            if (!string.IsNullOrEmpty(effectText))
            {
                _labelFont.Size = itemH * 0.35f;
                _textPaint.Color = UIRenderer.Success.WithAlpha(180);
                canvas.DrawText(effectText, _itemRects[i].MidX,
                    _itemRects[i].MidY + itemH * 0.12f, SKTextAlign.Center, _labelFont, _textPaint);
            }
        }

        // Zurück-Button
        var backH = itemH * 0.9f;
        var backY = panelRect.Bottom - backH - 8f;
        _itemBackRect = new SKRect(panelRect.MidX - 60f, backY, panelRect.MidX + 60f, backY + backH);
        UIRenderer.DrawButton(canvas, _itemBackRect, _backText, false, false, UIRenderer.TextMuted);

        // Hinweis wenn keine Items
        if (_availableItems.Count == 0)
        {
            _labelFont.Size = bounds.Width * 0.03f;
            _textPaint.Color = UIRenderer.TextMuted;
            canvas.DrawText(_noItemsText, panelRect.MidX, panelRect.MidY,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
    }

    private static string GetItemEffectText(Item item)
    {
        if (item.HealPercent > 0) return $"+{item.HealPercent}% HP/MP";
        if (item.HealHp > 0 && item.HealMp > 0) return $"+{item.HealHp} HP +{item.HealMp} MP";
        if (item.HealHp > 0) return $"+{item.HealHp} HP";
        if (item.HealMp > 0) return $"+{item.HealMp} MP";
        return item.Effect ?? "";
    }

    // --- Floating Numbers ---

    private void SpawnFloatingNumber(float x, float y, string text, SKColor color, float fontSize)
    {
        for (int i = 0; i < _floatingNumbers.Length; i++)
        {
            if (!_floatingNumbers[i].IsActive)
            {
                _floatingNumbers[i] = new FloatingNumber
                {
                    X = x,
                    Y = y,
                    VelocityY = -60f,
                    Life = 1.2f,
                    Text = text,
                    Color = color,
                    FontSize = fontSize,
                    IsActive = true
                };
                return;
            }
        }
    }

    private void UpdateFloatingNumbers(float deltaTime)
    {
        for (int i = 0; i < _floatingNumbers.Length; i++)
        {
            if (!_floatingNumbers[i].IsActive) continue;

            _floatingNumbers[i].Y += _floatingNumbers[i].VelocityY * deltaTime;
            _floatingNumbers[i].VelocityY *= MathF.Pow(0.3f, deltaTime); // Abbremsen
            _floatingNumbers[i].Life -= deltaTime;

            if (_floatingNumbers[i].Life <= 0)
                _floatingNumbers[i].IsActive = false;
        }
    }

    private void RenderFloatingNumbers(SKCanvas canvas, SKRect bounds)
    {
        for (int i = 0; i < _floatingNumbers.Length; i++)
        {
            ref var fn = ref _floatingNumbers[i];
            if (!fn.IsActive) continue;

            var alpha = (byte)(Math.Min(1f, fn.Life * 2f) * 255);
            _dmgFont.Size = fn.FontSize;
            _textPaint.Color = fn.Color.WithAlpha(alpha);

            // Schatten
            var shadowPaint = _textPaint.Color;
            _textPaint.Color = SKColors.Black.WithAlpha((byte)(alpha * 0.6f));
            canvas.DrawText(fn.Text, fn.X + 2, fn.Y + 2, SKTextAlign.Center, _dmgFont, _textPaint);

            // Text
            _textPaint.Color = fn.Color.WithAlpha(alpha);
            canvas.DrawText(fn.Text, fn.X, fn.Y, SKTextAlign.Center, _dmgFont, _textPaint);
        }
    }

    // --- Kampf-Logik ---

    // --- Tutorial-System ---

    /// <summary>Zeigt einen Tutorial-Hint via TutorialOverlay.</summary>
    private void ShowTutorialHint(string messageKey, SKRect? highlightRect = null)
    {
        _tutorialOverlayActive = true;
        var message = _localization.GetString(messageKey) ?? messageKey;
        // Eindeutige hintId pro Schritt (verhindert MarkSeen-Konflikte)
        var hintId = $"battle_tutorial_{_tutorialStep}";
        var overlay = SceneManager.ShowOverlay<TutorialOverlay>();
        overlay.SetHint(hintId, _tutorialAriaTitle, message, highlightRect ?? default);
    }

    /// <summary>Prüft ob eine Aktion im Tutorial erlaubt ist.</summary>
    private bool IsTutorialActionEnabled(int actionIndex)
    {
        if (!_isTutorialBattle || _tutorialStep >= 5) return true;
        return _tutorialStep switch
        {
            1 => actionIndex == 0,  // Nur Angriff
            2 => actionIndex == 2,  // Nur Skill
            3 => actionIndex == 3,  // Nur Item
            4 => actionIndex == 1,  // Nur Ausweichen
            _ => true               // Intro/Free: alles
        };
    }

    /// <summary>Schrittzähler weiterschalten nach erfolgreicher Aktion.</summary>
    private void AdvanceTutorialStep()
    {
        if (!_isTutorialBattle) return;
        _tutorialStep++;
    }

    private void TransitionTo(BattlePhase phase)
    {
        _phase = phase;
        _phaseTimer = 0;

        if (phase == BattlePhase.PlayerTurn)
        {
            _actionsEnabled = true;

            // Tutorial-Hint zeigen wenn Tutorial aktiv und kein Overlay offen
            if (_isTutorialBattle && !_tutorialOverlayActive)
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
        }
        else
            _actionsEnabled = false;
    }

    /// <summary>Führt den gewählten Skill-Angriff aus (mit MP-Kosten und Multiplikator).</summary>
    private void ExecuteSkillAttack()
    {
        if (_selectedSkill == null)
        {
            TransitionTo(BattlePhase.PlayerTurn);
            return;
        }

        // Angriffs-Animation starten
        _attackAnimTimer = AttackAnimDuration;
        _showSlashEffect = false;

        var skill = _selectedSkill.Definition;

        // Ultimate-Skill: SplashArt-Effekt starten
        if (skill.IsUltimate)
        {
            var accentColor = skill.Element.HasValue
                ? GetElementColor(skill.Element.Value)
                : new SKColor(0x9B, 0x59, 0xB6);
            var ultName = _localization.GetString(skill.NameKey) ?? skill.NameKey;
            _splashArt.Start(ultName, _player.Class.ToString(), accentColor, 1.5f);
        }

        // MP abziehen
        _player.Mp = Math.Max(0, _player.Mp - skill.MpCost);

        // Skill-Nutzung registrieren (für Evolution)
        var evolved = _skillService.UseSkill(skill.Id);

        if (skill.Multiplier > 0)
        {
            // Schadens-Skill
            var atkStat = skill.Element == Element.Light || skill.Element == Element.Dark
                || _player.Class == ClassName.Arcanist
                ? Math.Max(_player.Atk, _player.Int) // Magische Skills nutzen höheren Wert
                : _player.Atk;

            var (damage, isCrit) = _battleEngine.CalculateDamage(
                atkStat, skill.Multiplier, _enemy.Def, skill.Element, _cachedEnemyElement, _player.Luk);

            _enemyHp = Math.Max(0, _enemyHp - damage);
            _enemyFlashTimer = 0.4f;
            _shakeIntensity = isCrit ? 15f : 8f; // Stärker als normaler Angriff

            // Skill-farbige Floating Number
            var elemColor = skill.Element.HasValue ? GetElementColor(skill.Element.Value) : new SKColor(0x9B, 0x59, 0xB6);
            var dmgText = isCrit ? $"{damage}!" : damage.ToString();
            SpawnFloatingNumber(_lastBounds.MidX, _lastBounds.Height * 0.28f, dmgText, elemColor, isCrit ? 36f : 28f);

            // Magische Partikel am Gegner
            var enemyCenter = new SKPoint(_lastBounds.MidX, _lastBounds.Height * 0.35f);
            _particles.Emit(enemyCenter.X, enemyCenter.Y, isCrit ? 20 : 10, ParticleSystem.MagicSparkle);

            // Combo
            _comboCount++;
            _cachedComboText = _comboCount.ToString();

            // Gegner besiegt?
            if (_enemyHp <= 0)
            {
                if (_enemy.Phases > 1 && _currentBossPhase < _enemy.Phases)
                {
                    _currentBossPhase++;
                    UpdateEnemySpriteKey();
                    _enemyHp = _enemyMaxHp;
                    _shakeIntensity = 15f;
                    _particles.Emit(enemyCenter.X, enemyCenter.Y, 20, ParticleSystem.SystemGlitch);
                    TransitionTo(BattlePhase.BossPhaseChange);
                }
                else
                {
                    OnVictory();
                }
                return;
            }
        }
        else
        {
            // Buff/Heal-Skill (Multiplikator 0)
            ApplySkillEffect(skill.Effect);
        }

        // Evolution-Hinweis
        if (evolved != null)
        {
            SpawnFloatingNumber(_lastBounds.MidX, _lastBounds.Height * 0.5f, _skillEvolutionText, UIRenderer.Accent, 24f);
            _particles.Emit(_lastBounds.MidX, _lastBounds.Height * 0.5f, 15, ParticleSystem.LevelUpGlow);
        }

        _selectedSkill = null;
        AdvanceTutorialStep();
        TransitionTo(BattlePhase.EnemyTurn);
    }

    /// <summary>Wendet nicht-schadensbasierte Skill-Effekte an.</summary>
    private void ApplySkillEffect(string? effect)
    {
        if (string.IsNullOrEmpty(effect)) return;

        if (effect.StartsWith("heal_"))
        {
            // z.B. "heal_10pct" → 10% MaxHP heilen
            var pctStr = effect.Replace("heal_", "").Replace("pct", "");
            if (int.TryParse(pctStr, out var pct))
            {
                var healAmount = _player.MaxHp * pct / 100;
                _player.Hp = Math.Min(_player.MaxHp, _player.Hp + healAmount);
                SpawnFloatingNumber(_lastBounds.MidX * 0.5f, _lastBounds.Height * 0.65f,
                    $"+{healAmount}", UIRenderer.Success, 22f);
                _particles.Emit(_lastBounds.MidX * 0.5f, _lastBounds.Height * 0.65f, 8, ParticleSystem.MagicSparkle);
            }
        }
        else if (effect.StartsWith("atk_buff_"))
        {
            // z.B. "atk_buff_30" → ATK um 30% für diesen Kampf erhöhen
            // Bonus wird in _tempAtkBonus getrackt und am Kampfende zurückgesetzt
            var pctStr = effect.Replace("atk_buff_", "");
            if (int.TryParse(pctStr, out var pct))
            {
                var bonus = _player.Atk * pct / 100;
                _player.Atk += bonus;
                _tempAtkBonus += bonus;
                SpawnFloatingNumber(_lastBounds.MidX * 0.5f, _lastBounds.Height * 0.65f,
                    $"ATK+{bonus}", new SKColor(0xFF, 0x45, 0x00), 20f);
            }
        }
    }

    private void ExecutePlayerAttack()
    {
        // Angriffs-Animation starten (Translate + Slash)
        _attackAnimTimer = AttackAnimDuration;
        _showSlashEffect = false;

        var atkElem = _player.Class switch
        {
            ClassName.Swordmaster => (Element?)null,
            ClassName.Arcanist => Element.Fire,
            ClassName.Shadowblade => Element.Dark,
            _ => null
        };

        // Für Magic-Klassen (Arkanist): Höheren Wert aus ATK/INT verwenden
        var normalAtkStat = _player.Class == ClassName.Arcanist
            ? Math.Max(_player.Atk, _player.Int)
            : _player.Atk;

        var (damage, isCrit) = _battleEngine.CalculateDamage(
            normalAtkStat, 1f, _enemy.Def, atkElem, _cachedEnemyElement, _player.Luk);

        _enemyHp = Math.Max(0, _enemyHp - damage);
        _enemyFlashTimer = 0.3f;
        _shakeIntensity = isCrit ? 12f : 6f;

        // Floating Number (relative Positionen)
        var dmgText = isCrit ? $"{damage}!" : damage.ToString();
        var dmgColor = isCrit ? UIRenderer.Accent : UIRenderer.TextPrimary;
        var dmgSize = isCrit ? 32f : 24f;
        SpawnFloatingNumber(_lastBounds.MidX, _lastBounds.Height * 0.3f, dmgText, dmgColor, dmgSize);

        // Partikel am Gegner (relative Position)
        var enemyCenter = new SKPoint(_lastBounds.MidX, _lastBounds.Height * 0.35f);
        _particles.Emit(enemyCenter.X, enemyCenter.Y, isCrit ? 15 : 6, ParticleSystem.BloodSplatter);

        // Combo erhöhen + Cache aktualisieren
        _comboCount++;
        _cachedComboText = _comboCount.ToString();

        // Prüfen: Gegner besiegt?
        if (_enemyHp <= 0)
        {
            // Boss-Phasen-Wechsel?
            if (_enemy.Phases > 1 && _currentBossPhase < _enemy.Phases)
            {
                _currentBossPhase++;
                UpdateEnemySpriteKey();
                _enemyHp = _enemyMaxHp; // Volle HP für nächste Phase
                _shakeIntensity = 15f;
                _particles.Emit(enemyCenter.X, enemyCenter.Y, 20, ParticleSystem.SystemGlitch);
                TransitionTo(BattlePhase.BossPhaseChange);
            }
            else
            {
                OnVictory();
            }
        }
        else
        {
            TransitionTo(BattlePhase.EnemyTurn);
        }
    }

    private void ExecuteEnemyAttack()
    {
        var (damage, isCrit) = _battleEngine.CalculateDamage(
            _enemy.Atk, 1f, _player.Def, _cachedEnemyElement, null, 5);

        // Tutorial-Override: Schaden kontrollieren
        if (_isTutorialBattle)
        {
            if (_tutorialStep == 3)
            {
                // Schritt 3 (Item-Tutorial): Spieler auf ~30% HP bringen
                var targetHp = (int)(_player.MaxHp * 0.3f);
                if (_player.Hp > targetHp)
                    damage = _player.Hp - targetHp;
            }
            else if (_tutorialStep < 3)
            {
                // Vor Item-Tutorial: Schaden niedrig halten (max 10% HP)
                damage = Math.Min(damage, Math.Max(1, (int)(_player.MaxHp * 0.1f)));
            }
        }

        _player.Hp = Math.Max(0, _player.Hp - damage);
        _playerFlashTimer = 0.4f;
        _shakeIntensity = isCrit ? 10f : 5f;
        _comboCount = 0; // Combo zurücksetzen bei Treffer
        _cachedComboText = "";

        // Floating Number über Spieler (relative Position)
        var playerY = _lastBounds.Height * 0.72f;
        var dmgText = isCrit ? $"{damage}!" : damage.ToString();
        SpawnFloatingNumber(_lastBounds.MidX * 0.5f, playerY, dmgText, UIRenderer.Danger, isCrit ? 28f : 22f);
        _particles.Emit(_lastBounds.MidX * 0.5f, playerY + 20, 5, ParticleSystem.BloodSplatter);

        // Spieler besiegt?
        if (_player.Hp <= 0)
        {
            TransitionTo(BattlePhase.Defeat);
            return;
        }

        TransitionTo(BattlePhase.PlayerTurn);
    }

    /// <summary>Setzt temporäre Kampf-Buffs zurück (ATK-Buff etc.).</summary>
    private void ResetTempBuffs()
    {
        if (_tempAtkBonus > 0)
        {
            _player.Atk -= _tempAtkBonus;
            _tempAtkBonus = 0;
        }
    }

    private void OnVictory()
    {
        // Temporäre Buffs zurücksetzen bevor EXP/Gold verteilt wird
        ResetTempBuffs();

        // Tutorial als abgeschlossen markieren
        if (_isTutorialBattle)
        {
            _tutorialService?.MarkSeen("FirstBattle");
        }

        _earnedExp = _enemy.Exp;
        _earnedGold = _enemy.Gold;
        _earnedDrops.Clear();
        _earnedDropNames.Clear();
        _earnedDrops.AddRange(_battleEngine.CalculateDrops(_enemy));

        // EXP und Gold tatsächlich an den Spieler vergeben
        _progressionService.AwardExp(_player, _earnedExp);
        _goldService.AddGold(_player, _earnedGold);

        // Drops dem Inventar hinzufügen
        foreach (var dropId in _earnedDrops)
            _inventoryService.AddItem(dropId);

        // Drop-IDs → lokalisierte Display-Namen auflösen
        foreach (var dropId in _earnedDrops)
        {
            var item = _inventoryService.GetItemDefinition(dropId);
            var name = item != null
                ? (_localization.GetString(item.NameKey) ?? item.NameKey)
                : dropId;
            _earnedDropNames.Add(name);
        }

        // Partikel-Explosion (Gold + Magie, relative Position)
        var cx = _lastBounds.MidX;
        var cy = _lastBounds.Height * 0.35f;
        _particles.Emit(cx, cy, 25, ParticleSystem.LevelUpGlow);
        _particles.Emit(cx, cy, 15, ParticleSystem.MagicSparkle);

        TransitionTo(BattlePhase.Victory);
    }

    /// <summary>Sieg: Story-Effekte anwenden, zum nächsten Knoten navigieren.</summary>
    private void HandlePostVictory()
    {
        // Kampf abgeschlossen — keine weitere Interaktion
        _phase = BattlePhase.Done;

        // Story zum nächsten Knoten vorspulen
        _storyEngine.AdvanceToNext();

        if (SceneManager.HasSceneBelow)
        {
            // BattleScene wurde per PushScene geöffnet (z.B. von OverworldScene)
            // → zurück poppen, darunterliegende Szene läuft weiter
            SceneManager.PopScene(new FadeTransition());
        }
        else
        {
            // BattleScene hat DialogueScene ersetzt → neue DialogueScene erstellen
            SceneManager.ChangeScene<DialogueScene>(new FadeTransition());
        }
    }

    /// <summary>Niederlage: GameOverOverlay anzeigen mit Revive/Laden.</summary>
    private void HandlePostDefeat()
    {
        ResetTempBuffs();
        _phase = BattlePhase.Done;

        // Guard: Wenn bereits ein GameOverOverlay aktiv ist, keine erneute Subscription
        if (_gameOverOverlay != null) return;

        _gameOverOverlay = SceneManager.ShowOverlay<GameOverOverlay>();
        _gameOverOverlay.ReviveRequested += OnReviveRequested;
        _gameOverOverlay.LoadSaveRequested += OnLoadSaveRequested;
    }

    /// <summary>Revive-Callback: Spieler wiederbeleben und Kampf fortsetzen.</summary>
    private void OnReviveRequested()
    {
        _player.Hp = Math.Max(1, _player.MaxHp / 2);
        _player.Mp = _player.MaxMp / 2;

        if (_gameOverOverlay != null)
        {
            SceneManager.HideOverlay(_gameOverOverlay);
            // Events abmelden um Leaks zu vermeiden
            _gameOverOverlay.ReviveRequested -= OnReviveRequested;
            _gameOverOverlay.LoadSaveRequested -= OnLoadSaveRequested;
            _gameOverOverlay = null;
        }

        _phase = BattlePhase.PlayerTurn;
        _actionsEnabled = true;
        _phaseTimer = 0;
    }

    /// <summary>Laden-Callback: Overlay schliessen und SaveSlotScene oeffnen.</summary>
    private void OnLoadSaveRequested()
    {
        if (_gameOverOverlay != null)
        {
            SceneManager.HideOverlay(_gameOverOverlay);
            // Events abmelden um Leaks zu vermeiden
            _gameOverOverlay.ReviveRequested -= OnReviveRequested;
            _gameOverOverlay.LoadSaveRequested -= OnLoadSaveRequested;
            _gameOverOverlay = null;
        }

        SceneManager.ChangeScene<SaveSlotScene>(new FadeTransition());
    }

    // --- Input ---

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action != InputAction.Tap) return;

        switch (_phase)
        {
            case BattlePhase.PlayerTurn when _actionsEnabled:
                for (int i = 0; i < 4; i++)
                {
                    if (UIRenderer.HitTest(_actionRects[i], position))
                    {
                        HandleAction(i);
                        return;
                    }
                }
                break;

            case BattlePhase.SkillSelect:
                HandleSkillSelectInput(position);
                break;

            case BattlePhase.ItemSelect:
                HandleItemSelectInput(position);
                break;

            case BattlePhase.Victory when _phaseTimer > 1.5f:
                BattleWon?.Invoke(_earnedExp, _earnedGold, _earnedDrops);
                HandlePostVictory();
                break;

            case BattlePhase.Defeat when _phaseTimer > 1.5f:
                BattleLost?.Invoke();
                HandlePostDefeat();
                break;
        }
    }

    private void HandleSkillSelectInput(SKPoint position)
    {
        // Zurück-Button
        if (UIRenderer.HitTest(_skillBackRect, position))
        {
            TransitionTo(BattlePhase.PlayerTurn);
            return;
        }

        // Skill-Auswahl
        for (int i = 0; i < Math.Min(_availableSkills.Count, 6); i++)
        {
            if (UIRenderer.HitTest(_skillRects[i], position))
            {
                var skill = _availableSkills[i];
                if (_player.Mp >= skill.Definition.MpCost)
                {
                    _selectedSkill = skill;
                    TransitionTo(BattlePhase.PlayerSkillAttack);
                }
                else
                {
                    // Nicht genug MP - Floating-Hinweis
                    SpawnFloatingNumber(_lastBounds.MidX, _lastBounds.Height * 0.6f,
                        _notEnoughMpText, UIRenderer.Danger, 18f);
                }
                return;
            }
        }
    }

    private void HandleItemSelectInput(SKPoint position)
    {
        // Zurück-Button
        if (UIRenderer.HitTest(_itemBackRect, position))
        {
            TransitionTo(BattlePhase.PlayerTurn);
            return;
        }

        // Item-Auswahl
        for (int i = 0; i < Math.Min(_availableItems.Count, 6); i++)
        {
            if (UIRenderer.HitTest(_itemRects[i], position))
            {
                var (item, _) = _availableItems[i];
                if (_inventoryService.UseItem(item.Id, _player))
                {
                    // Heil-Effekt anzeigen
                    var healY = _lastBounds.Height * 0.65f;
                    if (item.HealHp > 0 || item.HealPercent > 0)
                    {
                        var healText = item.HealPercent > 0 ? $"+{item.HealPercent}%" : $"+{item.HealHp}";
                        SpawnFloatingNumber(_lastBounds.MidX * 0.5f, healY, healText, UIRenderer.Success, 22f);
                    }
                    if (item.HealMp > 0 || item.HealPercent > 0)
                    {
                        SpawnFloatingNumber(_lastBounds.MidX * 0.5f, healY - 30f,
                            "MP+", new SKColor(0x58, 0xA6, 0xFF), 18f);
                    }
                    _particles.Emit(_lastBounds.MidX * 0.5f, healY, 8, ParticleSystem.MagicSparkle);

                    // Item-Liste aktualisieren und Zug beenden
                    _availableItems = _inventoryService.GetItemsByType(ItemType.Consumable);
                    AdvanceTutorialStep();
                    TransitionTo(BattlePhase.EnemyTurn);
                }
                return;
            }
        }
    }

    private void HandleAction(int actionIndex)
    {
        // Tutorial: Blockierte Aktionen ignorieren
        if (_isTutorialBattle && !IsTutorialActionEnabled(actionIndex))
            return;

        _actionsEnabled = false;

        switch (actionIndex)
        {
            case 0: // Angriff
                AdvanceTutorialStep();
                TransitionTo(BattlePhase.PlayerAttack);
                break;

            case 1: // Ausweichen
                // Tutorial: Dodge ist im Tutorial immer erfolgreich
                if (_isTutorialBattle && _tutorialStep == 4)
                    _dodgeSuccessful = true;
                else
                    _dodgeSuccessful = _battleEngine.TryDodge(_player.Spd, _enemy.Spd);

                if (_dodgeSuccessful)
                {
                    var dodgeY = _lastBounds.Height * 0.65f;
                    SpawnFloatingNumber(_lastBounds.MidX * 0.5f, dodgeY, _dodgedText, UIRenderer.Success, 20f);
                    _comboCount++;
                    _cachedComboText = _comboCount.ToString();
                }
                _dodgeTimer = DodgeDuration;
                AdvanceTutorialStep();
                TransitionTo(BattlePhase.PlayerDodge);
                break;

            case 2: // Skill-Auswahl öffnen
                _availableSkills = _skillService.GetUnlockedSkills();
                TransitionTo(BattlePhase.SkillSelect);
                // Tutorial: Step wird nach Skill-Ausführung weitergeschaltet (in ExecuteSkillAttack)
                break;

            case 3: // Item-Auswahl öffnen
                _availableItems = _inventoryService.GetItemsByType(ItemType.Consumable);
                TransitionTo(BattlePhase.ItemSelect);
                // Tutorial: Step wird nach Item-Nutzung weitergeschaltet (in HandleItemSelectInput)
                break;
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _hoveredAction = -1;
        if (!_actionsEnabled) return;

        for (int i = 0; i < 4; i++)
        {
            if (UIRenderer.HitTest(_actionRects[i], position))
            {
                _hoveredAction = i;
                return;
            }
        }
    }

    // --- Helfer ---

    private static SKColor GetElementColor(Element element) => element switch
    {
        Element.Fire => new SKColor(0xFF, 0x45, 0x00),
        Element.Ice => new SKColor(0x00, 0xBF, 0xFF),
        Element.Lightning => new SKColor(0xFF, 0xD7, 0x00),
        Element.Wind => new SKColor(0x7C, 0xFC, 0x00),
        Element.Light => new SKColor(0xFF, 0xFF, 0xE0),
        Element.Dark => new SKColor(0x8B, 0x00, 0x8B),
        _ => UIRenderer.TextPrimary
    };

    public void Dispose()
    {
        // GameOverOverlay-Events abmelden (verhindert Leak bei Dispose waehrend aktivem Overlay)
        if (_gameOverOverlay != null)
        {
            _gameOverOverlay.ReviveRequested -= OnReviveRequested;
            _gameOverOverlay.LoadSaveRequested -= OnLoadSaveRequested;
            _gameOverOverlay = null;
        }
        _particles.Dispose();
    }

    /// <summary>
    /// Statische Ressourcen werden NICHT disposed - sie leben fuer die gesamte App-Lifetime.
    /// Dispose von static readonly SKPaint/SKFont crasht beim zweiten Kampf (ObjectDisposedException).
    /// GC räumt beim App-Ende auf.
    /// </summary>
    public static void Cleanup()
    {
        // Absichtlich leer: statische Paints/Fonts/MaskFilter duerfen nicht
        // disposed werden, da sie ueber mehrere Kampf-Instanzen geteilt werden.
    }
}
