using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace HandwerkerImperium.Android;

/// <summary>
/// BroadcastReceiver für geplante Benachrichtigungen.
/// Wird von AlarmManager aufgerufen und zeigt die Notification an.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class NotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var notificationId = intent.GetIntExtra("notification_id", 0);
        var messageKey = intent.GetStringExtra("message_key") ?? "";
        var channelId = intent.GetStringExtra("channel_id") ?? "handwerker_game";

        // Lokalisierte Nachricht laden (Fallback auf Key)
        var message = GetLocalizedMessage(context, messageKey);

        var notificationIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
        var pendingIntent = PendingIntent.GetActivity(
            context,
            0,
            notificationIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, channelId)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle("Handwerker Imperium")
            .SetContentText(message)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault);

        var manager = NotificationManagerCompat.From(context);
        manager.Notify(notificationId, builder.Build());
    }

    private static string GetLocalizedMessage(Context context, string messageKey)
    {
        // Lokalisierte Benachrichtigungstexte basierend auf Gerätesprache
        var locale = Java.Util.Locale.Default?.Language ?? "en";
        return messageKey switch
        {
            "ResearchDoneNotif" => locale switch
            {
                "de" => "Forschung abgeschlossen! Hole deine Ergebnisse ab.",
                "es" => "Investigacion completada! Recoge tus resultados.",
                "fr" => "Recherche terminee ! Collectez vos resultats.",
                "it" => "Ricerca completata! Raccogli i tuoi risultati.",
                "pt" => "Pesquisa concluida! Recolha os seus resultados.",
                _ => "Research complete! Come collect your results."
            },
            "DeliveryWaitingNotif" => locale switch
            {
                "de" => "Ein Lieferant wartet mit einer Lieferung!",
                "es" => "Un proveedor espera con una entrega!",
                "fr" => "Un fournisseur attend avec une livraison !",
                "it" => "Un fornitore aspetta con una consegna!",
                "pt" => "Um fornecedor espera com uma entrega!",
                _ => "A supplier is waiting with a delivery!"
            },
            "RushAvailableNotif" => locale switch
            {
                "de" => "Feierabend-Rush verfügbar! Verdopple jetzt dein Einkommen.",
                "es" => "Hora punta disponible! Duplica tus ingresos ahora.",
                "fr" => "Rush disponible ! Doublez vos revenus maintenant.",
                "it" => "Rush disponibile! Raddoppia le tue entrate ora.",
                "pt" => "Rush disponivel! Duplique a sua renda agora.",
                _ => "Rush hour is available! Double your income now."
            },
            "DailyRewardNotif" => locale switch
            {
                "de" => "Deine tägliche Belohnung wartet! Nicht verpassen.",
                "es" => "Tu recompensa diaria te espera! No te la pierdas.",
                "fr" => "Votre recompense quotidienne vous attend !",
                "it" => "La tua ricompensa giornaliera ti aspetta!",
                "pt" => "A sua recompensa diaria esta a espera!",
                _ => "Your daily reward is waiting! Don't miss it."
            },
            _ => messageKey
        };
    }
}
