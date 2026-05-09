namespace BomberBlast.Core.Combat;

/// <summary>
/// Combo-System (v2.0.54 — AAA-Audit Phase 12, Subsystem-Extraktion aus GameEngine.Collision.cs).
/// Pure-Logic-Klasse: Counter + Timer + Score-Bonus-Berechnung. Keine Engine-Referenzen.
/// Macht die Combo-Mechanik isoliert testbar (Window-Verlängerung, Score-Tabelle, ULTRA-Schwelle).
/// </summary>
public sealed class ComboSystem
{
    /// <summary>Standard-Window 2 Sekunden — bei Combo ≥ 6 wird auf 2.5s verlängert.</summary>
    public const float COMBO_WINDOW = 2f;

    /// <summary>Verlängerung des Windows bei hoher Combo (≥6).</summary>
    public const float COMBO_WINDOW_EXTENSION = 0.5f;

    /// <summary>ULTRA-Combo-Schwelle (Game-Juice + Subtitle-Throttling).</summary>
    public const int ULTRA_THRESHOLD = 10;

    /// <summary>MEGA-Combo-Schwelle.</summary>
    public const int MEGA_THRESHOLD = 5;

    /// <summary>CHAIN-Kill-Schwelle (3+).</summary>
    public const int CHAIN_THRESHOLD = 3;

    /// <summary>Aktuelle Combo-Anzahl.</summary>
    public int Count { get; private set; }

    /// <summary>Verbleibende Window-Zeit in Sekunden.</summary>
    public float Timer { get; private set; }

    /// <summary>Ob aktuell ein aktives Combo-Window läuft.</summary>
    public bool IsActive => Timer > 0;

    /// <summary>Ob die Combo eine Chain-Kill ist (3+).</summary>
    public bool IsChainKill => Count >= CHAIN_THRESHOLD;

    /// <summary>Ob die Combo Mega ist (5+).</summary>
    public bool IsMega => Count >= MEGA_THRESHOLD;

    /// <summary>Ob die Combo Ultra ist (10+).</summary>
    public bool IsUltra => Count >= ULTRA_THRESHOLD;

    /// <summary>
    /// Wird bei jedem Enemy-Kill aufgerufen. Erhöht den Counter (oder reset auf 1 wenn Timer abgelaufen),
    /// setzt das Window neu (mit Verlängerung bei hoher Combo).
    /// </summary>
    public void RegisterKill()
    {
        if (Timer > 0)
            Count++;
        else
            Count = 1;

        Timer = Count >= 6 ? COMBO_WINDOW + COMBO_WINDOW_EXTENSION : COMBO_WINDOW;
    }

    /// <summary>
    /// Pro Frame aufrufen — verringert den Timer. Bei Timer ≤ 0 wird die Combo bei nächstem Kill neu gestartet.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (Timer > 0)
        {
            Timer -= deltaTime;
            if (Timer < 0) Timer = 0;
        }
    }

    /// <summary>
    /// Score-Bonus für die aktuelle Combo. Gestaffelte Tabelle x2 → x10+.
    /// Pure Funktion — testbar ohne Engine-State.
    /// </summary>
    public int GetScoreBonus() => Count switch
    {
        < 2  => 0,
        2    => 200,
        3    => 500,
        4    => 1000,
        5    => 2000,
        6    => 4000,
        7    => 8000,
        8    => 15000,
        9    => 20000,
        _    => 30000,  // 10+
    };

    /// <summary>
    /// Slow-Motion-Dauer in Sekunden für die aktuelle Combo (1.5× bei ULTRA).
    /// </summary>
    public float GetSlowMotionDuration(float baseDuration) =>
        Count >= ULTRA_THRESHOLD ? baseDuration * 1.5f : baseDuration;

    /// <summary>
    /// Reset: Timer auf 0, Counter auf 0. Wird bei Level-Wechsel/GameOver aufgerufen.
    /// </summary>
    public void Reset()
    {
        Count = 0;
        Timer = 0;
    }
}
