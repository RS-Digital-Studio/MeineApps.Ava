#nullable enable
using System;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Save;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class SaveMigratorTests
    {
        [Test]
        public void MigrationSetztSchemaVersionAufAktuell()
        {
            var save = new PlayerSave(new PlayerProfile("u", "Test", "Poseidon", DateTime.UtcNow));
            save.SchemaVersion = 1;
            SaveMigrator.MigrateToCurrent(save);
            Assert.AreEqual(SaveMigrator.CurrentSchemaVersion, save.SchemaVersion);
        }

        [Test]
        public void NeuerSaveIstBereitsAktuell()
        {
            var save = new PlayerSave(new PlayerProfile("u", "Test", "Poseidon", DateTime.UtcNow));
            Assert.AreEqual(SaveMigrator.CurrentSchemaVersion, save.SchemaVersion);
        }

        [Test]
        public void V2SlicesSindNichtNullAufNeuemSave()
        {
            var save = new PlayerSave(new PlayerProfile("u", "Test", "Poseidon", DateTime.UtcNow));
            Assert.NotNull(save.Tutorial);
            Assert.NotNull(save.Achievements);
            Assert.NotNull(save.FriendsSlice);
            Assert.NotNull(save.ChatSlice);
            Assert.NotNull(save.PendingClaims);
            Assert.NotNull(save.PackPityCounters);
            Assert.NotNull(save.UnlockedFeatureKeys);
            Assert.NotNull(save.SaisonPassXp);
        }

        [Test]
        public void MigrationIstIdempotent()
        {
            var save = new PlayerSave(new PlayerProfile("u", "Test", "Poseidon", DateTime.UtcNow));
            SaveMigrator.MigrateToCurrent(save);
            SaveMigrator.MigrateToCurrent(save);
            Assert.AreEqual(SaveMigrator.CurrentSchemaVersion, save.SchemaVersion);
        }
    }
}
