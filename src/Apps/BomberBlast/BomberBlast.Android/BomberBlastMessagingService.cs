using Android.App;
using Android.Content;
using Firebase.Messaging;

namespace BomberBlast.Droid;

/// <summary>
/// FirebaseMessagingService-Subclass — empfängt FCM-Push-Notifications + Token-Refreshes.
/// Im Manifest unter org.rsdigital.bomberblast.BomberBlastMessagingService registriert.
///
/// Behandelt zwei Payload-Arten:
/// 1. `notification`-Payload (vom Server-SDK gesetzt) → System zeigt die Notification automatisch,
///    wir machen nichts.
/// 2. `data`-Payload (silent push oder hybrid) → wir bauen die Notification selbst aus den
///    "title"/"body"/"channel"-Keys und zeigen sie via NotificationManager an.
/// </summary>
[Android.Runtime.Register("org.rsdigital.bomberblast.BomberBlastMessagingService")]
[Service(Name = "org.rsdigital.bomberblast.BomberBlastMessagingService", Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public sealed class BomberBlastMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        if (string.IsNullOrEmpty(token)) return;

        // Static Event feuern — AndroidPushNotificationService haengt sich daran und
        // propagiert ueber FcmTokenChanged an Abonnenten (z.B. Server-Sync).
        try
        {
            AndroidPushNotificationService.RaiseTokenRefresh(token);
        }
        catch
        {
            // Best-Effort: Wenn der Service noch nicht instanziiert ist, geht der Token verloren.
            // Beim naechsten App-Start holt sich PushNotificationService den Token via GetToken() neu.
        }
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);
        if (message == null) return;

        // Wenn das Server-SDK eine notification-Payload mitgeschickt hat, zeigt Android sie
        // bereits selbst an — dann nichts doppelt machen.
        if (message.GetNotification() != null) return;

        var data = message.Data;
        if (data == null || data.Count == 0) return;

        var title = data.TryGetValue("title", out var t) ? t : "BomberBlast";
        var body = data.TryGetValue("body", out var b) ? b : string.Empty;
        var channelId = data.TryGetValue("channel", out var c) ? c : "bomberblast_liveops";

        ShowNotification(title, body, channelId);
    }

    private void ShowNotification(string title, string body, string channelId)
    {
        try
        {
            // Tap-Action: MainActivity oeffnen
            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            var pendingIntent = PendingIntent.GetActivity(
                this,
                requestCode: 0,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // SmallIcon ueber ApplicationInfo holen — kein hardcoded Resource-Verweis noetig
            var iconResId = ApplicationInfo?.Icon ?? Android.Resource.Drawable.IcDialogInfo;

            Notification notification;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                notification = new Notification.Builder(this, channelId)
                    .SetContentTitle(title)
                    .SetContentText(body)
                    .SetStyle(new Notification.BigTextStyle().BigText(body))
                    .SetSmallIcon(iconResId)
                    .SetAutoCancel(true)
                    .SetContentIntent(pendingIntent)
                    .Build();
            }
            else
            {
#pragma warning disable CA1422 // Notification.Builder(Context) ist seit API 26 deprecated, aber API 24+25 brauchen es
                notification = new Notification.Builder(this)
                    .SetContentTitle(title)
                    .SetContentText(body)
                    .SetSmallIcon(iconResId)
                    .SetAutoCancel(true)
                    .SetContentIntent(pendingIntent)
                    .Build();
#pragma warning restore CA1422
            }

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.Notify(body.GetHashCode(), notification);
        }
        catch
        {
            // Best-Effort: Wenn das Bauen scheitert, schweigen — die Notification geht verloren,
            // aber die App stuerzt nicht ab.
        }
    }
}
