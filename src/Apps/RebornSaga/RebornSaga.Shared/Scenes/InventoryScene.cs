namespace RebornSaga.Scenes;

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
/// Inventar-Szene. Grid-Ansicht aller Items mit Ausrüsten/Benutzen-Funktionalität.
/// Wird als Push-Szene über der Overworld geöffnet.
/// </summary>
public class InventoryScene : Scene
{
    private readonly InventoryService _inventory;
    private readonly Player _player;
    private readonly SpriteCache? _spriteCache;

    // Tabs: Alle, Waffen, Rüstung, Accessoire, Verbrauch, Key
    private static readonly ItemType?[] Tabs = { null, ItemType.Weapon, ItemType.Armor, ItemType.Accessory, ItemType.Consumable, ItemType.KeyItem };
    private static readonly string[] TabNames = { "Alle", "Waffen", "Rüstung", "Accessoire", "Verbrauch", "Schlüssel" };
    private int _selectedTab;

    // Items in der aktuellen Ansicht
    private List<(Item item, int count)> _currentItems = new();
    private int _selectedItemIndex = -1;
    private int _scrollOffset;

    // Layout-Rects (gecacht bei Bounds-Änderung)
    private SKRect _lastBounds;
    private SKRect _backButtonRect;
    private readonly SKRect[] _tabRects = new SKRect[6];
    private readonly SKRect[] _itemRects = new SKRect[8]; // Max 8 sichtbare Items
    private SKRect _actionButton1Rect;
    private SKRect _actionButton2Rect;
    private SKRect _detailPanelRect;

    // Gecachte Strings
    private string _cachedGoldText = "";
    private int _lastGold;

    // Gecachte Detail-Panel Strings (nur bei Item-Wechsel aktualisieren)
    private string? _lastDetailItemId;
    private string _cachedTypeText = "";
    private string _cachedSellText = "";
    private readonly List<(string text, SKColor color)> _cachedDetailStats = new();

    // Gecachte Item-Listen Strings (pro sichtbarem Slot)
    private readonly string[] _cachedItemSuffix = new string[8];
    private readonly int[] _lastItemCount = new int[8];
    private readonly bool[] _lastItemEquipped = new bool[8];
    private readonly string?[] _lastItemIds = new string?[8];

    // Statische Paints
    private static readonly SKPaint _overlayPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = UIRenderer.DarkBg.WithAlpha(240) };
    private static readonly SKPaint _selectedPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = UIRenderer.PrimaryGlow };
    private static readonly SKPaint _equippedPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = UIRenderer.Accent.WithAlpha(40) };

    // Qualitäts-Glow Paints (gecacht, NICHT per-Frame allokieren)
    private static readonly SKMaskFilter _qualityGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
    private static readonly SKPaint _glowBorderPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f
    };
    private static readonly SKPaint _iconBitmapPaint = new() { IsAntialias = true };

    public InventoryScene(InventoryService inventory, Player player, SpriteCache? spriteCache = null)
    {
        _inventory = inventory;
        _player = player;
        _spriteCache = spriteCache;
    }

    public override void OnEnter()
    {
        RefreshItemList();
    }

    private void RefreshItemList()
    {
        var filter = Tabs[_selectedTab];
        _currentItems = filter.HasValue
            ? _inventory.GetItemsByType(filter.Value)
            : _inventory.GetInventoryItems();

        _selectedItemIndex = _currentItems.Count > 0 ? 0 : -1;
        _scrollOffset = 0;
        // Detail- und Listen-Caches invalidieren bei Refresh
        _lastDetailItemId = null;
        Array.Clear(_lastItemIds);
    }

    public override void Update(float deltaTime)
    {
        // Gold-String cachen
        if (_player.Gold != _lastGold)
        {
            _lastGold = _player.Gold;
            _cachedGoldText = $"Gold: {_player.Gold:N0}";
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Layout bei Bounds-Änderung neu berechnen
        if (_lastBounds != bounds)
        {
            _lastBounds = bounds;
            CalculateLayout(bounds);
        }

        // Hintergrund
        canvas.DrawRect(bounds, _overlayPaint);

        // Header: "Inventar" + Gold
        UIRenderer.DrawTextWithShadow(canvas, "Inventar", bounds.MidX, bounds.Top + bounds.Height * 0.04f,
            bounds.Width * 0.06f, UIRenderer.PrimaryGlow);
        UIRenderer.DrawText(canvas, _cachedGoldText,
            bounds.Right - bounds.Width * 0.05f, bounds.Top + bounds.Height * 0.04f,
            bounds.Width * 0.035f, UIRenderer.Accent, SKTextAlign.Right);

        // Zurück-Button
        UIRenderer.DrawButton(canvas, _backButtonRect, "Zurück", color: UIRenderer.Border);

        // Tabs
        for (int i = 0; i < TabNames.Length; i++)
        {
            var isActive = i == _selectedTab;
            var tabColor = isActive ? UIRenderer.Primary : UIRenderer.CardBg;
            UIRenderer.DrawButton(canvas, _tabRects[i], TabNames[i], color: tabColor);
        }

        // Item-Liste
        var maxVisible = Math.Min(_itemRects.Length, _currentItems.Count - _scrollOffset);
        for (int i = 0; i < maxVisible; i++)
        {
            var idx = i + _scrollOffset;
            var (item, count) = _currentItems[idx];
            var rect = _itemRects[i];
            var isSelected = idx == _selectedItemIndex;
            var isEquipped = _inventory.IsEquipped(item.Id);

            // Hintergrund
            if (isEquipped)
                canvas.DrawRect(rect, _equippedPaint);

            UIRenderer.DrawPanel(canvas, rect, UIRenderer.CardBg, 4f,
                isSelected ? UIRenderer.PrimaryGlow : null);

            if (isSelected)
            {
                using var selRect = new SKRoundRect(rect, 4f);
                canvas.DrawRoundRect(selRect, _selectedPaint);
            }

            // AI-generiertes Item-Icon (links im Slot)
            var iconSize = rect.Height - 8f;
            var iconLeft = rect.Left + 4f;
            var hasIcon = false;

            var category = item.Type.ToString().ToLowerInvariant();
            var icon = _spriteCache?.GetItemIcon(category, item.Id);
            if (icon != null)
            {
                var iconRect = new SKRect(iconLeft, rect.Top + 4f, iconLeft + iconSize, rect.Top + 4f + iconSize);
                var srcRect = new SKRect(0, 0, icon.Width, icon.Height);
                canvas.DrawBitmap(icon, srcRect, iconRect, _iconBitmapPaint);

                // Qualitäts-Glow basierend auf BuyPrice
                DrawQualityGlow(canvas, iconRect, item.BuyPrice);
                hasIcon = true;
            }

            // Item-Name + Anzahl (nach Icon versetzt wenn vorhanden)
            var textLeft = hasIcon ? iconLeft + iconSize + 6f : rect.Left + 8f;
            var fontSize = rect.Height * 0.35f;
            UIRenderer.DrawText(canvas, item.NameKey, textLeft, rect.MidY - fontSize * 0.3f,
                fontSize, UIRenderer.TextPrimary);

            // Typ + Anzahl rechts (gecacht pro sichtbarem Slot)
            if (_lastItemIds[i] != item.Id || _lastItemCount[i] != count || _lastItemEquipped[i] != isEquipped)
            {
                var countStr = count > 1 ? $"x{count}" : "";
                var equipStr = isEquipped ? " [E]" : "";
                _cachedItemSuffix[i] = $"{countStr}{equipStr}";
                _lastItemIds[i] = item.Id;
                _lastItemCount[i] = count;
                _lastItemEquipped[i] = isEquipped;
            }
            UIRenderer.DrawText(canvas, _cachedItemSuffix[i], rect.Right - 8, rect.MidY,
                fontSize * 0.8f, UIRenderer.TextSecondary, SKTextAlign.Right, true);
        }

        if (_currentItems.Count == 0)
        {
            UIRenderer.DrawText(canvas, "Keine Items", bounds.MidX, bounds.MidY,
                bounds.Width * 0.04f, UIRenderer.TextMuted, SKTextAlign.Center, true);
        }

        // Detail-Panel (rechts oder unten je nach Ausrichtung)
        if (_selectedItemIndex >= 0 && _selectedItemIndex < _currentItems.Count)
        {
            DrawDetailPanel(canvas, _currentItems[_selectedItemIndex].item);
        }
    }

    private void DrawDetailPanel(SKCanvas canvas, Item item)
    {
        UIRenderer.DrawPanel(canvas, _detailPanelRect, UIRenderer.PanelBg, 6f, UIRenderer.Primary);

        // Detail-Strings nur bei Item-Wechsel neu erstellen
        if (_lastDetailItemId != item.Id)
        {
            _lastDetailItemId = item.Id;
            RebuildDetailStrings(item);
        }

        var x = _detailPanelRect.Left + 12;
        var y = _detailPanelRect.Top + 16;
        var fontSize = _detailPanelRect.Width * 0.06f;
        var lineH = fontSize * 1.8f;

        // Name
        UIRenderer.DrawText(canvas, item.NameKey, x, y, fontSize * 1.2f, UIRenderer.PrimaryGlow);
        y += lineH * 1.2f;

        // Typ (gecacht)
        UIRenderer.DrawText(canvas, _cachedTypeText, x, y, fontSize * 0.9f, UIRenderer.TextSecondary);
        y += lineH;

        // Stats + Heal + Effekt (gecacht)
        foreach (var (text, color) in _cachedDetailStats)
        {
            UIRenderer.DrawText(canvas, text, x, y, fontSize, color);
            y += lineH;
        }

        // Preis (gecacht)
        if (!string.IsNullOrEmpty(_cachedSellText))
        {
            UIRenderer.DrawText(canvas, _cachedSellText, x, y, fontSize * 0.85f, UIRenderer.Accent);
        }

        // Aktions-Buttons (String-Literale, keine Allokation)
        if (item.IsEquippable)
        {
            var isEquipped = _inventory.IsEquipped(item.Id);
            UIRenderer.DrawButton(canvas, _actionButton1Rect, isEquipped ? "Ablegen" : "Ausrüsten",
                color: isEquipped ? UIRenderer.Border : UIRenderer.Primary);
        }
        else if (item.IsUsable)
        {
            UIRenderer.DrawButton(canvas, _actionButton1Rect, "Benutzen", color: UIRenderer.Success);
        }
    }

    /// <summary>
    /// Baut die gecachten Detail-Strings für das aktuell gewählte Item neu auf.
    /// Wird nur bei Item-Wechsel aufgerufen (nicht pro Frame).
    /// </summary>
    private void RebuildDetailStrings(Item item)
    {
        _cachedTypeText = item.Type switch
        {
            ItemType.Weapon => "Waffe",
            ItemType.Armor => "Rüstung",
            ItemType.Accessory => "Accessoire",
            ItemType.Consumable => "Verbrauchbar",
            ItemType.KeyItem => "Schlüsselitem",
            _ => ""
        };

        _cachedDetailStats.Clear();
        if (item.AtkBonus > 0) _cachedDetailStats.Add(($"ATK +{item.AtkBonus}", UIRenderer.Danger));
        if (item.DefBonus > 0) _cachedDetailStats.Add(($"DEF +{item.DefBonus}", UIRenderer.Primary));
        if (item.IntBonus > 0) _cachedDetailStats.Add(($"INT +{item.IntBonus}", UIRenderer.Secondary));
        if (item.SpdBonus != 0) _cachedDetailStats.Add(($"SPD {(item.SpdBonus > 0 ? "+" : "")}{item.SpdBonus}", UIRenderer.Success));
        if (item.HpBonus > 0) _cachedDetailStats.Add(($"HP +{item.HpBonus}", UIRenderer.Danger));
        if (item.MpBonus > 0) _cachedDetailStats.Add(($"MP +{item.MpBonus}", UIRenderer.Primary));
        if (item.LukBonus > 0) _cachedDetailStats.Add(($"LUK +{item.LukBonus}", UIRenderer.Accent));
        if (item.HealHp > 0) _cachedDetailStats.Add(($"Heilt {item.HealHp} HP", UIRenderer.Success));
        if (item.HealMp > 0) _cachedDetailStats.Add(($"Heilt {item.HealMp} MP", UIRenderer.Primary));
        if (item.HealPercent > 0) _cachedDetailStats.Add(($"Heilt {item.HealPercent}% HP+MP", UIRenderer.Success));
        if (!string.IsNullOrEmpty(item.Effect))
            _cachedDetailStats.Add((item.Effect, UIRenderer.TextMuted));

        _cachedSellText = item.SellPrice > 0 ? $"Verkauf: {item.SellPrice}G" : "";
    }

    private void CalculateLayout(SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        // Zurück-Button
        _backButtonRect = new SKRect(bounds.Left + 8, bounds.Top + 8, bounds.Left + w * 0.15f, bounds.Top + h * 0.06f);

        // Tabs
        var tabW = w / TabNames.Length;
        var tabY = bounds.Top + h * 0.08f;
        var tabH = h * 0.05f;
        for (int i = 0; i < TabNames.Length; i++)
            _tabRects[i] = new SKRect(bounds.Left + i * tabW + 2, tabY, bounds.Left + (i + 1) * tabW - 2, tabY + tabH);

        // Item-Liste (linke Hälfte)
        var listLeft = bounds.Left + 8;
        var listW = w * 0.45f;
        var listTop = tabY + tabH + 8;
        var itemH = h * 0.07f;
        for (int i = 0; i < _itemRects.Length; i++)
            _itemRects[i] = new SKRect(listLeft, listTop + i * (itemH + 4), listLeft + listW, listTop + i * (itemH + 4) + itemH);

        // Detail-Panel (rechte Hälfte)
        _detailPanelRect = new SKRect(listLeft + listW + 8, listTop, bounds.Right - 8, bounds.Bottom - h * 0.08f);

        // Aktions-Buttons
        var btnW = _detailPanelRect.Width * 0.45f;
        var btnH = h * 0.06f;
        var btnY = _detailPanelRect.Bottom - btnH - 8;
        _actionButton1Rect = new SKRect(_detailPanelRect.Left + 8, btnY, _detailPanelRect.Left + 8 + btnW, btnY + btnH);
        _actionButton2Rect = new SKRect(_detailPanelRect.Right - 8 - btnW, btnY, _detailPanelRect.Right - 8, btnY + btnH);
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
                RefreshItemList();
                return;
            }
        }

        // Item-Liste
        var maxVisible = Math.Min(_itemRects.Length, _currentItems.Count - _scrollOffset);
        for (int i = 0; i < maxVisible; i++)
        {
            if (UIRenderer.HitTest(_itemRects[i], position))
            {
                _selectedItemIndex = i + _scrollOffset;
                return;
            }
        }

        // Aktions-Button
        if (_selectedItemIndex >= 0 && _selectedItemIndex < _currentItems.Count)
        {
            var item = _currentItems[_selectedItemIndex].item;

            if (UIRenderer.HitTest(_actionButton1Rect, position))
            {
                if (item.IsEquippable)
                {
                    if (_inventory.IsEquipped(item.Id))
                        _inventory.UnequipSlot(item.Slot, _player);
                    else
                        _inventory.EquipItem(item.Id, _player);
                    RefreshItemList();
                }
                else if (item.IsUsable)
                {
                    _inventory.UseItem(item.Id, _player);
                    RefreshItemList();
                }
            }
        }
    }

    /// <summary>
    /// Preis-basierter Qualitäts-Glow um Item-Icons (Item hat kein Rarity-Enum).
    /// Common (kein Glow), Uncommon (Grün), Rare (Blau), Epic (Lila), Legendary (Gold).
    /// </summary>
    private static void DrawQualityGlow(SKCanvas canvas, SKRect rect, int buyPrice)
    {
        // Billige Items = kein Glow (Common)
        if (buyPrice <= 50) return;

        var color = buyPrice switch
        {
            <= 200 => new SKColor(0x10, 0xB9, 0x81),   // Grün (Uncommon)
            <= 800 => new SKColor(0x3B, 0x82, 0xF6),    // Blau (Rare)
            <= 2000 => new SKColor(0x8B, 0x5C, 0xF6),   // Lila (Epic)
            _ => new SKColor(0xFF, 0xD7, 0x00)           // Gold (Legendary)
        };

        _glowBorderPaint.Color = color.WithAlpha(80);
        _glowBorderPaint.MaskFilter = _qualityGlow;
        canvas.DrawRoundRect(rect, 4f, 4f, _glowBorderPaint);
        _glowBorderPaint.MaskFilter = null;
    }

    public static void Cleanup()
    {
        _overlayPaint.Dispose();
        _selectedPaint.Dispose();
        _equippedPaint.Dispose();
        _qualityGlow.Dispose();
        _glowBorderPaint.Dispose();
        _iconBitmapPaint.Dispose();
    }
}
