# Services — Client-seitige Service-Implementierungen

Shared-eigene Services, die Lücken schließen die von `BingXBot.ClientApi` nicht abgedeckt werden.
Alle Trading-Domain-Services (Local/Remote-Impls, BotEventBus, DB) leben in den Backend-Libraries
`BingXBot.Trading`, `BingXBot.ClientApi` und `BingXBot.Core`. Interfaces → `BingXBot.Contracts`.

## Dateien

| Datei | Zweck |
|-------|-------|
| `RemoteSettingsAutoSync.cs` | Subscribed `IBotEventStream.ConnectionChanged`. Bei jedem Re-Connect werden die Server-Settings via dem injizierten `Func<Task> refreshAsync` (= `App.RefreshRemoteSettingsAsync`) neu in die Client-DI-Singletons gespielt. Verhindert den "Client überschreibt Server-Werte beim nächsten Save"-Fall nach Reconnect. |

## Settings-Sync-Architektur

```
IBotEventStream.SettingsChanged
    → RemoteSettingsService.RaiseChanged
        → ISettingsService.SettingsChanged
            → ViewModels.LoadFromSettings()

IBotEventStream.ConnectionChanged (auf jedem Re-Connect)
    → RemoteSettingsAutoSync
        → refreshAsync()   (REST GET /settings, dann RestoreSettingsFromDb)
```

**Debounce-Schutz**: `RemoteSettingsAutoSync.MarkRefreshed()` setzt `_lastRefreshUtc = DateTime.UtcNow`.
Verhindert einen doppelten Refresh wenn `ConnectionChanged` unmittelbar nach dem initialen
Startup-Refresh feuert (2s-Fenster). `MarkRefreshed` muss vom Aufrufer nach dem Initial-Refresh
explizit aufgerufen werden — sonst ist `_lastRefreshUtc = DateTime.MinValue` und der erste
`ConnectionChanged` triggert stets einen redundanten zweiten Refresh (Race-Quelle).

## ISettingsPersistenceService (Ziel-Migration)

Neue ViewModels sollen `ISettingsPersistenceService.SaveAllAsync()` per DI injizieren statt
`App.SaveAllSettingsAsync()` (statischer Legacy-Wrapper). Interface in `BingXBot.Contracts`,
Impl `SettingsPersistenceService` in `BingXBot.Trading`, registriert als Singleton in `App.axaml.cs`.
