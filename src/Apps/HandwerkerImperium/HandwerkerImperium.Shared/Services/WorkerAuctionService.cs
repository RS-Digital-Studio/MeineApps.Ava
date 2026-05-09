using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Worker-Markt-Auktionen (v2.1.0 Phase B Implementierung).
///
/// Firebase-Pfad: <c>guilds/{guildId}/auctions/{auctionId}</c>.
/// 30s Bidding-Phase auf S+-Tier-Worker. Solo-Spieler bieten gegen NPC-Bots,
/// Gilden-Spieler gegen Mitglieder. 1s-Cooldown gegen Spam-Bidding.
///
/// Phase-B-Status: Service-Logik komplett (PlaceBid, RefreshAuction, Settle, Refund).
/// Spawn-Cron (5min-Intervall) wird vom GuildTickService oder einem dedizierten
/// AuctionScheduler getrieben — nicht in diesem Service implementiert.
/// </summary>
public sealed class WorkerAuctionService : IWorkerAuctionService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly IGameIntegrityService _integrity;
    private readonly IWorkerService _workerService;

    private const string AuctionHmacSalt = "worker-auction-v1";

    /// <summary>1s-Cooldown gegen Spam-Bidding — pro Spieler.</summary>
    private DateTime _lastBidAt = DateTime.MinValue;

    public event Action<WorkerAuctionState>? AuctionUpdated;
    public event Action<WorkerAuctionState>? AuctionSettled;

    public WorkerAuctionState? CurrentAuction { get; private set; }

    public WorkerAuctionService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        IGameIntegrityService integrity,
        IWorkerService workerService)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _integrity = integrity;
        _workerService = workerService;
    }

    private string? CurrentGuildId => _gameStateService.State.GuildMembership?.GuildId;

    public async Task<bool> PlaceBidAsync(decimal amount)
    {
        var auction = CurrentAuction;
        if (auction == null || auction.Status != WorkerAuctionStatus.Active) return false;
        if (string.IsNullOrEmpty(_firebase.PlayerId) || string.IsNullOrEmpty(CurrentGuildId)) return false;
        if (DateTime.UtcNow > auction.EndsAt) return false;

        // 1s-Cooldown gegen Spam-Bidding.
        if ((DateTime.UtcNow - _lastBidAt).TotalSeconds < 1) return false;

        // Mindest-Erhoehung: 10% des Hoechstgebots, mindestens 100 EUR.
        var minBid = auction.HighestBid > 0
            ? Math.Ceiling(auction.HighestBid * 1.1m)
            : 100m;
        if (amount < minBid) return false;

        // Spieler muss das Geld haben.
        if (!_gameStateService.CanAfford(amount)) return false;

        // Lokales Geld-Locking: Nur das Delta zum bisherigen Eigen-Bid wird abgezogen.
        decimal myPrevious = auction.AllBids.TryGetValue(_firebase.PlayerId!, out var prev) ? prev : 0m;
        decimal delta = amount - myPrevious;
        if (!_gameStateService.TrySpendMoney(delta)) return false;

        auction.HighestBid = amount;
        auction.HighestBidderId = _firebase.PlayerId;
        auction.AllBids[_firebase.PlayerId!] = amount;
        auction.Hmac = ComputeHmac(auction);
        _lastBidAt = DateTime.UtcNow;

        var path = IWorkerAuctionService.GetFirebasePath(CurrentGuildId, auction.AuctionId);
        var ok = await _firebase.SetAsync(path, auction).ConfigureAwait(false);
        if (ok) AuctionUpdated?.Invoke(auction);
        return ok;
    }

    public async Task<WorkerAuctionState?> RefreshAuctionAsync()
    {
        if (string.IsNullOrEmpty(CurrentGuildId)) return null;

        // v2.1.0 Recovery: Wenn lokal noch keine Auktion bekannt ist (App-Restart-Szenario),
        // alle Auktionen der Gilde laden + ungeclaimte Refunds nachholen + aktive auswaehlen.
        if (CurrentAuction == null)
        {
            await DiscoverAndRecoverAsync().ConfigureAwait(false);
            if (CurrentAuction == null) return null;
        }

        var path = IWorkerAuctionService.GetFirebasePath(CurrentGuildId, CurrentAuction.AuctionId);
        var fresh = await _firebase.GetAsync<WorkerAuctionState>(path).ConfigureAwait(false);
        if (fresh == null) return null;

        // HMAC-Validierung — manipulierte Bids werden erkannt.
        var expected = ComputeHmac(fresh, useExisting: false);
        if (fresh.Hmac != expected) return CurrentAuction; // ignoriere manipulierten State

        var prevStatus = CurrentAuction.Status;
        CurrentAuction = fresh;
        AuctionUpdated?.Invoke(fresh);

        // Settle wenn die Auktions-Endzeit erreicht ist und Status noch Active war.
        if (fresh.Status == WorkerAuctionStatus.Active && DateTime.UtcNow > fresh.EndsAt)
        {
            await SettleAsync(fresh).ConfigureAwait(false);
        }
        else if (fresh.Status == WorkerAuctionStatus.Settled && prevStatus != WorkerAuctionStatus.Settled)
        {
            AuctionSettled?.Invoke(fresh);
            ApplyRefunds(fresh); // idempotent via ClaimedAuctionIds
        }
        return fresh;
    }

    /// <summary>
    /// v2.1.0 Recovery-Pfad: Liest alle Auktionen der Gilde, claimed ungeclaimte Settled-
    /// Refunds und setzt CurrentAuction auf die juengste aktive (oder die neueste Settled
    /// als Fallback). Wird beim ersten Refresh nach App-Start aufgerufen — kein Spieler
    /// soll Geld + Worker verlieren, weil seine App waehrend der Settlement-Transition
    /// geschlossen war.
    /// </summary>
    private async Task DiscoverAndRecoverAsync()
    {
        try
        {
            var all = await _firebase.GetAsync<Dictionary<string, WorkerAuctionState>>(
                $"guilds/{CurrentGuildId}/auctions").ConfigureAwait(false);
            if (all == null || all.Count == 0) return;

            WorkerAuctionState? activeAuction = null;
            var claimed = _gameStateService.State.ClaimedAuctionIds;
            foreach (var (id, state) in all)
            {
                if (state == null) continue;
                state.AuctionId = string.IsNullOrEmpty(state.AuctionId) ? id : state.AuctionId;

                // HMAC-Validierung — manipulierte Auktionen ignorieren.
                var expected = ComputeHmac(state, useExisting: false);
                if (state.Hmac != expected) continue;

                if (state.Status == WorkerAuctionStatus.Settled)
                {
                    if (!claimed.Contains(state.AuctionId)
                        && state.AllBids.ContainsKey(_firebase.PlayerId ?? ""))
                    {
                        ApplyRefunds(state); // idempotent — markiert sich selbst als geclaimt
                    }
                }
                else if (state.Status == WorkerAuctionStatus.Active && DateTime.UtcNow < state.EndsAt)
                {
                    activeAuction = state;
                }
            }
            CurrentAuction = activeAuction;
            if (activeAuction != null) AuctionUpdated?.Invoke(activeAuction);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auction] Recovery-Lesen fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Settled die Auktion — Hoechstbieter erhaelt den Worker, alle anderen Bieter bekommen
    /// ihr Geld zurueck. Wird auf der Client-Seite getriggert (besser: Server-Side, aber
    /// hier reicht Client-Konsens via HMAC).
    /// </summary>
    private async Task SettleAsync(WorkerAuctionState auction)
    {
        auction.Status = WorkerAuctionStatus.Settled;
        auction.Hmac = ComputeHmac(auction);

        var path = IWorkerAuctionService.GetFirebasePath(CurrentGuildId!, auction.AuctionId);
        await _firebase.SetAsync(path, auction).ConfigureAwait(false);
        AuctionSettled?.Invoke(auction);
        ApplyRefunds(auction);
    }

    /// <summary>
    /// Erstattet allen Bietern (ausser dem Gewinner) ihr Geld zurueck. Die Lock-Logik
    /// im PlaceBid hat bereits Geld vom Spieler abgezogen — wenn er nicht gewinnt,
    /// bekommt er es hier zurueck. Wenn der Spieler gewinnt, wird der Worker generiert
    /// und an den ersten passenden Workshop uebergeben (HireWorker).
    ///
    /// v2.1.0 Idempotenz: GameState.ClaimedAuctionIds verhindert Double-Pay nach App-Restart
    /// oder Polling-Race. Wenn die Auktion bereits geclaimed wurde, ist der Aufruf ein No-Op.
    /// </summary>
    private void ApplyRefunds(WorkerAuctionState auction)
    {
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return;
        if (!auction.AllBids.TryGetValue(_firebase.PlayerId, out var myBid)) return;

        var claimed = _gameStateService.State.ClaimedAuctionIds;
        if (claimed.Contains(auction.AuctionId)) return; // bereits geclaimed (App-Restart, Doppel-Polling)

        if (auction.HighestBidderId == _firebase.PlayerId)
        {
            // v2.1.0: Gewinner — Worker generieren und in einen Workshop einstellen.
            // Strategie: ersten freigeschalteten Workshop ohne volle Slot-Belegung waehlen.
            // Wenn keiner frei ist, kommt der Worker in den Carpenter (Default-Start-Workshop).
            try
            {
                var newWorker = Worker.CreateForTier(auction.WorkerTier);
                newWorker.Name = string.IsNullOrEmpty(auction.WorkerName) ? newWorker.Name : auction.WorkerName;
                var targetWorkshop = PickWorkshopForAuctionWinner();
                _workerService.HireWorker(newWorker, targetWorkshop);
                claimed.Add(auction.AuctionId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Auction] Sieg-Worker konnte nicht eingestellt werden: {ex.Message}");
            }
            return;
        }

        // Verlierer — Geld zurueck.
        if (myBid > 0)
        {
            _gameStateService.AddMoney(myBid);
            claimed.Add(auction.AuctionId);
        }
    }

    /// <summary>
    /// Auktions-Sieger-Workshop-Auswahl: ersten freigeschalteten Workshop nehmen.
    /// Bewusst simpel — der Spieler kann den Worker im Worker-Profil per TransferWorker
    /// zu einem anderen Workshop verschieben.
    /// </summary>
    private WorkshopType PickWorkshopForAuctionWinner()
    {
        var state = _gameStateService.State;
        foreach (var ws in state.Workshops)
        {
            if (ws.IsUnlocked) return ws.Type;
        }
        return WorkshopType.Carpenter;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // v2.1.0 Phase B: Spawn-Cron + NPC-Bot-Bidding (Master-Client-Pattern)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Default Auktions-Dauer (30s active-Phase). Bei Bedarf konfigurierbar.</summary>
    private static readonly TimeSpan AuctionDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Master-Client-Pattern: Pruefe ob *dieser* Client der Master ist. Master = Spieler mit
    /// lexikografisch kleinster PlayerId in der Mitgliederliste. Deterministisch ohne Server-Logik.
    /// In Solo-Gilden ist man immer Master.
    /// </summary>
    private async Task<bool> IsMasterClientAsync()
    {
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return false;
        if (string.IsNullOrEmpty(CurrentGuildId)) return false;
        try
        {
            var members = await _firebase.GetAsync<Dictionary<string, FirebaseGuildMember>>(
                $"guild_members/{CurrentGuildId}").ConfigureAwait(false);
            if (members == null || members.Count == 0) return true; // Solo-Master
            string smallestId = _firebase.PlayerId!;
            foreach (var key in members.Keys)
            {
                if (string.CompareOrdinal(key, smallestId) < 0) smallestId = key;
            }
            return smallestId == _firebase.PlayerId;
        }
        catch
        {
            // Bei Firebase-Fehler keinen Spawn riskieren — kein Master.
            return false;
        }
    }

    public async Task<bool> SpawnAuctionIfMasterAsync()
    {
        if (string.IsNullOrEmpty(CurrentGuildId)) return false;
        // Bestehende aktive Auktion: kein neuer Spawn.
        if (CurrentAuction != null && CurrentAuction.Status != WorkerAuctionStatus.Settled)
            return false;
        if (!await IsMasterClientAsync().ConfigureAwait(false)) return false;

        // S/SS/SSS-Tier-Worker generieren — gewichtet (S 70%, SS 25%, SSS 5%).
        var rng = new Random();
        int roll = rng.Next(0, 100);
        WorkerTier tier = roll < 70 ? WorkerTier.S : roll < 95 ? WorkerTier.SS : WorkerTier.SSS;

        var auction = new WorkerAuctionState
        {
            AuctionId = $"auc_{DateTime.UtcNow:yyyyMMddHHmmss}_{rng.Next(1000, 9999)}",
            WorkerTier = tier,
            WorkerName = GenerateWorkerName(rng),
            Status = WorkerAuctionStatus.Active,
            EndsAt = DateTime.UtcNow.Add(AuctionDuration),
            HighestBid = 0m,
            HighestBidderId = null,
            AllBids = new Dictionary<string, decimal>()
        };
        auction.Hmac = ComputeHmac(auction);

        var path = IWorkerAuctionService.GetFirebasePath(CurrentGuildId, auction.AuctionId);
        var ok = await _firebase.SetAsync(path, auction).ConfigureAwait(false);
        if (ok)
        {
            CurrentAuction = auction;
            AuctionUpdated?.Invoke(auction);
        }
        return ok;
    }

    public async Task RunNpcBotTickAsync()
    {
        var auction = CurrentAuction;
        if (auction == null || auction.Status != WorkerAuctionStatus.Active) return;
        if (string.IsNullOrEmpty(CurrentGuildId)) return;
        // Nur Master simuliert die Bots — andere Clients lesen das Resultat ueber Polling.
        if (!await IsMasterClientAsync().ConfigureAwait(false)) return;

        var rng = new Random();
        // 35% Chance pro Tick — fuehrt zu mehreren Bot-Bids in einer 30s-Auktion.
        if (rng.Next(0, 100) >= 35) return;

        // Mindestgebot bestimmen.
        decimal currentMin = auction.HighestBid > 0
            ? Math.Ceiling(auction.HighestBid * 1.1m)
            : 100m;

        // Bot-ID waehlen (1-3 Bots, deterministisch pro Auktion). Damit AllBids konsistent bleibt.
        int botCount = 1 + (Math.Abs(auction.AuctionId.GetHashCode()) % 3);
        int botIndex = rng.Next(0, botCount);
        string botId = $"npc_bot_{auction.AuctionId}_{botIndex}";

        // Bid: 5-25% ueber dem aktuellen Min, mit Tier-Skalierung.
        decimal bidIncrement = currentMin * (decimal)(0.05 + rng.NextDouble() * 0.20);
        decimal bid = currentMin + Math.Round(bidIncrement, 2);
        // Bot-Maximum: ungefaehr Tier-Wert × 2 (Bots geben nicht unbegrenzt).
        decimal botMax = auction.WorkerTier switch
        {
            WorkerTier.S => 50_000m,
            WorkerTier.SS => 250_000m,
            WorkerTier.SSS => 1_000_000m,
            _ => 25_000m
        };
        if (bid > botMax) return;

        auction.HighestBid = bid;
        auction.HighestBidderId = botId;
        auction.AllBids[botId] = bid;
        auction.Hmac = ComputeHmac(auction);

        var path = IWorkerAuctionService.GetFirebasePath(CurrentGuildId, auction.AuctionId);
        var ok = await _firebase.SetAsync(path, auction).ConfigureAwait(false);
        if (ok) AuctionUpdated?.Invoke(auction);
    }

    /// <summary>Erzeugt einen zufaelligen Worker-Namen aus festen Listen (deterministisch genug).</summary>
    private static string GenerateWorkerName(Random rng)
    {
        string[] firstNames = { "Max", "Anna", "Tom", "Lisa", "Felix", "Marie", "Paul", "Sophie", "Leon", "Emma" };
        string[] lastNames = { "Schmidt", "Mueller", "Weber", "Becker", "Hoffmann", "Fischer", "Wagner", "Koch", "Bauer", "Klein" };
        return $"{firstNames[rng.Next(firstNames.Length)]} {lastNames[rng.Next(lastNames.Length)]}";
    }

    /// <summary>HMAC ueber AuctionId + Bid-State — verhindert Bid-Manipulation in Firebase.</summary>
    private string ComputeHmac(WorkerAuctionState state, bool useExisting = true)
    {
        var oldHmac = state.Hmac;
        if (!useExisting) state.Hmac = null;

        // Sortierte AllBids fuer deterministische Signatur (Dictionary-Iteration ist nicht stabil).
        var bidsRaw = string.Join(",", state.AllBids
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value:F2}"));

        var raw = $"{AuctionHmacSalt}|{state.AuctionId}|{state.WorkerTier}|{state.WorkerName}|"
                  + $"{state.Status}|{state.HighestBidderId}|{state.HighestBid:F2}|{bidsRaw}";
        state.Hmac = oldHmac;
        return _integrity.ComputeStringHmac(raw);
    }
}
