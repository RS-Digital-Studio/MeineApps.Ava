using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;

namespace SunSeeker.Android.Services;

/// <summary>
/// Positions-Provider auf Basis des nativen <see cref="LocationManager"/> (kein Google Play
/// Services). Fuer die Sonnenstandsberechnung genuegt grobe Genauigkeit (der Sonnen-Azimut
/// aendert sich ueber Kilometer vernachlaessigbar), daher GPS + Network-Provider ohne
/// FusedLocation. Erbt von <see cref="Java.Lang.Object"/>, weil <see cref="ILocationListener"/>
/// ein Java-Interface ist.
/// </summary>
public sealed class AndroidLocationService : Java.Lang.Object, ILocationService, ILocationListener
{
    private readonly LocationManager? _locationManager;
    private GeoLocation? _current;

    public AndroidLocationService(Context context)
    {
        _locationManager = context.GetSystemService(Context.LocationService) as LocationManager;
        var last = GetBestLastKnown();
        if (last != null)
            _current = ToGeoLocation(last);
    }

    public GeoLocation? Current => _current;

    public bool IsAvailable => _locationManager != null;

    public event EventHandler<GeoLocation>? LocationChanged;

    public Task<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_current);

    public void Start()
    {
        if (_locationManager == null) return;
        try
        {
            if (_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
                _locationManager.RequestLocationUpdates(LocationManager.GpsProvider, 5000, 25, this);
            if (_locationManager.IsProviderEnabled(LocationManager.NetworkProvider))
                _locationManager.RequestLocationUpdates(LocationManager.NetworkProvider, 5000, 25, this);
        }
        catch (Java.Lang.SecurityException)
        {
            // Permission noch nicht erteilt — Start wird nach Permission-Grant erneut aufgerufen.
        }
    }

    public void Stop()
    {
        try { _locationManager?.RemoveUpdates(this); }
        catch (Exception) { /* ignorieren */ }
    }

    public void OnLocationChanged(Location location)
    {
        var loc = ToGeoLocation(location);
        _current = loc;
        LocationChanged?.Invoke(this, loc);
    }

    public void OnProviderEnabled(string provider) { }

    public void OnProviderDisabled(string provider) { }

    public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras) { }

    private Location? GetBestLastKnown()
    {
        if (_locationManager == null) return null;
        try
        {
            Location? best = null;
            foreach (var provider in _locationManager.GetProviders(true))
            {
                var loc = _locationManager.GetLastKnownLocation(provider);
                if (loc != null && (best == null || loc.Time > best.Time))
                    best = loc;
            }
            return best;
        }
        catch (Java.Lang.SecurityException)
        {
            return null;
        }
    }

    private static GeoLocation ToGeoLocation(Location l)
        => new(l.Latitude, l.Longitude, l.HasAltitude ? l.Altitude : 0);
}
