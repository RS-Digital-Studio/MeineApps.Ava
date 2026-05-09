using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BingXBot.Core.Interfaces;

namespace BingXBot.Server;

/// <summary>
/// Server-seitiger Credentials-Store fuer den Pi: Speichert BingX API-Key/Secret
/// in ~/.config/bingxbot/credentials.bin (Linux: chmod 600).
///
/// Verschluesselung:
///   - Linux: AES-256-CBC mit PBKDF2-abgeleitetem Key (wie SecureStorageService in Shared)
///   - Windows (Entwickler-Maschine): DPAPI (User-Scope)
///
/// Hinweis: Auf einem Pi hat NUR der bingxbot-User Leserechte. Root/sudo koennen natuerlich
/// auslesen — deshalb gehoert der Pi in ein vertrauenswuerdiges Netz (Tailscale/LAN).
/// </summary>
public sealed class PiCredentialStore : ISecureStorageService
{
    private readonly string _credentialsPath;
    private readonly string _masterKeyPath;
    // Schuetzt MasterKey-Read+Write vor parallelem Race: Save+Load in zwei Threads ohne Lock
    // koennte zwei unabhaengige Master-Keys generieren, der zweite ueberschreibt den ersten
    // → vorher gespeicherte credentials.bin nicht mehr entschluesselbar.
    private readonly Lock _masterKeyLock = new();
    private bool _hasCredentials;

    public bool HasCredentials => _hasCredentials;

    public PiCredentialStore(IConfiguration config)
    {
        var dataDir = config.GetValue<string>("Server:DataDirectory");
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            dataDir = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingXBot", "Server")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "bingxbot");
        }
        Directory.CreateDirectory(dataDir);
        _credentialsPath = Path.Combine(dataDir, "credentials.bin");
        _masterKeyPath = Path.Combine(dataDir, ".masterkey");
        _hasCredentials = File.Exists(_credentialsPath);
    }

    /// <summary>Phase 18 / G2 — Ciphertext-Versions-Marker. v2 = AES-GCM (authentisiert).</summary>
    private const byte CiphertextVersionAesGcm = 0x02;

    public async Task SaveCredentialsAsync(string apiKey, string apiSecret)
    {
        var json = JsonSerializer.Serialize(new CredentialData(apiKey, apiSecret));
        // Phase 18 / G2 — AES-GCM statt AES-CBC. GCM ist authentisiert: jeder Modifikations-
        // Versuch am Ciphertext wirft beim Decrypt eine CryptographicException (Tag-Mismatch),
        // statt mit "interessantem" Klartext zu arbeiten. Auf Windows weiterhin DPAPI (legacy
        // Verhalten — DPAPI ist vom Windows-Login-Account abgeleitet, AES-GCM auf Pi-Linux).
        var encrypted = Protect(Encoding.UTF8.GetBytes(json), GetOrCreateMasterKey());
        await AtomicWriteAsync(_credentialsPath, encrypted,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
        _hasCredentials = true;
    }

    /// <summary>
    /// Atomic write per Temp-File + Move/Replace. Verhindert corrupted file bei Power-Loss
    /// oder Prozess-Crash mid-write. File.WriteAllBytes truncated die Ziel-Datei sofort und
    /// schreibt dann – Power-Loss mittendrin = 0-byte-Datei = unleserlich.
    /// </summary>
    private static async Task AtomicWriteAsync(string path, byte[] data, UnixFileMode unixMode)
    {
        var tmp = path + ".tmp";
        await File.WriteAllBytesAsync(tmp, data);
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmp, unixMode);
        }
        catch { }

        // File.Move mit overwrite:true ist atomar auf NTFS/ext4 (rename syscall)
        File.Move(tmp, path, overwrite: true);
    }

    public async Task<(string ApiKey, string ApiSecret)?> LoadCredentialsAsync()
    {
        if (!File.Exists(_credentialsPath)) return null;

        var encrypted = await File.ReadAllBytesAsync(_credentialsPath);

        // Primaerer Pfad: Per-Installation .masterkey (sicher gegen Leak der credentials.bin,
        // da .masterkey mit chmod 400 und Zufallsinhalt separat geschuetzt ist).
        try
        {
            var decrypted = Unprotect(encrypted, GetOrCreateMasterKey());
            var json = Encoding.UTF8.GetString(decrypted);
            var creds = JsonSerializer.Deserialize<CredentialData>(json);
            if (creds != null)
            {
                // Phase 18 / G2 — Auto-Migrate v1 (AES-CBC) → v2 (AES-GCM) on first successful read.
                // Wenn die Datei nicht mit dem v2-Magic-Byte beginnt, schreiben wir sie mit
                // dem neuen Format zurueck. Bei v2 ist das ein No-Op (Re-Save nur bei Versions-Drift).
                if (encrypted.Length == 0 || encrypted[0] != CiphertextVersionAesGcm)
                {
                    try
                    {
                        await SaveCredentialsAsync(creds.ApiKey, creds.ApiSecret).ConfigureAwait(false);
                        Console.Error.WriteLine("[PiCredentialStore] credentials.bin auf v2 (AES-GCM) migriert.");
                    }
                    catch { /* Lese-Pfad darf nicht fehlschlagen nur weil Save scheitert */ }
                }
                return (creds.ApiKey, creds.ApiSecret);
            }
        }
        catch { /* ggf. Legacy-Format → Fallback unten */ }

        // Legacy-Pfad (Installationen vor 17.04.2026): AES-Key aus MachineName+UserName.
        // Bei Erfolg migrieren wir den Store sofort auf die neue .masterkey-basierte Ableitung.
        // SUNSET 2026-06-01: Der Legacy-Key ist trivial ableitbar (bingxbot@raspberrypi ist
        // Standard-Install-Pfad). Nach dem Sunset-Datum wird der Fallback deaktiviert —
        // Nutzer mit Legacy-Installs muessen ihre BingX-API-Keys via /api/v1/credentials
        // PUT neu setzen (re-encrypted mit Master-Key).
        var legacySunset = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        if (DateTime.UtcNow >= legacySunset)
        {
            Console.Error.WriteLine(
                "[PiCredentialStore] Legacy-credentials.bin-Format nicht mehr unterstuetzt " +
                "(Sunset 2026-06-01). Bitte /api/v1/credentials PUT aufrufen um neu zu setzen.");
            return null;
        }

        try
        {
            var decrypted = Unprotect(encrypted, DeriveLegacyMachineKey());
            var json = Encoding.UTF8.GetString(decrypted);
            var creds = JsonSerializer.Deserialize<CredentialData>(json);
            if (creds == null) return null;

            // Warnung: Legacy-Pfad wurde erfolgreich genutzt — Operator soll migrieren.
            Console.Error.WriteLine(
                "[PiCredentialStore] WARNUNG: credentials.bin wurde mit Legacy-Machine-Key entschluesselt. " +
                "Bitte /api/v1/credentials PUT aufrufen um auf Master-Key-Schema zu migrieren. " +
                "Legacy-Fallback wird am 2026-06-01 deaktiviert.");

            // Re-encrypt with new master key (auto-migration) — best-effort
            try { await SaveCredentialsAsync(creds.ApiKey, creds.ApiSecret); }
            catch { /* Lese-Pfad darf nicht fehlschlagen nur weil Save scheitert */ }

            return (creds.ApiKey, creds.ApiSecret);
        }
        catch
        {
            return null;
        }
    }

    public Task DeleteCredentialsAsync()
    {
        if (File.Exists(_credentialsPath))
            File.Delete(_credentialsPath);
        _hasCredentials = false;
        return Task.CompletedTask;
    }

    private static byte[] Protect(byte[] data, byte[] key)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return System.Security.Cryptography.ProtectedData.Protect(data, null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
        }

        // Phase 18 / G2 — AES-GCM (authentisiert). Layout:
        // [0]: Version-Magic (0x02)
        // [1..13]: 12-Byte Nonce (zufaellig pro Save)
        // [13..N-16]: Ciphertext
        // [N-16..N]: 16-Byte GCM-Tag
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[data.Length];
        var tag = new byte[16];
        using (var aesGcm = new AesGcm(key, tagSizeInBytes: 16))
        {
            aesGcm.Encrypt(nonce, data, ciphertext, tag);
        }

        var output = new byte[1 + 12 + ciphertext.Length + 16];
        output[0] = CiphertextVersionAesGcm;
        Buffer.BlockCopy(nonce, 0, output, 1, 12);
        Buffer.BlockCopy(ciphertext, 0, output, 13, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, 13 + ciphertext.Length, 16);
        return output;
    }

    private static byte[] Unprotect(byte[] data, byte[] key)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return System.Security.Cryptography.ProtectedData.Unprotect(data, null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
        }

        // Phase 18 / G2 — Versionsmarker pruefen. Neues Format ist v2 (AES-GCM), Legacy v1 ist
        // 16-Byte-IV + AES-CBC ohne Versionsbyte. Heuristik: Wenn das erste Byte exakt 0x02 ist
        // UND die Datenlaenge mindestens 1+12+16 = 29 ist, behandeln wir das als v2.
        if (data.Length >= 29 && data[0] == CiphertextVersionAesGcm)
        {
            var nonce = new byte[12];
            Buffer.BlockCopy(data, 1, nonce, 0, 12);
            var cipherLen = data.Length - 13 - 16;
            var ciphertext = new byte[cipherLen];
            Buffer.BlockCopy(data, 13, ciphertext, 0, cipherLen);
            var tag = new byte[16];
            Buffer.BlockCopy(data, 13 + cipherLen, tag, 0, 16);
            var plaintext = new byte[cipherLen];
            using var aesGcm = new AesGcm(key, tagSizeInBytes: 16);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext); // wirft CryptographicException bei Tag-Mismatch
            return plaintext;
        }

        // Legacy v1: AES-CBC. Wir entschluesseln und der Aufrufer schreibt im Erfolgsfall mit v2 zurueck.
        var iv = new byte[16];
        Array.Copy(data, 0, iv, 0, 16);
        var legacyCiphertext = new byte[data.Length - 16];
        Array.Copy(data, 16, legacyCiphertext, 0, legacyCiphertext.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cs.Write(legacyCiphertext);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    /// <summary>
    /// Laedt den Master-Key aus .masterkey oder erzeugt einen neuen 32-byte-Zufallsschluessel.
    /// Der Master-Key ist pro-Installation und NICHT aus oeffentlichen Werten ableitbar —
    /// ein Leak der credentials.bin reicht damit NICHT mehr zum offline Entschluesseln.
    /// Linux: chmod 400 (owner-read-only), ausser bingxbot-User niemand kann lesen.
    /// </summary>
    private byte[] GetOrCreateMasterKey()
    {
        // Lock um Check+Read+Create+Write: ohne das koennen zwei parallele Save/Load Aufrufe
        // jeweils einen neuen Master-Key generieren und sich gegenseitig ueberschreiben —
        // vorher mit altem Key verschluesselte Credentials waeren danach nicht mehr lesbar.
        lock (_masterKeyLock)
        {
            if (File.Exists(_masterKeyPath))
            {
                var existing = File.ReadAllBytes(_masterKeyPath);
                if (existing.Length == 32) return existing;
                // Corrupt/partial-write: Datei loeschen und neu generieren.
                // Legacy-Fallback im LoadCredentials kann die credentials.bin dann trotzdem entschluesseln
                // und mit neuem Master-Key re-encrypten.
                try { File.Delete(_masterKeyPath); } catch { }
            }

            var key = RandomNumberGenerator.GetBytes(32);
            // Atomic write: Temp + Move. Verhindert partial-write-Loop bei Power-Loss.
            var tmp = _masterKeyPath + ".tmp";
            File.WriteAllBytes(tmp, key);
            if (!OperatingSystem.IsWindows())
            {
                try { File.SetUnixFileMode(tmp, UnixFileMode.UserRead); }
                catch { }
            }
            File.Move(tmp, _masterKeyPath, overwrite: true);
            return key;
        }
    }

    /// <summary>
    /// Legacy-Ableitung aus MachineName+UserName fuer Migrations-Kompatibilitaet.
    /// Wird NUR im Lese-Pfad als Fallback genutzt, danach re-encrypted mit Master-Key.
    /// NIEMALS fuer neue Protects verwenden.
    /// </summary>
    private static byte[] DeriveLegacyMachineKey()
    {
        var password = $"{Environment.MachineName}:{Environment.UserName}:BingXBot:key";
        var salt = SHA256.HashData(Encoding.UTF8.GetBytes($"{Environment.MachineName}:{Environment.UserName}:BingXBot:salt"));
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
    }

    private sealed record CredentialData(string ApiKey, string ApiSecret);
}
