namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Models;
using RebornSaga.Models.Enums;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Status-Szene im Solo Leveling Stil. Zeigt detaillierte Spieler-Stats,
/// Skill-Mastery, Equipment und Level-Fortschritt.
/// </summary>
public class StatusScene : Scene
{
    private readonly Player _player;
    private readonly SkillService _skillService;
    private readonly InventoryService _inventory;
    private readonly ProgressionService _progression;
    private readonly ILocalizationService _localization;

    // Tabs: Stats, Skills, Equipment
    private int _selectedTab;
    private readonly string[] _tabLabels = new string[3];

    // Lokalisierte Strings (gecacht im Konstruktor)
    private readonly string _statusWindowTitle;
    private readonly string _noSkillsText;
    private readonly string _emptySlotText;
    private readonly string _freePointsFormat;
    private readonly string _freeText;
    private readonly string _buffText;
    private readonly string _classSwordmaster;
    private readonly string _classMage;
    private readonly string _classAssassin;
    private readonly string _slotWeapon;
    private readonly string _slotArmor;
    private readonly string _slotAccessory;

    // Layout
    private SKRect _lastBounds;
    private SKRect _backButtonRect;
    private readonly SKRect[] _tabRects = new SKRect[3];

    // Gecachte Strings (nur bei Änderung aktualisieren)
    private string _cachedLevelText = "";
    private string _cachedHpText = "";
    private string _cachedMpText = "";
    private string _cachedExpText = "";
    private string _cachedAtkText = "";
    private string _cachedDefText = "";
    private string _cachedIntText = "";
    private string _cachedSpdText = "";
    private string _cachedLukText = "";
    private string _cachedGoldText = "";
    private string _cachedFreePoints = "";
    private int _lastLevel, _lastHp, _lastMp, _lastExp;

    // Stat-Buttons (für freie Punkte)
    private readonly SKRect[] _statPlusRects = new SKRect[5];

    // Stat-Anzeige Arrays (vermeidet Array-Allokation pro Frame)
    private readonly string[] _statDisplayLabels = new string[5];
    private static readonly SKColor[] StatColors = { UIRenderer.Danger, UIRenderer.Primary, UIRenderer.Secondary, UIRenderer.Success, UIRenderer.Accent };

    // Gecachte Klassen-Text (nur bei Änderung aktualisieren)
    private string _cachedClassLine = "";

    // Gecachte Skill-Strings (nur bei Tab-Wechsel aktualisieren)
    private bool _skillsCacheDirty = true;
    private readonly List<CachedSkillEntry> _cachedSkills = new();

    // Gecachte Equipment-Strings (nur bei Tab-Wechsel aktualisieren)
    private bool _equipCacheDirty = true;
    private readonly (EquipSlot slot, string label)[] _equipSlots;
    private readonly string[] _cachedEquipBonusTexts = new string[3];

    /// <summary>Gecachte Daten für einen Skill-Eintrag im Skills-Tab.</summary>
    private struct CachedSkillEntry
    {
        public string TierText { get; set; }
        public bool IsUltimate { get; set; }
        public string NameKey { get; set; }
        public string InfoLine { get; set; } // "X MP  |  Y.Zx"
        public int Mastery { get; set; }
        public int MasteryRequired { get; set; }
        public bool CanEvolve { get; set; }
        public string? MasteryText { get; set; } // "X/Y"
        public string? TagsText { get; set; } // "AoE Feuer"
    }

    // Statische Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = UIRenderer.DarkBg.WithAlpha(230) };
    private static readonly SKPaint _glowBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKMaskFilter _panelGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);

    public StatusScene(Player player, SkillService skillService, InventoryService inventory,
        ProgressionService progression, ILocalizationService localization)
    {
        _player = player;
        _skillService = skillService;
        _inventory = inventory;
        _progression = progression;
        _localization = localization;

        _tabLabels[0] = _localization.GetString("Status") ?? "Status";
        _tabLabels[1] = _localization.GetString("Skills") ?? "Skills";
        _tabLabels[2] = _localization.GetString("Equipment") ?? "Equipment";

        _statusWindowTitle = _localization.GetString("StatusWindow") ?? "STATUS WINDOW";
        _noSkillsText = _localization.GetString("NoSkills") ?? "No skills";
        _emptySlotText = _localization.GetString("EmptySlot") ?? "- Empty -";
        _freePointsFormat = _localization.GetString("FreePoints") ?? "Free Points: {0}";
        _freeText = _localization.GetString("Free") ?? "Free";
        _buffText = _localization.GetString("Buff") ?? "Buff";
        _classSwordmaster = _localization.GetString("ClassSwordmaster") ?? "Swordmaster";
        _classMage = _localization.GetString("ClassMage") ?? "Mage";
        _classAssassin = _localization.GetString("ClassAssassin") ?? "Assassin";

        _slotWeapon = _localization.GetString("TypeWeapon") ?? "Weapon";
        _slotArmor = _localization.GetString("TypeArmor") ?? "Armor";
        _slotAccessory = _localization.GetString("TypeAccessory") ?? "Accessory";
        _equipSlots = new[]
        {
            (EquipSlot.Weapon, _slotWeapon),
            (EquipSlot.Armor, _slotArmor),
            (EquipSlot.Accessory, _slotAccessory)
        };
    }

    public override void OnEnter()
    {
        UpdateCachedStrings();
    }

    private void UpdateCachedStrings()
    {
        _cachedLevelText = $"Lv. {_player.Level}";
        _cachedHpText = $"HP: {_player.Hp}/{_player.MaxHp}";
        _cachedMpText = $"MP: {_player.Mp}/{_player.MaxMp}";
        _cachedExpText = $"EXP: {_player.Exp}/{_player.ExpToNextLevel}";
        _cachedAtkText = $"ATK: {_player.Atk}";
        _cachedDefText = $"DEF: {_player.Def}";
        _cachedIntText = $"INT: {_player.Int}";
        _cachedSpdText = $"SPD: {_player.Spd}";
        _cachedLukText = $"LUK: {_player.Luk}";
        _cachedGoldText = $"Gold: {_player.Gold:N0}";
        _cachedFreePoints = _player.FreeStatPoints > 0 ? string.Format(_freePointsFormat, _player.FreeStatPoints) : "";
        _lastLevel = _player.Level;
        _lastHp = _player.Hp;
        _lastMp = _player.Mp;
        _lastExp = _player.Exp;

        // Klassen-Text für Status-Tab (gecacht statt pro Frame)
        var classText = _player.Class switch
        {
            ClassName.Swordmaster => _classSwordmaster,
            ClassName.Arcanist => _classMage,
            ClassName.Shadowblade => _classAssassin,
            _ => ""
        };
        _cachedClassLine = $"{classText}  {_cachedLevelText}";

        // Skill- und Equipment-Caches invalidieren bei Stat-Änderung
        _skillsCacheDirty = true;
        _equipCacheDirty = true;
    }

    public override void Update(float deltaTime)
    {
        // Nur bei tatsächlicher Änderung neu cachen
        if (_player.Level != _lastLevel || _player.Hp != _lastHp ||
            _player.Mp != _lastMp || _player.Exp != _lastExp)
        {
            UpdateCachedStrings();
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        if (_lastBounds != bounds)
        {
            _lastBounds = bounds;
            CalculateLayout(bounds);
        }

        // Hintergrund mit Solo Leveling Stil
        canvas.DrawRect(bounds, _bgPaint);

        // Glowing Border
        var panelRect = new SKRect(bounds.Left + 8, bounds.Top + 8, bounds.Right - 8, bounds.Bottom - 8);
        using var panelRR = new SKRoundRect(panelRect, 12f);
        _glowBorderPaint.Color = UIRenderer.PrimaryGlow.WithAlpha(80);
        _glowBorderPaint.MaskFilter = _panelGlow;
        canvas.DrawRoundRect(panelRR, _glowBorderPaint);
        _glowBorderPaint.MaskFilter = null;
        _glowBorderPaint.Color = UIRenderer.Primary;
        canvas.DrawRoundRect(panelRR, _glowBorderPaint);

        // Header: "STATUS WINDOW"
        UIRenderer.DrawTextWithShadow(canvas, _statusWindowTitle, bounds.MidX, bounds.Top + bounds.Height * 0.05f,
            bounds.Width * 0.055f, UIRenderer.PrimaryGlow);

        // Zurück-Button
        UIRenderer.DrawButton(canvas, _backButtonRect, "X", color: UIRenderer.Border);

        // Tabs
        for (int i = 0; i < _tabLabels.Length; i++)
        {
            UIRenderer.DrawButton(canvas, _tabRects[i], _tabLabels[i],
                color: i == _selectedTab ? UIRenderer.Primary : UIRenderer.CardBg);
        }

        var contentTop = _tabRects[0].Bottom + 12;

        switch (_selectedTab)
        {
            case 0: RenderStatusTab(canvas, bounds, contentTop); break;
            case 1: RenderSkillsTab(canvas, bounds, contentTop); break;
            case 2: RenderEquipmentTab(canvas, bounds, contentTop); break;
        }
    }

    private void RenderStatusTab(SKCanvas canvas, SKRect bounds, float startY)
    {
        var x = bounds.Left + bounds.Width * 0.08f;
        var w = bounds.Width * 0.84f;
        var fontSize = bounds.Width * 0.04f;
        var lineH = fontSize * 2.2f;
        var y = startY;

        // Name + Klasse + Level (gecachter Klassen-String)
        UIRenderer.DrawText(canvas, _player.Name, x, y, fontSize * 1.4f, UIRenderer.TextPrimary);
        UIRenderer.DrawText(canvas, _cachedClassLine, bounds.Right - bounds.Width * 0.08f, y,
            fontSize, UIRenderer.PrimaryGlow, SKTextAlign.Right);
        y += lineH * 1.2f;

        // EXP-Bar
        UIRenderer.DrawText(canvas, _cachedExpText, x, y, fontSize * 0.9f, UIRenderer.TextSecondary);
        y += fontSize * 1.2f;
        UIRenderer.DrawProgressBar(canvas, new SKRect(x, y, x + w, y + fontSize * 0.8f),
            _player.Exp, _player.ExpToNextLevel, UIRenderer.Primary);
        y += lineH;

        // HP-Bar
        UIRenderer.DrawText(canvas, _cachedHpText, x, y, fontSize, UIRenderer.TextPrimary);
        y += fontSize * 1.2f;
        UIRenderer.DrawProgressBar(canvas, new SKRect(x, y, x + w, y + fontSize),
            _player.Hp, _player.MaxHp, UIRenderer.Danger);
        y += lineH;

        // MP-Bar
        UIRenderer.DrawText(canvas, _cachedMpText, x, y, fontSize, UIRenderer.TextPrimary);
        y += fontSize * 1.2f;
        UIRenderer.DrawProgressBar(canvas, new SKRect(x, y, x + w, y + fontSize),
            _player.Mp, _player.MaxMp, UIRenderer.Primary);
        y += lineH * 1.2f;

        // Stats-Grid mit +Buttons (statische Arrays, keine Allokation pro Frame)
        _statDisplayLabels[0] = _cachedAtkText; _statDisplayLabels[1] = _cachedDefText;
        _statDisplayLabels[2] = _cachedIntText; _statDisplayLabels[3] = _cachedSpdText;
        _statDisplayLabels[4] = _cachedLukText;
        var hasFreePoints = _player.FreeStatPoints > 0;

        for (int i = 0; i < _statDisplayLabels.Length; i++)
        {
            UIRenderer.DrawText(canvas, _statDisplayLabels[i], x, y, fontSize, StatColors[i]);

            if (hasFreePoints)
            {
                _statPlusRects[i] = new SKRect(x + w - fontSize * 2, y - fontSize * 0.5f, x + w, y + fontSize * 0.8f);
                UIRenderer.DrawButton(canvas, _statPlusRects[i], "+", color: UIRenderer.Success);
            }

            y += lineH * 0.8f;
        }

        // Gold
        y += lineH * 0.3f;
        UIRenderer.DrawText(canvas, _cachedGoldText, x, y, fontSize, UIRenderer.Accent);

        // Freie Punkte
        if (hasFreePoints)
        {
            y += lineH;
            UIRenderer.DrawText(canvas, _cachedFreePoints, x, y, fontSize, UIRenderer.PrimaryGlow);
        }
    }

    private void RenderSkillsTab(SKCanvas canvas, SKRect bounds, float startY)
    {
        var x = bounds.Left + bounds.Width * 0.06f;
        var w = bounds.Width * 0.88f;
        var fontSize = bounds.Width * 0.035f;
        var lineH = fontSize * 2.5f;
        var y = startY;

        // Skill-Cache bei Bedarf aktualisieren (nicht pro Frame)
        if (_skillsCacheDirty)
        {
            RebuildSkillsCache();
            _skillsCacheDirty = false;
        }

        if (_cachedSkills.Count == 0)
        {
            UIRenderer.DrawText(canvas, _noSkillsText, bounds.MidX, bounds.MidY,
                fontSize * 1.2f, UIRenderer.TextMuted, SKTextAlign.Center, true);
            return;
        }

        foreach (var entry in _cachedSkills)
        {
            var cardRect = new SKRect(x, y, x + w, y + lineH * 1.5f);

            // Hintergrund mit Glow für Ultimate
            var glowColor = entry.IsUltimate ? UIRenderer.Accent : (SKColor?)null;
            UIRenderer.DrawPanel(canvas, cardRect, UIRenderer.CardBg, 6f, glowColor);

            // Tier-Indikator (gecacht)
            var tierColor = entry.IsUltimate ? UIRenderer.Accent : UIRenderer.TextSecondary;
            UIRenderer.DrawText(canvas, entry.TierText, x + 8, y + lineH * 0.4f, fontSize * 0.8f, tierColor);

            // Name
            UIRenderer.DrawText(canvas, entry.NameKey, x + fontSize * 3, y + lineH * 0.35f, fontSize, UIRenderer.TextPrimary);

            // MP-Kosten + Multiplier (gecacht)
            UIRenderer.DrawText(canvas, entry.InfoLine, x + fontSize * 3, y + lineH * 0.85f,
                fontSize * 0.85f, UIRenderer.TextSecondary);

            // Mastery-Bar
            if (!entry.IsUltimate && entry.MasteryRequired > 0)
            {
                var barRect = new SKRect(x + w - w * 0.3f, y + lineH * 0.3f, x + w - 8, y + lineH * 0.6f);
                UIRenderer.DrawProgressBar(canvas, barRect, entry.Mastery, entry.MasteryRequired, UIRenderer.Secondary);
                UIRenderer.DrawText(canvas, entry.MasteryText!,
                    barRect.MidX, barRect.Bottom + fontSize * 0.5f, fontSize * 0.7f,
                    entry.CanEvolve ? UIRenderer.Success : UIRenderer.TextMuted, SKTextAlign.Center);
            }

            // AoE + Element Tags (gecacht)
            if (entry.TagsText != null)
            {
                UIRenderer.DrawText(canvas, entry.TagsText, x + w - 8, y + lineH * 0.85f,
                    fontSize * 0.75f, UIRenderer.TextMuted, SKTextAlign.Right);
            }

            y += lineH * 1.7f;
            if (y > bounds.Bottom - lineH * 2) break;
        }
    }

    /// <summary>
    /// Baut den Skill-Cache komplett neu auf.
    /// Wird nur bei Tab-Wechsel oder Stat-Änderung aufgerufen.
    /// </summary>
    private void RebuildSkillsCache()
    {
        _cachedSkills.Clear();
        var skills = _skillService.GetUnlockedSkills();

        foreach (var skill in skills.Where(s => s.IsUnlocked))
        {
            var def = skill.Definition;
            var mpText = def.MpCost > 0 ? $"{def.MpCost} MP" : _freeText;
            var multiText = def.Multiplier > 0 ? $"{def.Multiplier:F1}x" : _buffText;

            string? tagsText = null;
            var tags = "";
            if (def.IsAoe) tags += "AoE ";
            if (def.Element.HasValue) tags += def.Element.Value.ToString();
            if (!string.IsNullOrEmpty(tags)) tagsText = tags.Trim();

            _cachedSkills.Add(new CachedSkillEntry
            {
                TierText = def.IsUltimate ? "ULT" : $"T{def.Tier}",
                IsUltimate = def.IsUltimate,
                NameKey = _localization.GetString(def.NameKey) ?? def.NameKey,
                InfoLine = $"{mpText}  |  {multiText}",
                Mastery = skill.Mastery,
                MasteryRequired = def.MasteryRequired,
                CanEvolve = skill.CanEvolve,
                MasteryText = def.MasteryRequired > 0 ? $"{skill.Mastery}/{def.MasteryRequired}" : null,
                TagsText = tagsText
            });
        }
    }

    private void RenderEquipmentTab(SKCanvas canvas, SKRect bounds, float startY)
    {
        var x = bounds.Left + bounds.Width * 0.06f;
        var w = bounds.Width * 0.88f;
        var fontSize = bounds.Width * 0.04f;
        var lineH = fontSize * 2.5f;
        var y = startY;

        // Equipment-Cache bei Bedarf aktualisieren (nicht pro Frame)
        if (_equipCacheDirty)
        {
            RebuildEquipmentCache();
            _equipCacheDirty = false;
        }

        for (int i = 0; i < _equipSlots.Length; i++)
        {
            var (slot, label) = _equipSlots[i];
            var cardRect = new SKRect(x, y, x + w, y + lineH * 1.4f);
            UIRenderer.DrawPanel(canvas, cardRect, UIRenderer.CardBg, 6f);

            // Slot-Label (statischer String, keine Allokation)
            UIRenderer.DrawText(canvas, label, x + 8, y + lineH * 0.3f, fontSize * 0.85f, UIRenderer.TextSecondary);

            // Ausgerüstetes Item
            var equipped = _inventory.GetEquipped(slot);
            if (equipped != null)
            {
                var equipName = _localization.GetString(equipped.NameKey) ?? equipped.NameKey;
                UIRenderer.DrawText(canvas, equipName, x + 8, y + lineH * 0.8f, fontSize, UIRenderer.TextPrimary);

                // Stat-Boni (gecacht)
                UIRenderer.DrawText(canvas, _cachedEquipBonusTexts[i], x + w - 8, y + lineH * 0.8f,
                    fontSize * 0.75f, UIRenderer.Accent, SKTextAlign.Right);
            }
            else
            {
                UIRenderer.DrawText(canvas, _emptySlotText, x + 8, y + lineH * 0.8f, fontSize, UIRenderer.TextMuted);
            }

            y += lineH * 1.6f;
        }
    }

    /// <summary>
    /// Baut den Equipment-Bonus-Cache neu auf.
    /// Wird nur bei Tab-Wechsel oder Stat-Änderung aufgerufen.
    /// </summary>
    private void RebuildEquipmentCache()
    {
        for (int i = 0; i < _equipSlots.Length; i++)
        {
            var equipped = _inventory.GetEquipped(_equipSlots[i].slot);
            if (equipped != null)
            {
                var bonusText = "";
                if (equipped.AtkBonus > 0) bonusText += $"ATK+{equipped.AtkBonus} ";
                if (equipped.DefBonus > 0) bonusText += $"DEF+{equipped.DefBonus} ";
                if (equipped.IntBonus > 0) bonusText += $"INT+{equipped.IntBonus} ";
                if (equipped.SpdBonus != 0) bonusText += $"SPD{(equipped.SpdBonus > 0 ? "+" : "")}{equipped.SpdBonus} ";
                if (equipped.HpBonus > 0) bonusText += $"HP+{equipped.HpBonus} ";
                if (equipped.MpBonus > 0) bonusText += $"MP+{equipped.MpBonus} ";
                _cachedEquipBonusTexts[i] = bonusText.Trim();
            }
            else
            {
                _cachedEquipBonusTexts[i] = "";
            }
        }
    }

    private void CalculateLayout(SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        _backButtonRect = new SKRect(bounds.Right - w * 0.1f, bounds.Top + 12, bounds.Right - 12, bounds.Top + h * 0.055f);

        var tabY = bounds.Top + h * 0.08f;
        var tabH = h * 0.045f;
        var tabW = (w - 32) / 3f;
        for (int i = 0; i < 3; i++)
            _tabRects[i] = new SKRect(bounds.Left + 12 + i * (tabW + 4), tabY, bounds.Left + 12 + (i + 1) * tabW + i * 4, tabY + tabH);
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action == InputAction.Back)
        {
            SceneManager.PopScene();
            return;
        }
        if (action != InputAction.Tap) return;

        // Zurück
        if (UIRenderer.HitTest(_backButtonRect, position))
        {
            SceneManager.PopScene();
            return;
        }

        // Tabs
        for (int i = 0; i < _tabRects.Length; i++)
        {
            if (UIRenderer.HitTest(_tabRects[i], position))
            {
                _selectedTab = i;
                // Caches invalidieren beim Tab-Wechsel
                _skillsCacheDirty = true;
                _equipCacheDirty = true;
                return;
            }
        }

        // Stat-Plus-Buttons (nur im Status-Tab)
        if (_selectedTab == 0 && _player.FreeStatPoints > 0)
        {
            var stats = new[] { StatType.Atk, StatType.Def, StatType.Int, StatType.Spd, StatType.Luk };
            for (int i = 0; i < _statPlusRects.Length; i++)
            {
                if (UIRenderer.HitTest(_statPlusRects[i], position))
                {
                    AllocateStat(stats[i]);
                    return;
                }
            }
        }
    }

    private void AllocateStat(StatType stat)
    {
        if (_progression.AllocateStatPoint(_player, stat))
            UpdateCachedStrings();
    }

    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _glowBorderPaint.Dispose();
        _panelGlow.Dispose();
    }
}
