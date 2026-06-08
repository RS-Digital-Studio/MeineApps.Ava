using System.Globalization;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;
using HandwerkerImperium.Domain.Save;

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
            Assert.That(s.Franchise.PrestigeCount, Is.EqualTo(SaveSanitizer.MaxPrestige));
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

            // Signatur überlebt den Roundtrip (decimal/Format stabil).
            Assert.That(SaveSignature.Verify(loaded, KeyA), Is.True);
        }
    }
}
