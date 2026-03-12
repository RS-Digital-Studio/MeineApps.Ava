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
/// Shop-Szene. Kaufen und Verkaufen von Items beim Händler.
/// Wird als Push-Szene über der Overworld geöffnet.
/// </summary>
public class ShopScene : Scene
{
    private readonly InventoryService _inventory;
    private readonly GoldService _goldService;
    private readonly Player _player;
    private readonly List<Item> _shopItems;
    private readonly ILocalizationService _localization;

    // Lokalisierte Strings (gecacht im Konstruktor)
    private readonly string _merchantText;
    private readonly string _backText;
    private readonly string _buyText;
    private readonly string _sellText;
    private readonly string _nothingToSellText;
    private readonly string _noWaresText;
    private readonly string _buyFormat;
    private readonly string _sellFormat;
    private readonly string _healHpFormat;
    private readonly string _healMpFormat;
    private readonly string _onlyFormat;

    // Modus: Kaufen oder Verkaufen
    private bool _isSelling;

    // Angezeigte Items (Shop-Items oder Spieler-Inventar zum Verkaufen)
    private List<(Item item, int count)> _displayItems = new();
    private int _selectedIndex = -1;
    private int _scrollOffset;

    // Layout
    private SKRect _lastBounds;
    private SKRect _backButtonRect;
    private SKRect _buyTabRect;
    private SKRect _sellTabRect;
    private readonly SKRect[] _itemRects = new SKRect[7];
    private SKRect _detailPanelRect;
    private SKRect _actionButtonRect;

    // Gecachte Strings
    private string _cachedGoldText = "";
    private int _lastGold;

    // Gecachte Detail-Panel Strings (nur bei Item-Wechsel aktualisieren)
    private string? _lastDetailItemId;
    private bool _lastDetailIsSelling;
    private string _cachedBtnText = "";
    private readonly List<(string text, SKColor color)> _cachedDetailStats = new();

    // Gecachte Preis-Strings pro Item-Index (max 7 sichtbar)
    private readonly string[] _cachedPriceTexts = new string[7];
    private readonly int[] _lastPrices = new int[7];

    // Statische Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = UIRenderer.DarkBg.WithAlpha(240) };
    private static readonly SKPaint _affordPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = UIRenderer.Success.WithAlpha(30) };
    private static readonly SKPaint _cantAffordPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = UIRenderer.Danger.WithAlpha(20) };

    /// <summary>
    /// Erstellt die Shop-Szene mit den verfügbaren Items.
    /// </summary>
    /// <param name="inventory">Inventar-Service.</param>
    /// <param name="player">Spieler-Instanz.</param>
    /// <param name="shopItems">Verfügbare Items im Shop.</param>
    public ShopScene(InventoryService inventory, GoldService goldService, Player player,
        List<Item> shopItems, ILocalizationService localization)
    {
        _inventory = inventory;
        _goldService = goldService;
        _player = player;
        _shopItems = shopItems;
        _localization = localization;

        _merchantText = _localization.GetString("Merchant") ?? "Merchant";
        _backText = _localization.GetString("Back") ?? "Back";
        _buyText = _localization.GetString("Buy") ?? "Buy";
        _sellText = _localization.GetString("Sell") ?? "Sell";
        _nothingToSellText = _localization.GetString("NothingToSell") ?? "Nothing to sell";
        _noWaresText = _localization.GetString("NoWares") ?? "No wares";
        _buyFormat = _localization.GetString("BuyFormat") ?? "Buy ({0}G)";
        _sellFormat = _localization.GetString("SellFormat") ?? "Sell ({0}G)";
        _healHpFormat = _localization.GetString("HealHpFormat") ?? "Heals {0} HP";
        _healMpFormat = _localization.GetString("HealMpFormat") ?? "Heals {0} MP";
        _onlyFormat = _localization.GetString("OnlyRestriction") ?? "Only: {0}";
    }

    public override void OnEnter()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_isSelling)
        {
            _displayItems = _inventory.GetInventoryItems()
                .Where(x => x.item.SellPrice > 0 && x.item.Type != ItemType.KeyItem)
                .ToList();
        }
        else
        {
            _displayItems = _shopItems.Select(i => (i, 1)).ToList();
        }

        _selectedIndex = _displayItems.Count > 0 ? 0 : -1;
        _scrollOffset = 0;
        // Detail- und Preis-Caches invalidieren bei Refresh
        _lastDetailItemId = null;
        Array.Clear(_lastPrices);
    }

    public override void Update(float deltaTime)
    {
        if (_player.Gold != _lastGold)
        {
            _lastGold = _player.Gold;
            _cachedGoldText = $"Gold: {_player.Gold:N0}";
        }
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        if (_lastBounds != bounds)
        {
            _lastBounds = bounds;
            CalculateLayout(bounds);
        }

        // Hintergrund
        canvas.DrawRect(bounds, _bgPaint);

        // Header
        UIRenderer.DrawTextWithShadow(canvas, _merchantText, bounds.MidX, bounds.Top + bounds.Height * 0.04f,
            bounds.Width * 0.06f, UIRenderer.Accent);
        UIRenderer.DrawText(canvas, _cachedGoldText,
            bounds.Right - bounds.Width * 0.05f, bounds.Top + bounds.Height * 0.04f,
            bounds.Width * 0.035f, UIRenderer.Accent, SKTextAlign.Right);

        // Zurück-Button
        UIRenderer.DrawButton(canvas, _backButtonRect, _backText, color: UIRenderer.Border);

        // Kaufen/Verkaufen Tabs
        UIRenderer.DrawButton(canvas, _buyTabRect, _buyText, color: !_isSelling ? UIRenderer.Primary : UIRenderer.CardBg);
        UIRenderer.DrawButton(canvas, _sellTabRect, _sellText, color: _isSelling ? UIRenderer.Primary : UIRenderer.CardBg);

        // Item-Liste
        var maxVisible = Math.Min(_itemRects.Length, _displayItems.Count - _scrollOffset);
        for (int i = 0; i < maxVisible; i++)
        {
            var idx = i + _scrollOffset;
            var (item, _) = _displayItems[idx];
            var rect = _itemRects[i];
            var isSelected = idx == _selectedIndex;
            var price = _isSelling ? item.SellPrice : item.BuyPrice;
            var canAfford = _isSelling || _player.Gold >= price;

            // Hintergrund-Highlight
            if (!_isSelling)
                canvas.DrawRect(rect, canAfford ? _affordPaint : _cantAffordPaint);

            UIRenderer.DrawPanel(canvas, rect, UIRenderer.CardBg, 4f,
                isSelected ? UIRenderer.PrimaryGlow : null);

            // Name
            var fontSize = rect.Height * 0.35f;
            var itemDisplayName = _localization.GetString(item.NameKey) ?? item.NameKey;
            UIRenderer.DrawText(canvas, itemDisplayName, rect.Left + 8, rect.MidY - fontSize * 0.3f,
                fontSize, canAfford ? UIRenderer.TextPrimary : UIRenderer.TextMuted);

            // Preis (gecacht pro sichtbarem Slot)
            if (price != _lastPrices[i])
            {
                _cachedPriceTexts[i] = $"{price}G";
                _lastPrices[i] = price;
            }
            UIRenderer.DrawText(canvas, _cachedPriceTexts[i], rect.Right - 8, rect.MidY,
                fontSize, canAfford ? UIRenderer.Accent : UIRenderer.Danger, SKTextAlign.Right, true);
        }

        if (_displayItems.Count == 0)
        {
            var emptyText = _isSelling ? _nothingToSellText : _noWaresText;
            UIRenderer.DrawText(canvas, emptyText, bounds.MidX, bounds.MidY,
                bounds.Width * 0.04f, UIRenderer.TextMuted, SKTextAlign.Center, true);
        }

        // Detail-Panel
        if (_selectedIndex >= 0 && _selectedIndex < _displayItems.Count)
        {
            DrawDetailPanel(canvas, _displayItems[_selectedIndex].item);
        }
    }

    private void DrawDetailPanel(SKCanvas canvas, Item item)
    {
        UIRenderer.DrawPanel(canvas, _detailPanelRect, UIRenderer.PanelBg, 6f, UIRenderer.Accent);

        // Detail-Strings nur bei Item- oder Modus-Wechsel neu erstellen
        if (_lastDetailItemId != item.Id || _lastDetailIsSelling != _isSelling)
        {
            _lastDetailItemId = item.Id;
            _lastDetailIsSelling = _isSelling;
            RebuildDetailStrings(item);
        }

        var x = _detailPanelRect.Left + 12;
        var y = _detailPanelRect.Top + 16;
        var fontSize = _detailPanelRect.Width * 0.055f;
        var lineH = fontSize * 1.8f;

        // Name
        var detailName = _localization.GetString(item.NameKey) ?? item.NameKey;
        UIRenderer.DrawText(canvas, detailName, x, y, fontSize * 1.2f, UIRenderer.Accent);
        y += lineH * 1.2f;

        // Gecachte Stats zeichnen
        foreach (var (text, color) in _cachedDetailStats)
        {
            UIRenderer.DrawText(canvas, text, x, y, fontSize, color);
            y += lineH;
        }

        // Klassen-Beschränkung (gecacht in _cachedDetailStats, separat wegen fontSize)
        // → wird bereits in RebuildDetailStrings() berücksichtigt

        // Aktions-Button (gecacht)
        var price = _isSelling ? item.SellPrice : item.BuyPrice;
        var canAfford = _isSelling || _player.Gold >= price;
        var btnColor = canAfford ? (_isSelling ? UIRenderer.Accent : UIRenderer.Success) : UIRenderer.Border;
        UIRenderer.DrawButton(canvas, _actionButtonRect, _cachedBtnText, color: btnColor);
    }

    /// <summary>
    /// Baut die gecachten Detail-Strings für das aktuell gewählte Item neu auf.
    /// Wird nur bei Item- oder Modus-Wechsel aufgerufen (nicht pro Frame).
    /// </summary>
    private void RebuildDetailStrings(Item item)
    {
        _cachedDetailStats.Clear();

        if (item.AtkBonus > 0) _cachedDetailStats.Add(($"ATK +{item.AtkBonus}", UIRenderer.Danger));
        if (item.DefBonus > 0) _cachedDetailStats.Add(($"DEF +{item.DefBonus}", UIRenderer.Primary));
        if (item.IntBonus > 0) _cachedDetailStats.Add(($"INT +{item.IntBonus}", UIRenderer.Secondary));
        if (item.SpdBonus != 0) _cachedDetailStats.Add(($"SPD {(item.SpdBonus > 0 ? "+" : "")}{item.SpdBonus}", UIRenderer.Success));
        if (item.HpBonus > 0) _cachedDetailStats.Add(($"HP +{item.HpBonus}", UIRenderer.Danger));
        if (item.MpBonus > 0) _cachedDetailStats.Add(($"MP +{item.MpBonus}", UIRenderer.Primary));
        if (item.HealHp > 0) _cachedDetailStats.Add((string.Format(_healHpFormat, item.HealHp), UIRenderer.Success));
        if (item.HealMp > 0) _cachedDetailStats.Add((string.Format(_healMpFormat, item.HealMp), UIRenderer.Primary));
        if (!string.IsNullOrEmpty(item.ClassRestriction))
        {
            // Klassen-Enum lokalisieren statt rohen englischen Namen anzeigen
            var localizedClass = _localization.GetString($"Class{item.ClassRestriction}") ?? item.ClassRestriction;
            _cachedDetailStats.Add((string.Format(_onlyFormat, localizedClass), UIRenderer.TextMuted));
        }

        var price = _isSelling ? item.SellPrice : item.BuyPrice;
        _cachedBtnText = _isSelling ? string.Format(_sellFormat, price) : string.Format(_buyFormat, price);
    }

    private void CalculateLayout(SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        _backButtonRect = new SKRect(bounds.Left + 8, bounds.Top + 8, bounds.Left + w * 0.15f, bounds.Top + h * 0.06f);

        // Kaufen/Verkaufen Tabs
        var tabY = bounds.Top + h * 0.08f;
        var tabH = h * 0.05f;
        _buyTabRect = new SKRect(bounds.Left + 8, tabY, bounds.Left + w * 0.48f, tabY + tabH);
        _sellTabRect = new SKRect(bounds.Left + w * 0.52f, tabY, bounds.Right - 8, tabY + tabH);

        // Item-Liste
        var listTop = tabY + tabH + 8;
        var listW = w * 0.45f;
        var itemH = h * 0.07f;
        for (int i = 0; i < _itemRects.Length; i++)
            _itemRects[i] = new SKRect(bounds.Left + 8, listTop + i * (itemH + 4), bounds.Left + 8 + listW, listTop + i * (itemH + 4) + itemH);

        // Detail-Panel
        _detailPanelRect = new SKRect(bounds.Left + 8 + listW + 8, listTop, bounds.Right - 8, bounds.Bottom - h * 0.04f);

        // Aktions-Button
        var btnH = h * 0.06f;
        _actionButtonRect = new SKRect(_detailPanelRect.Left + 8, _detailPanelRect.Bottom - btnH - 8,
            _detailPanelRect.Right - 8, _detailPanelRect.Bottom - 8);
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
        if (UIRenderer.HitTest(_buyTabRect, position))
        {
            _isSelling = false;
            RefreshDisplay();
            return;
        }
        if (UIRenderer.HitTest(_sellTabRect, position))
        {
            _isSelling = true;
            RefreshDisplay();
            return;
        }

        // Item-Auswahl
        var maxVisible = Math.Min(_itemRects.Length, _displayItems.Count - _scrollOffset);
        for (int i = 0; i < maxVisible; i++)
        {
            if (UIRenderer.HitTest(_itemRects[i], position))
            {
                _selectedIndex = i + _scrollOffset;
                return;
            }
        }

        // Kauf/Verkauf-Button
        if (_selectedIndex >= 0 && _selectedIndex < _displayItems.Count &&
            UIRenderer.HitTest(_actionButtonRect, position))
        {
            var item = _displayItems[_selectedIndex].item;
            if (_isSelling)
            {
                if (!_inventory.IsEquipped(item.Id) && _inventory.RemoveItem(item.Id))
                {
                    _goldService.AddGold(_player, item.SellPrice);
                    RefreshDisplay();
                }
            }
            else if (_goldService.SpendGold(_player, item.BuyPrice))
            {
                _inventory.AddItem(item);
                var prevIdx = _selectedIndex;
                RefreshDisplay();
                _selectedIndex = Math.Min(prevIdx, _displayItems.Count - 1);
            }
        }
    }

    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _affordPaint.Dispose();
        _cantAffordPaint.Dispose();
    }
}
