using Android.App;
using Android.Content;

namespace HandwerkerImperium.Android;

/// <summary>
/// Empfängt BOOT_COMPLETED nach Geräte-Neustart und plant
/// persistierte Benachrichtigungen erneut über AlarmManager.
/// Ohne diesen Receiver gehen alle geplanten Alarme bei Neustart verloren.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false,
    Permission = "android.permission.RECEIVE_BOOT_COMPLETED")]
[IntentFilter([Intent.ActionBootCompleted])]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        if (intent?.Action != Intent.ActionBootCompleted) return;

        AndroidNotificationService.RescheduleFromPreferences(context);
    }
}
