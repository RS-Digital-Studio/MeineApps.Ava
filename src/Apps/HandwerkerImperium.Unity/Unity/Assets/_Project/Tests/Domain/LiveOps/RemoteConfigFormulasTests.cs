using NUnit.Framework;
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Tests.LiveOps
{
    /// <summary>
    /// Verifiziert die Remote-Config-Logik: deterministische A/B-Buckets (im Bereich, stabil) und den
    /// prozentualen Feature-Flag-Rollout (0 % nie, 100 % immer).
    /// </summary>
    [TestFixture]
    public class RemoteConfigFormulasTests
    {
        [Test]
        public void AbBucket_InRange_AndStable()
        {
            int b = RemoteConfigFormulas.AbBucket("player-1", "exp_loop", 3);
            Assert.That(b, Is.InRange(0, 2));
            Assert.That(RemoteConfigFormulas.AbBucket("player-1", "exp_loop", 3), Is.EqualTo(b));
            Assert.That(RemoteConfigFormulas.AbBucket("player-1", "exp_loop", 1), Is.EqualTo(0));
        }

        [Test]
        public void IsInRollout_BoundsAndStability()
        {
            Assert.That(RemoteConfigFormulas.IsInRollout("p", "flag", 0), Is.False, "0% -> nie");
            Assert.That(RemoteConfigFormulas.IsInRollout("p", "flag", 100), Is.True, "100% -> immer");
            bool a = RemoteConfigFormulas.IsInRollout("player-x", "flag_y", 50);
            Assert.That(RemoteConfigFormulas.IsInRollout("player-x", "flag_y", 50), Is.EqualTo(a), "stabil");
        }
    }
}
