using System.Runtime.InteropServices;
using BingXBot.Server;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BingXBot.Tests.Server;

// Phase 18 / G2 — AES-GCM-authentisierte Credentials-Persistenz mit Auto-Migration aus v1 (AES-CBC).
// Tests laufen NUR auf Linux/macOS, weil PiCredentialStore auf Windows DPAPI nutzt (kein eigenes Crypto).
public class PiCredentialStoreTests
{
    private static IConfiguration BuildConfig(string dataDir)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:DataDirectory"] = dataDir
            })
            .Build();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_ReturnsSameCredentials()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // DPAPI-Pfad nicht testbar
        var dir = Path.Combine(Path.GetTempPath(), $"bxbot-creds-{Guid.NewGuid():N}");
        try
        {
            var store = new PiCredentialStore(BuildConfig(dir));
            await store.SaveCredentialsAsync("apiKey-AAA", "secret-BBB");
            var loaded = await store.LoadCredentialsAsync();
            loaded.HasValue.Should().BeTrue();
            loaded!.Value.ApiKey.Should().Be("apiKey-AAA");
            loaded.Value.ApiSecret.Should().Be("secret-BBB");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Save_ProducesV2Format_StartsWith0x02()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var dir = Path.Combine(Path.GetTempPath(), $"bxbot-creds-{Guid.NewGuid():N}");
        try
        {
            var store = new PiCredentialStore(BuildConfig(dir));
            await store.SaveCredentialsAsync("k", "s");
            var raw = await File.ReadAllBytesAsync(Path.Combine(dir, "credentials.bin"));
            raw[0].Should().Be(0x02, "Phase-18-Format hat Versions-Magic 0x02 als erstes Byte");
            // 1 Magic + 12 Nonce + N Ciphertext + 16 Tag = mindestens 29
            raw.Length.Should().BeGreaterThanOrEqualTo(29);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Load_TamperedCiphertext_ThrowsOrReturnsNull()
    {
        // GCM ist authentisiert: jede Modifikation des Ciphertext muss erkannt werden.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var dir = Path.Combine(Path.GetTempPath(), $"bxbot-creds-{Guid.NewGuid():N}");
        try
        {
            var store = new PiCredentialStore(BuildConfig(dir));
            await store.SaveCredentialsAsync("k", "s");

            var path = Path.Combine(dir, "credentials.bin");
            var raw = await File.ReadAllBytesAsync(path);
            // Flip ein Bit im Ciphertext-Bereich (zwischen Nonce und Tag).
            var middleIdx = raw.Length / 2;
            raw[middleIdx] ^= 0x01;
            await File.WriteAllBytesAsync(path, raw);

            // Re-Load mit neuem Store-Objekt (frischer Master-Key-Cache umgehen ist nicht noetig — der
            // Master-Key bleibt derselbe weil .masterkey-Datei unveraendert).
            var store2 = new PiCredentialStore(BuildConfig(dir));
            var loaded = await store2.LoadCredentialsAsync();
            loaded.Should().BeNull("Tampered Ciphertext muss vom Auth-Tag erkannt werden — null statt korrupte Daten");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task HasCredentials_AfterSave_ReturnsTrue()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var dir = Path.Combine(Path.GetTempPath(), $"bxbot-creds-{Guid.NewGuid():N}");
        try
        {
            var store = new PiCredentialStore(BuildConfig(dir));
            store.HasCredentials.Should().BeFalse();
            await store.SaveCredentialsAsync("k", "s");
            store.HasCredentials.Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Delete_RemovesFileAndResetsHasCredentials()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var dir = Path.Combine(Path.GetTempPath(), $"bxbot-creds-{Guid.NewGuid():N}");
        try
        {
            var store = new PiCredentialStore(BuildConfig(dir));
            await store.SaveCredentialsAsync("k", "s");
            await store.DeleteCredentialsAsync();
            store.HasCredentials.Should().BeFalse();
            File.Exists(Path.Combine(dir, "credentials.bin")).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
