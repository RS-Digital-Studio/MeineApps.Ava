namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Models;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// Kodex-Szene: Bestiary, Lore und Charakter-Profile.
/// Zeigt freigeschaltete Einträge nach Kategorien sortiert.
/// </summary>
public class CodexScene : Scene
{
    private readonly CodexService _codexService;
    private readonly ILocalizationService _localization;
    private float _time;

    // Kategorien und Einträge
    private List<string> _categories = new();
    private List<CodexEntry> _currentEntries = new();
    private int _selectedCategory;
    private int _selectedEntry = -1;
    private int _scrollOffset;

    // Layout
    private SKRect _backButtonRect;
    private readonly SKRect[] _categoryRects = new SKRect[6];
    private readonly SKRect[] _entryRects = new SKRect[20]; // Max sichtbare Einträge
    private int _visibleEntryCount;
    private int _hoveredEntry = -1;

    // Gecachte Texte
    private string _progressText = "";
    private int _lastTotal, _lastUnlocked;

    // Lokalisierte Strings (gecacht im Konstruktor)
    private readonly string _codexTitle;
    private readonly string _backText;
    private readonly string _closeText;
    private readonly string _generalText;
    private readonly string _noEntriesText;
    private readonly string _noEntriesYetText;
    private readonly string _discoveredFormat;

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _bodyFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    public CodexScene(CodexService codexService, ILocalizationService localization)
    {
        _codexService = codexService;
        _localization = localization;

        _codexTitle = _localization.GetString("Codex") ?? "Codex";
        _backText = _localization.GetString("Back") ?? "Back";
        _closeText = _localization.GetString("Close") ?? "Close";
        _generalText = _localization.GetString("General") ?? "General";
        _noEntriesText = _localization.GetString("NoEntries") ?? "No entries";
        _noEntriesYetText = _localization.GetString("NoEntriesYet") ?? "No entries discovered yet.";
        _discoveredFormat = _localization.GetString("DiscoveredFormat") ?? "{0}/{1} discovered";
    }

    public override void OnEnter()
    {
        _time = 0;
        _selectedCategory = 0;
        _selectedEntry = -1;
        _scrollOffset = 0;
        RefreshCategories();
    }

    private void RefreshCategories()
    {
        _categories = _codexService.GetCategories();
        if (_categories.Count == 0)
            _categories.Add(_generalText);
        RefreshEntries();
    }

    private void RefreshEntries()
    {
        if (_selectedCategory < _categories.Count)
            _currentEntries = _codexService.GetEntriesByCategory(_categories[_selectedCategory]);
        else
            _currentEntries = _codexService.GetUnlockedEntries();
        _scrollOffset = 0;
        _selectedEntry = -1;
    }

    private void UpdateProgressText()
    {
        var (total, unlocked) = _codexService.GetProgress();
        if (total != _lastTotal || unlocked != _lastUnlocked)
        {
            _progressText = total > 0 ? string.Format(_discoveredFormat, unlocked, total) : _noEntriesText;
            _lastTotal = total;
            _lastUnlocked = unlocked;
        }
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Hintergrund
        _bgPaint.Color = new SKColor(0x0D, 0x11, 0x17, 255);
        canvas.DrawRect(bounds, _bgPaint);

        // Titel
        UIRenderer.DrawTextWithShadow(canvas, _codexTitle, bounds.MidX, bounds.Height * 0.06f,
            bounds.Width * 0.06f, UIRenderer.PrimaryGlow);

        // Fortschritt
        UpdateProgressText();
        UIRenderer.DrawText(canvas, _progressText, bounds.MidX, bounds.Height * 0.10f,
            bounds.Width * 0.025f, UIRenderer.TextMuted, SKTextAlign.Center);

        // Kategorie-Tabs
        RenderCategoryTabs(canvas, bounds);

        // Einträge-Liste oder Detail-Ansicht
        if (_selectedEntry >= 0 && _selectedEntry < _currentEntries.Count)
            RenderEntryDetail(canvas, bounds, _currentEntries[_selectedEntry]);
        else
            RenderEntryList(canvas, bounds);

        // Back-Button
        var backW = bounds.Width * 0.2f;
        var backH = bounds.Height * 0.05f;
        _backButtonRect = new SKRect(
            bounds.MidX - backW / 2, bounds.Height * 0.92f,
            bounds.MidX + backW / 2, bounds.Height * 0.92f + backH);
        UIRenderer.DrawButton(canvas, _backButtonRect,
            _selectedEntry >= 0 ? _backText : _closeText,
            false, false, UIRenderer.TextMuted);
    }

    private void RenderCategoryTabs(SKCanvas canvas, SKRect bounds)
    {
        var tabY = bounds.Height * 0.13f;
        var tabH = bounds.Height * 0.04f;
        var tabW = Math.Min(bounds.Width * 0.25f, bounds.Width / Math.Max(1, _categories.Count) - 8);
        var totalTabW = _categories.Count * (tabW + 6) - 6;
        var startX = bounds.MidX - totalTabW / 2;

        for (int i = 0; i < _categories.Count && i < _categoryRects.Length; i++)
        {
            var x = startX + i * (tabW + 6);
            _categoryRects[i] = new SKRect(x, tabY, x + tabW, tabY + tabH);

            var isActive = i == _selectedCategory;
            _bgPaint.Color = isActive ? UIRenderer.Primary.WithAlpha(60) : UIRenderer.CardBg;
            using var rr = new SKRoundRect(_categoryRects[i], 4f);
            canvas.DrawRoundRect(rr, _bgPaint);

            if (isActive)
            {
                _borderPaint.Color = UIRenderer.Primary.WithAlpha(120);
                canvas.DrawRoundRect(rr, _borderPaint);
            }

            _bodyFont.Size = tabH * 0.5f;
            _textPaint.Color = isActive ? UIRenderer.Primary : UIRenderer.TextSecondary;
            canvas.DrawText(_categories[i], _categoryRects[i].MidX, _categoryRects[i].MidY + tabH * 0.15f,
                SKTextAlign.Center, _bodyFont, _textPaint);
        }
    }

    private void RenderEntryList(SKCanvas canvas, SKRect bounds)
    {
        var listY = bounds.Height * 0.19f;
        var itemH = bounds.Height * 0.06f;
        var margin = bounds.Width * 0.08f;
        var listW = bounds.Width - 2 * margin;
        _visibleEntryCount = 0;

        if (_currentEntries.Count == 0)
        {
            UIRenderer.DrawText(canvas, _noEntriesYetText,
                bounds.MidX, bounds.MidY, bounds.Width * 0.03f,
                UIRenderer.TextMuted, SKTextAlign.Center);
            return;
        }

        for (int i = _scrollOffset; i < _currentEntries.Count && _visibleEntryCount < _entryRects.Length; i++)
        {
            var idx = _visibleEntryCount;
            var y = listY + idx * (itemH + 4);
            if (y + itemH > bounds.Height * 0.9f) break;

            _entryRects[idx] = new SKRect(margin, y, margin + listW, y + itemH);
            _visibleEntryCount++;

            var entry = _currentEntries[i];
            var isHovered = idx == _hoveredEntry;

            // Eintrag-Hintergrund
            _bgPaint.Color = isHovered ? UIRenderer.CardBg.WithAlpha(230) : UIRenderer.CardBg.WithAlpha(180);
            using var rr = new SKRoundRect(_entryRects[idx], 4f);
            canvas.DrawRoundRect(rr, _bgPaint);

            // Titel
            _bodyFont.Size = itemH * 0.4f;
            _textPaint.Color = UIRenderer.TextPrimary;
            canvas.DrawText(entry.TitleKey, margin + 12, y + itemH * 0.55f,
                SKTextAlign.Left, _bodyFont, _textPaint);

            // Kategorie-Tag rechts
            _bodyFont.Size = itemH * 0.3f;
            _textPaint.Color = UIRenderer.TextMuted;
            canvas.DrawText(entry.CategoryKey, margin + listW - 12, y + itemH * 0.55f,
                SKTextAlign.Right, _bodyFont, _textPaint);
        }
    }

    private void RenderEntryDetail(SKCanvas canvas, SKRect bounds, CodexEntry entry)
    {
        var margin = bounds.Width * 0.08f;
        var contentY = bounds.Height * 0.19f;
        var contentW = bounds.Width - 2 * margin;

        // Detail-Panel
        var panelRect = new SKRect(margin, contentY, margin + contentW, bounds.Height * 0.88f);
        UIRenderer.DrawPanel(canvas, panelRect, UIRenderer.CardBg, 8f, UIRenderer.Primary);

        // Titel
        _titleFont.Size = bounds.Width * 0.04f;
        _textPaint.Color = UIRenderer.Primary;
        canvas.DrawText(entry.TitleKey, panelRect.MidX, contentY + 30,
            SKTextAlign.Center, _titleFont, _textPaint);

        // Inhalt
        _bodyFont.Size = bounds.Width * 0.028f;
        _textPaint.Color = UIRenderer.TextPrimary;
        canvas.DrawText(entry.ContentKey, panelRect.Left + 15, contentY + 60,
            SKTextAlign.Left, _bodyFont, _textPaint);
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _hoveredEntry = -1;
        for (int i = 0; i < _visibleEntryCount; i++)
        {
            if (UIRenderer.HitTest(_entryRects[i], position))
            {
                _hoveredEntry = i;
                return;
            }
        }
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        switch (action)
        {
            case InputAction.Tap:
                // Back-Button
                if (UIRenderer.HitTest(_backButtonRect, position))
                {
                    if (_selectedEntry >= 0)
                        _selectedEntry = -1; // Zurück zur Liste
                    else
                        SceneManager.PopScene();
                    return;
                }

                // Kategorie-Tabs
                for (int i = 0; i < _categories.Count && i < _categoryRects.Length; i++)
                {
                    if (UIRenderer.HitTest(_categoryRects[i], position))
                    {
                        _selectedCategory = i;
                        RefreshEntries();
                        return;
                    }
                }

                // Eintrag antippen
                for (int i = 0; i < _visibleEntryCount; i++)
                {
                    if (UIRenderer.HitTest(_entryRects[i], position))
                    {
                        _selectedEntry = _scrollOffset + i;
                        return;
                    }
                }
                break;

            case InputAction.Back:
                if (_selectedEntry >= 0)
                    _selectedEntry = -1;
                else
                    SceneManager.PopScene();
                break;

            case InputAction.SwipeUp:
                if (_scrollOffset + _visibleEntryCount < _currentEntries.Count)
                    _scrollOffset++;
                break;

            case InputAction.SwipeDown:
                if (_scrollOffset > 0)
                    _scrollOffset--;
                break;
        }
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _titleFont.Dispose();
        _bodyFont.Dispose();
        _textPaint.Dispose();
    }
}
