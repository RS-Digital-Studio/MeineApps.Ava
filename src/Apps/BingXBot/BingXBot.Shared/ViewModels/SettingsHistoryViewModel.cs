using System.Collections.ObjectModel;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot.ViewModels;

/// <summary>
/// v1.6.3 Phase 14 — Audit-Trail-View: zeigt Settings-Aenderungen mit Filter
/// nach Field-Prefix (Bot/Risk/Scanner/Backtest), Since-Datum und Limit.
/// Quelle: <see cref="ISettingsService.GetHistoryAsync"/> (REST im Remote-Mode,
/// direkter DB-Zugriff im Local-Mode).
/// </summary>
public partial class SettingsHistoryViewModel : ViewModelBase
{
    private readonly ISettingsService? _settings;

    public ObservableCollection<SettingsChangeDto> Changes { get; } = new();

    public IReadOnlyList<string> AvailableFields { get; } = new[]
    {
        "(alle)", "Bot", "Risk", "Scanner", "Backtest"
    };

    public IReadOnlyList<string> AvailableLimits { get; } = new[] { "50", "100", "200", "500", "1000" };

    [ObservableProperty] private string _selectedField = "(alle)";
    [ObservableProperty] private DateTimeOffset? _sinceDate;
    [ObservableProperty] private string _selectedLimit = "200";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _displayedCount;

    public SettingsHistoryViewModel(ISettingsService? settings = null)
    {
        _settings = settings;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_settings == null)
        {
            StatusText = "Settings-Service nicht verfügbar";
            return;
        }
        IsLoading = true;
        StatusText = "Lade...";
        try
        {
            string? field = SelectedField == "(alle)" ? null : SelectedField;
            DateTime? since = SinceDate?.UtcDateTime;
            int limit = int.TryParse(SelectedLimit, out var l) ? l : 200;
            var dto = await _settings.GetHistoryAsync(field, since, limit);
            Changes.Clear();
            foreach (var c in dto.Changes)
                Changes.Add(c);
            DisplayedCount = Changes.Count;
            StatusText = Changes.Count == 0
                ? "Keine Einträge im gewählten Zeitraum"
                : $"{Changes.Count} Einträge";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SelectedField = "(alle)";
        SinceDate = null;
        SelectedLimit = "200";
    }
}
