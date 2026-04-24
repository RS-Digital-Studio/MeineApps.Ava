using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel für den Activity-Feed im Dashboard.
/// Empfängt Activity-Einträge über IBotEventStream (vorgefiltertes Subset der Logs).
/// </summary>
public partial class ActivityFeedViewModel : ViewModelBase, IDisposable
{
    private readonly IBotEventStream _eventStream;

    /// <summary>Letzte 50 Aktionen des Bots (kein Debug-Spam).</summary>
    public ObservableCollection<ActivityItem> RecentActivity { get; } = new();

    public ActivityFeedViewModel(IBotEventStream eventStream)
    {
        _eventStream = eventStream;
        _eventStream.ActivityFeed += OnActivity;
    }

    private void OnActivity(ActivityFeedDto entry)
    {
        if (entry.Level == LogLevel.Debug) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RecentActivity.Insert(0, new ActivityItem(
                entry.TimestampUtc,
                entry.Category,
                entry.Message,
                entry.Level,
                entry.Symbol));

            while (RecentActivity.Count > 50)
                RecentActivity.RemoveAt(RecentActivity.Count - 1);
        });
    }

    public void Dispose()
    {
        _eventStream.ActivityFeed -= OnActivity;
    }
}
