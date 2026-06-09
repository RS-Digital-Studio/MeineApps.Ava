using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Runtime;
using HandwerkerImperium.Game;

namespace HandwerkerImperium.Game.Tests
{
    /// <summary>
    /// Verifiziert die Game-Layer-Persistenz: GameModel → PlayerPrefs(JSON+HMAC) → GameModel zurück,
    /// kein Save → null, und Reparatur-statt-Wipe bei ungültiger Signatur (kein Crash).
    /// </summary>
    public sealed class RuntimeSaveTests
    {
        private const string Key = "test-device-key-runtime";

        [Test]
        public void Save_Then_Load_PreservesState()
        {
            RuntimeSave.Clear();
            try
            {
                var idleBal = new IdleBalancing();
                var m = GameModel.CreateNew(idleBal);
                m.Idle.Money = 4242m; m.Gems = 9m;
                m.Meta.MasteryLevel = 5; m.Meta.PrestigeCount = 1; m.Meta.PrestigeMultiplier = 3m;
                m.Idle.Stations[0].Stock = 4;
                RuntimeSave.Save(m, Key);

                var loaded = RuntimeSave.Load(Key, idleBal);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Idle.Money, Is.EqualTo(4242m));
                Assert.That(loaded.Gems, Is.EqualTo(9m));
                Assert.That(loaded.Meta.MasteryLevel, Is.EqualTo(5));
                Assert.That(loaded.Meta.PrestigeMultiplier, Is.EqualTo(3m));
                Assert.That(loaded.Idle.Stations[0].Stock, Is.EqualTo(4));
            }
            finally { RuntimeSave.Clear(); }
        }

        [Test]
        public void Load_NoSave_ReturnsNull()
        {
            RuntimeSave.Clear();
            Assert.That(RuntimeSave.Load(Key, new IdleBalancing()), Is.Null);
        }

        [Test]
        public void Load_InvalidSignature_RepairsNotWipe_NoCrash()
        {
            RuntimeSave.Clear();
            try
            {
                var idleBal = new IdleBalancing();
                var m = GameModel.CreateNew(idleBal);
                m.Idle.Money = 1000m;
                RuntimeSave.Save(m, Key);
                // Falscher Geräteschlüssel -> Signatur ungueltig -> Sanitize-Reparatur, kein Crash, Modell zurueck.
                var loaded = RuntimeSave.Load("wrong-key", idleBal);
                Assert.That(loaded, Is.Not.Null, "Reparatur statt Wipe");
                Assert.That(loaded.Idle.Money, Is.GreaterThanOrEqualTo(0m));
            }
            finally { RuntimeSave.Clear(); }
        }
    }
}
