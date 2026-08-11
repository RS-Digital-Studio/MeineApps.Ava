using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

/// <summary>Tests der Sitzungs-Einpassung (2D-Helmert über Passpunkte). Realer
/// CoordinateService — reine UTM-Mathematik ohne externe Abhaengigkeiten.
///
/// Aufbau der Faelle: ein "Bestand" wird definiert, dann eine kuenstlich verschobene und/oder
/// verdrehte Kopie als neue Sitzung uebergeben. Die Einpassung muss die Kopie wieder auf den
/// Bestand ziehen.</summary>
public class SessionRegistrationServiceTests
{
    private const double RefLat = 48.7758;
    private const double RefLon = 9.1829;

    private static readonly CoordinateService Coord = new();

    private static SessionRegistrationService MakeService() => new(Coord);

    /// <summary>Erzeugt einen SurveyPoint aus lokalen Meter-Koordinaten relativ zur Referenz.</summary>
    private static SurveyPoint At(double x, double y, string? label = null)
    {
        var (lat, lon, _) = Coord.LocalToLatLon(x, y, 0, RefLat, RefLon, 0);
        return new SurveyPoint { Latitude = lat, Longitude = lon, Altitude = 520, Label = label };
    }

    /// <summary>Lokale Meter-Koordinaten eines Punkts relativ zur Referenz (fuer Assertions).</summary>
    private static (double x, double y) Local(SurveyPoint p)
    {
        var (x, y, _) = Coord.LatLonToLocal(p.Latitude, p.Longitude, 0, RefLat, RefLon, 0);
        return (x, y);
    }

    [Fact]
    public void OhneBestand_LehntAbUndLaesstPunkteUnveraendert()
    {
        var svc = MakeService();
        var incoming = new List<SurveyPoint> { At(0, 0), At(5, 0) };
        var before = incoming.Select(Local).ToList();

        var result = svc.Register([], incoming);

        result.Applied.Should().BeFalse();
        result.Rejection.Should().Be(SessionRegistrationRejection.NoExistingPoints);
        result.Points.Select(Local).Should().BeEquivalentTo(before);
    }

    [Fact]
    public void ReineVerschiebung_WirdZurueckgezogen()
    {
        // Bestand: zwei Punkte. Neue Sitzung: dieselben Stellen, aber um (1,5 | -0,8) m versetzt
        // (typischer Anker-Fehler). Nach der Einpassung muessen sie wieder aufeinander liegen.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0), At(10, 0) };
        var incoming = new List<SurveyPoint> { At(1.5, -0.8), At(11.5, -0.8) };

        var result = svc.Register(existing, incoming);

        result.Applied.Should().BeTrue();
        result.PairCount.Should().Be(2);
        result.TranslationMeters.Should().BeApproximately(1.7, 0.1); // sqrt(1.5² + 0.8²)
        result.ResidualRmsMeters.Should().BeLessThan(0.05);

        var p0 = Local(result.Points[0]);
        p0.x.Should().BeApproximately(0, 0.05);
        p0.y.Should().BeApproximately(0, 0.05);
    }

    [Fact]
    public void VerdrehungUndVerschiebung_WerdenBeideKorrigiert()
    {
        // Heading-Fehler von 4° plus 1 m Versatz — der klassische Folge-Sitzungs-Fall.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0), At(20, 0), At(20, 15) };

        const double deg = 4.0;
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var incoming = new List<SurveyPoint>();
        foreach (var (x, y) in new[] { (0.0, 0.0), (20.0, 0.0), (20.0, 15.0) })
        {
            // Vorwaerts drehen + verschieben; die Einpassung muss das umkehren.
            incoming.Add(At(x * cos - y * sin + 1.0, x * sin + y * cos + 1.0));
        }

        var result = svc.Register(existing, incoming);

        result.Applied.Should().BeTrue();
        result.PairCount.Should().Be(3);
        result.RotationDeg.Should().BeApproximately(-deg, 0.3); // Rueckdrehung
        result.ResidualRmsMeters.Should().BeLessThan(0.05);

        for (var i = 0; i < 3; i++)
        {
            var expected = Local(existing[i]);
            var actual = Local(result.Points[i]);
            actual.x.Should().BeApproximately(expected.x, 0.05);
            actual.y.Should().BeApproximately(expected.y, 0.05);
        }
    }

    [Fact]
    public void NichtGepaarteNeuePunkte_WerdenMitTransformiert()
    {
        // DER eigentliche Zweck: zwei Passpunkte definieren die Transformation, ein dritter
        // NEUER Punkt (ohne Partner im Bestand) muss dieselbe Korrektur erfahren.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0), At(10, 0) };
        var incoming = new List<SurveyPoint>
        {
            At(2.0, 0),      // Passpunkt zu (0,0)
            At(12.0, 0),     // Passpunkt zu (10,0)
            At(50.0, 30.0),  // frisch gemessen, kein Partner — liegt weit weg
        };

        var result = svc.Register(existing, incoming);

        result.Applied.Should().BeTrue();
        result.PairCount.Should().Be(2);

        // Der neue Punkt muss um dieselben -2 m in X verschoben worden sein.
        var moved = Local(result.Points[2]);
        moved.x.Should().BeApproximately(48.0, 0.1);
        moved.y.Should().BeApproximately(30.0, 0.1);
    }

    [Fact]
    public void ZuWenigePasspunkte_LehntAb()
    {
        // Nur ein Punkt in Reichweite → Rotation unbestimmt, also ablehnen statt raten.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0) };
        var incoming = new List<SurveyPoint> { At(0.5, 0.5), At(80, 80) };

        var result = svc.Register(existing, incoming);

        result.Applied.Should().BeFalse();
        result.Rejection.Should().Be(SessionRegistrationRejection.TooFewPairs);
        result.PairCount.Should().Be(1);
    }

    [Fact]
    public void KeinPunktInReichweite_LehntAb()
    {
        // Zweite Messung an einer ganz anderen Ecke des Grundstuecks: keine
        // Wiederholungsmessung, also keine Einpassung — die Punkte bleiben, wo sie sind.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0), At(5, 0) };
        var incoming = new List<SurveyPoint> { At(100, 100), At(105, 100) };
        var before = incoming.Select(Local).ToList();

        var result = svc.Register(existing, incoming);

        result.Applied.Should().BeFalse();
        result.Rejection.Should().Be(SessionRegistrationRejection.TooFewPairs);
        result.Points.Select(Local).Should().BeEquivalentTo(before);
    }

    [Fact]
    public void WiderspruechlichePaare_LehntWegenRestklaffungAb()
    {
        // Zwei Paare, die eine unmoegliche Transformation fordern: die neuen Punkte liegen
        // 7,5 m auseinander, ihre Partner im Bestand 10 m. Ohne Massstabsfaktor laesst sich das
        // nicht erfuellen — die Restklaffung muss die Einpassung kippen, statt die Messung zu
        // verbiegen. Beide Paare liegen bewusst INNERHALB des 3-m-Suchradius (0,5 m bzw. 2,0 m),
        // sonst wuerde schon die Paarbildung ablehnen und der Fall waere nicht geprueft.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0), At(10, 0) };
        var incoming = new List<SurveyPoint> { At(0.5, 0), At(8.0, 0) };

        var result = svc.Register(existing, incoming);

        result.PairCount.Should().Be(2);
        result.Applied.Should().BeFalse();
        result.Rejection.Should().Be(SessionRegistrationRejection.ResidualTooLarge);
    }

    [Fact]
    public void PasspunkteAufDerselbenStelle_LehntWegenGeometrieAb()
    {
        // Beide Paare praktisch am selben Ort → aus ihnen ist keine Drehung ableitbar.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0), At(0.02, 0.02) };
        var incoming = new List<SurveyPoint> { At(0.5, 0.5), At(0.52, 0.52) };

        var result = svc.Register(existing, incoming);

        result.Applied.Should().BeFalse();
        result.Rejection.Should().Be(SessionRegistrationRejection.DegenerateGeometry);
    }

    [Fact]
    public void Hoehen_BleibenUnangetastet()
    {
        // Die Einpassung korrigiert nur die Lage. Der Hoehenbezug kommt aus Anker-Hoehe +
        // Boden-Offset und wird hier bewusst nicht verbogen.
        var svc = MakeService();
        var existing = new List<SurveyPoint> { At(0, 0), At(10, 0) };
        var incoming = new List<SurveyPoint> { At(1, 0), At(11, 0) };
        incoming[0].Altitude = 517.5;
        incoming[1].Altitude = 518.25;

        var result = svc.Register(existing, incoming);

        result.Applied.Should().BeTrue();
        result.Points[0].Altitude.Should().Be(517.5);
        result.Points[1].Altitude.Should().Be(518.25);
    }
}
