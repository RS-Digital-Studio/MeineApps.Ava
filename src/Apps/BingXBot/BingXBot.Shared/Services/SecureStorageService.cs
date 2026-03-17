using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BingXBot.Core.Interfaces;

namespace BingXBot.Services;

public class SecureStorageService : ISecureStorageService
{
    private readonly string _credentialsPath;
    private bool _hasCredentials;

    public bool HasCredentials => _hasCredentials;

    public SecureStorageService()
    {
        var folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BingXBot")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "BingXBot");

        Directory.CreateDirectory(folder);
        _credentialsPath = Path.Combine(folder, "credentials.dat");
        _hasCredentials = File.Exists(_credentialsPath);
    }

    public async Task SaveCredentialsAsync(string apiKey, string apiSecret)
    {
        var data = JsonSerializer.Serialize(new { ApiKey = apiKey, ApiSecret = apiSecret });
        var encrypted = Protect(Encoding.UTF8.GetBytes(data));
        await File.WriteAllBytesAsync(_credentialsPath, encrypted);
        _hasCredentials = true;
    }

    public async Task<(string ApiKey, string ApiSecret)?> LoadCredentialsAsync()
    {
        if (!File.Exists(_credentialsPath)) return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_credentialsPath);
            var decrypted = Unprotect(encrypted);
            var json = Encoding.UTF8.GetString(decrypted);
            var creds = JsonSerializer.Deserialize<CredentialData>(json);
            if (creds == null) return null;
            return (creds.ApiKey, creds.ApiSecret);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Credentials laden/entschlüsseln fehlgeschlagen: {ex.Message}");
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

    private static byte[] Protect(byte[] data)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        else
        {
            // Linux: AES-256-CBC mit maschinenspezifischem Schlüssel + zufälligem IV
            var key = DeriveLinuxKey();

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV(); // Kryptographisch zufälliger IV pro Verschlüsselung

            using var ms = new MemoryStream();
            // IV voranstellen (16 Bytes) - wird beim Entschlüsseln gelesen
            ms.Write(aes.IV, 0, aes.IV.Length);
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(data);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
        else
        {
            // Prüfe ob Daten im neuen Format sind (IV vorangestellt, min. 16+16 Bytes)
            // oder im alten Format (statischer IV, ohne Prefix)
            if (data.Length >= 32)
            {
                try
                {
                    return UnprotectWithRandomIv(data);
                }
                catch (CryptographicException)
                {
                    // Fallback: Altes Format mit statischem IV (Migration)
                    return UnprotectLegacy(data);
                }
            }

            return UnprotectLegacy(data);
        }
    }

    /// <summary>Neues Format: Zufälliger IV in den ersten 16 Bytes.</summary>
    private static byte[] UnprotectWithRandomIv(byte[] data)
    {
        var key = DeriveLinuxKey();
        var iv = new byte[16];
        Array.Copy(data, 0, iv, 0, 16);
        var ciphertext = new byte[data.Length - 16];
        Array.Copy(data, 16, ciphertext, 0, ciphertext.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cs.Write(ciphertext);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    /// <summary>Altes Format: Statischer IV (Abwärtskompatibilität für bestehende Credentials).</summary>
    private static byte[] UnprotectLegacy(byte[] data)
    {
        var key = DeriveLinuxKey();
        var ivSource = $"{Environment.MachineName}:{Environment.UserName}:BingXBot:iv";
        using var sha = SHA256.Create();
        var ivHash = sha.ComputeHash(Encoding.UTF8.GetBytes(ivSource));
        var iv = new byte[16];
        Array.Copy(ivHash, iv, 16);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cs.Write(data);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    /// <summary>Leitet den AES-Key aus maschinenspezifischen Daten ab.</summary>
    private static byte[] DeriveLinuxKey()
    {
        var keySource = $"{Environment.MachineName}:{Environment.UserName}:BingXBot:key";
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(keySource));
    }

    private class CredentialData
    {
        public string ApiKey { get; set; } = "";
        public string ApiSecret { get; set; } = "";
    }
}
