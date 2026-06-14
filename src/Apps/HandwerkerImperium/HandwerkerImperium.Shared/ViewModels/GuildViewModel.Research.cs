using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// GuildViewModel — Gilden-Forschung: Beitrags-Dialog, Liste, Countdown, Auto-Abschluss.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Gilden-Forschung
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowResearchContributeDialog(GuildResearchDisplay? research)
    {
        if (research == null || research.IsCompleted || research.IsResearching || !research.IsActive) return;

        SelectedResearchId = research.Id;
        SelectedResearchName = research.Name;
        ResearchContributePercent = 10;
        UpdateResearchContributeDisplay();
        IsResearchContributeDialogVisible = true;
    }

    [RelayCommand]
    private void CancelResearchContribute()
    {
        IsResearchContributeDialogVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmResearchContributeAsync()
    {
        if (string.IsNullOrEmpty(SelectedResearchId)) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var money = _gameStateService.State.Money;
            var amount = (long)(money * (decimal)ResearchContributePercent / 100m);
            if (amount < 100) return;

            IsResearchContributeDialogVisible = false;
            ViewState = GuildViewState.Loading;

            var success = await _facade.Research.ContributeToResearchAsync(SelectedResearchId, amount);
            if (success)
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("GuildResearchTitle") ?? "Guild Research",
                    string.Format(
                        _localizationService.GetString("GuildResearchContributed") ?? "€{0} beigetragen!",
                        MoneyFormatter.Format(amount, 0)));

                // Forschungsliste aktualisieren
                await LoadGuildResearchAsync();
            }

            ViewState = GuildViewState.InGuild;
        }
        finally
        {
            _isBusy = false;
        }
    }

    partial void OnResearchContributePercentChanged(double value)
    {
        UpdateResearchContributeDisplay();
    }

    private void UpdateResearchContributeDisplay()
    {
        var money = _gameStateService.State.Money;
        var amount = money * (decimal)ResearchContributePercent / 100m;
        ResearchContributeDisplay = $"{MoneyFormatter.Format(amount, 0)} ({ResearchContributePercent:F0}%)";
    }

    private async Task LoadGuildResearchAsync()
    {
        var items = await _facade.Research.GetGuildResearchAsync();

        // Namen und Beschreibungen lokalisieren
        foreach (var item in items)
        {
            item.Name = _localizationService.GetString(item.Name) ?? item.Name;
            item.Description = _localizationService.GetString(item.Description) ?? item.Description;
        }

        // RemainingTime für forschende Items berechnen (für SkiaSharp-Fortschrittsring)
        var effects = _facade.Research.GetCachedEffects();
        foreach (var item in items.Where(r => r.IsResearching && !string.IsNullOrEmpty(r.ResearchStartedAt)))
        {
            if (DateTime.TryParse(item.ResearchStartedAt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
            {
                var durH = item.DurationHours;
                if (effects.ResearchSpeedBonus > 0)
                    durH *= (double)(1m - effects.ResearchSpeedBonus);
                var endTime = startedAt.AddHours(durH);
                var remaining = endTime - DateTime.UtcNow;
                item.RemainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        GuildResearch = new ObservableCollection<GuildResearchDisplay>(items);
        HasGuildResearch = GuildResearch.Count > 0;

        // Laufende Forschung finden
        var researching = items.FirstOrDefault(r => r.IsResearching);
        if (researching != null)
        {
            HasActiveResearch = true;
            ActiveResearchName = researching.Name;
            ActiveResearchId = researching.Id;
            UpdateResearchCountdown(researching);
        }
        else
        {
            HasActiveResearch = false;
            ActiveResearchName = "";
            ActiveResearchCountdown = "";
            ActiveResearchId = "";
        }

        var completed = items.Count(i => i.IsCompleted);
        GuildResearchSummary = $"{completed}/{items.Count}";

        var maxMembers = _facade.Guild.GetMaxMembers();
        MaxMembersDisplay = $"Max. {maxMembers}";
    }

    /// <summary>
    /// Aktualisiert die Countdown-Anzeige für die laufende Forschung.
    /// </summary>
    private void UpdateResearchCountdown(GuildResearchDisplay research)
    {
        if (string.IsNullOrEmpty(research.ResearchStartedAt)) return;

        if (!DateTime.TryParse(research.ResearchStartedAt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
            return;

        var durationHours = research.DurationHours;
        // Schnellforschung-Bonus
        var effects = _facade.Research.GetCachedEffects();
        if (effects.ResearchSpeedBonus > 0)
            durationHours *= (double)(1m - effects.ResearchSpeedBonus);

        var endTime = startedAt.AddHours(durationHours);
        var remaining = endTime - DateTime.UtcNow;

        // RemainingTime im Display-Objekt aktualisieren (für SkiaSharp-Renderer)
        research.RemainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;

        if (remaining <= TimeSpan.Zero)
        {
            ActiveResearchCountdown = _localizationService.GetString("GuildResearchDone") ?? "Done!";
        }
        else
        {
            // Lokalisiertes Format: "Noch 3h 24min" / "3h 24min remaining"
            var timeStr = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}min"
                : $"{remaining.Minutes}min {remaining.Seconds:D2}s";
            var template = _localizationService.GetString("GuildResearchTimeRemaining") ?? "{0}";
            ActiveResearchCountdown = string.Format(template, timeStr);
        }
    }

    /// <summary>
    /// Öffentlicher Aufruf zur Countdown-Aktualisierung (für periodischen Refresh aus der View).
    /// </summary>
    public void RefreshActiveResearchCountdown()
    {
        if (!HasActiveResearch) return;

        var researching = GuildResearch.FirstOrDefault(r => r.IsResearching);
        if (researching != null)
        {
            UpdateResearchCountdown(researching);

            // Timer abgelaufen → automatisch abschließen (einmalig)
            if (researching.RemainingTime.HasValue && researching.RemainingTime.Value <= TimeSpan.Zero)
            {
                HasActiveResearch = false; // Guard: verhindert mehrfachen Aufruf bis Reload
                CompleteResearchTimerAsync().SafeFireAndForget();
            }
        }
    }

    /// <summary>
    /// Schließt eine abgelaufene Forschung in Firebase ab und lädt Forschungsdaten neu.
    /// </summary>
    private async Task CompleteResearchTimerAsync()
    {
        try
        {
            var completed = await _facade.Research.CheckResearchCompletionAsync();
            if (completed)
            {
                CelebrationRequested?.Invoke();
            }
            // Forschungsdaten immer neu laden (auch wenn kein Completion - Daten könnten veraltet sein)
            await LoadGuildResearchAsync();
        }
        catch
        {
            // Bei Fehler: HasActiveResearch wird durch LoadGuildResearchAsync korrekt gesetzt
        }
    }
}
