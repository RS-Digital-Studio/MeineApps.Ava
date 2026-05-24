#nullable enable
using ArcaneKingdom.Domain.Chat;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class ChatValidatorTests
    {
        [Test]
        public void LeereNachrichtIstUngueltig()
        {
            Assert.IsFalse(ChatValidator.IsLengthValid(""));
            Assert.IsFalse(ChatValidator.IsLengthValid("   "));
            Assert.IsFalse(ChatValidator.IsLengthValid(null!));
        }

        [Test]
        public void NormaleNachrichtIstGueltig()
        {
            Assert.IsTrue(ChatValidator.IsLengthValid("Hallo Gilde!"));
        }

        [Test]
        public void ZuLangeNachrichtIstUngueltig()
        {
            var tooLong = new string('A', ChatValidator.MaxMessageLength + 1);
            Assert.IsFalse(ChatValidator.IsLengthValid(tooLong));
        }

        [Test]
        public void GrenzwertMaxLengthIstGueltig()
        {
            var atLimit = new string('A', ChatValidator.MaxMessageLength);
            Assert.IsTrue(ChatValidator.IsLengthValid(atLimit));
        }
    }
}
