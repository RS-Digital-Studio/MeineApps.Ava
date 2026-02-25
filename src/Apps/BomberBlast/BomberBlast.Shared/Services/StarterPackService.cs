using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Einmaliges Starterpaket: 5000 Coins + 20 Gems + 3 Rare-Karten für 0,99 EUR.
/// Verfügbar ab Level 5, einmalig pro Spieler.
/// Persistenz via IPreferencesService (JSON).
/// </summary>
public class StarterPackService : IStarterPackService
{
    private const string DATA_KEY = "StarterPackData";
    private const int REQUIRED_LEVEL = 5;
    private const int PACK_COINS = 5000;
    private const int PACK_GEMS = 20;
    private const int PACK_RARE_CARDS = 3;

    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ICardService _cardService;
    private StarterPackData _data;

    public bool IsAvailable => _data.IsEligible && !_data.IsPurchased;
    public bool IsAlreadyPurchased => _data.IsPurchased;

    public StarterPackService(
        IPreferencesService preferences,
        ICoinService coinService,
        IGemService gemService,
        ICardService cardService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _gemService = gemService;
        _cardService = cardService;
        _data = Load();
    }

    public void CheckEligibility(int currentLevel)
    {
        if (_data.IsPurchased) return;
        if (_data.IsEligible) return;

        if (currentLevel >= REQUIRED_LEVEL)
        {
            _data.IsEligible = true;
            Save();
        }
    }

    public void MarkAsPurchased()
    {
        if (_data.IsPurchased) return;

        // Belohnungen vergeben
        _coinService.AddCoins(PACK_COINS);
        _gemService.AddGems(PACK_GEMS);

        // 3 Rare-Karten droppen (Welt 7 = höhere Rare-Drop-Rate)
        for (int i = 0; i < PACK_RARE_CARDS; i++)
        {
            var drop = _cardService.GenerateDrop(worldNumber: 7);
            if (drop.HasValue)
                _cardService.AddCard(drop.Value);
        }

        _data.IsPurchased = true;
        _data.PurchaseDate = DateTime.UtcNow.ToString("O");
        Save();
    }

    // === Persistenz ===

    private StarterPackData Load()
    {
        try
        {
            string json = _preferences.Get<string>(DATA_KEY, "");
            if (!string.IsNullOrEmpty(json))
                return JsonSerializer.Deserialize<StarterPackData>(json, JsonOptions) ?? new StarterPackData();
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new StarterPackData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(DATA_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    private class StarterPackData
    {
        public bool IsEligible { get; set; }
        public bool IsPurchased { get; set; }
        public string? PurchaseDate { get; set; }
    }
}
