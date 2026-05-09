namespace BomberBlast.Core.Audio;

/// <summary>
/// Helper-Methoden fuer raeumliches Audio (Stereo-Pan + Distance-Falloff).
/// SoundPool kann kein echtes 3D-Audio, aber wir koennen Volume + Pan deterministisch
/// aus Spielfeld-Koordinaten ableiten. Das gibt 2D-Game-Feel-Eindruck (Bomben links/rechts hoerbar).
/// </summary>
public static class AudioSpatial
{
    /// <summary>
    /// Berechnet den Stereo-Pan-Wert aus Sound-Position relativ zum Spieler.
    /// </summary>
    /// <param name="soundGridX">Grid-X der Schallquelle.</param>
    /// <param name="playerGridX">Grid-X des Spielers.</param>
    /// <param name="gridWidth">Spielfeldbreite (typisch 15).</param>
    /// <returns>Pan-Wert in [-1, 1]. 0 = mittig.</returns>
    public static float CalculatePan(int soundGridX, int playerGridX, int gridWidth)
    {
        if (gridWidth <= 0) return 0f;
        // dx skaliert von -gridWidth/2 .. +gridWidth/2 in [-1, 1]
        var dx = (float)(soundGridX - playerGridX);
        var halfWidth = gridWidth / 2f;
        if (halfWidth <= 0f) return 0f;
        return Math.Clamp(dx / halfWidth, -1f, 1f);
    }

    /// <summary>
    /// Distance-Falloff: weit entfernte Sounds werden leiser.
    /// Manhattan-Distanz, linear interpoliert zwischen FullVolumeRadius und SilenceRadius.
    /// </summary>
    /// <param name="soundGridX">Grid-X der Schallquelle.</param>
    /// <param name="soundGridY">Grid-Y der Schallquelle.</param>
    /// <param name="playerGridX">Grid-X des Spielers.</param>
    /// <param name="playerGridY">Grid-Y des Spielers.</param>
    /// <param name="fullVolumeRadius">Radius mit voller Lautstärke (Default 3 Zellen).</param>
    /// <param name="silenceRadius">Radius ab dem 0% Lautstärke (Default 12 Zellen).</param>
    /// <returns>Volume-Multiplikator [0, 1].</returns>
    public static float CalculateDistanceVolume(
        int soundGridX, int soundGridY,
        int playerGridX, int playerGridY,
        int fullVolumeRadius = 3, int silenceRadius = 12)
    {
        var dx = Math.Abs(soundGridX - playerGridX);
        var dy = Math.Abs(soundGridY - playerGridY);
        var dist = dx + dy;
        if (dist <= fullVolumeRadius) return 1f;
        if (dist >= silenceRadius) return 0f;
        var range = silenceRadius - fullVolumeRadius;
        var falloff = (silenceRadius - dist) / (float)range;
        return Math.Clamp(falloff, 0f, 1f);
    }

    /// <summary>
    /// Equal-Power-Crossfade-Faktor (sin/cos-Kurve statt linear).
    /// </summary>
    /// <param name="t">Crossfade-Phase [0, 1] — 0 = voll alter Track, 1 = voll neuer Track.</param>
    /// <returns>Tuple (oldVolume, newVolume) — beide [0, 1].</returns>
    public static (float oldVolume, float newVolume) EqualPowerCrossfade(float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        // Equal-Power: cos(t*PI/2) für alten, sin(t*PI/2) für neuen
        var rad = clamped * (float)(Math.PI / 2.0);
        var oldVol = (float)Math.Cos(rad);
        var newVol = (float)Math.Sin(rad);
        return (oldVol, newVol);
    }
}

/// <summary>
/// Reverb-Preset pro Welt. Mapping zu echtem Hardware-Reverb ist plattform-spezifisch
/// (Android AudioFx kann ReverbPresets, SoundPool nicht). Diese Klasse stellt eine
/// abstrakte Beschreibung bereit, die SoundService-Implementierungen umsetzen können.
/// </summary>
public enum ReverbPreset
{
    /// <summary>Trockener Sound, kein Reverb.</summary>
    None = 0,

    /// <summary>Kleiner Raum (Default für Menü, Industrial).</summary>
    SmallRoom = 1,

    /// <summary>Großer Raum (Default für Forest mit Echo).</summary>
    LargeRoom = 2,

    /// <summary>Höhle (langer Hall, dunkel) — passend für Cavern-Welt + Dungeon.</summary>
    Cave = 3,

    /// <summary>Halle (langer, klarer Reverb) — passend für Sky-Welt.</summary>
    Hall = 4,

    /// <summary>Outdoor mit weitem Echo — passend für Inferno-Welt.</summary>
    Outdoor = 5,
}

/// <summary>
/// Mapping: Welt-Index → ReverbPreset.
/// </summary>
public static class WorldReverbMap
{
    /// <summary>Liefert das Reverb-Preset für eine Welt (0-basiert, 0=Forest..4=Inferno).</summary>
    public static ReverbPreset GetPresetForWorld(int worldIndex) => worldIndex switch
    {
        0 => ReverbPreset.LargeRoom,    // Forest = großer offener Wald
        1 => ReverbPreset.SmallRoom,    // Industrial = enge Räume mit Stahl-Reflexion
        2 => ReverbPreset.Cave,         // Cavern = klassische Höhle
        3 => ReverbPreset.Hall,         // Sky = wolkige Halle
        4 => ReverbPreset.Outdoor,      // Inferno = Lava-Outdoor
        _ => ReverbPreset.None,
    };

    /// <summary>Reverb für Dungeon-Modus.</summary>
    public static ReverbPreset DungeonPreset => ReverbPreset.Cave;
}
