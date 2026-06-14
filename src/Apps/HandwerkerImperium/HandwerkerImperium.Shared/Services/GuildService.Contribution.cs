using System.Globalization;
using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services;

/// <summary>
/// GuildService — Wochenziel-Beitrag + wöchentliches Reset/Reward.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildService
{
    // ═══════════════════════════════════════════════════════════════════════
    // CONTRIBUTE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<bool> ContributeAsync(decimal amount)
    {
        var uid = _firebaseService.PlayerId;
        if (string.IsNullOrEmpty(uid)) return false;

        var state = _gameStateService.State;
        var membership = state.GuildMembership;
        if (membership == null || amount <= 0) return false;

        // Integritaetspruefung: Manipulierte Werte nicht an Firebase senden
        if (!VerifyIntegrityForFirebase(state)) return false;

        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            return false; // Timeout: Lock nicht erhalten
        try
        {
            // Wöchentliches Spenden-Cap prüfen (max 30% des Wochenziels pro Spieler)
            var weekKey = GetCurrentMondayUtc().ToString("yyyy-MM-dd");
            var donationPrefKey = $"guild_weekly_donation_{weekKey}_{uid}";
            var alreadyDonated = _preferences.Get(donationPrefKey, 0L);

            // Wochenziel aus Firebase laden (Fallback auf Default)
            var guildDataForCap = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{membership.GuildId}");
            var weeklyGoal = guildDataForCap?.WeeklyGoal ?? DefaultWeeklyGoal;
            var maxDonation = (long)(weeklyGoal * 0.30);
            var remaining = maxDonation - alreadyDonated;
            if (remaining <= 0) return false;

            // Betrag auf verbleibendes Cap begrenzen
            var cappedAmount = Math.Min((long)amount, remaining);
            if (cappedAmount <= 0) return false;
            amount = cappedAmount;

            // Spieler muss genug Geld haben
            if (!_gameStateService.TrySpendMoney(amount)) return false;

            var guildId = membership.GuildId;
            var contributionLong = (long)amount;

            // Aktuelle Gilden-Daten laden für atomisches Update
            var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
            if (guildData == null)
            {
                // Rollback: Geld zurückgeben
                _gameStateService.AddMoney(amount);
                return false;
            }

            // Wochenziel-Fortschritt ATOMAR serverseitig erhoehen (statt read-modify-write des
            // geteilten Keys) — sonst ueberschreiben gleichzeitig spendende Mitglieder ihre Beitraege
            // gegenseitig (Last-Write-Wins). Bei Fehler Rollback.
            if (!await _firebaseService.IncrementAsync($"guilds/{guildId}/weeklyProgress", contributionLong))
            {
                _gameStateService.AddMoney(amount);
                return false;
            }

            // Spenden-Tracking aktualisieren (Wöchentliches Cap)
            _preferences.Set(donationPrefKey, alreadyDonated + contributionLong);

            // Spieler-Beitrag aktualisieren (bei Fehler akzeptabel, nur Anzeige-Wert)
            var memberData = await _firebaseService.GetAsync<FirebaseGuildMember>($"guild_members/{guildId}/{uid}");
            if (memberData != null)
            {
                await _firebaseService.UpdateAsync($"guild_members/{guildId}/{uid}", new Dictionary<string, object>
                {
                    ["contribution"] = memberData.Contribution + contributionLong,
                    ["playerLevel"] = state.PlayerLevel
                });
            }

            GuildUpdated?.Invoke();

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Prüft ob ein neuer Wochenstart ist und resettet ggf. das Wochenziel.
    /// Verteilt Belohnungen wenn das Ziel erreicht wurde.
    /// </summary>
    private async Task CheckWeeklyResetAsync(string guildId, FirebaseGuildData guildData)
    {
        var currentMonday = GetCurrentMonday();
        var weekStartParsed = DateTime.MinValue;

        if (!string.IsNullOrEmpty(guildData.WeekStartUtc))
        {
            DateTime.TryParse(guildData.WeekStartUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out weekStartParsed);
        }

        if (weekStartParsed >= currentMonday) return;

        // Wochenreset nötig
        var updates = new Dictionary<string, object>
        {
            ["weekStartUtc"] = currentMonday.ToString("O"),
            ["weeklyProgress"] = 0
        };

        // Ziel erreicht? → Level-Up + Belohnung
        if (guildData.WeeklyProgress >= guildData.WeeklyGoal)
        {
            var newLevel = guildData.Level + 1;
            updates["level"] = newLevel;
            updates["totalWeeksCompleted"] = guildData.TotalWeeksCompleted + 1;
            // Neues Wochenziel skaliert mit Level
            // Diminishing Returns: sqrt(level) statt linear → hohe Level skalieren sanfter
            updates["weeklyGoal"] = (long)(DefaultWeeklyGoal * (1.0 + Math.Sqrt(newLevel) * 0.2));

            // Belohnung: Duplikat-Schutz via Preferences (verhindert doppelte GS bei parallelem Reset)
            var rewardKey = $"guild_weekly_reward_{currentMonday:yyyy-MM-dd}";
            if (!_preferences.Get(rewardKey, false))
            {
                int screwReward = Math.Min(50, 5 + guildData.Level * 2);
                _gameStateService.AddGoldenScrews(screwReward);
                _preferences.Set(rewardKey, true);
            }

            guildData.Level = newLevel;
        }

        await _firebaseService.UpdateAsync($"guilds/{guildId}", updates);

        // Eigenen Beitrag zurücksetzen (Firebase-Rules erlauben nur Schreibzugriff auf eigenen Eintrag)
        var uid = _firebaseService.PlayerId;
        if (!string.IsNullOrEmpty(uid))
        {
            await _firebaseService.UpdateAsync($"guild_members/{guildId}/{uid}", new Dictionary<string, object>
            {
                ["contribution"] = 0
            });
        }

        guildData.WeeklyProgress = 0;
    }
}
