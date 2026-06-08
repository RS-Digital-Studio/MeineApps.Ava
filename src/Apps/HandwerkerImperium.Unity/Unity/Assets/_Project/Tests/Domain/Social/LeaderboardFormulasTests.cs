using NUnit.Framework;
using HandwerkerImperium.Domain.Social;

namespace HandwerkerImperium.Domain.Tests.Social
{
    /// <summary>
    /// Verifiziert die Leaderboard-Score-Signatur (HMAC): Sign/Verify-Roundtrip, Manipulations- und
    /// Falschschlüssel-Erkennung, Leerschlüssel-Sperre.
    /// </summary>
    [TestFixture]
    public class LeaderboardFormulasTests
    {
        private const string Key = "server-key-secret";
        private const string Pid = "player-uuid-9";

        [Test]
        public void Sign_Then_Verify_True()
        {
            string sig = LeaderboardFormulas.Sign(Pid, 12345, LeaderboardCategory.Income, 100L, Key);
            Assert.That(sig, Is.Not.Empty);
            Assert.That(LeaderboardFormulas.Verify(Pid, 12345, LeaderboardCategory.Income, 100L, Key, sig), Is.True);
        }

        [Test]
        public void Tamper_Score_Or_Category_Fails()
        {
            string sig = LeaderboardFormulas.Sign(Pid, 12345, LeaderboardCategory.Income, 100L, Key);
            Assert.That(LeaderboardFormulas.Verify(Pid, 99999, LeaderboardCategory.Income, 100L, Key, sig), Is.False);
            Assert.That(LeaderboardFormulas.Verify(Pid, 12345, LeaderboardCategory.Cash, 100L, Key, sig), Is.False);
        }

        [Test]
        public void WrongOrEmptyKey_Fails()
        {
            string sig = LeaderboardFormulas.Sign(Pid, 12345, LeaderboardCategory.Income, 100L, Key);
            Assert.That(LeaderboardFormulas.Verify(Pid, 12345, LeaderboardCategory.Income, 100L, "other-key", sig), Is.False);
            Assert.That(LeaderboardFormulas.Verify(Pid, 12345, LeaderboardCategory.Income, 100L, "", sig), Is.False);
        }
    }
}
