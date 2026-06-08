#nullable enable
using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Notifications
{
    /// <summary>
    /// Ein einzelnes Notification-Item im Notification-Center (Bell). IDs sind dedupliziert.
    /// 1:1-Port aus dem Avalonia-Original (Models/NotificationItem.cs). NotificationKind-Enum ist in
    /// NotificationKind.cs (Schicht 10). Reine Persistenz-Daten; die Lokalisierungs-/Icon-Auflösung
    /// (TitleKey/BodyKey/IconKind → Text/Bitmap) übernimmt die Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class NotificationItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("kind")]
        public NotificationKind Kind { get; set; }

        /// <summary>Resource-Key für den Titel. Wird vom ViewModel zur Laufzeit lokalisiert.</summary>
        [JsonProperty("titleKey")]
        public string TitleKey { get; set; } = "";

        /// <summary>Optionale Format-Argumente für den Titel (z.B. Achievement-Name).</summary>
        [JsonProperty("titleArg")]
        public string? TitleArg { get; set; }

        /// <summary>Resource-Key für den Body-Text.</summary>
        [JsonProperty("bodyKey")]
        public string BodyKey { get; set; } = "";

        /// <summary>Optionale Format-Argumente für den Body.</summary>
        [JsonProperty("bodyArg")]
        public string? BodyArg { get; set; }

        /// <summary>Erstellungs-Zeitpunkt (UTC, ISO 8601 "O").</summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Wurde das Item bereits in der Bell angezeigt (gesehen)?</summary>
        [JsonProperty("seen")]
        public bool Seen { get; set; }

        /// <summary>Optionaler GameIcon-Name für das Item. Wird vom ViewModel auf das passende Bitmap gemappt.</summary>
        [JsonProperty("iconKind")]
        public string? IconKind { get; set; }
    }
}
