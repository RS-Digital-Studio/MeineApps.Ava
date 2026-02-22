namespace BomberBlast.Models;

/// <summary>
/// Master-Container für Cloud Save.
/// Enthält alle Persistenz-Keys als Dictionary, plus Metadaten für Konflikt-Resolution.
/// Wird als ein JSON-Blob in Google Play Games Snapshots gespeichert (~10-20KB).
/// </summary>
public class CloudSaveData
{
    /// <summary>Schema-Version für Migrations-Kompatibilität</summary>
    public int Version { get; set; } = 1;

    /// <summary>Zeitstempel der letzten Speicherung (UTC, ISO 8601 "O")</summary>
    public string TimestampUtc { get; set; } = "";

    /// <summary>Gesamtsterne als Haupt-Fortschritts-Indikator</summary>
    public int TotalStars { get; set; }

    /// <summary>Coin-Balance zum Zeitpunkt der Speicherung</summary>
    public int CoinBalance { get; set; }

    /// <summary>Gem-Balance zum Zeitpunkt der Speicherung</summary>
    public int GemBalance { get; set; }

    /// <summary>Anzahl gesammelter Karten (für Konflikt-Resolution)</summary>
    public int TotalCards { get; set; }

    /// <summary>
    /// Alle Persistenz-Keys als Schlüssel-Wert-Paare.
    /// Key = IPreferencesService-Key (z.B. "GameProgress", "CoinData")
    /// Value = JSON-String oder einfacher Wert
    /// </summary>
    public Dictionary<string, string> Keys { get; set; } = new();

    /// <summary>
    /// Vergleicht zwei Speicherstände und gibt den "besseren" zurück.
    /// Reihenfolge: TotalStars → CoinBalance + GemBalance → Timestamp (neuer gewinnt).
    /// </summary>
    public static CloudSaveData ChooseBest(CloudSaveData local, CloudSaveData cloud)
    {
        // 1. Mehr Sterne = mehr Fortschritt
        if (local.TotalStars != cloud.TotalStars)
            return local.TotalStars > cloud.TotalStars ? local : cloud;

        // 2. Mehr Währung = mehr Spielzeit
        int localWealth = local.CoinBalance + local.GemBalance * 100; // Gems gewichten
        int cloudWealth = cloud.CoinBalance + cloud.GemBalance * 100;
        if (localWealth != cloudWealth)
            return localWealth > cloudWealth ? local : cloud;

        // 3. Mehr Karten = mehr Sammlung
        if (local.TotalCards != cloud.TotalCards)
            return local.TotalCards > cloud.TotalCards ? local : cloud;

        // 4. Neuerer Timestamp
        return string.CompareOrdinal(local.TimestampUtc, cloud.TimestampUtc) >= 0 ? local : cloud;
    }
}
