using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// GuildViewModel — Gilden-Chat: Senden, Polling-Timer, Diff-basiertes Laden.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
public sealed partial class GuildViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Chat
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SendChatMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || ChatInput.Length > 200) return;
        if (!CanSendChat) return;
        if ((DateTime.UtcNow - _lastChatSend).TotalSeconds < 5) return;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return;

        CanSendChat = false;
        try
        {
            var success = await _facade.Chat.SendMessageAsync(membership.GuildId, ChatInput.Trim());
            if (success)
            {
                ChatInput = "";
                _lastChatSend = DateTime.UtcNow;
                await LoadChatMessagesAsync();
                // Cooldown 5 Sekunden nur bei Erfolg
                _ = Task.Delay(5000).ContinueWith(_ =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => CanSendChat = true));
            }
            else
            {
                CanSendChat = true;
            }
        }
        catch
        {
            CanSendChat = true;
        }
    }

    /// <summary>
    /// Startet den Polling-Timer fuer den Chat.
    /// F-30: Intervall von 15s auf 20s erhoeht (Battery-Saving — 25% weniger Reads/min).
    /// Pause bei Tab-Wechsel weg vom GuildChat ist schon ueber MainViewModel.OnActivePageChanged
    /// implementiert (StopChatPolling). FCM-Push als langfristige Loesung bleibt offen.
    /// </summary>
    public void StartChatPolling()
    {
        StopChatPolling();
        _chatPollTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _chatPollHandler = async (_, _) => await LoadChatMessagesAsync();
        _chatPollTimer.Tick += _chatPollHandler;
        _chatPollTimer.Start();
    }

    /// <summary>
    /// Stoppt den Chat-Polling-Timer und entfernt den Event-Handler.
    /// </summary>
    public void StopChatPolling()
    {
        if (_chatPollTimer != null && _chatPollHandler != null)
            _chatPollTimer.Tick -= _chatPollHandler;
        _chatPollTimer?.Stop();
        _chatPollHandler = null;
        _chatPollTimer = null;
    }

    /// <summary>
    /// Laedt die letzten 50 Chat-Nachrichten der Gilde.
    /// Diff-basiert: Nur neue Nachrichten werden angehängt, alte getrimmt.
    /// Vermeidet kompletten UI-Rebuild bei jedem Polling-Zyklus.
    /// </summary>
    public async Task LoadChatMessagesAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return;

        try
        {
            var messages = await _facade.Chat.GetRecentMessagesAsync(membership.GuildId);
            var latest = messages.TakeLast(50).ToList();

            if (ChatMessages.Count == 0)
            {
                // Erstbefüllung: Komplett initialisieren
                ChatMessages = new ObservableCollection<ChatMessageDisplay>(latest);
            }
            else
            {
                // Diff-Update: Nur neue Nachrichten anhängen (Vergleich per Timestamp + Uid)
                var lastKnownTimestamp = ChatMessages[^1].Timestamp;
                var lastKnownUid = ChatMessages[^1].Uid;

                var newMessages = new List<ChatMessageDisplay>();
                var foundLast = false;
                foreach (var msg in latest)
                {
                    if (foundLast)
                    {
                        newMessages.Add(msg);
                    }
                    else if (msg.Timestamp == lastKnownTimestamp && msg.Uid == lastKnownUid)
                    {
                        foundLast = true;
                    }
                }

                // Neue Nachrichten anhängen
                foreach (var msg in newMessages)
                    ChatMessages.Add(msg);

                // Alte Nachrichten am Anfang trimmen wenn > 50
                while (ChatMessages.Count > 50)
                    ChatMessages.RemoveAt(0);
            }

            ChatSubtitle = latest.Count > 0
                ? latest[^1].Text
                : (_localizationService.GetString("NoChatMessages") ?? "No messages");
        }
        catch
        {
            ChatSubtitle = _localizationService.GetString("NoChatMessages") ?? "No messages";
        }
    }
}
