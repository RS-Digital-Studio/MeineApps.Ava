namespace RebornSaga.Rendering.Characters;

/// <summary>
/// Render-Modus bestimmt Detailgrad und sichtbare Körperteile.
/// </summary>
public enum RenderMode
{
    Portrait,   // Brust aufwärts (Dialoge) — mehr Gesichtsdetail
    FullBody,   // Ganzer Körper (Klassenwahl, Status)
    Icon        // Nur Kopf (Save-Slots, Inventar)
}

/// <summary>
/// Zentrale Positionsberechnung für alle Charakter-Renderer.
/// Wird einmal pro DrawCharacter()-Aufruf berechnet (struct = kein Heap).
/// Alle Renderer arbeiten mit diesem Layout statt rohen (cx, cy, scale).
/// </summary>
public readonly struct CharacterLayout
{
    // Kopf
    public readonly float HeadCenterX;
    public readonly float HeadCenterY;
    public readonly float HeadWidth;   // Halbe Breite des Kopf-Ovals
    public readonly float HeadHeight;  // Halbe Höhe des Kopf-Ovals

    // Hals
    public readonly float NeckTopY;    // Oberkante Hals (= untere Kinn-Linie)
    public readonly float NeckBottomY; // Unterkante Hals (= Schulter-Ansatz)
    public readonly float NeckWidth;   // Halbe Breite des Halses

    // Schultern
    public readonly float ShoulderLeftX;
    public readonly float ShoulderRightX;
    public readonly float ShoulderY;

    // Augen
    public readonly float EyeY;
    public readonly float EyeOffsetX;  // Abstand der Augen von der Mitte
    public readonly float EyeSize;     // Basis-Größe eines Auges

    // Nase + Mund
    public readonly float NoseY;
    public readonly float MouthY;

    // Ohren
    public readonly float EarY;

    // Körper
    public readonly float BodyTop;     // Oberkante Körper (unter Schultern)
    public readonly float BodyBottom;  // Unterkante (abhängig von RenderMode)
    public readonly float BodyWidth;   // Halbe Breite auf Schulterhöhe

    // Meta
    public readonly float Scale;
    public readonly RenderMode Mode;

    private CharacterLayout(float cx, float cy, float scale, RenderMode mode)
    {
        Scale = scale;
        Mode = mode;

        // Kopf — leicht größer als bisherige 28x34 für bessere Proportionen
        HeadCenterX = cx;
        HeadCenterY = cy;
        HeadWidth = 30 * scale;
        HeadHeight = 36 * scale;

        // Augen — im oberen Drittel des Gesichts
        EyeY = cy - 3 * scale;
        EyeOffsetX = 11 * scale;
        EyeSize = 5.5f * scale;

        // Nase — knapp unter den Augen
        NoseY = cy + 6 * scale;

        // Mund — unteres Drittel des Gesichts
        MouthY = cy + 14 * scale;

        // Ohren — auf Augenhöhe
        EarY = cy - 2 * scale;

        // Hals — kurzer Übergang zwischen Kinn und Schultern
        NeckTopY = cy + HeadHeight * 0.65f;
        NeckBottomY = cy + HeadHeight * 0.9f;
        NeckWidth = 8 * scale;

        // Schultern
        ShoulderY = NeckBottomY;
        var shoulderSpan = 38 * scale;
        ShoulderLeftX = cx - shoulderSpan * 0.55f;
        ShoulderRightX = cx + shoulderSpan * 0.55f;

        // Körper
        BodyTop = ShoulderY;
        BodyWidth = shoulderSpan * 0.55f;
        BodyBottom = mode switch
        {
            RenderMode.Portrait => cy + 90 * scale,    // Bis Brust/Bauch
            RenderMode.FullBody => cy + 130 * scale,    // Bis Hüfte
            _ => cy + HeadHeight                         // Icon: kein Körper
        };
    }

    /// <summary>
    /// Berechnet das Layout für einen Charakter.
    /// </summary>
    public static CharacterLayout Calculate(float cx, float cy, float scale, RenderMode mode = RenderMode.Portrait)
    {
        return new CharacterLayout(cx, cy, scale, mode);
    }
}
