# Gildenforschung Timer + Einladungs-Inbox Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Gildenforschung um Timer-Phase erweitern (1h/4h/12h nach Vollfinanzierung) und Einladungs-Inbox für gildenlose Spieler implementieren.

**Architecture:** Timer wird serverbasiert via `researchStartedAt` Feld in Firebase gespeichert. Completion-Check bei Tab-Öffnung und im GameLoop. Einladungs-Inbox nutzt neuen Firebase-Pfad `/player_invites/{uid}/{guildId}`. Beide Features greifen auf bestehende GuildService/GuildViewModel-Architektur zu.

**Tech Stack:** C#/.NET 10, Avalonia 11.3, Firebase Realtime Database REST API, SkiaSharp, CommunityToolkit.Mvvm

---

## Task 1: GuildResearchState + GuildResearchDefinition erweitern (Model-Layer)

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/GuildResearch.cs`

**Step 1: GuildResearchState um `ResearchStartedAt` erweitern**

In `GuildResearchState` (Zeile 244-254) neues Feld hinzufügen:

```csharp
[JsonPropertyName("researchStartedAt")]
public string? ResearchStartedAt { get; set; }
```

**Step 2: Timer-Dauer-Methode in GuildResearchDefinition**

Neue statische Methode nach `GetCategoryNameKey()` (Zeile 233):

```csharp
/// <summary>
/// Gibt die Forschungsdauer in Stunden zurück.
/// Tier 1 (< 100M): 1h, Tier 2 (100M-2B): 4h, Tier 3 (> 2B): 12h
/// </summary>
public static double GetResearchDurationHours(long cost)
{
    if (cost < 100_000_000) return 1.0;
    if (cost <= 2_000_000_000) return 4.0;
    return 12.0;
}
```

**Step 3: GuildResearchDisplay um Timer-Properties erweitern**

In `GuildResearchDisplay` (Zeile 264-287) neue Properties:

```csharp
/// <summary>Forschung ist voll bezahlt und Timer läuft.</summary>
public bool IsResearching { get; set; }

/// <summary>Wann die Forschung gestartet wurde (UTC ISO 8601).</summary>
public string? ResearchStartedAt { get; set; }

/// <summary>Verbleibende Forschungszeit (berechnet im ViewModel).</summary>
public TimeSpan? RemainingTime { get; set; }

/// <summary>Forschungsdauer in Stunden (aus Definition).</summary>
public double DurationHours { get; set; }
```

`IsLocked` Property anpassen:
```csharp
public bool IsLocked => !IsCompleted && !IsActive && !IsResearching;
```

**Step 4: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 2: GuildService - Forschungs-Timer-Logik

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/GuildService.cs`
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/Interfaces/IGuildService.cs`

**Step 1: IGuildService um Timer-Check erweitern**

Neue Methode im Interface:
```csharp
/// <summary>Prüft ob eine laufende Forschung abgeschlossen ist (Timer abgelaufen).</summary>
Task<bool> CheckResearchCompletionAsync();
```

**Step 2: ContributeToResearchAsync anpassen**

In `GuildService.ContributeToResearchAsync()` (Zeile 547-606): Statt `completed=true` bei Erreichen des Ziels, `researchStartedAt` setzen.

Ersetze den Block ab Zeile 587-591:
```csharp
// Abschluss prüfen → Timer starten statt sofort abschließen
if (researchState.Progress >= definition.Cost && string.IsNullOrEmpty(researchState.ResearchStartedAt))
{
    researchState.ResearchStartedAt = DateTime.UtcNow.ToString("O");
    // completed wird NICHT gesetzt - erst wenn Timer abläuft
}
```

**Step 3: CheckResearchCompletionAsync implementieren**

Neue Methode in GuildService (nach ContributeToResearchAsync):
```csharp
public async Task<bool> CheckResearchCompletionAsync()
{
    try
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return false;

        await _firebaseService.EnsureAuthenticatedAsync();

        var guildId = membership.GuildId;
        var statesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildResearchState>>(
            $"guild_research/{guildId}");
        if (statesRaw == null) return false;

        var now = DateTime.UtcNow;
        var anyCompleted = false;

        foreach (var (id, state) in statesRaw)
        {
            if (state.Completed || string.IsNullOrEmpty(state.ResearchStartedAt)) continue;

            // Timer-Start parsen
            if (!DateTime.TryParse(state.ResearchStartedAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
                continue;

            // Forschungsdauer ermitteln
            var definition = GuildResearchDefinition.GetAll().FirstOrDefault(d => d.Id == id);
            if (definition == null) continue;

            var durationHours = GuildResearchDefinition.GetResearchDurationHours(definition.Cost);

            // Schnellforschung-Bonus (guild_mastery_1: +20% Speed = -20% Dauer)
            if (_cachedResearchEffects.ResearchSpeedBonus > 0)
                durationHours *= (double)(1m - _cachedResearchEffects.ResearchSpeedBonus);

            var endTime = startedAt.AddHours(durationHours);
            if (now >= endTime)
            {
                // Timer abgelaufen → Forschung abschließen
                state.Completed = true;
                state.CompletedAt = now.ToString("O");
                await _firebaseService.SetAsync($"guild_research/{guildId}/{id}", state);
                anyCompleted = true;
            }
        }

        if (anyCompleted)
        {
            await RefreshResearchEffectsAsync(guildId);
            GuildUpdated?.Invoke();
        }

        return anyCompleted;
    }
    catch
    {
        return false;
    }
}
```

**Step 4: GetGuildResearchAsync anpassen**

In `GetGuildResearchAsync()` (Zeile 477-545): Timer-Check VOR dem Erstellen der Display-Liste einbauen + IsResearching/ResearchStartedAt/DurationHours in GuildResearchDisplay setzen.

Nach Zeile 491 (`var states = statesRaw ?? ...`) einfügen:
```csharp
// Timer-Check: Abgelaufene Forschungen automatisch abschließen
var now = DateTime.UtcNow;
foreach (var (id, state) in states)
{
    if (state.Completed || string.IsNullOrEmpty(state.ResearchStartedAt)) continue;
    if (!DateTime.TryParse(state.ResearchStartedAt, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
        continue;
    var def = GuildResearchDefinition.GetAll().FirstOrDefault(d => d.Id == id);
    if (def == null) continue;
    var durH = GuildResearchDefinition.GetResearchDurationHours(def.Cost);
    if (_cachedResearchEffects.ResearchSpeedBonus > 0)
        durH *= (double)(1m - _cachedResearchEffects.ResearchSpeedBonus);
    if (now >= startedAt.AddHours(durH))
    {
        state.Completed = true;
        state.CompletedAt = now.ToString("O");
        await _firebaseService.SetAsync($"guild_research/{membership.GuildId}/{id}", state);
    }
}
```

Im `result.Add(new GuildResearchDisplay { ... })` Block (Zeile 522-536):
- `IsResearching` und `ResearchStartedAt` und `DurationHours` setzen:
```csharp
IsResearching = !isCompleted && !string.IsNullOrEmpty(researchState?.ResearchStartedAt),
ResearchStartedAt = researchState?.ResearchStartedAt,
DurationHours = GuildResearchDefinition.GetResearchDurationHours(def.Cost),
```

- `IsActive` Logik anpassen: Aktiv NUR wenn nicht completed UND nicht researching UND erste unfertige in Kategorie:
```csharp
var isResearching = !isCompleted && !string.IsNullOrEmpty(researchState?.ResearchStartedAt);
var isActive = false;
if (!isCompleted && !isResearching && !categoryFirstIncomplete.ContainsKey(def.Category))
{
    isActive = true;
    categoryFirstIncomplete[def.Category] = true;
}
```

**Step 5: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 3: GuildViewModel - Timer-Anzeige + Forschungs-Sperre

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/ViewModels/GuildViewModel.cs`

**Step 1: Neue Properties für Timer-Anzeige**

```csharp
[ObservableProperty]
private bool _hasActiveResearch;

[ObservableProperty]
private string _activeResearchName = "";

[ObservableProperty]
private string _activeResearchCountdown = "";

[ObservableProperty]
private string _activeResearchId = "";
```

**Step 2: Timer-Refresh in LoadGuildResearchAsync**

Nach dem Laden der Forschungsliste: Laufende Forschung finden und Properties setzen.

```csharp
// Laufende Forschung finden
var researching = research.FirstOrDefault(r => r.IsResearching);
if (researching != null)
{
    HasActiveResearch = true;
    ActiveResearchName = _localizationService.GetString(researching.Name) ?? researching.Name;
    ActiveResearchId = researching.Id;
    UpdateResearchCountdown(researching);
}
else
{
    HasActiveResearch = false;
    ActiveResearchName = "";
    ActiveResearchCountdown = "";
    ActiveResearchId = "";
}
```

**Step 3: UpdateResearchCountdown Methode**

```csharp
private void UpdateResearchCountdown(GuildResearchDisplay research)
{
    if (string.IsNullOrEmpty(research.ResearchStartedAt)) return;

    if (!DateTime.TryParse(research.ResearchStartedAt,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
        return;

    var durationHours = research.DurationHours;
    // Schnellforschung-Bonus
    var effects = _guildService.GetResearchEffects();
    if (effects.ResearchSpeedBonus > 0)
        durationHours *= (double)(1m - effects.ResearchSpeedBonus);

    var endTime = startedAt.AddHours(durationHours);
    var remaining = endTime - DateTime.UtcNow;

    if (remaining <= TimeSpan.Zero)
    {
        ActiveResearchCountdown = _localizationService.GetString("GuildResearchDone") ?? "Fertig!";
    }
    else if (remaining.TotalHours >= 1)
    {
        ActiveResearchCountdown = $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}min";
    }
    else
    {
        ActiveResearchCountdown = $"{remaining.Minutes}min {remaining.Seconds:D2}s";
    }
}
```

**Step 4: Forschungs-Beitrag blockieren wenn Timer läuft**

In der Methode die den Beitrags-Dialog öffnet: Prüfe ob `HasActiveResearch` und der ausgewählte Item NICHT der aktive ist. Wenn ja → Beitrag blockieren (nur eine Forschung gleichzeitig + Forschung mit laufendem Timer braucht kein weiteres Geld).

Blockiere `ContributeToResearchAsync` wenn Item bereits `IsResearching`:
```csharp
// Am Anfang von ShowResearchContributeDialog oder ContributeToResearchAsync:
var item = GuildResearch.FirstOrDefault(r => r.Id == researchId);
if (item == null || item.IsCompleted || item.IsResearching) return;
```

**Step 5: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 4: GuildResearchTreeRenderer - Timer-Visualisierung

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/GuildResearchTreeRenderer.cs`

**Step 1: IsResearching-State im Renderer**

Items mit `IsResearching = true` sollen:
- Pulsierendes Zahnrad-Icon (Rotation-Animation)
- Amber/Orange Rahmen statt grau
- Countdown-Text unter dem Item-Namen
- Fortschritts-Ring zeigt Timer-Fortschritt (nicht Geld-Fortschritt)

**Step 2: Render-Logik für RESEARCHING-Status**

In der `DrawNode()` Methode (oder equivalent): Neuer Branch für `IsResearching`:
- Ring: Amber (#F59E0B) pulsierend (Sinus-basiert Alpha)
- Icon: Zahnrad-Rotation (anstatt statisches Icon)
- Text: Countdown-String unterhalb

**Step 3: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 5: Einladungs-Inbox Model + Firebase-Methoden

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/Guild.cs` (oder neue Datei)
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/Interfaces/IGuildService.cs`
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/GuildService.cs`

**Step 1: GuildInvitation Model**

Neues Model (in Guild.cs oder GuildResearch.cs):

```csharp
/// <summary>
/// Firebase-Daten einer Gilden-Einladung.
/// Pfad: /player_invites/{uid}/{guildId}
/// </summary>
public class GuildInvitation
{
    [JsonPropertyName("guildName")]
    public string GuildName { get; set; } = "";

    [JsonPropertyName("guildIcon")]
    public string GuildIcon { get; set; } = "";

    [JsonPropertyName("guildColor")]
    public string GuildColor { get; set; } = "";

    [JsonPropertyName("guildLevel")]
    public int GuildLevel { get; set; }

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("invitedBy")]
    public string InvitedBy { get; set; } = "";

    [JsonPropertyName("invitedAt")]
    public string InvitedAt { get; set; } = "";
}

/// <summary>
/// UI-Anzeige-Daten einer empfangenen Einladung.
/// </summary>
public class GuildInvitationDisplay
{
    public string GuildId { get; set; } = "";
    public string GuildName { get; set; } = "";
    public string GuildIcon { get; set; } = "";
    public string GuildColor { get; set; } = "";
    public int GuildLevel { get; set; }
    public string MemberDisplay { get; set; } = "";
    public string InvitedByDisplay { get; set; } = "";
}
```

**Step 2: IGuildService erweitern**

```csharp
// ── Einladungs-Inbox ──

/// <summary>Sendet eine direkte Einladung an einen Spieler.</summary>
Task<bool> SendInviteAsync(string targetUid);

/// <summary>Lädt empfangene Einladungen für den aktuellen Spieler.</summary>
Task<List<(string guildId, GuildInvitation invite)>> GetReceivedInvitesAsync();

/// <summary>Nimmt eine Einladung an (beitritt Gilde, löscht alle anderen Einladungen).</summary>
Task<bool> AcceptInviteAsync(string guildId);

/// <summary>Lehnt eine einzelne Einladung ab.</summary>
Task<bool> DeclineInviteAsync(string guildId);
```

**Step 3: GuildService - SendInviteAsync**

```csharp
public async Task<bool> SendInviteAsync(string targetUid)
{
    try
    {
        var uid = _firebaseService.Uid;
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(targetUid)) return false;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return false;

        var guildId = membership.GuildId;

        // Gilden-Daten für die Einladung laden
        var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{guildId}");
        if (guildData == null) return false;

        var invite = new GuildInvitation
        {
            GuildName = guildData.Name,
            GuildIcon = guildData.Icon,
            GuildColor = guildData.Color,
            GuildLevel = guildData.Level,
            MemberCount = guildData.MemberCount,
            InvitedBy = PlayerName ?? "Spieler",
            InvitedAt = DateTime.UtcNow.ToString("O")
        };

        // Max 10 Einladungen pro Spieler prüfen
        var existing = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
            $"player_invites/{targetUid}");
        if (existing != null && existing.Count >= 10)
        {
            // Älteste Einladung löschen
            var oldest = existing.OrderBy(e => e.Value.InvitedAt).First();
            await _firebaseService.DeleteAsync($"player_invites/{targetUid}/{oldest.Key}");
        }

        await _firebaseService.SetAsync($"player_invites/{targetUid}/{guildId}", invite);
        return true;
    }
    catch
    {
        return false;
    }
}
```

**Step 4: GuildService - GetReceivedInvitesAsync**

```csharp
public async Task<List<(string guildId, GuildInvitation invite)>> GetReceivedInvitesAsync()
{
    try
    {
        var uid = _firebaseService.Uid;
        if (string.IsNullOrEmpty(uid)) return [];

        await _firebaseService.EnsureAuthenticatedAsync();

        var invitesRaw = await _firebaseService.GetAsync<Dictionary<string, GuildInvitation>>(
            $"player_invites/{uid}");
        if (invitesRaw == null || invitesRaw.Count == 0) return [];

        var result = new List<(string guildId, GuildInvitation invite)>();
        foreach (var (guildId, invite) in invitesRaw)
        {
            result.Add((guildId, invite));
        }

        // Nach Datum sortieren (neueste zuerst)
        result.Sort((a, b) => string.Compare(b.invite.InvitedAt, a.invite.InvitedAt, StringComparison.Ordinal));
        return result;
    }
    catch
    {
        return [];
    }
}
```

**Step 5: GuildService - AcceptInviteAsync / DeclineInviteAsync**

```csharp
public async Task<bool> AcceptInviteAsync(string guildId)
{
    try
    {
        var uid = _firebaseService.Uid;
        if (string.IsNullOrEmpty(uid)) return false;

        // Gilde beitreten
        var success = await JoinGuildAsync(guildId);
        if (!success) return false;

        // Alle Einladungen löschen
        await _firebaseService.DeleteAsync($"player_invites/{uid}");
        return true;
    }
    catch
    {
        return false;
    }
}

public async Task<bool> DeclineInviteAsync(string guildId)
{
    try
    {
        var uid = _firebaseService.Uid;
        if (string.IsNullOrEmpty(uid)) return false;

        await _firebaseService.DeleteAsync($"player_invites/{uid}/{guildId}");
        return true;
    }
    catch
    {
        return false;
    }
}
```

**Step 6: InvitePlayerAsync aktualisieren**

Die bestehende `InvitePlayerAsync` im ViewModel soll jetzt `SendInviteAsync` aufrufen statt nur visuelles Feedback:

```csharp
// In InvitePlayerAsync:
var success = await _guildService.SendInviteAsync(player.Uid);
if (success)
{
    player.IsInvited = true;
    // ... bestehende Nachricht
}
```

**Step 7: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 6: GuildViewModel - Einladungs-Inbox UI-Logik

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/ViewModels/GuildViewModel.cs`

**Step 1: Neue Properties für Inbox**

```csharp
[ObservableProperty]
private ObservableCollection<GuildInvitationDisplay> _receivedInvites = [];

[ObservableProperty]
private bool _hasReceivedInvites;

[ObservableProperty]
private int _receivedInviteCount;
```

**Step 2: Einladungen laden (in LoadGuildDataInternalAsync)**

Wenn Spieler NICHT in einer Gilde ist (Browse-State): Einladungen laden.

```csharp
// Am Ende von LoadGuildDataInternalAsync, wenn state.GuildMembership == null:
var invites = await _guildService.GetReceivedInvitesAsync();
var maxMembers = _guildService.GetMaxMembers();
var displays = new ObservableCollection<GuildInvitationDisplay>();
foreach (var (guildId, invite) in invites)
{
    displays.Add(new GuildInvitationDisplay
    {
        GuildId = guildId,
        GuildName = invite.GuildName,
        GuildIcon = invite.GuildIcon,
        GuildColor = invite.GuildColor,
        GuildLevel = invite.GuildLevel,
        MemberDisplay = $"{invite.MemberCount}/{maxMembers}",
        InvitedByDisplay = invite.InvitedBy
    });
}
ReceivedInvites = displays;
HasReceivedInvites = displays.Count > 0;
ReceivedInviteCount = displays.Count;
```

**Step 3: AcceptInvite Command**

```csharp
[RelayCommand]
private async Task AcceptInviteAsync(GuildInvitationDisplay? invite)
{
    if (invite == null || _isBusy) return;
    _isBusy = true;
    try
    {
        var success = await _guildService.AcceptInviteAsync(invite.GuildId);
        if (success)
        {
            CelebrationRequested?.Invoke(this, EventArgs.Empty);
            MessageRequested?.Invoke(
                _localizationService.GetString("Guild") ?? "Innung",
                _localizationService.GetString("GuildJoined") ?? "Gilde beigetreten!");
            await LoadGuildDataInternalAsync();
        }
        else
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Guild") ?? "Innung",
                _localizationService.GetString("GuildFull") ?? "Gilde ist voll");
        }
    }
    finally
    {
        _isBusy = false;
    }
}
```

**Step 4: DeclineInvite Command**

```csharp
[RelayCommand]
private async Task DeclineInviteAsync(GuildInvitationDisplay? invite)
{
    if (invite == null || _isBusy) return;
    _isBusy = true;
    try
    {
        await _guildService.DeclineInviteAsync(invite.GuildId);
        ReceivedInvites.Remove(invite);
        HasReceivedInvites = ReceivedInvites.Count > 0;
        ReceivedInviteCount = ReceivedInvites.Count;
    }
    finally
    {
        _isBusy = false;
    }
}
```

**Step 5: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 7: GuildView.axaml - Einladungs-Inbox UI

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/GuildView.axaml`

**Step 1: Inbox-Sektion im Browse-Panel**

Im Browse-Panel (der State für gildenlose Spieler): Vor der "Verfügbare Gilden"-Liste eine Einladungs-Sektion einfügen.

Die Sektion zeigt:
- Header "Einladungen (X)" mit EmailPlus-Icon
- Für jede Einladung: Card mit GuildIcon + GuildName + Level + MemberCount + InvitedBy
- Zwei Buttons: "Annehmen" (grün/CraftPrimary) + "Ablehnen" (grau)
- Trennlinie "── oder Gilde suchen ──"
- Darunter weiter die bestehende Gilden-Liste + Erstellen-Button

```xml
<!-- Einladungs-Inbox (nur sichtbar wenn Einladungen vorhanden) -->
<StackPanel IsVisible="{Binding HasReceivedInvites}" Margin="0,0,0,16">
    <TextBlock Text="{Binding ReceivedInviteCount, StringFormat='Einladungen ({0})'}"
               ... />
    <ItemsControl ItemsSource="{Binding ReceivedInvites}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Classes="Card" Margin="0,0,0,8">
                    <!-- Guild-Icon + Name + Level + Members -->
                    <!-- Annehmen / Ablehnen Buttons -->
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</StackPanel>
```

**Step 2: Einladungs-Inbox lokalisieren**

Verwende `{loc:Translate InvitationsHeader}` statt hardcodiertem Text.

**Step 3: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 8: Forschungs-Timer im GuildResearchView anzeigen

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/Guild/GuildResearchView.axaml`
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/Guild/GuildResearchView.axaml.cs`

**Step 1: Timer-Banner im Header**

Wenn `HasActiveResearch` true ist: Amber-Banner unter dem GuildHall-Header mit:
- Zahnrad-Icon (animiert)
- Forschungsname
- Countdown-Text
- Optional: "Fertig!"-Button wenn Timer abgelaufen

**Step 2: Countdown-Refresh**

In Code-Behind: Im bestehenden 20fps Render-Loop-Timer auch den Countdown aktualisieren (alle 1s reicht). Der ViewModel hat `UpdateResearchCountdown()` - dieses wird periodisch aufgerufen.

**Step 3: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 9: Lokalisierung (RESX-Keys in 6 Sprachen)

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Resources/AppStrings.resx` (+ .de/.en/.es/.fr/.it/.pt)
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Resources/AppStrings.Designer.cs`

**Neue RESX-Keys:**

| Key | DE | EN |
|-----|----|----|
| `GuildResearchDone` | Fertig! | Done! |
| `GuildResearchInProgress` | Wird erforscht... | Researching... |
| `GuildResearchTimeRemaining` | Noch {0} | {0} remaining |
| `GuildResearchBlocked` | Forschung läuft bereits | Research in progress |
| `InvitationsHeader` | Einladungen ({0}) | Invitations ({0}) |
| `InvitedByPrefix` | Eingeladen von: | Invited by: |
| `AcceptInvite` | Annehmen | Accept |
| `DeclineInvite` | Ablehnen | Decline |
| `OrSearchGuild` | oder Gilde suchen | or search guild |
| `InvitePlayer` | Einladen | Invite |

Alle 10 Keys in 6 Sprachen (DE/EN/ES/FR/IT/PT).

**Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: 0 Fehler

---

## Task 10: CLAUDE.md + Build-Check

**Files:**
- Modify: `src/Apps/HandwerkerImperium/CLAUDE.md`
- Run: `dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln`
- Run: `dotnet run --project tools/AppChecker HandwerkerImperium`

**Step 1: CLAUDE.md aktualisieren**

Gilden-Forschung-Sektion erweitern um Timer-Info:
- Forschungs-Timer: 3 Tiers (1h/4h/12h), `researchStartedAt` Firebase-Feld
- Einladungs-Inbox: `/player_invites/{uid}/{guildId}`, Annehmen/Ablehnen

**Step 2: Full Solution Build**

Run: `dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln`
Expected: 0 Fehler

**Step 3: AppChecker**

Run: `dotnet run --project tools/AppChecker HandwerkerImperium`
Expected: Keine neuen kritischen Fehler
