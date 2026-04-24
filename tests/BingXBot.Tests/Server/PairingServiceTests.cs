using BingXBot.Server.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Server;

/// <summary>
/// Tests fuer die PairComplete-Outcome-Logik (v1.3.0, nachdem TryComplete auf ein Enum statt bool
/// umgestellt wurde). Sichert ab, dass Client + Server klar zwischen "Tippfehler — Session lebt
/// weiter" und "Session tot — zurueck zu Schritt 1" unterscheiden koennen.
/// </summary>
public class PairingServiceTests
{
    private static PairingService CreateService(string tempDir, int codeLength = 6, int lifetimeSeconds = 300)
    {
        Directory.CreateDirectory(tempDir);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:PairingCodeLengthDigits"] = codeLength.ToString(),
                ["Server:PairingCodeLifetimeSeconds"] = lifetimeSeconds.ToString(),
                ["Server:DataDirectory"] = tempDir
            })
            .Build();
        return new PairingService(config, NullLogger<PairingService>.Instance);
    }

    private static string ReadGeneratedCode(string tempDir)
    {
        // PairingService schreibt den Code in pairing-code.txt (3-zeiliges Format)
        var text = File.ReadAllText(Path.Combine(tempDir, "pairing-code.txt"));
        // "Code: 123456" rausziehen
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("Code:", StringComparison.Ordinal))
                return t.Substring("Code:".Length).Trim();
        }
        throw new InvalidOperationException("Kein Code in pairing-code.txt gefunden.");
    }

    [Fact]
    public void TryComplete_RichtigerCode_LiefertSuccess()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            var (pairingId, _) = svc.StartPairing("TestDevice");
            var code = ReadGeneratedCode(tmp);

            var outcome = svc.TryComplete(pairingId, code, out var deviceName);

            outcome.Should().Be(PairCompleteOutcome.Success);
            deviceName.Should().Be("TestDevice");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void TryComplete_FalscherCode_LiefertInvalidCode_SessionLebt()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            var (pairingId, _) = svc.StartPairing("TestDevice");

            var outcome = svc.TryComplete(pairingId, "000000", out _);

            outcome.Should().Be(PairCompleteOutcome.InvalidCode);
            // Session lebt noch — derselbe pairingId ist weiterhin gueltig fuer weitere Versuche.
            svc.HasActivePairing.Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void TryComplete_UnbekanntePairingId_LiefertUnknown()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            // Kein StartPairing → keine Session.
            var outcome = svc.TryComplete(Guid.NewGuid().ToString("N"), "123456", out _);

            outcome.Should().Be(PairCompleteOutcome.UnknownPairingId);
            svc.HasActivePairing.Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void TryComplete_FuenfFalscheVersuche_ErsterDanachLiefertTooManyAttempts()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            var (pairingId, _) = svc.StartPairing("TestDevice");

            // Erste 5 Versuche sind InvalidCode (Session lebt).
            for (int i = 0; i < 5; i++)
            {
                svc.TryComplete(pairingId, "000000", out _).Should().Be(PairCompleteOutcome.InvalidCode);
            }

            // Der 6. Versuch schlaegt mit TooManyAttempts fehl — Session wird geloescht.
            var outcome = svc.TryComplete(pairingId, "000000", out _);

            outcome.Should().Be(PairCompleteOutcome.TooManyAttempts);
            svc.HasActivePairing.Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void TryComplete_NachTooManyAttempts_SessionIstTot_NaechsterCallLiefertUnknown()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            var (pairingId, _) = svc.StartPairing("TestDevice");

            // 6 Versuche triggern TooManyAttempts beim 6. Call.
            for (int i = 0; i < 6; i++) svc.TryComplete(pairingId, "000000", out _);

            // Weiterer Versuch mit derselben ID → die Session ist weg → UnknownPairingId.
            var outcome = svc.TryComplete(pairingId, "000000", out _);

            outcome.Should().Be(PairCompleteOutcome.UnknownPairingId);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void StartPairing_BereitsAktiv_WirftInvalidOperation()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            svc.StartPairing("Device1");

            var act = () => svc.StartPairing("Device2");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Device1*");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void CancelPairing_BekanntePairingId_LiefertTrue_UndSessionIstWeg()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            var (pairingId, _) = svc.StartPairing("TestDevice");

            var result = svc.CancelPairing(pairingId);

            result.Should().BeTrue();
            svc.HasActivePairing.Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void CancelPairing_UnbekanntePairingId_LiefertFalse()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp);
        try
        {
            var result = svc.CancelPairing(Guid.NewGuid().ToString("N"));

            result.Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void TryComplete_AbgelaufenerCode_LiefertExpired()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        // Lifetime = 1 Sekunde, dann warten.
        var svc = CreateService(tmp, lifetimeSeconds: 1);
        try
        {
            var (pairingId, _) = svc.StartPairing("TestDevice");
            var code = ReadGeneratedCode(tmp);

            Thread.Sleep(1500);

            var outcome = svc.TryComplete(pairingId, code, out _);

            outcome.Should().Be(PairCompleteOutcome.Expired);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void StartPairing_GeneriertCodeMitKonfigurierterLaenge()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bingxbot-test-{Guid.NewGuid():N}");
        var svc = CreateService(tmp, codeLength: 8);
        try
        {
            svc.StartPairing("TestDevice");
            var code = ReadGeneratedCode(tmp);

            code.Length.Should().Be(8);
            code.Should().MatchRegex("^[0-9]+$");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }
}
