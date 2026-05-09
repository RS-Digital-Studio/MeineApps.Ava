using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels.MiniGames;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>
/// ViewModel fuer die Co-op-Auftraege-Liste in der GuildView (v2.1.0).
/// Polling-Loop: 2s waehrend offene Auftraege existieren, sonst Timer aus.
/// </summary>
public sealed partial class GuildCoopOrderViewModel : ViewModelBase, IDisposable
{
    private readonly IGuildCoopOrderService _coopService;
    private readonly IFirebaseService _firebaseService;
    private readonly DispatcherTimer _pollingTimer;
    private bool _disposed;

    /// <summary>
    /// v2.1.0: Wird gefeuert wenn ein Co-op-Auftrag akzeptiert/erstellt wurde und der
    /// Spieler ins MiniGame navigieren soll. GuildViewModel forwardet das an den
    /// NavigationService.
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>Offene Co-op-Auftraege fuer den Spieler (eingeladen oder erstellt).</summary>
    public ObservableCollection<CoopOrderState> OpenOrders { get; } = new();

    /// <summary>
    /// v2.1.0 Player-Picker: Lieferant fuer waehlbare Gilden-Mitglieder.
    /// Wird vom GuildViewModel beim Init verdrahtet (vermeidet Circular-DI).
    /// Liefert (PlayerId, Name)-Paare ohne den eigenen Spieler.
    /// </summary>
    public Func<IReadOnlyList<(string PlayerId, string Name)>>? AvailableMembersProvider { get; set; }

    /// <summary>v2.1.0: Mitglieder-Picker-Liste (gefuellt beim OpenPicker).</summary>
    public ObservableCollection<CoopMemberPickItem> PickerCandidates { get; } = new();

    /// <summary>True wenn der Picker-Dialog sichtbar ist.</summary>
    [ObservableProperty]
    private bool _isPickerOpen;

    /// <summary>True wenn das Polling laeuft (UI-Indicator).</summary>
    [ObservableProperty]
    private bool _isPolling;

    /// <summary>Anzeige: Anzahl offener Auftraege als Badge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(HasOrders))]
    private int _openOrderCount;

    public string BadgeText => OpenOrderCount > 9 ? "9+" : OpenOrderCount.ToString();
    public bool HasOrders => OpenOrderCount > 0;

    public GuildCoopOrderViewModel(IGuildCoopOrderService coopService, IFirebaseService firebaseService)
    {
        _coopService = coopService;
        _firebaseService = firebaseService;
        _coopService.CoopOrderUpdated += OnCoopOrderUpdated;

        _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollingTimer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// v2.1.0: Setzt die statischen Co-op-Felder im BaseMiniGameViewModel und feuert
    /// NavigationRequested mit der MiniGame-Route. Wird von Create/Accept-Flow genutzt.
    /// </summary>
    private void StartCoopMiniGame(CoopOrderState state)
    {
        BaseMiniGameViewModel.ActiveCoopOrderId = state.OrderId;
        BaseMiniGameViewModel.ActiveCoopIsPlayer1 = state.CreatedBy == _firebaseService.PlayerId;
        var route = state.MiniGameType.GetRoute();
        NavigationRequested?.Invoke(route);
    }

    /// <summary>v2.1.0: Oeffnet den Picker-Dialog mit allen Gilden-Mitgliedern (ausser sich selbst).</summary>
    [RelayCommand]
    private void OpenPicker()
    {
        PickerCandidates.Clear();
        var provider = AvailableMembersProvider;
        if (provider == null) return;
        foreach (var (id, name) in provider())
            PickerCandidates.Add(new CoopMemberPickItem(id, name));
        IsPickerOpen = PickerCandidates.Count > 0;
    }

    /// <summary>v2.1.0: Schliesst den Picker-Dialog ohne Auswahl.</summary>
    [RelayCommand]
    private void ClosePicker() => IsPickerOpen = false;

    /// <summary>v2.1.0: Mitglied ausgewaehlt — Co-op-Einladung erstellen + Picker schliessen.</summary>
    [RelayCommand]
    private async Task PickMemberAsync(CoopMemberPickItem? member)
    {
        if (member == null || string.IsNullOrEmpty(member.PlayerId)) return;
        IsPickerOpen = false;
        await CreateInviteAsync(member.PlayerId).ConfigureAwait(true);
    }

    /// <summary>Startet das Polling — wird von der GuildView beim Sichtbar-Werden aufgerufen.</summary>
    public void StartPolling()
    {
        if (IsPolling) return;
        _pollingTimer.Start();
        IsPolling = true;
        // Initial-Refresh damit User nicht 2s wartet.
        _ = RefreshAsync();
    }

    /// <summary>Stoppt das Polling — wird beim Verlassen der View aufgerufen.</summary>
    public void StopPolling()
    {
        if (!IsPolling) return;
        _pollingTimer.Stop();
        IsPolling = false;
    }

    /// <summary>Manueller Refresh (Pull-to-refresh oder Initial-Load).</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var open = await _coopService.GetOpenForPlayerAsync().ConfigureAwait(true);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OpenOrders.Clear();
                foreach (var order in open) OpenOrders.Add(order);
                OpenOrderCount = open.Count;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CoopOrderVM] Refresh-Fehler: {ex.Message}");
        }
    }

    /// <summary>Erstellt eine neue Co-op-Einladung an den ausgewaehlten Mitspieler.</summary>
    [RelayCommand]
    public async Task CreateInviteAsync(string invitedPlayerId)
    {
        if (string.IsNullOrEmpty(invitedPlayerId)) return;
        var state = await _coopService.CreateInviteAsync(invitedPlayerId).ConfigureAwait(true);
        if (state != null)
        {
            await RefreshAsync().ConfigureAwait(true);
            // v2.1.0: Initiator (Player1) startet bereits jetzt — wartet aber im MiniGame
            // bis der Eingeladene die Einladung annimmt (Co-op-Service rechnet erst beim
            // 2. Score ab). Player1 sieht den eigenen Score-Submit als Halbfortschritt.
            StartCoopMiniGame(state);
        }
    }

    /// <summary>Eingeladener Spieler nimmt den Auftrag an.</summary>
    [RelayCommand]
    public async Task AcceptAsync(CoopOrderState? order)
    {
        if (order == null) return;
        var ok = await _coopService.AcceptAsync(order.OrderId).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        if (ok)
        {
            // v2.1.0: Annahme erfolgreich → ins MiniGame springen mit gesetzter ActiveCoopOrderId.
            StartCoopMiniGame(order);
        }
    }

    /// <summary>Eingeladener Spieler lehnt ab.</summary>
    [RelayCommand]
    public async Task DeclineAsync(CoopOrderState? order)
    {
        if (order == null) return;
        await _coopService.DeclineAsync(order.OrderId).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private void OnCoopOrderUpdated(CoopOrderState state)
    {
        // Service feuert nach jeder Schreib-Aktion — UI-Refresh anstoßen.
        Dispatcher.UIThread.Post(() => _ = RefreshAsync());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollingTimer.Stop();
        _coopService.CoopOrderUpdated -= OnCoopOrderUpdated;
    }
}

/// <summary>
/// v2.1.0: Anzeige-Item fuer den Co-op-Member-Picker (eindeutige PlayerId + Anzeige-Name).
/// </summary>
public sealed record CoopMemberPickItem(string PlayerId, string Name);
