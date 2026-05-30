# Services — Client-seitige Service-Implementierungen

Shared-eigene Services, die Lücken schließen die von `BingXBot.ClientApi` nicht abgedeckt werden.
Alle Trading-Domain-Services (Local/Remote-Impls, BotEventBus, DB) leben in den Backend-Libraries
`BingXBot.Trading`, `BingXBot.ClientApi` und `BingXBot.Core`. Interfaces → `BingXBot.Contracts`.

## Dateien

| Datei | Zweck |
|-------|-------|
| `RemoteSettingsAutoSync.cs` | Subscribed `IBotEventStream.ConnectionChanged`. Bei jedem Re-Connect werden die Server-Settings via `App.RefreshRemoteSettingsAsync()` neu in die Client-DI-Singletons gespielt. Verhindert den "Client überschreibt Server-Werte beim nächsten Save"-Fall nach Reconnect. |

## Settings-Sync-Architektur

```
IBotEventStream.SettingsChanged
    → RemoteSettingsService.RaiseChanged
        → ISettingsService.SettingsChanged
            → ViewModels.LoadFromSettings()

IBotEventStream.ConnectionChanged (auf jedem Re-Connect)
    → RemoteSettingsAutoSync
        → App.RefreshRemoteSettingsAsync()   (REST GET /settings, dann RestoreSettingsFromDb)
```

**Debounce-Schutz**: `RemoteSettingsAutoSync.MarkRefreshed()` setzt einen 2s-Debounce nach dem
App-Start-Refresh. Verhindert einen doppelten Refresh wenn `ConnectionChanged` unmittelbar nach
dem manuellen Startup-Refresh feuert (Race-Window-Bug 27.04.2026).

## ISettingsPersistenceService (Ziel-Migration)

Neue ViewModels sollen `ISettingsPersistenceService.SaveAllAsync()` per DI injizieren statt
`App.SaveAllSettingsAsync()` (statischer Legacy-Wrapper). `SettingsPersistenceService` ist in der
Contracts-Library und dort registriert.
