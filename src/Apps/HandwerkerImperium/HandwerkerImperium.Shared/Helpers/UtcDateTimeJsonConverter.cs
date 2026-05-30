using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HandwerkerImperium.Helpers;

/// <summary>
/// Erzwingt UTC fuer alle persistierten DateTime-Werte im Spielstand.
///
/// System.Text.Json deserialisiert ISO-8601-Strings mit "Z"/Offset standardmaessig zu
/// <see cref="DateTimeKind.Local"/> und konvertiert dabei in die lokale Geraete-Zeit. Der Code
/// vergleicht Reset-Zeitpunkte aber per <c>.Date</c> gegen <c>DateTime.UtcNow.Date</c> — nach einem
/// App-Neustart verschiebt sich die Tagesgrenze dann um den UTC-Offset des Geraets. Folge: Daily-/
/// Weekly-/QuickJob-Resets feuern bis zu einen Kalendertag versetzt oder werden uebersprungen.
///
/// Dieser Converter liest jeden Wert deterministisch als UTC zurueck (interpretiert Unspecified als
/// UTC, konvertiert Local) und schreibt ihn im Roundtrip-Format "O". Damit ist die gesamte
/// DateTime-Persistenz zeitzonenunabhaengig — analog zum projektweiten DateTimeStyles.RoundtripKind.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ToUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtc(value).ToString("O", CultureInfo.InvariantCulture));
}
