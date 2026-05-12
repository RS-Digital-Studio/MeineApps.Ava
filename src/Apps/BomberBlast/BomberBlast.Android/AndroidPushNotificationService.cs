using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using BomberBlast.Services;
using Firebase.Messaging;

// "NotificationChannel" gibt es zweimal: Android.App.NotificationChannel (Plattform) und
// BomberBlast.Services.NotificationChannel (Service-Enum). Alias-Direktiven verhindern den Konflikt.
using PlatformChannel = Android.App.NotificationChannel;
using ServiceChannel = BomberBlast.Services.NotificationChannel;

namespace BomberBlast.Droid;

/// <summary>
/// Android-Implementation für IPushNotificationService (FCM + AlarmManager).
/// Aktiv ab v2.0.56 — google-services.json + Xamarin.Firebase.Messaging-Binding vorhanden.
///
/// 3 Verantwortungen:
/// 1. FCM-Token holen + Refresh-Event (Server-seitige Push-Sends)
/// 2. Topic-Subscription (Saison-Reminders, Daily-Race-Reset)
/// 3. Lokale Notifications via AlarmManager (Daily-Reward-Reminder etc.)
///
/// Token-Refreshes kommen aus BomberBlastMessagingService.OnNewToken → das static Event
/// FcmTokenChangedStatic feuert, dieser Service propagiert es an Abonnenten via FcmTokenChanged.
/// </summary>
public sealed class AndroidPushNotificationService : IPushNotificationService
{
    private const int RequestCodePostNotifications = 1031;

    private readonly Activity _activity;
    private readonly Context _appContext;
    private string? _fcmToken;
    private TaskCompletionSource<bool>? _pendingPermissionRequest;

    /// <summary>
    /// Static Event vom BomberBlastMessagingService gefeuert bei Token-Refresh.
    /// Wir leiten es an unsere Instanz-Event-Subscriber weiter.
    /// </summary>
    internal static event EventHandler<string>? FcmTokenChangedStatic;

    /// <summary>
    /// Wird von BomberBlastMessagingService.OnNewToken aufgerufen. Events koennen nur
    /// in ihrer Definitions-Klasse gefeuert werden — daher dieser Indirection-Helfer.
    /// </summary>
    internal static void RaiseTokenRefresh(string newToken)
    {
        if (string.IsNullOrEmpty(newToken)) return;
        FcmTokenChangedStatic?.Invoke(null, newToken);
    }

    public AndroidPushNotificationService(Activity activity)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _appContext = activity.ApplicationContext ?? activity;
        FcmTokenChangedStatic += OnTokenRefreshFromService;
    }

    public string? FcmToken => _fcmToken;
    public bool ArePermissionsGranted { get; private set; }

    public event EventHandler<string>? FcmTokenChanged;

    public async Task InitializeAsync()
    {
        try
        {
            EnsureNotificationChannels();

            // Permission-Status pruefen (Android 13+). Vor API 33 sind Notifications by default erlaubt.
            ArePermissionsGranted = AreNotificationsCurrentlyAllowed();

            // FCM-Token einmal abrufen — Server-seitige Push-Sends brauchen den.
            // FirebaseMessaging.Instance.GetToken() liefert Android.Gms.Tasks.Task<string>.
            var token = await GetFcmTokenAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(token))
            {
                _fcmToken = token;
                FcmTokenChanged?.Invoke(this, token);
            }
        }
        catch
        {
            // Best-Effort: Init-Fehler haelt die App nicht auf
        }
    }

    public Task<bool> RequestPermissionAsync()
    {
        // Vor Android 13: keine Runtime-Permission noetig
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            ArePermissionsGranted = true;
            return Task.FromResult(true);
        }

        if (AreNotificationsCurrentlyAllowed())
        {
            ArePermissionsGranted = true;
            return Task.FromResult(true);
        }

        // Pending TCS — wird im MainActivity.OnRequestPermissionsResult via OnPermissionResult aufgeloest
        _pendingPermissionRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _activity.RequestPermissions(
            new[] { Android.Manifest.Permission.PostNotifications },
            RequestCodePostNotifications);

        return _pendingPermissionRequest.Task;
    }

    /// <summary>
    /// Wird von MainActivity.OnRequestPermissionsResult aufgerufen wenn der User
    /// auf den POST_NOTIFICATIONS-Dialog reagiert hat.
    /// </summary>
    public void OnPermissionResult(int requestCode, Permission[] grantResults)
    {
        if (requestCode != RequestCodePostNotifications) return;
        var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
        ArePermissionsGranted = granted;
        _pendingPermissionRequest?.TrySetResult(granted);
        _pendingPermissionRequest = null;
    }

    public Task SubscribeToTopicAsync(string topic)
    {
        if (string.IsNullOrEmpty(topic)) return Task.CompletedTask;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            FirebaseMessaging.Instance.SubscribeToTopic(topic)
                .AddOnCompleteListener(new SimpleCompleteListener(tcs));
        }
        catch { tcs.TrySetResult(false); }
        return tcs.Task;
    }

    public Task UnsubscribeFromTopicAsync(string topic)
    {
        if (string.IsNullOrEmpty(topic)) return Task.CompletedTask;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            FirebaseMessaging.Instance.UnsubscribeFromTopic(topic)
                .AddOnCompleteListener(new SimpleCompleteListener(tcs));
        }
        catch { tcs.TrySetResult(false); }
        return tcs.Task;
    }

    public void ScheduleLocalNotification(string id, DateTime triggerUtc, string title, string body, ServiceChannel channel)
    {
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            EnsureNotificationChannels();

            var intent = new Intent(_appContext, typeof(NotificationReceiver));
            intent.SetAction(NotificationReceiver.ActionShowLocal);
            intent.PutExtra(NotificationReceiver.ExtraId, id);
            intent.PutExtra(NotificationReceiver.ExtraTitle, title ?? string.Empty);
            intent.PutExtra(NotificationReceiver.ExtraBody, body ?? string.Empty);
            intent.PutExtra(NotificationReceiver.ExtraChannel, MapChannelId(channel));

            var pendingIntent = PendingIntent.GetBroadcast(
                _appContext,
                requestCode: id.GetHashCode(),
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            if (pendingIntent == null) return;

            var alarmManager = (AlarmManager?)_appContext.GetSystemService(Context.AlarmService);
            if (alarmManager == null) return;

            var triggerMs = (long)(triggerUtc.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds;

            // Android 12+ (API 31): SetExactAndAllowWhileIdle braucht USE_EXACT_ALARM oder
            // SCHEDULE_EXACT_ALARM Permission. Bei Verweigerung fallen wir auf SetAndAllowWhileIdle zurueck.
            if (OperatingSystem.IsAndroidVersionAtLeast(31) && !alarmManager.CanScheduleExactAlarms())
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMs, pendingIntent);
            else
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMs, pendingIntent);
        }
        catch { /* Best-Effort */ }
    }

    public void CancelLocalNotification(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            var intent = new Intent(_appContext, typeof(NotificationReceiver));
            intent.SetAction(NotificationReceiver.ActionShowLocal);
            var pendingIntent = PendingIntent.GetBroadcast(
                _appContext,
                requestCode: id.GetHashCode(),
                intent,
                PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
            if (pendingIntent == null) return;

            var alarmManager = (AlarmManager?)_appContext.GetSystemService(Context.AlarmService);
            alarmManager?.Cancel(pendingIntent);
            pendingIntent.Cancel();
        }
        catch { /* Best-Effort */ }
    }

    // ─── FCM-Token-Refresh-Bruecke (vom BomberBlastMessagingService) ──────────────
    private void OnTokenRefreshFromService(object? sender, string newToken)
    {
        _fcmToken = newToken;
        FcmTokenChanged?.Invoke(this, newToken);
    }

    // ─── FCM-Token-Abruf via Tasks-API ────────────────────────────────────────────
    private Task<string?> GetFcmTokenAsync()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            FirebaseMessaging.Instance.GetToken()
                .AddOnCompleteListener(new TokenCompleteListener(tcs));
        }
        catch
        {
            tcs.TrySetResult(null);
        }
        return tcs.Task;
    }

    // ─── Notification-Permission auf Android 13+ pruefen ──────────────────────────
    private bool AreNotificationsCurrentlyAllowed()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return true;
        return _appContext.CheckSelfPermission(Android.Manifest.Permission.PostNotifications)
               == Permission.Granted;
    }

    // ─── Notification-Channels einmalig registrieren ──────────────────────────────
    private void EnsureNotificationChannels()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        var manager = (NotificationManager?)_appContext.GetSystemService(Context.NotificationService);
        if (manager == null) return;

        EnsureChannel(manager, "bomberblast_daily", "Daily Rewards", NotificationImportance.Low);
        EnsureChannel(manager, "bomberblast_liveops", "Live Events", NotificationImportance.Default);
        EnsureChannel(manager, "bomberblast_important", "Important", NotificationImportance.High);
    }

    private static void EnsureChannel(NotificationManager manager, string id, string label, NotificationImportance importance)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        if (manager.GetNotificationChannel(id) != null) return;

        var channel = new PlatformChannel(id, label, importance);
        if (importance == NotificationImportance.High)
            channel.EnableVibration(true);
        manager.CreateNotificationChannel(channel);
    }

    internal static string MapChannelId(ServiceChannel channel) => channel switch
    {
        ServiceChannel.DailyRewards => "bomberblast_daily",
        ServiceChannel.LiveOps => "bomberblast_liveops",
        ServiceChannel.Important => "bomberblast_important",
        _ => "bomberblast_liveops"
    };

    // ─── Java-Callback-Wrapper fuer Tasks-API ─────────────────────────────────────
    private sealed class TokenCompleteListener : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly TaskCompletionSource<string?> _tcs;
        public TokenCompleteListener(TaskCompletionSource<string?> tcs) => _tcs = tcs;

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful)
                _tcs.TrySetResult(task.Result?.ToString());
            else
                _tcs.TrySetResult(null);
        }
    }

    private sealed class SimpleCompleteListener : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly TaskCompletionSource<bool> _tcs;
        public SimpleCompleteListener(TaskCompletionSource<bool> tcs) => _tcs = tcs;

        public void OnComplete(Android.Gms.Tasks.Task task) =>
            _tcs.TrySetResult(task.IsSuccessful);
    }
}
