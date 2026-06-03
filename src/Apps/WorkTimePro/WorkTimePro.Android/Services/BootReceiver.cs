using Android.App;
using Android.Content;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using WorkTimePro.Services;

namespace WorkTimePro.Android.Services;

/// <summary>
/// Plant die geplanten Reminder nach einem Geräte-Neustart (oder App-Update) neu.
/// AlarmManager-Alarme werden beim Reboot vom System verworfen — ohne diesen Receiver
/// entfiele z.B. die Morgen-Erinnerung nach einem Neustart über Nacht, bis der Nutzer
/// die App das nächste Mal manuell öffnet.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        if (intent?.Action != Intent.ActionBootCompleted &&
            intent?.Action != Intent.ActionMyPackageReplaced)
            return;

        // Async-Arbeit über GoAsync() abwickeln (OnReceive selbst muss schnell zurückkehren).
        var pending = GoAsync();
        _ = RescheduleAsync().ContinueWith(_ => pending.Finish());
    }

    private static async Task RescheduleAsync()
    {
        try
        {
            // Minimaler Service-Graph: App.Services (Avalonia-DI) existiert beim Boot-Broadcast
            // nicht, da die App nicht über den Launcher gestartet wurde.
            var database = new DatabaseService();
            var preferences = new PreferencesService("WorkTimePro");
            var localization = new LocalizationService(
                WorkTimePro.Resources.Strings.AppStrings.ResourceManager, preferences);
            localization.Initialize();
            var notification = new AndroidNotificationService();
            var calculation = new CalculationService(database);
            var tracking = new TimeTrackingService(database, calculation);

            using var reminder = new ReminderService(notification, tracking, database, localization);
            await reminder.InitializeAsync();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("WorkTimePro", $"BootReceiver reschedule failed: {ex.Message}");
        }
    }
}
