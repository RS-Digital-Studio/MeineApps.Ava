using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace ZeitManager.Android.Services;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback, Exported = false)]
public class TimerForegroundService : Service
{
    private const int NotificationId = 9001;
    private const string ChannelId = "zeitmanager_timer";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var timerName = intent?.GetStringExtra("timer_name") ?? "Timer";
        var remaining = intent?.GetStringExtra("remaining") ?? "";

        var notification = CreateNotification(timerName, remaining);
        StartForeground(NotificationId, notification);

        return StartCommandResult.NotSticky;
    }

    private Notification CreateNotification(string title, string body)
    {
        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetOngoing(true);
        builder.SetCategory(NotificationCompat.CategoryService);
        builder.SetPriority(NotificationCompat.PriorityLow);

        return builder.Build()
            ?? throw new InvalidOperationException("NotificationCompat.Builder lieferte keine Notification.");
    }

    public static void UpdateNotification(Context context, string timerName, string remaining)
    {
        var intent = new Intent(context, typeof(TimerForegroundService));
        intent.PutExtra("timer_name", timerName);
        intent.PutExtra("remaining", remaining);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }

    public static void StopService(Context context)
    {
        var intent = new Intent(context, typeof(TimerForegroundService));
        context.StopService(intent);
    }
}
