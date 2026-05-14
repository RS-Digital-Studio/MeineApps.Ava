using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Co-op-Auftraege zwischen zwei Gildenmitgliedern (v2.1.0 Phase B Implementierung).
///
/// Firebase-Pfad: <c>guilds/{guildId}/coopOrders/{orderId}</c>.
/// 5min Annahme-Frist (Pending), dann Active-Phase mit MiniGame-Sync via Polling.
/// Reward-Split: 50/50 Default, +25% Bonus bei beidseitig Perfect (Score >= 95).
///
/// Phase-B-Status: Service-Logik mit Firebase-CRUD + Reward-Berechnung. UI in GuildView
/// und Polling-Loop sind separate Tasks (UI braucht eine eigene CoopOrderViewModel,
/// Polling-Loop kann via DispatcherTimer aus dem ViewModel laufen).
/// </summary>
public sealed class GuildCoopOrderService : IGuildCoopOrderService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly IGameIntegrityService _integrity;

    /// <summary>HMAC-Salt-Praefix fuer die Score-Signatur (verhindert Score-Tampering).</summary>
    private const string CoopHmacSalt = "coop-order-v1";

    public event Action<CoopOrderState>? CoopOrderUpdated;

    public GuildCoopOrderService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        IGameIntegrityService integrity)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _integrity = integrity;
    }

    private string? CurrentGuildId => _gameStateService.State.GuildMembership?.GuildId;

    public async Task<CoopOrderState?> CreateInviteAsync(string invitedPlayerId, MiniGameType miniGameType)
    {
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return null;
        if (string.IsNullOrEmpty(CurrentGuildId)) return null;
        if (string.IsNullOrEmpty(invitedPlayerId) || invitedPlayerId == _firebase.PlayerId) return null;

        // OrderId = GUID, Pfad = guilds/{id}/coopOrders/{orderId}
        var orderId = Guid.NewGuid().ToString("N");
        var state = new CoopOrderState
        {
            OrderId = orderId,
            CreatedBy = _firebase.PlayerId!,
            InvitedPlayer = invitedPlayerId,
            Status = CoopOrderStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            // Mini-Game-Typ kommt jetzt vom Aufrufer — frueher hier hart Sawing,
            // wodurch JEDER Co-op-Auftrag fuer alle Spieler immer Sawing war.
            MiniGameType = miniGameType,
            BaseReward = 100_000m,
            RewardSplit = 0.5
        };
        state.Hmac = ComputeHmac(state);

        var path = IGuildCoopOrderService.GetFirebasePath(CurrentGuildId, orderId);
        var success = await _firebase.SetAsync(path, state).ConfigureAwait(false);
        if (!success) return null;

        CoopOrderUpdated?.Invoke(state);
        return state;
    }

    public async Task<bool> AcceptAsync(string orderId)
    {
        var state = await GetStateAsync(orderId).ConfigureAwait(false);
        if (state == null || state.Status != CoopOrderStatus.Pending) return false;
        if (state.InvitedPlayer != _firebase.PlayerId) return false;
        if (DateTime.UtcNow > state.ExpiresAt) return false;

        state.Status = CoopOrderStatus.Active;
        state.ExpiresAt = DateTime.UtcNow.AddMinutes(3); // 3min MiniGame-Window
        state.Hmac = ComputeHmac(state);

        var path = IGuildCoopOrderService.GetFirebasePath(CurrentGuildId!, orderId);
        var ok = await _firebase.SetAsync(path, state).ConfigureAwait(false);
        if (ok) CoopOrderUpdated?.Invoke(state);
        return ok;
    }

    public async Task<bool> DeclineAsync(string orderId)
    {
        var state = await GetStateAsync(orderId).ConfigureAwait(false);
        if (state == null) return false;

        state.Status = CoopOrderStatus.Expired;
        state.Hmac = ComputeHmac(state);

        var path = IGuildCoopOrderService.GetFirebasePath(CurrentGuildId!, orderId);
        var ok = await _firebase.SetAsync(path, state).ConfigureAwait(false);
        if (ok) CoopOrderUpdated?.Invoke(state);
        return ok;
    }

    public async Task<bool> SubmitScoreAsync(string orderId, int score, bool isPlayer1)
    {
        // v2.1.0 RACE-FIX: Statt SetAsync (PUT) wird der eigene Score-Slot per PATCH
        // (UpdateAsync) atomar gesetzt — verhindert dass zwei gleichzeitig submittete
        // Scores einander ueberschreiben. Frueher: Player A liest Player2Score=null,
        // Player B liest Player1Score=null, beide schreiben PUT mit ihrer Score-Sicht
        // → der zweite ueberschreibt den Score des ersten. Mit PATCH werden nur die
        // expliziten Felder ersetzt, die anderen bleiben in Firebase erhalten.

        var state = await GetStateAsync(orderId).ConfigureAwait(false);
        if (state == null || state.Status != CoopOrderStatus.Active) return false;
        if (DateTime.UtcNow > state.ExpiresAt) return false;

        // Score-Plausibilitaet: 0-100, harter Cap. Firebase-Rules validate ebenfalls.
        score = Math.Clamp(score, 0, 100);

        var path = IGuildCoopOrderService.GetFirebasePath(CurrentGuildId!, orderId);

        // PATCH 1: nur das eigene Score-Feld setzen.
        var scoreFieldName = isPlayer1 ? "player1Score" : "player2Score";
        var scorePatch = new Dictionary<string, object> { [scoreFieldName] = score };
        var ok = await _firebase.UpdateAsync(path, scorePatch).ConfigureAwait(false);
        if (!ok) return false;

        // Re-Read um zu sehen ob der andere Spieler schon submitted hat. Da der HMAC nur
        // ueber stabile Felder geht, bleibt er nach dem PATCH valide.
        var fresh = await GetStateAsync(orderId).ConfigureAwait(false);
        if (fresh == null) return false;

        // PATCH 2 (idempotent): Wenn beide Scores da sind und Status noch Active, auf
        // Completed setzen. Beide Clients koennen das parallel patchen — Last-Write-Wins
        // mit identischem Wert ist OK.
        if (fresh.Player1Score.HasValue && fresh.Player2Score.HasValue
            && fresh.Status == CoopOrderStatus.Active)
        {
            var statusPatch = new Dictionary<string, object>
            {
                ["status"] = (int)CoopOrderStatus.Completed
            };
            await _firebase.UpdateAsync(path, statusPatch).ConfigureAwait(false);
            fresh.Status = CoopOrderStatus.Completed;
        }

        // Reward-Auszahlung POLLING-BASIERT auf BEIDEN Clients via TryClaimCompletedRewardAsync
        // (idempotent ueber GameState.ClaimedCoopOrderIds + Server-Marker claimedBy).
        if (fresh.Status == CoopOrderStatus.Completed)
            await TryClaimCompletedRewardAsync(fresh).ConfigureAwait(false);

        CoopOrderUpdated?.Invoke(fresh);
        return true;
    }

    /// <summary>
    /// v2.1.0/FB-C01: Idempotente Reward-Auszahlung. Wird auf JEDEM Client beim Polling/SubmitScore
    /// aufgerufen. Doppel-Pay wird zweifach verhindert: lokal ueber GameState.ClaimedCoopOrderIds
    /// (Schnell-Check) UND serverseitig ueber den Write-once-Marker <c>claimedBy/{playerId}</c> —
    /// letzterer schuetzt auch ueber Geraetewechsel / Cloud-Restore aelterer Saves hinweg.
    /// </summary>
    private async Task TryClaimCompletedRewardAsync(CoopOrderState state)
    {
        if (state.Status != CoopOrderStatus.Completed) return;
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return;
        if (string.IsNullOrEmpty(CurrentGuildId)) return;
        bool isPlayer1 = state.CreatedBy == _firebase.PlayerId;
        bool isPlayer2 = state.InvitedPlayer == _firebase.PlayerId;
        if (!isPlayer1 && !isPlayer2) return;

        var claimed = _gameStateService.State.ClaimedCoopOrderIds;
        if (claimed.Contains(state.OrderId)) return; // lokaler Schnell-Check

        // Server-seitiger Write-once-Claim-Marker. Die Rule erlaubt den
        // Write nur, wenn claimedBy/{playerId} noch nicht existiert. Verhindert Doppelauszahlung
        // beim Geraetewechsel, weil die lokale ClaimedCoopOrderIds-Liste durch Cloud-Restore
        // eines aelteren Saves verlorengehen kann.
        var claimPath = $"{IGuildCoopOrderService.GetFirebasePath(CurrentGuildId, state.OrderId)}/claimedBy";
        var claimPatch = new Dictionary<string, object> { [_firebase.PlayerId!] = true };
        var claimOk = await _firebase.UpdateAsync(claimPath, claimPatch).ConfigureAwait(false);
        if (!claimOk)
        {
            // Write abgelehnt ODER Netzwerkfehler. Pruefen welcher Fall: existiert der Marker
            // schon auf dem Server? Dann wurde bereits ausgezahlt → lokal nachziehen.
            // Bei Netzwerkfehler (Marker fehlt) NICHT markieren → naechstes Polling versucht erneut.
            var existing = await _firebase.GetAsync<Dictionary<string, object>>(claimPath).ConfigureAwait(false);
            if (existing != null && existing.ContainsKey(_firebase.PlayerId!))
                claimed.Add(state.OrderId);
            return;
        }

        bool bothPerfect = state.Player1Score >= 95 && state.Player2Score >= 95;
        decimal multiplier = bothPerfect ? 1.25m : 1.0m;
        decimal myShare = state.BaseReward * (decimal)state.RewardSplit * multiplier;
        if (myShare > 0)
        {
            _gameStateService.AddMoney(myShare);
            claimed.Add(state.OrderId);
        }
    }

    public async Task<CoopOrderState?> GetStateAsync(string orderId)
    {
        if (string.IsNullOrEmpty(CurrentGuildId)) return null;
        var path = IGuildCoopOrderService.GetFirebasePath(CurrentGuildId, orderId);
        var state = await _firebase.GetAsync<CoopOrderState>(path).ConfigureAwait(false);
        if (state == null) return null;

        // HMAC-Validierung — ein manipuliertes Score-Feld faellt hier auf.
        var expected = ComputeHmac(state, useExisting: false);
        if (state.Hmac != expected)
        {
            // Tampering erkannt — als Expired markieren.
            state.Status = CoopOrderStatus.Expired;
        }
        return state;
    }

    public async Task<IReadOnlyList<CoopOrderState>> GetOpenForPlayerAsync()
    {
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return Array.Empty<CoopOrderState>();
        if (string.IsNullOrEmpty(CurrentGuildId)) return Array.Empty<CoopOrderState>();

        // Firebase-GET liefert Dictionary<string, CoopOrderState> aller Auftraege der Gilde.
        var rootPath = $"guilds/{CurrentGuildId}/coopOrders";
        var all = await _firebase.GetAsync<Dictionary<string, CoopOrderState>>(rootPath).ConfigureAwait(false);
        if (all == null) return Array.Empty<CoopOrderState>();

        var result = new List<CoopOrderState>();
        foreach (var kvp in all)
        {
            var state = kvp.Value;
            if (state == null) continue;
            if (state.CreatedBy != _firebase.PlayerId && state.InvitedPlayer != _firebase.PlayerId) continue;

            // v2.1.0: Completed-Auftraege durchlaufen einen Reward-Claim-Pass
            // (idempotent ueber GameState.ClaimedCoopOrderIds + Server-Marker claimedBy).
            // Erster Submitter holt hier seinen Anteil, der sonst verloren ginge.
            if (state.Status == CoopOrderStatus.Completed)
            {
                await TryClaimCompletedRewardAsync(state).ConfigureAwait(false);
                continue; // nicht in OpenOrders anzeigen
            }
            if (state.Status == CoopOrderStatus.Expired) continue;
            result.Add(state);
        }
        return result;
    }

    /// <summary>
    /// HMAC ueber die STABILEN Felder — verhindert Tampering der Auftrags-Identitaet
    /// (OrderId, Teilnehmer, BaseReward). Score/Status sind NICHT im HMAC, weil sie
    /// inkrementell via PATCH aktualisiert werden (Race-Condition-Fix v2.1.0).
    /// Score-Wertebereich ist via Firebase-Rules begrenzt (0-100, validate). Status-Tampering
    /// wuerde nichts bewirken weil TryClaimCompletedReward beide Scores != null braucht.
    /// </summary>
    private string ComputeHmac(CoopOrderState state, bool useExisting = true)
    {
        var oldHmac = state.Hmac;
        if (!useExisting) state.Hmac = null;

        var raw = $"{CoopHmacSalt}|{state.OrderId}|{state.CreatedBy}|{state.InvitedPlayer}|"
                  + $"{state.BaseReward}|{state.MiniGameType}";
        state.Hmac = oldHmac;
        return _integrity.ComputeStringHmac(raw);
    }
}
