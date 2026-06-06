using SunSeeker.Shared.Models;
using static SunSeeker.Shared.Services.SunMath;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Sonnenstandsberechnung nach dem NOAA Solar Calculator (Algorithmus basiert auf Jean Meeus,
/// "Astronomical Algorithms"). Genauigkeit der Sonnenposition besser als ~0,01 Grad für die
/// Jahre 1800-2100 — weit genauer als für eine Panel-Ausrichtung nötig. Vollständig offline.
///
/// Schritte: Julianisches Datum -> Julianisches Jahrhundert -> geometrische mittlere Länge und
/// Anomalie der Sonne -> Mittelpunktsgleichung -> wahre/scheinbare Länge -> Schiefe der Ekliptik
/// -> Deklination + Zeitgleichung -> wahre Ortszeit -> Stundenwinkel -> Zenit/Elevation/Azimut.
/// </summary>
public sealed class SolarPositionService : ISolarPositionService
{
    /// <summary>Geometrischer Zenitwinkel des Sonnenmittelpunkts bei Auf-/Untergang
    /// (90 Grad + 50 Bogenminuten für Refraktion + scheinbaren Sonnenradius).</summary>
    private const double SunriseZenith = 90.833;

    public SolarPosition GetPosition(GeoLocation location, DateTime utc)
    {
        var u = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();

        var jd = ToJulianDay(u);
        var t = JulianCentury(jd);

        var decl = SunDeclination(t);          // Grad
        var eqTime = EquationOfTime(t);        // Minuten

        // Wahre Sonnenzeit (Minuten) aus der UTC-Tageszeit, korrigiert um Zeitgleichung
        // und Längengrad (4 Minuten pro Grad östlich).
        var utcMinutes = u.TimeOfDay.TotalMinutes;
        var trueSolarTime = Mod(utcMinutes + eqTime + 4.0 * location.Longitude, 1440.0);

        // Stundenwinkel: 0 zu Sonnenmittag, +15 Grad pro Stunde nachmittags.
        var hourAngle = trueSolarTime / 4.0 - 180.0;

        var latRad = Deg2Rad(location.Latitude);
        var declRad = Deg2Rad(decl);
        var haRad = Deg2Rad(hourAngle);

        var cosZenith = Math.Sin(latRad) * Math.Sin(declRad)
                      + Math.Cos(latRad) * Math.Cos(declRad) * Math.Cos(haRad);
        cosZenith = Math.Clamp(cosZenith, -1.0, 1.0);
        var zenith = Rad2Deg(Math.Acos(cosZenith));

        var elevation = 90.0 - zenith;
        var elevationCorrected = elevation + AtmosphericRefraction(elevation);

        var azimuth = SolarAzimuth(latRad, zenith, decl, hourAngle);

        return new SolarPosition(azimuth, elevationCorrected, u);
    }

    public SunTimes GetSunTimes(GeoLocation location, DateOnly date)
    {
        // Deklination + Zeitgleichung bei (ungefährem) Sonnenmittag berechnen — eine
        // Iteration genügt für Genauigkeit deutlich unter einer Minute.
        var noonGuessUtc = new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);
        var t = JulianCentury(ToJulianDay(noonGuessUtc));

        var decl = SunDeclination(t);
        var eqTime = EquationOfTime(t);

        // Sonnenmittag (UTC, Minuten): 720 - 4*lon - eqTime.
        var solarNoonMin = 720.0 - 4.0 * location.Longitude - eqTime;
        var solarNoonUtc = StartOfDayUtc(date).AddMinutes(solarNoonMin);

        var latRad = Deg2Rad(location.Latitude);
        var declRad = Deg2Rad(decl);

        // Halber Tagbogen (Grad) bis zum Auf-/Untergang.
        var cosH = (Math.Cos(Deg2Rad(SunriseZenith)) - Math.Sin(latRad) * Math.Sin(declRad))
                 / (Math.Cos(latRad) * Math.Cos(declRad));

        var noonElevation = 90.0 - Math.Abs(location.Latitude - decl);

        if (cosH < -1.0)
        {
            // Sonne geht nicht unter (Polartag).
            return new SunTimes(null, null, solarNoonUtc, noonElevation, PolarDay: true, PolarNight: false);
        }
        if (cosH > 1.0)
        {
            // Sonne geht nicht auf (Polarnacht).
            return new SunTimes(null, null, solarNoonUtc, noonElevation, PolarDay: false, PolarNight: true);
        }

        var haMinutes = Rad2Deg(Math.Acos(Math.Clamp(cosH, -1.0, 1.0))) * 4.0;
        var sunrise = solarNoonUtc.AddMinutes(-haMinutes);
        var sunset = solarNoonUtc.AddMinutes(haMinutes);

        return new SunTimes(sunrise, sunset, solarNoonUtc, noonElevation, PolarDay: false, PolarNight: false);
    }

    public IReadOnlyList<SolarPosition> GetDayArc(GeoLocation location, DateOnly date, int stepMinutes = 10)
    {
        if (stepMinutes < 1) stepMinutes = 1;

        var start = StartOfDayUtc(date);
        var result = new List<SolarPosition>(1440 / stepMinutes + 1);

        for (var minute = 0; minute <= 1440; minute += stepMinutes)
            result.Add(GetPosition(location, start.AddMinutes(minute)));

        return result;
    }

    // ── NOAA-Teilformeln ────────────────────────────────────────────────

    private static double JulianCentury(double jd) => (jd - 2451545.0) / 36525.0;

    /// <summary>Sonnen-Deklination (Grad).</summary>
    private static double SunDeclination(double t)
    {
        var lambda = SunApparentLongitude(t);
        var obliquity = ObliquityCorrection(t);
        var sinDecl = Math.Sin(Deg2Rad(obliquity)) * Math.Sin(Deg2Rad(lambda));
        return Rad2Deg(Math.Asin(Math.Clamp(sinDecl, -1.0, 1.0)));
    }

    /// <summary>Geometrische mittlere Länge der Sonne (Grad, 0..360).</summary>
    private static double GeomMeanLongSun(double t)
        => Normalize360(280.46646 + t * (36000.76983 + t * 0.0003032));

    /// <summary>Geometrische mittlere Anomalie der Sonne (Grad).</summary>
    private static double GeomMeanAnomalySun(double t)
        => 357.52911 + t * (35999.05029 - 0.0001537 * t);

    /// <summary>Exzentrizitaet der Erdumlaufbahn (dimensionslos).</summary>
    private static double EccentricityEarthOrbit(double t)
        => 0.016708634 - t * (0.000042037 + 0.0000001267 * t);

    /// <summary>Mittelpunktsgleichung der Sonne (Grad).</summary>
    private static double SunEquationOfCenter(double t)
    {
        var mRad = Deg2Rad(GeomMeanAnomalySun(t));
        return Math.Sin(mRad) * (1.914602 - t * (0.004817 + 0.000014 * t))
             + Math.Sin(2 * mRad) * (0.019993 - 0.000101 * t)
             + Math.Sin(3 * mRad) * 0.000289;
    }

    /// <summary>Wahre Länge der Sonne (Grad).</summary>
    private static double SunTrueLongitude(double t) => GeomMeanLongSun(t) + SunEquationOfCenter(t);

    /// <summary>Scheinbare Länge der Sonne (Grad), korrigiert um Nutation/Aberration.</summary>
    private static double SunApparentLongitude(double t)
    {
        var omega = 125.04 - 1934.136 * t;
        return SunTrueLongitude(t) - 0.00569 - 0.00478 * Math.Sin(Deg2Rad(omega));
    }

    /// <summary>Mittlere Schiefe der Ekliptik (Grad).</summary>
    private static double MeanObliquityOfEcliptic(double t)
    {
        var seconds = 21.448 - t * (46.8150 + t * (0.00059 - t * 0.001813));
        return 23.0 + (26.0 + seconds / 60.0) / 60.0;
    }

    /// <summary>Korrigierte Schiefe der Ekliptik (Grad).</summary>
    private static double ObliquityCorrection(double t)
    {
        var omega = 125.04 - 1934.136 * t;
        return MeanObliquityOfEcliptic(t) + 0.00256 * Math.Cos(Deg2Rad(omega));
    }

    /// <summary>Zeitgleichung (Minuten) — Differenz zwischen wahrer und mittlerer Sonnenzeit.</summary>
    private static double EquationOfTime(double t)
    {
        var epsilon = ObliquityCorrection(t);
        var l0 = GeomMeanLongSun(t);
        var e = EccentricityEarthOrbit(t);
        var m = GeomMeanAnomalySun(t);

        var y = Math.Tan(Deg2Rad(epsilon / 2.0));
        y *= y;

        var l0Rad = Deg2Rad(l0);
        var mRad = Deg2Rad(m);

        var eTime = y * Math.Sin(2 * l0Rad)
                  - 2 * e * Math.Sin(mRad)
                  + 4 * e * y * Math.Sin(mRad) * Math.Cos(2 * l0Rad)
                  - 0.5 * y * y * Math.Sin(4 * l0Rad)
                  - 1.25 * e * e * Math.Sin(2 * mRad);

        return 4.0 * Rad2Deg(eTime); // Bogenmass -> Grad -> Minuten (4 min/Grad)
    }

    /// <summary>Sonnen-Azimut (Grad, im Uhrzeigersinn von Nord) nach der NOAA-Formel.</summary>
    private static double SolarAzimuth(double latRad, double zenith, double decl, double hourAngle)
    {
        var zenithRad = Deg2Rad(zenith);
        var declRad = Deg2Rad(decl);

        var denom = Math.Cos(latRad) * Math.Sin(zenithRad);
        if (Math.Abs(denom) < 1e-9)
            return latRad >= 0 ? 180.0 : 0.0; // Sonne im Zenit/Pol-Sonderfall

        var arg = (Math.Sin(latRad) * Math.Cos(zenithRad) - Math.Sin(declRad)) / denom;
        arg = Math.Clamp(arg, -1.0, 1.0);
        var azCore = Rad2Deg(Math.Acos(arg));

        return hourAngle > 0
            ? Normalize360(azCore + 180.0)
            : Normalize360(540.0 - azCore);
    }

    /// <summary>Atmosphärische Refraktion (Grad) als Funktion der scheinbaren Elevation.
    /// NOAA-Näherung; nahe dem Horizont am größten (~0,57 Grad).</summary>
    private static double AtmosphericRefraction(double elevationDeg)
    {
        if (elevationDeg > 85.0) return 0.0;

        var te = Math.Tan(Deg2Rad(elevationDeg));
        double refractionArcsec;

        if (elevationDeg > 5.0)
            refractionArcsec = 58.1 / te - 0.07 / (te * te * te) + 0.000086 / Math.Pow(te, 5);
        else if (elevationDeg > -0.575)
            refractionArcsec = 1735.0
                + elevationDeg * (-518.2 + elevationDeg * (103.4 + elevationDeg * (-12.79 + elevationDeg * 0.711)));
        else
            refractionArcsec = -20.772 / te;

        return refractionArcsec / 3600.0;
    }

    private static double Mod(double value, double modulus)
    {
        var r = value % modulus;
        return r < 0 ? r + modulus : r;
    }

    private static DateTime StartOfDayUtc(DateOnly date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
}
