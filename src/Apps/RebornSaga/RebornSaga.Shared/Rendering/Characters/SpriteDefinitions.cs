namespace RebornSaga.Rendering.Characters;

using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

/// <summary>
/// Pose eines Charakters. Bestimmt welches komplette Bild geladen wird.
/// Wird zusammen mit Emotion zum Asset-Pfad kombiniert: {pose}_{emotion}.webp
/// </summary>
public enum Pose
{
    Standing,
    Battle,
    Sitting,
    Kneeling,
    Floating,   // System_ARIA
    Lying,      // Verwundete Szenen
    Running
}

/// <summary>
/// Metadaten für einen Charakter-Sprite-Ordner.
/// Beschreibt welche Pose+Emotion-Kombinationen verfügbar sind
/// und wo die Overlays (Blinzeln, Mund) liegen.
/// </summary>
public class CharacterSpriteData
{
    /// <summary>Charakter-ID (z.B. "aria", "protag_sword").</summary>
    [JsonPropertyName("characterId")]
    public string CharacterId { get; set; } = "";

    /// <summary>
    /// Verfügbare Sprite-Dateien (relative Pfade innerhalb des Charakter-Ordners).
    /// Z.B. ["standing_neutral", "standing_happy", "battle_determined"]
    /// </summary>
    [JsonPropertyName("sprites")]
    public List<string> Sprites { get; set; } = new();

    /// <summary>
    /// Overlay-Positionen (Pixel-Koordinaten im Sprite-Bild).
    /// Für Blinzel- und Mund-Overlays.
    /// </summary>
    [JsonPropertyName("overlays")]
    public OverlayInfo? Overlays { get; set; }
}

/// <summary>
/// Positionen der Overlay-Regionen im Charakter-Sprite.
/// </summary>
public class OverlayInfo
{
    /// <summary>Augen-Region für Blinzel-Overlay (x, y, w, h).</summary>
    [JsonPropertyName("eyeRegion")]
    public SpriteRegion? EyeRegion { get; set; }

    /// <summary>Mund-Region für Sprech-Overlay (x, y, w, h).</summary>
    [JsonPropertyName("mouthRegion")]
    public SpriteRegion? MouthRegion { get; set; }
}

/// <summary>
/// Rechteck-Region in einem Sprite-Bild (Pixel-Koordinaten).
/// </summary>
public class SpriteRegion
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("w")]
    public int W { get; set; }

    [JsonPropertyName("h")]
    public int H { get; set; }
}

/// <summary>
/// Statische Helfer für Asset-Pfade. Alle Pfade relativ zum Asset-Root
/// (wird von IAssetDeliveryService aufgelöst).
/// Neue Struktur: Einzelbilder pro Pose+Emotion statt Atlanten.
/// </summary>
public static class SpriteAssetPaths
{
    private const string CharacterDir = "characters";
    private const string EnemyDir = "enemies";
    private const string BackgroundDir = "backgrounds";
    private const string SceneDir = "scenes";
    private const string ItemDir = "items";

    /// <summary>
    /// Pfad zum kompletten Charakter-Sprite (Pose + Emotion).
    /// Z.B. "characters/aria/full/standing_neutral.webp"
    /// </summary>
    public static string GetCharacterSpritePath(string charId, Pose pose, Emotion emotion)
    {
        var poseName = pose.ToString().ToLowerInvariant();
        var emotionName = emotion.ToString().ToLowerInvariant();
        return $"{CharacterDir}/{charId}/full/{poseName}_{emotionName}.webp";
    }

    /// <summary>
    /// Pfad zum Blinzel-Overlay eines Charakters.
    /// Z.B. "characters/aria/overlays/blink.webp"
    /// </summary>
    public static string GetBlinkOverlayPath(string charId)
        => $"{CharacterDir}/{charId}/overlays/blink.webp";

    /// <summary>
    /// Pfad zum Mund-Overlay eines Charakters (offen/weit).
    /// Z.B. "characters/aria/overlays/mouth_open.webp"
    /// </summary>
    public static string GetMouthOverlayPath(string charId, bool wide)
        => $"{CharacterDir}/{charId}/overlays/{(wide ? "mouth_wide" : "mouth_open")}.webp";

    /// <summary>Pfad zur Charakter-Metadaten-JSON.</summary>
    public static string GetCharacterMetaPath(string charId)
        => $"{CharacterDir}/{charId}/meta.json";

    /// <summary>Pfad zum Sprite eines Gegners (einzelnes Bild).</summary>
    public static string GetEnemySpritePath(string enemyId)
        => $"{EnemyDir}/{enemyId}.webp";

    /// <summary>Pfad zum Gegner-Sprite für eine bestimmte Phase (Multi-Phase Bosse).</summary>
    public static string GetEnemyPhaseSpritePath(string enemyId, int phase)
        => $"{EnemyDir}/{enemyId}_phase{phase}.webp";

    /// <summary>
    /// Pfad zu einem Hintergrund-Bild (AI-generiert).
    /// Konvertiert camelCase Story-Keys zu snake_case Dateinamen:
    /// "forestDay" → "backgrounds/forest_day.webp"
    /// </summary>
    public static string GetBackgroundPath(string sceneKey)
        => $"{BackgroundDir}/{ToSnakeCase(sceneKey)}.webp";

    /// <summary>Pfad zu einer animierten Szene (CG/Cutscene als Animated WebP).</summary>
    public static string GetAnimatedScenePath(string sceneId)
        => $"{SceneDir}/{sceneId}.webp";

    /// <summary>Pfad zu einem Item-Icon.</summary>
    public static string GetItemIconPath(string category, string itemId)
        => $"{ItemDir}/{category}/{itemId}.webp";

    /// <summary>Pfad zum Title Key Visual.</summary>
    public static string GetTitleKeyVisualPath()
        => "title_key_visual.webp";

    /// <summary>
    /// Konvertiert camelCase zu snake_case für Asset-Dateinamen.
    /// "forestDay" → "forest_day", "villageSquare" → "village_square"
    /// Bereits snake_case oder einwortige Keys bleiben unverändert.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length + 4);
        sb.Append(char.ToLowerInvariant(input[0]));
        for (var i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(input[i]));
            }
            else
            {
                sb.Append(input[i]);
            }
        }
        return sb.ToString();
    }
}
