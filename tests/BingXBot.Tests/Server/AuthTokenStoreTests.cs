using BingXBot.Server.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Server;

// Phase 18 / G3 — Bearer-Token-Rotation: IssueToken/Validate/Refresh/RevokeAllExcept/PurgeExpired.
public class AuthTokenStoreTests
{
    private static IConfiguration BuildConfig(string dataDir, int tokenSecs = 3600, int refreshSecs = 7200)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:DataDirectory"] = dataDir,
                ["Server:TokenLifetimeSeconds"] = tokenSecs.ToString(),
                ["Server:RefreshTokenLifetimeSeconds"] = refreshSecs.ToString()
            })
            .Build();
    }

    private static AuthTokenStore CreateStore(int tokenSecs = 3600, int refreshSecs = 7200)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bxbot-tokens-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return new AuthTokenStore(BuildConfig(dir, tokenSecs, refreshSecs), NullLogger<AuthTokenStore>.Instance);
    }

    [Fact]
    public void IssueAndValidate_RoundTrip_ReturnsRecord()
    {
        var store = CreateStore();
        var rec = store.IssueToken("dev1", "MyDevice");
        rec.Should().NotBeNull();
        rec.Token.Should().NotBeNullOrEmpty();
        rec.RefreshToken.Should().NotBeNullOrEmpty();
        store.Validate(rec.Token, out var loaded).Should().BeTrue();
        loaded.Should().NotBeNull();
        loaded!.DeviceId.Should().Be("dev1");
    }

    [Fact]
    public void Refresh_ValidRefreshToken_IssuesNewTokenPair()
    {
        var store = CreateStore();
        var rec = store.IssueToken("dev1", "MyDevice");
        var refreshed = store.Refresh(rec.RefreshToken);
        refreshed.Should().NotBeNull();
        refreshed!.Token.Should().NotBe(rec.Token, "neuer Token nach Refresh");
        refreshed.RefreshToken.Should().NotBe(rec.RefreshToken, "neuer Refresh-Token nach Refresh");
        // Alter Token soll nicht mehr validierbar sein.
        store.Validate(rec.Token, out _).Should().BeFalse();
    }

    [Fact]
    public void Refresh_InvalidRefreshToken_ReturnsNull()
    {
        var store = CreateStore();
        store.IssueToken("dev1", "MyDevice");
        store.Refresh("nonexistent-refresh-token-XXX").Should().BeNull();
    }

    [Fact]
    public void RevokeAllExcept_KeepsOnlySpecified()
    {
        var store = CreateStore();
        var keep = store.IssueToken("dev1", "Desktop");
        var other1 = store.IssueToken("dev2", "Mobile");
        var other2 = store.IssueToken("dev3", "Tablet");

        var removed = store.RevokeAllExcept(keep.Token);
        removed.Should().Be(2);

        store.Validate(keep.Token, out _).Should().BeTrue();
        store.Validate(other1.Token, out _).Should().BeFalse();
        store.Validate(other2.Token, out _).Should().BeFalse();
    }

    [Fact]
    public void Revoke_RemovesToken()
    {
        var store = CreateStore();
        var rec = store.IssueToken("dev1", "MyDevice");
        store.Revoke(rec.Token);
        store.Validate(rec.Token, out _).Should().BeFalse();
    }

    [Fact]
    public void PurgeExpired_RemovesOnlyOverdueRecords()
    {
        // Token mit kurzer Lifetime: 0 Sekunden Refresh = sofort abgelaufen.
        var storeShort = CreateStore(tokenSecs: 0, refreshSecs: 0);
        storeShort.IssueToken("dev1", "MyDevice");
        // Direkt nach Issue ist Refresh-Deadline schon vorbei.
        var purged = storeShort.PurgeExpired();
        purged.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Validate_ExpiredToken_ReturnsFalseButRecordKept()
    {
        // Access-Token sofort abgelaufen, Refresh aber noch gueltig.
        var store = CreateStore(tokenSecs: 0, refreshSecs: 7200);
        var rec = store.IssueToken("dev1", "MyDevice");
        store.Validate(rec.Token, out _).Should().BeFalse("Access-Token ist abgelaufen");
        // Refresh soll trotzdem noch funktionieren — Record wurde NICHT entfernt.
        var refreshed = store.Refresh(rec.RefreshToken);
        refreshed.Should().NotBeNull("Refresh-Token ist noch gültig, Record war noch im Store");
    }
}
