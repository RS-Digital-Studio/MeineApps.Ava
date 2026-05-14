using BomberBlast.Models.Cosmetics;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Prozeduraler Victory-Animator (Phase 29c — Cosmetic-Integration).
///
/// <para>Liefert pro Frame ein <see cref="VictoryFrame"/>-Struct mit Sprite-Transformation
/// (Scale-X, Scale-Y, Rotation, Y-Offset) und optionalen Effekt-Hooks (Particle-Burst-Trigger,
/// Camera-Shake-Trigger). Der Renderer wendet die Transformation auf das Player-Sprite an.</para>
///
/// <para>Pattern: <see cref="GetFrame(VictoryAnimationType, float, float)"/> liefert für die
/// gewünschte Animations-Type bei normalisierter Zeit <c>t ∈ [0, 1]</c> die aktuelle Frame-Daten.
/// Reine Pure-Funktion — kein Side-Effect, kein State.</para>
/// </summary>
public static class VictoryAnimator
{
    /// <summary>Frame-Daten für das Player-Sprite (relative Transformations).</summary>
    public readonly struct VictoryFrame
    {
        public VictoryFrame(float scaleX, float scaleY, float rotation, float offsetY,
            bool particleTrigger = false, SKColor particleColor = default)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            Rotation = rotation;
            OffsetY = offsetY;
            ParticleTrigger = particleTrigger;
            ParticleColor = particleColor;
        }

        public float ScaleX { get; }
        public float ScaleY { get; }
        public float Rotation { get; } // Grad
        public float OffsetY { get; }
        public bool ParticleTrigger { get; }
        public SKColor ParticleColor { get; }

        /// <summary>Statisches Default (keine Animation).</summary>
        public static VictoryFrame Identity => new(1, 1, 0, 0);
    }

    /// <summary>
    /// Liefert die Frame-Daten für die gewünschte Victory-Animation.
    /// </summary>
    /// <param name="type">Animation-Type aus VictoryDefinition.</param>
    /// <param name="t">Normalisierte Zeit [0, 1].</param>
    /// <param name="duration">Vollständige Dauer in Sekunden (für Frequenz-Skalierung).</param>
    public static VictoryFrame GetFrame(VictoryAnimationType type, float t, float duration = 2f)
    {
        t = Math.Clamp(t, 0f, 1f);

        return type switch
        {
            // === Common ===
            VictoryAnimationType.Wave => GetWaveFrame(t),
            VictoryAnimationType.Jump => GetJumpFrame(t),
            VictoryAnimationType.Clap => GetClapFrame(t),
            VictoryAnimationType.Nod => GetNodFrame(t),
            VictoryAnimationType.Spin => GetSpinFrame(t),

            // === Rare ===
            VictoryAnimationType.Dance => GetDanceFrame(t),
            VictoryAnimationType.Flex => GetFlexFrame(t),
            VictoryAnimationType.Backflip => GetBackflipFrame(t),
            VictoryAnimationType.Headbang => GetHeadbangFrame(t),
            VictoryAnimationType.Moonwalk => GetMoonwalkFrame(t),

            // === Epic ===
            VictoryAnimationType.Dab => GetDabFrame(t),
            VictoryAnimationType.Breakdance => GetBreakdanceFrame(t),
            VictoryAnimationType.Tornado => GetTornadoFrame(t),
            VictoryAnimationType.FireDance => GetFireDanceFrame(t),
            VictoryAnimationType.FrostAura => GetFrostAuraFrame(t),

            // === Legendary ===
            VictoryAnimationType.DragonRoar => GetDragonRoarFrame(t),
            VictoryAnimationType.SuperNova => GetSuperNovaFrame(t),
            VictoryAnimationType.GoldExplosion => GetGoldExplosionFrame(t),

            // === Phase 29 — Welt-thematisch ===
            VictoryAnimationType.PumpkinBurst => GetPumpkinBurstFrame(t),
            VictoryAnimationType.SnowflakeSpin => GetSnowflakeSpinFrame(t),
            VictoryAnimationType.CherryBloom => GetCherryBloomFrame(t),
            VictoryAnimationType.NeonGlitch => GetNeonGlitchFrame(t),
            VictoryAnimationType.BoneRattle => GetBoneRattleFrame(t),
            VictoryAnimationType.OceanSplash => GetOceanSplashFrame(t),
            VictoryAnimationType.MechSalute => GetMechSaluteFrame(t),
            VictoryAnimationType.SunBurst => GetSunBurstFrame(t),
            VictoryAnimationType.SamuraiBow => GetSamuraiBowFrame(t),
            VictoryAnimationType.SteamWhistle => GetSteamWhistleFrame(t),

            // === Phase 29 — Karriere-Status ===
            VictoryAnimationType.PrestigeFlare => GetPrestigeFlareFrame(t),
            VictoryAnimationType.DiamondCascade => GetDiamondCascadeFrame(t),
            VictoryAnimationType.AscensionRise => GetAscensionRiseFrame(t),
            VictoryAnimationType.ChampionPose => GetChampionPoseFrame(t),
            VictoryAnimationType.SeasonFinale => GetSeasonFinaleFrame(t),

            _ => VictoryFrame.Identity,
        };
    }

    // === Common ===========================================================

    private static VictoryFrame GetWaveFrame(float t)
    {
        // Sanftes seitliches Wackeln + leichte Y-Hebung
        return new(1, 1, MathF.Sin(t * MathF.PI * 4) * 8, -MathF.Sin(t * MathF.PI) * 5);
    }

    private static VictoryFrame GetJumpFrame(float t)
    {
        // Parabel: rauf in 1. Hälfte, runter in 2.
        var arc = MathF.Sin(t * MathF.PI);
        return new(1, 1, 0, -arc * 30);
    }

    private static VictoryFrame GetClapFrame(float t)
    {
        // X-Squash beim Klatschen (3 Wellen)
        var pulse = 1f + MathF.Sin(t * MathF.PI * 6) * 0.1f;
        return new(pulse, 1, 0, 0);
    }

    private static VictoryFrame GetNodFrame(float t)
    {
        // Vertikales Nicken (kleine Y-Squash + Offset)
        var nod = MathF.Sin(t * MathF.PI * 4);
        return new(1, 1f - nod * 0.05f, 0, nod * 3);
    }

    private static VictoryFrame GetSpinFrame(float t)
    {
        // 360° Drehung
        return new(1, 1, t * 360, 0);
    }

    // === Rare ===========================================================

    private static VictoryFrame GetDanceFrame(float t)
    {
        // Seitwärts-Schritt + leichte Rotation
        var sway = MathF.Sin(t * MathF.PI * 4);
        return new(1, 1, sway * 5, 0);
    }

    private static VictoryFrame GetFlexFrame(float t)
    {
        // Erst aufpumpen, dann halten
        var pump = t < 0.5f ? t * 2 : 1f;
        return new(1f + pump * 0.15f, 1f + pump * 0.1f, 0, 0);
    }

    private static VictoryFrame GetBackflipFrame(float t)
    {
        // Vollständige Rotation rückwärts + Höhe
        var arc = MathF.Sin(t * MathF.PI);
        return new(1, 1, -t * 360, -arc * 25);
    }

    private static VictoryFrame GetHeadbangFrame(float t)
    {
        // Schnelle Vor/Zurück-Rotation
        return new(1, 1, MathF.Sin(t * MathF.PI * 8) * 25, 0);
    }

    private static VictoryFrame GetMoonwalkFrame(float t)
    {
        // Subtle Y-Squash + langsame seitliche Drift
        var glide = MathF.Sin(t * MathF.PI * 3);
        return new(1, 0.95f + MathF.Sin(t * MathF.PI * 6) * 0.05f, glide * 4, 0);
    }

    // === Epic ============================================================

    private static VictoryFrame GetDabFrame(float t)
    {
        // Schnelles Aufrichten + Halten + Lösen
        var pose = t < 0.3f ? t / 0.3f : t > 0.85f ? (1f - t) / 0.15f : 1f;
        return new(1f + pose * 0.1f, 1, pose * 15, -pose * 3);
    }

    private static VictoryFrame GetBreakdanceFrame(float t)
    {
        // Mehrfache Rotation + Y-Squash
        var arc = MathF.Sin(t * MathF.PI);
        return new(1f + arc * 0.2f, 1f - arc * 0.1f, t * 720, -arc * 15);
    }

    private static VictoryFrame GetTornadoFrame(float t)
    {
        // Spirale: schnelle Rotation + abnehmende Y-Hebung
        var arc = MathF.Sin(t * MathF.PI);
        return new(1, 1, t * 1080, -arc * 20, particleTrigger: t < 0.1f, particleColor: new SKColor(180, 220, 255));
    }

    private static VictoryFrame GetFireDanceFrame(float t)
    {
        var sway = MathF.Sin(t * MathF.PI * 6);
        return new(1, 1, sway * 8, 0,
            particleTrigger: (t * 6) % 1 < 0.1f,
            particleColor: new SKColor(255, 100, 30));
    }

    private static VictoryFrame GetFrostAuraFrame(float t)
    {
        // Pulsierend wachsende Aura
        var pulse = 1f + MathF.Sin(t * MathF.PI * 4) * 0.08f;
        return new(pulse, pulse, 0, 0,
            particleTrigger: (t * 4) % 1 < 0.1f,
            particleColor: new SKColor(160, 220, 255));
    }

    // === Legendary =======================================================

    private static VictoryFrame GetDragonRoarFrame(float t)
    {
        // Initiales Aufrichten + Schockwelle in der Mitte
        var pump = t < 0.4f ? t / 0.4f : 1f;
        var shake = t > 0.4f && t < 0.6f ? MathF.Sin(t * MathF.PI * 30) * 4 : 0;
        return new(1f + pump * 0.2f, 1f + pump * 0.15f, shake, -pump * 5,
            particleTrigger: t > 0.4f && t < 0.5f,
            particleColor: new SKColor(255, 80, 30));
    }

    private static VictoryFrame GetSuperNovaFrame(float t)
    {
        // Wachsende Skala + finale Mega-Explosion
        var grow = 1f + t * 0.5f;
        return new(grow, grow, t * 360, -t * 10,
            particleTrigger: t > 0.85f,
            particleColor: new SKColor(255, 220, 100));
    }

    private static VictoryFrame GetGoldExplosionFrame(float t)
    {
        // Subtiler Bounce + Gold-Konfetti-Trigger
        var bounce = MathF.Sin(t * MathF.PI * 2) * 0.1f;
        return new(1f + bounce, 1f - bounce * 0.3f, 0, -MathF.Sin(t * MathF.PI) * 8,
            particleTrigger: (t * 8) % 1 < 0.15f,
            particleColor: BomberBlastColors.Gold);
    }

    // === Phase 29 — Welt-thematisch ======================================

    private static VictoryFrame GetPumpkinBurstFrame(float t)
    {
        var pulse = MathF.Sin(t * MathF.PI * 3);
        return new(1f + pulse * 0.15f, 1f - pulse * 0.05f, 0, 0,
            particleTrigger: t > 0.5f && t < 0.55f,
            particleColor: new SKColor(255, 120, 0));
    }

    private static VictoryFrame GetSnowflakeSpinFrame(float t)
    {
        return new(1, 1, t * 540, 0,
            particleTrigger: (t * 6) % 1 < 0.1f,
            particleColor: new SKColor(220, 240, 255));
    }

    private static VictoryFrame GetCherryBloomFrame(float t)
    {
        var grow = 1f + t * 0.2f;
        return new(grow, grow, t * 30, -MathF.Sin(t * MathF.PI) * 5,
            particleTrigger: (t * 4) % 1 < 0.15f,
            particleColor: new SKColor(255, 180, 200));
    }

    private static VictoryFrame GetNeonGlitchFrame(float t)
    {
        // Rapides Glitchen
        var glitch = ((int)(t * 30) % 2 == 0) ? 1.1f : 0.9f;
        return new(glitch, 1, MathF.Sin(t * MathF.PI * 16) * 15, 0,
            particleTrigger: (t * 10) % 1 < 0.1f,
            particleColor: new SKColor(255, 0, 200));
    }

    private static VictoryFrame GetBoneRattleFrame(float t)
    {
        var rattle = MathF.Sin(t * MathF.PI * 12);
        return new(1f + rattle * 0.05f, 1f - rattle * 0.05f, rattle * 10, 0);
    }

    private static VictoryFrame GetOceanSplashFrame(float t)
    {
        var arc = MathF.Sin(t * MathF.PI);
        return new(1f + arc * 0.1f, 1f - arc * 0.1f, 0, -arc * 12,
            particleTrigger: t > 0.4f && t < 0.5f,
            particleColor: new SKColor(60, 180, 220));
    }

    private static VictoryFrame GetMechSaluteFrame(float t)
    {
        var pose = t < 0.5f ? t * 2 : 1f;
        return new(1, 1, pose * 8, -pose * 2,
            particleTrigger: t > 0.55f && t < 0.6f,
            particleColor: new SKColor(0, 220, 255));
    }

    private static VictoryFrame GetSunBurstFrame(float t)
    {
        var grow = 1f + MathF.Sin(t * MathF.PI) * 0.3f;
        return new(grow, grow, 0, 0,
            particleTrigger: (t * 6) % 1 < 0.15f,
            particleColor: new SKColor(255, 200, 50));
    }

    private static VictoryFrame GetSamuraiBowFrame(float t)
    {
        // Tiefe Verbeugung dann hoch
        var bow = MathF.Sin(t * MathF.PI);
        return new(1, 1f - bow * 0.15f, bow * 25, bow * 8);
    }

    private static VictoryFrame GetSteamWhistleFrame(float t)
    {
        var pulse = 1f + MathF.Sin(t * MathF.PI * 5) * 0.1f;
        return new(pulse, pulse, 0, 0,
            particleTrigger: (t * 5) % 1 < 0.15f,
            particleColor: new SKColor(180, 180, 200));
    }

    // === Phase 29 — Karriere-Status ======================================

    private static VictoryFrame GetPrestigeFlareFrame(float t)
    {
        // Iridescent: HSV-Cycle als Particle-Color
        var grow = 1f + MathF.Sin(t * MathF.PI * 2) * 0.15f;
        var hue = (t * 720f) % 360f;
        var color = HsvToColor(hue, 0.8f, 1f);
        return new(grow, grow, t * 180, -MathF.Sin(t * MathF.PI) * 8,
            particleTrigger: (t * 5) % 1 < 0.2f,
            particleColor: color);
    }

    private static VictoryFrame GetDiamondCascadeFrame(float t)
    {
        var pulse = 1f + MathF.Sin(t * MathF.PI * 3) * 0.1f;
        return new(pulse, pulse, 0, 0,
            particleTrigger: (t * 8) % 1 < 0.15f,
            particleColor: new SKColor(180, 240, 255));
    }

    private static VictoryFrame GetAscensionRiseFrame(float t)
    {
        // Stetiges Aufsteigen
        return new(1, 1, 0, -t * 25,
            particleTrigger: (t * 6) % 1 < 0.2f,
            particleColor: new SKColor(140, 80, 220));
    }

    private static VictoryFrame GetChampionPoseFrame(float t)
    {
        // Pose mit Trophäe — Aufrichten + halten
        var pose = t < 0.3f ? t / 0.3f : 1f;
        return new(1f + pose * 0.1f, 1f + pose * 0.1f, 0, -pose * 5,
            particleTrigger: t > 0.32f && t < 0.4f,
            particleColor: BomberBlastColors.Gold);
    }

    private static VictoryFrame GetSeasonFinaleFrame(float t)
    {
        // Konfetti-Storm — kontinuierliche Trigger
        return new(1f + MathF.Sin(t * MathF.PI * 4) * 0.05f, 1, 0, 0,
            particleTrigger: (t * 12) % 1 < 0.2f,
            particleColor: new SKColor(255, 100, 200));
    }

    private static SKColor HsvToColor(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
        float m = v - c;
        float r1 = 0, g1 = 0, b1 = 0;
        if (h < 60) { r1 = c; g1 = x; }
        else if (h < 120) { r1 = x; g1 = c; }
        else if (h < 180) { g1 = c; b1 = x; }
        else if (h < 240) { g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; b1 = c; }
        else { r1 = c; b1 = x; }
        return new SKColor((byte)((r1 + m) * 255), (byte)((g1 + m) * 255), (byte)((b1 + m) * 255));
    }
}
