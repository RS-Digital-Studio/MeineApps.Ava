using System.Text;
using FluentAssertions;
using GardenControl.Core;
using GardenControl.Server.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GardenControl.Tests;

/// <summary>
/// Tests der Shared-Secret-Header-Auth-Middleware: Header-Pruefung, Health-Ausnahme,
/// SignalR-access_token-Query, falsches/fehlendes Secret, Default-Dev-Secret-Fallback.
/// </summary>
public class SharedSecretAuthMiddlewareTests
{
    private const string Secret = "super-geheim-123";

    private static SharedSecretAuthMiddleware Create(RequestDelegate next, string? configuredSecret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SharedSecret"] = configuredSecret
            })
            .Build();
        var logger = Substitute.For<ILogger<SharedSecretAuthMiddleware>>();
        return new SharedSecretAuthMiddleware(next, config, logger);
    }

    private static DefaultHttpContext MakeContext(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    // ── Erfolgsfaelle ───────────────────────────────────────────────────────

    [Fact]
    public async Task ValidSecretHeader_CallsNext()
    {
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/api/status");
        ctx.Request.Headers[GardenAuth.SecretHeader] = Secret;

        await mw.InvokeAsync(ctx);

        called.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task HealthEndpoint_IsPublic_NoSecretRequired()
    {
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/api/health");
        // kein Header

        await mw.InvokeAsync(ctx);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task HubPath_AcceptsSecretAsAccessTokenQuery()
    {
        // SignalR-WebSocket-Upgrade kann keinen Header setzen → access_token-Query.
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/hub/garden");
        ctx.Request.QueryString = new QueryString($"?access_token={Secret}");

        await mw.InvokeAsync(ctx);

        called.Should().BeTrue();
    }

    // ── Fehlerfaelle ────────────────────────────────────────────────────────

    [Fact]
    public async Task MissingSecret_Returns401_AndDoesNotCallNext()
    {
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/api/zones");

        await mw.InvokeAsync(ctx);

        called.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task WrongSecret_Returns401()
    {
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/api/emergency-stop");
        ctx.Request.Headers[GardenAuth.SecretHeader] = "falsch";

        await mw.InvokeAsync(ctx);

        called.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task HubPath_WrongAccessToken_Returns401()
    {
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/hub/garden");
        ctx.Request.QueryString = new QueryString("?access_token=falsch");

        await mw.InvokeAsync(ctx);

        called.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task AccessTokenQuery_OnNonHubPath_IsIgnored_Returns401()
    {
        // Schutz gegen Secret-Leak via URL/Logs auf REST-Pfaden: dort gilt NUR der Header.
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/api/zones");
        ctx.Request.QueryString = new QueryString($"?access_token={Secret}");

        await mw.InvokeAsync(ctx);

        called.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // ── Default-Dev-Secret-Fallback ──────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NoSecretConfigured_FallsBackToDefaultDevSecret(string? configured)
    {
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, configured);
        var ctx = MakeContext("/api/status");
        ctx.Request.Headers[GardenAuth.SecretHeader] = GardenAuth.DefaultDevSecret;

        await mw.InvokeAsync(ctx);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task ConfiguredSecret_DoesNotAcceptDefaultDevSecret()
    {
        // Wenn ein echtes Secret konfiguriert ist, darf das oeffentlich bekannte Default-Secret NICHT gelten.
        var called = false;
        var mw = Create(_ => { called = true; return Task.CompletedTask; }, Secret);
        var ctx = MakeContext("/api/status");
        ctx.Request.Headers[GardenAuth.SecretHeader] = GardenAuth.DefaultDevSecret;

        await mw.InvokeAsync(ctx);

        called.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void HeaderName_And_DefaultSecret_AreStableConstants()
    {
        // Header-Name + Default-Secret muessen zwischen Client und Server identisch bleiben.
        GardenAuth.SecretHeader.Should().Be("X-Garden-Secret");
        Encoding.UTF8.GetByteCount(GardenAuth.DefaultDevSecret).Should().BeGreaterThan(0);
    }
}
