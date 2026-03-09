namespace RebornSaga.Scenes;

using RebornSaga.Engine;
using RebornSaga.Engine.Transitions;
using RebornSaga.Models;
using RebornSaga.Rendering.Backgrounds;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Modus der SaveSlotScene: Neues Spiel oder Fortsetzen.
/// </summary>
public enum SaveSlotMode
{
    /// <summary>Neues Spiel starten (nur leere Slots auswählbar).</summary>
    NewGame,
    /// <summary>Vorhandenen Spielstand laden (nur belegte Slots auswählbar).</summary>
    LoadGame
}

/// <summary>
/// 3 Save-Slot-Karten. Belegte Slots zeigen Klasse/Level/Kapitel/Spielzeit.
/// Leere Slots: "Tap zum Starten". Long-Press → Löschen-Bestätigung.
/// Je nach Modus (NewGame/LoadGame) unterschiedliches Verhalten.
/// </summary>
public class SaveSlotScene : Scene
{
    private readonly SaveGameService _saveGameService;
    private readonly StoryEngine _storyEngine;
    private readonly SkillService _skillService;
    private readonly InventoryService _inventoryService;
    private readonly AffinityService _affinityService;
    private readonly FateTrackingService _fateTrackingService;
    private readonly CodexService _codexService;

    private float _time;
    private readonly SKRect[] _slotRects = new SKRect[3];
    private readonly SaveSlotData[] _slots = new SaveSlotData[3];
    private int _hoveredSlot = -1;
    private int _pressedSlot = -1;
    private bool _isLoading; // Verhindert doppelte Aktionen während Async-Operationen

    /// <summary>Modus: Neues Spiel oder Fortsetzen. Wird vor OnEnter gesetzt.</summary>
    public SaveSlotMode Mode { get; set; } = SaveSlotMode.NewGame;

    // Löschen-Bestätigung
    private int _deleteConfirmSlot = -1;
    private SKRect _deleteYesRect;
    private SKRect _deleteNoRect;

    // Back-Button
    private SKRect _backButtonRect;

    // Statische Paints
    private static readonly SKPaint _overlayPaint = new() { Color = new SKColor(0, 0, 0, 180) };

    public SaveSlotScene(
        SaveGameService saveGameService,
        StoryEngine storyEngine,
        SkillService skillService,
        InventoryService inventoryService,
        AffinityService affinityService,
        FateTrackingService fateTrackingService,
        CodexService codexService)
    {
        _saveGameService = saveGameService;
        _storyEngine = storyEngine;
        _skillService = skillService;
        _inventoryService = inventoryService;
        _affinityService = affinityService;
        _fateTrackingService = fateTrackingService;
        _codexService = codexService;
    }

    public override void OnEnter()
    {
        _time = 0;
        _deleteConfirmSlot = -1;
        _isLoading = false;

        // Echte Slot-Daten aus SaveGameService laden
        // AppChecker:ignore
        _ = LoadSlotDataAsync();
    }

    /// <summary>
    /// Lädt die Slot-Metadaten aus der SQLite-Datenbank.
    /// </summary>
    private async System.Threading.Tasks.Task LoadSlotDataAsync()
    {
        try
        {
            // Slots 1-3 laden (UI-Index 0-2 → DB-SlotNumber 1-3)
            for (int i = 0; i < 3; i++)
            {
                var entity = await _saveGameService.GetSlotInfoAsync(i + 1);
                if (entity != null)
                {
                    // Klassen-Name lokalisieren
                    var className = entity.ClassName;
                    if (Enum.TryParse<Models.Enums.ClassName>(className, out var cls))
                    {
                        className = cls switch
                        {
                            Models.Enums.ClassName.Swordmaster => "Schwertmeister",
                            Models.Enums.ClassName.Arcanist => "Arkanist",
                            Models.Enums.ClassName.Shadowblade => "Schattenklinke",
                            _ => className
                        };
                    }

                    // Kapitel-Name ableiten
                    var chapterName = entity.ChapterId switch
                    {
                        "p1" => "Prolog: Anfang",
                        "p2" => "Prolog: Erwachen",
                        "p3" => "Prolog: System",
                        _ when entity.ChapterId.StartsWith("k") => $"Kapitel {entity.ChapterId[1..]}",
                        _ => entity.ChapterId
                    };

                    _slots[i] = new SaveSlotData
                    {
                        IsEmpty = false,
                        ClassName = className,
                        Level = entity.Level,
                        ChapterName = chapterName,
                        PlayTime = TimeSpan.FromSeconds(entity.PlayTimeSeconds)
                    };
                }
                else
                {
                    _slots[i] = new SaveSlotData(); // Leer
                }
            }
        }
        catch
        {
            // Bei Fehler: alle Slots als leer anzeigen
            for (int i = 0; i < 3; i++)
                _slots[i] = new SaveSlotData();
        }
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Hintergrund
        BackgroundCompositor.SetScene("systemVoid");
        BackgroundCompositor.RenderBack(canvas, bounds, _time);

        // Titel (modusabhängig)
        var title = Mode == SaveSlotMode.NewGame ? "Neues Spiel" : "Spielstand laden";
        UIRenderer.DrawTextWithShadow(canvas, title,
            bounds.MidX, bounds.Height * 0.08f, bounds.Width * 0.06f,
            UIRenderer.PrimaryGlow);

        // 3 Slot-Karten
        var cardW = bounds.Width * 0.8f;
        var cardH = bounds.Height * 0.2f;
        var startY = bounds.Height * 0.18f;
        var spacing = cardH + bounds.Height * 0.03f;

        for (int i = 0; i < 3; i++)
        {
            _slotRects[i] = new SKRect(
                bounds.MidX - cardW / 2, startY + i * spacing,
                bounds.MidX + cardW / 2, startY + i * spacing + cardH);

            RenderSlotCard(canvas, bounds, i);
        }

        // Szenen-Partikel (ScanLines etc.)
        BackgroundCompositor.RenderFront(canvas, bounds, _time);

        // Back-Button
        var backW = bounds.Width * 0.25f;
        var backH = bounds.Height * 0.055f;
        _backButtonRect = new SKRect(
            bounds.MidX - backW / 2, bounds.Height * 0.9f,
            bounds.MidX + backW / 2, bounds.Height * 0.9f + backH);
        UIRenderer.DrawButton(canvas, _backButtonRect, "Zurück", false, false, UIRenderer.TextMuted);

        // Löschen-Dialog (Overlay)
        if (_deleteConfirmSlot >= 0)
            RenderDeleteConfirmation(canvas, bounds);
    }

    private void RenderSlotCard(SKCanvas canvas, SKRect bounds, int index)
    {
        var rect = _slotRects[index];
        var slot = _slots[index];
        var isHovered = index == _hoveredSlot;
        var isPressed = index == _pressedSlot;

        // Slot im aktuellen Modus nicht auswählbar? (NewGame → belegte, LoadGame → leere)
        var isDisabled = (Mode == SaveSlotMode.NewGame && !slot.IsEmpty)
                      || (Mode == SaveSlotMode.LoadGame && slot.IsEmpty);

        // Karten-Hintergrund (gedimmt wenn deaktiviert)
        var bgColor = isDisabled
            ? UIRenderer.CardBg.WithAlpha(100)
            : isPressed
                ? UIRenderer.CardBg.WithAlpha(200)
                : isHovered
                    ? UIRenderer.CardBg.WithAlpha(230)
                    : UIRenderer.CardBg;

        UIRenderer.DrawPanel(canvas, rect, bgColor, 10f,
            isHovered ? UIRenderer.PrimaryGlow : null);

        // Slot-Nummer
        var slotLabel = $"Slot {index + 1}";
        UIRenderer.DrawText(canvas, slotLabel,
            rect.Left + 15, rect.Top + rect.Height * 0.25f,
            rect.Height * 0.18f, UIRenderer.TextMuted);

        if (slot.IsEmpty)
        {
            // Leerer Slot
            UIRenderer.DrawText(canvas, "- Leer -",
                rect.MidX, rect.MidY, rect.Height * 0.2f,
                UIRenderer.TextSecondary, SKTextAlign.Center, true);

            UIRenderer.DrawText(canvas, "Tap zum Starten",
                rect.MidX, rect.MidY + rect.Height * 0.25f, rect.Height * 0.14f,
                UIRenderer.TextMuted, SKTextAlign.Center);
        }
        else
        {
            // Belegter Slot: Klasse, Level, Kapitel, Spielzeit
            var infoY = rect.Top + rect.Height * 0.4f;
            var fontSize = rect.Height * 0.17f;
            var leftX = rect.Left + 15;

            UIRenderer.DrawText(canvas, slot.ClassName,
                leftX, infoY, fontSize * 1.2f, UIRenderer.Primary);

            UIRenderer.DrawText(canvas, $"Level {slot.Level}",
                leftX, infoY + fontSize * 1.6f, fontSize, UIRenderer.TextPrimary);

            UIRenderer.DrawText(canvas, slot.ChapterName,
                rect.MidX, infoY + fontSize * 1.6f, fontSize, UIRenderer.TextSecondary, SKTextAlign.Center);

            // Spielzeit rechts
            UIRenderer.DrawText(canvas, slot.PlayTimeFormatted,
                rect.Right - 15, infoY + fontSize * 1.6f, fontSize,
                UIRenderer.TextMuted, SKTextAlign.Right);

            // HP/EXP Bars
            var barY = rect.Bottom - rect.Height * 0.2f;
            var barH = rect.Height * 0.08f;
            var barW = (rect.Width - 40) / 2f;

            // HP Bar
            UIRenderer.DrawProgressBar(canvas,
                new SKRect(leftX, barY, leftX + barW - 5, barY + barH),
                slot.Hp, slot.MaxHp, UIRenderer.Danger);

            // EXP Bar
            UIRenderer.DrawProgressBar(canvas,
                new SKRect(leftX + barW + 5, barY, rect.Right - 15, barY + barH),
                slot.Exp, slot.MaxExp, UIRenderer.Primary);
        }
    }

    private void RenderDeleteConfirmation(SKCanvas canvas, SKRect bounds)
    {
        // Overlay-Hintergrund
        canvas.DrawRect(bounds, _overlayPaint);

        // Dialog-Box
        var dialogW = bounds.Width * 0.7f;
        var dialogH = bounds.Height * 0.25f;
        var dialogRect = new SKRect(
            bounds.MidX - dialogW / 2, bounds.MidY - dialogH / 2,
            bounds.MidX + dialogW / 2, bounds.MidY + dialogH / 2);
        UIRenderer.DrawPanel(canvas, dialogRect, UIRenderer.PanelBg, 12f, UIRenderer.Danger);

        // Text
        UIRenderer.DrawText(canvas, $"Slot {_deleteConfirmSlot + 1} löschen?",
            dialogRect.MidX, dialogRect.Top + dialogH * 0.25f,
            dialogH * 0.15f, UIRenderer.TextPrimary, SKTextAlign.Center);
        UIRenderer.DrawText(canvas, "Dieser Vorgang kann nicht rückgängig gemacht werden.",
            dialogRect.MidX, dialogRect.Top + dialogH * 0.45f,
            dialogH * 0.1f, UIRenderer.TextSecondary, SKTextAlign.Center);

        // Buttons
        var btnW = dialogW * 0.35f;
        var btnH = dialogH * 0.2f;
        var btnY = dialogRect.Bottom - dialogH * 0.3f;

        _deleteNoRect = new SKRect(
            dialogRect.MidX - btnW - 10, btnY,
            dialogRect.MidX - 10, btnY + btnH);
        UIRenderer.DrawButton(canvas, _deleteNoRect, "Abbrechen", false, false, UIRenderer.TextMuted);

        _deleteYesRect = new SKRect(
            dialogRect.MidX + 10, btnY,
            dialogRect.MidX + btnW + 10, btnY + btnH);
        UIRenderer.DrawButton(canvas, _deleteYesRect, "Löschen", false, false, UIRenderer.Danger);
    }

    public override void HandlePointerDown(SKPoint position)
    {
        if (_deleteConfirmSlot >= 0) return;

        for (int i = 0; i < 3; i++)
        {
            if (UIRenderer.HitTest(_slotRects[i], position))
            {
                _pressedSlot = i;
                return;
            }
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        if (_deleteConfirmSlot >= 0) return;

        _hoveredSlot = -1;
        for (int i = 0; i < 3; i++)
        {
            if (UIRenderer.HitTest(_slotRects[i], position))
            {
                _hoveredSlot = i;
                return;
            }
        }
    }

    public override void HandlePointerUp(SKPoint position)
    {
        _pressedSlot = -1;
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        // Löschen-Dialog aktiv
        if (_deleteConfirmSlot >= 0)
        {
            if (action == InputAction.Tap)
            {
                if (UIRenderer.HitTest(_deleteNoRect, position))
                {
                    _deleteConfirmSlot = -1;
                }
                else if (UIRenderer.HitTest(_deleteYesRect, position))
                {
                    var slotIndex = _deleteConfirmSlot;
                    _deleteConfirmSlot = -1;
                    // Slot in DB löschen und UI aktualisieren
                    DeleteSlotAsync(slotIndex);
                }
            }
            else if (action == InputAction.Back)
            {
                _deleteConfirmSlot = -1;
            }
            return;
        }

        switch (action)
        {
            case InputAction.Tap:
                // Back-Button
                if (UIRenderer.HitTest(_backButtonRect, position))
                {
                    SceneManager.ChangeScene<TitleScene>(new FadeTransition());
                    return;
                }

                // Slot getappt
                for (int i = 0; i < 3; i++)
                {
                    if (UIRenderer.HitTest(_slotRects[i], position))
                    {
                        OnSlotTapped(i);
                        return;
                    }
                }
                break;

            case InputAction.Hold:
                // Long-Press auf belegtem Slot → Löschen
                for (int i = 0; i < 3; i++)
                {
                    if (UIRenderer.HitTest(_slotRects[i], position) && !_slots[i].IsEmpty)
                    {
                        _deleteConfirmSlot = i;
                        return;
                    }
                }
                break;

            case InputAction.Back:
                SceneManager.ChangeScene<TitleScene>(new FadeTransition());
                break;
        }
    }

    private void OnSlotTapped(int index)
    {
        if (_isLoading) return;

        if (Mode == SaveSlotMode.NewGame)
        {
            // Neues Spiel: Nur leere Slots sind auswählbar
            if (!_slots[index].IsEmpty) return;

            // Neues Spiel → Prolog starten (Prolog-Held Level 50, Schwertmeister)
            // Klassenwahl erfolgt erst in K1 im Story-Verlauf
            StartPrologAsync(index);
        }
        else
        {
            // Fortsetzen: Nur belegte Slots sind auswählbar
            if (_slots[index].IsEmpty) return;

            // Spielstand laden und zur letzten Position navigieren
            LoadGameAsync(index);
        }
    }

    /// <summary>
    /// Lädt einen Spielstand aus der DB und navigiert zur Overworld oder DialogueScene.
    /// </summary>
    private async void LoadGameAsync(int uiSlotIndex)
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            // UI-Index 0-2 → DB-SlotNumber 1-3
            var slotNumber = uiSlotIndex + 1;
            var player = await _saveGameService.LoadGameAsync(
                slotNumber, _skillService, _inventoryService,
                _affinityService, _fateTrackingService, _codexService);

            if (player == null)
            {
                _isLoading = false;
                return;
            }

            // StoryEngine mit geladenem Spieler synchronisieren
            _storyEngine.SetPlayer(player);

            // Kapitel laden
            await _storyEngine.LoadChapterAsync(player.CurrentChapterId);

            // Zur Overworld navigieren (Standard nach Laden)
            SceneManager.ChangeScene<OverworldScene>(
                scene => scene.SetPlayer(player),
                new FadeTransition());
        }
        catch
        {
            // Laden fehlgeschlagen - Szene bleibt offen
            _isLoading = false;
        }
    }

    /// <summary>
    /// Startet den Prolog mit einem Level-50-Held (Schwertmeister).
    /// Klassenwahl erfolgt erst in K1.
    /// </summary>
    private async void StartPrologAsync(int uiSlotIndex)
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            // Prolog-Held erstellen (Level 50 Schwertmeister)
            var player = Player.CreatePrologHero();
            player.CurrentChapterId = "p1";

            // StoryEngine initialisieren
            _storyEngine.SetPlayer(player);
            await _storyEngine.LoadChapterAsync("p1");

            // Zur DialogueScene wechseln
            SceneManager.ChangeScene<DialogueScene>(new FadeTransition());
        }
        catch
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Löscht einen Slot in der DB und aktualisiert die UI.
    /// </summary>
    private async void DeleteSlotAsync(int uiSlotIndex)
    {
        try
        {
            // UI-Index 0-2 → DB-SlotNumber 1-3
            await _saveGameService.DeleteSlotAsync(uiSlotIndex + 1);
            _slots[uiSlotIndex] = new SaveSlotData();
        }
        catch
        {
            // Fehler beim Löschen ignorieren
        }
    }
}

/// <summary>
/// Daten eines Spielstand-Slots. Leere Slots haben IsEmpty = true.
/// Wird später vom SaveGameService befüllt.
/// </summary>
public class SaveSlotData
{
    public bool IsEmpty { get; set; } = true;
    public string ClassName { get; set; } = "";
    public int Level { get; set; }
    public string ChapterName { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Exp { get; set; }
    public int MaxExp { get; set; }
    public TimeSpan PlayTime { get; set; }

    public string PlayTimeFormatted => PlayTime.TotalHours >= 1
        ? $"{(int)PlayTime.TotalHours}h {PlayTime.Minutes:D2}m"
        : $"{PlayTime.Minutes}m {PlayTime.Seconds:D2}s";
}
