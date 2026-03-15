namespace BingXBot.Core.Interfaces;

public interface ISecureStorageService
{
    Task SaveCredentialsAsync(string apiKey, string apiSecret);
    Task<(string ApiKey, string ApiSecret)?> LoadCredentialsAsync();
    Task DeleteCredentialsAsync();
    bool HasCredentials { get; }
}
