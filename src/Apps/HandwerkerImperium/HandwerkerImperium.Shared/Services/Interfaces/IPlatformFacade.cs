namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bounded-Context "Platform": Bündelt alle Subsysteme die mit der Hardware-Plattform und
/// Cloud-Diensten reden — Audio, Push-Notifications, Cloud-Save, Analytics, RemoteConfig,
/// Play Games, Review. AAA-Audit P1 Service-Sprawl-Reduction.
///
/// Konsumenten die ausschliesslich auf Plattform-Funktionen zugreifen (z.B. SettingsViewModel)
/// koennen optional die Facade nutzen statt 5-7 Einzel-Dependencies.
/// </summary>
public interface IPlatformFacade
{
    /// <summary>SFX + Music + Vibration (Android: AndroidAudioService, Desktop: DesktopAudioService).</summary>
    IAudioService Audio { get; }

    /// <summary>Push-Notifications (8 Trigger-Typen via AndroidNotificationService).</summary>
    INotificationService Notifications { get; }

    /// <summary>Cloud-Save (Firebase Realtime Database, Konflikt-Resolution, Auto-Upload).</summary>
    ICloudSaveService? CloudSave { get; }

    /// <summary>Firebase Analytics (50+ Events via AnalyticsEvents-Katalog).</summary>
    IAnalyticsService? Analytics { get; }

    /// <summary>Firebase Remote Config (Feature-Flags, A/B-Test-Cohorts).</summary>
    IRemoteConfigService? RemoteConfig { get; }

    /// <summary>Google Play Games (Leaderboards, Achievements, Sign-In).</summary>
    IPlayGamesService? PlayGames { get; }

    /// <summary>In-App-Review (Google Play, Apple App Store).</summary>
    IReviewService? Review { get; }
}
