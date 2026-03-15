using BingXBot.Exchange;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Exchange;

public class BingXRestClientTests
{
    [Fact]
    public void GenerateSignature_ShouldProduceValidHmac()
    {
        var signature = BingXRestClient.GenerateSignature("timestamp=1234567890", "testsecret");
        signature.Should().NotBeNullOrEmpty();
        signature.Length.Should().Be(64); // SHA256 hex = 64 chars
    }

    [Fact]
    public void GenerateSignature_SameInput_ShouldProduceSameOutput()
    {
        var s1 = BingXRestClient.GenerateSignature("param=value", "secret");
        var s2 = BingXRestClient.GenerateSignature("param=value", "secret");
        s1.Should().Be(s2);
    }

    [Fact]
    public void GenerateSignature_DifferentInput_ShouldProduceDifferentOutput()
    {
        var s1 = BingXRestClient.GenerateSignature("param=value1", "secret");
        var s2 = BingXRestClient.GenerateSignature("param=value2", "secret");
        s1.Should().NotBe(s2);
    }
}
