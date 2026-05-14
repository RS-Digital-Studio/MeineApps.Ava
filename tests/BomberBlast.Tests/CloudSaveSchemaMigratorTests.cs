using BomberBlast.Models;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für CloudSaveSchemaMigrator (v2.0.44 — ).
/// Validiert: V1→V2 Migration füllt fehlende Keys mit Defaults,
/// Validation erkennt korrupte Werte (negativ / unplausibel hoch),
/// CurrentSchemaVersion wird beim Build verwendet.
/// </summary>
public class CloudSaveSchemaMigratorTests
{
    [Fact]
    public void TryMigrateAndValidate_V1Snapshot_HebtAufV2()
    {
        var data = new CloudSaveData
        {
            Version = 1,
            CoinBalance = 1000,
            GemBalance = 50,
            TotalStars = 30,
            TotalCards = 10,
            TimestampUtc = "2025-01-01T00:00:00Z"
        };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out var error);

        success.Should().BeTrue($"Migration sollte erfolgreich sein, war aber: {error}");
        data.Version.Should().Be(CloudSaveSchemaMigrator.CurrentSchemaVersion);
    }

    [Fact]
    public void TryMigrateAndValidate_V1Snapshot_FuelltFehlendeKeysMitDefaults()
    {
        var data = new CloudSaveData
        {
            Version = 1,
            Keys = new Dictionary<string, string> { ["GameProgress"] = "{}" }
        };

        CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out _);

        data.Keys.Should().ContainKey("master_mode_status_v1");
        data.Keys.Should().ContainKey("master_mode_active");
        data.Keys.Should().ContainKey("deck_telemetry_v1");
        data.Keys.Should().ContainKey("LoadoutData");
        data.Keys.Should().ContainKey("BossRushData");
    }

    [Fact]
    public void TryMigrateAndValidate_V1MitMasterModeKeys_BehaeltVorhandeneWerte()
    {
        var data = new CloudSaveData
        {
            Version = 1,
            Keys = new Dictionary<string, string>
            {
                ["master_mode_active"] = "true",
                ["master_mode_status_v1"] = "{\"customdata\":1}"
            }
        };

        CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out _);

        data.Keys["master_mode_active"].Should().Be("true");
        data.Keys["master_mode_status_v1"].Should().Be("{\"customdata\":1}");
    }

    [Fact]
    public void TryMigrateAndValidate_NegativeCoins_LehntDatenAb()
    {
        var data = new CloudSaveData
        {
            Version = 1,
            CoinBalance = -100,
            GemBalance = 0,
            TotalStars = 0,
            TotalCards = 0
        };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Negative Werte");
    }

    [Fact]
    public void TryMigrateAndValidate_TotalStarsUeber300_LehntDatenAb()
    {
        var data = new CloudSaveData { Version = 1, TotalStars = 999 };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out var error);

        success.Should().BeFalse();
        error.Should().Contain("TotalStars");
    }

    [Fact]
    public void TryMigrateAndValidate_PlausibilityCapUeber10Mio_LehntDatenAb()
    {
        var data = new CloudSaveData
        {
            Version = 1,
            CoinBalance = 11_000_000  // > 10M Plausibility-Cap
        };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Plausibilitäts");
    }

    [Fact]
    public void TryMigrateAndValidate_AktuelleVersion_PassiertDurch()
    {
        var data = new CloudSaveData
        {
            Version = CloudSaveSchemaMigrator.CurrentSchemaVersion,
            CoinBalance = 5000,
            GemBalance = 100,
            TotalStars = 50
        };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out var error);

        success.Should().BeTrue($"Aktuelle Version sollte ohne Migration validieren, Fehler war: {error}");
    }

    [Fact]
    public void TryMigrateAndValidate_NullKeys_WirdAlsLeeresDictionaryBehandelt()
    {
        // Defensive: Alte Snapshots haben Keys möglicherweise nicht initialisiert
        var data = new CloudSaveData { Version = 1, Keys = null! };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out _);

        success.Should().BeTrue();
        data.Keys.Should().NotBeNull();
        data.Keys.Should().ContainKey("master_mode_status_v1");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // V3 (v2.0.46): Accessibility + Analytics-Consent
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TryMigrateAndValidate_V1ZuV3_DurchwandertBeideMigratoren()
    {
        var data = new CloudSaveData { Version = 1 };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out var error);

        success.Should().BeTrue($"V1→V3 muss durchgehen, war: {error}");
        data.Version.Should().Be(3);
        // V1→V2 Keys
        data.Keys.Should().ContainKey("master_mode_status_v1");
        // V2→V3 Keys
        data.Keys.Should().ContainKey("Accessibility_ColorblindMode");
        data.Keys.Should().ContainKey("AnalyticsConsent");
    }

    [Fact]
    public void TryMigrateAndValidate_V2Snapshot_HebtAufV3()
    {
        var data = new CloudSaveData
        {
            Version = 2,
            Keys = new Dictionary<string, string> { ["master_mode_active"] = "true" }
        };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out _);

        success.Should().BeTrue();
        data.Version.Should().Be(3);
        data.Keys["master_mode_active"].Should().Be("true", "Bestand-Keys bleiben unverändert");
        data.Keys["Accessibility_ColorblindMode"].Should().Be("Off");
        data.Keys["AnalyticsConsent"].Should().Be("false", "DSGVO: Consent default false");
        data.Keys["CrashlyticsConsent"].Should().Be("false");
        data.Keys["TargetFrameRate"].Should().Be("30", "Default 30 FPS Battery-Mode");
    }

    [Fact]
    public void TryMigrateAndValidate_V2MitAccessibilitySettings_BehaeltVorhandeneWerte()
    {
        var data = new CloudSaveData
        {
            Version = 2,
            Keys = new Dictionary<string, string>
            {
                ["Accessibility_ColorblindMode"] = "Deuteranopia",
                ["Accessibility_UiScale"] = "1.25",
                ["AnalyticsConsent"] = "true"
            }
        };

        CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out _);

        data.Keys["Accessibility_ColorblindMode"].Should().Be("Deuteranopia");
        data.Keys["Accessibility_UiScale"].Should().Be("1.25");
        data.Keys["AnalyticsConsent"].Should().Be("true");
    }

    [Fact]
    public void TryMigrateAndValidate_AktuelleVersion3_PassiertOhneMigration()
    {
        var data = new CloudSaveData { Version = 3 };

        var success = CloudSaveSchemaMigrator.TryMigrateAndValidate(data, out _);

        success.Should().BeTrue();
        data.Version.Should().Be(3);
    }
}
