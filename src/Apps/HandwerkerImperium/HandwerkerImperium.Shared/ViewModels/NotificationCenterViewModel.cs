using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer die Bell-UI (Notification-Center). Zeigt eine sortierte Liste
/// aller offenen Notifications und stellt Tap-Aktionen bereit. Der zugrunde liegende
/// State (Persistenz, Lock) lebt im <see cref="INotificationCenterService"/>.
///
/// Tap-Aktionen werden ueber <see cref="OnItemActivated"/> an MainViewModel geliefert,
/// damit der entsprechende Dialog/Tab geoeffnet wird.
/// </summary>
public sealed partial class NotificationCenterViewModel : ViewModelBase
{
    private readonly INotificationCenterService _service;
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Wird ausgeloest, wenn der Spieler ein Notification-Item antippt.
    /// MainViewModel verbindet das mit der konkreten Tap-Aktion (z.B. ClaimDailyReward).
    /// </summary>
    public event Action<NotificationItem>? ItemActivated;

    /// <summary>Anzeige-Items fuer das Popup. Reset bei <see cref="Refresh"/>.</summary>
    public ObservableCollection<NotificationDisplayItem> DisplayItems { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(HasUnseen))]
    private int _unseenCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private int _itemCount;

    [ObservableProperty]
    private bool _isPopupVisible;

    public bool HasUnseen => UnseenCount > 0;
    public bool HasItems => ItemCount > 0;
    public bool IsEmpty => ItemCount == 0;

    /// <summary>Cap auf 99+ fuer kompakte Badge-Darstellung.</summary>
    public string BadgeText => UnseenCount > 99 ? "99+" : UnseenCount.ToString();

    public NotificationCenterViewModel(INotificationCenterService service, ILocalizationService localization)
    {
        _service = service;
        _localization = localization;
        _service.Changed += OnServiceChanged;
        Refresh();
    }

    private void OnServiceChanged()
    {
        Dispatcher.UIThread.Post(Refresh);
    }

    /// <summary>
    /// Aktualisiert <see cref="DisplayItems"/>, <see cref="UnseenCount"/> und <see cref="ItemCount"/>
    /// basierend auf dem aktuellen Service-State.
    /// </summary>
    public void Refresh()
    {
        var items = _service.Items;
        UnseenCount = _service.UnseenCount;
        ItemCount = items.Count;

        DisplayItems.Clear();
        foreach (var item in items)
        {
            DisplayItems.Add(new NotificationDisplayItem
            {
                Item = item,
                Title = ResolveText(item.TitleKey, item.TitleArg),
                Body = ResolveText(item.BodyKey, item.BodyArg),
                Icon = item.IconKind ?? GetDefaultIcon(item.Kind),
                IsUnseen = !item.Seen,
                ShowPulse = item.Kind == NotificationKind.NewStoryChapter && !item.Seen,
            });
        }
    }

    private string ResolveText(string key, string? arg)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var raw = _localization.GetString(key) ?? key;
        return string.IsNullOrEmpty(arg) ? raw : string.Format(raw, arg);
    }

    private static string GetDefaultIcon(NotificationKind kind) => kind switch
    {
        NotificationKind.OfflineEarnings => "Money",
        NotificationKind.DailyReward => "Gift",
        NotificationKind.WelcomeBackOffer => "Heart",
        NotificationKind.AchievementUnlocked => "Trophy",
        NotificationKind.StreakSaved => "Fire",
        NotificationKind.NewStoryChapter => "Star",
        NotificationKind.LiveOrderAvailable => "Bell",
        _ => "Bell"
    };

    /// <summary>Oeffnet das Popup, markiert alle Items als gesehen.</summary>
    [RelayCommand]
    private void OpenPopup()
    {
        if (DisplayItems.Count == 0) return;
        IsPopupVisible = true;
        _service.MarkAllSeen();
    }

    /// <summary>Schliesst das Popup. Items bleiben sichtbar bis erledigt.</summary>
    [RelayCommand]
    private void ClosePopup() => IsPopupVisible = false;

    /// <summary>Aktiviert ein Item (Tap auf Card oder „Erledigen"-Button).</summary>
    [RelayCommand]
    private void ActivateItem(NotificationDisplayItem? display)
    {
        if (display?.Item == null) return;

        ItemActivated?.Invoke(display.Item);
        // Item wird vom Aufrufer (MainViewModel) ueber Service.Dismiss entfernt,
        // sobald die Aktion ausgefuehrt wurde. Falls die Aktion synchron beendet:
        // Dispatcher.UIThread.Post(Refresh) wird durch Changed-Event gefeuert.
    }

    /// <summary>Manuelles Schliessen ohne Aktion.</summary>
    [RelayCommand]
    private void DismissItem(NotificationDisplayItem? display)
    {
        if (display?.Item == null) return;
        _service.Dismiss(display.Item.Id);
    }

    /// <summary>Schliesst das Popup und entfernt alle Items.</summary>
    [RelayCommand]
    private void DismissAll()
    {
        _service.Clear();
        IsPopupVisible = false;
    }
}

/// <summary>
/// Anzeige-Wrapper fuer ein <see cref="NotificationItem"/> mit lokalisierten Texten.
/// </summary>
public sealed class NotificationDisplayItem
{
    public NotificationItem Item { get; init; } = new();
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public string Icon { get; init; } = "Bell";
    public bool IsUnseen { get; init; }
    public bool ShowPulse { get; init; }
}
