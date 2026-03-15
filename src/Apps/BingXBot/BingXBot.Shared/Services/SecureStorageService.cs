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

    private static byte[] Protect(byte[] data)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        else
        {
            // Linux: AES-256-CBC mit maschinenspezifischem Schluessel
            // Key und IV werden separat abgeleitet (IV NICHT aus dem Key kopiert)
            var keySource = $"{Environment.MachineName}:{Environment.UserName}:BingXBot:key";
            var ivSource = $"{Environment.MachineName}:{Environment.UserName}:BingXBot:iv";
            using var sha = SHA256.Create();
            var key = sha.ComputeHash(Encoding.UTF8.GetBytes(keySource));
            var ivHash = sha.ComputeHash(Encoding.UTF8.GetBytes(ivSource));
            var iv = new byte[16];
            Array.Copy(ivHash, iv, 16);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var ms = new MemoryStream();
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
            var keySource = $"{Environment.MachineName}:{Environment.UserName}:BingXBot:key";
            var ivSource = $"{Environment.MachineName}:{Environment.UserName}:BingXBot:iv";
            using var sha = SHA256.Create();
            var key = sha.ComputeHash(Encoding.UTF8.GetBytes(keySource));
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
    }

    private class CredentialData
    {
        public string ApiKey { get; set; } = "";
        public string ApiSecret { get; set; } = "";
    }
}
