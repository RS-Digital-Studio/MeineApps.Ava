using SunSeeker.Shared.Models;
using static SunSeeker.Shared.Services.SunMath;

namespace SunSeeker.Shared.Services;

/// <summary>
/// Berechnet Soll-Ausrichtung und Live-Einfallswinkel. Die Festwinkel-Faustformeln
/// (Jahres-/Winter-Optimum) stammen aus solarpaneltilt.com und gelten fuer Breiten 25-50 Grad;
/// das Jahres-Optimum liegt bewusst FLACHER als der Breitengrad, weil im Winter Diffuslicht
/// dominiert. Der Einfallswinkel folgt der Standard-PVPMC/Sandia-Formel
/// (cos(AOI) = sin(elev)*cos(tilt) + cos(elev)*sin(tilt)*cos(sonnenAz - panelAz)).
/// </summary>
public sealed class AlignmentService(ISolarPositionService solarPosition) : IAlignmentService
{
    private readonly ISolarPositionService _solarPosition = solarPosition;

    public AlignmentRecommendation GetRecommendation(GeoLocation location, DateTime utcNow, AlignmentGoal goal, PanelProfile panel)
    {
        // Optimale Himmelsrichtung: aequatorwaerts (Sued auf der Nord-, Nord auf der Suedhalbkugel).
        var southAzimuth = location.IsNorthernHemisphere ? 180.0 : 0.0;
        var absLat = Math.Abs(location.Latitude);

        double targetAzimuth;
        double targetTilt;

        switch (goal)
        {
            case AlignmentGoal.NowMaximum:
            {
                var sun = _solarPosition.GetPosition(location, utcNow);
                if (sun.IsDaylight)
                {
                    targetAzimuth = sun.Azimuth;
                    targetTilt = Math.Clamp(sun.Zenith, 0.0, 90.0);
                }
                else
                {
                    // Sonne unter dem Horizont: auf den naechsten Sonnen-Hoechststand zeigen.
                    targetAzimuth = southAzimuth;
                    targetTilt = Math.Clamp(absLat, 0.0, 90.0);
                }
                break;
            }

            case AlignmentGoal.TodayYield:
            {
                var date = DateOnly.FromDateTime(utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime());
                var times = _solarPosition.GetSunTimes(location, date);
                targetAzimuth = southAzimuth;
                targetTilt = Math.Clamp(90.0 - times.NoonElevation, 0.0, 90.0);
                break;
            }

            case AlignmentGoal.WinterYield:
                targetAzimuth = southAzimuth;
                targetTilt = Math.Clamp(absLat * 0.875 + 19.2, 0.0, 90.0);
                break;

            case AlignmentGoal.AnnualYield:
            default:
                targetAzimuth = southAzimuth;
                targetTilt = Math.Clamp(absLat * 0.76 + 3.1, 0.0, 90.0);
                break;
        }

        var kickstand = panel.NearestKickstand(targetTilt);
        return new AlignmentRecommendation(goal, targetAzimuth, targetTilt, kickstand);
    }

    public AlignmentState Evaluate(SolarPosition sun, double panelAzimuth, double panelTilt, AlignmentRecommendation recommendation)
    {
        var elevRad = Deg2Rad(sun.Elevation);
        var tiltRad = Deg2Rad(panelTilt);
        var deltaAz = Deg2Rad(sun.Azimuth - panelAzimuth);

        var cosAoi = Math.Sin(elevRad) * Math.Cos(tiltRad)
                   + Math.Cos(elevRad) * Math.Sin(tiltRad) * Math.Cos(deltaAz);
        cosAoi = Math.Clamp(cosAoi, -1.0, 1.0);

        var aoi = Rad2Deg(Math.Acos(cosAoi));
        var sunBehind = cosAoi < 0.0 || !sun.IsDaylight;
        var directGain = sun.IsDaylight ? Math.Max(0.0, cosAoi) : 0.0;

        var azimuthError = NormalizeSigned(panelAzimuth - recommendation.TargetAzimuth);
        var tiltError = panelTilt - recommendation.TargetTilt;

        var quality = ClassifyQuality(azimuthError, tiltError);

        return new AlignmentState(
            panelAzimuth, panelTilt, azimuthError, tiltError,
            aoi, directGain, sunBehind, quality);
    }

    /// <summary>
    /// Ampel aus der Abweichung von Soll. Azimut-Fehler wird gegenueber Tilt-Fehler abgewertet
    /// (Faktor 0,6), weil der Ertrag laut Literatur deutlich toleranter gegen Azimut- als gegen
    /// Tilt-Abweichungen ist.
    /// </summary>
    private static AlignmentQuality ClassifyQuality(double azimuthError, double tiltError)
    {
        var weighted = Math.Sqrt(0.6 * azimuthError * azimuthError + tiltError * tiltError);
        return weighted switch
        {
            < 5.0 => AlignmentQuality.Excellent,
            < 12.0 => AlignmentQuality.Good,
            < 25.0 => AlignmentQuality.Fair,
            _ => AlignmentQuality.Poor,
        };
    }
}
