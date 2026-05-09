using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Auctions;

/// <summary>
/// ViewModel fuer die Worker-Markt-Auktion (v2.1.0).
/// Polling-Loop: 1s waehrend Auktion aktiv, sonst Timer aus.
/// Bid-Eingabe-Validation + Countdown-Display.
/// </summary>
public sealed partial class WorkerAuctionViewModel : ViewModelBase, IDisposable
{
    private readonly IWorkerAuctionService _auctionService;
    private readonly DispatcherTimer _pollingTimer;
    private readonly DispatcherTimer _countdownTimer;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAuction))]
    [NotifyPropertyChangedFor(nameof(WorkerNameDisplay))]
    [NotifyPropertyChangedFor(nameof(HighestBidDisplay))]
    [NotifyPropertyChangedFor(nameof(MinBidDisplay))]
    private WorkerAuctionState? _currentAuction;

    [ObservableProperty]
    private string _bidInput = "";

    [ObservableProperty]
    private string _countdownText = "";

    [ObservableProperty]
    private string _bidError = "";

    public bool HasActiveAuction => CurrentAuction != null && CurrentAuction.Status == WorkerAuctionStatus.Active;

    public string WorkerNameDisplay => CurrentAuction?.WorkerName ?? "";
    public string HighestBidDisplay => CurrentAuction?.HighestBid.ToString("N0") ?? "0";
    public string MinBidDisplay
    {
        get
        {
            var ca = CurrentAuction;
            if (ca == null) return "0";
            decimal min = ca.HighestBid > 0 ? Math.Ceiling(ca.HighestBid * 1.1m) : 100m;
            return min.ToString("N0");
        }
    }

    public WorkerAuctionViewModel(IWorkerAuctionService auctionService)
    {
        _auctionService = auctionService;
        _auctionService.AuctionUpdated += OnAuctionUpdated;
        _auctionService.AuctionSettled += OnAuctionSettled;

        _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pollingTimer.Tick += async (_, _) => await PollAsync().ConfigureAwait(false);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => UpdateCountdown();
    }

    public void StartPolling()
    {
        _pollingTimer.Start();
        _countdownTimer.Start();
        _ = PollAsync();
    }

    public void StopPolling()
    {
        _pollingTimer.Stop();
        _countdownTimer.Stop();
    }

    private async Task PollAsync()
    {
        try
        {
            var fresh = await _auctionService.RefreshAuctionAsync().ConfigureAwait(true);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentAuction = fresh;
                UpdateCountdown();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuctionVM] Poll-Fehler: {ex.Message}");
        }
    }

    private void UpdateCountdown()
    {
        var ca = CurrentAuction;
        if (ca == null || ca.Status != WorkerAuctionStatus.Active)
        {
            CountdownText = "";
            return;
        }
        var remaining = ca.EndsAt - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        CountdownText = remaining.TotalSeconds < 60
            ? $"{(int)remaining.TotalSeconds}s"
            : $"{(int)remaining.TotalMinutes}m {remaining.Seconds:D2}s";
    }

    [RelayCommand]
    private async Task PlaceBidAsync()
    {
        BidError = "";
        if (!decimal.TryParse(BidInput, out var amount) || amount <= 0)
        {
            BidError = "Ungueltiger Bid-Betrag";
            return;
        }
        var ok = await _auctionService.PlaceBidAsync(amount).ConfigureAwait(true);
        if (!ok)
        {
            BidError = "Bid abgelehnt (zu niedrig, kein Geld, oder Cooldown).";
        }
        else
        {
            BidInput = "";
            await PollAsync().ConfigureAwait(true);
        }
    }

    private void OnAuctionUpdated(WorkerAuctionState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentAuction = state;
            UpdateCountdown();
        });
    }

    private void OnAuctionSettled(WorkerAuctionState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentAuction = state;
            CountdownText = "Beendet";
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollingTimer.Stop();
        _countdownTimer.Stop();
        _auctionService.AuctionUpdated -= OnAuctionUpdated;
        _auctionService.AuctionSettled -= OnAuctionSettled;
    }
}
