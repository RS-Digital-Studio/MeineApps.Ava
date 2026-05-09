namespace BomberBlast.Models;

/// <summary>
/// Schema-Migrator für CloudSaveData. Wendet ordered Migrations auf alte Cloud-Snapshots an,
/// damit eine v2.0.41-Cloud auf v2.0.44 ohne Datenverlust geladen werden kann.
///
/// Pattern: jeder Migrator hebt von <c>FromVersion</c> auf <c>FromVersion+1</c> an.
/// Validation am Ende prüft Pflicht-Keys + Wertebereiche.
/// </summary>
public static class CloudSaveSchemaMigrator
{
    /// <summary>Aktuelle Schema-Version. Bei jeder Erweiterung erhöhen + Migrator anhängen.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>
    /// Migriert das übergebene CloudSaveData-Objekt auf die aktuelle Schema-Version.
    /// Returnt true wenn alle Migrations erfolgreich + Validation passt.
    /// Bei Fehler wird das Objekt nicht angewendet (CloudSaveService fällt auf lokalen State).
    /// </summary>
    public static bool TryMigrateAndValidate(CloudSaveData data, out string? error)
    {
        error = null;
        try
        {
            // Schritt 1: Ordered Migrations
            while (data.Version < CurrentSchemaVersion)
            {
                bool migrated = data.Version switch
                {
                    1 => MigrateV1ToV2(data),
                    2 => MigrateV2ToV3(data),
                    _ => false
                };

                if (!migrated)
                {
                    error = $"Keine Migration für Schema-Version {data.Version}.";
                    return false;
                }

                // Endlosschleifen-Schutz: Version MUSS sich erhöhen
                if (data.Version >= CurrentSchemaVersion) break;
            }

            // Schritt 2: Validation nach Migration
            return Validate(data, out error);
        }
        catch (Exception ex)
        {
            error = $"Migration fehlgeschlagen: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// V1 → V2: v2.0.41+ neue Keys mit Defaults füllen wenn fehlend.
    /// (master_mode_status_v1, master_mode_active, deck_telemetry_v1 wurden in v2.0.34/v2.0.35 hinzugefügt.)
    /// </summary>
    private static bool MigrateV1ToV2(CloudSaveData data)
    {
        // Defensive: alte Snapshots haben Keys-Dictionary möglicherweise nicht initialisiert
        data.Keys ??= new Dictionary<string, string>();

        // Defaults für v2.0.34+ Keys
        if (!data.Keys.ContainsKey("master_mode_status_v1"))
            data.Keys["master_mode_status_v1"] = "{}";  // leeres JSON-Object
        if (!data.Keys.ContainsKey("master_mode_active"))
            data.Keys["master_mode_active"] = "false";
        if (!data.Keys.ContainsKey("deck_telemetry_v1"))
            data.Keys["deck_telemetry_v1"] = "{}";

        // Defaults für v2.0.41+ Keys (Loadout/BossRush/DailyRace/Events sind alle eigene JSON-Objekte)
        if (!data.Keys.ContainsKey("LoadoutData"))
            data.Keys["LoadoutData"] = "{}";
        if (!data.Keys.ContainsKey("BossRushData"))
            data.Keys["BossRushData"] = "{}";
        if (!data.Keys.ContainsKey("DungeonStatsData") && data.Keys.ContainsKey("DungeonRunData"))
            data.Keys["DungeonStatsData"] = "{}";

        data.Version = 2;
        return true;
    }

    /// <summary>
    /// V2 → V3 (v2.0.46): Accessibility-Settings + Analytics-Consent-Flags.
    /// Diese Settings wandern jetzt mit dem Cloud-Save mit, damit der User auf
    /// einem neuen Gerät seine Sehhilfe-Konfiguration behält.
    /// </summary>
    private static bool MigrateV2ToV3(CloudSaveData data)
    {
        data.Keys ??= new Dictionary<string, string>();

        // Accessibility-Defaults
        if (!data.Keys.ContainsKey("Accessibility_ColorblindMode"))
            data.Keys["Accessibility_ColorblindMode"] = "Off";
        if (!data.Keys.ContainsKey("Accessibility_HighContrast"))
            data.Keys["Accessibility_HighContrast"] = "false";
        if (!data.Keys.ContainsKey("Accessibility_UiScale"))
            data.Keys["Accessibility_UiScale"] = "1";
        if (!data.Keys.ContainsKey("Accessibility_Subtitles"))
            data.Keys["Accessibility_Subtitles"] = "false";

        // Frame-Rate-Setting (v2.0.44, default 30 FPS Battery-Mode)
        if (!data.Keys.ContainsKey("TargetFrameRate"))
            data.Keys["TargetFrameRate"] = "30";

        // Analytics-Consent (DSGVO-Pflicht — default false bis User explizit zustimmt)
        if (!data.Keys.ContainsKey("AnalyticsConsent"))
            data.Keys["AnalyticsConsent"] = "false";
        if (!data.Keys.ContainsKey("CrashlyticsConsent"))
            data.Keys["CrashlyticsConsent"] = "false";

        data.Version = 3;
        return true;
    }

    /// <summary>
    /// Plausibilitäts-Validation: Wertebereiche, Mindestschlüssel.
    /// </summary>
    private static bool Validate(CloudSaveData data, out string? error)
    {
        error = null;

        if (data.Version != CurrentSchemaVersion)
        {
            error = $"Schema-Version {data.Version} != erwartet {CurrentSchemaVersion}.";
            return false;
        }

        if (data.Keys == null)
        {
            error = "Keys-Dictionary ist null.";
            return false;
        }

        // Negative Wealth → Korruption (Underflow auf int.MinValue möglich bei alten Builds)
        if (data.CoinBalance < 0 || data.GemBalance < 0 || data.TotalStars < 0 || data.TotalCards < 0)
        {
            error = $"Negative Werte erkannt (Coin={data.CoinBalance}, Gem={data.GemBalance}, Stars={data.TotalStars}, Cards={data.TotalCards}).";
            return false;
        }

        // Plausibilitäts-Cap (Anti-Cheat-Erkennung): >10 Mio Coins/Gems sind in normalem Spiel kaum erreichbar
        const int Plausibility = 10_000_000;
        if (data.CoinBalance > Plausibility || data.GemBalance > Plausibility)
        {
            error = $"Werte überschreiten Plausibilitäts-Cap ({Plausibility}).";
            return false;
        }

        // TotalStars: max 300 (100 Story-Levels × 3 Sterne)
        if (data.TotalStars > 300)
        {
            error = $"TotalStars {data.TotalStars} > 300 (Maximum).";
            return false;
        }

        return true;
    }
}
