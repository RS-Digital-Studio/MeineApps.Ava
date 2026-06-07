namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Seltenheitsstufe für Meisterwerkzeuge.
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/MasterToolRarity.cs). Enum-Reihenfolge = Persistenz-Integer.
    /// Die Original-Extensions (GetLocalizationKey, GetColor) sind rein UI und wandern in die Präsentationsschicht.
    /// </summary>
    public enum MasterToolRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}
