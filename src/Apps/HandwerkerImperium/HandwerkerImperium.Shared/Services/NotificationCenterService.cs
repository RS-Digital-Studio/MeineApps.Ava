using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Notification-Center-Implementierung. Persistiert Items in
/// <see cref="GameState.NotificationInbox"/> und feuert <see cref="Changed"/>
/// bei jeder Aenderung.
///
/// es (K4 + NC2 + NC4 + P1):
/// - Mutationen laufen unter <see cref="IGameStateService.ExecuteWithLock"/>
///   (gleicher Lock wie SaveGameService) — verhindert „Collection was modified"
///   beim JSON-Serialize wenn AutoSave und Add/Dismiss kollidieren.
/// - Inbox-Cap = 100 Items: aelteste werden bei Ueberlauf evicted (kumulative
///   Datenverlust-Vermeidung gegen unbegrenztes Wachstum).
/// - Items-Get cacht die sortierte Liste — vermeidet O(n log n)+Allokation
///   pro UI-Frame bei offener Bell (~60 Allokationen/s bei 30fps zuvor).
/// </summary>
public sealed class NotificationCenterService : INotificationCenterService
{
    private const int MaxInboxSize = 100;

    private readonly IGameStateService _gameStateService;
    private readonly object _cacheLock = new();
    private List<NotificationItem>? _sortedCache;
    private bool _isCacheDirty = true;

    public event Action? Changed;

    public NotificationCenterService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    /// <summary>
    /// Sortierte Snapshot-Liste — neueste zuerst. Cached bis zur naechsten
    /// Mutation (Add/Dismiss/Clear/MarkAllSeen).
    /// </summary>
    public IReadOnlyList<NotificationItem> Items
    {
        get
        {
            lock (_cacheLock)
            {
                if (_sortedCache == null || _isCacheDirty)
                {
                    // Snapshot unter Lock damit kein Mutator den Sort durcheinanderbringt.
                    var sorted = _gameStateService.ExecuteWithLock(() =>
                        _gameStateService.State.NotificationInbox
                            .OrderByDescending(i => i.CreatedAt)
                            .ToList());
                    _sortedCache = sorted;
                    _isCacheDirty = false;
                }
                return _sortedCache;
            }
        }
    }

    public int UnseenCount
    {
        get
        {
            return _gameStateService.ExecuteWithLock(() =>
                _gameStateService.State.NotificationInbox.Count(i => !i.Seen));
        }
    }

    public void Add(NotificationItem item)
    {
        if (string.IsNullOrEmpty(item.Id)) return;

        _gameStateService.ExecuteWithLock(() =>
        {
            var inbox = _gameStateService.State.NotificationInbox;
            var existing = inbox.FirstOrDefault(i => i.Id == item.Id);
            if (existing != null)
            {
                // Update vorhandenes Item, aber behalte Seen-Flag.
                existing.Kind = item.Kind;
                existing.TitleKey = item.TitleKey;
                existing.TitleArg = item.TitleArg;
                existing.BodyKey = item.BodyKey;
                existing.BodyArg = item.BodyArg;
                existing.IconKind = item.IconKind;
                existing.CreatedAt = item.CreatedAt;
            }
            else
            {
                inbox.Add(item);
                // Cap: aelteste Items evicten (FIFO ueber CreatedAt).
                while (inbox.Count > MaxInboxSize)
                {
                    var oldest = inbox.OrderBy(i => i.CreatedAt).First();
                    inbox.Remove(oldest);
                }
            }
        });

        InvalidateCache();
        Changed?.Invoke();
    }

    public void Dismiss(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        bool removed = _gameStateService.ExecuteWithLock(() =>
            _gameStateService.State.NotificationInbox.RemoveAll(i => i.Id == id) > 0);

        if (removed)
        {
            InvalidateCache();
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        bool hadItems = _gameStateService.ExecuteWithLock(() =>
        {
            var inbox = _gameStateService.State.NotificationInbox;
            var had = inbox.Count > 0;
            inbox.Clear();
            return had;
        });

        if (hadItems)
        {
            InvalidateCache();
            Changed?.Invoke();
        }
    }

    public void MarkAllSeen()
    {
        bool changed = _gameStateService.ExecuteWithLock(() =>
        {
            bool any = false;
            foreach (var item in _gameStateService.State.NotificationInbox)
            {
                if (!item.Seen)
                {
                    item.Seen = true;
                    any = true;
                }
            }
            return any;
        });

        if (changed)
        {
            InvalidateCache();
            Changed?.Invoke();
        }
    }

    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return _gameStateService.ExecuteWithLock(() =>
            _gameStateService.State.NotificationInbox.Any(i => i.Id == id));
    }

    private void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _isCacheDirty = true;
        }
    }
}
