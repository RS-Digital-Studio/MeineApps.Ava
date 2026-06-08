using NUnit.Framework;
using HandwerkerImperium.Domain.Social;

namespace HandwerkerImperium.Domain.Tests.Social
{
    /// <summary>
    /// Verifiziert das Referral-System: deterministischer 6-stelliger Code, Formatprüfung, Selbst-Referral-Sperre,
    /// 3-Tier-Stufung und Gem-Belohnung je Stufe.
    /// </summary>
    [TestFixture]
    public class ReferralFormulasTests
    {
        [Test]
        public void GenerateCode_DeterministicAndValidFormat()
        {
            string a = ReferralFormulas.GenerateCode("player-uuid-1");
            string b = ReferralFormulas.GenerateCode("player-uuid-1");
            Assert.That(a, Is.EqualTo(b), "gleiche PlayerId -> gleicher Code");
            Assert.That(a.Length, Is.EqualTo(ReferralFormulas.CodeLength));
            Assert.That(ReferralFormulas.IsValidFormat(a), Is.True);
        }

        [Test]
        public void IsValidFormat_RejectsBadCodes()
        {
            Assert.That(ReferralFormulas.IsValidFormat("ABC123"), Is.True);
            Assert.That(ReferralFormulas.IsValidFormat("abc123"), Is.False, "Kleinbuchstaben");
            Assert.That(ReferralFormulas.IsValidFormat("AB12"), Is.False, "zu kurz");
            Assert.That(ReferralFormulas.IsValidFormat("AB-123"), Is.False, "Sonderzeichen");
            Assert.That(ReferralFormulas.IsValidFormat(null), Is.False);
        }

        [Test]
        public void IsSelfReferral_BlocksOwnCode_CaseInsensitive()
        {
            Assert.That(ReferralFormulas.IsSelfReferral("ABC123", "ABC123"), Is.True);
            Assert.That(ReferralFormulas.IsSelfReferral("ABC123", "abc123"), Is.True, "Kleinschreibung umgeht die Sperre nicht");
            Assert.That(ReferralFormulas.IsSelfReferral("ABC123", "XYZ789"), Is.False);
        }

        [Test]
        public void Tiers_AndRewards()
        {
            int[] thresholds = { 1, 5, 10 };
            int[] rewards = { 50, 200, 500 };
            Assert.That(ReferralFormulas.TierForCount(0, thresholds), Is.EqualTo(0));
            Assert.That(ReferralFormulas.TierForCount(1, thresholds), Is.EqualTo(1));
            Assert.That(ReferralFormulas.TierForCount(5, thresholds), Is.EqualTo(2));
            Assert.That(ReferralFormulas.TierForCount(12, thresholds), Is.EqualTo(3));
            Assert.That(ReferralFormulas.TierReward(1, rewards), Is.EqualTo(50));
            Assert.That(ReferralFormulas.TierReward(3, rewards), Is.EqualTo(500));
            Assert.That(ReferralFormulas.TierReward(0, rewards), Is.EqualTo(0));
        }
    }
}
