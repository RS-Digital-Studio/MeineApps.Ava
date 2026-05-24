using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class ArMathHelpersTests
{
    // ===== Bowditch =====

    [Fact]
    public void ApplyBowditchCorrection_NichtGeschlossen_KeineAenderung()
    {
        var c = new ArContour { IsClosed = false };
        c.Points.Add(new ArPoint { X = 0, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 1, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 1, Y = 0, Z = 1 });
        var before = c.Points[^1].X;
        ArMathHelpers.ApplyBowditchCorrection(c);
        c.Points[^1].X.Should().Be(before);
    }

    [Fact]
    public void ApplyBowditchCorrection_GeschlossenMitSchlussfehler_GleichmaessigVerteilt()
    {
        // Quadrat (0,0)-(2,0)-(2,2)-(0,2)-(0.1,0.05) — der "letzte" Punkt liegt 10cm
        // off vom ersten (Schluss-Vektor = (-0.1, 0, -0.05)). Bowditch verteilt das.
        var c = new ArContour { IsClosed = true };
        c.Points.Add(new ArPoint { X = 0, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 2, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 2, Y = 0, Z = 2 });
        c.Points.Add(new ArPoint { X = 0, Y = 0, Z = 2 });
        c.Points.Add(new ArPoint { X = 0.1f, Y = 0, Z = 0.05f }); // sollte (0,0,0) sein

        ArMathHelpers.ApplyBowditchCorrection(c);

        // Letzter Punkt = exakt erster Punkt nach Korrektur
        c.Points[^1].X.Should().Be(0f);
        c.Points[^1].Y.Should().Be(0f);
        c.Points[^1].Z.Should().Be(0f);

        // Mittlere Punkte sind etwas verschoben (anteilig zur Distanz)
        c.Points[1].X.Should().BeApproximately(2f - 0.025f, 0.01f);
        c.Points[2].X.Should().BeApproximately(2f - 0.050f, 0.01f);
        c.Points[3].X.Should().BeApproximately(0f - 0.075f, 0.01f);
    }

    [Fact]
    public void ApplyBowditchCorrection_FehlerUnter1cm_KeineKorrektur()
    {
        // Schlussfehler 5mm — unter Schwelle, soll nicht korrigiert werden.
        var c = new ArContour { IsClosed = true };
        c.Points.Add(new ArPoint { X = 0, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 1, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 1, Y = 0, Z = 1 });
        c.Points.Add(new ArPoint { X = 0.005f, Y = 0, Z = 0 });

        var lastBefore = (c.Points[^1].X, c.Points[^1].Z);
        ArMathHelpers.ApplyBowditchCorrection(c);
        (c.Points[^1].X, c.Points[^1].Z).Should().Be(lastBefore);
    }

    [Fact]
    public void ApplyBowditchCorrection_FehlerUeber2m_KeineKorrektur()
    {
        // Schlussfehler 3m — viel zu gross, das ist ein echter Mess-Fehler, nicht
        // Float-Drift. Bowditch laesst es liegen damit der User es sieht.
        var c = new ArContour { IsClosed = true };
        c.Points.Add(new ArPoint { X = 0, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 5, Y = 0, Z = 0 });
        c.Points.Add(new ArPoint { X = 5, Y = 0, Z = 5 });
        c.Points.Add(new ArPoint { X = 3f, Y = 0, Z = 0 });

        var lastBefore = c.Points[^1].X;
        ArMathHelpers.ApplyBowditchCorrection(c);
        c.Points[^1].X.Should().Be(lastBefore);
    }

    // ===== Quaternion → Heading =====
    // Quaternion-Konventionen: Identity = (0,0,0,1). Rotation um Y-Achse um Winkel a:
    //   (0, sin(a/2), 0, cos(a/2))
    // Die Activity nutzt die zu erwartende ARCore-Definition: -Z lokal = Blickrichtung,
    // Welt-Koord: +X=Ost, -Z=Nord.

    [Fact]
    public void ExtractHeading_IdentityQuaternion_LiefertNord()
    {
        // Identity → Kamera blickt Richtung -Z, also Norden. Heading = 0.
        var h = ArMathHelpers.ExtractHeadingFromQuaternion(0f, 0f, 0f, 1f);
        h.Should().NotBeNull();
        h!.Value.Should().BeApproximately(0f, 0.5f);
    }

    [Fact]
    public void ExtractHeading_YawNach90Grad_LiefertOst()
    {
        // Y-Achsen-Rotation um -90° (Rechts-Drehung im Welt-System: Kamera blickt nach Osten)
        var a = -MathF.PI / 4f; // halber Winkel
        var h = ArMathHelpers.ExtractHeadingFromQuaternion(0f, MathF.Sin(a), 0f, MathF.Cos(a));
        h.Should().NotBeNull();
        h!.Value.Should().BeApproximately(90f, 1f);
    }

    [Fact]
    public void ExtractHeading_YawNach180Grad_LiefertSued()
    {
        // 180° Yaw → Kamera blickt nach Sueden
        var h = ArMathHelpers.ExtractHeadingFromQuaternion(0f, 1f, 0f, 0f);
        h.Should().NotBeNull();
        h!.Value.Should().BeApproximately(180f, 1f);
    }

    [Fact]
    public void ExtractHeading_YawMinus90Grad_LiefertWest()
    {
        // +90° Yaw → Kamera blickt nach Westen (im Welt-System Links-Drehung)
        var a = MathF.PI / 4f;
        var h = ArMathHelpers.ExtractHeadingFromQuaternion(0f, MathF.Sin(a), 0f, MathF.Cos(a));
        h.Should().NotBeNull();
        h!.Value.Should().BeApproximately(270f, 1f);
    }

    [Fact]
    public void ExtractHeading_KameraSteilNachUnten_LiefertNull()
    {
        // 80° Pitch nach unten: horizontaler Anteil zu klein → null erwartet.
        // X-Achsen-Rotation um -80°: q = (sin(-40°), 0, 0, cos(-40°))
        var a = -80f * MathF.PI / 360f; // halber Winkel
        var h = ArMathHelpers.ExtractHeadingFromQuaternion(MathF.Sin(a), 0f, 0f, MathF.Cos(a));
        h.Should().BeNull();
    }

    // ===== Quaternion → Pitch =====

    [Fact]
    public void ExtractPitch_IdentityQuaternion_LiefertNullGrad()
    {
        // Identity → Kamera horizontal → Pitch 0.
        var p = ArMathHelpers.ExtractPitchFromQuaternion(0f, 0f, 0f, 1f);
        p.Should().BeApproximately(0f, 0.5f);
    }

    [Fact]
    public void ExtractPitch_KameraNachOben30Grad_Liefert30()
    {
        // X-Achsen-Rotation um +30° = Kamera blickt 30° nach oben.
        // q = (sin(15°), 0, 0, cos(15°))
        var a = 30f * MathF.PI / 360f;
        var p = ArMathHelpers.ExtractPitchFromQuaternion(MathF.Sin(a), 0f, 0f, MathF.Cos(a));
        p.Should().BeApproximately(30f, 1f);
    }

    [Fact]
    public void ExtractPitch_KameraNachUnten45Grad_LiefertMinus45()
    {
        var a = -45f * MathF.PI / 360f;
        var p = ArMathHelpers.ExtractPitchFromQuaternion(MathF.Sin(a), 0f, 0f, MathF.Cos(a));
        p.Should().BeApproximately(-45f, 1f);
    }
}
