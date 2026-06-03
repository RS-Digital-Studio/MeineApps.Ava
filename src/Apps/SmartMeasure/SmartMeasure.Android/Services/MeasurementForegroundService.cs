using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace SmartMeasure.Android.Services;

/// <summary>
/// Foreground-Service gegen Doze-Kill während aktiver Vermessung.
/// Android 12+ droppt BLE-Verbindungen wenn die App in den Doze-Mode fällt —
/// ein Foreground-Service mit persistenter Notification verhindert das.
///
/// Start per: <c>Context.StartForegroundService(new Intent(ctx, typeof(MeasurementForegroundService)))</c>
/// Stop per: <c>Context.StopService(new Intent(ctx, typeof(MeasurementForegroundService)))</c>
/// </summary>
[Service(
    Name = "de.meineapps.smartmeasure.MeasurementForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeConnectedDevice)]
public sealed class MeasurementForegroundService : Service
{
    private const string ChannelId = "smartmeasure_measurement";
    private const string ChannelName = "Vermessung";
    private const int NotificationId = 1001;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();

        var notification = BuildNotification();

        // Android 14+ (API 34): StartForeground MUSS foregroundServiceType übergeben.
        // Android 10+ (API 29): TypeConnectedDevice passt zu BLE-Verbindungen.
        // OperatingSystem.IsAndroidVersionAtLeast wird vom Static-Analyzer (CA1416) verstanden.
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(NotificationId, notification,
                ForegroundService.TypeConnectedDevice);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        // START_STICKY: Android startet den Service neu wenn er durch Speicherdruck gekillt wird.
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        // Notification-Channel nur auf Android 8+ (Oreo) nötig — MinSdk=26 garantiert das aber bereits.
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager == null) return;

        // Idempotent — bei existierendem Channel passiert nichts
        if (manager.GetNotificationChannel(ChannelId) != null) return;

        var channel = new NotificationChannel(
            ChannelId,
            ChannelName,
            NotificationImportance.Low) // Low = keine Sound/Vibration
        {
            Description = "Vermessung aktiv — verhindert dass Android die BLE-Verbindung zum Stab abbricht"
        };
        channel.SetShowBadge(false);
        manager.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        // Tap auf Notification öffnet die App (MainActivity)
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? string.Empty);
        PendingIntent? pendingIntent = null;
        if (launchIntent != null)
        {
            launchIntent.SetFlags(ActivityFlags.SingleTop);
            pendingIntent = PendingIntent.GetActivity(
                this, 0, launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }

        // Die Fluent-Setter von NotificationCompat.Builder sind als nullable annotiert,
        // geben aber immer denselben Builder zurück — über die lokale Variable verketten,
        // damit der Nullable-Analyzer keinen möglichen Null-Verweis sieht.
        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle("SmartMeasure");
        builder.SetContentText("Vermessung aktiv — Verbindung zum Stab wird gehalten");
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcMenuCompass);
        builder.SetOngoing(true);
        builder.SetPriority(NotificationCompat.PriorityLow);
        builder.SetCategory(NotificationCompat.CategoryService);

        if (pendingIntent != null)
            builder.SetContentIntent(pendingIntent);

        return builder.Build() ?? throw new InvalidOperationException("NotificationCompat.Builder.Build() lieferte null");
    }

    /// <summary>Convenience-Helper: Service starten.</summary>
    public static void Start(Context context)
    {
        var intent = new Intent(context, typeof(MeasurementForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    /// <summary>Convenience-Helper: Service stoppen.</summary>
    public static void Stop(Context context)
    {
        var intent = new Intent(context, typeof(MeasurementForegroundService));
        context.StopService(intent);
    }
}
