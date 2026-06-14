using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;

namespace HandwerkerImperium.Services;

/// <summary>
/// GuildService — Initialisierung + UID→PlayerId-Migration.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildService
{
    // ═══════════════════════════════════════════════════════════════════════
    // INIT
    // ═══════════════════════════════════════════════════════════════════════

    public async Task InitializeAsync()
    {
        try
        {
            await _firebaseService.EnsureAuthenticatedAsync();

            // Stabile Spieler-ID initialisieren (überlebt Firebase-Account-Wechsel)
            _firebaseService.InitializePlayerId(_gameStateService.State.PlayerGuid);

            var playerId = _firebaseService.PlayerId;
            if (string.IsNullOrEmpty(playerId)) return;

            // auth_to_player Mapping sicherstellen (Security Rules benötigen es für Lese-Zugriff)
            // MUSS awaited werden: guild_members-Read-Rules prüfen auth_to_player/{uid}.
            // Ohne Mapping schlagen alle Gilden-Reads fehl (Permission denied).
            await _firebaseService.SyncAuthToPlayerMappingAsync();

            // PlayerId im GameState als Backup sichern
            if (_gameStateService.State.PlayerGuid != playerId)
            {
                _gameStateService.State.PlayerGuid = playerId;
            }

            // Migration: Daten von alter Firebase-UID zu PlayerId migrieren
            await MigrateFromUidToPlayerIdAsync(playerId);

            // Prüfen ob Spieler in einer Gilde ist (Schnell-Lookup)
            var guildId = await _firebaseService.GetAsync<string>($"player_guilds/{playerId}");
            if (!string.IsNullOrEmpty(guildId))
            {
                // Gilden-Basisdaten laden für lokalen Cache
                var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
                if (guildData != null)
                {
                    UpdateLocalCache(guildId, guildData);
                }
                else if (_firebaseService.IsOnline)
                {
                    // Gilde existiert definitiv nicht mehr (Online bestätigt)
                    ClearLocalCache();
                    await _firebaseService.DeleteAsync($"player_guilds/{playerId}");
                }
                // else: Offline → lokalen Cache beibehalten
            }
            else if (_firebaseService.IsOnline)
            {
                // Definitiv nicht in einer Gilde (Online bestätigt)
                ClearLocalCache();
                await RegisterAsAvailableInternalAsync();
            }
            // else: Offline → lokalen Cache beibehalten
        }
        catch (Exception ex)
        {
            _log.Error("Gilden-Initialisierung fehlgeschlagen", ex);
        }

        GuildUpdated?.Invoke();
    }

    /// <summary>
    /// Migriert Spieler-Daten von alter Firebase-UID zu stabiler PlayerId.
    /// Einmalig beim ersten Start nach dem Update.
    /// </summary>
    private async Task MigrateFromUidToPlayerIdAsync(string playerId)
    {
        var firebaseUid = _firebaseService.Uid;
        if (string.IsNullOrEmpty(firebaseUid) || firebaseUid == playerId) return;

        // Schon unter neuer PlayerId vorhanden? → bereits migriert
        var existingGuild = await _firebaseService.GetAsync<string>($"player_guilds/{playerId}");
        if (!string.IsNullOrEmpty(existingGuild)) return;

        // Alte Daten unter Firebase-UID vorhanden?
        var oldGuildId = await _firebaseService.GetAsync<string>($"player_guilds/{firebaseUid}");
        if (string.IsNullOrEmpty(oldGuildId)) return;

        // Gilden-Zuordnung migrieren
        await _firebaseService.SetAsync($"player_guilds/{playerId}", oldGuildId);

        // Mitglieds-Eintrag migrieren — Set neuer + Delete alter Eintrag atomar als
        // Multi-Path-Update. Frueher waren das zwei Operationen; bei Delete-Failure blieb der
        // alte Eintrag als Geister-Member stehen und zaehlte weiter in memberCount.
        var memberData = await _firebaseService.GetAsync<FirebaseGuildMember>(
            $"guild_members/{oldGuildId}/{firebaseUid}");
        if (memberData != null)
        {
            await _firebaseService.UpdateAsync($"guild_members/{oldGuildId}", new Dictionary<string, object>
            {
                [playerId] = memberData,
                [firebaseUid] = null!  // Firebase: null-Wert loescht den Pfad — atomar mit dem Set
            });
        }

        // Einladungen migrieren
        var invites = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
            $"player_invites/{firebaseUid}");
        if (invites != null)
        {
            foreach (var (guildId, invite) in invites)
                await _firebaseService.SetAsync($"player_invites/{playerId}/{guildId}", invite);
            try { await _firebaseService.DeleteAsync($"player_invites/{firebaseUid}"); } catch { /* Nicht-kritisch */ }
        }

        // Alte Einträge aufräumen (jeweils try/catch, damit ein Fehler nicht den Rest blockiert)
        try { await _firebaseService.DeleteAsync($"player_guilds/{firebaseUid}"); } catch { /* Nicht-kritisch */ }
        try { await _firebaseService.DeleteAsync($"available_players/{firebaseUid}"); } catch { /* Nicht-kritisch */ }
    }
}
