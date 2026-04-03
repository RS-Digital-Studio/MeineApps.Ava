using BingXBot.Core.Models;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für den Activity-Feed im Dashboard.
/// Empfängt Log-Einträge vom BotEventBus und zeigt die letzten 20 relevanten Einträge an.
/// </summary>
public partial class ActivityFeedViewModel : ViewModelBase, IDisposable
{
    private readonly BotEventBus _eventBus;

    /// <summary>Letzte 20 Aktionen des Bots (kein Debug-Spam).</summary>
    public ObservableCollection<ActivityItem> RecentActivity { get; } = new();

    public ActivityFeedViewModel(BotEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.LogEmitted += OnLogEmitted;
    }

    /// <summary>Empfängt Log-Einträge vom EventBus und fügt relevante in den Activity-Feed ein.</summary>
    private void OnLogEmitted(object? sender, LogEntry entry)
    {
        // Nur relevante Kategorien anzeigen (kein Debug-Spam)
        if (entry.Level == Core.Enums.LogLevel.Debug) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RecentActivity.Insert(0, new ActivityItem(
                entry.Timestamp,
                entry.Category,
                entry.Message,
                entry.Level,
                entry.Symbol));

            // Max 20 Einträge
            while (RecentActivity.Count > 20)
                RecentActivity.RemoveAt(RecentActivity.Count - 1);
        });
    }

    public void Dispose()
    {
        _eventBus.LogEmitted -= OnLogEmitted;
    }
}
