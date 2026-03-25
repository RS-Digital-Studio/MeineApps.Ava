namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Engine.Transitions;
using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Rendering.Characters;
using RebornSaga.Rendering.Effects;
using RebornSaga.Rendering.Backgrounds;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Klassenwahl-Szene: 3 Klassen mit Portraits, Stats-Vergleich und Bestätigung.
/// Im Prolog vorgewählt (Schwertmeister Lv50), in Kapitel 1 freie Wahl.
/// </summary>
public class ClassSelectScene : Scene, IDisposable
{
    private readonly StoryEngine _storyEngine;
    private readonly ILocalizationService _localization;
    private float _time;
    private int _selectedClass; // 0=Schwert, 1=Magier, 2=Assassine
    private bool _confirmed;

    // Prolog-Modus: Klasse ist vorgewählt, nur Bestätigen
    private bool _isPrologMode;

    // Lokalisierte Strings (gecacht im Konstruktor)
    private readonly string[] _classNames = new string[3];
    private readonly string[][] _classDescLines = new string[3][];
    private readonly string _titleChoose;
    private readonly string _titleYourClass;
    private readonly string _confirmText;

    /// <summary>Slot-Nummer in dem das neue Spiel gespeichert wird.</summary>
    public int SelectedSlot { get; set; }

    // UI-Rects
    private readonly SKRect[] _classCardRects = new SKRect[3];
    private SKRect _confirmButtonRect;
    private int _hoveredCard = -1;

    // Partikel für die gewählte Klasse
    private readonly ParticleSystem _particles = new();

    // Gepoolte Paints
    private static readonly SKPaint _cardBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _cardBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _statBarBgPaint = new() { IsAntialias = true, Color = new SKColor(0x20, 0x20, 0x30, 200) };
    private static readonly SKPaint _statBarFillPaint = new() { IsAntialias = true };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _labelFont = new() { LinearMetrics = true };
    private static readonly SKFont _statFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    // Klassen-Farben
    private static readonly SKColor[] _classColors =
    {
        new(0xC0, 0x39, 0x2B), // Rot (Schwertmeister)
        new(0x9B, 0x59, 0xB6), // Lila (Arkanist)
        new(0x2E, 0xCC, 0x71)  // Grün (Schattenklinke)
    };

    /// <summary>Event wenn die Klasse bestätigt wird (Index 0-2).</summary>
    public event Action<int>? ClassSelected;

    public ClassSelectScene(StoryEngine storyEngine, ILocalizationService localization)
    {
        _storyEngine = storyEngine;
        _localization = localization;

        // Klassen-Namen cachen
        _classNames[0] = _localization.GetString("ClassSwordmaster") ?? "Swordmaster";
        _classNames[1] = _localization.GetString("ClassArcanist") ?? "Arcanist";
        _classNames[2] = _localization.GetString("ClassShadowblade") ?? "Shadowblade";

        // Klassen-Beschreibungen cachen (aufgeteilt in Zeilen via ". ")
        var descKeys = new[] { "ClassDescSwordmaster", "ClassDescArcanist", "ClassDescShadowblade" };
        var descFallbacks = new[]
        {
            "High ATK & DEF. Classic melee fighter.",
            "Powerful magic & high MP. Ranged destroyer.",
            "Fast & deadly. Critical hits. Dodge master."
        };
        for (int i = 0; i < 3; i++)
        {
            var text = _localization.GetString(descKeys[i]) ?? descFallbacks[i];
            var parts = text.Split(". ", StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < parts.Length - 1; j++)
                if (!parts[j].EndsWith('.')) parts[j] += ".";
            _classDescLines[i] = parts;
        }

        _titleChoose = _localization.GetString("ChooseYourClass") ?? "Choose your class";
        _titleYourClass = _localization.GetString("YourClass") ?? "Your Class";
        _confirmText = _localization.GetString("Confirm") ?? "Confirm";
    }

    public override void OnEnter()
    {
        _time = 0;
        _confirmed = false;
    }

    /// <summary>Aktiviert den Prolog-Modus (Schwertmeister vorgewählt, nur Bestätigen).</summary>
    public void SetPrologMode()
    {
        _isPrologMode = true;
        _selectedClass = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
        _particles.Update(deltaTime);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Hintergrund: System-Void für futuristisches Gefühl
        BackgroundCompositor.SetScene("systemVoid");
        BackgroundCompositor.RenderBack(canvas, bounds, _time);

        // Titel
        var titleY = bounds.Height * 0.06f;
        var title = _isPrologMode ? _titleYourClass : _titleChoose;
        UIRenderer.DrawTextWithShadow(canvas, title, bounds.MidX, titleY,
            bounds.Width * 0.05f, UIRenderer.PrimaryGlow);

        // 3 Klassen-Karten nebeneinander (breitere Karten, engerer Abstand)
        var cardW = bounds.Width * 0.30f;
        var cardH = bounds.Height * 0.72f;
        var cardSpacing = bounds.Width * 0.02f;
        var totalW = 3 * cardW + 2 * cardSpacing;
        var startX = (bounds.Width - totalW) / 2;
        var cardY = bounds.Height * 0.11f;

        for (int i = 0; i < 3; i++)
        {
            var x = startX + i * (cardW + cardSpacing);
            _classCardRects[i] = new SKRect(x, cardY, x + cardW, cardY + cardH);

            var isSelected = i == _selectedClass;
            var isHovered = i == _hoveredCard;
            var isDisabled = _isPrologMode && i != 0;

            DrawClassCard(canvas, _classCardRects[i], i, isSelected, isHovered, isDisabled);
        }

        // Partikel um die gewählte Karte
        _particles.Render(canvas);

        // Szenen-Partikel (ScanLines etc.)
        BackgroundCompositor.RenderFront(canvas, bounds, _time);

        // Bestätigen-Button
        var btnW = bounds.Width * 0.3f;
        var btnH = bounds.Height * 0.055f;
        _confirmButtonRect = new SKRect(
            bounds.MidX - btnW / 2, bounds.Height * 0.9f,
            bounds.MidX + btnW / 2, bounds.Height * 0.9f + btnH);

        var btnColor = _classColors[_selectedClass];
        UIRenderer.DrawButton(canvas, _confirmButtonRect, _confirmText, false, false, btnColor);
    }

    private void DrawClassCard(SKCanvas canvas, SKRect rect, int classIndex,
        bool isSelected, bool isHovered, bool isDisabled)
    {
        var cls = PlayerClass.Get(classIndex);
        var def = CharacterDefinitions.GetProtagonist(classIndex);
        var color = _classColors[classIndex];

        // Karten-Hintergrund
        _cardBgPaint.Color = isDisabled
            ? new SKColor(0x15, 0x18, 0x20, 150)
            : isSelected
                ? new SKColor(0x1A, 0x1F, 0x2E, 240)
                : isHovered
                    ? new SKColor(0x18, 0x1C, 0x28, 220)
                    : new SKColor(0x12, 0x16, 0x1F, 200);

        using var roundRect = new SKRoundRect(rect, 12f);
        canvas.DrawRoundRect(roundRect, _cardBgPaint);

        // Rand (pulsierend wenn ausgewählt)
        var borderAlpha = isSelected
            ? (byte)(160 + MathF.Sin(_time * 3f) * 60)
            : isHovered ? (byte)100 : (byte)50;
        _cardBorderPaint.Color = isDisabled ? UIRenderer.TextMuted.WithAlpha(30) : color.WithAlpha(borderAlpha);
        _cardBorderPaint.StrokeWidth = isSelected ? 2.5f : 1.5f;
        canvas.DrawRoundRect(roundRect, _cardBorderPaint);

        // Charakter-Portrait (obere 36%, geclippt auf Karte)
        var portraitBounds = new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + rect.Height * 0.36f);
        canvas.Save();
        canvas.ClipRoundRect(roundRect);
        CharacterRenderer.DrawFullBody(canvas, portraitBounds, def, Pose.Standing, Emotion.Determined, _time);
        canvas.Restore();

        // Klassen-Name (unter dem Portrait)
        var nameY = rect.Top + rect.Height * 0.40f;
        _titleFont.Size = rect.Width * 0.12f;
        _textPaint.Color = isDisabled ? UIRenderer.TextMuted : color;
        canvas.DrawText(_classNames[classIndex], rect.MidX, nameY,
            SKTextAlign.Center, _titleFont, _textPaint);

        // Beschreibung (3 Zeilen, größer, heller, mit Clipping)
        canvas.Save();
        canvas.ClipRect(new SKRect(rect.Left + 4, rect.Top, rect.Right - 4, rect.Bottom));

        var descY = nameY + rect.Height * 0.05f;
        _labelFont.Size = rect.Width * 0.08f;
        _textPaint.Color = isDisabled ? UIRenderer.TextMuted.WithAlpha(100) : UIRenderer.TextPrimary.WithAlpha(200);
        var descLines = _classDescLines[classIndex];
        foreach (var line in descLines)
        {
            canvas.DrawText(line, rect.MidX, descY, SKTextAlign.Center, _labelFont, _textPaint);
            descY += _labelFont.Size * 1.25f;
        }

        canvas.Restore();

        // Stats-Balken (mit Clipping damit nichts überläuft)
        canvas.Save();
        canvas.ClipRect(rect);

        var statsY = rect.Top + rect.Height * 0.60f;
        var statsMargin = rect.Width * 0.06f;
        var barWidth = rect.Width - 2 * statsMargin;
        var barHeight = rect.Height * 0.022f;
        var barSpacing = barHeight * 2.4f;

        DrawStatBar(canvas, rect.Left + statsMargin, statsY, barWidth, barHeight,
            "HP", cls.BaseHp, 120, color, isDisabled);
        DrawStatBar(canvas, rect.Left + statsMargin, statsY + barSpacing, barWidth, barHeight,
            "MP", cls.BaseMp, 80, color, isDisabled);
        DrawStatBar(canvas, rect.Left + statsMargin, statsY + barSpacing * 2, barWidth, barHeight,
            "ATK", cls.BaseAtk, 18, color, isDisabled);
        DrawStatBar(canvas, rect.Left + statsMargin, statsY + barSpacing * 3, barWidth, barHeight,
            "DEF", cls.BaseDef, 12, color, isDisabled);
        DrawStatBar(canvas, rect.Left + statsMargin, statsY + barSpacing * 4, barWidth, barHeight,
            "INT", cls.BaseInt, 18, color, isDisabled);
        DrawStatBar(canvas, rect.Left + statsMargin, statsY + barSpacing * 5, barWidth, barHeight,
            "SPD", cls.BaseSpd, 16, color, isDisabled);
        DrawStatBar(canvas, rect.Left + statsMargin, statsY + barSpacing * 6, barWidth, barHeight,
            "LUK", cls.BaseLuk, 14, color, isDisabled);

        canvas.Restore();
    }

    private void DrawStatBar(SKCanvas canvas, float x, float y, float width, float height,
        string label, int value, int maxValue, SKColor color, bool isDisabled)
    {
        // Label (links, feste Breite 22%)
        _statFont.Size = height * 1.3f;
        _textPaint.Color = isDisabled ? UIRenderer.TextMuted.WithAlpha(80) : UIRenderer.TextSecondary;
        canvas.DrawText(label, x, y + height * 0.9f, SKTextAlign.Left, _statFont, _textPaint);

        // Bar (Mitte, 55%)
        var barX = x + width * 0.22f;
        var barW = width * 0.53f;
        canvas.DrawRect(barX, y, barW, height, _statBarBgPaint);

        // Füllung
        var fillRatio = Math.Min(1f, (float)value / maxValue);
        _statBarFillPaint.Color = isDisabled ? UIRenderer.TextMuted.WithAlpha(60) : color.WithAlpha(200);
        canvas.DrawRect(barX, y, barW * fillRatio, height, _statBarFillPaint);

        // Wert (rechts-ausgerichtet am rechten Rand, verhindert Überlauf)
        _textPaint.Color = isDisabled ? UIRenderer.TextMuted.WithAlpha(80) : UIRenderer.TextPrimary;
        canvas.DrawText(value.ToString(), x + width, y + height * 0.9f,
            SKTextAlign.Right, _statFont, _textPaint);
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (_confirmed) return;

        switch (action)
        {
            case InputAction.Tap:
                // Klassen-Karten prüfen
                for (int i = 0; i < 3; i++)
                {
                    if (UIRenderer.HitTest(_classCardRects[i], position))
                    {
                        if (_isPrologMode && i != 0) return; // Im Prolog nur Schwertmeister
                        _selectedClass = i;

                        // Partikel um die gewählte Karte
                        var rect = _classCardRects[i];
                        _particles.Emit(rect.MidX, rect.MidY, 8, ParticleSystem.MagicSparkle);
                        return;
                    }
                }

                // Bestätigen-Button
                if (UIRenderer.HitTest(_confirmButtonRect, position))
                {
                    _confirmed = true;
                    ClassSelected?.Invoke(_selectedClass);
                    StartNewGame();
                }
                break;

            case InputAction.Back:
                // Zurück zum Titelbildschirm
                SceneManager.PopScene();
                break;
        }
    }

    /// <summary>
    /// Erstellt den Spieler, lädt das erste Kapitel und startet die Dialog-Szene.
    /// </summary>
    private async void StartNewGame()
    {
        try
        {
            // Spieler mit gewählter Klasse erstellen
            var className = _selectedClass switch
            {
                0 => ClassName.Swordmaster,
                1 => ClassName.Arcanist,
                2 => ClassName.Shadowblade,
                _ => ClassName.Swordmaster
            };
            var player = Player.Create(className);

            // StoryEngine initialisieren
            _storyEngine.SetPlayer(player);
            await _storyEngine.LoadChapterAsync("p1");

            // Zur DialogueScene wechseln (wird den ersten Knoten automatisch präsentieren)
            SceneManager.ChangeScene<DialogueScene>(new FadeTransition());
        }
        catch (Exception ex)
        {
            // Kapitel-Daten nicht verfügbar - zurück zum Titel
            System.Diagnostics.Debug.WriteLine($"StartNewGame Fehler: {ex.Message}");
            _confirmed = false;
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _hoveredCard = -1;
        for (int i = 0; i < 3; i++)
        {
            if (UIRenderer.HitTest(_classCardRects[i], position))
            {
                _hoveredCard = i;
                return;
            }
        }
    }

    public void Dispose()
    {
        _particles.Dispose();
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _cardBgPaint.Dispose();
        _cardBorderPaint.Dispose();
        _statBarBgPaint.Dispose();
        _statBarFillPaint.Dispose();
        _titleFont.Dispose();
        _labelFont.Dispose();
        _statFont.Dispose();
        _textPaint.Dispose();
    }
}
