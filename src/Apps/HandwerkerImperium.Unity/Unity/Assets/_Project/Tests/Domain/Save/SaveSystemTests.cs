using System.Globalization;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;
using HandwerkerImperium.Domain.Save;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Tests.Save
{
    /// <summary>
    /// P1-Verifikation des Save-Schemas + HMAC-Anti-Cheat (CLAUDE.md §7 / GDD §12):
    /// Signatur sign/verify, Manipulations-/Falschschlüssel-Erkennung, kultur-invariante Kanonik,
    /// Schema-Migration, strukturelle Sanitize-Reparatur (klemmen statt wipen) + JSON-Roundtrip aller Slices.
    /// </summary>
    [TestFixture]
    public class SaveSystemTests
    {
        private const string KeyA = "device-key-AAAA-1111";
        private const string KeyB = "device-key-BBBB-2222";

        private static GameSave NewPopulatedSave()
        {
            var s = new GameSave { SchemaVersion = SaveMigrator.CurrentSchemaVersion, LastSeenUtcTicks = 638_000_000_000_000_000L };
            s.Economy.Money = 12345.67m;
            s.Economy.Gems = 42m;
            s.Stations.StationSpeedLevel = 3;
            s.Stations.CollectRadiusLevel = 1;
            s.Stations.CarryCapacityLevel = 2;
            s.Stations.Stations.Add(new StationSaveData { Id = "schreiner", Unlocked = true, Stock = 5 });
            s.Stations.Stations.Add(new StationSaveData { Id = "dachdecker", Unlocked = false, Stock = 0 });
            s.Workers.Workers.Add(new WorkerSaveData { StationId = "schreiner", Hired = true, Level = 2 });
            s.Orders.TotalServed = 137;
            s.Orders.PendingCount = 2;
            s.Restoration.Landmarks.Add(new LandmarkSaveData { Id = "marktplatz", PhasesComplete = 3, TotalPhases = 5 });
            s.Franchise.PrestigeCount = 1;
            s.Franchise.CityIndex = 1;
            s.Franchise.PrestigeMultiplier = 3m;
            s.Franchise.PrestigeCurrency = 8m;
            s.Town.CurrentStar = 4;
            s.Mastery.Level = 7;
            s.Mastery.Xp = 1234.5;
            s.Cosmetics.OwnedSkins.Add("default");
            s.Cosmetics.OwnedSkins.Add("premium");
            s.Cosmetics.ActiveSkin = "premium";
            s.Endgame.MeistergradGrade = 2;
            s.Endgame.Renommee = 333.33m;
            s.Perkboard.AvailableMarks = 7;
            s.Perkboard.PerkLevels.Add(1); s.Perkboard.PerkLevels.Add(0); s.Perkboard.PerkLevels.Add(3);
            s.Collection.CollectedMasterTools.Add("mt_golden_hammer");
            s.Progress.DailyLastClaimUtcTicks = 638_000_000_000_000_001L;
            s.Progress.DailyStreakDay = 4;
            s.Progress.PlayedStoryBeats.Add("intro");
            s.Progress.ClaimedAchievements.Add("orders_10");
            return s;
        }

        [Test]
        public void Sign_Then_Verify_True()
        {
            var s = NewPopulatedSave();
            SaveSignature.Sign(s, KeyA);
            Assert.That(s.Signature, Is.Not.Empty);
            Assert.That(SaveSignature.Verify(s, KeyA), Is.True);
        }

        [Test]
        public void Tamper_Money_Fails_Verify()
        {
            var s = NewPopulatedSave();
            SaveSignature.Sign(s, KeyA);
            s.Economy.Money += 1_000_000m; // Cheat
            Assert.That(SaveSignature.Verify(s, KeyA), Is.False);
        }

        [Test]
        public void Tamper_Prestige_Or_Stars_Fails_Verify()
        {
            var s1 = NewPopulatedSave(); SaveSignature.Sign(s1, KeyA); s1.Franchise.PrestigeCount = 3;
            Assert.That(SaveSignature.Verify(s1, KeyA), Is.False);

            var s2 = NewPopulatedSave(); SaveSignature.Sign(s2, KeyA); s2.Town.CurrentStar = 5;
            Assert.That(SaveSignature.Verify(s2, KeyA), Is.False);

            var s3 = NewPopulatedSave(); SaveSignature.Sign(s3, KeyA); s3.Orders.TotalServed += 1;
            Assert.That(SaveSignature.Verify(s3, KeyA), Is.False);
        }

        [Test]
        public void WrongKey_Fails_Verify()
        {
            var s = NewPopulatedSave();
            SaveSignature.Sign(s, KeyA);
            Assert.That(SaveSignature.Verify(s, KeyB), Is.False);
        }

        [Test]
        public void Null_Or_Empty_Signature_Fails_Verify()
        {
            var s = NewPopulatedSave();
            s.Signature = "";
            Assert.That(SaveSignature.Verify(s, KeyA), Is.False);
        }

        [Test]
        public void Canonical_Is_CultureInvariant_And_Deterministic()
        {
            var s = NewPopulatedSave();
            string p1 = SaveSignature.CanonicalPayload(s);
            string p2 = SaveSignature.CanonicalPayload(s);
            Assert.That(p1, Is.EqualTo(p2), "deterministisch");
            Assert.That(p1, Does.Contain("m12345.67"), "Money mit invariantem Dezimalpunkt");

            // Unter einer Komma-Kultur darf sich die Nutzlast NICHT ändern.
            var prev = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.That(SaveSignature.CanonicalPayload(s), Is.EqualTo(p1));
            }
            finally { Thread.CurrentThread.CurrentCulture = prev; }
        }

        [Test]
        public void Migrate_Raises_SchemaVersion_To_Current()
        {
            var s = new GameSave { SchemaVersion = 0 };
            SaveMigrator.Migrate(s);
            Assert.That(s.SchemaVersion, Is.EqualTo(SaveMigrator.CurrentSchemaVersion));
            Assert.That(SaveMigrator.IsCurrent(s), Is.True);

            var future = new GameSave { SchemaVersion = 99 };
            SaveMigrator.Migrate(future); // aus neuerer App -> auf aktuelle klemmen
            Assert.That(future.SchemaVersion, Is.EqualTo(SaveMigrator.CurrentSchemaVersion));
        }

        [Test]
        public void Sanitize_Clamps_OutOfRange()
        {
            var s = NewPopulatedSave();
            s.Economy.Money = -5m;
            s.Economy.Gems = -1m;
            s.Franchise.PrestigeCount = 9;
            s.Franchise.CityIndex = -2;
            s.Franchise.PrestigeMultiplier = 0m;
            s.Town.CurrentStar = 7;
            s.Mastery.Xp = -10;
            s.Orders.TotalServed = -3;
            s.Stations.Stations[0].Stock = -8;
            s.Restoration.Landmarks[0].PhasesComplete = 99; // > TotalPhases 5

            SaveSanitizer.Sanitize(s);

            Assert.That(s.Economy.Money, Is.EqualTo(0m));
            Assert.That(s.Economy.Gems, Is.EqualTo(0m));
            Assert.That(s.Franchise.PrestigeCount, Is.EqualTo(PrestigeFormulas.MaxPrestige));
            Assert.That(s.Franchise.CityIndex, Is.EqualTo(0));
            Assert.That(s.Franchise.PrestigeMultiplier, Is.EqualTo(1m));
            Assert.That(s.Town.CurrentStar, Is.EqualTo(5));
            Assert.That(s.Mastery.Xp, Is.EqualTo(0));
            Assert.That(s.Orders.TotalServed, Is.EqualTo(0));
            Assert.That(s.Stations.Stations[0].Stock, Is.EqualTo(0));
            Assert.That(s.Restoration.Landmarks[0].PhasesComplete, Is.EqualTo(5));
        }

        [Test]
        public void Sanitize_Is_Idempotent_And_LeavesValidSaveUntouched()
        {
            var valid = NewPopulatedSave();
            SaveSanitizer.Sanitize(valid);
            Assert.That(valid.Economy.Money, Is.EqualTo(12345.67m), "gültiger Save bleibt unverändert");
            Assert.That(valid.Town.CurrentStar, Is.EqualTo(4));
            Assert.That(valid.Restoration.Landmarks[0].PhasesComplete, Is.EqualTo(3));
        }

        [Test]
        public void Json_Roundtrip_Preserves_AllSlices_And_SignatureStillVerifies()
        {
            var s = NewPopulatedSave();
            SaveSignature.Sign(s, KeyA);

            string json = JsonConvert.SerializeObject(s);
            var loaded = JsonConvert.DeserializeObject<GameSave>(json);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Economy.Money, Is.EqualTo(12345.67m));
            Assert.That(loaded.Economy.Gems, Is.EqualTo(42m));
            Assert.That(loaded.Stations.StationSpeedLevel, Is.EqualTo(3));
            Assert.That(loaded.Stations.Stations.Count, Is.EqualTo(2));
            Assert.That(loaded.Stations.Stations[0].Id, Is.EqualTo("schreiner"));
            Assert.That(loaded.Workers.Workers[0].Level, Is.EqualTo(2));
            Assert.That(loaded.Orders.TotalServed, Is.EqualTo(137));
            Assert.That(loaded.Restoration.Landmarks[0].TotalPhases, Is.EqualTo(5));
            Assert.That(loaded.Franchise.PrestigeMultiplier, Is.EqualTo(3m));
            Assert.That(loaded.Town.CurrentStar, Is.EqualTo(4));
            Assert.That(loaded.Mastery.Level, Is.EqualTo(7));
            Assert.That(loaded.Cosmetics.ActiveSkin, Is.EqualTo("premium"));
            Assert.That(loaded.LastSeenUtcTicks, Is.EqualTo(638_000_000_000_000_000L));
            Assert.That(loaded.Endgame.MeistergradGrade, Is.EqualTo(2));
            Assert.That(loaded.Endgame.Renommee, Is.EqualTo(333.33m));
            Assert.That(loaded.Perkboard.AvailableMarks, Is.EqualTo(7));
            Assert.That(loaded.Perkboard.PerkLevels, Is.EqualTo(new[] { 1, 0, 3 }).AsCollection);
            Assert.That(loaded.Collection.CollectedMasterTools[0], Is.EqualTo("mt_golden_hammer"));
            Assert.That(loaded.Progress.DailyStreakDay, Is.EqualTo(4));
            Assert.That(loaded.Progress.ClaimedAchievements[0], Is.EqualTo("orders_10"));

            // Signatur überlebt den Roundtrip (decimal/Format stabil).
            Assert.That(SaveSignature.Verify(loaded, KeyA), Is.True);
        }

        [Test]
        public void Tamper_PreviouslyUnsignedFields_NowDetected()
        {
            // Felder, die frueher NICHT signiert waren (Anti-Cheat-Loch) -> muessen jetzt erkannt werden.
            var a = NewPopulatedSave(); SaveSignature.Sign(a, KeyA); a.Franchise.PrestigeMultiplier = 999m;
            Assert.That(SaveSignature.Verify(a, KeyA), Is.False, "PrestigeMultiplier");

            var b = NewPopulatedSave(); SaveSignature.Sign(b, KeyA); b.Stations.StationSpeedLevel = 50;
            Assert.That(SaveSignature.Verify(b, KeyA), Is.False, "Stations-Level");

            var c = NewPopulatedSave(); SaveSignature.Sign(c, KeyA); c.Stations.Stations[0].Stock = 99999;
            Assert.That(SaveSignature.Verify(c, KeyA), Is.False, "Station-Stock");

            var d = NewPopulatedSave(); SaveSignature.Sign(d, KeyA); d.Stations.Stations[1].Unlocked = true;
            Assert.That(SaveSignature.Verify(d, KeyA), Is.False, "Station-Unlock");

            var e = NewPopulatedSave(); SaveSignature.Sign(e, KeyA); e.Workers.Workers[0].Hired = false;
            Assert.That(SaveSignature.Verify(e, KeyA), Is.False, "Worker-Hired");

            var g = NewPopulatedSave(); SaveSignature.Sign(g, KeyA); g.Mastery.Level = 999;
            Assert.That(SaveSignature.Verify(g, KeyA), Is.False, "Mastery-Level");

            var h = NewPopulatedSave(); SaveSignature.Sign(h, KeyA); h.Restoration.Landmarks[0].PhasesComplete = 5;
            Assert.That(SaveSignature.Verify(h, KeyA), Is.False, "Restoration-Fortschritt");

            var i = NewPopulatedSave(); SaveSignature.Sign(i, KeyA); i.Franchise.PrestigeCurrency = 777m;
            Assert.That(SaveSignature.Verify(i, KeyA), Is.False, "Prestige-Waehrung");

            var j = NewPopulatedSave(); SaveSignature.Sign(j, KeyA); j.Endgame.MeistergradGrade = 99;
            Assert.That(SaveSignature.Verify(j, KeyA), Is.False, "Meistergrad");

            var k = NewPopulatedSave(); SaveSignature.Sign(k, KeyA); k.Endgame.Renommee += 1_000_000m;
            Assert.That(SaveSignature.Verify(k, KeyA), Is.False, "Renommee");

            var l = NewPopulatedSave(); SaveSignature.Sign(l, KeyA); l.Perkboard.AvailableMarks = 9999;
            Assert.That(SaveSignature.Verify(l, KeyA), Is.False, "Perkboard-Marken");

            var n = NewPopulatedSave(); SaveSignature.Sign(n, KeyA); n.Collection.CollectedMasterTools.Add("mt_master_crown");
            Assert.That(SaveSignature.Verify(n, KeyA), Is.False, "Master-Tool-Sammlung");

            var o = NewPopulatedSave(); SaveSignature.Sign(o, KeyA); o.Progress.ClaimedAchievements.Add("orders_100");
            Assert.That(SaveSignature.Verify(o, KeyA), Is.False, "eingeloeste Achievements");
        }

        [Test]
        public void EmptyOrNullDeviceKey_FailsVerify_NoBypass()
        {
            var s = NewPopulatedSave();
            SaveSignature.Sign(s, KeyA);
            Assert.That(SaveSignature.Verify(s, ""), Is.False, "Leerschluessel darf nicht bestaetigen");
            Assert.That(SaveSignature.Verify(s, null!), Is.False, "Null-Schluessel darf nicht bestaetigen");
            Assert.That(SaveSignature.Verify(s, KeyA), Is.True);
        }

        [Test]
        public void Verify_OnNullSlice_DoesNotCrash()
        {
            var s = NewPopulatedSave();
            SaveSignature.Sign(s, KeyA);
            s.Economy = null!; // simuliert "Economy": null aus korruptem JSON
            Assert.That(SaveSignature.Verify(s, KeyA), Is.False); // kein NRE, einfach ungueltig
        }

        [Test]
        public void Sanitize_NullSlices_AreRepaired_NoCrash()
        {
            var s = NewPopulatedSave();
            s.Economy = null!;
            s.Town = null!;
            s.Stations = null!;
            s.Restoration = null!;
            SaveSanitizer.Sanitize(s);
            Assert.That(s.Economy, Is.Not.Null);
            Assert.That(s.Town, Is.Not.Null);
            Assert.That(s.Town.CurrentStar, Is.EqualTo(1), "Default-Stern gueltig");
            Assert.That(s.Stations, Is.Not.Null);
            Assert.That(s.Stations.Stations, Is.Not.Null);
            Assert.That(s.Restoration.Landmarks, Is.Not.Null);
        }

        [Test]
        public void Sanitize_ClampsPrestigeMultiplierUpper_And_RepairsNaNXp()
        {
            var s = NewPopulatedSave();
            s.Franchise.PrestigeMultiplier = 1_000_000_000m;
            s.Mastery.Xp = double.NaN;
            SaveSanitizer.Sanitize(s);
            Assert.That(s.Franchise.PrestigeMultiplier, Is.EqualTo(SaveSanitizer.MaxPrestigeMultiplier));
            Assert.That(s.Mastery.Xp, Is.EqualTo(0.0), "NaN repariert");
        }
    }
}
